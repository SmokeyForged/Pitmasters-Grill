using System;
using System.Threading;
using System.Threading.Tasks;
using PitmastersGrill.Persistence;

namespace PitmastersGrill.Services
{
    public sealed class KillmailDbWriteGate
    {
        private static readonly SemaphoreSlim SharedGate = new(1, 1);

        public IDisposable Enter(string operationName, CancellationToken cancellationToken = default)
        {
            var normalizedOperation = NormalizeOperationName(operationName);
            AppLogger.KillmailImportDebug($"Killmail DB write gate wait. operation='{normalizedOperation}'");
            SharedGate.Wait(cancellationToken);
            AppLogger.KillmailImportDebug($"Killmail DB write gate acquired. operation='{normalizedOperation}'");
            return new Releaser(normalizedOperation);
        }

        public async Task<IDisposable> EnterAsync(string operationName, CancellationToken cancellationToken = default)
        {
            var normalizedOperation = NormalizeOperationName(operationName);
            AppLogger.KillmailImportDebug($"Killmail DB write gate wait. operation='{normalizedOperation}'");
            await SharedGate.WaitAsync(cancellationToken);
            AppLogger.KillmailImportDebug($"Killmail DB write gate acquired. operation='{normalizedOperation}'");
            return new Releaser(normalizedOperation);
        }

        public async Task WaitForIdleAsync(string operationName, CancellationToken cancellationToken = default)
        {
            using var gate = await EnterAsync(
                $"{NormalizeOperationName(operationName)} quiescence barrier",
                cancellationToken);
        }

        private static string NormalizeOperationName(string operationName)
        {
            return string.IsNullOrWhiteSpace(operationName)
                ? "unspecified"
                : operationName.Trim();
        }

        private static void Release(string operationName)
        {
            SharedGate.Release();
            AppLogger.KillmailImportDebug($"Killmail DB write gate released. operation='{operationName}'");
        }

        private sealed class Releaser : IDisposable
        {
            private readonly string _operationName;
            private int _disposed;

            public Releaser(string operationName)
            {
                _operationName = operationName;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                {
                    return;
                }

                Release(_operationName);
            }
        }
    }
}
