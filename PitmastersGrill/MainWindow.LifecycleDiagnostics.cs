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
            StateChanged += MainWindow_LifecycleStateChanged;
            IsVisibleChanged += MainWindow_LifecycleIsVisibleChanged;
            Closing += MainWindow_LifecycleClosing;
            Closed += MainWindow_LifecycleClosed;

            LogMainWindowLifecycle("diagnostics-attached");
        }

        private void MainWindow_LifecycleStateChanged(object? sender, EventArgs e) =>
            LogMainWindowLifecycle("state-changed");

        private void MainWindow_LifecycleIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) =>
            LogMainWindowLifecycle("visibility-changed", $"old='{e.OldValue}' new='{e.NewValue}'");

        private void MainWindow_LifecycleClosing(object? sender, CancelEventArgs e) =>
            LogMainWindowLifecycle("closing", $"cancel='{e.Cancel}'");

        private void MainWindow_LifecycleClosed(object? sender, EventArgs e) =>
            LogMainWindowLifecycle("closed");

        private void LogMainWindowLifecycle(string eventName, string? detail = null)
        {
            try
            {
                var sequence = Interlocked.Increment(ref _lifecycleDiagnosticSequence);
                var barrierStatus = _shutdownBarrierTask?.Status.ToString() ?? "none";
                var suffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" {detail}";

                AppLogger.UiInfo(
                    $"MainWindow lifecycle. seq={sequence} event='{eventName}' thread={Environment.CurrentManagedThreadId} " +
                    $"windowState='{WindowState}' visible={IsVisible} shuttingDown={_isShuttingDown} " +
                    $"barrierStatus='{barrierStatus}' barrierComplete={_shutdownBarrierComplete} " +
                    $"dispatcherShutdownStarted={Dispatcher.HasShutdownStarted} dispatcherShutdownFinished={Dispatcher.HasShutdownFinished}.{suffix}");
            }
            catch (Exception ex)
            {
                AppLogger.UiWarn($"MainWindow lifecycle logging failed. event='{eventName}' error={ex.Message}");
            }
        }
    }
}
