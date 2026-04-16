using Microsoft.Data.Sqlite;
using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersGrill.Services
{
    public class BackgroundIntelUpdateService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(10);

        private readonly object _sync = new();
        private readonly SemaphoreSlim _wakeSignal = new(0, 1);
        private readonly KillmailDatasetFreshnessService _freshnessService;
        private readonly KillmailDayImportService _killmailDayImportService;
        private readonly KillmailDatasetMetadataRepository _metadataRepository;

        private Task? _backgroundTask;
        private readonly CancellationTokenSource _shutdownCts = new();

        private string _currentImportDayUtc = "";
        private string _lastError = "";
        private string _notPublishedBoundaryDayUtc = "";
        private bool _isRunning;
        private int _foregroundPriorityRequests;

        public event Action<IntelUpdateStatusSnapshot>? StatusChanged;

        public BackgroundIntelUpdateService(
            KillmailDatasetFreshnessService freshnessService,
            KillmailDayImportService killmailDayImportService)
        {
            _freshnessService = freshnessService;
            _killmailDayImportService = killmailDayImportService;
            _metadataRepository = new KillmailDatasetMetadataRepository(KillmailPaths.GetKillmailDatabasePath());
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
                    _backgroundTask = Task.Run(() => RunLoopAsync(_shutdownCts.Token));
                }

                PublishLocked();
            }

            ReleaseWakeSignal();
        }

        public async Task EnableKillmailDbPullAsync(int lookbackDays, CancellationToken cancellationToken = default)
        {
            if (lookbackDays < 1)
            {
                lookbackDays = 30;
            }

            var requiredThroughDay = DateTime.UtcNow.Date.AddDays(-1);
            var bootstrapStartDay = requiredThroughDay.AddDays(-(lookbackDays - 1));
            var bootstrapStartDayUtc = bootstrapStartDay.ToString("yyyy-MM-dd");

            lock (_sync)
            {
                _currentImportDayUtc = bootstrapStartDayUtc;
                _lastError = "";
                _notPublishedBoundaryDayUtc = "";
                _isRunning = true;
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
                _shutdownCts.Cancel();
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
                    if (freshness.IsCurrentThroughRequiredDay || freshness.MissingDayCount <= 0)
                    {
                        lock (_sync)
                        {
                            _currentImportDayUtc = "";
                            _lastError = "";
                            _isRunning = false;
                            PublishLocked();
                        }

                        await WaitForWakeOrTimeoutAsync(cancellationToken);
                        continue;
                    }

                    var nextDayUtc = freshness.FirstMissingDayUtc;

                    lock (_sync)
                    {
                        _currentImportDayUtc = nextDayUtc;
                        _lastError = "";
                        _notPublishedBoundaryDayUtc = "";
                        _isRunning = true;
                        PublishLocked();
                    }

                    var result = await _killmailDayImportService.ImportSingleDayAsync(
                        new KillmailRemoteDayInfo
                        {
                            DayUtc = nextDayUtc,
                            RemoteTotalCount = 0
                        },
                        cancellationToken);

                    if (result.ArchiveUnavailableNotPublishedYet)
                    {
                        lock (_sync)
                        {
                            _notPublishedBoundaryDayUtc = result.ArchiveUnavailableDayUtc;
                            _lastError = "";
                            _currentImportDayUtc = "";
                            _isRunning = false;
                            PublishLocked();
                        }

                        await WaitForWakeOrTimeoutAsync(cancellationToken);
                        continue;
                    }

                    if (!result.Success)
                    {
                        lock (_sync)
                        {
                            _lastError = result.Error;
                            _currentImportDayUtc = "";
                            _isRunning = false;
                            PublishLocked();
                        }

                        await WaitForWakeOrTimeoutAsync(cancellationToken);
                        continue;
                    }

                    lock (_sync)
                    {
                        _currentImportDayUtc = "";
                        _lastError = "";
                        _notPublishedBoundaryDayUtc = "";
                        _isRunning = false;
                        PublishLocked();
                    }

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
                PublishLocked();
            }
        }

        private void ResetLocalKillmailDerivedState(string bootstrapStartDayUtc, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DebugTraceWriter.WriteLine("killmail reset start: clearing archive cache and derived state");
            KillmailPaths.ClearArchiveCacheBestEffort();

            cancellationToken.ThrowIfCancellationRequested();

            var connectionString = $"Data Source={KillmailPaths.GetKillmailDatabasePath()}";
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();
            ExecuteNonQuery(connection, transaction, "DELETE FROM day_import_state;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM pilot_registry_day;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM pilot_fleet_observations_day;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM pilot_ship_observations_day;");
            transaction.Commit();

            cancellationToken.ThrowIfCancellationRequested();

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
            var foregroundActive = Volatile.Read(ref _foregroundPriorityRequests) > 0;
            var coverageDetail = BuildCoverageDetail(freshness);

            if (!string.IsNullOrWhiteSpace(_lastError))
            {
                return new IntelUpdateStatusSnapshot
                {
                    IsRunning = false,
                    IsCurrentThroughYesterday = freshness.IsCurrentThroughRequiredDay,
                    HasError = true,
                    IsForegroundPriorityActive = foregroundActive,
                    LatestCompleteDayUtc = freshness.LatestCompleteDayUtc,
                    RequiredThroughDayUtc = freshness.RequiredThroughDayUtc,
                    CurrentImportDayUtc = _currentImportDayUtc,
                    MissingDayCount = freshness.MissingDayCount,
                    StatusText = "LOCAL INTEL UPDATE FAILED",
                    DetailText = _lastError,
                    ErrorText = _lastError
                };
            }

            if (!string.IsNullOrWhiteSpace(_notPublishedBoundaryDayUtc))
            {
                return new IntelUpdateStatusSnapshot
                {
                    IsRunning = false,
                    IsCurrentThroughYesterday = true,
                    HasError = false,
                    IsForegroundPriorityActive = foregroundActive,
                    LatestCompleteDayUtc = freshness.LatestCompleteDayUtc,
                    RequiredThroughDayUtc = freshness.RequiredThroughDayUtc,
                    CurrentImportDayUtc = "",
                    MissingDayCount = 0,
                    StatusText = "LOCAL INTEL CURRENT — through latest published archive",
                    DetailText = coverageDetail,
                    ErrorText = ""
                };
            }

            if (_isRunning)
            {
                var detail = foregroundActive
                    ? "Foreground activity detected. Background intel update is paused until current clipboard/API work finishes."
                    : $"Current day: {_currentImportDayUtc} • Remaining day(s): {freshness.MissingDayCount}";

                return new IntelUpdateStatusSnapshot
                {
                    IsRunning = true,
                    IsCurrentThroughYesterday = false,
                    HasError = false,
                    IsForegroundPriorityActive = foregroundActive,
                    LatestCompleteDayUtc = freshness.LatestCompleteDayUtc,
                    RequiredThroughDayUtc = freshness.RequiredThroughDayUtc,
                    CurrentImportDayUtc = _currentImportDayUtc,
                    MissingDayCount = freshness.MissingDayCount,
                    StatusText = "LOCAL INTEL STALE — updating in progress",
                    DetailText = detail,
                    ErrorText = ""
                };
            }

            if (freshness.IsCurrentThroughRequiredDay)
            {
                return new IntelUpdateStatusSnapshot
                {
                    IsRunning = false,
                    IsCurrentThroughYesterday = true,
                    HasError = false,
                    IsForegroundPriorityActive = foregroundActive,
                    LatestCompleteDayUtc = freshness.LatestCompleteDayUtc,
                    RequiredThroughDayUtc = freshness.RequiredThroughDayUtc,
                    CurrentImportDayUtc = "",
                    MissingDayCount = 0,
                    StatusText = "LOCAL INTEL CURRENT — through yesterday",
                    DetailText = coverageDetail,
                    ErrorText = ""
                };
            }

            return new IntelUpdateStatusSnapshot
            {
                IsRunning = false,
                IsCurrentThroughYesterday = false,
                HasError = false,
                IsForegroundPriorityActive = foregroundActive,
                LatestCompleteDayUtc = freshness.LatestCompleteDayUtc,
                RequiredThroughDayUtc = freshness.RequiredThroughDayUtc,
                CurrentImportDayUtc = "",
                MissingDayCount = freshness.MissingDayCount,
                StatusText = "LOCAL INTEL STALE — awaiting update",
                DetailText = coverageDetail,
                ErrorText = ""
            };
        }

        private static string BuildCoverageDetail(KillmailDatasetFreshnessStatus freshness)
        {
            if (freshness == null)
            {
                return "Coverage unavailable.";
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