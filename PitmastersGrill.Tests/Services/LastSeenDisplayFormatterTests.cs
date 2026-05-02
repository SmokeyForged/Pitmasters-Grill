using System;
using PitmastersGrill.Services;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public class LastSeenDisplayFormatterTests
    {
        [Fact]
        public void FormatLastSeen_FormatsRecentAndPastValues()
        {
            var nowUtc = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

            Assert.Equal("now", LastSeenDisplayFormatter.FormatLastSeen("2026-05-01T12:00:00Z", nowUtc));
            Assert.Equal("15m ago", LastSeenDisplayFormatter.FormatLastSeen("2026-05-01T11:45:00Z", nowUtc));
            Assert.Equal("2h ago", LastSeenDisplayFormatter.FormatLastSeen("2026-05-01T10:00:00Z", nowUtc));
            Assert.Equal("2d ago", LastSeenDisplayFormatter.FormatLastSeen("2026-04-29T23:59:59Z", nowUtc));
        }

        [Fact]
        public void FormatLastSeen_ReturnsEmptyForBlankOrInvalidInput()
        {
            var nowUtc = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

            Assert.Equal(string.Empty, LastSeenDisplayFormatter.FormatLastSeen("", nowUtc));
            Assert.Equal(string.Empty, LastSeenDisplayFormatter.FormatLastSeen("not-a-date", nowUtc));
        }

        [Fact]
        public void FormatLastSeen_ClampsFutureTimestampsToNow()
        {
            var nowUtc = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

            var result = LastSeenDisplayFormatter.FormatLastSeen("2026-05-01T13:00:00Z", nowUtc);

            Assert.Equal("now", result);
        }
    }
}
