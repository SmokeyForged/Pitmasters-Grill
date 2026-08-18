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

        private void VersionUpdateView_RepositoryNavigateRequested(object sender, RequestNavigateEventArgs e)
        {
            var url = e.Uri?.AbsoluteUri ?? "https://github.com/SmokeyForged/Pitmasters-Grill";
            var result = ExternalNavigation.OpenUrl(url, "GitHub repository");
            ShowExternalNavigationErrorIfNeeded(result);
            e.Handled = true;
        }

        private async void VersionUpdateView_ManualUpdateCheckRequested(object sender, RoutedEventArgs e)
        {
            if (_manualUpdateCheckController == null)
            {
                return;
            }

            await _manualUpdateCheckController.RunAsync();
        }

        private void OpenManualUpdateReleasePage(string url)
        {
            _ = ExternalNavigation.OpenUrl(url, "manual update release page");
        }
    }
}
