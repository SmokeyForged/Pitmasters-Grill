using PitmastersGrill.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PitmastersGrill.Services
{
    public sealed class BoardAffiliationCountService
    {
        public void ApplyCounts(IReadOnlyList<PilotBoardRow> rows, bool showCounts)
        {
            ArgumentNullException.ThrowIfNull(rows);

            var corpCounts = BuildCounts(rows, row => BuildAffiliationCountKey(row.CorpId, row.CorpName));
            var allianceCounts = BuildCounts(rows, row => BuildAffiliationCountKey(row.AllianceId, row.AllianceName));

            foreach (var row in rows)
            {
                row.ShowCorpAllianceCounts = showCounts;

                var corpKey = BuildAffiliationCountKey(row.CorpId, row.CorpName);
                row.CorpLocalCount = TryGetCount(corpCounts, corpKey);

                var allianceKey = BuildAffiliationCountKey(row.AllianceId, row.AllianceName);
                row.AllianceLocalCount = TryGetCount(allianceCounts, allianceKey);
            }
        }

        private static Dictionary<string, int> BuildCounts(
            IReadOnlyList<PilotBoardRow> rows,
            Func<PilotBoardRow, string> keySelector)
        {
            return rows
                .Select(keySelector)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .GroupBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        }

        private static int TryGetCount(IReadOnlyDictionary<string, int> counts, string key)
        {
            return !string.IsNullOrWhiteSpace(key) && counts.TryGetValue(key, out var count)
                ? count
                : 0;
        }

        private static string BuildAffiliationCountKey(string id, string name)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                return $"id:{id.Trim()}";
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                return $"name:{name.Trim().ToUpperInvariant()}";
            }

            return string.Empty;
        }
    }
}
