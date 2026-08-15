using System;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersGrill.Services
{
    internal sealed class ForegroundFreshnessCoordinator
    {
        private readonly SemaphoreSlim _operationGate = new(1, 1);
        private int _priorityRequests;

        public event Action<bool>? PriorityChanged;

        public bool IsPriorityActive => Volatile.Read(ref _priorityRequests) > 0;

        public async Task<T> RunExclusiveAsync<T>(
            Func<Task<T>> operation,
            Func<T> busyResultFactory,
            string busyLogMessage,
            CancellationToken cancellationToken)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (busyResultFactory == null)
            {
                throw new ArgumentNullException(nameof(busyResultFactory));
            }

            if (!await _operationGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            {
                AppLogger.KillmailImportInfo(busyLogMessage);
                return busyResultFactory();
            }

            try
            {
                return await operation().ConfigureAwait(false);
            }
            finally
            {
                _operationGate.Release();
            }
        }

        public IDisposable BeginPriority()
        {
            Interlocked.Increment(ref _priorityRequests);
            PriorityChanged?.Invoke(true);
            return new PriorityHandle(this);
        }

        public async Task WaitForPriorityToClearAsync(CancellationToken cancellationToken)
        {
            while (IsPriorityActive)
            {
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task WaitForIdleAsync()
        {
            await _operationGate.WaitAsync().ConfigureAwait(false);
            _operationGate.Release();
        }

        private void EndPriority()
        {
            var updated = Interlocked.Decrement(ref _priorityRequests);
            if (updated < 0)
            {
                Interlocked.Exchange(ref _priorityRequests, 0);
                updated = 0;
            }

            PriorityChanged?.Invoke(updated > 0);
        }

        private sealed class PriorityHandle : IDisposable
        {
            private readonly ForegroundFreshnessCoordinator _owner;
            private bool _disposed;

            public PriorityHandle(ForegroundFreshnessCoordinator owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _owner.EndPriority();
            }
        }
    }
}
