using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System.Collections.Generic;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class BoardSummaryTextBuilderTests
    {
        [Fact]
        public void Build_WithNoRows_ReturnsZeroCounts()
        {
            var result = BoardSummaryTextBuilder.Build(new List<PilotBoardRow>());

            Assert.Equal(
                "Visible 0 | Watched 0 | Bait 0 | Hard Cyno 0 | Covert Cyno 0",
                result);
        }

        [Fact]
        public void Build_WithMixedRows_CountsVisibleWatchedBaitAndCynoKinds()
        {
            var rows = new List<PilotBoardRow>
            {
                new()
                {
                    CharacterName = "Scout One",
                    IsWatched = true,
                    BoardSignalKind = "ConfirmedNormal"
                },
                new()
                {
                    CharacterName = "Scout Two",
                    HasDerivedBaitEvidence = true,
                    BoardSignalKind = "ConfirmedCovert"
                },
                new()
                {
                    CharacterName = "Scout Three",
                    BaitOverride = true,
                    BoardSignalKind = "Unknown"
                },
                new()
                {
                    CharacterName = "Scout Four"
                }
            };

            var result = BoardSummaryTextBuilder.Build(rows);

            Assert.Equal(
                "Visible 4 | Watched 1 | Bait 2 | Hard Cyno 1 | Covert Cyno 1",
                result);
        }

        [Fact]
        public void Build_TreatsSignalKindsCaseInsensitively()
        {
            var rows = new List<PilotBoardRow>
            {
                new() { BoardSignalKind = "confirmednormal" },
                new() { BoardSignalKind = "CONFIRMEDCOVERT" }
            };

            var result = BoardSummaryTextBuilder.Build(rows);

            Assert.Equal(
                "Visible 2 | Watched 0 | Bait 0 | Hard Cyno 1 | Covert Cyno 1",
                result);
        }

        [Fact]
        public void Build_WithNullRows_ReturnsZeroCounts()
        {
            var result = BoardSummaryTextBuilder.Build(null);

            Assert.Equal(
                "Visible 0 | Watched 0 | Bait 0 | Hard Cyno 0 | Covert Cyno 0",
                result);
        }
    }
}
