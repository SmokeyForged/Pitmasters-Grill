using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System.Collections.Generic;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public class AnalysisTabControllerTests
    {
        [Fact]
        public void BuildSummary_ReturnsEmptyStateForNoVisibleRows()
        {
            var controller = new AnalysisTabController();

            var summary = controller.BuildSummary(new List<PilotBoardRow>());

            Assert.False(summary.HasVisibleRows);
            Assert.Equal("No visible pilots yet. Load or refresh the Grill to see aggregate analysis.", summary.EmptyStateText);
            Assert.Empty(summary.TopAlliances);
            Assert.Empty(summary.TopCorps);
            Assert.Empty(summary.AllAlliances);
            Assert.Empty(summary.AllCorps);
            Assert.Empty(summary.Highlights);
            Assert.Equal(string.Empty, summary.VisibleCountsText);
            Assert.Equal(string.Empty, summary.UniqueCountsText);
        }

        [Fact]
        public void BuildSummary_ComputesVisibleCountsUniqueCountsAndAffiliationLists()
        {
            var controller = new AnalysisTabController();
            var rows = new List<PilotBoardRow>
            {
                CreateRow(
                    characterName: "Alice",
                    corpName: " Corp One ",
                    corpId: "2001",
                    allianceName: " Alliance A ",
                    allianceId: "3001",
                    isWatched: true,
                    boardSignalKind: "ConfirmedNormal"),
                CreateRow(
                    characterName: "Bob",
                    corpName: "corp one",
                    corpId: "",
                    allianceName: "alliance a",
                    allianceId: "",
                    hasDerivedBaitEvidence: true,
                    boardSignalKind: "ConfirmedCovert"),
                CreateRow(
                    characterName: "Cara",
                    corpName: "Corp Two",
                    corpId: "2002",
                    allianceName: "Alliance B",
                    allianceId: "3002",
                    baitOverride: true),
                CreateRow(
                    characterName: "Dax",
                    corpName: "  ",
                    allianceName: null)
            };

            var summary = controller.BuildSummary(rows);

            Assert.True(summary.HasVisibleRows);
            Assert.Equal("Visible pilots: 4 | Watched pilots: 1 | Confirmed cynos: Hard 1 | Covert 1 | Bait 2", summary.VisibleCountsText);
            Assert.Equal("Unique corps: 2 | Unique alliances: 2", summary.UniqueCountsText);

            Assert.Equal(2, summary.TopAlliances.Count);
            Assert.Equal("Alliance A", summary.TopAlliances[0].Name);
            Assert.Equal(2, summary.TopAlliances[0].Count);
            Assert.Equal("3001", summary.TopAlliances[0].Id);
            Assert.Equal("Alliance B", summary.TopAlliances[1].Name);

            Assert.Equal(2, summary.TopCorps.Count);
            Assert.Equal("Corp One", summary.TopCorps[0].Name);
            Assert.Equal(2, summary.TopCorps[0].Count);
            Assert.Equal("2001", summary.TopCorps[0].Id);
            Assert.Equal("Corp Two", summary.TopCorps[1].Name);

            var allianceItems = controller.BuildAffiliationListItems(summary.AllAlliances, "alliance");
            Assert.Equal(2, allianceItems.Count);
            Assert.Equal("alliance", allianceItems[0].EntityType);
            Assert.Equal("Alliance A [2]", allianceItems[0].DisplayText);

            var corpItems = controller.BuildAffiliationListItems(summary.AllCorps, "corporation");
            Assert.Equal(2, corpItems.Count);
            Assert.Equal("corporation", corpItems[0].EntityType);
            Assert.Equal("Corp One [2]", corpItems[0].DisplayText);
        }

        [Fact]
        public void BuildSummary_BuildsDangerHighlightsWithDedupingAndStableOrdering()
        {
            var controller = new AnalysisTabController();
            var rows = new List<PilotBoardRow>
            {
                CreateRow(characterName: "Ace", characterId: "9001", killCount: 9, lossCount: 1),
                CreateRow(characterName: "Ace Duplicate", characterId: "9001", killCount: 1, lossCount: 0),
                CreateRow(characterName: "Blaze", characterId: "", killCount: 4, lossCount: 1),
                CreateRow(characterName: "blaze", characterId: "", killCount: 2, lossCount: 1),
                CreateRow(characterName: "Cora", characterId: "9003", killCount: 1, lossCount: 1),
                CreateRow(characterName: "Echo", characterId: "9004", killCount: 2, lossCount: 8),
                CreateRow(characterName: "Idle", characterId: "9005", killCount: 0, lossCount: 0)
            };

            var summary = controller.BuildSummary(rows);

            Assert.Equal(3, summary.Highlights.Count);

            Assert.Equal("Top Dangerous", summary.Highlights[0].Label);
            Assert.Equal("Ace Duplicate", summary.Highlights[0].CharacterName);
            Assert.Equal("9001", summary.Highlights[0].CharacterId);
            Assert.Equal("100%", summary.Highlights[0].ValueText);

            Assert.Equal(string.Empty, summary.Highlights[1].Label);
            Assert.Equal("Blaze", summary.Highlights[1].CharacterName);
            Assert.Equal("80%", summary.Highlights[1].ValueText);

            Assert.Equal("Cora", summary.Highlights[2].CharacterName);
            Assert.Equal("50%", summary.Highlights[2].ValueText);
        }

        [Fact]
        public void BuildSummary_TrimsNamesAndSortsTiedAffiliationsAlphabetically()
        {
            var controller = new AnalysisTabController();
            var rows = new List<PilotBoardRow>
            {
                CreateRow(characterName: "Pilot 1", corpName: " Bravo Corp ", corpId: "22", allianceName: "Zulu", allianceId: "92"),
                CreateRow(characterName: "Pilot 2", corpName: "Alpha Corp", corpId: "11", allianceName: "Alpha", allianceId: "91"),
                CreateRow(characterName: "Pilot 3", corpName: "alpha corp", corpId: "", allianceName: "alpha", allianceId: ""),
                CreateRow(characterName: "Pilot 4", corpName: "bravo corp", corpId: "", allianceName: "zulu", allianceId: "")
            };

            var summary = controller.BuildSummary(rows);

            Assert.Equal("Alpha", summary.TopAlliances[0].Name);
            Assert.Equal("Zulu", summary.TopAlliances[1].Name);
            Assert.Equal("Alpha Corp", summary.TopCorps[0].Name);
            Assert.Equal("Bravo Corp", summary.TopCorps[1].Name);
            Assert.Equal("11", summary.TopCorps[0].Id);
            Assert.Equal("22", summary.TopCorps[1].Id);
        }

        private static PilotBoardRow CreateRow(
            string characterName,
            string? characterId = null,
            string? corpName = null,
            string? corpId = null,
            string? allianceName = null,
            string? allianceId = null,
            bool isWatched = false,
            bool hasDerivedBaitEvidence = false,
            bool baitOverride = false,
            string? boardSignalKind = null,
            int? killCount = null,
            int? lossCount = null)
        {
            return new PilotBoardRow
            {
                CharacterName = characterName,
                CharacterId = characterId ?? string.Empty,
                CorpName = corpName ?? string.Empty,
                CorpId = corpId ?? string.Empty,
                AllianceName = allianceName ?? string.Empty,
                AllianceId = allianceId ?? string.Empty,
                IsWatched = isWatched,
                HasDerivedBaitEvidence = hasDerivedBaitEvidence,
                BaitOverride = baitOverride,
                BoardSignalKind = boardSignalKind ?? string.Empty,
                KillCount = killCount,
                LossCount = lossCount
            };
        }
    }
}
