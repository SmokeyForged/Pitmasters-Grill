using PitmastersGrill.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersGrill.Services
{
    public sealed class BoardPopulationFinalizeDecision
    {
        public bool IsComplete { get; init; }
        public bool RetryLimitReached { get; init; }
        public bool ShouldScheduleRetry { get; init; }
        public int RetryableCount { get; init; }
        public int CompleteCount { get; init; }
        public int PartialCount { get; init; }
        public string StatusText { get; init; } = string.Empty;
        public BoardPopulationStatusKind StatusKind { get; init; } = BoardPopulationStatusKind.Neutral;
    }

    public sealed class BoardPopulationPassController
    {
        private const int MaxConcurrentRowProcessors = 6;

        private readonly BoardPopulationRetryPolicy _boardPopulationRetryPolicy;

        public BoardPopulationPassController(BoardPopulationRetryPolicy boardPopulationRetryPolicy)
        {
            _boardPopulationRetryPolicy = boardPopulationRetryPolicy;
        }

        public async Task ProcessRowBatchAsync(
            IReadOnlyCollection<PilotBoardRow> rows,
            int generation,
            Func<PilotBoardRow, SemaphoreSlim, int, Task> processSingleRowAsync)
        {
            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            if (processSingleRowAsync == null)
            {
                throw new ArgumentNullException(nameof(processSingleRowAsync));
            }

            using var semaphore = new SemaphoreSlim(MaxConcurrentRowProcessors);

            var tasks = rows
                .Select(row => processSingleRowAsync(row, semaphore, generation))
                .ToList();

            await Task.WhenAll(tasks);
        }

        public BoardPopulationFinalizeDecision BuildFinalizeDecision(
            IReadOnlyCollection<PilotBoardRow> currentRows,
            int activeBoardPopulationRetryAttempt,
            int maxBoardPopulationRetryAttempts)
        {
            if (currentRows == null)
            {
                throw new ArgumentNullException(nameof(currentRows));
            }

            var evaluation = _boardPopulationRetryPolicy.EvaluateRows(currentRows);
            var retryableCount = evaluation.RetryableCount;
            var completeCount = evaluation.CompleteCount;
            var partialCount = evaluation.PartialCount;

            if (retryableCount <= 0)
            {
                var completionText = partialCount > 0
                    ? $"Board population complete ({completeCount} complete, {partialCount} partial)"
                    : "Board population complete";

                return new BoardPopulationFinalizeDecision
                {
                    IsComplete = true,
                    RetryableCount = retryableCount,
                    CompleteCount = completeCount,
                    PartialCount = partialCount,
                    StatusText = completionText,
                    StatusKind = BoardPopulationStatusKind.Success
                };
            }

            if (activeBoardPopulationRetryAttempt >= maxBoardPopulationRetryAttempts)
            {
                return new BoardPopulationFinalizeDecision
                {
                    RetryLimitReached = true,
                    RetryableCount = retryableCount,
                    CompleteCount = completeCount,
                    PartialCount = partialCount,
                    StatusText = $"Board population incomplete — retry limit reached ({retryableCount} retryable, {partialCount} partial)",
                    StatusKind = BoardPopulationStatusKind.Error
                };
            }

            return new BoardPopulationFinalizeDecision
            {
                ShouldScheduleRetry = true,
                RetryableCount = retryableCount,
                CompleteCount = completeCount,
                PartialCount = partialCount,
                StatusText = $"Board population delayed by source throttling or temporary failures ({retryableCount} retryable, {partialCount} partial)",
                StatusKind = BoardPopulationStatusKind.Warning
            };
        }
    }
}
