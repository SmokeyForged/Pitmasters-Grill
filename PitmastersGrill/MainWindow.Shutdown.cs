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
            Closing += MainWindow_DeterministicClosing;
        }

        private void MainWindow_DeterministicClosing(object? sender, CancelEventArgs e)
        {
            if (!_isMainWindowInitialized || _shutdownBarrierComplete)
            {
                return;
            }

            e.Cancel = true;

            if (_shutdownBarrierTask != null)
            {
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
                _windowShutdownCts.Cancel();
                CancelBoardPopulationRetry();
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Failed while signalling window-owned shutdown cancellation.", ex);
            }

            _shutdownBarrierTask = CompleteDeterministicShutdownAsync();
        }

        private async Task CompleteDeterministicShutdownAsync()
        {
            try
            {
                await _backgroundIntelUpdateService.StopAsync();
                AppLogger.UiInfo("PMG-owned killmail work reached shutdown quiescence.");
            }
            catch (Exception ex)
            {
                AppLogger.UiError("PMG-owned killmail shutdown barrier failed.", ex);
            }
            finally
            {
                _shutdownBarrierComplete = true;

                if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                {
                    await Dispatcher.InvokeAsync(Close);
                }
            }
        }
    }
}
