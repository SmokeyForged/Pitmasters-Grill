using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace PitmastersGrill.Views
{
    public partial class VersionUpdateView : UserControl
    {
        public VersionUpdateView()
        {
            InitializeComponent();
        }

        public event RequestNavigateEventHandler? RepositoryNavigateRequested;
        public event RoutedEventHandler? ManualUpdateCheckRequested;

        public void SetManualUpdateCheckEnabled(bool enabled)
        {
            ManualUpdateCheckButton.IsEnabled = enabled;
        }

        public void SetManualUpdateStatusText(string text)
        {
            ManualUpdateStatusText.Text = text;
        }

        private void RepositoryLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            RepositoryNavigateRequested?.Invoke(sender, e);
            e.Handled = true;
        }

        private void ManualUpdateCheckButton_Click(object sender, RoutedEventArgs e) =>
            ManualUpdateCheckRequested?.Invoke(sender, e);
    }
}
