using PitmastersGrill.Services;
using System;
using System.ComponentModel;

namespace PitmastersGrill
{
    public partial class MainWindow
    {
        private MainWindowLifecycleObserver? _lifecycleObserver;

        private MainWindowLifecycleObserver LifecycleObserver =>
            _lifecycleObserver ??= new MainWindowLifecycleObserver(
                CaptureLifecycleSnapshot,
                AppLogger.UiInfo,
                AppLogger.UiWarn);

        private void EnableMainWindowLifecycleObserver()
        {
            StateChanged += MainWindow_LifecycleStateChanged;
            IsVisibleChanged += MainWindow_LifecycleIsVisibleChanged;
            Closing += MainWindow_LifecycleClosing;
            Closed += MainWindow_LifecycleClosed;

            ObserveMainWindowLifecycle("diagnostics-attached");
        }

        private void MainWindow_LifecycleStateChanged(object? sender, EventArgs e) =>
            ObserveMainWindowLifecycle("state-changed");

        private void MainWindow_LifecycleIsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e) =>
            ObserveMainWindowLifecycle("visibility-changed", $"old='{e.OldValue}' new='{e.NewValue}'");

        private void MainWindow_LifecycleClosing(object? sender, CancelEventArgs e) =>
            ObserveMainWindowLifecycle("closing", $"cancel='{e.Cancel}'");

        private void MainWindow_LifecycleClosed(object? sender, EventArgs e) =>
            ObserveMainWindowLifecycle("closed");

        private void ObserveMainWindowLifecycle(string eventName, string? detail = null) =>
            LifecycleObserver.Observe(eventName, detail);

        private MainWindowLifecycleSnapshot CaptureLifecycleSnapshot() =>
            new(
                WindowState.ToString(),
                IsVisible,
                _isShuttingDown,
                ShutdownBarrierStatus,
                ShutdownBarrierComplete,
                Dispatcher.HasShutdownStarted,
                Dispatcher.HasShutdownFinished,
                Environment.CurrentManagedThreadId);
    }
}
