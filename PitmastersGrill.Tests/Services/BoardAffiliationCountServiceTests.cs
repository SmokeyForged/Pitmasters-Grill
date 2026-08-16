using PitmastersGrill.Models;
using PitmastersGrill.Services;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class BoardAffiliationCountServiceTests
    {
        private readonly BoardAffiliationCountService _service = new();

        [Fact]
        public void ApplyCounts_GroupsByIdBeforeName()
        {
            var rows = new[]
            {
                new PilotBoardRow { CorpId = "100", CorpName = "Shared Corp" },
                new PilotBoardRow { CorpId = "200", CorpName = "Shared Corp" }
            };

            _service.ApplyCounts(rows, showCounts: true);

            Assert.All(rows, row => Assert.Equal(1, row.CorpLocalCount));
        }

        [Fact]
        public void ApplyCounts_UsesCaseInsensitiveNameFallbackWithoutId()
        {
            var rows = new[]
            {
                new PilotBoardRow { CorpName = "Fallback Corp" },
                new PilotBoardRow { CorpName = "fallback corp" }
            };

            _service.ApplyCounts(rows, showCounts: true);

            Assert.All(rows, row => Assert.Equal(2, row.CorpLocalCount));
        }

        [Fact]
        public void ApplyCounts_PrefersSharedIdEvenWhenNamesDiffer()
        {
            var rows = new[]
            {
                new PilotBoardRow { AllianceId = "300", AllianceName = "Alliance A" },
                new PilotBoardRow { AllianceId = "300", AllianceName = "Alliance B" }
            };

            _service.ApplyCounts(rows, showCounts: true);

            Assert.All(rows, row => Assert.Equal(2, row.AllianceLocalCount));
        }

        [Fact]
        public void ApplyCounts_LeavesUnresolvedAffiliationsAtZero()
        {
            var row = new PilotBoardRow();

            _service.ApplyCounts(new[] { row }, showCounts: true);

            Assert.Equal(0, row.CorpLocalCount);
            Assert.Equal(0, row.AllianceLocalCount);
        }

        [Fact]
        public void ApplyCounts_PreservesCountsWhenDisplayIsDisabled()
        {
            var rows = new[]
            {
                new PilotBoardRow { CorpId = "100" },
                new PilotBoardRow { CorpId = "100" }
            };

            _service.ApplyCounts(rows, showCounts: false);

            Assert.All(rows, row =>
            {
                Assert.False(row.ShowCorpAllianceCounts);
                Assert.Equal(2, row.CorpLocalCount);
            });
        }

        [Fact]
        public void ApplyCounts_ComputesCorporationAndAllianceIndependently()
        {
            var rows = new[]
            {
                new PilotBoardRow { CorpId = "100", AllianceId = "900" },
                new PilotBoardRow { CorpId = "100", AllianceId = "901" },
                new PilotBoardRow { CorpId = "101", AllianceId = "900" }
            };

            _service.ApplyCounts(rows, showCounts: true);

            Assert.Equal(2, rows[0].CorpLocalCount);
            Assert.Equal(2, rows[0].AllianceLocalCount);
            Assert.Equal(2, rows[1].CorpLocalCount);
            Assert.Equal(1, rows[1].AllianceLocalCount);
            Assert.Equal(1, rows[2].CorpLocalCount);
            Assert.Equal(2, rows[2].AllianceLocalCount);
        }
    }
}
