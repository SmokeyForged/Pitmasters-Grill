using System;
using System.Threading.Tasks;

namespace PitmastersGrill.Services
{
    public enum ShutdownCloseDisposition
    {
        AllowClose,
        DeferAndStart,
        DeferExisting
    }

    public sealed class DeterministicShutdownCoordinator
    {
        private readonly Func<Task> _stopBackgroundWorkAsync;
        private readonly Action _signalCancellation;
        private readonly Func<bool> _canRequestFinalClose;
        private readonly Func<Task> _requestFinalCloseAsync;
        private readonly Action<string> _logInfo;
        private readonly Action<string, Exception> _logError;
        private readonly Action<string, string?> _logLifecycle;

        private bool _barrierStarted;
        private bool _barrierComplete;
        private Task? _barrierTask;

        public DeterministicShutdownCoordinator(
            Func<Task> stopBackgroundWorkAsync,
            Action signalCancellation,
            Func<bool> canRequestFinalClose,
            Func<Task> requestFinalCloseAsync,
            Action<string> logInfo,
            Action<string, Exception> logError,
            Action<string, string?> logLifecycle)
        {
            _stopBackgroundWorkAsync = stopBackgroundWorkAsync ?? throw new ArgumentNullException(nameof(stopBackgroundWorkAsync));
            _signalCancellation = signalCancellation ?? throw new ArgumentNullException(nameof(signalCancellation));
            _canRequestFinalClose = canRequestFinalClose ?? throw new ArgumentNullException(nameof(canRequestFinalClose));
            _requestFinalCloseAsync = requestFinalCloseAsync ?? throw new ArgumentNullException(nameof(requestFinalCloseAsync));
            _logInfo = logInfo ?? throw new ArgumentNullException(nameof(logInfo));
            _logError = logError ?? throw new ArgumentNullException(nameof(logError));
            _logLifecycle = logLifecycle ?? throw new ArgumentNullException(nameof(logLifecycle));
        }

        public bool IsComplete => _barrierComplete;

        public Task? BarrierTask => _barrierTask;

        public string BarrierStatus =>
            _barrierTask?.Status.ToString() ?? (_barrierStarted ? "Starting" : "none");

        public ShutdownCloseDisposition HandleClosing(bool isInitialized)
        {
            if (!isInitialized || _barrierComplete)
            {
                return ShutdownCloseDisposition.AllowClose;
            }

            if (_barrierStarted)
            {
                return ShutdownCloseDisposition.DeferExisting;
            }

            _barrierStarted = true;
            _logInfo("MainWindow close deferred until PMG-owned killmail work is quiescent.");
            _logLifecycle("shutdown-deferred", null);

            try
            {
                _signalCancellation();
            }
            catch (Exception ex)
            {
                _logError("Failed while signalling window-owned shutdown cancellation.", ex);
            }

            _barrierTask = CompleteShutdownAsync();
            return ShutdownCloseDisposition.DeferAndStart;
        }

        private async Task CompleteShutdownAsync()
        {
            _logLifecycle("shutdown-barrier-begin", null);

            try
            {
                await _stopBackgroundWorkAsync();
                _logInfo("PMG-owned killmail work reached shutdown quiescence.");
                _logLifecycle("shutdown-background-quiescent", null);
            }
            catch (Exception ex)
            {
                _logLifecycle("shutdown-barrier-fault", $"error='{ex.GetType().Name}'");
                _logError("PMG-owned killmail shutdown barrier failed.", ex);
            }
            finally
            {
                _barrierComplete = true;
                _logLifecycle("shutdown-barrier-complete", null);

                if (_canRequestFinalClose())
                {
                    _logLifecycle("shutdown-final-close", null);
                    try
                    {
                        await _requestFinalCloseAsync();
                    }
                    catch (Exception ex)
                    {
                        _logError("Failed while requesting final MainWindow close after shutdown barrier.", ex);
                    }
                }
            }
        }
    }
}
