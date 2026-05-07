using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Windows;
using System.Windows.Controls;

namespace PitmastersGrill.Services
{
    public sealed class MainWindowSettingsCoordinator
    {
        private readonly MainWindowAppearanceController _appearanceController;
        private readonly SettingsTabController _settingsTabController;
        private readonly BoardDisplaySettingsController _boardDisplaySettingsController;
        private readonly Action<AppSettings> _saveSettings;

        public MainWindowSettingsCoordinator(
            MainWindowAppearanceController appearanceController,
            SettingsTabController settingsTabController,
            BoardDisplaySettingsController boardDisplaySettingsController,
            Action<AppSettings> saveSettings)
        {
            _appearanceController = appearanceController ?? throw new ArgumentNullException(nameof(appearanceController));
            _settingsTabController = settingsTabController ?? throw new ArgumentNullException(nameof(settingsTabController));
            _boardDisplaySettingsController = boardDisplaySettingsController ?? throw new ArgumentNullException(nameof(boardDisplaySettingsController));
            _saveSettings = saveSettings ?? throw new ArgumentNullException(nameof(saveSettings));
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
            ComboBox logLevelComboBox,
            CheckBox enableLiveZkillFeedCheckBox,
            CheckBox backgroundHistoricalRepairEnabledCheckBox,
            ComboBox pilotDetailPlacementComboBox,
            CheckBox showBoardGridLinesCheckBox,
            ComboBox boardTextSizeComboBox,
            ComboBox boardFontFamilyComboBox)
        {
            _appearanceController.InitializeSettingsUi(
                settings,
                darkModeCheckBox,
                alwaysOnTopCheckBox,
                panelModeCheckBox,
                panelModeRestartNoticeText,
                windowOpacitySlider,
                windowOpacityValueText,
                maxKillmailAgeDaysTextBox,
                effectiveMaxKillmailAgeText,
                killmailDataRootPathTextBox,
                killmailDataPathModeText,
                effectiveKillmailDataPathText,
                visualThemeComboBox,
                colorBlindModeComboBox,
                logLevelComboBox);
            _settingsTabController.ApplySettingsToControls(
                settings,
                enableLiveZkillFeedCheckBox,
                backgroundHistoricalRepairEnabledCheckBox,
                pilotDetailPlacementComboBox);
            _boardDisplaySettingsController.ApplySettingsToControls(
                settings,
                showBoardGridLinesCheckBox,
                boardTextSizeComboBox,
                boardFontFamilyComboBox);
        }

        public void HandleDarkModeChanged(
            bool isApplyingSettings,
            AppSettings settings,
            bool enabled,
            ResourceDictionary resources,
            Window window,
            Action applyBoardPopulationStatusVisual,
            Action? applyDetailTheme)
        {
            if (isApplyingSettings)
            {
                return;
            }

            _appearanceController.HandleDarkModeChanged(settings, enabled, resources, window, applyBoardPopulationStatusVisual);
            applyDetailTheme?.Invoke();
        }

        public void HandleAlwaysOnTopChanged(
            bool isApplyingSettings,
            AppSettings settings,
            bool enabled,
            Window window,
            TextBlock? windowOpacityValueText,
            ResourceDictionary resources)
        {
            if (isApplyingSettings)
            {
                return;
            }

            _appearanceController.HandleAlwaysOnTopChanged(settings, enabled, window, windowOpacityValueText, resources);
        }

        public void HandlePanelModeChanged(
            bool isApplyingSettings,
            AppSettings settings,
            bool enabled,
            TextBlock panelModeRestartNoticeText)
        {
            if (isApplyingSettings)
            {
                return;
            }

            _appearanceController.HandlePanelModeChanged(settings, enabled, panelModeRestartNoticeText);
        }

        public void HandleWindowOpacityChanged(
            bool isApplyingSettings,
            AppSettings settings,
            double sliderValue,
            Window window,
            TextBlock? windowOpacityValueText,
            ResourceDictionary resources,
            Action? applyDetailTheme)
        {
            var opacityPercent = _appearanceController.CoerceOpacityPercent(sliderValue);
            if (windowOpacityValueText != null)
            {
                windowOpacityValueText.Text = $"{opacityPercent:0}%";
            }

            if (isApplyingSettings)
            {
                return;
            }

            _appearanceController.HandleWindowOpacityChanged(settings, sliderValue, window, windowOpacityValueText, resources);
            applyDetailTheme?.Invoke();
        }

        public void HandleLogLevelChanged(bool isApplyingSettings, AppSettings settings, ComboBox? logLevelComboBox)
        {
            if (isApplyingSettings || logLevelComboBox == null)
            {
                return;
            }

            _appearanceController.HandleLogLevelChanged(settings, logLevelComboBox);
        }

        public void HandleVisualThemeChanged(
            bool isApplyingSettings,
            AppSettings settings,
            ComboBox? visualThemeComboBox,
            ResourceDictionary resources,
            Window window,
            Action applyBoardPopulationStatusVisual,
            Action? applyDetailTheme)
        {
            if (isApplyingSettings || visualThemeComboBox == null)
            {
                return;
            }

            _appearanceController.HandleVisualThemeChanged(
                settings,
                visualThemeComboBox,
                resources,
                window,
                applyBoardPopulationStatusVisual);
            applyDetailTheme?.Invoke();
        }

        public void HandleColorBlindModeChanged(
            bool isApplyingSettings,
            AppSettings settings,
            ComboBox? colorBlindModeComboBox,
            ResourceDictionary resources,
            Window window,
            Action applyBoardPopulationStatusVisual,
            Action? applyDetailTheme,
            Action? refreshBoardItems)
        {
            if (isApplyingSettings || colorBlindModeComboBox == null)
            {
                return;
            }

            _appearanceController.HandleColorBlindModeChanged(
                settings,
                colorBlindModeComboBox,
                resources,
                window,
                applyBoardPopulationStatusVisual);
            applyDetailTheme?.Invoke();
            refreshBoardItems?.Invoke();
        }

        public void HandlePilotDetailPlacementPreferenceChanged(
            bool isApplyingSettings,
            AppSettings settings,
            ComboBox? pilotDetailPlacementComboBox)
        {
            if (isApplyingSettings || pilotDetailPlacementComboBox == null)
            {
                return;
            }

            _settingsTabController.SetPilotDetailPlacementPreference(settings, pilotDetailPlacementComboBox.SelectedIndex);
            _saveSettings(settings);

            AppLogger.UiInfo($"Pilot detail placement preference changed. preference={settings.PilotDetailPlacementPreference}");
        }

        public void HandleBackgroundHistoricalRepairChanged(bool isApplyingSettings, AppSettings settings, bool enabled)
        {
            if (isApplyingSettings)
            {
                return;
            }

            _settingsTabController.SetBackgroundHistoricalRepairEnabled(settings, enabled);
            _saveSettings(settings);

            AppLogger.UiInfo($"Background historical repair setting changed. enabled={enabled}");
        }

        public void HandleShowBoardGridLinesChanged(
            bool isApplyingSettings,
            AppSettings settings,
            CheckBox? showBoardGridLinesCheckBox,
            Action applyBoardDisplaySettings)
        {
            if (isApplyingSettings || showBoardGridLinesCheckBox == null)
            {
                return;
            }

            _boardDisplaySettingsController.SetShowBoardGridLines(settings, showBoardGridLinesCheckBox.IsChecked == true);
            applyBoardDisplaySettings();
            _saveSettings(settings);

            AppLogger.UiInfo($"Board grid lines changed. enabled={settings.ShowBoardGridLines}");
        }

        public void HandleBoardTextSizeChanged(
            bool isApplyingSettings,
            AppSettings settings,
            ComboBox? boardTextSizeComboBox,
            Action applyBoardDisplaySettings,
            Action updateWindowMinimumSize)
        {
            if (isApplyingSettings || boardTextSizeComboBox == null)
            {
                return;
            }

            _boardDisplaySettingsController.SetBoardTextSize(settings, boardTextSizeComboBox.SelectedIndex);
            applyBoardDisplaySettings();
            updateWindowMinimumSize();
            _saveSettings(settings);

            AppLogger.UiInfo($"Board text size changed. size={settings.BoardTextSize}");
        }

        public void HandleBoardFontFamilyChanged(
            bool isApplyingSettings,
            AppSettings settings,
            ComboBox? boardFontFamilyComboBox,
            Action applyBoardDisplaySettings,
            Action updateWindowMinimumSize)
        {
            if (isApplyingSettings || boardFontFamilyComboBox == null)
            {
                return;
            }

            _boardDisplaySettingsController.SetBoardFontFamily(settings, boardFontFamilyComboBox.SelectedIndex);
            applyBoardDisplaySettings();
            updateWindowMinimumSize();
            _saveSettings(settings);

            AppLogger.UiInfo(
                $"Board font family changed. family='{(settings.BoardFontFamily.Length == 0 ? "Default" : settings.BoardFontFamily)}'");
        }
    }
}
