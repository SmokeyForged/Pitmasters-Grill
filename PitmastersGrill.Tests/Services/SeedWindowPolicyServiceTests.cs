using System;
using PitmastersGrill.Services;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public class SeedWindowPolicyServiceTests
    {
        [Fact]
        public void GetExplicitWindowUtc_ReturnsInclusiveDayCount()
        {
            var service = new SeedWindowPolicyService();

            var result = service.GetExplicitWindowUtc("2026-04-01", "2026-04-03");

            Assert.Equal("2026-04-01", result.StartDayUtc);
            Assert.Equal("2026-04-03", result.EndDayUtc);
            Assert.Equal(3, result.DayCount);
        }

        [Fact]
        public void GetExplicitWindowUtc_RejectsInvertedRange()
        {
            var service = new SeedWindowPolicyService();

            var ex = Assert.Throws<InvalidOperationException>(() => service.GetExplicitWindowUtc("2026-04-03", "2026-04-01"));

            Assert.Contains("before start day", ex.Message, StringComparison.Ordinal);
        }
    }
}
