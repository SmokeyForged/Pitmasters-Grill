using PitmastersGrill.Persistence;
using PitmastersGrill.Services;
using System.Windows;
using System.Windows.Navigation;

namespace PitmastersGrill
{
    public partial class MainWindow
    {
        private ExternalNavigationCoordinator? _externalNavigationCoordinator;

        private ExternalNavigationCoordinator ExternalNavigation =>
            _externalNavigationCoordinator ??= new ExternalNavigationCoordinator(
                _zkillUrlBuilder,
                _browserLauncher.TryOpenUrl,
                AppLogger.UiInfo,
                AppLogger.UiWarn,
                AppLogger.UiError);

        private void ShowExternalNavigationErrorIfNeeded(BrowserLaunchResult result)
        {
            if (result.Exception == null)
            {
                return;
            }

            MessageBox.Show(
                $"Failed to open browser.\n\n{result.Exception.Message}",
                "PMG Browser Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private void VersionUpdateView_RepositoryNavigateRequested(object sender, RequestNavigateEventArgs e) =>
            GitHubRepoLink_RequestNavigate(sender, e);

        private void VersionUpdateView_ManualUpdateCheckRequested(object sender, RoutedEventArgs e) =>
            ManualUpdateCheckButton_Click(sender, e);

        private void OpenManualUpdateReleasePage(string url)
        {
            _ = ExternalNavigation.OpenUrl(url, "manual update release page");
        }
    }
}
