using PitmastersGrill.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PitmastersGrill.Services
{
    public sealed class CynoSignalAnalyzer
    {
        public const int NormalCynoModuleTypeId = 21096;
        public const int CovertCynoModuleTypeId = 28646;
        public const int IndustrialCynoModuleTypeId = 52694;

        private const int RecentModuleScore = 70;
        private const int StaleModuleScore = 60;
        private const int UnknownAgeModuleScore = 50;
        private const int RecentHullScore = 25;
        private const int RepeatedHullScore = 15;
        private const int RecentPublicActivityScore = 10;
        private const int ExistingContextHintScore = 5;

        private static readonly HashSet<string> NormalHullNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Falcon", "Rapier", "Pilgrim", "Arazu",
            "Redeemer", "Sin", "Widow", "Panther", "Marshal", "Python", "Enforcer",
            "Onyx", "Broadsword", "Devoter", "Phobos", "Laelaps"
        };

        private static readonly HashSet<string> CovertHullNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Redeemer", "Sin", "Widow", "Panther", "Marshal", "Python", "Enforcer",
            "Anathema", "Buzzard", "Cheetah", "Helios", "Pacifier",
            "Falcon", "Rapier", "Pilgrim", "Arazu",
            "Nemesis", "Manticore", "Hound", "Purifier",
            "Tengu", "Legion", "Proteus", "Loki",
            "Crane", "Prorator", "Prowler", "Viator",
            "Prospect", "Etana", "Rabisu"
        };

        private static readonly HashSet<string> IndustrialHullNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Badger", "Tayra", "Nereus", "Hoarder", "Mammoth", "Wreathe",
            "Kryos", "Epithal", "Miasmos", "Iteron Mark V", "Bestower", "Sigil",
            "Crane", "Bustard", "Prorator", "Prowler", "Viator", "Occator", "Mastodon", "Impel",
            "Venture", "Venture Consortium Issue"
        };

        public CynoSignalResult Analyze(PilotBoardRow row, IEnumerable<CynoModuleEvidence>? moduleEvidence = null)
        {
            var result = new CynoSignalResult();

            if (row == null)
            {
                result.Limitations.Add("No pilot row was available for analysis.");
                return result;
            }

            var evidence = new List<CynoEvidenceItem>();
            AddModuleEvidence(evidence, moduleEvidence, row);

            var hasConfirmedModuleEvidence = evidence.Any(x => x.IsConfirmedModuleEvidence);
            if (!hasConfirmedModuleEvidence)
            {
                AddHullEvidence(evidence, row);
                AddPublicActivityEvidence(evidence, row);
            }

            AddManualContextEvidence(evidence, row);

            result.Evidence = evidence;
            result.Score = Math.Min(100, evidence.Sum(x => Math.Max(0, x.ScoreContribution)));
            result.SignalType = DetermineSignalType(evidence);
            result.Status = DetermineStatus(result.Score, evidence, evidence.Count);
            result.SourceFreshness = BuildSourceFreshness(row, evidence);
            result.Limitations = BuildLimitations(evidence);

            return result;
        }

        public static bool TryGetModuleSignalType(int typeId, out CynoSignalType signalType, out string moduleName)
        {
            signalType = CynoSignalType.Unknown;
            moduleName = "";

            switch (typeId)
            {
                case NormalCynoModuleTypeId:
                    signalType = CynoSignalType.Normal;
                    moduleName = "Cynosural Field Generator I";
                    return true;
                case CovertCynoModuleTypeId:
                    signalType = CynoSignalType.Covert;
                    moduleName = "Covert Cynosural Field Generator I";
                    return true;
                case IndustrialCynoModuleTypeId:
                    signalType = CynoSignalType.Industrial;
                    moduleName = "Industrial Cynosural Field Generator";
                    return true;
                default:
                    return false;
            }
        }

        public static string GetSignalTypeDisplayName(CynoSignalType signalType)
        {
            return signalType switch
            {
                CynoSignalType.Normal => "Normal",
                CynoSignalType.Covert => "Covert",
                CynoSignalType.Industrial => "Industrial",
                CynoSignalType.Mixed => "Mixed",
                _ => "Unknown"
            };
        }

        public static string GetSignalTypeEvidenceName(CynoSignalType signalType)
        {
            return signalType switch
            {
                CynoSignalType.Normal => "normal cyno",
                CynoSignalType.Covert => "covert cyno",
                CynoSignalType.Industrial => "industrial cyno",
                _ => "cyno"
            };
        }

        private static void AddModuleEvidence(
            List<CynoEvidenceItem> evidence,
            IEnumerable<CynoModuleEvidence>? moduleEvidence,
            PilotBoardRow row)
        {
            if (moduleEvidence == null)
            {
                return;
            }

            var normalized = moduleEvidence
                .Where(module => module != null && TryGetModuleSignalType(module.TypeId, out _, out _))
                .GroupBy(module => new
                {
                    module.TypeId,
                    KillmailId = module.KillmailId ?? "",
                    Date = module.KillmailTimeUtc?.Date
                })
                .Select(group => group
                    .OrderByDescending(module => module.KillmailTimeUtc ?? DateTime.MinValue)
                    .First())
                .OrderByDescending(module => module.KillmailTimeUtc ?? DateTime.MinValue)
                .ThenBy(module => module.TypeId)
                .ToList();

            foreach (var module in normalized)
            {
                if (!TryGetModuleSignalType(module.TypeId, out var signalType, out var moduleName))
                {
                    continue;
                }

                var observedAt = module.KillmailTimeUtc;
                var ageDays = observedAt.HasValue
                    ? (DateTime.UtcNow - observedAt.Value).TotalDays
                    : (double?)null;
                var score = ageDays.HasValue
                    ? ageDays.Value <= 90 ? RecentModuleScore : StaleModuleScore
                    : UnknownAgeModuleScore;
                var shipName = !string.IsNullOrWhiteSpace(module.ShipName)
                    ? module.ShipName
                    : row?.LastShipSeenName ?? "";

                evidence.Add(new CynoEvidenceItem
                {
                    Summary = BuildModuleEvidenceSummary(shipName, observedAt, signalType),
                    SignalType = signalType,
                    ScoreContribution = score,
                    IsConfirmedModuleEvidence = true,
                    IsHullInference = false,
                    Source = string.IsNullOrWhiteSpace(module.Source) ? "public loss victim item list" : module.Source,
                    ObservedAtUtc = observedAt,
                    ShipName = shipName,
                    KillmailId = module.KillmailId ?? ""
                });
            }
        }

        private static void AddHullEvidence(List<CynoEvidenceItem> evidence, PilotBoardRow row)
        {
            var hullName = !string.IsNullOrWhiteSpace(row.LastPublicCynoCapableHull)
                ? row.LastPublicCynoCapableHull
                : row.LastShipSeenName;

            if (string.IsNullOrWhiteSpace(hullName))
            {
                return;
            }

            var hullTypes = GetHullSignalTypes(hullName).ToList();
            if (hullTypes.Count == 0)
            {
                return;
            }

            var observedAt = TryParseUtc(row.LastShipSeenAtUtc);
            var recent = observedAt.HasValue && (DateTime.UtcNow - observedAt.Value).TotalDays <= 90;

            foreach (var hullType in hullTypes)
            {
                evidence.Add(new CynoEvidenceItem
                {
                    Summary = $"Seen {hullName} on {FormatDateForEvidence(observedAt)}; {GetSignalTypeEvidenceName(hullType)} capable hull.",
                    SignalType = hullType,
                    ScoreContribution = recent ? RecentHullScore : RepeatedHullScore,
                    IsConfirmedModuleEvidence = false,
                    IsHullInference = true,
                    Source = "local public killmail-derived hull observation",
                    ObservedAtUtc = observedAt,
                    ShipName = hullName
                });
            }
        }

        private static void AddPublicActivityEvidence(List<CynoEvidenceItem> evidence, PilotBoardRow row)
        {
            var observedAt = TryParseUtc(row.LastShipSeenAtUtc);
            var hasActivity = (row.KillCount ?? 0) > 0 || (row.LossCount ?? 0) > 0 || observedAt.HasValue;

            if (!hasActivity)
            {
                return;
            }

            var recent = observedAt.HasValue && (DateTime.UtcNow - observedAt.Value).TotalDays <= 30;
            evidence.Add(new CynoEvidenceItem
            {
                Summary = recent
                    ? "Recent public kill/loss activity within 30 days."
                    : "Public kill/loss summary exists.",
                SignalType = CynoSignalType.Unknown,
                ScoreContribution = recent ? RecentPublicActivityScore : 0,
                Source = "public zKill/local cache summary",
                ObservedAtUtc = observedAt
            });
        }

        private static void AddManualContextEvidence(List<CynoEvidenceItem> evidence, PilotBoardRow row)
        {
            if (!row.KnownCynoOverride)
            {
                return;
            }

            evidence.Add(new CynoEvidenceItem
            {
                Summary = "Manual known-cyno override is set in PMG notes.",
                SignalType = CynoSignalType.Unknown,
                ScoreContribution = ExistingContextHintScore,
                Source = "local PMG note override"
            });
        }

        private static IEnumerable<CynoSignalType> GetHullSignalTypes(string hullName)
        {
            if (NormalHullNames.Contains(hullName))
            {
                yield return CynoSignalType.Normal;
            }

            if (CovertHullNames.Contains(hullName))
            {
                yield return CynoSignalType.Covert;
            }

            if (IndustrialHullNames.Contains(hullName))
            {
                yield return CynoSignalType.Industrial;
            }
        }

        private static CynoSignalType DetermineSignalType(IEnumerable<CynoEvidenceItem> evidence)
        {
            var evidenceList = evidence.ToList();
            var confirmedSignalTypes = evidenceList
                .Where(x => x.IsConfirmedModuleEvidence)
                .Select(x => x.SignalType)
                .Where(x => x != CynoSignalType.Unknown)
                .Distinct()
                .ToList();

            if (confirmedSignalTypes.Count == 1)
            {
                return confirmedSignalTypes[0];
            }

            if (confirmedSignalTypes.Count > 1)
            {
                return CynoSignalType.Mixed;
            }

            var signalTypes = evidenceList
                .Select(x => x.SignalType)
                .Where(x => x != CynoSignalType.Unknown)
                .Distinct()
                .ToList();

            return signalTypes.Count switch
            {
                0 => CynoSignalType.Unknown,
                1 => signalTypes[0],
                _ => CynoSignalType.Mixed
            };
        }

        private static CynoSignalStatus DetermineStatus(int score, IReadOnlyList<CynoEvidenceItem> evidence, int evidenceCount)
        {
            if (evidenceCount == 0 || score <= 14)
            {
                return CynoSignalStatus.Unknown;
            }

            if (evidence.Any(x => x.IsConfirmedModuleEvidence && x.ScoreContribution >= RecentModuleScore))
            {
                return CynoSignalStatus.Confirmed;
            }

            if (score >= 65)
            {
                return CynoSignalStatus.Likely;
            }

            if (score >= 40)
            {
                return CynoSignalStatus.Possible;
            }

            return CynoSignalStatus.Inferred;
        }

        private static string BuildSourceFreshness(PilotBoardRow row, IReadOnlyList<CynoEvidenceItem> evidence)
        {
            var latest = evidence
                .Where(x => x.ObservedAtUtc.HasValue)
                .Select(x => x.ObservedAtUtc!.Value)
                .OrderByDescending(x => x)
                .FirstOrDefault();

            if (latest != default)
            {
                return $"latest public evidence {latest:yyyy-MM-dd}";
            }

            if (!string.IsNullOrWhiteSpace(row.StatsStatusDetail))
            {
                return row.StatsStatusDetail;
            }

            return "No dated public cyno evidence available.";
        }

        private static List<string> BuildLimitations(IReadOnlyList<CynoEvidenceItem> evidence)
        {
            var limitations = new List<string>();
            var hasModuleEvidence = evidence.Any(x => x.IsConfirmedModuleEvidence);

            if (hasModuleEvidence)
            {
                limitations.Add("Public loss data only; no live fit visibility.");
            }
            else
            {
                limitations.Add("Inference only; no confirmed cyno module found.");
            }

            return limitations;
        }

        private static DateTime? TryParseUtc(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed)
                ? parsed
                : null;
        }

        private static string BuildModuleEvidenceSummary(string shipName, DateTime? observedAtUtc, CynoSignalType signalType)
        {
            var shipPart = string.IsNullOrWhiteSpace(shipName)
                ? "Seen"
                : $"Seen {shipName}";
            var datePart = observedAtUtc.HasValue
                ? $" on {observedAtUtc.Value:yyyy-MM-dd}"
                : "";
            return $"{shipPart}{datePart} as victim with {GetSignalTypeEvidenceName(signalType)} fitted.";
        }

        private static string FormatDateForEvidence(DateTime? observedAtUtc)
        {
            return observedAtUtc.HasValue
                ? observedAtUtc.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : "an unknown date";
        }
    }
}
