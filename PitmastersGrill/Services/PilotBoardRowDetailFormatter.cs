using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PitmastersGrill.Services
{
    public class PilotBoardRowDetailFormatter
    {
        private readonly BoardPopulationRetryPolicy _boardPopulationRetryPolicy;
        private readonly PilotCynoModuleObservationDayRepository? _cynoModuleObservationRepository;
        private readonly CynoSignalAnalyzer _cynoSignalAnalyzer = new();

        public PilotBoardRowDetailFormatter(
            BoardPopulationRetryPolicy boardPopulationRetryPolicy,
            PilotCynoModuleObservationDayRepository? cynoModuleObservationRepository = null)
        {
            _boardPopulationRetryPolicy = boardPopulationRetryPolicy ?? throw new ArgumentNullException(nameof(boardPopulationRetryPolicy));
            _cynoModuleObservationRepository = cynoModuleObservationRepository;
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
                return !string.IsNullOrWhiteSpace(row.ResolvedAtUtc)
                    ? $"Freshness: resolved by ESI exact fallback at {row.ResolvedAtUtc}"
                    : "Freshness: resolved by ESI exact fallback";
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

        public string GetExplainabilityText(PilotBoardRow row)
        {
            if (row == null)
            {
                return "Sources / Freshness: Unknown";
            }

            var lines = new[]
            {
                GetUpdatedUtcText(row),
                GetSourceText(row),
                string.IsNullOrWhiteSpace(row.StatsStatusDetail) ? "Stats: Unknown" : $"Stats: {row.StatsStatusDetail}"
            };

            return string.Join(Environment.NewLine, lines);
        }

        public string GetRecentPublicActivityText(PilotBoardRow row)
        {
            if (row == null)
            {
                return "Recent Public Kill/Loss Activity: Unknown";
            }

            return "Recent Public Kill/Loss Activity:" + Environment.NewLine + GetConciseRecentPublicActivityText(row);
        }

        public CynoSignalResult GetCynoSignal(PilotBoardRow row)
        {
            var moduleEvidence = GetStoredModuleEvidence(row).ToList();
            UpdateConfirmedCynoModuleState(row, moduleEvidence);

            var result = _cynoSignalAnalyzer.Analyze(row, moduleEvidence);
            if (row != null)
            {
                DiagnosticTelemetry.RecordCynoSignalSummary(
                    $"pilot={row.CharacterName}; id={row.CharacterId}; status={result.Status}; type={GetCynoSignalTypeDisplayText(result)}; score={result.Score}; sourceFreshness={result.SourceFreshness}; evidence={string.Join(" | ", result.Evidence.Select(x => x.Summary))}");
            }

            return result;
        }

        public void UpdateConfirmedCynoModuleState(PilotBoardRow row)
        {
            if (row == null)
            {
                return;
            }

            UpdateConfirmedCynoModuleState(row, GetStoredModuleEvidence(row).ToList());
        }

        public bool HasConfirmedCynoModuleEvidence(PilotBoardRow row)
        {
            if (row == null)
            {
                return false;
            }

            return GetStoredModuleEvidence(row).Any();
        }

        public string GetCynoSignalText(CynoSignalResult result)
        {
            if (result == null || result.Status == CynoSignalStatus.Unknown)
            {
                return "Cyno Signal: Unknown";
            }

            return $"Cyno Signal: {result.Status} - {GetCynoSignalTypeDisplayText(result)} - {result.Score}%";
        }

        public string GetCynoSignalHeadlineText(CynoSignalResult result)
        {
            if (result == null || result.Status == CynoSignalStatus.Unknown)
            {
                return "Unknown";
            }

            return $"{result.Status} - {GetCynoSignalTypeDisplayText(result)} - {result.Score}%";
        }

        public string GetCynoSignalTypeDisplayText(CynoSignalResult result)
        {
            if (result == null)
            {
                return "Unknown";
            }

            var confirmedTypes = result.Evidence
                .Where(x => x.IsConfirmedModuleEvidence)
                .Select(x => x.SignalType)
                .Where(x => x != CynoSignalType.Unknown)
                .Distinct()
                .OrderBy(GetSignalTypeSortOrder)
                .ToList();

            if (confirmedTypes.Count > 0)
            {
                return string.Join(" + ", confirmedTypes.Select(CynoSignalAnalyzer.GetSignalTypeDisplayName));
            }

            return CynoSignalAnalyzer.GetSignalTypeDisplayName(result.SignalType);
        }

        public string GetCynoEvidenceText(CynoSignalResult result)
        {
            return "Evidence:" + Environment.NewLine + GetConciseEvidenceText(result);
        }

        public string GetCynoLimitationsText(CynoSignalResult result)
        {
            return "Limitations:" + Environment.NewLine + GetConciseLimitationsText(result);
        }

        public string GetPilotSummaryText(PilotBoardRow row)
        {
            var corp = string.IsNullOrWhiteSpace(row?.CorpName) ? "Unknown" : row.CorpName;
            var alliance = string.IsNullOrWhiteSpace(row?.AllianceName) ? "None/Unknown" : row.AllianceName;
            var ship = string.IsNullOrWhiteSpace(row?.LastShipSeenName) ? "Not available" : row.LastShipSeenName;

            return $"Pilot: {row?.CharacterName ?? "Unknown"}{Environment.NewLine}Corp: {corp}{Environment.NewLine}Alliance: {alliance}{Environment.NewLine}Last ship: {ship}";
        }

        public string GetUpdatedUtcText(PilotBoardRow row)
        {
            if (!string.IsNullOrWhiteSpace(row?.LastShipSeenAtUtc))
            {
                return $"Updated: {FormatUtc(row.LastShipSeenAtUtc)}";
            }

            if (!string.IsNullOrWhiteSpace(row?.AffiliationCheckedAtUtc))
            {
                return $"Updated: {FormatUtc(row.AffiliationCheckedAtUtc)}";
            }

            if (!string.IsNullOrWhiteSpace(row?.ResolvedAtUtc))
            {
                return $"Updated: {FormatUtc(row.ResolvedAtUtc)}";
            }

            return "Updated: Unknown";
        }

        public string GetSourceText(PilotBoardRow row)
        {
            if (row?.StatsStage == EnrichmentStageState.Success)
            {
                return "Source: public killmail cache";
            }

            if (!string.IsNullOrWhiteSpace(row?.ResolverConfidence))
            {
                return $"Source: {row.ResolverConfidence}";
            }

            return "Source: Unknown";
        }

        public string GetBottomFreshnessText(PilotBoardRow row)
        {
            return $"{GetUpdatedUtcText(row)} | {GetSourceText(row)}";
        }

        public string GetConciseRecentPublicActivityText(PilotBoardRow row)
        {
            var kills = row?.KillCount?.ToString() ?? "Unknown";
            var losses = row?.LossCount?.ToString() ?? "Unknown";
            var lastShip = string.IsNullOrWhiteSpace(row?.LastShipSeenName)
                ? "Not available"
                : $"{row.LastShipSeenName}, {FormatUtc(row.LastShipSeenAtUtc)}";

            return $"Kills: {kills}{Environment.NewLine}Losses: {losses}{Environment.NewLine}Last ship seen: {lastShip}{Environment.NewLine}System: unavailable";
        }

        public string GetConciseEvidenceText(CynoSignalResult result)
        {
            if (result == null || result.Evidence.Count == 0)
            {
                return "- No relevant public cyno evidence found.";
            }

            var confirmedEvidence = BuildConfirmedModuleEvidenceLines(result).ToList();
            if (confirmedEvidence.Count > 0)
            {
                return string.Join(Environment.NewLine, confirmedEvidence.Take(3).Select(x => $"- {x}"));
            }

            var primaryEvidence = result.Evidence
                .Where(x => !x.IsConfirmedModuleEvidence)
                .Where(x => x.IsHullInference || x.ScoreContribution > 0)
                .Select(x => x.Summary)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .Select(x => $"- {x}");

            return string.Join(Environment.NewLine, primaryEvidence);
        }

        public string GetConciseLimitationsText(CynoSignalResult result)
        {
            if (result == null || result.Limitations.Count == 0)
            {
                return "- Public data may be incomplete.";
            }

            return string.Join(Environment.NewLine, result.Limitations.Take(2).Select(x => $"- {x}"));
        }

        private IEnumerable<CynoModuleEvidence> GetStoredModuleEvidence(PilotBoardRow row)
        {
            if (_cynoModuleObservationRepository == null || string.IsNullOrWhiteSpace(row?.CharacterId))
            {
                return Enumerable.Empty<CynoModuleEvidence>();
            }

            return _cynoModuleObservationRepository.GetRecentCynoModuleEvidenceByCharacterId(row.CharacterId);
        }

        private void UpdateConfirmedCynoModuleState(PilotBoardRow row, IReadOnlyCollection<CynoModuleEvidence> moduleEvidence)
        {
            if (row == null)
            {
                return;
            }

            var confirmedTypes = moduleEvidence
                .Where(x => x != null && CynoSignalAnalyzer.TryGetModuleSignalType(x.TypeId, out _, out _))
                .Select(x =>
                {
                    CynoSignalAnalyzer.TryGetModuleSignalType(x.TypeId, out var signalType, out _);
                    return signalType;
                })
                .Where(x => x != CynoSignalType.Unknown)
                .Distinct()
                .OrderBy(GetSignalTypeSortOrder)
                .ToList();

            row.HasConfirmedCynoModuleEvidence = confirmedTypes.Count > 0;
            row.ConfirmedCynoSignalTypesDisplay = confirmedTypes.Count == 0
                ? ""
                : string.Join(" + ", confirmedTypes.Select(CynoSignalAnalyzer.GetSignalTypeDisplayName));

            var signal = _cynoSignalAnalyzer.Analyze(row, moduleEvidence);
            ApplyBoardSignalState(row, confirmedTypes, signal);
        }

        private static IEnumerable<string> BuildConfirmedModuleEvidenceLines(CynoSignalResult result)
        {
            var confirmedGroups = result.Evidence
                .Where(x => x.IsConfirmedModuleEvidence)
                .Where(x => x.SignalType != CynoSignalType.Unknown)
                .GroupBy(x => new
                {
                    KillmailId = x.KillmailId ?? "",
                    Date = x.ObservedAtUtc?.Date,
                    ShipName = x.ShipName ?? ""
                })
                .OrderByDescending(group => group.Max(x => x.ObservedAtUtc ?? DateTime.MinValue));

            foreach (var group in confirmedGroups)
            {
                var first = group
                    .OrderByDescending(x => x.ObservedAtUtc ?? DateTime.MinValue)
                    .First();

                var shipPart = string.IsNullOrWhiteSpace(first.ShipName)
                    ? "Seen"
                    : $"Seen {first.ShipName}";

                var datePart = first.ObservedAtUtc.HasValue
                    ? $" on {first.ObservedAtUtc.Value:yyyy-MM-dd}"
                    : "";

                var typeNames = group
                    .Select(x => x.SignalType)
                    .Where(x => x != CynoSignalType.Unknown)
                    .Distinct()
                    .OrderBy(GetSignalTypeSortOrder)
                    .Select(CynoSignalAnalyzer.GetSignalTypeEvidenceName)
                    .Select(x => x.Replace(" cyno", "", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var typeDisplay = typeNames.Count == 0
                    ? "cyno"
                    : string.Join(" + ", typeNames) + " cyno";

                yield return $"{shipPart}{datePart} as victim with {typeDisplay} fitted.";
            }
        }

        private static void ApplyBoardSignalState(
            PilotBoardRow row,
            IReadOnlyCollection<CynoSignalType> confirmedTypes,
            CynoSignalResult signal)
        {
            if (row.KnownCynoOverride)
            {
                SetBoardSignal(row, "ConfirmedCovert", "✦", "Manual Known-Cyno Override");
                return;
            }

            if (row.BaitOverride)
            {
                SetBoardSignal(row, "Bait", "B", "Bait override");
                return;
            }

            if (confirmedTypes.Contains(CynoSignalType.Covert))
            {
                SetBoardSignal(row, "ConfirmedCovert", "✦", "Confirmed covert cyno module");
                return;
            }

            if (confirmedTypes.Contains(CynoSignalType.Normal))
            {
                SetBoardSignal(row, "ConfirmedNormal", "◆", "Confirmed normal cyno module");
                return;
            }

            if (signal.Status == CynoSignalStatus.Possible)
            {
                SetBoardSignal(row, "Possible", "?", "Possible cyno signal");
                return;
            }

            if (signal.Status == CynoSignalStatus.Inferred || signal.Status == CynoSignalStatus.Likely)
            {
                if (IsNormalOrCovertSignal(signal.SignalType))
                {
                    SetBoardSignal(row, "InferredCyno", "!", "Inferred normal/covert cyno signal");
                    return;
                }

                if (signal.SignalType == CynoSignalType.Industrial)
                {
                    SetBoardSignal(row, "None", "", "Industrial cyno inference only");
                    return;
                }
            }

            SetBoardSignal(row, "None", "", "No board signal");
        }

        private static bool IsNormalOrCovertSignal(CynoSignalType signalType)
        {
            return signalType == CynoSignalType.Normal ||
                   signalType == CynoSignalType.Covert ||
                   signalType == CynoSignalType.Mixed;
        }

        private static void SetBoardSignal(PilotBoardRow row, string kind, string icon, string toolTip)
        {
            row.BoardSignalKind = kind;
            row.BoardSignalIcon = icon;
            row.BoardSignalToolTip = toolTip;
        }

        private static int GetSignalTypeSortOrder(CynoSignalType signalType)
        {
            return signalType switch
            {
                CynoSignalType.Normal => 0,
                CynoSignalType.Covert => 1,
                CynoSignalType.Industrial => 2,
                CynoSignalType.Mixed => 3,
                _ => 4
            };
        }

        private static string FormatUtc(string value)
        {
            if (DateTime.TryParse(value, out var parsed))
            {
                return $"{parsed.ToUniversalTime():yyyy-MM-dd HH:mm} UTC";
            }

            return string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
        }
    }
}
