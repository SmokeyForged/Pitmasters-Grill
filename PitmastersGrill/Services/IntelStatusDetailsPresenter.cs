using System;
using System.Windows.Controls;

namespace PitmastersGrill.Services
{
    public sealed class IntelStatusDetailsPresenter
    {
        private readonly TextBlock _intelLastUpdatedText;
        private readonly TextBlock _intelOldestKillmailDayText;
        private readonly TextBlock _intelNewestKillmailDayText;
        private readonly TextBlock _intelCurrentUpdateStatusText;
        private readonly ProgressBar _intelTotalProgressBar;
        private readonly TextBlock _intelTotalProgressText;
        private readonly ProgressBar _intelCurrentDayProgressBar;
        private readonly TextBlock _intelCurrentDayProgressText;
        private readonly TextBlock _intelLiveFeedSourceText;
        private readonly TextBlock _intelLiveFeedStatusText;
        private readonly TextBlock _intelLiveFeedEnabledText;
        private readonly TextBlock _intelLiveFeedRecentImportsText;
        private readonly TextBlock _intelLiveFeedNextSequenceText;
        private readonly TextBlock _intelLiveFeedLastProcessedSequenceText;
        private readonly TextBlock _intelLiveFeedLastSuccessText;
        private readonly TextBlock _intelLiveFeedLastCaughtUpText;
        private readonly TextBlock _intelLiveFeedLastErrorText;
        private readonly TextBlock _todaysFreshnessStatusText;
        private readonly TextBlock _todaysFreshnessVisiblePilotsText;
        private readonly TextBlock _todaysFreshnessEntitiesQueriedText;
        private readonly TextBlock _todaysFreshnessResultsFoundText;
        private readonly TextBlock _todaysFreshnessKnownSkippedText;
        private readonly TextBlock _todaysFreshnessImportedText;
        private readonly TextBlock _todaysFreshnessFailedText;
        private readonly TextBlock _todaysFreshnessLastRunText;
        private readonly TextBlock _todaysFreshnessDetailText;
        private readonly TextBlock _todaysFreshnessLastErrorText;
        private readonly Button _runTodaysFreshnessButton;
        private readonly TextBlock _historicalFreshnessStatusText;
        private readonly TextBlock _historicalFreshnessModeText;
        private readonly TextBlock _historicalFreshnessVisiblePilotsText;
        private readonly TextBlock _historicalFreshnessCandidatesConsideredText;
        private readonly TextBlock _historicalFreshnessCandidatesSkippedCooldownText;
        private readonly TextBlock _historicalFreshnessPilotsCheckedText;
        private readonly TextBlock _historicalFreshnessDaysCheckedText;
        private readonly TextBlock _historicalFreshnessEntitiesQueriedText;
        private readonly TextBlock _historicalFreshnessResultsFoundText;
        private readonly TextBlock _historicalFreshnessKnownSkippedText;
        private readonly TextBlock _historicalFreshnessImportedText;
        private readonly TextBlock _historicalFreshnessFailedText;
        private readonly TextBlock _historicalFreshnessLastRunText;
        private readonly TextBlock _historicalFreshnessDetailText;
        private readonly TextBlock _historicalFreshnessLastErrorText;
        private readonly Button _runHistoricalFreshnessButton;

        public IntelStatusDetailsPresenter(
            TextBlock intelLastUpdatedText,
            TextBlock intelOldestKillmailDayText,
            TextBlock intelNewestKillmailDayText,
            TextBlock intelCurrentUpdateStatusText,
            ProgressBar intelTotalProgressBar,
            TextBlock intelTotalProgressText,
            ProgressBar intelCurrentDayProgressBar,
            TextBlock intelCurrentDayProgressText,
            TextBlock intelLiveFeedSourceText,
            TextBlock intelLiveFeedStatusText,
            TextBlock intelLiveFeedEnabledText,
            TextBlock intelLiveFeedRecentImportsText,
            TextBlock intelLiveFeedNextSequenceText,
            TextBlock intelLiveFeedLastProcessedSequenceText,
            TextBlock intelLiveFeedLastSuccessText,
            TextBlock intelLiveFeedLastCaughtUpText,
            TextBlock intelLiveFeedLastErrorText,
            TextBlock todaysFreshnessStatusText,
            TextBlock todaysFreshnessVisiblePilotsText,
            TextBlock todaysFreshnessEntitiesQueriedText,
            TextBlock todaysFreshnessResultsFoundText,
            TextBlock todaysFreshnessKnownSkippedText,
            TextBlock todaysFreshnessImportedText,
            TextBlock todaysFreshnessFailedText,
            TextBlock todaysFreshnessLastRunText,
            TextBlock todaysFreshnessDetailText,
            TextBlock todaysFreshnessLastErrorText,
            Button runTodaysFreshnessButton,
            TextBlock historicalFreshnessStatusText,
            TextBlock historicalFreshnessModeText,
            TextBlock historicalFreshnessVisiblePilotsText,
            TextBlock historicalFreshnessCandidatesConsideredText,
            TextBlock historicalFreshnessCandidatesSkippedCooldownText,
            TextBlock historicalFreshnessPilotsCheckedText,
            TextBlock historicalFreshnessDaysCheckedText,
            TextBlock historicalFreshnessEntitiesQueriedText,
            TextBlock historicalFreshnessResultsFoundText,
            TextBlock historicalFreshnessKnownSkippedText,
            TextBlock historicalFreshnessImportedText,
            TextBlock historicalFreshnessFailedText,
            TextBlock historicalFreshnessLastRunText,
            TextBlock historicalFreshnessDetailText,
            TextBlock historicalFreshnessLastErrorText,
            Button runHistoricalFreshnessButton)
        {
            _intelLastUpdatedText = intelLastUpdatedText ?? throw new ArgumentNullException(nameof(intelLastUpdatedText));
            _intelOldestKillmailDayText = intelOldestKillmailDayText ?? throw new ArgumentNullException(nameof(intelOldestKillmailDayText));
            _intelNewestKillmailDayText = intelNewestKillmailDayText ?? throw new ArgumentNullException(nameof(intelNewestKillmailDayText));
            _intelCurrentUpdateStatusText = intelCurrentUpdateStatusText ?? throw new ArgumentNullException(nameof(intelCurrentUpdateStatusText));
            _intelTotalProgressBar = intelTotalProgressBar ?? throw new ArgumentNullException(nameof(intelTotalProgressBar));
            _intelTotalProgressText = intelTotalProgressText ?? throw new ArgumentNullException(nameof(intelTotalProgressText));
            _intelCurrentDayProgressBar = intelCurrentDayProgressBar ?? throw new ArgumentNullException(nameof(intelCurrentDayProgressBar));
            _intelCurrentDayProgressText = intelCurrentDayProgressText ?? throw new ArgumentNullException(nameof(intelCurrentDayProgressText));
            _intelLiveFeedSourceText = intelLiveFeedSourceText ?? throw new ArgumentNullException(nameof(intelLiveFeedSourceText));
            _intelLiveFeedStatusText = intelLiveFeedStatusText ?? throw new ArgumentNullException(nameof(intelLiveFeedStatusText));
            _intelLiveFeedEnabledText = intelLiveFeedEnabledText ?? throw new ArgumentNullException(nameof(intelLiveFeedEnabledText));
            _intelLiveFeedRecentImportsText = intelLiveFeedRecentImportsText ?? throw new ArgumentNullException(nameof(intelLiveFeedRecentImportsText));
            _intelLiveFeedNextSequenceText = intelLiveFeedNextSequenceText ?? throw new ArgumentNullException(nameof(intelLiveFeedNextSequenceText));
            _intelLiveFeedLastProcessedSequenceText = intelLiveFeedLastProcessedSequenceText ?? throw new ArgumentNullException(nameof(intelLiveFeedLastProcessedSequenceText));
            _intelLiveFeedLastSuccessText = intelLiveFeedLastSuccessText ?? throw new ArgumentNullException(nameof(intelLiveFeedLastSuccessText));
            _intelLiveFeedLastCaughtUpText = intelLiveFeedLastCaughtUpText ?? throw new ArgumentNullException(nameof(intelLiveFeedLastCaughtUpText));
            _intelLiveFeedLastErrorText = intelLiveFeedLastErrorText ?? throw new ArgumentNullException(nameof(intelLiveFeedLastErrorText));
            _todaysFreshnessStatusText = todaysFreshnessStatusText ?? throw new ArgumentNullException(nameof(todaysFreshnessStatusText));
            _todaysFreshnessVisiblePilotsText = todaysFreshnessVisiblePilotsText ?? throw new ArgumentNullException(nameof(todaysFreshnessVisiblePilotsText));
            _todaysFreshnessEntitiesQueriedText = todaysFreshnessEntitiesQueriedText ?? throw new ArgumentNullException(nameof(todaysFreshnessEntitiesQueriedText));
            _todaysFreshnessResultsFoundText = todaysFreshnessResultsFoundText ?? throw new ArgumentNullException(nameof(todaysFreshnessResultsFoundText));
            _todaysFreshnessKnownSkippedText = todaysFreshnessKnownSkippedText ?? throw new ArgumentNullException(nameof(todaysFreshnessKnownSkippedText));
            _todaysFreshnessImportedText = todaysFreshnessImportedText ?? throw new ArgumentNullException(nameof(todaysFreshnessImportedText));
            _todaysFreshnessFailedText = todaysFreshnessFailedText ?? throw new ArgumentNullException(nameof(todaysFreshnessFailedText));
            _todaysFreshnessLastRunText = todaysFreshnessLastRunText ?? throw new ArgumentNullException(nameof(todaysFreshnessLastRunText));
            _todaysFreshnessDetailText = todaysFreshnessDetailText ?? throw new ArgumentNullException(nameof(todaysFreshnessDetailText));
            _todaysFreshnessLastErrorText = todaysFreshnessLastErrorText ?? throw new ArgumentNullException(nameof(todaysFreshnessLastErrorText));
            _runTodaysFreshnessButton = runTodaysFreshnessButton ?? throw new ArgumentNullException(nameof(runTodaysFreshnessButton));
            _historicalFreshnessStatusText = historicalFreshnessStatusText ?? throw new ArgumentNullException(nameof(historicalFreshnessStatusText));
            _historicalFreshnessModeText = historicalFreshnessModeText ?? throw new ArgumentNullException(nameof(historicalFreshnessModeText));
            _historicalFreshnessVisiblePilotsText = historicalFreshnessVisiblePilotsText ?? throw new ArgumentNullException(nameof(historicalFreshnessVisiblePilotsText));
            _historicalFreshnessCandidatesConsideredText = historicalFreshnessCandidatesConsideredText ?? throw new ArgumentNullException(nameof(historicalFreshnessCandidatesConsideredText));
            _historicalFreshnessCandidatesSkippedCooldownText = historicalFreshnessCandidatesSkippedCooldownText ?? throw new ArgumentNullException(nameof(historicalFreshnessCandidatesSkippedCooldownText));
            _historicalFreshnessPilotsCheckedText = historicalFreshnessPilotsCheckedText ?? throw new ArgumentNullException(nameof(historicalFreshnessPilotsCheckedText));
            _historicalFreshnessDaysCheckedText = historicalFreshnessDaysCheckedText ?? throw new ArgumentNullException(nameof(historicalFreshnessDaysCheckedText));
            _historicalFreshnessEntitiesQueriedText = historicalFreshnessEntitiesQueriedText ?? throw new ArgumentNullException(nameof(historicalFreshnessEntitiesQueriedText));
            _historicalFreshnessResultsFoundText = historicalFreshnessResultsFoundText ?? throw new ArgumentNullException(nameof(historicalFreshnessResultsFoundText));
            _historicalFreshnessKnownSkippedText = historicalFreshnessKnownSkippedText ?? throw new ArgumentNullException(nameof(historicalFreshnessKnownSkippedText));
            _historicalFreshnessImportedText = historicalFreshnessImportedText ?? throw new ArgumentNullException(nameof(historicalFreshnessImportedText));
            _historicalFreshnessFailedText = historicalFreshnessFailedText ?? throw new ArgumentNullException(nameof(historicalFreshnessFailedText));
            _historicalFreshnessLastRunText = historicalFreshnessLastRunText ?? throw new ArgumentNullException(nameof(historicalFreshnessLastRunText));
            _historicalFreshnessDetailText = historicalFreshnessDetailText ?? throw new ArgumentNullException(nameof(historicalFreshnessDetailText));
            _historicalFreshnessLastErrorText = historicalFreshnessLastErrorText ?? throw new ArgumentNullException(nameof(historicalFreshnessLastErrorText));
            _runHistoricalFreshnessButton = runHistoricalFreshnessButton ?? throw new ArgumentNullException(nameof(runHistoricalFreshnessButton));
        }

        public void Apply(IntelStatusDetailsProjection projection)
        {
            if (projection == null)
            {
                throw new ArgumentNullException(nameof(projection));
            }

            _intelLastUpdatedText.Text = projection.LastUpdatedText;
            _intelOldestKillmailDayText.Text = projection.OldestKillmailDayText;
            _intelNewestKillmailDayText.Text = projection.NewestKillmailDayText;
            _intelCurrentUpdateStatusText.Text = projection.CurrentUpdateStatusText;
            _intelTotalProgressBar.IsIndeterminate = projection.TotalProgressIsIndeterminate;
            _intelTotalProgressBar.Value = projection.TotalProgressValue;
            _intelTotalProgressText.Text = projection.TotalProgressText;
            _intelCurrentDayProgressBar.IsIndeterminate = projection.CurrentDayProgressIsIndeterminate;
            _intelCurrentDayProgressBar.Value = projection.CurrentDayProgressValue;
            _intelCurrentDayProgressText.Text = projection.CurrentDayProgressText;
            _intelLiveFeedSourceText.Text = projection.LiveFeedSourceText;
            _intelLiveFeedStatusText.Text = projection.LiveFeedStatusText;
            _intelLiveFeedEnabledText.Text = projection.LiveFeedEnabledText;
            _intelLiveFeedRecentImportsText.Text = projection.LiveFeedRecentImportsText;
            _intelLiveFeedNextSequenceText.Text = projection.LiveFeedNextSequenceText;
            _intelLiveFeedLastProcessedSequenceText.Text = projection.LiveFeedLastProcessedSequenceText;
            _intelLiveFeedLastSuccessText.Text = projection.LiveFeedLastSuccessText;
            _intelLiveFeedLastCaughtUpText.Text = projection.LiveFeedLastCaughtUpText;
            _intelLiveFeedLastErrorText.Text = projection.LiveFeedLastErrorText;
            _todaysFreshnessStatusText.Text = projection.TodaysFreshnessStatusText;
            _todaysFreshnessVisiblePilotsText.Text = projection.TodaysFreshnessVisiblePilotsText;
            _todaysFreshnessEntitiesQueriedText.Text = projection.TodaysFreshnessEntitiesQueriedText;
            _todaysFreshnessResultsFoundText.Text = projection.TodaysFreshnessResultsFoundText;
            _todaysFreshnessKnownSkippedText.Text = projection.TodaysFreshnessKnownSkippedText;
            _todaysFreshnessImportedText.Text = projection.TodaysFreshnessImportedText;
            _todaysFreshnessFailedText.Text = projection.TodaysFreshnessFailedText;
            _todaysFreshnessLastRunText.Text = projection.TodaysFreshnessLastRunText;
            _todaysFreshnessDetailText.Text = projection.TodaysFreshnessDetailText;
            _todaysFreshnessLastErrorText.Text = projection.TodaysFreshnessLastErrorText;
            _runTodaysFreshnessButton.IsEnabled = projection.RunTodaysFreshnessButtonIsEnabled;
            _runTodaysFreshnessButton.Content = projection.RunTodaysFreshnessButtonLabel;
            _historicalFreshnessStatusText.Text = projection.HistoricalFreshnessStatusText;
            _historicalFreshnessModeText.Text = projection.HistoricalFreshnessModeText;
            _historicalFreshnessVisiblePilotsText.Text = projection.HistoricalFreshnessVisiblePilotsText;
            _historicalFreshnessCandidatesConsideredText.Text = projection.HistoricalFreshnessCandidatesConsideredText;
            _historicalFreshnessCandidatesSkippedCooldownText.Text = projection.HistoricalFreshnessCandidatesSkippedCooldownText;
            _historicalFreshnessPilotsCheckedText.Text = projection.HistoricalFreshnessPilotsCheckedText;
            _historicalFreshnessDaysCheckedText.Text = projection.HistoricalFreshnessDaysCheckedText;
            _historicalFreshnessEntitiesQueriedText.Text = projection.HistoricalFreshnessEntitiesQueriedText;
            _historicalFreshnessResultsFoundText.Text = projection.HistoricalFreshnessResultsFoundText;
            _historicalFreshnessKnownSkippedText.Text = projection.HistoricalFreshnessKnownSkippedText;
            _historicalFreshnessImportedText.Text = projection.HistoricalFreshnessImportedText;
            _historicalFreshnessFailedText.Text = projection.HistoricalFreshnessFailedText;
            _historicalFreshnessLastRunText.Text = projection.HistoricalFreshnessLastRunText;
            _historicalFreshnessDetailText.Text = projection.HistoricalFreshnessDetailText;
            _historicalFreshnessLastErrorText.Text = projection.HistoricalFreshnessLastErrorText;
            _runHistoricalFreshnessButton.IsEnabled = projection.RunHistoricalFreshnessButtonIsEnabled;
            _runHistoricalFreshnessButton.Content = projection.RunHistoricalFreshnessButtonLabel;
        }
    }
}
