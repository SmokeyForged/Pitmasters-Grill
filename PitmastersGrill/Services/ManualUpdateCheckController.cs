using PitmastersGrill.Diagnostics;
using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PitmastersGrill.Services
{
    public sealed class ManualUpdateCheckController
    {
        private readonly Window _owner;
        private readonly Button _manualUpdateCheckButton;
        private readonly TextBlock _manualUpdateStatusText;
        private readonly BrowserLauncher _browserLauncher;
        private readonly AppSettings _appSettings;
        private readonly CancellationToken _shutdownToken;
        private readonly Func<bool> _isShuttingDown;

        public ManualUpdateCheckController(
            Window owner,
            Button manualUpdateCheckButton,
            TextBlock manualUpdateStatusText,
            BrowserLauncher browserLauncher,
            AppSettings appSettings,
            CancellationToken shutdownToken,
            Func<bool> isShuttingDown)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _manualUpdateCheckButton = manualUpdateCheckButton ?? throw new ArgumentNullException(nameof(manualUpdateCheckButton));
            _manualUpdateStatusText = manualUpdateStatusText ?? throw new ArgumentNullException(nameof(manualUpdateStatusText));
            _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
            _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
            _shutdownToken = shutdownToken;
            _isShuttingDown = isShuttingDown ?? throw new ArgumentNullException(nameof(isShuttingDown));
        }

        public async Task RunAsync()
        {
            try
            {
                _manualUpdateCheckButton.IsEnabled = false;
                _manualUpdateStatusText.Text = "Checking GitHub for the latest stable PMG release...";

                var appSettingsService = new AppSettingsService();
                var settings = appSettingsService.Load();
                var currentVersion = AppReleaseMetadata.VersionText;

                var updateService = new PmgUpdateAwarenessService(new GitHubLatestReleaseChecker(), currentVersion);
                var result = await updateService.CheckAsync(
                    settings.SkippedUpdateVersion,
                    respectSkippedVersion: false,
                    _shutdownToken);

                if (!result.IsUpdateAvailable)
                {
                    _manualUpdateStatusText.Text =
                        $"PMG is current. Current version: {result.CurrentVersion}. Checked {DateTime.Now:g}.";

                    MessageBox.Show(
                        _owner,
                        $"PMG is current.\n\nCurrent version: {result.CurrentVersion}",
                        "PMG Update Check",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                _manualUpdateStatusText.Text =
                    $"PMG {result.LatestVersion} is available. Current version: {result.CurrentVersion}. Checked {DateTime.Now:g}.";

                var message =
                    $"PMG {result.LatestVersion} is available.\n\n" +
                    $"Current version: {result.CurrentVersion}\n" +
                    $"Latest version: {result.LatestVersion}\n\n" +
                    "Yes: open the GitHub release page for manual update.\n" +
                    "No: leave this reminder available.\n" +
                    "Cancel: skip this version.";

                var response = MessageBox.Show(
                    _owner,
                    message,
                    "PMG Update Available",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Information);

                if (response == MessageBoxResult.Yes)
                {
                    _browserLauncher.OpenUrl(result.ReleasePageUrl);
                }
                else if (response == MessageBoxResult.Cancel)
                {
                    settings.SkippedUpdateVersion = result.LatestVersion;
                    _appSettings.SkippedUpdateVersion = result.LatestVersion;
                    appSettingsService.Save(settings);

                    _manualUpdateStatusText.Text =
                        $"Skipped PMG {result.LatestVersion}. Manual checks will still show available releases.";
                }
            }
            catch (OperationCanceledException) when (_isShuttingDown() || _shutdownToken.IsCancellationRequested)
            {
                _manualUpdateStatusText.Text = "Update check cancelled.";
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Manual update check failed.", ex);
                _manualUpdateStatusText.Text = $"Update check failed: {ex.Message}";

                MessageBox.Show(
                    _owner,
                    $"PMG could not check for updates.\n\n{ex.Message}",
                    "PMG Update Check",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                _manualUpdateCheckButton.IsEnabled = true;
            }
        }
    }
}
