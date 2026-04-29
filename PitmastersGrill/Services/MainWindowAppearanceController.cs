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
        private const double MinimumOpacityPercent = 35;
        private const double MaximumOpacityPercent = 100;

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
            CheckBox panelModeCheckBox,
            TextBlock panelModeRestartNoticeText,
            Slider windowOpacitySlider,
            TextBlock windowOpacityValueText,
            TextBox maxKillmailAgeDaysTextBox,
            TextBlock effectiveMaxKillmailAgeText,
            TextBox killmailDataRootPathTextBox,
            TextBlock killmailDataPathModeText,
            TextBlock effectiveKillmailDataPathText,
            ComboBox visualThemeComboBox,
            ComboBox colorBlindModeComboBox,
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

            if (panelModeCheckBox != null)
            {
                panelModeCheckBox.IsChecked = settings.PanelModeEnabled;
            }

            UpdatePanelModeRestartNotice(panelModeRestartNoticeText, settings);

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
            ApplyThemeSelection(visualThemeComboBox, settings.VisualTheme);
            ApplyColorBlindModeSelection(colorBlindModeComboBox, settings.ColorBlindMode);
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

        public void HandlePanelModeChanged(
            AppSettings settings,
            bool panelModeEnabled,
            TextBlock panelModeRestartNoticeText)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.PanelModeEnabled = panelModeEnabled;
            _appSettingsService.Save(settings);
            UpdatePanelModeRestartNotice(panelModeRestartNoticeText, settings);

            AppLogger.UiInfo($"Panel mode changed. enabled={settings.PanelModeEnabled} restartRequired=true");

            MessageBox.Show(
                settings.PanelModeEnabled
                    ? "Panel Mode has been enabled. Restart PMG to apply the transparent overlay shell."
                    : "Panel Mode has been disabled. Restart PMG to return to the standard Windows shell.",
                "PMG Panel Mode",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        public void ApplyPanelModeShell(
            Window window,
            AppSettings settings,
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

            if (settings.PanelModeEnabled)
            {
                window.WindowStyle = WindowStyle.None;
                window.ResizeMode = ResizeMode.CanResizeWithGrip;
                window.AllowsTransparency = true;
                window.Background = Brushes.Transparent;

                AppLogger.UiInfo("Panel mode shell applied. windowStyle=None allowsTransparency=true resizeMode=CanResizeWithGrip");
                return;
            }

            window.WindowStyle = WindowStyle.SingleBorderWindow;
            window.ResizeMode = ResizeMode.CanResize;
            window.AllowsTransparency = false;
            window.SetResourceReference(Window.BackgroundProperty, "WindowBackgroundBrush");

            AppLogger.UiInfo("Standard window shell applied. windowStyle=SingleBorderWindow allowsTransparency=false resizeMode=CanResize");
        }

        public double HandleWindowOpacityChanged
(
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

        public void HandleVisualThemeChanged(
            AppSettings settings,
            ComboBox visualThemeComboBox,
            ResourceDictionary resources,
            Window window,
            Action applyBoardPopulationStatusVisual)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var selectedTheme = GetSelectedTheme(visualThemeComboBox);
            if (string.Equals(settings.VisualTheme, selectedTheme.ToString(), StringComparison.Ordinal))
            {
                return;
            }

            settings.VisualTheme = selectedTheme.ToString();
            _appSettingsService.Save(settings);
            ApplyTheme(resources, settings, window, applyBoardPopulationStatusVisual);

            AppLogger.UiInfo($"Visual theme changed. theme={settings.VisualTheme}");
        }

        public void HandleColorBlindModeChanged(
            AppSettings settings,
            ComboBox colorBlindModeComboBox,
            ResourceDictionary resources,
            Window window,
            Action applyBoardPopulationStatusVisual)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var selectedMode = GetSelectedColorBlindMode(colorBlindModeComboBox);
            if (string.Equals(settings.ColorBlindMode, selectedMode.ToString(), StringComparison.Ordinal))
            {
                return;
            }

            settings.ColorBlindMode = selectedMode.ToString();
            _appSettingsService.Save(settings);
            ApplyTheme(resources, settings, window, applyBoardPopulationStatusVisual);

            AppLogger.UiInfo($"Color-blind mode changed. mode={settings.ColorBlindMode}");
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
                ApplyThemeResourceDictionary(resources, GetSelectedTheme(settings.VisualTheme));
                SetBrushResource(resources, "HeaderTextBrush", GetColorResource(resources, "TextPrimary", "#FFFFFF"));
                SetBrushResource(resources, "BodyTextBrush", GetColorResource(resources, "TextPrimary", "#F3F3F3"));
                SetBrushResource(resources, "MutedTextBrush", GetColorResource(resources, "TextSecondary", "#DDDDDD"));
                SetBrushResource(resources, "GridLineBrush", GetColorResource(resources, "BoardGridLine", "#4A4A4A"));
                SetBrushResource(resources, "PanelBorderBrush", GetColorResource(resources, "PanelBorder", "#3A3A3A"));
                ApplyThemeBrushes(resources);
            }
            else
            {
                ApplyLightPaletteTokens(resources);
                SetBrushResource(resources, "HeaderTextBrush", "#111111");
                SetBrushResource(resources, "BodyTextBrush", "#222222");
                SetBrushResource(resources, "MutedTextBrush", "#444444");
                SetBrushResource(resources, "GridLineBrush", "#CFCFCF");
                SetBrushResource(resources, "PanelBorderBrush", "#D0D0D0");
                ApplyThemeBrushes(resources);
            }

            ApplySurfaceOpacity(resources, settings);
            ApplySignalAccessibilityPalette(resources, settings);
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
            if (value < MinimumOpacityPercent)
            {
                return MinimumOpacityPercent;
            }

            if (value > MaximumOpacityPercent)
            {
                return MaximumOpacityPercent;
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

        public void ApplyThemeSelection(ComboBox visualThemeComboBox, string visualTheme)
        {
            if (visualThemeComboBox == null)
            {
                return;
            }

            visualThemeComboBox.SelectedIndex = GetSelectedTheme(visualTheme) switch
            {
                PmgVisualTheme.TacticalGrill => 1,
                PmgVisualTheme.ClassicPmgGrill => 2,
                _ => 0
            };
        }

        public void ApplyColorBlindModeSelection(ComboBox colorBlindModeComboBox, string colorBlindMode)
        {
            if (colorBlindModeComboBox == null)
            {
                return;
            }

            colorBlindModeComboBox.SelectedIndex = GetSelectedColorBlindMode(colorBlindMode) switch
            {
                PmgColorBlindMode.DeuteranopiaFriendly => 1,
                PmgColorBlindMode.ProtanopiaFriendly => 2,
                PmgColorBlindMode.TritanopiaFriendly => 3,
                PmgColorBlindMode.HighContrast => 4,
                _ => 0
            };
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

        private void UpdatePanelModeRestartNotice(TextBlock panelModeRestartNoticeText, AppSettings settings)
        {
            if (panelModeRestartNoticeText == null || settings == null)
            {
                return;
            }

            panelModeRestartNoticeText.Text = settings.PanelModeEnabled
                ? "Panel Mode is enabled. Restart PMG to apply the transparent overlay shell."
                : "Panel Mode is disabled. Enable it to use the transparent overlay shell after restart.";
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
                SetBrushResource(resources, "WindowBackgroundBrush", GetColorResource(resources, "BackgroundBase", "#1E1E1E"), alpha);
                SetBrushResource(resources, "SurfaceBrush", GetColorResource(resources, "PanelBackground", "#1F1F1F"), alpha);
                SetBrushResource(resources, "SurfaceAltBrush", GetColorResource(resources, "PanelBackgroundAlt", "#252525"), alpha);

                SetBrushResource(resources, "GridBackgroundBrush", GetColorResource(resources, "BoardBackground", "#2B2B2B"), alpha);
                SetBrushResource(resources, "GridAlternateBrush", GetColorResource(resources, "BoardAlternate", "#323232"), alpha);
                SetBrushResource(resources, "GridHeaderBrush", GetColorResource(resources, "BoardHeaderBackground", "#202020"), alpha);
            }
            else
            {
                SetBrushResource(resources, "WindowBackgroundBrush", "#F5F5F5", alpha);
                SetBrushResource(resources, "SurfaceBrush", "#FFFFFF", alpha);
                SetBrushResource(resources, "SurfaceAltBrush", "#FAFAFA", alpha);

                SetBrushResource(resources, "GridBackgroundBrush", "#FFFFFF", alpha);
                SetBrushResource(resources, "GridAlternateBrush", "#F2F2F2", alpha);
                SetBrushResource(resources, "GridHeaderBrush", "#E8E8E8", alpha);
            }
        }

        private static PmgVisualTheme GetSelectedTheme(string? visualTheme)
        {
            return Enum.TryParse<PmgVisualTheme>(visualTheme, ignoreCase: true, out var parsed)
                ? parsed
                : PmgVisualTheme.CharcoalOps;
        }

        private static PmgVisualTheme GetSelectedTheme(ComboBox visualThemeComboBox)
        {
            if (visualThemeComboBox == null)
            {
                return PmgVisualTheme.CharcoalOps;
            }

            return visualThemeComboBox.SelectedIndex switch
            {
                1 => PmgVisualTheme.TacticalGrill,
                2 => PmgVisualTheme.ClassicPmgGrill,
                _ => PmgVisualTheme.CharcoalOps
            };
        }

        private static PmgColorBlindMode GetSelectedColorBlindMode(string? colorBlindMode)
        {
            return Enum.TryParse<PmgColorBlindMode>(colorBlindMode, ignoreCase: true, out var parsed)
                ? parsed
                : PmgColorBlindMode.Standard;
        }

        private static PmgColorBlindMode GetSelectedColorBlindMode(ComboBox colorBlindModeComboBox)
        {
            if (colorBlindModeComboBox == null)
            {
                return PmgColorBlindMode.Standard;
            }

            return colorBlindModeComboBox.SelectedIndex switch
            {
                1 => PmgColorBlindMode.DeuteranopiaFriendly,
                2 => PmgColorBlindMode.ProtanopiaFriendly,
                3 => PmgColorBlindMode.TritanopiaFriendly,
                4 => PmgColorBlindMode.HighContrast,
                _ => PmgColorBlindMode.Standard
            };
        }

        private static void ApplyThemeResourceDictionary(ResourceDictionary resources, PmgVisualTheme visualTheme)
        {
            var path = visualTheme switch
            {
                PmgVisualTheme.TacticalGrill => "Themes/TacticalGrill.xaml",
                PmgVisualTheme.ClassicPmgGrill => "Themes/ClassicPmgGrill.xaml",
                _ => "Themes/CharcoalOps.xaml"
            };

            try
            {
                if (Application.LoadComponent(new Uri(path, UriKind.Relative)) is not ResourceDictionary dictionary)
                {
                    return;
                }

                foreach (var key in dictionary.Keys)
                {
                    resources[key] = dictionary[key];
                }
            }
            catch (Exception ex)
            {
                AppLogger.UiWarn($"Theme dictionary load failed. theme={visualTheme} message={ex.Message}");
                ApplyLightPaletteTokens(resources);
            }
        }

        private static void ApplyLightPaletteTokens(ResourceDictionary resources)
        {
            resources["BackgroundBase"] = (Color)ColorConverter.ConvertFromString("#F5F5F5");
            resources["PanelBackground"] = (Color)ColorConverter.ConvertFromString("#FFFFFF");
            resources["PanelBackgroundAlt"] = (Color)ColorConverter.ConvertFromString("#FAFAFA");
            resources["PanelBorder"] = (Color)ColorConverter.ConvertFromString("#D0D0D0");
            resources["BoardBackground"] = (Color)ColorConverter.ConvertFromString("#FFFFFF");
            resources["BoardAlternate"] = (Color)ColorConverter.ConvertFromString("#F2F2F2");
            resources["BoardHeaderBackground"] = (Color)ColorConverter.ConvertFromString("#E8E8E8");
            resources["BoardGridLine"] = (Color)ColorConverter.ConvertFromString("#CFCFCF");
            resources["AccentEmber"] = (Color)ColorConverter.ConvertFromString("#D97706");
            resources["AccentHotCoal"] = (Color)ColorConverter.ConvertFromString("#B45309");
            resources["AccentAsh"] = (Color)ColorConverter.ConvertFromString("#6B7280");
            resources["ThreatCritical"] = (Color)ColorConverter.ConvertFromString("#B91C1C");
            resources["ThreatSevere"] = (Color)ColorConverter.ConvertFromString("#DC2626");
            resources["ThreatHigh"] = (Color)ColorConverter.ConvertFromString("#D97706");
            resources["ThreatElevated"] = (Color)ColorConverter.ConvertFromString("#B98235");
            resources["ThreatLow"] = (Color)ColorConverter.ConvertFromString("#85754E");
            resources["ThreatNeutral"] = (Color)ColorConverter.ConvertFromString("#6B7280");
            resources["SuccessGreen"] = (Color)ColorConverter.ConvertFromString("#15803D");
            resources["WarningAmber"] = (Color)ColorConverter.ConvertFromString("#B45309");
            resources["ErrorRed"] = (Color)ColorConverter.ConvertFromString("#B91C1C");
        }

        private static string GetColorResource(ResourceDictionary resources, string resourceKey, string fallbackHexColor)
        {
            return resources[resourceKey] is Color color
                ? color.ToString()
                : fallbackHexColor;
        }

        private static void ApplyThemeBrushes(ResourceDictionary resources)
        {
            SetBrushResource(resources, "AccentEmberBrush", GetColorResource(resources, "AccentEmber", "#F28C28"));
            SetBrushResource(resources, "AccentHotCoalBrush", GetColorResource(resources, "AccentHotCoal", "#D94A1E"));
            SetBrushResource(resources, "AccentAshBrush", GetColorResource(resources, "AccentAsh", "#858985"));
            SetBrushResource(resources, "ThreatCriticalBrush", GetColorResource(resources, "ThreatCritical", "#FF3B21"));
            SetBrushResource(resources, "ThreatSevereBrush", GetColorResource(resources, "ThreatSevere", "#D94A1E"));
            SetBrushResource(resources, "ThreatHighBrush", GetColorResource(resources, "ThreatHigh", "#F28C28"));
            SetBrushResource(resources, "ThreatElevatedBrush", GetColorResource(resources, "ThreatElevated", "#C79035"));
            SetBrushResource(resources, "ThreatLowBrush", GetColorResource(resources, "ThreatLow", "#B6A36B"));
            SetBrushResource(resources, "ThreatNeutralBrush", GetColorResource(resources, "ThreatNeutral", "#858985"));
            SetBrushResource(resources, "SuccessGreenBrush", GetColorResource(resources, "SuccessGreen", "#6FBF73"));
            SetBrushResource(resources, "WarningAmberBrush", GetColorResource(resources, "WarningAmber", "#F6B94B"));
            SetBrushResource(resources, "ErrorRedBrush", GetColorResource(resources, "ErrorRed", "#EF5350"));
            SetBrushResource(resources, "BoardSignalConfirmedCovertBrush", "#B48CFF");
            SetBrushResource(resources, "BoardSignalConfirmedNormalBrush", GetColorResource(resources, "ThreatCritical", "#EF5350"));
            SetBrushResource(resources, "BoardSignalInferredCynoBrush", GetColorResource(resources, "ThreatHigh", "#F28C28"));
            SetBrushResource(resources, "BoardSignalPossibleBrush", GetColorResource(resources, "WarningAmber", "#F6B94B"));
            SetBrushResource(resources, "BoardSignalBaitBrush", "#B8915E");
        }

        private static void ApplySignalAccessibilityPalette(ResourceDictionary resources, AppSettings settings)
        {
            switch (GetSelectedColorBlindMode(settings.ColorBlindMode))
            {
                case PmgColorBlindMode.DeuteranopiaFriendly:
                    SetSignalBrushes(resources, "#CC79A7", "#D55E00", "#E69F00", "#F0E442", "#999999");
                    break;
                case PmgColorBlindMode.ProtanopiaFriendly:
                    SetSignalBrushes(resources, "#CC79A7", "#D55E00", "#0072B2", "#E69F00", "#999999");
                    break;
                case PmgColorBlindMode.TritanopiaFriendly:
                    SetSignalBrushes(resources, "#9467BD", "#D62728", "#2CA02C", "#FF7F0E", "#8C564B");
                    break;
                case PmgColorBlindMode.HighContrast:
                    SetSignalBrushes(resources, "#FFFFFF", "#FF1744", "#00E5FF", "#FFD600", "#BDBDBD");
                    break;
                default:
                    break;
            }
        }

        private static void SetSignalBrushes(
            ResourceDictionary resources,
            string confirmedCovert,
            string confirmedNormal,
            string inferred,
            string possible,
            string bait)
        {
            SetBrushResource(resources, "BoardSignalConfirmedCovertBrush", confirmedCovert);
            SetBrushResource(resources, "BoardSignalConfirmedNormalBrush", confirmedNormal);
            SetBrushResource(resources, "BoardSignalInferredCynoBrush", inferred);
            SetBrushResource(resources, "BoardSignalPossibleBrush", possible);
            SetBrushResource(resources, "BoardSignalBaitBrush", bait);
        }

        private static void SetBrushResource(ResourceDictionary resources, string resourceKey, string hexColor)
        {
            var color = (Color)ColorConverter.ConvertFromString(hexColor);
            resources[resourceKey] = new SolidColorBrush(color);
        }

        private static void SetBrushResource(ResourceDictionary resources, string resourceKey, string hexColor, byte alpha)
        {
            var baseColor = (Color)ColorConverter.ConvertFromString(hexColor);
            resources[resourceKey] = new SolidColorBrush(Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B));
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
    }
}
