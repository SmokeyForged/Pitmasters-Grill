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
        private readonly PilotBaitObservationDayRepository? _baitObservationRepository;
        private readonly PilotCynoTackleObservationDayRepository? _cynoTackleObservationRepository;
        private readonly CynoSignalAnalyzer _cynoSignalAnalyzer = new();

        public PilotBoardRowDetailFormatter(
            BoardPopulationRetryPolicy boardPopulationRetryPolicy,
            PilotCynoModuleObservationDayRepository? cynoModuleObservationRepository = null,
            PilotBaitObservationDayRepository? baitObservationRepository = null,
            PilotCynoTackleObservationDayRepository? cynoTackleObservationRepository = null)
        {
            _boardPopulationRetryPolicy = boardPopulationRetryPolicy ?? throw new ArgumentNullException(nameof(boardPopulationRetryPolicy));
            _cynoModuleObservationRepository = cynoModuleObservationRepository;
            _baitObservationRepository = baitObservationRepository;
            _cynoTackleObservationRepository = cynoTackleObservationRepository;
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
            var baitEvidence = GetStoredBaitEvidence(row).ToList();
            UpdateConfirmedCynoModuleState(row, moduleEvidence, baitEvidence);

            var result = _cynoSignalAnalyzer.Analyze(row, moduleEvidence);
            if (row != null)
            {
                DiagnosticTelemetry.RecordCynoSignalSummary(
                    $"pilot={row.CharacterName}; id={row.CharacterId}; status={result.Status}; type={GetCynoSignalTypeDisplayText(result)}; score={result.Score}; manualBait={row.BaitOverride}; derivedBaitEvidenceCount={row.DerivedBaitEvidenceCount}; boardSignal={row.BoardSignalKind}; sourceFreshness={result.SourceFreshness}; evidence={string.Join(" | ", result.Evidence.Select(x => x.Summary))}");
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

        public IReadOnlyList<IndustrialCynoBaitEvidence> GetDerivedBaitEvidence(PilotBoardRow row)
        {
            return GetStoredBaitEvidence(row).ToList();
        }

        public string GetBaitSignalHeadlineText(PilotBoardRow row)
        {
            return GetCompactBaitStatusText(row);
        }

        public string GetBaitEvidenceText(PilotBoardRow row)
        {
            var line = BuildBaitEvidenceLines(GetStoredBaitEvidence(row).ToList()).FirstOrDefault();
            return string.IsNullOrWhiteSpace(line)
                ? "Evidence: none"
                : $"Evidence: {line}";
        }

        public string GetBaitLimitationsText(PilotBoardRow row)
        {
            var baitEvidence = GetStoredBaitEvidence(row).ToList();
            if (baitEvidence.Count == 0 && row?.BaitOverride != true)
            {
                return "";
            }

            return "- Based on public loss victim item data, not live fit visibility.";
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

        public string GetCompactCynoSignalHeadlineText(CynoSignalResult result)
        {
            if (result == null || result.Status == CynoSignalStatus.Unknown)
            {
                return "Cyno: Unknown";
            }

            return $"Cyno: {result.Status} — {GetCompactCynoSignalTypeDisplayText(result)} — {result.Score}%";
        }

        public string GetCompactPilotAffiliationText(PilotBoardRow row)
        {
            var corp = string.IsNullOrWhiteSpace(row?.CorpName) ? "Unknown corp" : row.CorpName;
            var alliance = string.IsNullOrWhiteSpace(row?.AllianceName) ? "No alliance" : row.AllianceName;
            return $"{corp} / {alliance}";
        }

        public string GetCompactPilotActivityText(PilotBoardRow row)
        {
            var ship = string.IsNullOrWhiteSpace(row?.LastShipSeenName) ? "Ship unavailable" : row.LastShipSeenName;
            var seen = string.IsNullOrWhiteSpace(row?.LastShipSeenAtUtc)
                ? "last seen unknown"
                : $"last seen {FormatCompactUtc(row.LastShipSeenAtUtc)}";

            return $"{ship} • {seen}";
        }

        public string GetCompactKillLossText(PilotBoardRow row)
        {
            var kills = row?.KillCount?.ToString(CultureInfo.InvariantCulture) ?? "Unknown";
            var losses = row?.LossCount?.ToString(CultureInfo.InvariantCulture) ?? "Unknown";
            return $"Kills {kills} • Losses {losses}";
        }

        public string GetCompactBaitStatusText(PilotBoardRow row)
        {
            var baitEvidence = GetStoredBaitEvidence(row).ToList();
            UpdateDerivedBaitState(row, baitEvidence);

            if (row?.BaitOverride == true && baitEvidence.Count > 0)
            {
                return "Bait: Override + evidence";
            }

            if (row?.BaitOverride == true)
            {
                return "Bait: Override";
            }

            if (baitEvidence.Count > 0)
            {
                return "Bait: Confirmed";
            }

            return "Bait: No evidence";
        }

        public string GetPrimaryCompactEvidenceText(PilotBoardRow row, CynoSignalResult result)
        {
            var baitLine = BuildBaitEvidenceLines(GetStoredBaitEvidence(row).ToList()).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(baitLine))
            {
                return $"Evidence: {baitLine}";
            }

            var cynoLine = BuildConfirmedModuleEvidenceLines(result).FirstOrDefault();
            var tackleLine = BuildCynoHullTackleEvidenceLines(GetStoredCynoHullTackleEvidence(row).ToList()).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(cynoLine))
            {
                return string.IsNullOrWhiteSpace(tackleLine)
                    ? $"Evidence: {cynoLine}"
                    : $"Evidence: {cynoLine}{Environment.NewLine}Tackle: {tackleLine}";
            }

            if (!string.IsNullOrWhiteSpace(tackleLine))
            {
                return $"Tackle: {tackleLine}";
            }

            return "Evidence: none";
        }

        public string GetCompactLimitationsText(PilotBoardRow row, CynoSignalResult result)
        {
            var hasBait = GetStoredBaitEvidence(row).Any();
            var hasConfirmedCyno = result?.Evidence?.Any(x => x.IsConfirmedModuleEvidence) == true;

            if (hasBait || hasConfirmedCyno)
            {
                return "Limitations: Public loss data only";
            }

            if (result == null || result.Status == CynoSignalStatus.Unknown)
            {
                return "";
            }

            return "Limitations: Inference only";
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

        private IEnumerable<IndustrialCynoBaitEvidence> GetStoredBaitEvidence(PilotBoardRow row)
        {
            if (_baitObservationRepository == null || string.IsNullOrWhiteSpace(row?.CharacterId))
            {
                return Enumerable.Empty<IndustrialCynoBaitEvidence>();
            }

            return _baitObservationRepository.GetRecentBaitEvidenceByCharacterId(row.CharacterId);
        }

        private IEnumerable<CynoHullTackleEvidence> GetStoredCynoHullTackleEvidence(PilotBoardRow row)
        {
            if (_cynoTackleObservationRepository == null || string.IsNullOrWhiteSpace(row?.CharacterId))
            {
                return Enumerable.Empty<CynoHullTackleEvidence>();
            }

            return _cynoTackleObservationRepository.GetRecentTackleEvidenceByCharacterId(row.CharacterId);
        }

        private void UpdateConfirmedCynoModuleState(PilotBoardRow row, IReadOnlyCollection<CynoModuleEvidence> moduleEvidence)
        {
            UpdateConfirmedCynoModuleState(row, moduleEvidence, GetStoredBaitEvidence(row).ToList());
        }

        private void UpdateConfirmedCynoModuleState(
            PilotBoardRow row,
            IReadOnlyCollection<CynoModuleEvidence> moduleEvidence,
            IReadOnlyCollection<IndustrialCynoBaitEvidence> baitEvidence)
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
            UpdateDerivedBaitState(row, baitEvidence);

            var signal = _cynoSignalAnalyzer.Analyze(row, moduleEvidence);
            ApplyBoardSignalState(row, confirmedTypes, signal, baitEvidence.Count > 0);
        }

        private static void UpdateDerivedBaitState(
            PilotBoardRow row,
            IReadOnlyCollection<IndustrialCynoBaitEvidence> baitEvidence)
        {
            if (row == null)
            {
                return;
            }

            row.HasDerivedBaitEvidence = baitEvidence.Count > 0;
            row.DerivedBaitEvidenceCount = baitEvidence.Count;
        }

        private static IEnumerable<string> BuildConfirmedModuleEvidenceLines(CynoSignalResult result)
        {
            if (result == null)
            {
                yield break;
            }

            var confirmedGroups = result.Evidence
                .Where(x => x.IsConfirmedModuleEvidence)
                .Where(x => x.SignalType != CynoSignalType.Unknown)
                .GroupBy(x => new
                {
                    KillmailId = x.KillmailId ?? "",
                    Date = x.ObservedAtUtc?.Date
                })
                .OrderByDescending(group => group.Max(x => x.ObservedAtUtc ?? DateTime.MinValue));

            foreach (var group in confirmedGroups)
            {
                var first = group
                    .OrderByDescending(x => x.ObservedAtUtc ?? DateTime.MinValue)
                    .First();

                var datePart = FormatEvidenceDate(first.ObservedAtUtc);
                var typeDisplay = string.Join(" + ", group
                    .Select(x => x.SignalType)
                    .Where(x => x != CynoSignalType.Unknown)
                    .Distinct()
                    .OrderBy(GetSignalTypeSortOrder)
                    .Select(GetShortCynoEvidenceName));

                yield return $"{datePart} — {typeDisplay}";
            }
        }

        private static void ApplyBoardSignalState(
            PilotBoardRow row,
            IReadOnlyCollection<CynoSignalType> confirmedTypes,
            CynoSignalResult signal,
            bool hasDerivedBaitEvidence)
        {
            if (row.BaitOverride)
            {
                SetBoardSignal(row, "Bait", "B", "Bait override");
                return;
            }

            if (hasDerivedBaitEvidence)
            {
                SetBoardSignal(row, "Bait", "B", "Auto bait: industrial cyno + tackle module found on public loss");
                return;
            }

            if (row.KnownCynoOverride)
            {
                SetBoardSignal(row, "ConfirmedCovert", "✦", "Manual Known-Cyno Override");
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
            row.BoardHoverToolTip = BuildBoardHoverToolTip(row, signal, hasDerivedBaitEvidence);
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
            row.BoardHoverToolTip = toolTip;
        }

        private static string BuildBoardHoverToolTip(
            PilotBoardRow row,
            CynoSignalResult signal,
            bool hasDerivedBaitEvidence)
        {
            if (row == null)
            {
                return string.Empty;
            }

            var lines = new List<string>();

            if (!string.IsNullOrWhiteSpace(row.BoardSignalToolTip) &&
                !string.Equals(row.BoardSignalToolTip, "No board signal", StringComparison.OrdinalIgnoreCase))
            {
                lines.Add(row.BoardSignalToolTip);
            }

            if (hasDerivedBaitEvidence)
            {
                lines.Add($"Bait evidence: {row.DerivedBaitEvidenceCount}");
            }
            else if (row.HasConfirmedCynoModuleEvidence && !string.IsNullOrWhiteSpace(row.ConfirmedCynoSignalTypesDisplay))
            {
                lines.Add($"Confirmed: {row.ConfirmedCynoSignalTypesDisplay}");
            }
            else if (signal != null && signal.Status != CynoSignalStatus.Unknown)
            {
                lines.Add($"Signal: {signal.Status} ({GetCompactSignalTypeForHover(signal)})");
            }

            if (!string.IsNullOrWhiteSpace(row.LastPublicCynoCapableHull))
            {
                lines.Add($"Cyno hull: {row.LastPublicCynoCapableHull}");
            }

            if (lines.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(Environment.NewLine, lines.Take(3));
        }

        private static string GetCompactSignalTypeForHover(CynoSignalResult signal)
        {
            return signal.SignalType switch
            {
                CynoSignalType.Normal => "hard",
                CynoSignalType.Covert => "covert",
                CynoSignalType.Industrial => "industrial",
                CynoSignalType.Mixed => "mixed",
                _ => "unknown"
            };
        }

        private static IEnumerable<string> BuildBaitEvidenceLines(IReadOnlyCollection<IndustrialCynoBaitEvidence> evidence)
        {
            if (evidence == null)
            {
                yield break;
            }

            var grouped = evidence
                .GroupBy(x => new
                {
                    KillmailId = x.KillmailId ?? "",
                    Date = x.KillmailTimeUtc?.Date
                })
                .OrderByDescending(group => group.Max(x => x.KillmailTimeUtc ?? DateTime.MinValue));

            foreach (var group in grouped)
            {
                var first = group
                    .OrderByDescending(x => x.KillmailTimeUtc ?? DateTime.MinValue)
                    .First();

                var datePart = FormatEvidenceDate(first.KillmailTimeUtc);
                var tacklePart = string.Join(" + ", group
                    .Select(x => string.IsNullOrWhiteSpace(x.TackleModuleName) ? GetTackleTypeDisplayName(x.TackleType) : x.TackleModuleName)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

                if (string.IsNullOrWhiteSpace(tacklePart))
                {
                    tacklePart = "tackle";
                }

                yield return $"{datePart} — indi + {tacklePart}";
            }
        }

        private static string GetShortCynoEvidenceName(CynoSignalType signalType)
        {
            return signalType switch
            {
                CynoSignalType.Normal => "hard",
                CynoSignalType.Covert => "covert",
                CynoSignalType.Industrial => "indi",
                _ => "cyno"
            };
        }

        private static IEnumerable<string> BuildCynoHullTackleEvidenceLines(IReadOnlyCollection<CynoHullTackleEvidence> evidence)
        {
            if (evidence == null)
            {
                yield break;
            }

            var grouped = evidence
                .GroupBy(x => new
                {
                    KillmailId = x.KillmailId ?? "",
                    Date = x.KillmailTimeUtc?.Date
                })
                .OrderByDescending(group => group.Max(x => x.KillmailTimeUtc ?? DateTime.MinValue));

            foreach (var group in grouped)
            {
                var first = group
                    .OrderByDescending(x => x.KillmailTimeUtc ?? DateTime.MinValue)
                    .First();

                var datePart = FormatEvidenceDate(first.KillmailTimeUtc);
                var shipPart = string.IsNullOrWhiteSpace(first.VictimShipName)
                    ? ""
                    : $"{first.VictimShipName} + ";
                var tacklePart = string.Join(" + ", group
                    .Select(x => string.IsNullOrWhiteSpace(x.TackleModuleName) ? GetTackleTypeDisplayName(x.TackleType) : x.TackleModuleName)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

                if (string.IsNullOrWhiteSpace(tacklePart))
                {
                    tacklePart = "tackle";
                }

                yield return $"{datePart} - {shipPart}{tacklePart}";
            }
        }

        private static string GetTackleTypeDisplayName(TackleModuleType tackleType)
        {
            return tackleType switch
            {
                TackleModuleType.WarpScrambler => "warp scrambler",
                TackleModuleType.WarpDisruptor => "warp disruptor",
                _ => "tackle"
            };
        }

        private string GetCompactCynoSignalTypeDisplayText(CynoSignalResult result)
        {
            if (result == null)
            {
                return "unknown";
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
                return string.Join(" + ", confirmedTypes.Select(GetShortCynoEvidenceName));
            }

            return GetShortCynoEvidenceName(result.SignalType);
        }

        private static string FormatEvidenceDate(DateTime? value)
        {
            return value.HasValue
                ? value.Value.ToString("yy/MM/dd", CultureInfo.InvariantCulture)
                : "unknown date";
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

        private static string FormatCompactUtc(string value)
        {
            if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
            {
                return $"{parsed.ToUniversalTime():yy/MM/dd HH:mm} UTC";
            }

            return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
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
