using PitmastersGrill.Persistence;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace PitmastersGrill
{
    public partial class MainWindow
    {
        private Task? _shutdownBarrierTask;
        private bool _shutdownBarrierComplete;

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            EnableMainWindowLifecycleDiagnostics();
            Closing += MainWindow_DeterministicClosing;
            LogMainWindowLifecycle("initialized");
        }

        private void MainWindow_DeterministicClosing(object? sender, CancelEventArgs e)
        {
            LogMainWindowLifecycle(
                "deterministic-closing-enter",
                $"cancel='{e.Cancel}' initialized='{_isMainWindowInitialized}' barrierComplete='{_shutdownBarrierComplete}'");

            if (!_isMainWindowInitialized || _shutdownBarrierComplete)
            {
                LogMainWindowLifecycle(
                    "deterministic-closing-bypass",
                    $"initialized='{_isMainWindowInitialized}' barrierComplete='{_shutdownBarrierComplete}'");
                return;
            }

            e.Cancel = true;
            LogMainWindowLifecycle("deterministic-closing-deferred", "cancel='true'");

            if (_shutdownBarrierTask != null)
            {
                LogMainWindowLifecycle(
                    "deterministic-closing-existing-barrier",
                    $"barrierStatus='{_shutdownBarrierTask.Status}'");
                return;
            }

            _isShuttingDown = true;
            AppLogger.UiInfo("MainWindow close deferred until PMG-owned killmail work is quiescent.");

            if (ExitApplicationButton != null)
            {
                ExitApplicationButton.IsEnabled = false;
                ExitApplicationButton.Content = "Exiting...";
            }

            try
            {
                LogMainWindowLifecycle("window-shutdown-cancellation-begin");
                _windowShutdownCts.Cancel();
                CancelBoardPopulationRetry();
                LogMainWindowLifecycle("window-shutdown-cancellation-complete");
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Failed while signalling window-owned shutdown cancellation.", ex);
            }

            LogMainWindowLifecycle("shutdown-barrier-create-begin");
            _shutdownBarrierTask = CompleteDeterministicShutdownAsync();
            LogMainWindowLifecycle(
                "shutdown-barrier-create-complete",
                $"barrierStatus='{_shutdownBarrierTask.Status}'");
        }

        private async Task CompleteDeterministicShutdownAsync()
        {
            LogMainWindowLifecycle("shutdown-barrier-enter");

            try
            {
                LogMainWindowLifecycle("background-stop-await-begin");
                await _backgroundIntelUpdateService.StopAsync();
                LogMainWindowLifecycle("background-stop-await-complete");
                AppLogger.UiInfo("PMG-owned killmail work reached shutdown quiescence.");
            }
            catch (Exception ex)
            {
                LogMainWindowLifecycle("background-stop-await-fault", $"error='{ex.GetType().Name}'");
                AppLogger.UiError("PMG-owned killmail shutdown barrier failed.", ex);
            }
            finally
            {
                LogMainWindowLifecycle("shutdown-barrier-finally-enter");
                _shutdownBarrierComplete = true;
                LogMainWindowLifecycle("shutdown-barrier-marked-complete");

                if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                {
                    LogMainWindowLifecycle("dispatcher-close-dispatch-begin");
                    await Dispatcher.InvokeAsync(() =>
                    {
                        LogMainWindowLifecycle("dispatcher-close-callback-enter");
                        Close();
                        LogMainWindowLifecycle("dispatcher-close-callback-returned");
                    });
                    LogMainWindowLifecycle("dispatcher-close-dispatch-complete");
                }
                else
                {
                    LogMainWindowLifecycle("dispatcher-close-skipped", "dispatcher already shutting down");
                }
            }
        }
    }
}
