using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System.Collections.Generic;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class IntelActionsControllerTests
    {
        [Fact]
        public void CollectVisibleCharacterIds_FiltersInvalidValuesAndPreservesFirstDistinctOrder()
        {
            var rows = new List<PilotBoardRow>
            {
                new() { CharacterId = "1001" },
                new() { CharacterId = "abc" },
                new() { CharacterId = "1002" },
                new() { CharacterId = "1001" },
                new() { CharacterId = "" },
                new() { CharacterId = "1003" }
            };

            var result = IntelActionsController.CollectVisibleCharacterIds(rows);

            Assert.Equal(new long[] { 1001, 1002, 1003 }, result);
        }

        [Fact]
        public void CollectVisibleCharacterIds_ReturnsEmptyWhenNoResolvedCharacterIdsExist()
        {
            var rows = new List<PilotBoardRow>
            {
                new() { CharacterId = string.Empty },
                new() { CharacterId = "" },
                new() { CharacterId = "not-a-number" }
            };

            var result = IntelActionsController.CollectVisibleCharacterIds(rows);

            Assert.Empty(result);
        }
    }
}
