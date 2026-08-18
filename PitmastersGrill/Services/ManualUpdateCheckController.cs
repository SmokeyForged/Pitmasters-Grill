using PitmastersGrill.Diagnostics;
using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PitmastersGrill.Services
{
    public sealed class ManualUpdateCheckController
    {
        private readonly Action<bool> _setManualUpdateCheckEnabled;
        private readonly Action<string> _setManualUpdateStatusText;
        private readonly Action<string> _openReleasePage;
        private readonly AppSettings _appSettings;
        private readonly CancellationToken _shutdownToken;
        private readonly Func<bool> _isShuttingDown;
        private readonly Func<AppSettings> _loadSettings;
        private readonly Action<AppSettings> _saveSettings;
        private readonly Func<string?, CancellationToken, Task<PmgUpdateAwarenessResult>> _checkForUpdatesAsync;
        private readonly Func<string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult> _showMessage;

        public ManualUpdateCheckController(
            Window owner,
            Action<bool> setManualUpdateCheckEnabled,
            Action<string> setManualUpdateStatusText,
            Action<string> openReleasePage,
            AppSettings appSettings,
            CancellationToken shutdownToken,
            Func<bool> isShuttingDown)
            : this(
                setManualUpdateCheckEnabled,
                setManualUpdateStatusText,
                openReleasePage,
                appSettings,
                shutdownToken,
                isShuttingDown,
                LoadSettings,
                SaveSettings,
                CheckForUpdatesAsync,
                CreateMessagePresenter(owner))
        {
        }

        internal ManualUpdateCheckController(
            Action<bool> setManualUpdateCheckEnabled,
            Action<string> setManualUpdateStatusText,
            Action<string> openReleasePage,
            AppSettings appSettings,
            CancellationToken shutdownToken,
            Func<bool> isShuttingDown,
            Func<AppSettings> loadSettings,
            Action<AppSettings> saveSettings,
            Func<string?, CancellationToken, Task<PmgUpdateAwarenessResult>> checkForUpdatesAsync,
            Func<string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult> showMessage)
        {
            _setManualUpdateCheckEnabled = setManualUpdateCheckEnabled ?? throw new ArgumentNullException(nameof(setManualUpdateCheckEnabled));
            _setManualUpdateStatusText = setManualUpdateStatusText ?? throw new ArgumentNullException(nameof(setManualUpdateStatusText));
            _openReleasePage = openReleasePage ?? throw new ArgumentNullException(nameof(openReleasePage));
            _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
            _shutdownToken = shutdownToken;
            _isShuttingDown = isShuttingDown ?? throw new ArgumentNullException(nameof(isShuttingDown));
            _loadSettings = loadSettings ?? throw new ArgumentNullException(nameof(loadSettings));
            _saveSettings = saveSettings ?? throw new ArgumentNullException(nameof(saveSettings));
            _checkForUpdatesAsync = checkForUpdatesAsync ?? throw new ArgumentNullException(nameof(checkForUpdatesAsync));
            _showMessage = showMessage ?? throw new ArgumentNullException(nameof(showMessage));
        }

        public async Task RunAsync()
        {
            try
            {
                _setManualUpdateCheckEnabled(false);
                _setManualUpdateStatusText("Checking GitHub for the latest stable PMG release...");

                var settings = _loadSettings();
                var result = await _checkForUpdatesAsync(settings.SkippedUpdateVersion, _shutdownToken);

                if (!result.IsUpdateAvailable)
                {
                    _setManualUpdateStatusText(
                        $"PMG is current. Current version: {result.CurrentVersion}. Checked {DateTime.Now:g}.");

                    _showMessage(
                        $"PMG is current.\n\nCurrent version: {result.CurrentVersion}",
                        "PMG Update Check",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                _setManualUpdateStatusText(
                    $"PMG {result.LatestVersion} is available. Current version: {result.CurrentVersion}. Checked {DateTime.Now:g}.");

                var message =
                    $"PMG {result.LatestVersion} is available.\n\n" +
                    $"Current version: {result.CurrentVersion}\n" +
                    $"Latest version: {result.LatestVersion}\n\n" +
                    "Yes: open the GitHub release page for manual update.\n" +
                    "No: leave this reminder available.\n" +
                    "Cancel: skip this version.";

                var response = _showMessage(
                    message,
                    "PMG Update Available",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Information);

                if (response == MessageBoxResult.Yes)
                {
                    _openReleasePage(result.ReleasePageUrl);
                }
                else if (response == MessageBoxResult.Cancel)
                {
                    settings.SkippedUpdateVersion = result.LatestVersion;
                    _appSettings.SkippedUpdateVersion = result.LatestVersion;
                    _saveSettings(settings);

                    _setManualUpdateStatusText(
                        $"Skipped PMG {result.LatestVersion}. Manual checks will still show available releases.");
                }
            }
            catch (OperationCanceledException) when (_isShuttingDown() || _shutdownToken.IsCancellationRequested)
            {
                _setManualUpdateStatusText("Update check cancelled.");
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Manual update check failed.", ex);
                _setManualUpdateStatusText($"Update check failed: {ex.Message}");

                _showMessage(
                    $"PMG could not check for updates.\n\n{ex.Message}",
                    "PMG Update Check",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                _setManualUpdateCheckEnabled(true);
            }
        }

        private static AppSettings LoadSettings() => new AppSettingsService().Load();

        private static void SaveSettings(AppSettings settings) => new AppSettingsService().Save(settings);

        private static Task<PmgUpdateAwarenessResult> CheckForUpdatesAsync(
            string? skippedUpdateVersion,
            CancellationToken cancellationToken)
        {
            var updateService = new PmgUpdateAwarenessService(
                new GitHubLatestReleaseChecker(),
                AppReleaseMetadata.VersionText);
            return updateService.CheckAsync(
                skippedUpdateVersion,
                respectSkippedVersion: false,
                cancellationToken);
        }

        private static Func<string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult> CreateMessagePresenter(Window owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            return (message, title, buttons, image) => MessageBox.Show(owner, message, title, buttons, image);
        }
    }
}
