using PitmastersGrill.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PitmastersGrill.Services
{
    public static class BoardSummaryTextBuilder
    {
        public static string Build(IEnumerable<PilotBoardRow>? rows)
        {
            var visibleRows = rows?
                .Where(row => row != null)
                .ToList()
                ?? new List<PilotBoardRow>();

            var watchedCount = visibleRows.Count(row => row.IsWatched);
            var baitCount = visibleRows.Count(row => row.BaitOverride || row.HasDerivedBaitEvidence);
            var hardCynoCount = visibleRows.Count(row =>
                string.Equals(row.BoardSignalKind, "ConfirmedNormal", StringComparison.OrdinalIgnoreCase));
            var covertCynoCount = visibleRows.Count(row =>
                string.Equals(row.BoardSignalKind, "ConfirmedCovert", StringComparison.OrdinalIgnoreCase));

            return string.Join(" | ", new[]
            {
                $"Visible {visibleRows.Count}",
                $"Watched {watchedCount}",
                $"Bait {baitCount}",
                $"Hard Cyno {hardCynoCount}",
                $"Covert Cyno {covertCynoCount}"
            });
        }
    }
}
