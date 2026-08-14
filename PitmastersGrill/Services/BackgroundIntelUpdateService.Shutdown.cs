using System;
using System.Threading.Tasks;

namespace PitmastersGrill.Services
{
    public partial class BackgroundIntelUpdateService
    {
        private readonly object _shutdownLifecycleSync = new();
        private Task? _trackedLiveFeedStartupTask;
        private Task? _deterministicStopTask;

        public void StartLiveFeedIfConfiguredAfterUiShownTracked()
        {
            lock (_sync)
            {
                if (_shutdownCts.IsCancellationRequested)
                {
                    AppLogger.KillmailImportInfo("R2Z2 deferred startup skipped because shutdown has started.");
                    return;
                }

                if (_trackedLiveFeedStartupTask != null && !_trackedLiveFeedStartupTask.IsCompleted)
                {
                    return;
                }

                _trackedLiveFeedStartupTask = Task.Run(() =>
                {
                    try
                    {
                        _shutdownCts.Token.ThrowIfCancellationRequested();
                        _r2z2LiveKillmailService.StartIfConfiguredAfterUiShown();
                    }
                    catch (OperationCanceledException)
                    {
                        AppLogger.KillmailImportInfo("R2Z2 deferred startup cancelled during shutdown.");
                    }
                    catch (Exception ex)
                    {
                        AppLogger.KillmailImportWarn($"R2Z2 deferred startup failed. message={ex.Message}");
                        AppLogger.ErrorOnly("R2Z2 deferred startup exception.", ex);
                    }
                }, _shutdownCts.Token);
            }
        }

        public Task StopAsync()
        {
            lock (_shutdownLifecycleSync)
            {
                if (_deterministicStopTask != null)
                {
                    return _deterministicStopTask;
                }

                AppLogger.KillmailImportInfo("Background intel deterministic stop requested.");

                _shutdownCts.Cancel();
                _r2z2LiveKillmailService.Stop();
                _archiveSyncWorker.Wake();

                Task? liveFeedStartupTask;
                lock (_sync)
                {
                    liveFeedStartupTask = _trackedLiveFeedStartupTask;
                }

                _deterministicStopTask = CompleteStopAsync(liveFeedStartupTask);
                return _deterministicStopTask;
            }
        }

        private async Task CompleteStopAsync(Task? liveFeedStartupTask)
        {
            await AwaitOwnedTaskAsync(liveFeedStartupTask, "R2Z2 deferred startup").ConfigureAwait(false);

            try
            {
                // Stop again after the deferred-start task completes so a startup that was
                // already crossing the cancellation boundary cannot leave a worker running.
                await _r2z2LiveKillmailService.StopAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLogger.KillmailImportWarn($"R2Z2 stop-and-wait completed with an error. message={ex.Message}");
                AppLogger.ErrorOnly("R2Z2 stop-and-wait exception.", ex);
            }

            await _archiveSyncWorker.WaitForCompletionAsync().ConfigureAwait(false);
            await _backgroundHistoricalRepairScheduler.WaitForCompletionAsync().ConfigureAwait(false);

            // Foreground freshness owns one operation gate for both Today's and Historical
            // requests. Shared shutdown cancellation is triggered before this barrier.
            await _foregroundFreshnessCoordinator.WaitForIdleAsync().ConfigureAwait(false);

            // Final persistence barrier: any writer already inside the process-wide gate must
            // leave before shutdown may continue. Cancelled producers cannot acquire it again.
            await _writeGate.WaitForIdleAsync("application shutdown quiescence").ConfigureAwait(false);

            AppLogger.KillmailImportInfo("Background intel deterministic stop complete; killmail DB is quiescent.");
        }

        private static async Task AwaitOwnedTaskAsync(Task? task, string taskName)
        {
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
                // Expected during normal shutdown.
            }
            catch (Exception ex)
            {
                // A faulted task is completed and therefore no longer active. Preserve the
                // evidence, then continue to the final DB-quiescence barrier.
                AppLogger.KillmailImportWarn($"Owned shutdown task completed with an error. task='{taskName}' message={ex.Message}");
                AppLogger.ErrorOnly($"Owned shutdown task exception. task='{taskName}'", ex);
            }
        }
    }
}
