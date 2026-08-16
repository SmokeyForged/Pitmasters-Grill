using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System.Collections.Generic;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class BoardRowStateHydratorTests
    {
        [Fact]
        public void Hydrate_AppliesPersistedRowStateAndDerivedDisplayUpdate()
        {
            var updatedRows = new List<PilotBoardRow>();
            var hydrator = new BoardRowStateHydrator(
                characterName => characterName == "Pilot One",
                characterName => characterName == "Pilot One",
                characterName => characterName == "Pilot One",
                characterId => characterId == "42",
                row =>
                {
                    row.HasConfirmedCynoModuleEvidence = true;
                    updatedRows.Add(row);
                });
            var row = new PilotBoardRow
            {
                CharacterName = "Pilot One",
                CharacterId = "42"
            };

            hydrator.Hydrate(row);

            Assert.True(row.KnownCynoOverride);
            Assert.True(row.BaitOverride);
            Assert.True(row.HasNotes);
            Assert.True(row.IsWatched);
            Assert.True(row.HasConfirmedCynoModuleEvidence);
            Assert.Same(row, Assert.Single(updatedRows));
        }

        [Fact]
        public void Hydrate_PreservesUnresolvedIdentityInputs()
        {
            string? watchedLookup = null;
            var formatterCallCount = 0;
            var hydrator = new BoardRowStateHydrator(
                _ => false,
                _ => false,
                _ => false,
                characterId =>
                {
                    watchedLookup = characterId;
                    return false;
                },
                _ => formatterCallCount++);
            var row = new PilotBoardRow
            {
                CharacterName = "Unresolved Pilot",
                CharacterId = string.Empty
            };

            hydrator.Hydrate(row);

            Assert.Equal(string.Empty, watchedLookup);
            Assert.False(row.IsWatched);
            Assert.Equal(1, formatterCallCount);
        }

        [Fact]
        public void Hydrate_MultipleRowsAppliesEachRowExactlyOnce()
        {
            var knownCalls = 0;
            var baitCalls = 0;
            var noteCalls = 0;
            var watchCalls = 0;
            var formatterCalls = 0;
            var hydrator = new BoardRowStateHydrator(
                _ => { knownCalls++; return false; },
                _ => { baitCalls++; return false; },
                _ => { noteCalls++; return false; },
                _ => { watchCalls++; return false; },
                _ => formatterCalls++);
            var rows = new[]
            {
                new PilotBoardRow { CharacterName = "One", CharacterId = "1" },
                new PilotBoardRow { CharacterName = "Two", CharacterId = "2" }
            };

            hydrator.Hydrate(rows);

            Assert.Equal(2, knownCalls);
            Assert.Equal(2, baitCalls);
            Assert.Equal(2, noteCalls);
            Assert.Equal(2, watchCalls);
            Assert.Equal(2, formatterCalls);
        }
    }
}
