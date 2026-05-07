using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class MainWindowSettingsCoordinatorTests
    {
        [Fact]
        public void InitializeSettingsUi_AppliesGeneralIntelAndBoardDisplaySettings()
        {
            RunOnStaThread(() =>
            {
                var settings = new AppSettings
                {
                    DarkModeEnabled = true,
                    AlwaysOnTopEnabled = true,
                    PanelModeEnabled = false,
                    WindowOpacityPercent = 72,
                    MaxKillmailAgeDays = 45,
                    KillmailDataRootPath = @"C:\intel-cache",
                    VisualTheme = PmgVisualTheme.TacticalGrill.ToString(),
                    ColorBlindMode = PmgColorBlindMode.HighContrast.ToString(),
                    LogLevel = AppLogLevel.Debug,
                    LiveZkillFeedEnabled = true,
                    BackgroundHistoricalRepairEnabled = true,
                    PilotDetailPlacementPreference = PilotDetailPlacementPreference.AutoPreferLeft.ToString(),
                    ShowBoardGridLines = false,
                    BoardTextSize = 13,
                    BoardFontFamily = "Bahnschrift"
                };

                var coordinator = CreateCoordinator();
                var darkModeCheckBox = new CheckBox();
                var alwaysOnTopCheckBox = new CheckBox();
                var panelModeCheckBox = new CheckBox();
                var panelModeRestartNoticeText = new TextBlock();
                var windowOpacitySlider = new Slider
                {
                    Minimum = 35,
                    Maximum = 100
                };
                var windowOpacityValueText = new TextBlock();
                var maxKillmailAgeDaysTextBox = new TextBox();
                var effectiveMaxKillmailAgeText = new TextBlock();
                var killmailDataRootPathTextBox = new TextBox();
                var killmailDataPathModeText = new TextBlock();
                var effectiveKillmailDataPathText = new TextBlock();
                var visualThemeComboBox = CreateComboBox(3);
                var colorBlindModeComboBox = CreateComboBox(5);
                var logLevelComboBox = CreateComboBox(2);
                var enableLiveZkillFeedCheckBox = new CheckBox();
                var backgroundHistoricalRepairEnabledCheckBox = new CheckBox();
                var pilotDetailPlacementComboBox = CreateComboBox(2);
                var showBoardGridLinesCheckBox = new CheckBox();
                var boardTextSizeComboBox = CreateComboBox(7);
                var boardFontFamilyComboBox = CreateComboBox(4);

                coordinator.InitializeSettingsUi(
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
                    logLevelComboBox,
                    enableLiveZkillFeedCheckBox,
                    backgroundHistoricalRepairEnabledCheckBox,
                    pilotDetailPlacementComboBox,
                    showBoardGridLinesCheckBox,
                    boardTextSizeComboBox,
                    boardFontFamilyComboBox);

                Assert.True(darkModeCheckBox.IsChecked);
                Assert.True(alwaysOnTopCheckBox.IsChecked);
                Assert.False(panelModeCheckBox.IsChecked);
                Assert.Equal(72, windowOpacitySlider.Value);
                Assert.Equal("72%", windowOpacityValueText.Text);
                Assert.Equal("45", maxKillmailAgeDaysTextBox.Text);
                Assert.Equal(@"C:\intel-cache", killmailDataRootPathTextBox.Text);
                Assert.Equal(1, visualThemeComboBox.SelectedIndex);
                Assert.Equal(4, colorBlindModeComboBox.SelectedIndex);
                Assert.Equal(1, logLevelComboBox.SelectedIndex);
                Assert.True(enableLiveZkillFeedCheckBox.IsChecked);
                Assert.True(backgroundHistoricalRepairEnabledCheckBox.IsChecked);
                Assert.Equal(1, pilotDetailPlacementComboBox.SelectedIndex);
                Assert.False(showBoardGridLinesCheckBox.IsChecked);
                Assert.Equal(3, boardTextSizeComboBox.SelectedIndex);
                Assert.Equal(3, boardFontFamilyComboBox.SelectedIndex);
            });
        }

        [Fact]
        public void HandlePilotDetailPlacementPreferenceChanged_UpdatesSettingAndSaves()
        {
            RunOnStaThread(() =>
            {
                var saves = new List<AppSettings>();
                var coordinator = CreateCoordinator(saveSettings: settings => saves.Add(settings));
                var settings = new AppSettings();
                var comboBox = CreateComboBox(2);
                comboBox.SelectedIndex = 1;

                coordinator.HandlePilotDetailPlacementPreferenceChanged(false, settings, comboBox);

                Assert.Equal("AutoPreferLeft", settings.PilotDetailPlacementPreference);
                Assert.Single(saves);
            });
        }

        [Fact]
        public void HandleBackgroundHistoricalRepairChanged_UpdatesSettingAndSaves()
        {
            var saves = new List<AppSettings>();
            var coordinator = CreateCoordinator(saveSettings: settings => saves.Add(settings));
            var settings = new AppSettings();

            coordinator.HandleBackgroundHistoricalRepairChanged(false, settings, enabled: true);

            Assert.True(settings.BackgroundHistoricalRepairEnabled);
            Assert.Single(saves);
        }

        [Fact]
        public void HandleBoardTextSizeChanged_AppliesPersistsAndResizes()
        {
            RunOnStaThread(() =>
            {
                var saves = new List<AppSettings>();
                var applyCalls = 0;
                var resizeCalls = 0;
                var coordinator = CreateCoordinator(saveSettings: settings => saves.Add(settings));
                var settings = new AppSettings { BoardTextSize = 11 };
                var comboBox = CreateComboBox(7);
                comboBox.SelectedIndex = 4;

                coordinator.HandleBoardTextSizeChanged(
                    false,
                    settings,
                    comboBox,
                    () => applyCalls++,
                    () => resizeCalls++);

                Assert.Equal(14, settings.BoardTextSize);
                Assert.Equal(1, applyCalls);
                Assert.Equal(1, resizeCalls);
                Assert.Single(saves);
            });
        }

        private static MainWindowSettingsCoordinator CreateCoordinator(Action<AppSettings>? saveSettings = null)
        {
            return new MainWindowSettingsCoordinator(
                new MainWindowAppearanceController(new AppSettingsService()),
                new SettingsTabController(),
                new BoardDisplaySettingsController(),
                saveSettings ?? (_ => { }));
        }

        private static ComboBox CreateComboBox(int itemCount)
        {
            var comboBox = new ComboBox();
            for (var i = 0; i < itemCount; i++)
            {
                comboBox.Items.Add($"Item {i}");
            }

            return comboBox;
        }

        private static void RunOnStaThread(Action action)
        {
            Exception? capturedException = null;

            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    capturedException = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (capturedException != null)
            {
                ExceptionDispatchInfo.Capture(capturedException).Throw();
            }
        }
    }
}
