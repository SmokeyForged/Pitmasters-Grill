using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PitmastersGrill.Services
{
    public sealed class IntelStorageSettingsController
    {
        private readonly Action<AppSettings> _saveSettings;
        private readonly Action<string> _createDirectory;
        private readonly Action<string, string, MessageBoxImage> _showMessage;
        private readonly Func<string> _getEffectiveKillmailDataPath;
        private readonly Func<string> _getKillmailDataPathSourceDescription;

        public IntelStorageSettingsController(AppSettingsService appSettingsService)
            : this(
                CreateSaveAction(appSettingsService),
                path => Directory.CreateDirectory(path),
                (message, title, image) => MessageBox.Show(
                    message,
                    title,
                    MessageBoxButton.OK,
                    image),
                KillmailPaths.GetKillmailDataDirectoryDisplayPath,
                KillmailPaths.GetKillmailDataDirectorySourceDescription)
        {
        }

        internal IntelStorageSettingsController(
            Action<AppSettings> saveSettings,
            Action<string> createDirectory,
            Action<string, string, MessageBoxImage> showMessage,
            Func<string>? getEffectiveKillmailDataPath = null,
            Func<string>? getKillmailDataPathSourceDescription = null)
        {
            _saveSettings = saveSettings ?? throw new ArgumentNullException(nameof(saveSettings));
            _createDirectory = createDirectory ?? throw new ArgumentNullException(nameof(createDirectory));
            _showMessage = showMessage ?? throw new ArgumentNullException(nameof(showMessage));
            _getEffectiveKillmailDataPath = getEffectiveKillmailDataPath ?? KillmailPaths.GetKillmailDataDirectoryDisplayPath;
            _getKillmailDataPathSourceDescription = getKillmailDataPathSourceDescription ?? KillmailPaths.GetKillmailDataDirectorySourceDescription;
        }

        public void InitializeSettingsUi(
            AppSettings settings,
            TextBox maxKillmailAgeDaysTextBox,
            TextBlock effectiveMaxKillmailAgeText,
            TextBox killmailDataRootPathTextBox,
            TextBlock killmailDataPathModeText,
            TextBlock effectiveKillmailDataPathText)
        {
            ArgumentNullException.ThrowIfNull(settings);

            if (maxKillmailAgeDaysTextBox != null)
            {
                maxKillmailAgeDaysTextBox.Text = GetMaxKillmailAgeTextBoxText(settings);
            }

            if (effectiveMaxKillmailAgeText != null)
            {
                UpdateMaxKillmailAgeUi(effectiveMaxKillmailAgeText, settings);
            }

            if (killmailDataRootPathTextBox != null)
            {
                killmailDataRootPathTextBox.Text = GetKillmailPathEditorText(settings);
            }

            if (killmailDataPathModeText != null && effectiveKillmailDataPathText != null)
            {
                UpdateKillmailPathUi(killmailDataPathModeText, effectiveKillmailDataPathText);
            }
        }

        public void SaveMaxKillmailAge(
            AppSettings settings,
            TextBox maxKillmailAgeDaysTextBox,
            TextBlock effectiveMaxKillmailAgeText)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(maxKillmailAgeDaysTextBox);
            ArgumentNullException.ThrowIfNull(effectiveMaxKillmailAgeText);

            try
            {
                var rawValue = maxKillmailAgeDaysTextBox.Text?.Trim() ?? string.Empty;

                if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDays))
                {
                    _showMessage(
                        $"Enter a whole number between {KillmailDatasetFreshnessService.MinimumMaxKillmailAgeDays} and {KillmailDatasetFreshnessService.MaximumMaxKillmailAgeDays}.",
                        "PMG Max Killmail Age",
                        MessageBoxImage.Information);

                    maxKillmailAgeDaysTextBox.Text = GetMaxKillmailAgeTextBoxText(settings);
                    return;
                }

                var normalizedDays = KillmailDatasetFreshnessService.NormalizeMaxKillmailAgeDays(parsedDays);
                settings.MaxKillmailAgeDays = normalizedDays;
                _saveSettings(settings);
                maxKillmailAgeDaysTextBox.Text = GetMaxKillmailAgeTextBoxText(settings);
                UpdateMaxKillmailAgeUi(effectiveMaxKillmailAgeText, settings);

                AppLogger.UiInfo($"Max killmail age saved. days={normalizedDays}");

                _showMessage(
                    $"Max killmail age saved as {normalizedDays} day{(normalizedDays == 1 ? "" : "s")}. The new value will apply the next time you use Enable KillMail DB Pull.",
                    "PMG Max Killmail Age",
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Failed to save max killmail age.", ex);
                _showMessage(
                    $"Failed to save max killmail age.\n\n{ex.Message}",
                    "PMG Max Killmail Age Error",
                    MessageBoxImage.Error);
            }
        }

        public void ResetMaxKillmailAgeToDefault(
            AppSettings settings,
            TextBox maxKillmailAgeDaysTextBox,
            TextBlock effectiveMaxKillmailAgeText)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(maxKillmailAgeDaysTextBox);
            ArgumentNullException.ThrowIfNull(effectiveMaxKillmailAgeText);

            try
            {
                settings.MaxKillmailAgeDays = KillmailDatasetFreshnessService.DefaultMaxKillmailAgeDays;
                _saveSettings(settings);
                maxKillmailAgeDaysTextBox.Text = GetMaxKillmailAgeTextBoxText(settings);
                UpdateMaxKillmailAgeUi(effectiveMaxKillmailAgeText, settings);

                AppLogger.UiInfo($"Max killmail age reset to default. days={settings.MaxKillmailAgeDays}");

                _showMessage(
                    $"Max killmail age reset to the default of {settings.MaxKillmailAgeDays} days.",
                    "PMG Max Killmail Age",
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Failed to reset max killmail age to default.", ex);
                _showMessage(
                    $"Failed to reset max killmail age.\n\n{ex.Message}",
                    "PMG Max Killmail Age Error",
                    MessageBoxImage.Error);
            }
        }

        public void SaveKillmailPath(
            AppSettings settings,
            TextBox killmailDataRootPathTextBox,
            TextBlock killmailDataPathModeText,
            TextBlock effectiveKillmailDataPathText)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(killmailDataRootPathTextBox);
            ArgumentNullException.ThrowIfNull(killmailDataPathModeText);
            ArgumentNullException.ThrowIfNull(effectiveKillmailDataPathText);

            try
            {
                var rawValue = killmailDataRootPathTextBox.Text?.Trim() ?? string.Empty;
                var normalizedDefaultPath = KillmailPaths.NormalizeForComparison(
                    KillmailPaths.GetDefaultKillmailDataDirectoryDisplayPath());

                if (string.IsNullOrWhiteSpace(rawValue))
                {
                    settings.KillmailDataRootPath = string.Empty;
                    _saveSettings(settings);
                    RefreshKillmailPathUi(
                        killmailDataRootPathTextBox,
                        killmailDataPathModeText,
                        effectiveKillmailDataPathText,
                        settings);

                    AppLogger.UiInfo("Killmail data path override cleared via blank save. Restart required.");
                    _showMessage(
                        "Killmail data path reset to the default %LOCALAPPDATA% location. Restart PMG to apply the new path fully.",
                        "PMG Killmail Data Path",
                        MessageBoxImage.Information);
                    return;
                }

                var normalizedPath = KillmailPaths.NormalizeForComparison(rawValue);
                if (string.Equals(normalizedPath, normalizedDefaultPath, StringComparison.OrdinalIgnoreCase))
                {
                    settings.KillmailDataRootPath = string.Empty;
                }
                else
                {
                    var expandedPath = KillmailPaths.ExpandPathTokens(rawValue);
                    _createDirectory(expandedPath);
                    settings.KillmailDataRootPath = rawValue;
                }

                _saveSettings(settings);
                RefreshKillmailPathUi(
                    killmailDataRootPathTextBox,
                    killmailDataPathModeText,
                    effectiveKillmailDataPathText,
                    settings);

                AppLogger.UiInfo(
                    $"Killmail data path saved. configuredValue='{settings.KillmailDataRootPath ?? string.Empty}' displayPath='{_getEffectiveKillmailDataPath()}' source={_getKillmailDataPathSourceDescription()} restartRequired=true");

                _showMessage(
                    "Killmail data path saved. Restart PMG to apply the new path fully. Existing killmail data is not migrated automatically.",
                    "PMG Killmail Data Path",
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Failed to save killmail data path.", ex);
                _showMessage(
                    $"Failed to save killmail data path.\n\n{ex.Message}",
                    "PMG Killmail Data Path Error",
                    MessageBoxImage.Error);
            }
        }

        public void ResetKillmailPathToDefault(
            AppSettings settings,
            TextBox killmailDataRootPathTextBox,
            TextBlock killmailDataPathModeText,
            TextBlock effectiveKillmailDataPathText)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(killmailDataRootPathTextBox);
            ArgumentNullException.ThrowIfNull(killmailDataPathModeText);
            ArgumentNullException.ThrowIfNull(effectiveKillmailDataPathText);

            try
            {
                settings.KillmailDataRootPath = string.Empty;
                _saveSettings(settings);
                RefreshKillmailPathUi(
                    killmailDataRootPathTextBox,
                    killmailDataPathModeText,
                    effectiveKillmailDataPathText,
                    settings);

                AppLogger.UiInfo("Killmail data path reset to default %LOCALAPPDATA% location. Restart required.");
                _showMessage(
                    "Killmail data path reset to the default %LOCALAPPDATA% location. Restart PMG to apply the new path fully.",
                    "PMG Killmail Data Path",
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Failed to reset killmail data path to default.", ex);
                _showMessage(
                    $"Failed to reset killmail data path.\n\n{ex.Message}",
                    "PMG Killmail Data Path Error",
                    MessageBoxImage.Error);
            }
        }

        public int GetMaxKillmailAgeDaysSettingValue(AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return KillmailDatasetFreshnessService.NormalizeMaxKillmailAgeDays(settings.MaxKillmailAgeDays);
        }

        public string GetMaxKillmailAgeTextBoxText(AppSettings settings)
        {
            return GetMaxKillmailAgeDaysSettingValue(settings).ToString(CultureInfo.InvariantCulture);
        }

        public void UpdateMaxKillmailAgeUi(TextBlock effectiveMaxKillmailAgeText, AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull(effectiveMaxKillmailAgeText);
            var days = GetMaxKillmailAgeDaysSettingValue(settings);
            effectiveMaxKillmailAgeText.Text = $"Effective max killmail age: {days} {(days == 1 ? "day" : "days")}";
        }

        public void UpdateKillmailPathUi(
            TextBlock killmailDataPathModeText,
            TextBlock effectiveKillmailDataPathText)
        {
            ArgumentNullException.ThrowIfNull(killmailDataPathModeText);
            ArgumentNullException.ThrowIfNull(effectiveKillmailDataPathText);

            killmailDataPathModeText.Text = $"Source: {_getKillmailDataPathSourceDescription()}";
            effectiveKillmailDataPathText.Text = $"Effective path: {_getEffectiveKillmailDataPath()}";
        }

        public string GetKillmailPathEditorText(AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return string.IsNullOrWhiteSpace(settings.KillmailDataRootPath)
                ? KillmailPaths.GetDefaultKillmailDataDirectoryDisplayPath()
                : settings.KillmailDataRootPath;
        }

        private void RefreshKillmailPathUi(
            TextBox killmailDataRootPathTextBox,
            TextBlock killmailDataPathModeText,
            TextBlock effectiveKillmailDataPathText,
            AppSettings settings)
        {
            killmailDataRootPathTextBox.Text = GetKillmailPathEditorText(settings);
            UpdateKillmailPathUi(killmailDataPathModeText, effectiveKillmailDataPathText);
        }

        private static Action<AppSettings> CreateSaveAction(AppSettingsService appSettingsService)
        {
            ArgumentNullException.ThrowIfNull(appSettingsService);
            return appSettingsService.Save;
        }
    }
}
