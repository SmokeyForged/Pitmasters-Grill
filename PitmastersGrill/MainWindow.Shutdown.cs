using PitmastersGrill.Persistence;
using PitmastersGrill.Services;
using System;
using System.ComponentModel;

namespace PitmastersGrill
{
    public partial class MainWindow
    {
        private DeterministicShutdownCoordinator? _deterministicShutdownCoordinator;

        private DeterministicShutdownCoordinator ShutdownCoordinator =>
            _deterministicShutdownCoordinator ??= new DeterministicShutdownCoordinator(
                _backgroundIntelUpdateService.StopAsync,
                SignalWindowOwnedShutdownCancellation,
                () => !Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished,
                async () => await Dispatcher.InvokeAsync(Close),
                AppLogger.UiInfo,
                AppLogger.UiError,
                LogMainWindowLifecycle);

        private string ShutdownBarrierStatus => _deterministicShutdownCoordinator?.BarrierStatus ?? "none";
        private bool ShutdownBarrierComplete => _deterministicShutdownCoordinator?.IsComplete == true;

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            EnableMainWindowLifecycleDiagnostics();
            Closing += MainWindow_DeterministicClosing;
        }

        private void MainWindow_DeterministicClosing(object? sender, CancelEventArgs e)
        {
            if (_isMainWindowInitialized && !ShutdownCoordinator.IsComplete)
            {
                _isShuttingDown = true;
            }

            var disposition = ShutdownCoordinator.HandleClosing(_isMainWindowInitialized);
            if (disposition == ShutdownCloseDisposition.AllowClose)
            {
                return;
            }

            e.Cancel = true;
            if (disposition == ShutdownCloseDisposition.DeferExisting)
            {
                return;
            }

            if (ExitApplicationButton != null)
            {
                ExitApplicationButton.IsEnabled = false;
                ExitApplicationButton.Content = "Exiting...";
            }
        }

        private void SignalWindowOwnedShutdownCancellation()
        {
            _windowShutdownCts.Cancel();
            CancelBoardPopulationRetry();
        }
    }
}
