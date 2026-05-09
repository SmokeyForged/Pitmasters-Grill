using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class IntelStatusDetailsPresenterTests
    {
        [Fact]
        public void Apply_UpdatesRepresentativeFieldsAndButtonState()
        {
            RunOnStaThread(() =>
            {
                var controls = CreateControls();
                var presenter = CreatePresenter(controls);
                var projection = IntelStatusDetailsProjection.Create(
                    new IntelUpdateStatusSnapshot
                    {
                        LastSuccessfulUpdateAtUtc = "2026-05-08T12:34:56Z",
                        TotalProgressPercent = 42,
                        TotalProgressText = "Importing day 3 of 7",
                        LiveFeed = new R2Z2LiveFeedSnapshot
                        {
                            Enabled = true,
                            Status = "Running",
                            RecentLiveImportsCount = 7
                        },
                        TodaysFreshness = new TodaysFreshnessSnapshot
                        {
                            Status = "Idle",
                            VisiblePilotsTargeted = 12,
                            DetailText = "Today's Freshness is idle."
                        },
                        HistoricalFreshness = new HistoricalFreshnessSnapshot
                        {
                            Status = "Running",
                            Mode = "Recent"
                        }
                    },
                    isShuttingDown: false);

                presenter.Apply(projection);

                Assert.Equal("Importing day 3 of 7", controls.IntelTotalProgressText.Text);
                Assert.Equal(42, controls.IntelTotalProgressBar.Value);
                Assert.Equal("Yes", controls.IntelLiveFeedEnabledText.Text);
                Assert.Equal("7", controls.IntelLiveFeedRecentImportsText.Text);
                Assert.Equal("12", controls.TodaysFreshnessVisiblePilotsText.Text);
                Assert.False(controls.RunTodaysFreshnessButton.IsEnabled);
                Assert.Equal("Historical Freshness Running...", controls.RunTodaysFreshnessButton.Content);
                Assert.Equal("Running", controls.HistoricalFreshnessStatusText.Text);
                Assert.Equal("Recent", controls.HistoricalFreshnessModeText.Text);
            });
        }

        private static IntelStatusDetailsPresenter CreatePresenter(IntelStatusControls controls)
        {
            return new IntelStatusDetailsPresenter(
                controls.IntelLastUpdatedText,
                controls.IntelOldestKillmailDayText,
                controls.IntelNewestKillmailDayText,
                controls.IntelCurrentUpdateStatusText,
                controls.IntelTotalProgressBar,
                controls.IntelTotalProgressText,
                controls.IntelCurrentDayProgressBar,
                controls.IntelCurrentDayProgressText,
                controls.IntelLiveFeedSourceText,
                controls.IntelLiveFeedStatusText,
                controls.IntelLiveFeedEnabledText,
                controls.IntelLiveFeedRecentImportsText,
                controls.IntelLiveFeedNextSequenceText,
                controls.IntelLiveFeedLastProcessedSequenceText,
                controls.IntelLiveFeedLastSuccessText,
                controls.IntelLiveFeedLastCaughtUpText,
                controls.IntelLiveFeedLastErrorText,
                controls.TodaysFreshnessStatusText,
                controls.TodaysFreshnessVisiblePilotsText,
                controls.TodaysFreshnessEntitiesQueriedText,
                controls.TodaysFreshnessResultsFoundText,
                controls.TodaysFreshnessKnownSkippedText,
                controls.TodaysFreshnessImportedText,
                controls.TodaysFreshnessFailedText,
                controls.TodaysFreshnessLastRunText,
                controls.TodaysFreshnessDetailText,
                controls.TodaysFreshnessLastErrorText,
                controls.RunTodaysFreshnessButton,
                controls.HistoricalFreshnessStatusText,
                controls.HistoricalFreshnessModeText,
                controls.HistoricalFreshnessVisiblePilotsText,
                controls.HistoricalFreshnessCandidatesConsideredText,
                controls.HistoricalFreshnessCandidatesSkippedCooldownText,
                controls.HistoricalFreshnessPilotsCheckedText,
                controls.HistoricalFreshnessDaysCheckedText,
                controls.HistoricalFreshnessEntitiesQueriedText,
                controls.HistoricalFreshnessResultsFoundText,
                controls.HistoricalFreshnessKnownSkippedText,
                controls.HistoricalFreshnessImportedText,
                controls.HistoricalFreshnessFailedText,
                controls.HistoricalFreshnessLastRunText,
                controls.HistoricalFreshnessDetailText,
                controls.HistoricalFreshnessLastErrorText,
                controls.RunHistoricalFreshnessButton);
        }

        private static IntelStatusControls CreateControls()
        {
            return new IntelStatusControls(
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new ProgressBar(),
                new TextBlock(),
                new ProgressBar(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new Button(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new Button());
        }

        private sealed record IntelStatusControls(
            TextBlock IntelLastUpdatedText,
            TextBlock IntelOldestKillmailDayText,
            TextBlock IntelNewestKillmailDayText,
            TextBlock IntelCurrentUpdateStatusText,
            ProgressBar IntelTotalProgressBar,
            TextBlock IntelTotalProgressText,
            ProgressBar IntelCurrentDayProgressBar,
            TextBlock IntelCurrentDayProgressText,
            TextBlock IntelLiveFeedSourceText,
            TextBlock IntelLiveFeedStatusText,
            TextBlock IntelLiveFeedEnabledText,
            TextBlock IntelLiveFeedRecentImportsText,
            TextBlock IntelLiveFeedNextSequenceText,
            TextBlock IntelLiveFeedLastProcessedSequenceText,
            TextBlock IntelLiveFeedLastSuccessText,
            TextBlock IntelLiveFeedLastCaughtUpText,
            TextBlock IntelLiveFeedLastErrorText,
            TextBlock TodaysFreshnessStatusText,
            TextBlock TodaysFreshnessVisiblePilotsText,
            TextBlock TodaysFreshnessEntitiesQueriedText,
            TextBlock TodaysFreshnessResultsFoundText,
            TextBlock TodaysFreshnessKnownSkippedText,
            TextBlock TodaysFreshnessImportedText,
            TextBlock TodaysFreshnessFailedText,
            TextBlock TodaysFreshnessLastRunText,
            TextBlock TodaysFreshnessDetailText,
            TextBlock TodaysFreshnessLastErrorText,
            Button RunTodaysFreshnessButton,
            TextBlock HistoricalFreshnessStatusText,
            TextBlock HistoricalFreshnessModeText,
            TextBlock HistoricalFreshnessVisiblePilotsText,
            TextBlock HistoricalFreshnessCandidatesConsideredText,
            TextBlock HistoricalFreshnessCandidatesSkippedCooldownText,
            TextBlock HistoricalFreshnessPilotsCheckedText,
            TextBlock HistoricalFreshnessDaysCheckedText,
            TextBlock HistoricalFreshnessEntitiesQueriedText,
            TextBlock HistoricalFreshnessResultsFoundText,
            TextBlock HistoricalFreshnessKnownSkippedText,
            TextBlock HistoricalFreshnessImportedText,
            TextBlock HistoricalFreshnessFailedText,
            TextBlock HistoricalFreshnessLastRunText,
            TextBlock HistoricalFreshnessDetailText,
            TextBlock HistoricalFreshnessLastErrorText,
            Button RunHistoricalFreshnessButton);

        private static void RunOnStaThread(Action action)
        {
            Exception? capturedException = null;

            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    capturedException = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (capturedException != null)
            {
                ExceptionDispatchInfo.Capture(capturedException).Throw();
            }
        }
    }
}
