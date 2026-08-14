using PitmastersGrill.Persistence;
using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;

namespace PitmastersGrill
{
    public partial class MainWindow
    {
        private int _lifecycleDiagnosticSequence;

        private void EnableMainWindowLifecycleDiagnostics()
        {
            Activated += MainWindow_LifecycleActivated;
            Deactivated += MainWindow_LifecycleDeactivated;
            StateChanged += MainWindow_LifecycleStateChanged;
            IsVisibleChanged += MainWindow_LifecycleIsVisibleChanged;
            Loaded += MainWindow_LifecycleLoaded;
            ContentRendered += MainWindow_LifecycleContentRendered;
            Closing += MainWindow_LifecycleClosing;
            Closed += MainWindow_LifecycleClosed;

            LogMainWindowLifecycle("diagnostics-attached");
        }

        private void MainWindow_LifecycleActivated(object? sender, EventArgs e) =>
            LogMainWindowLifecycle("activated");

        private void MainWindow_LifecycleDeactivated(object? sender, EventArgs e) =>
            LogMainWindowLifecycle("deactivated");

        private void MainWindow_LifecycleStateChanged(object? sender, EventArgs e) =>
            LogMainWindowLifecycle("state-changed");

        private void MainWindow_LifecycleIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) =>
            LogMainWindowLifecycle("visibility-changed", $"old='{e.OldValue}' new='{e.NewValue}'");

        private void MainWindow_LifecycleLoaded(object sender, RoutedEventArgs e) =>
            LogMainWindowLifecycle("loaded-event");

        private void MainWindow_LifecycleContentRendered(object? sender, EventArgs e) =>
            LogMainWindowLifecycle("content-rendered");

        private void MainWindow_LifecycleClosing(object? sender, CancelEventArgs e) =>
            LogMainWindowLifecycle("closing-event", $"cancel='{e.Cancel}'");

        private void MainWindow_LifecycleClosed(object? sender, EventArgs e) =>
            LogMainWindowLifecycle("closed-event");

        private void LogMainWindowLifecycle(string eventName, string? detail = null)
        {
            try
            {
                var sequence = Interlocked.Increment(ref _lifecycleDiagnosticSequence);
                var barrierTask = _shutdownBarrierTask;
                var barrierStatus = barrierTask?.Status.ToString() ?? "none";
                var suffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" {detail}";

                AppLogger.UiInfo(
                    $"MainWindow lifecycle. seq={sequence} event='{eventName}' thread={Environment.CurrentManagedThreadId} " +
                    $"windowState='{WindowState}' visible={IsVisible} active={IsActive} loaded={IsLoaded} " +
                    $"initialized={_isMainWindowInitialized} shuttingDown={_isShuttingDown} " +
                    $"barrierStatus='{barrierStatus}' barrierComplete={_shutdownBarrierComplete} " +
                    $"dispatcherShutdownStarted={Dispatcher.HasShutdownStarted} dispatcherShutdownFinished={Dispatcher.HasShutdownFinished}.{suffix}");
            }
            catch (Exception ex)
            {
                AppLogger.UiWarn($"MainWindow lifecycle diagnostic logging failed. event='{eventName}' error={ex.Message}");
            }
        }
    }
}
