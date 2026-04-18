using PitmastersGrill.Models;
using System;

namespace PitmastersGrill.Services
{
    public class PilotBoardRowDetailFormatter
    {
        private readonly BoardPopulationRetryPolicy _boardPopulationRetryPolicy;

        public PilotBoardRowDetailFormatter(BoardPopulationRetryPolicy boardPopulationRetryPolicy)
        {
            _boardPopulationRetryPolicy = boardPopulationRetryPolicy ?? throw new ArgumentNullException(nameof(boardPopulationRetryPolicy));
        }

        public string GetCorpDisplayText(PilotBoardRow row)
        {
            if (row == null)
            {
                return "Full Corp: unresolved";
            }

            if (row.IdentityStage == EnrichmentStageState.NotFound)
            {
                return "Full Corp: not found on zKill or ESI exact match";
            }

            if (!string.IsNullOrWhiteSpace(row.CorpName))
            {
                return $"Full Corp: {row.CorpName}";
            }

            if (row.AffiliationStage == EnrichmentStageState.Success || row.AffiliationStage == EnrichmentStageState.NotFound)
            {
                return "Full Corp: unavailable after affiliation check";
            }

            if (row.AffiliationStage == EnrichmentStageState.Throttled || row.AffiliationStage == EnrichmentStageState.TemporaryFailure)
            {
                return $"Full Corp: delayed ({row.AffiliationStatusDetail})";
            }

            if (row.AffiliationStage == EnrichmentStageState.PermanentFailure)
            {
                return $"Full Corp: unavailable ({row.AffiliationStatusDetail})";
            }

            if (!string.IsNullOrWhiteSpace(row.CharacterId))
            {
                return "Full Corp: resolved, enrichment pending";
            }

            return "Full Corp: unresolved";
        }

        public string GetAllianceDisplayText(PilotBoardRow row)
        {
            if (row == null)
            {
                return "Full Alliance: unresolved";
            }

            if (row.IdentityStage == EnrichmentStageState.NotFound)
            {
                return "Full Alliance: not found on zKill or ESI exact match";
            }

            if (!string.IsNullOrWhiteSpace(row.AllianceName))
            {
                return $"Full Alliance: {row.AllianceName}";
            }

            if (row.AffiliationStage == EnrichmentStageState.Success || row.AffiliationStage == EnrichmentStageState.NotFound)
            {
                return "Full Alliance: none";
            }

            if (row.AffiliationStage == EnrichmentStageState.Throttled || row.AffiliationStage == EnrichmentStageState.TemporaryFailure)
            {
                return $"Full Alliance: delayed ({row.AffiliationStatusDetail})";
            }

            if (row.AffiliationStage == EnrichmentStageState.PermanentFailure)
            {
                return $"Full Alliance: unavailable ({row.AffiliationStatusDetail})";
            }

            if (!string.IsNullOrWhiteSpace(row.CharacterId))
            {
                return "Full Alliance: resolved, enrichment pending";
            }

            return "Full Alliance: unresolved";
        }

        public string GetFreshnessDisplayText(PilotBoardRow row)
        {
            if (row == null)
            {
                return "Freshness: unresolved";
            }

            if (row.KnownCynoOverride)
            {
                return "Freshness: known-cyno override applied";
            }

            if (row.BaitOverride)
            {
                return "Freshness: bait override applied";
            }

            if (row.IdentityStage == EnrichmentStageState.NotFound)
            {
                return "Freshness: terminal miss cached";
            }

            if (_boardPopulationRetryPolicy.HasRetryableStage(row) && row.NextRetryAtUtc.HasValue)
            {
                return $"Freshness: retry scheduled for {row.NextRetryAtUtc.Value:O}";
            }

            if (string.Equals(row.ResolverConfidence, "esi_exact_fallback", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(row.ResolvedAtUtc))
                {
                    return $"Freshness: resolved by ESI exact fallback at {row.ResolvedAtUtc}";
                }

                return "Freshness: resolved by ESI exact fallback";
            }

            if (row.StatsStage == EnrichmentStageState.NotFound)
            {
                return "Freshness: identity resolved; stats unavailable from current sources";
            }

            if (!string.IsNullOrWhiteSpace(row.ResolvedAtUtc))
            {
                return $"Freshness: {row.ResolvedAtUtc}";
            }

            return "Freshness: unresolved";
        }
    }
}
