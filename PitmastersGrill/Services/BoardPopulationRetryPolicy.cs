using PitmastersGrill.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PitmastersGrill.Services
{
    public class BoardPopulationRetryPolicy
    {
        public BoardPopulationPassEvaluation EvaluateRows(IEnumerable<PilotBoardRow> rows)
        {
            if (rows == null)
            {
                return new BoardPopulationPassEvaluation(0, 0, 0);
            }

            var rowList = rows.Where(row => row != null).ToList();

            return new BoardPopulationPassEvaluation(
                rowList.Count(HasRetryableStage),
                rowList.Count(IsCompleteRow),
                rowList.Count(IsPartialRow));
        }

        public int CountRowsNeedingRetry(IEnumerable<PilotBoardRow> rows)
        {
            if (rows == null)
            {
                return 0;
            }

            return rows.Count(HasRetryableStage);
        }

        public bool HasRetryableStage(PilotBoardRow row)
        {
            if (row == null)
            {
                return false;
            }

            return IsRetryableStage(row.IdentityStage)
                || IsRetryableStage(row.AffiliationStage)
                || IsRetryableStage(row.StatsStage);
        }

        public bool IsRetryableStage(EnrichmentStageState stage)
        {
            return stage == EnrichmentStageState.Throttled || stage == EnrichmentStageState.TemporaryFailure;
        }

        public bool IsRetryReady(PilotBoardRow row, DateTime nowUtc)
        {
            if (!HasRetryableStage(row))
            {
                return false;
            }

            return !row.NextRetryAtUtc.HasValue || row.NextRetryAtUtc.Value <= nowUtc;
        }

        public bool IsCompleteRow(PilotBoardRow row)
        {
            if (row == null)
            {
                return false;
            }

            return row.IdentityStage == EnrichmentStageState.Success
                && row.AffiliationStage == EnrichmentStageState.Success
                && row.StatsStage == EnrichmentStageState.Success;
        }

        public bool IsPartialRow(PilotBoardRow row)
        {
            if (row == null)
            {
                return false;
            }

            if (IsCompleteRow(row) || HasRetryableStage(row))
            {
                return false;
            }

            return row.IdentityStage == EnrichmentStageState.Success
                || row.AffiliationStage == EnrichmentStageState.Success
                || row.StatsStage == EnrichmentStageState.Success
                || row.IdentityStage == EnrichmentStageState.NotFound;
        }
    }

    public class BoardPopulationPassEvaluation
    {
        public BoardPopulationPassEvaluation(int retryableCount, int completeCount, int partialCount)
        {
            RetryableCount = retryableCount;
            CompleteCount = completeCount;
            PartialCount = partialCount;
        }

        public int RetryableCount { get; }

        public int CompleteCount { get; }

        public int PartialCount { get; }
    }
}
