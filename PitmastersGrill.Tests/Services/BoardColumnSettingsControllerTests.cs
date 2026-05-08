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
    public sealed class BoardColumnSettingsControllerTests
    {
        [Fact]
        public void HandleBoardColumnVisibilityChanged_WhenNotApplying_UpdatesSettingsAppliesVisibilityAndSaves()
        {
            RunOnStaThread(() =>
            {
                var saves = new List<AppSettings>();
                var applyCalls = 0;
                var controller = CreateController(saves.Add);
                var settings = new AppSettings();

                controller.HandleBoardColumnVisibilityChanged(
                    isApplyingSettings: false,
                    settings,
                    new CheckBox { IsChecked = false },
                    new CheckBox { IsChecked = true },
                    new CheckBox { IsChecked = false },
                    new CheckBox { IsChecked = true },
                    new CheckBox { IsChecked = false },
                    new CheckBox { IsChecked = true },
                    new CheckBox { IsChecked = false },
                    new CheckBox { IsChecked = true },
                    new CheckBox { IsChecked = false },
                    () => applyCalls++);

                Assert.False(settings.ShowSigColumn);
                Assert.True(settings.ShowAllianceColumn);
                Assert.False(settings.ShowCorpColumn);
                Assert.True(settings.ShowKillsColumn);
                Assert.False(settings.ShowLossesColumn);
                Assert.True(settings.ShowAvgFleetSizeColumn);
                Assert.False(settings.ShowLastShipSeenColumn);
                Assert.True(settings.ShowLastSeenColumn);
                Assert.False(settings.ShowCynoHullSeenColumn);
                Assert.Equal(1, applyCalls);
                Assert.Single(saves);
            });
        }

        [Fact]
        public void HandleBoardColumnVisibilityChanged_WhenApplying_DoesNothing()
        {
            RunOnStaThread(() =>
            {
                var saves = new List<AppSettings>();
                var applyCalls = 0;
                var controller = CreateController(saves.Add);
                var settings = new AppSettings
                {
                    ShowSigColumn = true,
                    ShowAllianceColumn = false
                };

                controller.HandleBoardColumnVisibilityChanged(
                    isApplyingSettings: true,
                    settings,
                    new CheckBox { IsChecked = false },
                    new CheckBox { IsChecked = true },
                    new CheckBox { IsChecked = false },
                    new CheckBox { IsChecked = true },
                    new CheckBox { IsChecked = false },
                    new CheckBox { IsChecked = true },
                    new CheckBox { IsChecked = false },
                    new CheckBox { IsChecked = true },
                    new CheckBox { IsChecked = false },
                    () => applyCalls++);

                Assert.True(settings.ShowSigColumn);
                Assert.False(settings.ShowAllianceColumn);
                Assert.Equal(0, applyCalls);
                Assert.Empty(saves);
            });
        }

        [Fact]
        public void HandleShowAllBoardColumns_SetsAllOptionalColumnsVisibleAndSaves()
        {
            var saves = new List<AppSettings>();
            var applyCheckBoxesCalls = 0;
            var applyVisibilityCalls = 0;
            var controller = CreateController(saves.Add);
            var settings = new AppSettings
            {
                ShowSigColumn = false,
                ShowAllianceColumn = false,
                ShowCorpColumn = false,
                ShowKillsColumn = false,
                ShowLossesColumn = false,
                ShowAvgFleetSizeColumn = false,
                ShowLastShipSeenColumn = false,
                ShowLastSeenColumn = false,
                ShowCynoHullSeenColumn = false
            };

            controller.HandleShowAllBoardColumns(
                settings,
                () => applyCheckBoxesCalls++,
                () => applyVisibilityCalls++);

            Assert.True(settings.ShowSigColumn);
            Assert.True(settings.ShowAllianceColumn);
            Assert.True(settings.ShowCorpColumn);
            Assert.True(settings.ShowKillsColumn);
            Assert.True(settings.ShowLossesColumn);
            Assert.True(settings.ShowAvgFleetSizeColumn);
            Assert.True(settings.ShowLastShipSeenColumn);
            Assert.True(settings.ShowLastSeenColumn);
            Assert.True(settings.ShowCynoHullSeenColumn);
            Assert.Equal(1, applyCheckBoxesCalls);
            Assert.Equal(1, applyVisibilityCalls);
            Assert.Single(saves);
        }

        [Fact]
        public void InitializeBoardColumnLayoutUi_InitializesLayoutAndAppliesCanonicalDefaults()
        {
            RunOnStaThread(() =>
            {
                var controller = CreateController(_ => { });
                var appliedLayouts = new List<IReadOnlyList<BoardColumnLayoutSetting>>();
                var reasons = new List<string>();

                controller.InitializeBoardColumnLayoutUi(
                    (layout, reason) =>
                    {
                        appliedLayouts.Add(new List<BoardColumnLayoutSetting>(layout));
                        reasons.Add(reason);
                    },
                    ("SigColumn", new DataGridTextColumn()),
                    ("CharacterColumn", new DataGridTextColumn()),
                    ("AllianceColumn", new DataGridTextColumn()),
                    ("CorpColumn", new DataGridTextColumn()),
                    ("KillsColumn", new DataGridTextColumn()),
                    ("LossesColumn", new DataGridTextColumn()),
                    ("AvgFleetSizeColumn", new DataGridTextColumn()),
                    ("LastShipSeenColumn", new DataGridTextColumn()),
                    ("LastSeenColumn", new DataGridTextColumn()),
                    ("CynoHullSeenColumn", new DataGridTextColumn()));

                Assert.Single(appliedLayouts);
                Assert.Equal("Apply canonical default board layout", reasons[0]);
                Assert.Equal(10, appliedLayouts[0].Count);
                Assert.Equal("SigColumn", appliedLayouts[0][0].ColumnKey);
            });
        }

        [Fact]
        public void HandleResetBoardLayout_ClearsSavedLayoutAndInvokesCallbacks()
        {
            var canonicalReasons = new List<string>();
            var saveReasons = new List<string>();
            var controller = CreateController(_ => { });
            var settings = new AppSettings
            {
                BoardColumnLayout = new List<BoardColumnLayoutSetting>
                {
                    new() { ColumnKey = "SigColumn", DisplayIndex = 0, WidthValue = 42, WidthUnitType = "Pixel" }
                }
            };

            controller.HandleResetBoardLayout(
                settings,
                canonicalReasons.Add,
                saveReasons.Add);

            Assert.Empty(settings.BoardColumnLayout);
            Assert.Equal("Reset board layout to canonical defaults", canonicalReasons[0]);
            Assert.Equal("Reset layout", saveReasons[0]);
        }

        [Fact]
        public void HandleShowCorpAllianceCountsChanged_WhenNotApplying_UpdatesSettingRecomputesAndSaves()
        {
            var saves = new List<AppSettings>();
            var recomputeCalls = 0;
            var controller = CreateController(saves.Add);
            var settings = new AppSettings();

            controller.HandleShowCorpAllianceCountsChanged(
                isApplyingSettings: false,
                settings,
                enabled: true,
                recomputeCorpAllianceCounts: () => recomputeCalls++);

            Assert.True(settings.ShowCorpAllianceCounts);
            Assert.Equal(1, recomputeCalls);
            Assert.Single(saves);
        }

        private static BoardColumnSettingsController CreateController(Action<AppSettings> saveSettings)
        {
            return new BoardColumnSettingsController(new BoardColumnLayoutController(), saveSettings);
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
