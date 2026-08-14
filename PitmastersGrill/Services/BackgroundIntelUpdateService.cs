using Microsoft.Data.Sqlite;
using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersGrill.Services
{
    public partial class BackgroundIntelUpdateService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(10);
        private const string ForegroundFreshnessBusyMessage = "Another freshness operation is already running.";

        private readonly object _sync = new();
        private readonly SemaphoreSlim _wakeSignal = new(0, 1);
        private readonly SemaphoreSlim _foregroundFreshnessOperationGate = new(1, 1);
        private readonly KillmailDatasetFreshnessService _freshnessService;
        private readonly KillmailDbWriteGate _writeGate;
        private readonly KillmailDayImportService _killmailDayImportService;
        private readonly KillmailDatasetMetadataRepository _metadataRepository;
        private readonly R2Z2LiveKillmailService _r2z2LiveKillmailService;
        private readonly TodaysFreshnessService _todaysFreshnessService;
        private readonly HistoricalFreshnessService _historicalFreshnessService;
        private Task? _backgroundHistoricalRepairTask;

        private Task? _backgroundTask;
        private readonly CancellationTokenSource _shutdownCts = new();

        private string _currentImportDayUtc = "";
        private string _lastError = "";
        private string _notPublishedBoundaryDayUtc = "";
        private bool _isRunning;
        private int _foregroundPriorityRequests;
        private int _totalDaysInCurrentRun;
        private int _completedDaysInCurrentRun;

        public event Action<IntelUpdateStatusSnapshot>? StatusChanged;

        public BackgroundIntelUpdateService(
            KillmailDatasetFreshnessService freshnessService,
            KillmailDbWriteGate writeGate,
            KillmailDayImportService killmailDayImportService,
            R2Z2LiveKillmailService r2z2LiveKillmailService,
            TodaysFreshnessService todaysFreshnessService,
            HistoricalFreshnessService historicalFreshnessService)
        {
            _freshnessService = freshnessService;
            _writeGate = writeGate ?? throw new ArgumentNullException(nameof(writeGate));
            _killmailDayImportService = killmailDayImportService;
            _metadataRepository = new KillmailDatasetMetadataRepository(KillmailPaths.GetKillmailDatabasePath());
            _r2z2LiveKillmailService = r2z2LiveKillmailService;
            _todaysFreshnessService = todaysFreshnessService;
            _historicalFreshnessService = historicalFreshnessService;
            _r2z2LiveKillmailService.StatusChanged += OnLiveFeedStatusChanged;
            _todaysFreshnessService.StatusChanged += OnTodaysFreshnessStatusChanged;
            _historicalFreshnessService.StatusChanged += OnHistoricalFreshnessStatusChanged;
        }

        public IntelUpdateStatusSnapshot GetSnapshot()
        {
            lock (_sync)
            {
                return BuildSnapshot();
            }
        }

        public void StartIfNeeded()
        {
            lock (_sync)
            {
                if (_backgroundTask == null || _backgroundTask.IsCompleted)
                {
                    AppLogger.KillmailImportInfo("Archive background worker starting.");
                    _backgroundTask = Task.Run(() => RunLoopAsync(_shutdownCts.Token));
                }

                PublishLocked();
            }

            ReleaseWakeSignal();
        }

        public Task SetLiveFeedEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        {
            return _r2z2LiveKillmailService.SetEnabledAsync(enabled, cancellationToken);
        }

        public async Task<TodaysFreshnessRunResult> RunTodaysFreshnessAsync(IReadOnlyCollection<long> characterIds, CancellationToken cancellationToken = default)
        {
            if (!await _foregroundFreshnessOperationGate.WaitAsync(0, cancellationToken))
            {
                AppLogger.KillmailImportInfo("Today's Freshness start skipped because another foreground freshness operation is already active.");
                return CreateForegroundFreshnessBusyTodaysResult(characterIds);
            }

            try
            {
                return await _todaysFreshnessService.RunAsync(characterIds, cancellationToken);
            }
            finally
            {
                _foregroundFreshnessOperationGate.Release();
            }
        }

        public async Task<HistoricalFreshnessRunResult> RunHistoricalFreshnessAsync(IReadOnlyCollection<long> characterIds, CancellationToken cancellationToken = default)
        {
            if (!await _foregroundFreshnessOperationGate.WaitAsync(0, cancellationToken))
            {
                AppLogger.KillmailImportInfo("Historical Freshness start skipped because another foreground freshness operation is already active.");
                return CreateForegroundFreshnessBusyHistoricalResult(characterIds);
            }

            try
            {
                return await _historicalFreshnessService.RunAsync(characterIds, cancellationToken);
            }
            finally
            {
                _foregroundFreshnessOperationGate.Release();
            }
        }

        public void ScheduleBackgroundHistoricalRepairAfterUiShown(Func<IReadOnlyCollection<long>> visibleCharacterIdsProvider)
        {
            if (visibleCharacterIdsProvider == null)
            {
                throw new ArgumentNullException(nameof(visibleCharacterIdsProvider));
            }

            lock (_sync)
            {
                if (_backgroundHistoricalRepairTask != null && !_backgroundHistoricalRepairTask.IsCompleted)
                {
                    AppLogger.KillmailImportInfo("Background historical repair startup scheduling skipped because a schedule is already active.");
                    return;
                }

                var configuration = _historicalFreshnessService.GetBackgroundStartupConfiguration();
                AppLogger.KillmailImportInfo(
                    $"Background historical repair startup configuration evaluated. enabled={configuration.Enabled} delaySeconds={configuration.DelaySeconds} cooldownHours={configuration.CooldownHours} lookbackDays={configuration.LookbackDays} maxPilots={configuration.MaxPilotsPerRun} recentPilotWindowDays={configuration.RecentPilotWindowDays}");

                if (!configuration.Enabled)
                {
                    AppLogger.KillmailImportInfo("Background historical repair startup skipped because AppSettings disabled it.");
                    return;
                }

                AppLogger.KillmailImportInfo(
                    $"Background historical repair scheduled after UI shown. delaySeconds={configuration.DelaySeconds}");

                _backgroundHistoricalRepairTask = Task.Run(async () =>
                {
                    try
                    {
                        if (configuration.DelaySeconds > 0)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(configuration.DelaySeconds), _shutdownCts.Token);
                        }

                        if (_shutdownCts.IsCancellationRequested)
                        {
                            return;
                        }

                        await WaitForForegroundPriorityToClearAsync(_shutdownCts.Token);

                        IReadOnlyCollection<long> visibleCharacterIds;
                        try
                        {
                            visibleCharacterIds = visibleCharacterIdsProvider() ?? Array.Empty<long>();
                        }
                        catch (Exception ex)
                        {
                            AppLogger.KillmailImportWarn($"Background historical repair could not read visible pilot IDs. message={ex.Message}");
                            AppLogger.ErrorOnly("Background historical repair visible pilot provider failed.", ex);
                            visibleCharacterIds = Array.Empty<long>();
                        }

                        AppLogger.KillmailImportInfo(
                            $"Background historical repair starting after UI shown. visiblePilotCount={visibleCharacterIds.Count}");

                        var result = await _historicalFreshnessService.RunBackgroundStartupRepairAsync(
                            visibleCharacterIds,
                            _shutdownCts.Token);

                        if (result.CandidatePilotsConsidered == 0)
                        {
                            AppLogger.KillmailImportInfo("Background historical repair skipped because no candidates were available.");
                        }
                        else if (result.PilotsChecked == 0 && result.CandidatePilotsSkippedCooldown > 0)
                        {
                            AppLogger.KillmailImportInfo(
                                $"Background historical repair skipped because all candidates were in cooldown. considered={result.CandidatePilotsConsidered} cooldownSkipped={result.CandidatePilotsSkippedCooldown}");
                        }
                        else if (!result.Success &&
                                 result.FailedCount > 0 &&
                                 string.Equals(result.DetailText, $"Background historical repair stopped after zKill rate limiting while checking pilot {result.PilotsChecked} of {result.CandidatePilotsConsidered}.", StringComparison.Ordinal))
                        {
                            AppLogger.KillmailImportInfo(
                                $"Background historical repair rate-limited and exited. pilotsChecked={result.PilotsChecked} failed={result.FailedCount} detail='{result.DetailText}'");
                        }

                        AppLogger.KillmailImportInfo(
                            $"Background historical repair completed. candidatePilots={result.CandidatePilotsConsidered} skippedCooldown={result.CandidatePilotsSkippedCooldown} pilotsChecked={result.PilotsChecked} imported={result.MissingImportedCount} failed={result.FailedCount} detail='{result.DetailText}'");
                    }
                    catch (OperationCanceledException)
                    {
                        AppLogger.KillmailImportInfo("Background historical repair cancelled.");
                    }
                    catch (Exception ex)
                    {
                        AppLogger.KillmailImportWarn($"Background historical repair failed. message={ex.Message}");
                        AppLogger.ErrorOnly("Background historical repair exception.", ex);
                    }
                }, _shutdownCts.Token);
            }
        }

        public async Task EnableKillmailDbPullAsync(int lookbackDays, CancellationToken cancellationToken = default)
        {
            var normalizedLookbackDays = KillmailDatasetFreshnessService.NormalizeMaxKillmailAgeDays(lookbackDays);
            var requiredThroughDay = DateTime.UtcNow.Date.AddDays(-1);
            var bootstrapStartDayUtc = KillmailDatasetFreshnessService.BuildBootstrapStartDayUtc(DateTime.UtcNow, normalizedLookbackDays);

            AppLogger.KillmailImportInfo(
                $"Killmail DB pull requested. requestedHistoryDays={lookbackDays} normalizedHistoryDays={normalizedLookbackDays} startDay={bootstrapStartDayUtc} endDay={requiredThroughDay:yyyy-MM-dd} plannedArchiveDays={normalizedLookbackDays}");

            lock (_sync)
            {
                _currentImportDayUtc = bootstrapStartDayUtc;
                _lastError = "";
                _notPublishedBoundaryDayUtc = "";
                _isRunning = true;
                _totalDaysInCurrentRun = normalizedLookbackDays;
                _completedDaysInCurrentRun = 0;
                PublishLocked();
            }

            try
            {
                await Task.Run(
                    () => ResetLocalKillmailDerivedState(bootstrapStartDayUtc, cancellationToken),
                    cancellationToken);
            }
            catch
            {
                lock (_sync)
                {
                    _currentImportDayUtc = "";
                    _isRunning = false;
                    PublishLocked();
                }

                throw;
            }

            lock (_sync)
            {
                _currentImportDayUtc = bootstrapStartDayUtc;
                _lastError = "";
                _notPublishedBoundaryDayUtc = "";
                _isRunning = true;
                _totalDaysInCurrentRun = normalizedLookbackDays;
                _completedDaysInCurrentRun = 0;
                PublishLocked();
            }

            StartIfNeeded();
        }

        public IDisposable BeginForegroundPriority()
        {
            Interlocked.Increment(ref _foregroundPriorityRequests);
            Publish();

            return new ForegroundPriorityHandle(this);
        }

        public void Stop()
        {
            try
            {
                AppLogger.KillmailImportInfo("Background intel update service stop requested.");
                _shutdownCts.Cancel();
                _r2z2LiveKillmailService.Stop();
            }
            catch
            {
            }
        }

        private void EndForegroundPriority()
        {
            var updated = Interlocked.Decrement(ref _foregroundPriorityRequests);
            if (updated < 0)
            {
                Interlocked.Exchange(ref _foregroundPriorityRequests, 0);
            }

            Publish();
            ReleaseWakeSignal();
        }

        private async Task RunLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await WaitForForegroundPriorityToClearAsync(cancellationToken);

                    var freshness = _freshnessService.GetFreshnessStatus();
                    if ((freshness.IsCurrentThroughRequiredDay && freshness.IsRequestedCoverageComplete) || freshness.MissingDayCount <= 0)
                    {
                        lock (_sync)
                        {
                            _currentImportDayUtc = "";
                            _lastError = "";
                            _isRunning = false;
                            ResetProgressSessionLocked();
                            PublishLocked();
                        }

                        await WaitForWakeOrTimeoutAsync(cancellationToken);
                        continue;
                    }

                    var nextDayUtc = freshness.FirstMissingDayUtc;

                    lock (_sync)
                    {
                        InitializeOrAdvanceProgressSessionLocked(freshness, nextDayUtc);
                        _currentImportDayUtc = nextDayUtc;
                        _lastError = "";
                        _notPublishedBoundaryDayUtc = "";
                        _isRunning = true;
                        PublishLocked();
                    }

                    AppLogger.KillmailImportInfo(
                        $"Killmail day import attempt. day={nextDayUtc} requestedStart={freshness.RequestedStartDayUtc} requiredThrough={freshness.RequiredThroughDayUtc} localCoverageDays={freshness.LocalCoverageDays} requestedCoverageDays={freshness.RequestedCoverageDays} missingDays={freshness.MissingDayCount}");

                    var result = await _killmailDayImportService.ImportSingleDayAsync(
                        new KillmailRemoteDayInfo
                        {
                            DayUtc = nextDayUtc,
                            RemoteTotalCount = 0
                        },
                        cancellationToken);

                    if (result.ArchiveUnavailableNotPublishedYet)
                    {
                        AppLogger.KillmailImportWarn(
                            $"Killmail day import skipped. day={nextDayUtc} reason=archive-not-published boundaryDay={result.ArchiveUnavailableDayUtc}");

                        lock (_sync)
                        {
                            _notPublishedBoundaryDayUtc = result.ArchiveUnavailableDayUtc;
                            _lastError = "";
                            _currentImportDayUtc = "";
                            _isRunning = false;
                            ResetProgressSessionLocked();
                            PublishLocked();
                        }

                        await WaitForWakeOrTimeoutAsync(cancellationToken);
                        continue;
                    }

                    if (!result.Success)
                    {
                        AppLogger.KillmailImportWarn(
                            $"Killmail day import failed. day={nextDayUtc} reason={result.Error}");

                        lock (_sync)
                        {
                            _lastError = result.Error;
                            _currentImportDayUtc = "";
                            _isRunning = false;
                            ResetProgressSessionLocked();
                            PublishLocked();
                        }

                        await WaitForWakeOrTimeoutAsync(cancellationToken);
                        continue;
                    }

                    lock (_sync)
                    {
                        _completedDaysInCurrentRun = Math.Min(_completedDaysInCurrentRun + 1, _totalDaysInCurrentRun);
                        _currentImportDayUtc = "";
                        _lastError = "";
                        _notPublishedBoundaryDayUtc = "";
                        _isRunning = false;
                        PublishLocked();
                    }

                    var postImportFreshness = _freshnessService.GetFreshnessStatus();
                    AppLogger.KillmailImportInfo(
                        $"Killmail day import complete. day={nextDayUtc} importedKillmails={result.ImportedKillmailCount} oldestDay={postImportFreshness.EarliestCompleteDayUtc} newestDay={postImportFreshness.LatestCompleteDayUtc} localCoverageDays={postImportFreshness.LocalCoverageDays} requestedCoverageDays={postImportFreshness.RequestedCoverageDays} missingDays={postImportFreshness.MissingDayCount}");

                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    lock (_sync)
                    {
                        _lastError = ex.Message;
                        _currentImportDayUtc = "";
                        _isRunning = false;
                        ResetProgressSessionLocked();
                        PublishLocked();
                    }

                    try
                    {
                        await WaitForWakeOrTimeoutAsync(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            lock (_sync)
            {
                _currentImportDayUtc = "";
                _isRunning = false;
                ResetProgressSessionLocked();
                PublishLocked();
            }
        }

        private void ResetLocalKillmailDerivedState(string bootstrapStartDayUtc, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DebugTraceWriter.WriteLine("killmail reset start: clearing archive cache and derived state");
            KillmailPaths.ClearArchiveCacheBestEffort();

            cancellationToken.ThrowIfCancellationRequested();
            using var writeGate = _writeGate.Enter(
                $"killmail reset/reseed startDay={bootstrapStartDayUtc}",
                cancellationToken);

            var connectionString = $"Data Source={KillmailPaths.GetKillmailDatabasePath()}";
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();
            ExecuteNonQuery(connection, transaction, "DELETE FROM day_import_state;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM pilot_registry_day;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM pilot_fleet_observations_day;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM pilot_ship_observations_day;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM pilot_cyno_module_observations_day;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM pilot_bait_observations_day;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM pilot_cyno_tackle_observations_day;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM live_killmail_seen;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM live_killmail_feed_state;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM historical_freshness_checkpoint;");
            transaction.Commit();

            cancellationToken.ThrowIfCancellationRequested();

            AppLogger.KillmailImportInfo("Historical freshness checkpoints cleared during full killmail reset/reseed.");

            _metadataRepository.SetValue("latest_complete_day_utc", "");
            _metadataRepository.SetValue("last_successful_update_at_utc", "");
            _metadataRepository.SetValue("bootstrap_start_day_utc", bootstrapStartDayUtc);

            DebugTraceWriter.WriteLine(
                $"killmail reset complete: bootstrapStartDay={bootstrapStartDayUtc}");
        }

        private static void ExecuteNonQuery(SqliteConnection connection, SqliteTransaction transaction, string sql)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        private static TodaysFreshnessRunResult CreateForegroundFreshnessBusyTodaysResult(IReadOnlyCollection<long> characterIds)
        {
            return new TodaysFreshnessRunResult
            {
                Success = false,
                VisiblePilotsTargeted = characterIds?.Count ?? 0,
                LastError = ForegroundFreshnessBusyMessage,
                DetailText = ForegroundFreshnessBusyMessage
            };
        }

        private static HistoricalFreshnessRunResult CreateForegroundFreshnessBusyHistoricalResult(IReadOnlyCollection<long> characterIds)
        {
            return new HistoricalFreshnessRunResult
            {
                Success = false,
                Mode = "Manual",
                VisiblePilotsTargeted = characterIds?.Count ?? 0,
                CandidatePilotsConsidered = characterIds?.Count ?? 0,
                LastError = ForegroundFreshnessBusyMessage,
                DetailText = ForegroundFreshnessBusyMessage
            };
        }

        private async Task WaitForForegroundPriorityToClearAsync(CancellationToken cancellationToken)
        {
            while (Volatile.Read(ref _foregroundPriorityRequests) > 0)
            {
                await Task.Delay(200, cancellationToken);
            }
        }

        private async Task WaitForWakeOrTimeoutAsync(CancellationToken cancellationToken)
        {
            await _wakeSignal.WaitAsync(PollInterval, cancellationToken);
        }

        private void ReleaseWakeSignal()
        {
            if (_wakeSignal.CurrentCount == 0)
            {
                try
                {
                    _wakeSignal.Release();
                }
                catch
                {
                }
            }
        }

        private IntelUpdateStatusSnapshot BuildSnapshot()
        {
            var freshness = _freshnessService.GetFreshnessStatus();
            var liveFeedSnapshot = _r2z2LiveKillmailService.GetSnapshot();
            var todaysFreshnessSnapshot = _todaysFreshnessService.GetSnapshot();
            var historicalFreshnessSnapshot = _historicalFreshnessService.GetSnapshot();
            var foregroundActive = Volatile.Read(ref _foregroundPriorityRequests) > 0;
            var coverageDetail = BuildCoverageDetail(freshness);
            var lastSuccessfulUpdateAtUtc = _metadataRepository.GetValue("last_successful_update_at_utc") ?? "";
            var totalProgressIsIndeterminate = _isRunning
                ? _totalDaysInCurrentRun <= 0
                : false;
            var totalProgressPercent = _isRunning
                ? BuildTotalProgressPercent(_completedDaysInCurrentRun, _totalDaysInCurrentRun)
                : freshness.IsCurrentThroughRequiredDay
                    ? 100
                    : 0;
            var totalProgressText = BuildTotalProgressText(freshness, _isRunning, _completedDaysInCurrentRun, _totalDaysInCurrentRun);
            var currentDayProgressText = _isRunning
                ? "Progress details unavailable for this phase."
                : freshness.IsCurrentThroughRequiredDay
                    ? "No update currently running."
                    : "Waiting for the next local intel update pass.";

            if (!string.IsNullOrWhiteSpace(_lastError))
            {
                return new IntelUpdateStatusSnapshot
                {
                    IsRunning = false,
                    IsCurrentThroughYesterday = freshness.IsCurrentThroughRequiredDay && freshness.IsRequestedCoverageComplete,
                    HasError = true,
                    IsForegroundPriorityActive = foregroundActive,
                    EarliestCompleteDayUtc = freshness.EarliestCompleteDayUtc,
                    LatestCompleteDayUtc = freshness.LatestCompleteDayUtc,
                    RequiredThroughDayUtc = freshness.RequiredThroughDayUtc,
                    RequestedStartDayUtc = freshness.RequestedStartDayUtc,
                    CurrentImportDayUtc = _currentImportDayUtc,
                    LastSuccessfulUpdateAtUtc = lastSuccessfulUpdateAtUtc,
                    IsRequestedCoverageComplete = freshness.IsRequestedCoverageComplete,
                    HasRequestedCoverageWindow = freshness.HasRequestedCoverageWindow,
                    RequestedHistoryDays = freshness.RequestedHistoryDays,
                    RequestedCoverageDays = freshness.RequestedCoverageDays,
                    LocalCoverageDays = freshness.LocalCoverageDays,
                    MissingDayCount = freshness.MissingDayCount,
                    TotalDaysInCurrentRun = _totalDaysInCurrentRun,
                    CompletedDaysInCurrentRun = _completedDaysInCurrentRun,
                    StatusText = "LOCAL INTEL UPDATE FAILED",
                    DetailText = _lastError,
                    ErrorText = _lastError,
                    TotalProgressIsIndeterminate = totalProgressIsIndeterminate,
                    TotalProgressPercent = totalProgressPercent,
                    TotalProgressText = totalProgressText,
                    CurrentDayProgressIsIndeterminate = true,
                    CurrentDayProgressPercent = 0,
                    CurrentDayProgressText = currentDayProgressText,
                    LiveFeed = liveFeedSnapshot,
                    TodaysFreshness = todaysFreshnessSnapshot,
                    HistoricalFreshness = historicalFreshnessSnapshot
                };
            }

            if (!string.IsNullOrWhiteSpace(_notPublishedBoundaryDayUtc))
            {
                var isBlockedOnlyByUnpublishedBoundary = IsBlockedOnlyByUnpublishedBoundary(freshness, _notPublishedBoundaryDayUtc);
                var isCurrentThroughLatestPublishedArchive = freshness.IsRequestedCoverageComplete || isBlockedOnlyByUnpublishedBoundary;
                var notPublishedDetail = BuildLatestPublishedArchiveDetail(freshness, _notPublishedBoundaryDayUtc);

                return new IntelUpdateStatusSnapshot
                {
                    IsRunning = false,
                    IsCurrentThroughYesterday = isCurrentThroughLatestPublishedArchive,
                    HasError = false,
                    IsForegroundPriorityActive = foregroundActive,
                    EarliestCompleteDayUtc = freshness.EarliestCompleteDayUtc,
                    LatestCompleteDayUtc = freshness.LatestCompleteDayUtc,
                    RequiredThroughDayUtc = freshness.RequiredThroughDayUtc,
                    RequestedStartDayUtc = freshness.RequestedStartDayUtc,
                    CurrentImportDayUtc = "",
                    LastSuccessfulUpdateAtUtc = lastSuccessfulUpdateAtUtc,
                    IsRequestedCoverageComplete = isCurrentThroughLatestPublishedArchive,
                    HasRequestedCoverageWindow = freshness.HasRequestedCoverageWindow,
                    RequestedHistoryDays = freshness.RequestedHistoryDays,
                    RequestedCoverageDays = freshness.RequestedCoverageDays,
                    LocalCoverageDays = freshness.LocalCoverageDays,
                    MissingDayCount = 0,
                    TotalDaysInCurrentRun = 0,
                    CompletedDaysInCurrentRun = 0,
                    StatusText = isCurrentThroughLatestPublishedArchive
                        ? "LOCAL INTEL CURRENT — through latest published archive"
                        : "LOCAL INTEL PARTIALLY POPULATED — latest published archive reached",
                    DetailText = notPublishedDetail,
                    ErrorText = "",
                    TotalProgressIsIndeterminate = false,
                    TotalProgressPercent = 100,
                    TotalProgressText = $"Local killmail intel is current through the latest published archive. Waiting for archive day {_notPublishedBoundaryDayUtc} to publish.",
                    CurrentDayProgressIsIndeterminate = false,
                    CurrentDayProgressPercent = 0,
                    CurrentDayProgressText = $"Archive day {_notPublishedBoundaryDayUtc} is not published yet. PMG will retry automatically.",
                    LiveFeed = liveFeedSnapshot,
                    TodaysFreshness = todaysFreshnessSnapshot,
                    HistoricalFreshness = historicalFreshnessSnapshot
                };
            }

            if (_isRunning)
            {
                var detail = foregroundActive
                    ? "Foreground activity detected. Killmail intel updating will resume after the current clipboard/API work finishes."
                    : $"Updating killmail intel… Current day: {_currentImportDayUtc} • Remaining day(s): {freshness.MissingDayCount}";

                return new IntelUpdateStatusSnapshot
                {
                    IsRunning = true,
                    IsCurrentThroughYesterday = false,
                    HasError = false,
                    IsForegroundPriorityActive = foregroundActive,
                    EarliestCompleteDayUtc = freshness.EarliestCompleteDayUtc,
                    LatestCompleteDayUtc = freshness.LatestCompleteDayUtc,
                    RequiredThroughDayUtc = freshness.RequiredThroughDayUtc,
                    RequestedStartDayUtc = freshness.RequestedStartDayUtc,
                    CurrentImportDayUtc = _currentImportDayUtc,
                    LastSuccessfulUpdateAtUtc = lastSuccessfulUpdateAtUtc,
                    IsRequestedCoverageComplete = freshness.IsRequestedCoverageComplete,
                    HasRequestedCoverageWindow = freshness.HasRequestedCoverageWindow,
                    RequestedHistoryDays = freshness.RequestedHistoryDays,
                    RequestedCoverageDays = freshness.RequestedCoverageDays,
                    LocalCoverageDays = freshness.LocalCoverageDays,
                    MissingDayCount = freshness.MissingDayCount,
                    TotalDaysInCurrentRun = _totalDaysInCurrentRun,
                    CompletedDaysInCurrentRun = _completedDaysInCurrentRun,
                    StatusText = foregroundActive
                        ? "LOCAL INTEL UPDATE PAUSED FOR FOREGROUND ACTIVITY"
                        : "LOCAL INTEL STALE — updating in progress",
                    DetailText = detail,
                    ErrorText = "",
                    TotalProgressIsIndeterminate = totalProgressIsIndeterminate,
                    TotalProgressPercent = totalProgressPercent,
                    TotalProgressText = totalProgressText,
                    CurrentDayProgressIsIndeterminate = true,
                    CurrentDayProgressPercent = 0,
                    CurrentDayProgressText = currentDayProgressText,
                    LiveFeed = liveFeedSnapshot,
                    TodaysFreshness = todaysFreshnessSnapshot,
                    HistoricalFreshness = historicalFreshnessSnapshot
                };
            }

            if (freshness.IsCurrentThroughRequiredDay && freshness.IsRequestedCoverageComplete)
            {
                return new IntelUpdateStatusSnapshot
                {
                    IsRunning = false,
                    IsCurrentThroughYesterday = true,
                    HasError = false,
                    IsForegroundPriorityActive = foregroundActive,
                    EarliestCompleteDayUtc = freshness.EarliestCompleteDayUtc,
                    LatestCompleteDayUtc = freshness.LatestCompleteDayUtc,
                    RequiredThroughDayUtc = freshness.RequiredThroughDayUtc,
                    RequestedStartDayUtc = freshness.RequestedStartDayUtc,
                    CurrentImportDayUtc = "",
                    LastSuccessfulUpdateAtUtc = lastSuccessfulUpdateAtUtc,
                    IsRequestedCoverageComplete = true,
                    HasRequestedCoverageWindow = freshness.HasRequestedCoverageWindow,
                    RequestedHistoryDays = freshness.RequestedHistoryDays,
                    RequestedCoverageDays = freshness.RequestedCoverageDays,
                    LocalCoverageDays = freshness.LocalCoverageDays,
                    MissingDayCount = 0,
                    TotalDaysInCurrentRun = 0,
                    CompletedDaysInCurrentRun = 0,
                    StatusText = "LOCAL INTEL CURRENT — through yesterday",
                    DetailText = coverageDetail,
                    ErrorText = "",
                    TotalProgressIsIndeterminate = false,
                    TotalProgressPercent = 100,
                    TotalProgressText = "Local killmail intel is current through yesterday.",
                    CurrentDayProgressIsIndeterminate = false,
                    CurrentDayProgressPercent = 0,
                    CurrentDayProgressText = currentDayProgressText,
                    LiveFeed = liveFeedSnapshot,
                    TodaysFreshness = todaysFreshnessSnapshot,
                    HistoricalFreshness = historicalFreshnessSnapshot
                };
            }

            return new IntelUpdateStatusSnapshot
            {
                IsRunning = false,
                IsCurrentThroughYesterday = false,
                HasError = false,
                IsForegroundPriorityActive = foregroundActive,
                EarliestCompleteDayUtc = freshness.EarliestCompleteDayUtc,
                LatestCompleteDayUtc = freshness.LatestCompleteDayUtc,
                RequiredThroughDayUtc = freshness.RequiredThroughDayUtc,
                RequestedStartDayUtc = freshness.RequestedStartDayUtc,
                CurrentImportDayUtc = "",
                LastSuccessfulUpdateAtUtc = lastSuccessfulUpdateAtUtc,
                IsRequestedCoverageComplete = freshness.IsRequestedCoverageComplete,
                HasRequestedCoverageWindow = freshness.HasRequestedCoverageWindow,
                RequestedHistoryDays = freshness.RequestedHistoryDays,
                RequestedCoverageDays = freshness.RequestedCoverageDays,
                LocalCoverageDays = freshness.LocalCoverageDays,
                MissingDayCount = freshness.MissingDayCount,
                TotalDaysInCurrentRun = 0,
                CompletedDaysInCurrentRun = 0,
                StatusText = freshness.IsCurrentThroughRequiredDay && !freshness.IsRequestedCoverageComplete
                    ? "LOCAL INTEL PARTIALLY POPULATED"
                    : "LOCAL INTEL STALE — awaiting update",
                DetailText = coverageDetail,
                ErrorText = "",
                TotalProgressIsIndeterminate = false,
                TotalProgressPercent = 0,
                TotalProgressText = BuildTotalProgressText(freshness, false, 0, freshness.MissingDayCount),
                CurrentDayProgressIsIndeterminate = false,
                CurrentDayProgressPercent = 0,
                CurrentDayProgressText = currentDayProgressText,
                LiveFeed = liveFeedSnapshot,
                TodaysFreshness = todaysFreshnessSnapshot,
                HistoricalFreshness = historicalFreshnessSnapshot
            };
        }

        private void OnLiveFeedStatusChanged()
        {
            Publish();
        }

        private void OnTodaysFreshnessStatusChanged()
        {
            Publish();
        }

        private void OnHistoricalFreshnessStatusChanged()
        {
            Publish();
        }

        private void InitializeOrAdvanceProgressSessionLocked(KillmailDatasetFreshnessStatus freshness, string nextDayUtc)
        {
            var remainingDays = Math.Max(0, freshness?.MissingDayCount ?? 0);
            var expectedRemainingDays = Math.Max(0, _totalDaysInCurrentRun - _completedDaysInCurrentRun);

            if (_totalDaysInCurrentRun <= 0 ||
                _completedDaysInCurrentRun < 0 ||
                remainingDays > expectedRemainingDays ||
                string.IsNullOrWhiteSpace(nextDayUtc))
            {
                _totalDaysInCurrentRun = remainingDays;
                _completedDaysInCurrentRun = 0;
                return;
            }

            if (remainingDays == 0)
            {
                ResetProgressSessionLocked();
            }
        }

        private void ResetProgressSessionLocked()
        {
            _totalDaysInCurrentRun = 0;
            _completedDaysInCurrentRun = 0;
        }

        private static double BuildTotalProgressPercent(int completedDays, int totalDays)
        {
            if (totalDays <= 0)
            {
                return 0;
            }

            return Math.Max(0, Math.Min(100, ((double)completedDays / totalDays) * 100.0));
        }

        private static string BuildTotalProgressText(
            KillmailDatasetFreshnessStatus freshness,
            bool isRunning,
            int completedDays,
            int totalDays)
        {
            if (isRunning)
            {
                if (totalDays > 0)
                {
                    var currentDayIndex = Math.Min(totalDays, completedDays + 1);
                    return $"Day {currentDayIndex} of {totalDays} in the current catch-up run.";
                }

                return "Updating killmail intel… Progress details unavailable for this phase.";
            }

            if (freshness?.IsCurrentThroughRequiredDay == true)
            {
                return "No catch-up update is currently required.";
            }

            if (freshness != null && freshness.MissingDayCount > 0)
            {
                return $"Waiting to catch up {freshness.MissingDayCount} day(s).";
            }

            return "No update currently running.";
        }

        private static bool IsBlockedOnlyByUnpublishedBoundary(KillmailDatasetFreshnessStatus freshness, string boundaryDayUtc)
        {
            if (freshness == null || string.IsNullOrWhiteSpace(boundaryDayUtc))
            {
                return false;
            }

            if (freshness.MissingDayCount <= 0)
            {
                return true;
            }

            return string.Equals(freshness.FirstMissingDayUtc, boundaryDayUtc, StringComparison.Ordinal);
        }

        private static string BuildLatestPublishedArchiveDetail(KillmailDatasetFreshnessStatus freshness, string boundaryDayUtc)
        {
            var baseDetail = BuildCoverageDetail(freshness);
            if (string.IsNullOrWhiteSpace(boundaryDayUtc))
            {
                return baseDetail;
            }

            return $"{baseDetail} Archive day {boundaryDayUtc} is not published yet; PMG will retry automatically.";
        }

        private static string BuildCoverageDetail(KillmailDatasetFreshnessStatus freshness)
        {
            if (freshness == null)
            {
                return "Coverage unavailable.";
            }

            if (freshness.HasRequestedCoverageWindow && freshness.RequestedCoverageDays > 0)
            {
                var requestedHistoryText = $"Requested History: {freshness.RequestedHistoryDays} day{(freshness.RequestedHistoryDays == 1 ? "" : "s")}.";
                var localCoverageText = $"Local Coverage: {freshness.LocalCoverageDays} of {freshness.RequestedCoverageDays} requested day{(freshness.RequestedCoverageDays == 1 ? "" : "s")}.";
                var missingText = freshness.MissingDayCount > 0
                    ? $"Missing Days: {freshness.MissingDayCount}. Last missing day: {freshness.LastMissingDayUtc}."
                    : "Missing Days: 0.";

                return $"{requestedHistoryText} {localCoverageText} {missingText}";
            }

            var earliest = freshness.EarliestCompleteDayUtc?.Trim() ?? "";
            var latest = freshness.LatestCompleteDayUtc?.Trim() ?? "";

            if (!string.IsNullOrWhiteSpace(earliest) && !string.IsNullOrWhiteSpace(latest))
            {
                if (string.Equals(earliest, latest, StringComparison.Ordinal))
                {
                    return $"Current through {latest}.";
                }

                return $"Current through {earliest} through {latest}.";
            }

            if (!string.IsNullOrWhiteSpace(latest))
            {
                return $"Current through {latest}.";
            }

            return "Coverage unavailable.";
        }

        private void Publish()
        {
            Action<IntelUpdateStatusSnapshot>? handler;
            IntelUpdateStatusSnapshot snapshot;

            lock (_sync)
            {
                handler = StatusChanged;
                snapshot = BuildSnapshot();
            }

            handler?.Invoke(snapshot);
        }

        private void PublishLocked()
        {
            var handler = StatusChanged;
            var snapshot = BuildSnapshot();
            handler?.Invoke(snapshot);
        }

        private sealed class ForegroundPriorityHandle : IDisposable
        {
            private readonly BackgroundIntelUpdateService _owner;
            private bool _disposed;

            public ForegroundPriorityHandle(BackgroundIntelUpdateService owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _owner.EndForegroundPriority();
            }
        }
    }
}
