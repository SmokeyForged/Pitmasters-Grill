using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class BoardInitialSessionAssemblerTests
    {
        [Fact]
        public void Assemble_PerformsPolicyStepsInDeterministicOrder()
        {
            var events = new List<string>();
            var rows = new[]
            {
                new PilotBoardRow { CharacterName = "Beta" },
                new PilotBoardRow { CharacterName = "Alpha" },
                new PilotBoardRow { CharacterName = "Ignored" }
            };
            using var session = new CurrentBoardSession();
            session.Changed += (_, e) =>
            {
                if (e.Kind == CurrentBoardSessionChangeKind.RowsChanged)
                {
                    events.Add($"session:{session.Count}");
                }
            };

            var assembler = new BoardInitialSessionAssembler(
                (_, _, _) =>
                {
                    events.Add("create");
                    return rows.ToList();
                },
                currentRows =>
                {
                    events.Add("hydrate");
                    foreach (var row in currentRows)
                    {
                        row.HasNotes = true;
                    }
                },
                session,
                (currentRows, applyOrderedRows) =>
                {
                    events.Add("ordering");
                    applyOrderedRows(currentRows.Reverse().ToList());
                },
                currentRows =>
                {
                    events.Add("ignore");
                    return currentRows.Where(row => row.CharacterName == "Ignored").ToList();
                },
                currentRows =>
                {
                    events.Add("counts");
                    foreach (var row in currentRows)
                    {
                        row.CorpLocalCount = currentRows.Count;
                    }
                });

            var result = assembler.Assemble(
                new List<string> { "Beta", "Alpha", "Ignored" },
                new Dictionary<string, ResolverCacheEntry>(),
                new Dictionary<string, StatsCacheEntry>());

            Assert.Equal(
                new[]
                {
                    "create",
                    "hydrate",
                    "session:3",
                    "ordering",
                    "session:3",
                    "ignore",
                    "session:2",
                    "counts"
                },
                events);
            Assert.Equal(3, result.CreatedRowCount);
            Assert.Equal(1, result.RemovedIgnoredRowCount);
            Assert.Equal(2, result.FinalRowCount);
            Assert.Equal(new[] { "Alpha", "Beta" }, session.Rows.Select(row => row.CharacterName));
            Assert.All(session.Rows, row =>
            {
                Assert.True(row.HasNotes);
                Assert.Equal(2, row.CorpLocalCount);
            });
        }

        [Fact]
        public void Assemble_AppliesCountsAfterIgnoredRowsAreRemoved()
        {
            var ignored = new PilotBoardRow { CharacterName = "Ignored", CorpId = "10" };
            var kept = new PilotBoardRow { CharacterName = "Kept", CorpId = "10" };
            using var session = new CurrentBoardSession();
            var countService = new BoardAffiliationCountService();
            var assembler = new BoardInitialSessionAssembler(
                (_, _, _) => new List<PilotBoardRow> { ignored, kept },
                _ => { },
                session,
                (_, _) => { },
                _ => new[] { ignored },
                rows => countService.ApplyCounts(rows, showCounts: true));

            var result = assembler.Assemble(
                new List<string> { "Ignored", "Kept" },
                new Dictionary<string, ResolverCacheEntry>(),
                new Dictionary<string, StatsCacheEntry>());

            Assert.Equal(1, result.FinalRowCount);
            Assert.Same(kept, Assert.Single(session.Rows));
            Assert.Equal(1, kept.CorpLocalCount);
        }
    }
}
