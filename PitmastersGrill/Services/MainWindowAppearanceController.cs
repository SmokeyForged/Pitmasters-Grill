using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace PitmastersGrill.Services
{
    public class MainWindowAppearanceController
    {
        private const int DwMWaUseImmersiveDarkMode = 20;

        private readonly AppSettingsService _appSettingsService;

        public MainWindowAppearanceController(AppSettingsService appSettingsService)
        {
            _appSettingsService = appSettingsService ?? throw new ArgumentNullException(nameof(appSettingsService));
        }

        public void SaveSettings(AppSettings settings)
        {
            _appSettingsService.Save(settings);
        }

        public void InitializeSettingsUi(
            AppSettings settings,
            CheckBox darkModeCheckBox,
            CheckBox alwaysOnTopCheckBox,
            Slider windowOpacitySlider,
            TextBlock windowOpacityValueText,
            TextBox maxKillmailAgeDaysTextBox,
            TextBlock effectiveMaxKillmailAgeText,
            TextBox killmailDataRootPathTextBox,
            TextBlock killmailDataPathModeText,
            TextBlock effectiveKillmailDataPathText,
            ComboBox logLevelComboBox)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (darkModeCheckBox != null)
            {
                darkModeCheckBox.IsChecked = settings.DarkModeEnabled;
            }

            if (alwaysOnTopCheckBox != null)
            {
                alwaysOnTopCheckBox.IsChecked = settings.AlwaysOnTopEnabled;
            }

            var opacityPercent = CoerceOpacityPercent(settings.WindowOpacityPercent);

            if (windowOpacitySlider != null)
            {
                windowOpacitySlider.Value = opacityPercent;
            }

            if (windowOpacityValueText != null)
            {
                windowOpacityValueText.Text = $"{opacityPercent:0}%";
            }

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

            ApplyLogLevelSelection(logLevelComboBox, settings.LogLevel);
        }

        public void HandleDarkModeChanged(
            AppSettings settings,
            bool darkModeEnabled,
            ResourceDictionary resources,
            Window window,
            Action applyBoardPopulationStatusVisual)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.DarkModeEnabled = darkModeEnabled;
            _appSettingsService.Save(settings);
            ApplyTheme(resources, settings, window, applyBoardPopulationStatusVisual);

            AppLogger.UiInfo($"Dark mode changed. enabled={settings.DarkModeEnabled}");
        }

        public void HandleAlwaysOnTopChanged(
            AppSettings settings,
            bool alwaysOnTopEnabled,
            Window window,
            TextBlock windowOpacityValueText,
            ResourceDictionary resources)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.AlwaysOnTopEnabled = alwaysOnTopEnabled;
            _appSettingsService.Save(settings);
            ApplyWindowSettings(window, settings, windowOpacityValueText, resources);

            AppLogger.UiInfo($"Always on top changed. enabled={settings.AlwaysOnTopEnabled}");
        }

        public double HandleWindowOpacityChanged(
            AppSettings settings,
            double sliderValue,
            Window window,
            TextBlock windowOpacityValueText,
            ResourceDictionary resources)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var opacityPercent = CoerceOpacityPercent(sliderValue);

            if (windowOpacityValueText != null)
            {
                windowOpacityValueText.Text = $"{opacityPercent:0}%";
            }

            settings.WindowOpacityPercent = opacityPercent;
            _appSettingsService.Save(settings);
            ApplyWindowSettings(window, settings, windowOpacityValueText, resources);

            AppLogger.UiInfo($"Window opacity changed. opacityPercent={opacityPercent:0}");

            return opacityPercent;
        }

        public void HandleLogLevelChanged(AppSettings settings, ComboBox logLevelComboBox)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var selectedLogLevel = GetSelectedLogLevel(logLevelComboBox);

            if (settings.LogLevel == selectedLogLevel)
            {
                return;
            }

            settings.LogLevel = selectedLogLevel;
            _appSettingsService.Save(settings);
            AppLogger.ConfigureLogLevel(selectedLogLevel);

            AppLogger.AppInfo($"Log level changed. level={selectedLogLevel}");
        }

        public void ApplyTheme(
            ResourceDictionary resources,
            AppSettings settings,
            Window window,
            Action applyBoardPopulationStatusVisual)
        {
            if (resources == null)
            {
                throw new ArgumentNullException(nameof(resources));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (settings.DarkModeEnabled)
            {
                SetBrushResource(resources, "HeaderTextBrush", "#FFFFFF");
                SetBrushResource(resources, "BodyTextBrush", "#F3F3F3");
                SetBrushResource(resources, "MutedTextBrush", "#DDDDDD");
                SetBrushResource(resources, "GridLineBrush", "#4A4A4A");
                SetBrushResource(resources, "PanelBorderBrush", "#3A3A3A");
            }
            else
            {
                SetBrushResource(resources, "HeaderTextBrush", "#111111");
                SetBrushResource(resources, "BodyTextBrush", "#222222");
                SetBrushResource(resources, "MutedTextBrush", "#444444");
                SetBrushResource(resources, "GridLineBrush", "#CFCFCF");
                SetBrushResource(resources, "PanelBorderBrush", "#D0D0D0");
            }

            ApplySurfaceOpacity(resources, settings);
            applyBoardPopulationStatusVisual?.Invoke();
            ApplyTitleBarTheme(window, settings.DarkModeEnabled);
        }

        public void ApplyWindowSettings(
            Window window,
            AppSettings settings,
            TextBlock windowOpacityValueText,
            ResourceDictionary resources)
        {
            if (window == null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (resources == null)
            {
                throw new ArgumentNullException(nameof(resources));
            }

            window.Topmost = settings.AlwaysOnTopEnabled;
            ApplySurfaceOpacity(resources, settings);

            var opacityPercent = CoerceOpacityPercent(settings.WindowOpacityPercent);

            if (windowOpacityValueText != null)
            {
                windowOpacityValueText.Text = $"{opacityPercent:0}%";
            }
        }

        public double CoerceOpacityPercent(double value)
        {
            if (value < 35)
            {
                return 35;
            }

            if (value > 100)
            {
                return 100;
            }

            return Math.Round(value, 0);
        }

        public void ApplyTitleBarTheme(Window window, bool darkModeEnabled)
        {
            if (window == null)
            {
                return;
            }

            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            var useDarkMode = darkModeEnabled ? 1 : 0;

            try
            {
                DwmSetWindowAttribute(hwnd, DwMWaUseImmersiveDarkMode, ref useDarkMode, sizeof(int));
            }
            catch
            {
            }
        }

        public void SaveMaxKillmailAge(
            AppSettings settings,
            TextBox maxKillmailAgeDaysTextBox,
            TextBlock effectiveMaxKillmailAgeText)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (maxKillmailAgeDaysTextBox == null)
            {
                throw new ArgumentNullException(nameof(maxKillmailAgeDaysTextBox));
            }

            if (effectiveMaxKillmailAgeText == null)
            {
                throw new ArgumentNullException(nameof(effectiveMaxKillmailAgeText));
            }

            try
            {
                var rawValue = maxKillmailAgeDaysTextBox.Text?.Trim() ?? string.Empty;

                if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDays))
                {
                    MessageBox.Show(
                        $"Enter a whole number between {KillmailDatasetFreshnessService.MinimumMaxKillmailAgeDays} and {KillmailDatasetFreshnessService.MaximumMaxKillmailAgeDays}.",
                        "PMG Max Killmail Age",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    maxKillmailAgeDaysTextBox.Text = GetMaxKillmailAgeTextBoxText(settings);
                    return;
                }

                var normalizedDays = KillmailDatasetFreshnessService.NormalizeMaxKillmailAgeDays(parsedDays);
                settings.MaxKillmailAgeDays = normalizedDays;
                _appSettingsService.Save(settings);
                maxKillmailAgeDaysTextBox.Text = GetMaxKillmailAgeTextBoxText(settings);
                UpdateMaxKillmailAgeUi(effectiveMaxKillmailAgeText, settings);

                AppLogger.UiInfo($"Max killmail age saved. days={normalizedDays}");

                MessageBox.Show(
                    $"Max killmail age saved as {normalizedDays} day{(normalizedDays == 1 ? "" : "s")}. The new value will apply the next time you use Enable KillMail DB Pull.",
                    "PMG Max Killmail Age",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Failed to save max killmail age.", ex);

                MessageBox.Show(
                    $"Failed to save max killmail age.\n\n{ex.Message}",
                    "PMG Max Killmail Age Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public void ResetMaxKillmailAgeToDefault(
            AppSettings settings,
            TextBox maxKillmailAgeDaysTextBox,
            TextBlock effectiveMaxKillmailAgeText)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (maxKillmailAgeDaysTextBox == null)
            {
                throw new ArgumentNullException(nameof(maxKillmailAgeDaysTextBox));
            }

            if (effectiveMaxKillmailAgeText == null)
            {
                throw new ArgumentNullException(nameof(effectiveMaxKillmailAgeText));
            }

            try
            {
                settings.MaxKillmailAgeDays = KillmailDatasetFreshnessService.DefaultMaxKillmailAgeDays;
                _appSettingsService.Save(settings);
                maxKillmailAgeDaysTextBox.Text = GetMaxKillmailAgeTextBoxText(settings);
                UpdateMaxKillmailAgeUi(effectiveMaxKillmailAgeText, settings);

                AppLogger.UiInfo($"Max killmail age reset to default. days={settings.MaxKillmailAgeDays}");

                MessageBox.Show(
                    $"Max killmail age reset to the default of {settings.MaxKillmailAgeDays} days.",
                    "PMG Max Killmail Age",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Failed to reset max killmail age to default.", ex);

                MessageBox.Show(
                    $"Failed to reset max killmail age.\n\n{ex.Message}",
                    "PMG Max Killmail Age Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public void SaveKillmailPath(
            AppSettings settings,
            TextBox killmailDataRootPathTextBox,
            TextBlock killmailDataPathModeText,
            TextBlock effectiveKillmailDataPathText)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (killmailDataRootPathTextBox == null)
            {
                throw new ArgumentNullException(nameof(killmailDataRootPathTextBox));
            }

            if (killmailDataPathModeText == null)
            {
                throw new ArgumentNullException(nameof(killmailDataPathModeText));
            }

            if (effectiveKillmailDataPathText == null)
            {
                throw new ArgumentNullException(nameof(effectiveKillmailDataPathText));
            }

            try
            {
                var rawValue = killmailDataRootPathTextBox.Text?.Trim() ?? string.Empty;
                var normalizedDefaultPath = KillmailPaths.NormalizeForComparison(KillmailPaths.GetDefaultKillmailDataDirectoryDisplayPath());

                if (string.IsNullOrWhiteSpace(rawValue))
                {
                    settings.KillmailDataRootPath = string.Empty;
                    _appSettingsService.Save(settings);
                    killmailDataRootPathTextBox.Text = GetKillmailPathEditorText(settings);
                    UpdateKillmailPathUi(killmailDataPathModeText, effectiveKillmailDataPathText);

                    AppLogger.UiInfo("Killmail data path override cleared via blank save. Restart required.");

                    MessageBox.Show(
                        "Killmail data path reset to the default %LOCALAPPDATA% location. Restart PMG to apply the new path fully.",
                        "PMG Killmail Data Path",
                        MessageBoxButton.OK,
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
                    Directory.CreateDirectory(expandedPath);

                    settings.KillmailDataRootPath = rawValue;
                }

                _appSettingsService.Save(settings);
                killmailDataRootPathTextBox.Text = GetKillmailPathEditorText(settings);
                UpdateKillmailPathUi(killmailDataPathModeText, effectiveKillmailDataPathText);

                AppLogger.UiInfo(
                    $"Killmail data path saved. configuredValue='{settings.KillmailDataRootPath ?? string.Empty}' displayPath='{KillmailPaths.GetKillmailDataDirectoryDisplayPath()}' source={KillmailPaths.GetKillmailDataDirectorySourceDescription()} restartRequired=true");

                MessageBox.Show(
                    "Killmail data path saved. Restart PMG to apply the new path fully. Existing killmail data is not migrated automatically.",
                    "PMG Killmail Data Path",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Failed to save killmail data path.", ex);

                MessageBox.Show(
                    $"Failed to save killmail data path.\n\n{ex.Message}",
                    "PMG Killmail Data Path Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public void ResetKillmailPathToDefault(
            AppSettings settings,
            TextBox killmailDataRootPathTextBox,
            TextBlock killmailDataPathModeText,
            TextBlock effectiveKillmailDataPathText)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (killmailDataRootPathTextBox == null)
            {
                throw new ArgumentNullException(nameof(killmailDataRootPathTextBox));
            }

            if (killmailDataPathModeText == null)
            {
                throw new ArgumentNullException(nameof(killmailDataPathModeText));
            }

            if (effectiveKillmailDataPathText == null)
            {
                throw new ArgumentNullException(nameof(effectiveKillmailDataPathText));
            }

            try
            {
                settings.KillmailDataRootPath = string.Empty;
                _appSettingsService.Save(settings);
                killmailDataRootPathTextBox.Text = GetKillmailPathEditorText(settings);
                UpdateKillmailPathUi(killmailDataPathModeText, effectiveKillmailDataPathText);

                AppLogger.UiInfo("Killmail data path reset to default %LOCALAPPDATA% location. Restart required.");

                MessageBox.Show(
                    "Killmail data path reset to the default %LOCALAPPDATA% location. Restart PMG to apply the new path fully.",
                    "PMG Killmail Data Path",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Failed to reset killmail data path to default.", ex);

                MessageBox.Show(
                    $"Failed to reset killmail data path.\n\n{ex.Message}",
                    "PMG Killmail Data Path Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public void ApplyLogLevelSelection(ComboBox logLevelComboBox, AppLogLevel logLevel)
        {
            if (logLevelComboBox == null)
            {
                return;
            }

            logLevelComboBox.SelectedIndex = logLevel == AppLogLevel.Debug ? 1 : 0;
        }

        public AppLogLevel GetSelectedLogLevel(ComboBox logLevelComboBox)
        {
            if (logLevelComboBox == null)
            {
                return AppLogLevel.Normal;
            }

            return logLevelComboBox.SelectedIndex == 1
                ? AppLogLevel.Debug
                : AppLogLevel.Normal;
        }

        public int GetMaxKillmailAgeDaysSettingValue(AppSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            return KillmailDatasetFreshnessService.NormalizeMaxKillmailAgeDays(settings.MaxKillmailAgeDays);
        }

        public string GetMaxKillmailAgeTextBoxText(AppSettings settings)
        {
            return GetMaxKillmailAgeDaysSettingValue(settings).ToString(CultureInfo.InvariantCulture);
        }

        public void UpdateMaxKillmailAgeUi(TextBlock effectiveMaxKillmailAgeText, AppSettings settings)
        {
            if (effectiveMaxKillmailAgeText == null)
            {
                throw new ArgumentNullException(nameof(effectiveMaxKillmailAgeText));
            }

            var days = GetMaxKillmailAgeDaysSettingValue(settings);
            var suffix = days == 1 ? "day" : "days";

            effectiveMaxKillmailAgeText.Text = $"Effective max killmail age: {days} {suffix}";
        }

        public void UpdateKillmailPathUi(TextBlock killmailDataPathModeText, TextBlock effectiveKillmailDataPathText)
        {
            if (killmailDataPathModeText == null)
            {
                throw new ArgumentNullException(nameof(killmailDataPathModeText));
            }

            if (effectiveKillmailDataPathText == null)
            {
                throw new ArgumentNullException(nameof(effectiveKillmailDataPathText));
            }

            var displayPath = KillmailPaths.GetKillmailDataDirectoryDisplayPath();
            var sourceDescription = KillmailPaths.GetKillmailDataDirectorySourceDescription();

            killmailDataPathModeText.Text = $"Source: {sourceDescription}";
            effectiveKillmailDataPathText.Text = $"Effective path: {displayPath}";
        }

        public string GetKillmailPathEditorText(AppSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (!string.IsNullOrWhiteSpace(settings.KillmailDataRootPath))
            {
                return settings.KillmailDataRootPath;
            }

            return KillmailPaths.GetDefaultKillmailDataDirectoryDisplayPath();
        }

        private void ApplySurfaceOpacity(ResourceDictionary resources, AppSettings settings)
        {
            if (resources == null)
            {
                throw new ArgumentNullException(nameof(resources));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var alpha = (byte)Math.Round(255 * (CoerceOpacityPercent(settings.WindowOpacityPercent) / 100.0));

            if (settings.DarkModeEnabled)
            {
                SetBrushResource(resources, "WindowBackgroundBrush", "#1E1E1E", alpha);
                SetBrushResource(resources, "SurfaceBrush", "#1F1F1F", alpha);
                SetBrushResource(resources, "SurfaceAltBrush", "#252525", alpha);

                SetBrushResource(resources, "GridBackgroundBrush", "#2B2B2B");
                SetBrushResource(resources, "GridAlternateBrush", "#323232");
                SetBrushResource(resources, "GridHeaderBrush", "#202020");
            }
            else
            {
                SetBrushResource(resources, "WindowBackgroundBrush", "#F5F5F5", alpha);
                SetBrushResource(resources, "SurfaceBrush", "#FFFFFF", alpha);
                SetBrushResource(resources, "SurfaceAltBrush", "#FAFAFA", alpha);

                SetBrushResource(resources, "GridBackgroundBrush", "#FFFFFF");
                SetBrushResource(resources, "GridAlternateBrush", "#F2F2F2");
                SetBrushResource(resources, "GridHeaderBrush", "#E8E8E8");
            }
        }

        private void SetBrushResource(ResourceDictionary resources, string resourceKey, string hexColor)
        {
            var color = (Color)ColorConverter.ConvertFromString(hexColor);
            resources[resourceKey] = new SolidColorBrush(color);
        }

        private void SetBrushResource(ResourceDictionary resources, string resourceKey, string hexColor, byte alpha)
        {
            var baseColor = (Color)ColorConverter.ConvertFromString(hexColor);
            resources[resourceKey] = new SolidColorBrush(Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B));
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
    }
}
