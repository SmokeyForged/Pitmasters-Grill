using PitmastersGrill.Services;
using System;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public class KillmailDatasetFreshnessServiceTests
    {
        [Theory]
        [InlineData(-1, 30)]
        [InlineData(0, 30)]
        [InlineData(1, 1)]
        [InlineData(30, 30)]
        [InlineData(999, 365)]
        public void NormalizeMaxKillmailAgeDays_ClampsToSupportedRange(int input, int expected)
        {
            var result = KillmailDatasetFreshnessService.NormalizeMaxKillmailAgeDays(input);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void BuildBootstrapStartDayUtc_UsesPreviousUtcDayAsRequiredThroughDay()
        {
            var nowUtc = new DateTime(2026, 5, 1, 15, 30, 0, DateTimeKind.Utc);

            var result = KillmailDatasetFreshnessService.BuildBootstrapStartDayUtc(nowUtc, 3);

            Assert.Equal("2026-04-28", result);
        }

        [Fact]
        public void BuildBootstrapStartDayUtc_UsesNormalizedDefaultForInvalidAge()
        {
            var nowUtc = new DateTime(2026, 5, 1, 0, 5, 0, DateTimeKind.Utc);

            var result = KillmailDatasetFreshnessService.BuildBootstrapStartDayUtc(nowUtc, 0);

            Assert.Equal("2026-04-01", result);
        }
    }
}
