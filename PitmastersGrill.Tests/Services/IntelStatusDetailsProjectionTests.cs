using PitmastersGrill.Models;
using PitmastersGrill.Services;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class IntelStatusDetailsProjectionTests
    {
        [Fact]
        public void Create_WhenProgressIsIndeterminate_ResetsProgressValuesAndAppliesIdleFallbacks()
        {
            var snapshot = new IntelUpdateStatusSnapshot
            {
                TotalProgressIsIndeterminate = true,
                TotalProgressPercent = 73,
                CurrentDayProgressIsIndeterminate = true,
                CurrentDayProgressPercent = 42,
                LiveFeed = null!,
                TodaysFreshness = null!,
                HistoricalFreshness = null!
            };

            var projection = IntelStatusDetailsProjection.Create(snapshot, isShuttingDown: false);

            Assert.True(projection.TotalProgressIsIndeterminate);
            Assert.Equal(0, projection.TotalProgressValue);
            Assert.Equal("No update currently running.", projection.TotalProgressText);
            Assert.True(projection.CurrentDayProgressIsIndeterminate);
            Assert.Equal(0, projection.CurrentDayProgressValue);
            Assert.Equal("No update currently running.", projection.CurrentDayProgressText);
            Assert.Equal("R2Z2", projection.LiveFeedSourceText);
            Assert.Equal("Disabled", projection.LiveFeedStatusText);
            Assert.Equal("Idle", projection.TodaysFreshnessStatusText);
            Assert.Equal("Idle", projection.HistoricalFreshnessStatusText);
        }

        [Fact]
        public void Create_WhenRetryStatesPresent_AppendsRetryTimestampAndFormatsErrorDetails()
        {
            var snapshot = new IntelUpdateStatusSnapshot
            {
                LiveFeed = new R2Z2LiveFeedSnapshot
                {
                    Status = "Backing off",
                    NextRetryAtUtc = "2026-05-06T12:34:56Z",
                    LastErrorAtUtc = "2026-05-06T12:00:00Z",
                    LastError = "HTTP 429"
                },
                TodaysFreshness = new TodaysFreshnessSnapshot
                {
                    Status = "Backing off / rate limited",
                    NextRetryAtUtc = "2026-05-06T12:35:00Z"
                },
                HistoricalFreshness = new HistoricalFreshnessSnapshot
                {
                    Status = "Rate limited",
                    NextRetryAtUtc = "2026-05-06T12:36:00Z"
                }
            };

            var projection = IntelStatusDetailsProjection.Create(snapshot, isShuttingDown: false);

            Assert.Contains("Backing off (retry ", projection.LiveFeedStatusText);
            Assert.Contains("2026-05-06", projection.LiveFeedStatusText);
            Assert.Contains("rate limited (retry ", projection.TodaysFreshnessStatusText);
            Assert.Contains("Rate limited (retry ", projection.HistoricalFreshnessStatusText);
            Assert.Contains("HTTP 429", projection.LiveFeedLastErrorText);
            Assert.Contains("2026-05-06", projection.LiveFeedLastErrorText);
        }

        [Fact]
        public void Create_WhenFreshnessOperationsRunning_DisablesButtonsAndSwapsLabels()
        {
            var snapshot = new IntelUpdateStatusSnapshot
            {
                TodaysFreshness = new TodaysFreshnessSnapshot
                {
                    Status = "Running"
                },
                HistoricalFreshness = new HistoricalFreshnessSnapshot()
            };

            var projection = IntelStatusDetailsProjection.Create(snapshot, isShuttingDown: false);

            Assert.False(projection.RunTodaysFreshnessButtonIsEnabled);
            Assert.Equal("Today's Freshness Running...", projection.RunTodaysFreshnessButtonLabel);
            Assert.False(projection.RunHistoricalFreshnessButtonIsEnabled);
            Assert.Equal("Today's Freshness Running...", projection.RunHistoricalFreshnessButtonLabel);
        }

        [Fact]
        public void Create_WhenHistoricalFreshnessRunning_DisablesOpposingButtonAndUsesHistoricalLabels()
        {
            var snapshot = new IntelUpdateStatusSnapshot
            {
                TodaysFreshness = new TodaysFreshnessSnapshot(),
                HistoricalFreshness = new HistoricalFreshnessSnapshot
                {
                    Status = "Backing off / rate limited"
                }
            };

            var projection = IntelStatusDetailsProjection.Create(snapshot, isShuttingDown: false);

            Assert.False(projection.RunTodaysFreshnessButtonIsEnabled);
            Assert.Equal("Historical Freshness Running...", projection.RunTodaysFreshnessButtonLabel);
            Assert.False(projection.RunHistoricalFreshnessButtonIsEnabled);
            Assert.Equal("Historical Freshness Running...", projection.RunHistoricalFreshnessButtonLabel);
        }

        [Fact]
        public void Create_WhenShuttingDown_DisablesFreshnessButtonsWithoutChangingIdleLabels()
        {
            var snapshot = new IntelUpdateStatusSnapshot
            {
                TodaysFreshness = new TodaysFreshnessSnapshot(),
                HistoricalFreshness = new HistoricalFreshnessSnapshot()
            };

            var projection = IntelStatusDetailsProjection.Create(snapshot, isShuttingDown: true);

            Assert.False(projection.RunTodaysFreshnessButtonIsEnabled);
            Assert.Equal("Refresh Today's zKill Intel", projection.RunTodaysFreshnessButtonLabel);
            Assert.False(projection.RunHistoricalFreshnessButtonIsEnabled);
            Assert.Equal("Repair Recent Historical Intel", projection.RunHistoricalFreshnessButtonLabel);
        }

        [Fact]
        public void Create_WhenProgressExceedsBounds_ClampsAndFormatsSequenceFallbacks()
        {
            var snapshot = new IntelUpdateStatusSnapshot
            {
                TotalProgressPercent = 140,
                CurrentDayProgressPercent = -5,
                LiveFeed = new R2Z2LiveFeedSnapshot
                {
                    NextSequenceId = null,
                    LastProcessedSequenceId = null
                },
                HistoricalFreshness = new HistoricalFreshnessSnapshot()
            };

            var projection = IntelStatusDetailsProjection.Create(snapshot, isShuttingDown: false);

            Assert.Equal(100, projection.TotalProgressValue);
            Assert.Equal(0, projection.CurrentDayProgressValue);
            Assert.Equal("Not initialized", projection.LiveFeedNextSequenceText);
            Assert.Equal("None", projection.LiveFeedLastProcessedSequenceText);
            Assert.Equal("Not run yet", projection.HistoricalFreshnessModeText);
        }
    }
}
