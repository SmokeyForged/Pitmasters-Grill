using PitmastersGrill.Services;
using System;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class BoardStatusPresenterTests
    {
        [Fact]
        public void BuildLastRefreshedText_UsesInjectedLocalClock()
        {
            var clock = new FixedTimeProvider(
                new DateTimeOffset(2026, 8, 18, 1, 52, 30, TimeSpan.Zero),
                TimeZoneInfo.CreateCustomTimeZone(
                    "Test Eastern",
                    TimeSpan.FromHours(-4),
                    "Test Eastern",
                    "Test Eastern"));
            var subject = new BoardStatusPresenter(clock);

            var text = subject.BuildLastRefreshedText();

            Assert.Equal("Last Refreshed: 2026-08-17 21:52:30", text);
        }

        private sealed class FixedTimeProvider : TimeProvider
        {
            private readonly DateTimeOffset _utcNow;
            private readonly TimeZoneInfo _localTimeZone;

            public FixedTimeProvider(DateTimeOffset utcNow, TimeZoneInfo localTimeZone)
            {
                _utcNow = utcNow;
                _localTimeZone = localTimeZone;
            }

            public override DateTimeOffset GetUtcNow() => _utcNow;

            public override TimeZoneInfo LocalTimeZone => _localTimeZone;
        }
    }
}
