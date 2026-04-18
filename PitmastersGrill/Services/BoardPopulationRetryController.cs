using PitmastersGrill.Diagnostics;
using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace PitmastersGrill.Services
{
    public sealed class BoardPopulationRetryController
    {
        private readonly BoardPopulationRetryPolicy _boardPopulationRetryPolicy;
        private readonly MainWindowDiagnostics _diagnostics;
        private readonly int _defaultBoardPopulationRetryDelaySeconds;

        private CancellationTokenSource? _boardPopulationRetryCts;
        private bool _isBoardPopulationIncomplete;
        private bool _isRetryScheduled;
        private int _retryAttempt;

        public BoardPopulationRetryController(
            BoardPopulationRetryPolicy boardPopulationRetryPolicy,
            MainWindowDiagnostics diagnostics,
            int defaultBoardPopulationRetryDelaySeconds)
        {
            _boardPopulationRetryPolicy = boardPopulationRetryPolicy ?? throw new ArgumentNullException(nameof(boardPopulationRetryPolicy));
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            _defaultBoardPopulationRetryDelaySeconds = defaultBoardPopulationRetryDelaySeconds;
        }

        public bool IsBoardPopulationIncomplete => _isBoardPopulationIncomplete;
        public bool IsRetryScheduled => _isRetryScheduled;
        public int RetryAttempt => _retryAttempt;

        public string GetClipboardComparisonText(string lastProcessedClipboardText)
        {
            return _isBoardPopulationIncomplete ? string.Empty : (lastProcessedClipboardText ?? string.Empty);
        }

        public void MarkComplete()
        {
            _isBoardPopulationIncomplete = false;
            _isRetryScheduled = false;
            _retryAttempt = 0;
        }

        public void MarkIncomplete()
        {
            _isBoardPopulationIncomplete = true;
        }

        public void CancelRetry()
        {
            try
            {
                _boardPopulationRetryCts?.Cancel();
                _diagnostics.RetryCancellationRequested();
            }
            catch
            {
            }

            _isRetryScheduled = false;
        }

        public void ResetTracking()
        {
            _isBoardPopulationIncomplete = false;
            _isRetryScheduled = false;
            _retryAttempt = 0;
            _diagnostics.BoardPopulationTrackingReset();
        }

        public void ScheduleRetry(
            IReadOnlyCollection<PilotBoardRow> currentRows,
            Dispatcher dispatcher,
            Action<string, BoardPopulationStatusKind> updateBoardPopulationStatus,
            Func<Task> processRetryPassAsync)
        {
            if (currentRows == null)
            {
                throw new ArgumentNullException(nameof(currentRows));
            }

            if (dispatcher == null)
            {
                throw new ArgumentNullException(nameof(dispatcher));
            }

            if (updateBoardPopulationStatus == null)
            {
                throw new ArgumentNullException(nameof(updateBoardPopulationStatus));
            }

            if (processRetryPassAsync == null)
            {
                throw new ArgumentNullException(nameof(processRetryPassAsync));
            }

            if (_isRetryScheduled)
            {
                _diagnostics.RetryScheduleIgnoredAlreadyScheduled();
                return;
            }

            var retryableRows = currentRows.Where(_boardPopulationRetryPolicy.HasRetryableStage).ToList();
            if (retryableRows.Count == 0)
            {
                _diagnostics.RetryScheduleIgnoredNoRows();
                return;
            }

            _isRetryScheduled = true;
            _retryAttempt++;

            var attempt = _retryAttempt;
            var nowUtc = DateTime.UtcNow;
            var earliestRetryAtUtc = retryableRows
                .Select(row => row.NextRetryAtUtc ?? nowUtc.AddSeconds(_defaultBoardPopulationRetryDelaySeconds))
                .OrderBy(value => value)
                .First();

            var delay = earliestRetryAtUtc - nowUtc;
            if (delay < TimeSpan.Zero)
            {
                delay = TimeSpan.Zero;
            }

            _boardPopulationRetryCts?.Cancel();
            _boardPopulationRetryCts = new CancellationTokenSource();
            var retryToken = _boardPopulationRetryCts.Token;

            _diagnostics.RetryScheduled(
                attempt,
                (int)delay.TotalMilliseconds,
                retryableRows.Count);

            DebugTraceWriter.WriteLine(
                $"board population retry scheduled: attempt={attempt}, delayMs={(int)delay.TotalMilliseconds}, rowCount={retryableRows.Count}");

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delay, retryToken);

                    if (retryToken.IsCancellationRequested)
                    {
                        _diagnostics.RetryDelayCancelledBeforeDispatch();
                        return;
                    }

                    await dispatcher.InvokeAsync(() =>
                    {
                        if (retryToken.IsCancellationRequested)
                        {
                            _diagnostics.RetryDispatchSkippedBecauseCancelled();
                            return;
                        }

                        _diagnostics.RetryDispatchFired(attempt);

                        updateBoardPopulationStatus(
                            "Board population retrying delayed rows",
                            BoardPopulationStatusKind.Warning);

                        _ = processRetryPassAsync();
                    });
                }
                catch (OperationCanceledException)
                {
                    _diagnostics.RetryDelayTaskCanceled();
                }
                finally
                {
                    _isRetryScheduled = false;
                }
            });
        }

        public async Task ProcessRetryPassAsync(
            IReadOnlyCollection<PilotBoardRow> currentRows,
            Func<IDisposable> beginForegroundPriority,
            Func<IReadOnlyCollection<PilotBoardRow>, int, Task> processRowBatchAsync,
            Func<int> getProcessingGeneration,
            Action updateLastRefreshed,
            Action<int> finalizeBoardPopulationPass)
        {
            if (currentRows == null)
            {
                throw new ArgumentNullException(nameof(currentRows));
            }

            if (beginForegroundPriority == null)
            {
                throw new ArgumentNullException(nameof(beginForegroundPriority));
            }

            if (processRowBatchAsync == null)
            {
                throw new ArgumentNullException(nameof(processRowBatchAsync));
            }

            if (getProcessingGeneration == null)
            {
                throw new ArgumentNullException(nameof(getProcessingGeneration));
            }

            if (updateLastRefreshed == null)
            {
                throw new ArgumentNullException(nameof(updateLastRefreshed));
            }

            if (finalizeBoardPopulationPass == null)
            {
                throw new ArgumentNullException(nameof(finalizeBoardPopulationPass));
            }

            using var foregroundPriority = beginForegroundPriority();

            var generation = getProcessingGeneration();
            var nowUtc = DateTime.UtcNow;
            var retryRows = currentRows
                .Where(row => _boardPopulationRetryPolicy.IsRetryReady(row, nowUtc))
                .ToList();

            if (retryRows.Count == 0)
            {
                _diagnostics.RetryPassSkipped(generation, 0);
                finalizeBoardPopulationPass(generation);
                return;
            }

            _diagnostics.RetryPassStart(generation, retryRows.Count);

            DebugTraceWriter.WriteLine(
                $"board retry pass start: generation={generation}, rowCount={retryRows.Count}");

            var retryStopwatch = System.Diagnostics.Stopwatch.StartNew();
            await processRowBatchAsync(retryRows, generation);
            retryStopwatch.Stop();

            _diagnostics.RetryPassComplete(generation, retryRows.Count, retryStopwatch.ElapsedMilliseconds);

            updateLastRefreshed();
            finalizeBoardPopulationPass(generation);
        }
    }
}