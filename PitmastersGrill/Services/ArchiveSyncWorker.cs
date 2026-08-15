using PitmastersGrill.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersGrill.Services
{
    internal sealed class ArchiveSyncWorker
    {
        private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan DefaultPostImportDelay = TimeSpan.FromSeconds(5);

        private readonly object _sync = new();
        private readonly SemaphoreSlim _wakeSignal = new(0, 1);
        private readonly Func<KillmailDatasetFreshnessStatus> _getFreshnessStatus;
        private readonly Func<KillmailRemoteDayInfo, CancellationToken, Task<KillmailDayImportResult>> _importSingleDayAsync;
        private readonly ForegroundFreshnessCoordinator _foregroundFreshnessCoordinator;
        private readonly CancellationToken _shutdownToken;
        private readonly TimeSpan _pollInterval;
        private readonly TimeSpan _postImportDelay;
        private Task? _backgroundTask;
        private ArchiveSyncState _state = new();

        public ArchiveSyncWorker(
            KillmailDatasetFreshnessService freshnessService,
            KillmailDayImportService killmailDayImportService,
            ForegroundFreshnessCoordinator foregroundFreshnessCoordinator,
            CancellationToken shutdownToken)
            : this(
                CreateFreshnessAccessor(freshnessService),
                CreateDayImporter(killmailDayImportService),
                foregroundFreshnessCoordinator,
                shutdownToken,
                DefaultPollInterval,
                DefaultPostImportDelay)
        {
        }

        internal ArchiveSyncWorker(
            Func<KillmailDatasetFreshnessStatus> getFreshnessStatus,
            Func<KillmailRemoteDayInfo, CancellationToken, Task<KillmailDayImportResult>> importSingleDayAsync,
            ForegroundFreshnessCoordinator foregroundFreshnessCoordinator,
            CancellationToken shutdownToken,
            TimeSpan pollInterval,
            TimeSpan postImportDelay)
        {
            _getFreshnessStatus = getFreshnessStatus ?? throw new ArgumentNullException(nameof(getFreshnessStatus));
            _importSingleDayAsync = importSingleDayAsync ?? throw new ArgumentNullException(nameof(importSingleDayAsync));
            _foregroundFreshnessCoordinator = foregroundFreshnessCoordinator ?? throw new ArgumentNullException(nameof(foregroundFreshnessCoordinator));
            _shutdownToken = shutdownToken;
            _pollInterval = pollInterval;
            _postImportDelay = postImportDelay;
        }

        public event Action? StateChanged;

        public ArchiveSyncState GetState()
        {
            lock (_sync)
            {
                return _state.Clone();
            }
        }

        public void BeginBootstrap(string startDayUtc, int totalDays)
        {
            UpdateState(state =>
            {
                state.CurrentImportDayUtc = startDayUtc ?? "";
                state.LastError = "";
                state.NotPublishedBoundaryDayUtc = "";
                state.IsRunning = true;
                state.TotalDaysInCurrentRun = Math.Max(0, totalDays);
                state.CompletedDaysInCurrentRun = 0;
            });
        }

        public void ResetAfterBootstrapFailure()
        {
            UpdateState(state =>
            {
                state.CurrentImportDayUtc = "";
                state.IsRunning = false;
            });
        }

        public void StartIfNeeded()
        {
            var started = false;

            lock (_sync)
            {
                if (_backgroundTask == null || _backgroundTask.IsCompleted)
                {
                    _backgroundTask = Task.Run(() => RunLoopAsync(_shutdownToken), _shutdownToken);
                    started = true;
                }
            }

            if (started)
            {
                AppLogger.KillmailImportInfo("Archive background worker starting.");
            }

            StateChanged?.Invoke();
            Wake();
        }

        public void Wake()
        {
            if (_wakeSignal.CurrentCount != 0)
            {
                return;
            }

            try
            {
                _wakeSignal.Release();
            }
            catch
            {
            }
        }

        public async Task WaitForCompletionAsync()
        {
            Task? task;
            lock (_sync)
            {
                task = _backgroundTask;
            }

            if (task == null)
            {
                return;
            }

            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task RunLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _foregroundFreshnessCoordinator.WaitForPriorityToClearAsync(cancellationToken).ConfigureAwait(false);

                    var freshness = _getFreshnessStatus();
                    if ((freshness.IsCurrentThroughRequiredDay && freshness.IsRequestedCoverageComplete) || freshness.MissingDayCount <= 0)
                    {
                        UpdateState(state =>
                        {
                            state.CurrentImportDayUtc = "";
                            state.LastError = "";
                            state.IsRunning = false;
                            ResetProgressSession(state);
                        });

                        await WaitForWakeOrTimeoutAsync(cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    var nextDayUtc = freshness.FirstMissingDayUtc;
                    UpdateState(state =>
                    {
                        InitializeOrAdvanceProgressSession(state, freshness, nextDayUtc);
                        state.CurrentImportDayUtc = nextDayUtc;
                        state.LastError = "";
                        state.NotPublishedBoundaryDayUtc = "";
                        state.IsRunning = true;
                    });

                    AppLogger.KillmailImportInfo(
                        $"Killmail day import attempt. day={nextDayUtc} requestedStart={freshness.RequestedStartDayUtc} requiredThrough={freshness.RequiredThroughDayUtc} localCoverageDays={freshness.LocalCoverageDays} requestedCoverageDays={freshness.RequestedCoverageDays} missingDays={freshness.MissingDayCount}");

                    var result = await _importSingleDayAsync(
                        new KillmailRemoteDayInfo
                        {
                            DayUtc = nextDayUtc,
                            RemoteTotalCount = 0
                        },
                        cancellationToken).ConfigureAwait(false);

                    if (result.ArchiveUnavailableNotPublishedYet)
                    {
                        AppLogger.KillmailImportWarn(
                            $"Killmail day import skipped. day={nextDayUtc} reason=archive-not-published boundaryDay={result.ArchiveUnavailableDayUtc}");

                        UpdateState(state =>
                        {
                            state.NotPublishedBoundaryDayUtc = result.ArchiveUnavailableDayUtc;
                            state.LastError = "";
                            state.CurrentImportDayUtc = "";
                            state.IsRunning = false;
                            ResetProgressSession(state);
                        });

                        await WaitForWakeOrTimeoutAsync(cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    if (!result.Success)
                    {
                        AppLogger.KillmailImportWarn(
                            $"Killmail day import failed. day={nextDayUtc} reason={result.Error}");

                        UpdateState(state =>
                        {
                            state.LastError = result.Error;
                            state.CurrentImportDayUtc = "";
                            state.IsRunning = false;
                            ResetProgressSession(state);
                        });

                        await WaitForWakeOrTimeoutAsync(cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    UpdateState(state =>
                    {
                        state.CompletedDaysInCurrentRun = Math.Min(
                            state.CompletedDaysInCurrentRun + 1,
                            state.TotalDaysInCurrentRun);
                        state.CurrentImportDayUtc = "";
                        state.LastError = "";
                        state.NotPublishedBoundaryDayUtc = "";
                        state.IsRunning = false;
                    });

                    var postImportFreshness = _getFreshnessStatus();
                    AppLogger.KillmailImportInfo(
                        $"Killmail day import complete. day={nextDayUtc} importedKillmails={result.ImportedKillmailCount} oldestDay={postImportFreshness.EarliestCompleteDayUtc} newestDay={postImportFreshness.LatestCompleteDayUtc} localCoverageDays={postImportFreshness.LocalCoverageDays} requestedCoverageDays={postImportFreshness.RequestedCoverageDays} missingDays={postImportFreshness.MissingDayCount}");

                    if (_postImportDelay > TimeSpan.Zero)
                    {
                        await Task.Delay(_postImportDelay, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    UpdateState(state =>
                    {
                        state.LastError = ex.Message;
                        state.CurrentImportDayUtc = "";
                        state.IsRunning = false;
                        ResetProgressSession(state);
                    });

                    try
                    {
                        await WaitForWakeOrTimeoutAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            UpdateState(state =>
            {
                state.CurrentImportDayUtc = "";
                state.IsRunning = false;
                ResetProgressSession(state);
            });
        }

        private async Task WaitForWakeOrTimeoutAsync(CancellationToken cancellationToken)
        {
            await _wakeSignal.WaitAsync(_pollInterval, cancellationToken).ConfigureAwait(false);
        }

        private void UpdateState(Action<ArchiveSyncState> update)
        {
            lock (_sync)
            {
                update(_state);
            }

            StateChanged?.Invoke();
        }

        private static Func<KillmailDatasetFreshnessStatus> CreateFreshnessAccessor(
            KillmailDatasetFreshnessService freshnessService)
        {
            if (freshnessService == null)
            {
                throw new ArgumentNullException(nameof(freshnessService));
            }

            return freshnessService.GetFreshnessStatus;
        }

        private static Func<KillmailRemoteDayInfo, CancellationToken, Task<KillmailDayImportResult>> CreateDayImporter(
            KillmailDayImportService killmailDayImportService)
        {
            if (killmailDayImportService == null)
            {
                throw new ArgumentNullException(nameof(killmailDayImportService));
            }

            return killmailDayImportService.ImportSingleDayAsync;
        }

        private static void InitializeOrAdvanceProgressSession(
            ArchiveSyncState state,
            KillmailDatasetFreshnessStatus freshness,
            string nextDayUtc)
        {
            var remainingDays = Math.Max(0, freshness?.MissingDayCount ?? 0);
            var expectedRemainingDays = Math.Max(0, state.TotalDaysInCurrentRun - state.CompletedDaysInCurrentRun);

            if (state.TotalDaysInCurrentRun <= 0 ||
                state.CompletedDaysInCurrentRun < 0 ||
                remainingDays > expectedRemainingDays ||
                string.IsNullOrWhiteSpace(nextDayUtc))
            {
                state.TotalDaysInCurrentRun = remainingDays;
                state.CompletedDaysInCurrentRun = 0;
                return;
            }

            if (remainingDays == 0)
            {
                ResetProgressSession(state);
            }
        }

        private static void ResetProgressSession(ArchiveSyncState state)
        {
            state.TotalDaysInCurrentRun = 0;
            state.CompletedDaysInCurrentRun = 0;
        }
    }

    internal sealed class ArchiveSyncState
    {
        public string CurrentImportDayUtc { get; set; } = "";
        public string LastError { get; set; } = "";
        public string NotPublishedBoundaryDayUtc { get; set; } = "";
        public bool IsRunning { get; set; }
        public int TotalDaysInCurrentRun { get; set; }
        public int CompletedDaysInCurrentRun { get; set; }

        public ArchiveSyncState Clone()
        {
            return new ArchiveSyncState
            {
                CurrentImportDayUtc = CurrentImportDayUtc,
                LastError = LastError,
                NotPublishedBoundaryDayUtc = NotPublishedBoundaryDayUtc,
                IsRunning = IsRunning,
                TotalDaysInCurrentRun = TotalDaysInCurrentRun,
                CompletedDaysInCurrentRun = CompletedDaysInCurrentRun
            };
        }
    }
}
