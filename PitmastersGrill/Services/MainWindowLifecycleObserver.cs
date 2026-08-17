using System;
using System.Threading;

namespace PitmastersGrill.Services
{
    public sealed record MainWindowLifecycleSnapshot(
        string WindowState,
        bool IsVisible,
        bool IsShuttingDown,
        string BarrierStatus,
        bool BarrierComplete,
        bool DispatcherShutdownStarted,
        bool DispatcherShutdownFinished,
        int ThreadId);

    public sealed class MainWindowLifecycleObserver
    {
        private readonly Func<MainWindowLifecycleSnapshot> _captureSnapshot;
        private readonly Action<string> _logInfo;
        private readonly Action<string> _logWarning;
        private int _sequence;

        public MainWindowLifecycleObserver(
            Func<MainWindowLifecycleSnapshot> captureSnapshot,
            Action<string> logInfo,
            Action<string> logWarning)
        {
            _captureSnapshot = captureSnapshot ?? throw new ArgumentNullException(nameof(captureSnapshot));
            _logInfo = logInfo ?? throw new ArgumentNullException(nameof(logInfo));
            _logWarning = logWarning ?? throw new ArgumentNullException(nameof(logWarning));
        }

        public int Sequence => Volatile.Read(ref _sequence);

        public void Observe(string eventName, string? detail = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

            try
            {
                var sequence = Interlocked.Increment(ref _sequence);
                var snapshot = _captureSnapshot();
                var suffix = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" {detail}";

                _logInfo(
                    $"MainWindow lifecycle. seq={sequence} event='{eventName}' thread={snapshot.ThreadId} " +
                    $"windowState='{snapshot.WindowState}' visible={snapshot.IsVisible} shuttingDown={snapshot.IsShuttingDown} " +
                    $"barrierStatus='{snapshot.BarrierStatus}' barrierComplete={snapshot.BarrierComplete} " +
                    $"dispatcherShutdownStarted={snapshot.DispatcherShutdownStarted} dispatcherShutdownFinished={snapshot.DispatcherShutdownFinished}.{suffix}");
            }
            catch (Exception ex)
            {
                try
                {
                    _logWarning($"MainWindow lifecycle logging failed. event='{eventName}' error={ex.Message}");
                }
                catch
                {
                    // Lifecycle diagnostics must never interfere with the window lifecycle.
                }
            }
        }
    }
}
