using PitmastersGrill.Persistence;
using System;
using System.Linq;
using System.Windows.Threading;

namespace PitmastersGrill.Diagnostics
{
    public sealed class MainWindowDiagnostics : IDisposable
    {
        private readonly DispatcherTimer _uiHeartbeatTimer;
        private DateTime _lastUiHeartbeatUtc = DateTime.UtcNow;

        public MainWindowDiagnostics(Dispatcher dispatcher)
        {
            _uiHeartbeatTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
            {
                Interval = TimeSpan.FromSeconds(5)
            };

            _uiHeartbeatTimer.Tick += OnHeartbeatTick;
            _uiHeartbeatTimer.Start();
        }

        public void Dispose()
        {
            _uiHeartbeatTimer.Stop();
            _uiHeartbeatTimer.Tick -= OnHeartbeatTick;
        }

        public void ClipboardProcessStart() => Debug("Clipboard handler start.");

        public void ClipboardNoText() => Debug("Clipboard handler aborted. Clipboard contains no text.");

        public void ClipboardTextRead(string? rawClipboardText)
        {
            if (!AppLogger.IsDebugEnabled)
            {
                return;
            }

            var charCount = rawClipboardText?.Length ?? 0;
            var nonEmptyLines = (rawClipboardText ?? string.Empty)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(x => x.Trim())
                .Count(x => !string.IsNullOrWhiteSpace(x));

            Debug($"Clipboard text read. charCount={charCount} nonEmptyLines={nonEmptyLines}");
        }

        public void ClipboardReadFailed(string? message) =>
            Debug($"Clipboard read failed. message={Sanitize(message)}");

        public void ClipboardIntakeIgnored(string? reason) =>
            Debug($"Clipboard intake ignored. reason={Sanitize(reason)}");

        public void ClipboardIntakeAccepted(int parsedNamesCount, bool retryReset) =>
            Debug($"Clipboard intake accepted. parsedNames={parsedNamesCount} retryReset={retryReset}");

        public void ClipboardProcessEnd() => Debug("Clipboard handler end.");

        public void BoardProcessRequested(bool retryPass, int incomingCount) =>
            Debug($"Board process requested. retryPass={retryPass} incomingCount={incomingCount}");

        public void BoardProcessAbortedNoCleanNames() =>
            Debug("Board process aborted. No cleaned names remained after normalization.");

        public void CacheHydrateComplete(bool retryPass, int cleanedNames, int identities, int stats, long elapsedMs) =>
            Debug($"Cache hydrate complete. retryPass={retryPass} cleanedNames={cleanedNames} identities={identities} stats={stats} elapsedMs={elapsedMs}");

        public void InitialBoardBuildStart(int characterNames, int identities, int stats) =>
            Debug($"Initial board build start. characterNames={characterNames} identities={identities} stats={stats}");

        public void InitialBoardBuildComplete(int rowCount, long elapsedMs) =>
            Debug($"Initial board build complete. rowCount={rowCount} elapsedMs={elapsedMs}");

        public void BoardProcessStart(int generation, int rowCount, bool retryPass)
        {
            var message = $"Board process start. generation={generation} rowCount={rowCount} retryPass={retryPass}";
            Debug(message);
            DebugTraceWriter.WriteLine(message);
        }

        public void BoardProcessSettled(int generation, long elapsedMs)
        {
            var message = $"Board process settled. generation={generation} elapsedMs={elapsedMs}";
            Debug(message);
            DebugTraceWriter.WriteLine(message);
        }

        public void BoardProcessSuperseded(int generation, long elapsedMs)
        {
            var message = $"Board process superseded. generation={generation} elapsedMs={elapsedMs}";
            Debug(message);
            DebugTraceWriter.WriteLine(message);
        }

        public void FinalizeSkipped(int generation, int activeGeneration) =>
            Debug($"Finalize skipped. generation={generation} activeGeneration={activeGeneration}");

        public void BoardProcessFinalizedComplete(int generation, int completeCount, int partialCount, int retryableCount) =>
            Debug($"Board process finalized complete. generation={generation} complete={completeCount} partial={partialCount} retryable={retryableCount}");

        public void BoardProcessRetryLimitReached(int generation, int retryableCount, int partialCount, int attempts) =>
            Debug($"Board process finalized with retry limit reached. generation={generation} retryable={retryableCount} partial={partialCount} attempts={attempts}");

        public void BoardProcessRequiresRetry(int generation, int retryableCount, int partialCount, int attempts) =>
            Debug($"Board process requires retry. generation={generation} retryable={retryableCount} partial={partialCount} attempts={attempts}");

        public void RetryScheduleIgnoredAlreadyScheduled() =>
            Debug("Retry schedule request ignored because a retry is already scheduled.");

        public void RetryScheduleIgnoredNoRows() =>
            Debug("Retry schedule request ignored because no retryable rows remained.");

        public void RetryScheduled(int attempt, int delayMs, int rowCount)
        {
            var message = $"Retry scheduled. attempt={attempt} delayMs={delayMs} rowCount={rowCount}";
            Debug(message);
            DebugTraceWriter.WriteLine($"board population retry scheduled: attempt={attempt}, delayMs={delayMs}, rowCount={rowCount}");
        }

        public void RetryDelayCancelledBeforeDispatch() =>
            Debug("Retry delay cancelled before dispatch.");

        public void RetryDispatchSkippedBecauseCancelled() =>
            Debug("Retry dispatch skipped because the token was already cancelled.");

        public void RetryDispatchFired(int attempt) =>
            Debug($"Retry dispatch fired. attempt={attempt}");

        public void RetryDelayTaskCanceled() =>
            Debug("Retry delay task canceled.");

        public void RetryPassSkipped(int generation, int readyRowCount) =>
            Debug($"Retry pass skipped. generation={generation} readyRowCount={readyRowCount}");

        public void RetryPassStart(int generation, int rowCount)
        {
            var message = $"Retry pass start. generation={generation} rowCount={rowCount}";
            Debug(message);
            DebugTraceWriter.WriteLine($"board retry pass start: generation={generation}, rowCount={rowCount}");
        }

        public void RetryPassComplete(int generation, int rowCount, long elapsedMs) =>
            Debug($"Retry pass complete. generation={generation} rowCount={rowCount} elapsedMs={elapsedMs}");

        public void RetryCancellationRequested() =>
            Debug("Retry cancellation requested.");

        public void BoardPopulationTrackingReset() =>
            Debug("Board population tracking reset.");

        public void ClearBoardStart(int clearedRowCount) =>
            Debug($"Clear board start. removedRows={clearedRowCount}");

        public void ClearBoardComplete() =>
            Debug("Clear board complete.");

        public long GetUiHeartbeatAgeMs()
        {
            var age = DateTime.UtcNow - _lastUiHeartbeatUtc;
            return age < TimeSpan.Zero ? 0 : (long)age.TotalMilliseconds;
        }

        private void OnHeartbeatTick(object? sender, EventArgs e)
        {
            _lastUiHeartbeatUtc = DateTime.UtcNow;
        }

        private void Debug(string message)
        {
            if (!AppLogger.IsDebugEnabled)
            {
                return;
            }

            var annotated = $"{message} uiHeartbeatAgeMs={GetUiHeartbeatAgeMs()}";
            AppLogger.UiDebug(annotated);
            DebugTraceWriter.WriteLine(annotated);
        }

        private static string Sanitize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
        }
    }
}
