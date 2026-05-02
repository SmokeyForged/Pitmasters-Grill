using PitmastersGrill.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PitmastersGrill.Services
{
    public sealed record AnalysisAffiliationSummary(string Name, int Count, string Id);

    public sealed record AnalysisAffiliationListItem(string Name, string Id, string EntityType, string DisplayText);

    public sealed record AnalysisHighlightSummary(string Label, string CharacterName, string CharacterId, string ValueText);

    public sealed class AnalysisTabSummary
    {
        public required bool HasVisibleRows { get; init; }
        public required string EmptyStateText { get; init; }
        public required string VisibleCountsText { get; init; }
        public required string UniqueCountsText { get; init; }
        public required IReadOnlyList<AnalysisAffiliationSummary> TopAlliances { get; init; }
        public required IReadOnlyList<AnalysisAffiliationSummary> TopCorps { get; init; }
        public required IReadOnlyList<AnalysisAffiliationSummary> AllAlliances { get; init; }
        public required IReadOnlyList<AnalysisAffiliationSummary> AllCorps { get; init; }
        public required IReadOnlyList<AnalysisHighlightSummary> Highlights { get; init; }
    }

    public sealed class AnalysisTabController
    {
        private const string EmptyStateMessage = "No visible pilots yet. Load or refresh the Grill to see aggregate analysis.";

        public AnalysisTabSummary BuildSummary(IReadOnlyList<PilotBoardRow> visibleRows)
        {
            if (visibleRows == null)
            {
                throw new ArgumentNullException(nameof(visibleRows));
            }

            if (visibleRows.Count == 0)
            {
                return new AnalysisTabSummary
                {
                    HasVisibleRows = false,
                    EmptyStateText = EmptyStateMessage,
                    VisibleCountsText = string.Empty,
                    UniqueCountsText = string.Empty,
                    TopAlliances = Array.Empty<AnalysisAffiliationSummary>(),
                    TopCorps = Array.Empty<AnalysisAffiliationSummary>(),
                    AllAlliances = Array.Empty<AnalysisAffiliationSummary>(),
                    AllCorps = Array.Empty<AnalysisAffiliationSummary>(),
                    Highlights = Array.Empty<AnalysisHighlightSummary>()
                };
            }

            var watchedCount = visibleRows.Count(row => row.IsWatched);
            var uniqueCorpCount = CountDistinctVisibleAffiliations(visibleRows, row => row.CorpName);
            var uniqueAllianceCount = CountDistinctVisibleAffiliations(visibleRows, row => row.AllianceName);
            var topAlliances = BuildVisibleAffiliationDetails(visibleRows, row => row.AllianceName, row => row.AllianceId, maxCount: 3);
            var topCorps = BuildVisibleAffiliationDetails(visibleRows, row => row.CorpName, row => row.CorpId, maxCount: 3);
            var allAlliances = BuildVisibleAffiliationDetails(visibleRows, row => row.AllianceName, row => row.AllianceId);
            var allCorps = BuildVisibleAffiliationDetails(visibleRows, row => row.CorpName, row => row.CorpId);
            var baitCount = visibleRows.Count(row => row.BaitOverride || row.HasDerivedBaitEvidence);
            var hardCynoCount = visibleRows.Count(row =>
                string.Equals(row.BoardSignalKind, "ConfirmedNormal", StringComparison.OrdinalIgnoreCase));
            var covertCynoCount = visibleRows.Count(row =>
                string.Equals(row.BoardSignalKind, "ConfirmedCovert", StringComparison.OrdinalIgnoreCase));

            return new AnalysisTabSummary
            {
                HasVisibleRows = true,
                EmptyStateText = EmptyStateMessage,
                VisibleCountsText = $"Visible pilots: {visibleRows.Count} | Watched pilots: {watchedCount} | Confirmed cynos: Hard {hardCynoCount} | Covert {covertCynoCount} | Bait {baitCount}",
                UniqueCountsText = $"Unique corps: {uniqueCorpCount} | Unique alliances: {uniqueAllianceCount}",
                TopAlliances = topAlliances,
                TopCorps = topCorps,
                AllAlliances = allAlliances,
                AllCorps = allCorps,
                Highlights = BuildHighlights(visibleRows)
            };
        }

        public IReadOnlyList<AnalysisAffiliationListItem> BuildAffiliationListItems(
            IReadOnlyList<AnalysisAffiliationSummary> summaries,
            string entityType)
        {
            if (summaries == null)
            {
                throw new ArgumentNullException(nameof(summaries));
            }

            if (entityType == null)
            {
                throw new ArgumentNullException(nameof(entityType));
            }

            return summaries
                .Select(summary => new AnalysisAffiliationListItem(
                    summary.Name,
                    summary.Id,
                    entityType,
                    $"{summary.Name} [{summary.Count}]"))
                .ToList();
        }

        private static int CountDistinctVisibleAffiliations(
            IEnumerable<PilotBoardRow> rows,
            Func<PilotBoardRow, string> selector)
        {
            return rows
                .Select(row => selector(row)?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        }

        private static List<AnalysisAffiliationSummary> BuildVisibleAffiliationDetails(
            IEnumerable<PilotBoardRow> rows,
            Func<PilotBoardRow, string> nameSelector,
            Func<PilotBoardRow, string> idSelector,
            int? maxCount = null)
        {
            var query = rows
                .Select(row => new
                {
                    Name = nameSelector(row)?.Trim(),
                    Id = idSelector(row)?.Trim()
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .GroupBy(item => item.Name!, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => new AnalysisAffiliationSummary(
                    group.Key,
                    group.Count(),
                    group.Select(item => item.Id).FirstOrDefault(id => !string.IsNullOrWhiteSpace(id)) ?? string.Empty));

            if (maxCount.HasValue)
            {
                query = query.Take(maxCount.Value);
            }

            return query.ToList();
        }

        private static IReadOnlyList<AnalysisHighlightSummary> BuildHighlights(IReadOnlyList<PilotBoardRow> visibleRows)
        {
            return visibleRows
                .Where(row => (row.KillCount ?? 0) > 0 || (row.LossCount ?? 0) > 0)
                .OrderByDescending(ComputeDangerPercent)
                .ThenByDescending(row => row.KillCount ?? 0)
                .ThenBy(row => row.CharacterName, StringComparer.OrdinalIgnoreCase)
                .GroupBy(
                    row => !string.IsNullOrWhiteSpace(row.CharacterId)
                        ? $"id:{row.CharacterId.Trim()}"
                        : $"name:{row.CharacterName.Trim().ToUpperInvariant()}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Take(3)
                .Select((row, index) => new AnalysisHighlightSummary(
                    index == 0 ? "Top Dangerous" : string.Empty,
                    row.CharacterName,
                    row.CharacterId,
                    $"{ComputeDangerPercent(row):0.#}%"))
                .ToList();
        }

        private static double ComputeDangerPercent(PilotBoardRow row)
        {
            var kills = Math.Max(0, row.KillCount ?? 0);
            var losses = Math.Max(0, row.LossCount ?? 0);
            var total = kills + losses;

            return total <= 0
                ? 0
                : (double)kills / total * 100d;
        }
    }
}
