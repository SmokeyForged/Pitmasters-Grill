using PitmastersGrill.Services;
using System;
using System.Linq;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public class IgnoreAllianceFilterServiceTests
    {
        [Fact]
        public void Filter_RemovesItemsWhoseAllianceIdsAreIgnored()
        {
            var service = new IgnoreAllianceFilterService();
            var items = new[]
            {
                new AllianceRow("Aura", 1001),
                new AllianceRow("Chribba", 2002),
                new AllianceRow("The Mittani", null),
                new AllianceRow("Mynxee", 1001)
            };

            var result = service.Filter(items, new long[] { 1001 }, item => item.AllianceId);

            Assert.Equal(new[] { "Chribba", "The Mittani" }, result.KeptItems.Select(x => x.Name).ToArray());
            Assert.Equal(new[] { "Aura", "Mynxee" }, result.RemovedItems.Select(x => x.Name).ToArray());
            Assert.Equal(2, result.RemovedCount);
        }

        [Fact]
        public void Filter_TreatsNullIgnoredAllianceListAsEmpty()
        {
            var service = new IgnoreAllianceFilterService();
            var items = new[]
            {
                new AllianceRow("Aura", 1001),
                new AllianceRow("Chribba", 2002)
            };

            var result = service.Filter(items, null!, item => item.AllianceId);

            Assert.Equal(2, result.KeptItems.Count);
            Assert.Empty(result.RemovedItems);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData(0L, false)]
        [InlineData(-5L, false)]
        [InlineData(44L, true)]
        [InlineData(45L, false)]
        public void ShouldIgnore_MatchesOnlyPositiveIdsInIgnoreSet(long? allianceId, bool expected)
        {
            var service = new IgnoreAllianceFilterService();

            var result = service.ShouldIgnore(allianceId, new long[] { -1, 0, 44 });

            Assert.Equal(expected, result);
        }

        private sealed record AllianceRow(string Name, long? AllianceId);
    }
}
