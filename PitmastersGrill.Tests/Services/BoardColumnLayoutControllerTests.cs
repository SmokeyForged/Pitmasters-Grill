using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public class BoardColumnLayoutControllerTests
    {
        [Fact]
        public void GetCanonicalBoardColumnLayout_IncludesSigColumnAfterInitialization()
        {
            RunOnStaThread(() =>
            {
                var (controller, _) = CreateInitializedController();

                var layout = controller.GetCanonicalBoardColumnLayout();

                Assert.Equal(10, layout.Count);
                Assert.Equal("SigColumn", layout[0].ColumnKey);
                Assert.Contains(layout, setting => string.Equals(setting.ColumnKey, "SigColumn", StringComparison.Ordinal));
                Assert.Contains(layout, setting => string.Equals(setting.ColumnKey, "CynoHullSeenColumn", StringComparison.Ordinal));
            });
        }

        [Fact]
        public void ApplyBoardColumnVisibility_CanApplySigColumnAfterInitialization()
        {
            RunOnStaThread(() =>
            {
                var (controller, columns) = CreateInitializedController();
                var settings = new AppSettings
                {
                    ShowSigColumn = false,
                    ShowAllianceColumn = false,
                    ShowCorpColumn = true,
                    ShowKillsColumn = false,
                    ShowLossesColumn = true,
                    ShowAvgFleetSizeColumn = false,
                    ShowLastShipSeenColumn = true,
                    ShowLastSeenColumn = false,
                    ShowCynoHullSeenColumn = true
                };

                controller.ApplyBoardColumnVisibility(settings);

                Assert.Equal(Visibility.Collapsed, columns["SigColumn"].Visibility);
                Assert.Equal(Visibility.Visible, columns["CharacterColumn"].Visibility);
                Assert.Equal(Visibility.Collapsed, columns["AllianceColumn"].Visibility);
                Assert.Equal(Visibility.Visible, columns["CorpColumn"].Visibility);
                Assert.Equal(Visibility.Collapsed, columns["KillsColumn"].Visibility);
                Assert.Equal(Visibility.Visible, columns["LossesColumn"].Visibility);
                Assert.Equal(Visibility.Collapsed, columns["AvgFleetSizeColumn"].Visibility);
                Assert.Equal(Visibility.Visible, columns["LastShipSeenColumn"].Visibility);
                Assert.Equal(Visibility.Collapsed, columns["LastSeenColumn"].Visibility);
                Assert.Equal(Visibility.Visible, columns["CynoHullSeenColumn"].Visibility);
            });
        }

        [Fact]
        public void ApplyBoardColumnSettingsToCheckBoxes_TreatsNullVisibilitySettingsAsEnabled()
        {
            RunOnStaThread(() =>
            {
                var (controller, _) = CreateInitializedController();
                var settings = new AppSettings
                {
                    ShowSigColumn = null,
                    ShowAllianceColumn = true,
                    ShowCorpColumn = false,
                    ShowKillsColumn = null,
                    ShowLossesColumn = true,
                    ShowAvgFleetSizeColumn = false,
                    ShowLastShipSeenColumn = true,
                    ShowLastSeenColumn = false,
                    ShowCynoHullSeenColumn = null,
                    ShowCorpAllianceCounts = true
                };

                var sig = new CheckBox();
                var alliance = new CheckBox();
                var corp = new CheckBox();
                var kills = new CheckBox();
                var losses = new CheckBox();
                var avgFleet = new CheckBox();
                var lastShip = new CheckBox();
                var lastSeen = new CheckBox();
                var cynoHull = new CheckBox();
                var corpAllianceCounts = new CheckBox();

                controller.ApplyBoardColumnSettingsToCheckBoxes(
                    settings,
                    sig,
                    alliance,
                    corp,
                    kills,
                    losses,
                    avgFleet,
                    lastShip,
                    lastSeen,
                    cynoHull,
                    corpAllianceCounts);

                Assert.True(sig.IsChecked);
                Assert.True(alliance.IsChecked);
                Assert.False(corp.IsChecked);
                Assert.True(kills.IsChecked);
                Assert.True(losses.IsChecked);
                Assert.False(avgFleet.IsChecked);
                Assert.True(lastShip.IsChecked);
                Assert.False(lastSeen.IsChecked);
                Assert.True(cynoHull.IsChecked);
                Assert.True(corpAllianceCounts.IsChecked);
            });
        }

        [Fact]
        public void SaveBoardColumnSettingsFromCheckBoxes_PersistsCheckBoxValues()
        {
            RunOnStaThread(() =>
            {
                var (controller, _) = CreateInitializedController();
                var settings = new AppSettings();

                controller.SaveBoardColumnSettingsFromCheckBoxes(
                    settings,
                    new CheckBox { IsChecked = false },
                    new CheckBox { IsChecked = true },
                    new CheckBox { IsChecked = false },
                    new CheckBox { IsChecked = true },
                    new CheckBox { IsChecked = false },
                    new CheckBox { IsChecked = true },
                    new CheckBox { IsChecked = false },
                    new CheckBox { IsChecked = true },
                    new CheckBox { IsChecked = false });

                Assert.False(settings.ShowSigColumn);
                Assert.True(settings.ShowAllianceColumn);
                Assert.False(settings.ShowCorpColumn);
                Assert.True(settings.ShowKillsColumn);
                Assert.False(settings.ShowLossesColumn);
                Assert.True(settings.ShowAvgFleetSizeColumn);
                Assert.False(settings.ShowLastShipSeenColumn);
                Assert.True(settings.ShowLastSeenColumn);
                Assert.False(settings.ShowCynoHullSeenColumn);
            });
        }

        [Fact]
        public void TryValidateSavedBoardColumnLayout_FillsMissingCanonicalColumns()
        {
            RunOnStaThread(() =>
            {
                var (controller, _) = CreateInitializedController();
                var partialLayout = new List<BoardColumnLayoutSetting>
                {
                    new()
                    {
                        ColumnKey = "SigColumn",
                        DisplayIndex = 0,
                        WidthValue = 42,
                        WidthUnitType = DataGridLengthUnitType.Pixel.ToString()
                    },
                    new()
                    {
                        ColumnKey = "CharacterColumn",
                        DisplayIndex = 1,
                        WidthValue = 120,
                        WidthUnitType = DataGridLengthUnitType.Pixel.ToString()
                    }
                };

                var result = controller.TryValidateSavedBoardColumnLayout(
                    partialLayout,
                    out var validLayout,
                    out var failureReason);

                Assert.True(result, failureReason);
                Assert.Equal(10, validLayout.Count);
                Assert.Equal("SigColumn", validLayout[0].ColumnKey);
                Assert.Contains(validLayout, setting => string.Equals(setting.ColumnKey, "CynoHullSeenColumn", StringComparison.Ordinal));
            });
        }

        [Fact]
        public void TryValidateSavedBoardColumnLayout_RejectsDuplicateDisplayIndexes()
        {
            RunOnStaThread(() =>
            {
                var (controller, _) = CreateInitializedController();
                var invalidLayout = new List<BoardColumnLayoutSetting>
                {
                    new()
                    {
                        ColumnKey = "SigColumn",
                        DisplayIndex = 0,
                        WidthValue = 42,
                        WidthUnitType = DataGridLengthUnitType.Pixel.ToString()
                    },
                    new()
                    {
                        ColumnKey = "CharacterColumn",
                        DisplayIndex = 0,
                        WidthValue = 120,
                        WidthUnitType = DataGridLengthUnitType.Pixel.ToString()
                    }
                };

                var result = controller.TryValidateSavedBoardColumnLayout(
                    invalidLayout,
                    out _,
                    out var failureReason);

                Assert.False(result);
                Assert.Contains("Duplicate DisplayIndex", failureReason, StringComparison.Ordinal);
            });
        }

        [Fact]
        public void TryValidateSavedBoardColumnLayout_RejectsUnknownOnlyLayouts()
        {
            RunOnStaThread(() =>
            {
                var (controller, _) = CreateInitializedController();
                var invalidLayout = new List<BoardColumnLayoutSetting>
                {
                    new()
                    {
                        ColumnKey = "UnknownColumn",
                        DisplayIndex = 0,
                        WidthValue = 120,
                        WidthUnitType = DataGridLengthUnitType.Pixel.ToString()
                    }
                };

                var result = controller.TryValidateSavedBoardColumnLayout(
                    invalidLayout,
                    out _,
                    out var failureReason);

                Assert.False(result);
                Assert.Contains("No known board column keys", failureReason, StringComparison.Ordinal);
            });
        }

        [Fact]
        public void TryValidateSavedBoardColumnLayout_RejectsWidthBelowMinimum()
        {
            RunOnStaThread(() =>
            {
                var (controller, _) = CreateInitializedController();
                var invalidLayout = new List<BoardColumnLayoutSetting>
                {
                    new()
                    {
                        ColumnKey = "SigColumn",
                        DisplayIndex = 0,
                        WidthValue = 1,
                        WidthUnitType = DataGridLengthUnitType.Pixel.ToString()
                    }
                };

                var result = controller.TryValidateSavedBoardColumnLayout(
                    invalidLayout,
                    out _,
                    out var failureReason);

                Assert.False(result);
                Assert.Contains("Width below minimum", failureReason, StringComparison.Ordinal);
            });
        }

        [Fact]
        public void BoardColumnLayoutsMatch_AllowsSmallWidthDifferences()
        {
            RunOnStaThread(() =>
            {
                var (controller, _) = CreateInitializedController();

                var left = new List<BoardColumnLayoutSetting>
                {
                    new()
                    {
                        ColumnKey = "SigColumn",
                        DisplayIndex = 0,
                        WidthValue = 42.000,
                        WidthUnitType = DataGridLengthUnitType.Pixel.ToString()
                    }
                };

                var right = new List<BoardColumnLayoutSetting>
                {
                    new()
                    {
                        ColumnKey = "sigcolumn",
                        DisplayIndex = 0,
                        WidthValue = 42.005,
                        WidthUnitType = DataGridLengthUnitType.Pixel.ToString()
                    }
                };

                Assert.True(controller.BoardColumnLayoutsMatch(left, right));
            });
        }

        [Fact]
        public void SetAllOptionalBoardColumnSettings_UpdatesEveryOptionalColumnSetting()
        {
            var controller = new BoardColumnLayoutController();
            var settings = new AppSettings();

            controller.SetAllOptionalBoardColumnSettings(settings, false);

            Assert.False(settings.ShowSigColumn);
            Assert.False(settings.ShowAllianceColumn);
            Assert.False(settings.ShowCorpColumn);
            Assert.False(settings.ShowKillsColumn);
            Assert.False(settings.ShowLossesColumn);
            Assert.False(settings.ShowAvgFleetSizeColumn);
            Assert.False(settings.ShowLastShipSeenColumn);
            Assert.False(settings.ShowLastSeenColumn);
            Assert.False(settings.ShowCynoHullSeenColumn);
        }

        private static (BoardColumnLayoutController Controller, Dictionary<string, DataGridColumn> Columns) CreateInitializedController()
        {
            var columns = new Dictionary<string, DataGridColumn>(StringComparer.OrdinalIgnoreCase)
            {
                ["SigColumn"] = new DataGridTextColumn { Header = "Sig" },
                ["CharacterColumn"] = new DataGridTextColumn { Header = "Character" },
                ["AllianceColumn"] = new DataGridTextColumn { Header = "Alliance" },
                ["CorpColumn"] = new DataGridTextColumn { Header = "Corp" },
                ["KillsColumn"] = new DataGridTextColumn { Header = "Kills" },
                ["LossesColumn"] = new DataGridTextColumn { Header = "Losses" },
                ["AvgFleetSizeColumn"] = new DataGridTextColumn { Header = "Avg Fleet" },
                ["LastShipSeenColumn"] = new DataGridTextColumn { Header = "Last Ship" },
                ["LastSeenColumn"] = new DataGridTextColumn { Header = "Last Seen" },
                ["CynoHullSeenColumn"] = new DataGridTextColumn { Header = "Cyno Hull" }
            };

            var grid = new DataGrid();
            foreach (var column in columns.Values)
            {
                grid.Columns.Add(column);
            }

            var controller = new BoardColumnLayoutController();
            controller.InitializeColumns(columns.Select(pair => (pair.Key, pair.Value)).ToArray());
            controller.ApplyColumnMinimumWidths();
            controller.BuildCanonicalBoardColumnLayout();

            return (controller, columns);
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
