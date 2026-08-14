using PitmastersGrill.Persistence;
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
                    AppLogger.KillmailImportInfo(
                        $"Background intel deterministic stop reused. task={DescribeTask(_deterministicStopTask)}");
                    return _deterministicStopTask;
                }

                AppLogger.KillmailImportInfo("Background intel deterministic stop requested.");

                _shutdownCts.Cancel();
                _r2z2LiveKillmailService.Stop();

                Task? liveFeedStartupTask;
                Task? archiveBackgroundTask;
                Task? historicalRepairTask;

                lock (_sync)
                {
                    liveFeedStartupTask = _trackedLiveFeedStartupTask;
                    archiveBackgroundTask = _backgroundTask;
                    historicalRepairTask = _backgroundHistoricalRepairTask;
                }

                AppLogger.KillmailImportInfo(
                    "Background intel shutdown snapshot. " +
                    $"liveFeedStartup=[{DescribeTask(liveFeedStartupTask)}] " +
                    $"archiveBackground=[{DescribeTask(archiveBackgroundTask)}] " +
                    $"historicalRepair=[{DescribeTask(historicalRepairTask)}] " +
                    $"foregroundGateCurrentCount={_foregroundFreshnessOperationGate.CurrentCount}");

                _deterministicStopTask = CompleteStopAsync(
                    liveFeedStartupTask,
                    archiveBackgroundTask,
                    historicalRepairTask);

                AppLogger.KillmailImportInfo(
                    $"Background intel deterministic stop task created. task={DescribeTask(_deterministicStopTask)}");

                return _deterministicStopTask;
            }
        }

        private async Task CompleteStopAsync(
            Task? liveFeedStartupTask,
            Task? archiveBackgroundTask,
            Task? historicalRepairTask)
        {
            AppLogger.KillmailImportInfo("Background intel shutdown stage begin. stage='deferred-live-feed-startup'");
            await AwaitOwnedTaskAsync(liveFeedStartupTask, "R2Z2 deferred startup").ConfigureAwait(false);
            AppLogger.KillmailImportInfo("Background intel shutdown stage complete. stage='deferred-live-feed-startup'");

            try
            {
                // Stop again after the deferred-start task completes so a startup that was
                // already crossing the cancellation boundary cannot leave a worker running.
                AppLogger.KillmailImportInfo("Background intel shutdown stage begin. stage='r2z2-stop-await'");
                await _r2z2LiveKillmailService.StopAsync().ConfigureAwait(false);
                AppLogger.KillmailImportInfo("Background intel shutdown stage complete. stage='r2z2-stop-await'");
            }
            catch (Exception ex)
            {
                AppLogger.KillmailImportWarn($"R2Z2 stop-and-wait completed with an error. message={ex.Message}");
                AppLogger.ErrorOnly("R2Z2 stop-and-wait exception.", ex);
            }

            AppLogger.KillmailImportInfo("Background intel shutdown stage begin. stage='archive-background-worker'");
            await AwaitOwnedTaskAsync(archiveBackgroundTask, "archive background worker").ConfigureAwait(false);
            AppLogger.KillmailImportInfo("Background intel shutdown stage complete. stage='archive-background-worker'");

            AppLogger.KillmailImportInfo("Background intel shutdown stage begin. stage='historical-repair'");
            await AwaitOwnedTaskAsync(historicalRepairTask, "background historical repair").ConfigureAwait(false);
            AppLogger.KillmailImportInfo("Background intel shutdown stage complete. stage='historical-repair'");

            // Today's/Historical foreground freshness operations are not stored as Tasks, but
            // each owns this semaphore for the duration of its run. MainWindow has already
            // cancelled their shared shutdown token before this barrier begins.
            AppLogger.KillmailImportInfo(
                $"Background intel shutdown stage begin. stage='foreground-freshness-gate' currentCount={_foregroundFreshnessOperationGate.CurrentCount}");
            await _foregroundFreshnessOperationGate.WaitAsync().ConfigureAwait(false);
            AppLogger.KillmailImportInfo(
                $"Background intel shutdown foreground gate acquired. currentCount={_foregroundFreshnessOperationGate.CurrentCount}");
            _foregroundFreshnessOperationGate.Release();
            AppLogger.KillmailImportInfo(
                $"Background intel shutdown stage complete. stage='foreground-freshness-gate' currentCount={_foregroundFreshnessOperationGate.CurrentCount}");

            // Final persistence barrier: any writer already inside the process-wide gate must
            // leave before shutdown may continue. Cancelled producers cannot acquire it again.
            AppLogger.KillmailImportInfo("Background intel shutdown stage begin. stage='killmail-write-gate'");
            await _writeGate.WaitForIdleAsync("application shutdown quiescence").ConfigureAwait(false);
            AppLogger.KillmailImportInfo("Background intel shutdown stage complete. stage='killmail-write-gate'");

            AppLogger.KillmailImportInfo("Background intel deterministic stop complete; killmail DB is quiescent.");
        }

        private static async Task AwaitOwnedTaskAsync(Task? task, string taskName)
        {
            if (task == null)
            {
                AppLogger.KillmailImportInfo($"Owned shutdown task absent. task='{taskName}'");
                return;
            }

            AppLogger.KillmailImportInfo(
                $"Owned shutdown task await begin. task='{taskName}' state=[{DescribeTask(task)}]");

            try
            {
                await task.ConfigureAwait(false);
                AppLogger.KillmailImportInfo(
                    $"Owned shutdown task await complete. task='{taskName}' state=[{DescribeTask(task)}]");
            }
            catch (OperationCanceledException)
            {
                AppLogger.KillmailImportInfo(
                    $"Owned shutdown task await cancelled. task='{taskName}' state=[{DescribeTask(task)}]");
            }
            catch (Exception ex)
            {
                // A faulted task is completed and therefore no longer active. Preserve the
                // evidence, then continue to the final DB-quiescence barrier.
                AppLogger.KillmailImportWarn($"Owned shutdown task completed with an error. task='{taskName}' message={ex.Message}");
                AppLogger.ErrorOnly($"Owned shutdown task exception. task='{taskName}'", ex);
            }
        }

        private static string DescribeTask(Task? task)
        {
            if (task == null)
            {
                return "null";
            }

            return $"status={task.Status} completed={task.IsCompleted} canceled={task.IsCanceled} faulted={task.IsFaulted}";
        }
    }
}
