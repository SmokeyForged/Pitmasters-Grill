using System;
using System.Threading;
using System.Threading.Tasks;
using PitmastersGrill.Persistence;

namespace PitmastersGrill.Services
{
    public sealed class KillmailDbWriteGate
    {
        private readonly SemaphoreSlim _gate = new(1, 1);

        public IDisposable Enter(string operationName, CancellationToken cancellationToken = default)
        {
            var normalizedOperation = NormalizeOperationName(operationName);
            AppLogger.KillmailImportDebug($"Killmail DB write gate wait. operation='{normalizedOperation}'");
            _gate.Wait(cancellationToken);
            AppLogger.KillmailImportDebug($"Killmail DB write gate acquired. operation='{normalizedOperation}'");
            return new Releaser(this, normalizedOperation);
        }

        public async Task<IDisposable> EnterAsync(string operationName, CancellationToken cancellationToken = default)
        {
            var normalizedOperation = NormalizeOperationName(operationName);
            AppLogger.KillmailImportDebug($"Killmail DB write gate wait. operation='{normalizedOperation}'");
            await _gate.WaitAsync(cancellationToken);
            AppLogger.KillmailImportDebug($"Killmail DB write gate acquired. operation='{normalizedOperation}'");
            return new Releaser(this, normalizedOperation);
        }

        private static string NormalizeOperationName(string operationName)
        {
            return string.IsNullOrWhiteSpace(operationName)
                ? "unspecified"
                : operationName.Trim();
        }

        private void Release(string operationName)
        {
            _gate.Release();
            AppLogger.KillmailImportDebug($"Killmail DB write gate released. operation='{operationName}'");
        }

        private sealed class Releaser : IDisposable
        {
            private readonly KillmailDbWriteGate _owner;
            private readonly string _operationName;
            private int _disposed;

            public Releaser(KillmailDbWriteGate owner, string operationName)
            {
                _owner = owner;
                _operationName = operationName;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                {
                    return;
                }

                _owner.Release(_operationName);
            }
        }
    }
}
