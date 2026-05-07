using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace PitmastersGrill.Views
{
    public partial class DiagnosticsSupportView : UserControl
    {
        public DiagnosticsSupportView()
        {
            InitializeComponent();
        }

        public event RoutedEventHandler? OpenLogsRequested;
        public event RoutedEventHandler? PackageDiagnosticsRequested;
        public event RoutedEventHandler? OpenDiagnosticsFolderRequested;
        public event SelectionChangedEventHandler? LogLevelSelectionChanged;
        public event RoutedEventHandler? RefreshProviderHealthRequested;
        public event RoutedEventHandler? RefreshCacheStatsRequested;
        public event RoutedEventHandler? ClearExpiredCacheRequested;
        public event RoutedEventHandler? VacuumCacheRequested;
        public event RoutedEventHandler? ClearAllCacheRequested;
        public event RoutedEventHandler? RebuildKillmailDerivedIntelRequested;

        public TextBlock DiagnosticsStatusTextBlock => DiagnosticsStatusText;
        public ComboBox LogLevelComboBoxControl => LogLevelComboBox;

        public void SetProviderHealthItemsSource(IEnumerable itemsSource)
        {
            ProviderHealthGrid.ItemsSource = itemsSource;
        }

        public void SetCacheStatsText(string text)
        {
            CacheStatsText.Text = text;
        }

        public void SetRebuildKillmailDerivedIntelEnabled(bool enabled)
        {
            RebuildKillmailDerivedIntelButton.IsEnabled = enabled;
        }

        private void OpenLogsButton_Click(object sender, RoutedEventArgs e) => OpenLogsRequested?.Invoke(sender, e);
        private void PackageDiagnosticsButton_Click(object sender, RoutedEventArgs e) => PackageDiagnosticsRequested?.Invoke(sender, e);
        private void OpenDiagnosticsFolderButton_Click(object sender, RoutedEventArgs e) => OpenDiagnosticsFolderRequested?.Invoke(sender, e);
        private void LogLevelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => LogLevelSelectionChanged?.Invoke(sender, e);
        private void RefreshProviderHealthButton_Click(object sender, RoutedEventArgs e) => RefreshProviderHealthRequested?.Invoke(sender, e);
        private void RefreshCacheStatsButton_Click(object sender, RoutedEventArgs e) => RefreshCacheStatsRequested?.Invoke(sender, e);
        private void ClearExpiredCacheButton_Click(object sender, RoutedEventArgs e) => ClearExpiredCacheRequested?.Invoke(sender, e);
        private void VacuumCacheButton_Click(object sender, RoutedEventArgs e) => VacuumCacheRequested?.Invoke(sender, e);
        private void ClearAllCacheButton_Click(object sender, RoutedEventArgs e) => ClearAllCacheRequested?.Invoke(sender, e);
        private void RebuildKillmailDerivedIntelButton_Click(object sender, RoutedEventArgs e) => RebuildKillmailDerivedIntelRequested?.Invoke(sender, e);
    }
}
