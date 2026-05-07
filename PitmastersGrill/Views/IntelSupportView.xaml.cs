using System.Windows;
using System.Windows.Controls;

namespace PitmastersGrill.Views
{
    public partial class IntelSupportView : UserControl
    {
        public IntelSupportView()
        {
            InitializeComponent();
        }

        public event RoutedEventHandler? SaveMaxKillmailAgeRequested;
        public event RoutedEventHandler? UseDefaultMaxKillmailAgeRequested;
        public event RoutedEventHandler? EnableKillmailDbPullRequested;
        public event RoutedEventHandler? EnableLiveZkillFeedToggled;
        public event RoutedEventHandler? BackgroundHistoricalRepairToggled;
        public event SelectionChangedEventHandler? PilotDetailPlacementSelectionChanged;
        public event RoutedEventHandler? SaveKillmailPathRequested;
        public event RoutedEventHandler? UseDefaultKillmailPathRequested;
        public event RoutedEventHandler? RunTodaysFreshnessRequested;
        public event RoutedEventHandler? RunHistoricalFreshnessRequested;

        public Border IntelUpdateBannerControl => IntelUpdateBanner;
        public TextBlock IntelUpdateStatusTextBlock => IntelUpdateStatusText;
        public TextBlock IntelUpdateDetailTextBlock => IntelUpdateDetailText;
        public TextBlock IntelLastUpdatedTextBlock => IntelLastUpdatedText;
        public TextBlock IntelOldestKillmailDayTextBlock => IntelOldestKillmailDayText;
        public TextBlock IntelNewestKillmailDayTextBlock => IntelNewestKillmailDayText;
        public TextBlock IntelCurrentUpdateStatusTextBlock => IntelCurrentUpdateStatusText;
        public ProgressBar IntelTotalProgressBarControl => IntelTotalProgressBar;
        public TextBlock IntelTotalProgressTextBlock => IntelTotalProgressText;
        public ProgressBar IntelCurrentDayProgressBarControl => IntelCurrentDayProgressBar;
        public TextBlock IntelCurrentDayProgressTextBlock => IntelCurrentDayProgressText;
        public TextBlock IntelLiveFeedSourceTextBlock => IntelLiveFeedSourceText;
        public TextBlock IntelLiveFeedStatusTextBlock => IntelLiveFeedStatusText;
        public TextBlock IntelLiveFeedEnabledTextBlock => IntelLiveFeedEnabledText;
        public TextBlock IntelLiveFeedRecentImportsTextBlock => IntelLiveFeedRecentImportsText;
        public TextBlock IntelLiveFeedNextSequenceTextBlock => IntelLiveFeedNextSequenceText;
        public TextBlock IntelLiveFeedLastProcessedSequenceTextBlock => IntelLiveFeedLastProcessedSequenceText;
        public TextBlock IntelLiveFeedLastSuccessTextBlock => IntelLiveFeedLastSuccessText;
        public TextBlock IntelLiveFeedLastCaughtUpTextBlock => IntelLiveFeedLastCaughtUpText;
        public TextBlock IntelLiveFeedLastErrorTextBlock => IntelLiveFeedLastErrorText;
        public TextBlock TodaysFreshnessStatusTextBlock => TodaysFreshnessStatusText;
        public TextBlock TodaysFreshnessVisiblePilotsTextBlock => TodaysFreshnessVisiblePilotsText;
        public TextBlock TodaysFreshnessEntitiesQueriedTextBlock => TodaysFreshnessEntitiesQueriedText;
        public TextBlock TodaysFreshnessResultsFoundTextBlock => TodaysFreshnessResultsFoundText;
        public TextBlock TodaysFreshnessKnownSkippedTextBlock => TodaysFreshnessKnownSkippedText;
        public TextBlock TodaysFreshnessImportedTextBlock => TodaysFreshnessImportedText;
        public TextBlock TodaysFreshnessFailedTextBlock => TodaysFreshnessFailedText;
        public TextBlock TodaysFreshnessLastRunTextBlock => TodaysFreshnessLastRunText;
        public TextBlock TodaysFreshnessDetailTextBlock => TodaysFreshnessDetailText;
        public TextBlock TodaysFreshnessLastErrorTextBlock => TodaysFreshnessLastErrorText;
        public Button RunTodaysFreshnessButtonControl => RunTodaysFreshnessButton;
        public TextBlock HistoricalFreshnessStatusTextBlock => HistoricalFreshnessStatusText;
        public TextBlock HistoricalFreshnessModeTextBlock => HistoricalFreshnessModeText;
        public TextBlock HistoricalFreshnessVisiblePilotsTextBlock => HistoricalFreshnessVisiblePilotsText;
        public TextBlock HistoricalFreshnessCandidatesConsideredTextBlock => HistoricalFreshnessCandidatesConsideredText;
        public TextBlock HistoricalFreshnessCandidatesSkippedCooldownTextBlock => HistoricalFreshnessCandidatesSkippedCooldownText;
        public TextBlock HistoricalFreshnessPilotsCheckedTextBlock => HistoricalFreshnessPilotsCheckedText;
        public TextBlock HistoricalFreshnessDaysCheckedTextBlock => HistoricalFreshnessDaysCheckedText;
        public TextBlock HistoricalFreshnessEntitiesQueriedTextBlock => HistoricalFreshnessEntitiesQueriedText;
        public TextBlock HistoricalFreshnessResultsFoundTextBlock => HistoricalFreshnessResultsFoundText;
        public TextBlock HistoricalFreshnessKnownSkippedTextBlock => HistoricalFreshnessKnownSkippedText;
        public TextBlock HistoricalFreshnessImportedTextBlock => HistoricalFreshnessImportedText;
        public TextBlock HistoricalFreshnessFailedTextBlock => HistoricalFreshnessFailedText;
        public TextBlock HistoricalFreshnessLastRunTextBlock => HistoricalFreshnessLastRunText;
        public TextBlock HistoricalFreshnessDetailTextBlock => HistoricalFreshnessDetailText;
        public TextBlock HistoricalFreshnessLastErrorTextBlock => HistoricalFreshnessLastErrorText;
        public Button RunHistoricalFreshnessButtonControl => RunHistoricalFreshnessButton;
        public TextBlock EffectiveMaxKillmailAgeTextBlock => EffectiveMaxKillmailAgeText;
        public TextBox MaxKillmailAgeDaysTextBoxControl => MaxKillmailAgeDaysTextBox;
        public Button EnableKillmailDbPullButtonControl => EnableKillmailDbPullButton;
        public CheckBox EnableLiveZkillFeedCheckBoxControl => EnableLiveZkillFeedCheckBox;
        public CheckBox BackgroundHistoricalRepairEnabledCheckBoxControl => BackgroundHistoricalRepairEnabledCheckBox;
        public ComboBox PilotDetailPlacementComboBoxControl => PilotDetailPlacementComboBox;
        public TextBlock KillmailDataPathModeTextBlock => KillmailDataPathModeText;
        public TextBlock EffectiveKillmailDataPathTextBlock => EffectiveKillmailDataPathText;
        public TextBox KillmailDataRootPathTextBoxControl => KillmailDataRootPathTextBox;

        private void SaveMaxKillmailAgeButton_Click(object sender, RoutedEventArgs e) => SaveMaxKillmailAgeRequested?.Invoke(sender, e);
        private void UseDefaultMaxKillmailAgeButton_Click(object sender, RoutedEventArgs e) => UseDefaultMaxKillmailAgeRequested?.Invoke(sender, e);
        private void EnableKillmailDbPullButton_Click(object sender, RoutedEventArgs e) => EnableKillmailDbPullRequested?.Invoke(sender, e);
        private void EnableLiveZkillFeedCheckBox_Checked(object sender, RoutedEventArgs e) => EnableLiveZkillFeedToggled?.Invoke(sender, e);
        private void BackgroundHistoricalRepairEnabledCheckBox_Checked(object sender, RoutedEventArgs e) => BackgroundHistoricalRepairToggled?.Invoke(sender, e);
        private void PilotDetailPlacementComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => PilotDetailPlacementSelectionChanged?.Invoke(sender, e);
        private void SaveKillmailPathButton_Click(object sender, RoutedEventArgs e) => SaveKillmailPathRequested?.Invoke(sender, e);
        private void UseDefaultKillmailPathButton_Click(object sender, RoutedEventArgs e) => UseDefaultKillmailPathRequested?.Invoke(sender, e);
        private void RunTodaysFreshnessButton_Click(object sender, RoutedEventArgs e) => RunTodaysFreshnessRequested?.Invoke(sender, e);
        private void RunHistoricalFreshnessButton_Click(object sender, RoutedEventArgs e) => RunHistoricalFreshnessRequested?.Invoke(sender, e);
    }
}
