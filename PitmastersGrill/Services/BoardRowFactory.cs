using PitmastersGrill.Models;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PitmastersGrill.Services
{
    public class BoardRowFactory
    {
        public List<PilotBoardRow> CreateRows(
            List<string> pilotNames,
            Dictionary<string, ResolverCacheEntry> cachedIdentities,
            Dictionary<string, StatsCacheEntry> cachedStats)
        {
            var rows = new List<PilotBoardRow>();

            foreach (var name in pilotNames)
            {
                if (cachedIdentities.TryGetValue(name, out var cachedIdentity))
                {
                    var hasStats = cachedStats.TryGetValue(cachedIdentity.CharacterId, out var statsEntry);
                    var row = new PilotBoardRow
                    {
                        CharacterName = cachedIdentity.CharacterName,
                        CharacterId = cachedIdentity.CharacterId,
                        AllianceName = cachedIdentity.AllianceName,
                        AllianceTicker = cachedIdentity.AllianceTicker,
                        CorpName = cachedIdentity.CorpName,
                        CorpTicker = cachedIdentity.CorpTicker,
                        KillCount = hasStats ? statsEntry!.KillCount : null,
                        LossCount = hasStats ? statsEntry!.LossCount : null,
                        AvgAttackersWhenAttacking = hasStats && statsEntry!.AvgAttackersWhenAttacking > 0
                            ? statsEntry.AvgAttackersWhenAttacking
                            : null,
                        LastPublicCynoCapableHull = hasStats ? statsEntry!.LastPublicCynoCapableHull : "",
                        LastShipSeenName = hasStats ? statsEntry!.LastShipSeenName : "",
                        LastShipSeenAtUtc = hasStats ? statsEntry!.LastShipSeenAtUtc : "",
                        LastShipSeenDateDisplay = hasStats ? FormatLastSeenDate(statsEntry!.LastShipSeenAtUtc) : "",
                        IsResolved = !string.IsNullOrWhiteSpace(cachedIdentity.CharacterId),
                        ResolverConfidence = cachedIdentity.ResolverConfidence,
                        ResolvedAtUtc = cachedIdentity.ResolvedAtUtc,
                        AffiliationCheckedAtUtc = cachedIdentity.AffiliationCheckedAtUtc,
                        IdentityStage = DetermineIdentityStage(cachedIdentity),
                        AffiliationStage = DetermineAffiliationStage(cachedIdentity),
                        StatsStage = DetermineStatsStage(cachedIdentity, hasStats),
                        IdentityStatusDetail = DetermineIdentityDetail(cachedIdentity),
                        AffiliationStatusDetail = DetermineAffiliationDetail(cachedIdentity),
                        StatsStatusDetail = hasStats ? "Stats available from cache" : "Stats not started"
                    };

                    rows.Add(row);
                    continue;
                }

                rows.Add(new PilotBoardRow
                {
                    CharacterName = name,
                    CharacterId = "",
                    AllianceName = "",
                    AllianceTicker = "",
                    CorpName = "",
                    CorpTicker = "",
                    KillCount = null,
                    LossCount = null,
                    AvgAttackersWhenAttacking = null,
                    LastPublicCynoCapableHull = "",
                    LastShipSeenName = "",
                    LastShipSeenAtUtc = "",
                    LastShipSeenDateDisplay = "",
                    IsResolved = false,
                    ResolverConfidence = "",
                    ResolvedAtUtc = "",
                    AffiliationCheckedAtUtc = "",
                    IdentityStage = EnrichmentStageState.NotStarted,
                    AffiliationStage = EnrichmentStageState.NotStarted,
                    StatsStage = EnrichmentStageState.NotStarted,
                    IdentityStatusDetail = "Identity not started",
                    AffiliationStatusDetail = "Affiliation not started",
                    StatsStatusDetail = "Stats not started"
                });
            }

            return rows;
        }

        private static EnrichmentStageState DetermineIdentityStage(ResolverCacheEntry identity)
        {
            if (identity == null)
            {
                return EnrichmentStageState.NotStarted;
            }

            if (string.Equals(identity.ResolverConfidence, "not_found", StringComparison.OrdinalIgnoreCase))
            {
                return EnrichmentStageState.NotFound;
            }

            if (!string.IsNullOrWhiteSpace(identity.CharacterId))
            {
                return EnrichmentStageState.Success;
            }

            return EnrichmentStageState.NotStarted;
        }

        private static EnrichmentStageState DetermineAffiliationStage(ResolverCacheEntry identity)
        {
            if (identity == null)
            {
                return EnrichmentStageState.NotStarted;
            }

            if (string.Equals(identity.ResolverConfidence, "not_found", StringComparison.OrdinalIgnoreCase))
            {
                return EnrichmentStageState.Skipped;
            }

            if (string.IsNullOrWhiteSpace(identity.CharacterId))
            {
                return EnrichmentStageState.NotStarted;
            }

            if (!string.IsNullOrWhiteSpace(identity.AffiliationCheckedAtUtc))
            {
                return EnrichmentStageState.Success;
            }

            return EnrichmentStageState.NotStarted;
        }

        private static EnrichmentStageState DetermineStatsStage(ResolverCacheEntry identity, bool hasStats)
        {
            if (hasStats)
            {
                return EnrichmentStageState.Success;
            }

            if (identity == null)
            {
                return EnrichmentStageState.NotStarted;
            }

            if (string.Equals(identity.ResolverConfidence, "not_found", StringComparison.OrdinalIgnoreCase))
            {
                return EnrichmentStageState.Skipped;
            }

            if (!string.IsNullOrWhiteSpace(identity.CharacterId))
            {
                return EnrichmentStageState.NotStarted;
            }

            return EnrichmentStageState.NotStarted;
        }

        private static string DetermineIdentityDetail(ResolverCacheEntry identity)
        {
            if (identity == null)
            {
                return "Identity not started";
            }

            if (string.Equals(identity.ResolverConfidence, "not_found", StringComparison.OrdinalIgnoreCase))
            {
                return "Terminal miss cached";
            }

            if (string.Equals(identity.ResolverConfidence, "esi_exact_fallback", StringComparison.OrdinalIgnoreCase))
            {
                return "Resolved by ESI exact fallback";
            }

            if (!string.IsNullOrWhiteSpace(identity.CharacterId))
            {
                return "Identity available from cache";
            }

            return "Identity not started";
        }

        private static string DetermineAffiliationDetail(ResolverCacheEntry identity)
        {
            if (identity == null)
            {
                return "Affiliation not started";
            }

            if (string.Equals(identity.ResolverConfidence, "not_found", StringComparison.OrdinalIgnoreCase))
            {
                return "Affiliation skipped after terminal miss";
            }

            if (string.IsNullOrWhiteSpace(identity.CharacterId))
            {
                return "Affiliation waiting on identity";
            }

            if (!string.IsNullOrWhiteSpace(identity.AffiliationCheckedAtUtc))
            {
                return "Affiliation available from cache";
            }

            return "Affiliation not started";
        }

        private static string FormatLastSeenDate(string utcValue)
        {
            if (string.IsNullOrWhiteSpace(utcValue))
            {
                return "";
            }

            if (!DateTime.TryParse(
                    utcValue,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsed))
            {
                return "";
            }

            return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }
}