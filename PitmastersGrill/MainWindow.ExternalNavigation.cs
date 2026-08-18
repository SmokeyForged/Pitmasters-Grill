using PitmastersGrill.Persistence;
using PitmastersGrill.Services;
using System.Windows;

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
    }
}
