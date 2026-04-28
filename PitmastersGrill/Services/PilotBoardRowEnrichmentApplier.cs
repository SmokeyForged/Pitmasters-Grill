using PitmastersGrill.Models;
using System;
using System.Globalization;
using System.Linq;

namespace PitmastersGrill.Services
{
    public class PilotBoardRowEnrichmentApplier
    {
        private readonly int _defaultRetryDelaySeconds;

        public PilotBoardRowEnrichmentApplier(int defaultRetryDelaySeconds)
        {
            _defaultRetryDelaySeconds = defaultRetryDelaySeconds;
        }

        public void ApplyIdentityOutcomeToRow(PilotBoardRow row, ProviderOutcome<ResolverCacheEntry> outcome)
        {
            if (outcome.Value != null)
            {
                ApplyIdentityToRow(row, outcome.Value);
            }

            row.IdentityStage = MapOutcomeToStageState(outcome.Kind);
            row.IdentityStatusDetail = BuildOutcomeDetail(outcome, "identity");
            row.IdentityRetryAtUtc = GetRetryAtUtc(outcome);

            if (outcome.Kind == ProviderOutcomeKind.Throttled)
            {
                row.LastThrottleProvider = outcome.ProviderName;
            }

            if (outcome.Kind == ProviderOutcomeKind.NotFound)
            {
                row.AffiliationStage = EnrichmentStageState.Skipped;
                row.AffiliationStatusDetail = "Affiliation skipped after terminal miss";
                row.AffiliationRetryAtUtc = null;
                row.StatsStage = EnrichmentStageState.Skipped;
                row.StatsStatusDetail = "Stats skipped after terminal miss";
                row.StatsRetryAtUtc = null;
            }

            RecalculateRetryMetadata(row);
        }

        public void ApplyAffiliationOutcomeToRow(PilotBoardRow row, ProviderOutcome<ResolverCacheEntry> outcome)
        {
            if (outcome.Value != null)
            {
                ApplyIdentityToRow(row, outcome.Value);
            }

            row.AffiliationStage = MapOutcomeToStageState(outcome.Kind);
            row.AffiliationStatusDetail = BuildOutcomeDetail(outcome, "affiliation");
            row.AffiliationRetryAtUtc = GetRetryAtUtc(outcome);

            if (outcome.Kind == ProviderOutcomeKind.Throttled)
            {
                row.LastThrottleProvider = outcome.ProviderName;
            }

            RecalculateRetryMetadata(row);
        }

        public void ApplyStatsOutcomeToRow(PilotBoardRow row, ProviderOutcome<StatsCacheEntry> outcome)
        {
            if (outcome.Value != null)
            {
                ApplyStatsToRow(row, outcome.Value);
            }

            row.StatsStage = MapOutcomeToStageState(outcome.Kind);
            row.StatsStatusDetail = BuildOutcomeDetail(outcome, "stats");
            row.StatsRetryAtUtc = GetRetryAtUtc(outcome);

            if (outcome.Kind == ProviderOutcomeKind.Throttled)
            {
                row.LastThrottleProvider = outcome.ProviderName;
            }

            RecalculateRetryMetadata(row);
        }

        public void RecalculateRetryMetadata(PilotBoardRow row)
        {
            row.NextRetryAtUtc = GetEarlierRetryAtUtc(row.IdentityRetryAtUtc, row.AffiliationRetryAtUtc, row.StatsRetryAtUtc);

            if (!HasRetryableStage(row))
            {
                row.NextRetryAtUtc = null;

                if (row.IdentityStage != EnrichmentStageState.Throttled &&
                    row.AffiliationStage != EnrichmentStageState.Throttled &&
                    row.StatsStage != EnrichmentStageState.Throttled)
                {
                    row.LastThrottleProvider = string.Empty;
                }
            }
        }

        public bool HasIdentityStageChange(ResolverCacheEntry before, ResolverCacheEntry after)
        {
            return !string.Equals(before.CharacterId, after.CharacterId, StringComparison.Ordinal)
                || !string.Equals(before.AllianceId, after.AllianceId, StringComparison.Ordinal)
                || !string.Equals(before.CorpId, after.CorpId, StringComparison.Ordinal)
                || !string.Equals(before.CorpName, after.CorpName, StringComparison.Ordinal)
                || !string.Equals(before.CorpTicker, after.CorpTicker, StringComparison.Ordinal)
                || !string.Equals(before.AllianceName, after.AllianceName, StringComparison.Ordinal)
                || !string.Equals(before.AllianceTicker, after.AllianceTicker, StringComparison.Ordinal)
                || !string.Equals(before.ResolverConfidence, after.ResolverConfidence, StringComparison.Ordinal)
                || !string.Equals(before.AffiliationCheckedAtUtc, after.AffiliationCheckedAtUtc, StringComparison.Ordinal);
        }

        private static void ApplyIdentityToRow(PilotBoardRow row, ResolverCacheEntry identity)
        {
            row.CharacterId = identity.CharacterId;
            row.AllianceId = identity.AllianceId;
            row.AllianceName = identity.AllianceName;
            row.AllianceTicker = identity.AllianceTicker;
            row.CorpId = identity.CorpId;
            row.CorpName = identity.CorpName;
            row.CorpTicker = identity.CorpTicker;
            row.IsResolved = !string.IsNullOrWhiteSpace(identity.CharacterId);
            row.ResolverConfidence = identity.ResolverConfidence;
            row.ResolvedAtUtc = identity.ResolvedAtUtc;
            row.AffiliationCheckedAtUtc = identity.AffiliationCheckedAtUtc;
        }

        private static void ApplyStatsToRow(PilotBoardRow row, StatsCacheEntry stats)
        {
            row.KillCount = stats.KillCount;
            row.LossCount = stats.LossCount;
            row.AvgAttackersWhenAttacking = stats.AvgAttackersWhenAttacking > 0
                ? Math.Round(stats.AvgAttackersWhenAttacking, 0, MidpointRounding.AwayFromZero)
                : null;
            row.LastPublicCynoCapableHull = stats.LastPublicCynoCapableHull;
            row.LastShipSeenName = stats.LastShipSeenName;
            row.LastShipSeenAtUtc = stats.LastShipSeenAtUtc;
            row.LastShipSeenDateDisplay = FormatLastSeenDate(stats.LastShipSeenAtUtc);
        }

        private static string FormatLastSeenDate(string utcValue)
        {
            if (string.IsNullOrWhiteSpace(utcValue))
            {
                return string.Empty;
            }

            if (!DateTime.TryParse(
                    utcValue,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsed))
            {
                return string.Empty;
            }

            return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static EnrichmentStageState MapOutcomeToStageState(ProviderOutcomeKind outcomeKind)
        {
            return outcomeKind switch
            {
                ProviderOutcomeKind.Success => EnrichmentStageState.Success,
                ProviderOutcomeKind.NotFound => EnrichmentStageState.NotFound,
                ProviderOutcomeKind.Throttled => EnrichmentStageState.Throttled,
                ProviderOutcomeKind.TemporaryFailure => EnrichmentStageState.TemporaryFailure,
                ProviderOutcomeKind.PermanentFailure => EnrichmentStageState.PermanentFailure,
                ProviderOutcomeKind.Skipped => EnrichmentStageState.Skipped,
                _ => EnrichmentStageState.NotStarted
            };
        }

        private DateTime? GetRetryAtUtc<T>(ProviderOutcome<T> outcome)
        {
            if (outcome.Kind == ProviderOutcomeKind.Throttled)
            {
                return outcome.RetryAfterUtc ?? DateTime.UtcNow.AddSeconds(_defaultRetryDelaySeconds);
            }

            if (outcome.Kind == ProviderOutcomeKind.TemporaryFailure)
            {
                return outcome.RetryAfterUtc ?? DateTime.UtcNow.AddSeconds(_defaultRetryDelaySeconds);
            }

            return null;
        }

        private static string BuildOutcomeDetail<T>(ProviderOutcome<T> outcome, string stageName)
        {
            if (!string.IsNullOrWhiteSpace(outcome.Detail))
            {
                return outcome.Detail;
            }

            return outcome.Kind switch
            {
                ProviderOutcomeKind.Success => $"{stageName} succeeded",
                ProviderOutcomeKind.NotFound => $"{stageName} returned not found",
                ProviderOutcomeKind.Throttled => $"{stageName} delayed by throttling",
                ProviderOutcomeKind.TemporaryFailure => $"{stageName} temporarily failed",
                ProviderOutcomeKind.PermanentFailure => $"{stageName} permanently failed",
                ProviderOutcomeKind.Skipped => $"{stageName} skipped",
                _ => $"{stageName} not started"
            };
        }

        private static DateTime? GetEarlierRetryAtUtc(params DateTime?[] retryAtCandidates)
        {
            return retryAtCandidates
                .Where(candidate => candidate.HasValue)
                .Select(candidate => candidate!.Value)
                .OrderBy(candidate => candidate)
                .Cast<DateTime?>()
                .FirstOrDefault();
        }

        private static bool HasRetryableStage(PilotBoardRow row)
        {
            return IsRetryableStage(row.IdentityStage)
                || IsRetryableStage(row.AffiliationStage)
                || IsRetryableStage(row.StatsStage);
        }

        private static bool IsRetryableStage(EnrichmentStageState stage)
        {
            return stage == EnrichmentStageState.Throttled ||
                   stage == EnrichmentStageState.TemporaryFailure;
        }
    }
}
