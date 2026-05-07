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
    public sealed class BoardColumnLayoutPersistenceControllerTests
    {
        [Fact]
        public void ApplySavedBoardColumnLayout_WhenInvalid_ClearsSettingsSavesAndFallsBackToCanonical()
        {
            RunOnStaThread(() =>
            {
                var saves = new List<AppSettings>();
                var appliedReasons = new List<string>();
                var canonicalReasons = new List<string>();
                var (layoutController, _) = CreateInitializedLayoutController();
                var controller = new BoardColumnLayoutPersistenceController(layoutController, saves.Add);
                var settings = new AppSettings
                {
                    BoardColumnLayout = new List<BoardColumnLayoutSetting>
                    {
                        new() { ColumnKey = "SigColumn", DisplayIndex = 0, WidthValue = 1, WidthUnitType = "Pixel" }
                    }
                };

                controller.ApplySavedBoardColumnLayout(
                    settings,
                    (_, reason) => appliedReasons.Add(reason),
                    canonicalReasons.Add);

                Assert.Empty(settings.BoardColumnLayout);
                Assert.Single(saves);
                Assert.Empty(appliedReasons);
                Assert.Equal("Discard invalid saved board layout", canonicalReasons[0]);
            });
        }

        [Fact]
        public void TryQueueBoardColumnLayoutSave_WhenReady_CapturesReason()
        {
            RunOnStaThread(() =>
            {
                var (layoutController, _) = CreateInitializedLayoutController();
                var controller = new BoardColumnLayoutPersistenceController(layoutController, _ => { });

                controller.MarkBoardColumnLayoutReady();
                var queued = controller.TryQueueBoardColumnLayoutSave(
                    isApplyingSettings: false,
                    isBoardLayoutHostReady: () => true,
                    reason: "Column width changed");

                Assert.True(queued);
                Assert.Equal("Column width changed", controller.DequeuePendingBoardColumnLayoutSaveReason());
            });
        }

        [Fact]
        public void SaveCurrentBoardColumnLayout_WhenLayoutChanged_SavesSanitizedLayout()
        {
            RunOnStaThread(() =>
            {
                var saves = new List<AppSettings>();
                var (layoutController, columns) = CreateInitializedLayoutController();
                var controller = new BoardColumnLayoutPersistenceController(layoutController, saves.Add);
                var settings = new AppSettings();

                columns["SigColumn"].Width = new DataGridLength(60, DataGridLengthUnitType.Pixel);
                columns["CharacterColumn"].Width = new DataGridLength(140, DataGridLengthUnitType.Pixel);
                controller.MarkBoardColumnLayoutReady();
                controller.SaveCurrentBoardColumnLayout(settings, () => true, "Test save");

                Assert.Single(saves);
                Assert.NotEmpty(settings.BoardColumnLayout);
                Assert.Contains(settings.BoardColumnLayout, entry => entry.ColumnKey == "SigColumn" && entry.WidthValue >= 60d);
            });
        }

        [Fact]
        public void FitVisibleBoardColumnsToWidth_WhenWidthIsTight_ShrinksColumnsAboveMinimum()
        {
            RunOnStaThread(() =>
            {
                var (layoutController, columns) = CreateInitializedLayoutController();
                var controller = new BoardColumnLayoutPersistenceController(layoutController, _ => { });

                columns["SigColumn"].Width = new DataGridLength(80, DataGridLengthUnitType.Pixel);
                columns["CharacterColumn"].Width = new DataGridLength(200, DataGridLengthUnitType.Pixel);
                columns["AllianceColumn"].Width = new DataGridLength(180, DataGridLengthUnitType.Pixel);
                columns["CorpColumn"].Visibility = Visibility.Collapsed;
                columns["KillsColumn"].Visibility = Visibility.Collapsed;
                columns["LossesColumn"].Visibility = Visibility.Collapsed;
                columns["AvgFleetSizeColumn"].Visibility = Visibility.Collapsed;
                columns["LastShipSeenColumn"].Visibility = Visibility.Collapsed;
                columns["LastSeenColumn"].Visibility = Visibility.Collapsed;
                columns["CynoHullSeenColumn"].Visibility = Visibility.Collapsed;

                controller.FitVisibleBoardColumnsToWidth(220d);

                var visibleColumns = new[]
                {
                    columns["SigColumn"],
                    columns["CharacterColumn"],
                    columns["AllianceColumn"]
                };

                var totalWidth = visibleColumns.Sum(column => column.Width.DisplayValue);
                Assert.True(totalWidth <= 221d);
                Assert.True(columns["SigColumn"].Width.DisplayValue >= layoutController.GetBoardColumnMinimumWidth("SigColumn"));
                Assert.True(columns["CharacterColumn"].Width.DisplayValue >= layoutController.GetBoardColumnMinimumWidth("CharacterColumn"));
                Assert.True(columns["AllianceColumn"].Width.DisplayValue >= layoutController.GetBoardColumnMinimumWidth("AllianceColumn"));
            });
        }

        private static (BoardColumnLayoutController Controller, Dictionary<string, DataGridColumn> Columns) CreateInitializedLayoutController()
        {
            var columns = new Dictionary<string, DataGridColumn>(StringComparer.OrdinalIgnoreCase)
            {
                ["SigColumn"] = new DataGridTextColumn { Width = new DataGridLength(42, DataGridLengthUnitType.Pixel), DisplayIndex = 0 },
                ["CharacterColumn"] = new DataGridTextColumn { Width = new DataGridLength(120, DataGridLengthUnitType.Pixel), DisplayIndex = 1 },
                ["AllianceColumn"] = new DataGridTextColumn { Width = new DataGridLength(120, DataGridLengthUnitType.Pixel), DisplayIndex = 2 },
                ["CorpColumn"] = new DataGridTextColumn { Width = new DataGridLength(140, DataGridLengthUnitType.Pixel), DisplayIndex = 3 },
                ["KillsColumn"] = new DataGridTextColumn { Width = new DataGridLength(55, DataGridLengthUnitType.Pixel), DisplayIndex = 4 },
                ["LossesColumn"] = new DataGridTextColumn { Width = new DataGridLength(55, DataGridLengthUnitType.Pixel), DisplayIndex = 5 },
                ["AvgFleetSizeColumn"] = new DataGridTextColumn { Width = new DataGridLength(70, DataGridLengthUnitType.Pixel), DisplayIndex = 6 },
                ["LastShipSeenColumn"] = new DataGridTextColumn { Width = new DataGridLength(100, DataGridLengthUnitType.Pixel), DisplayIndex = 7 },
                ["LastSeenColumn"] = new DataGridTextColumn { Width = new DataGridLength(90, DataGridLengthUnitType.Pixel), DisplayIndex = 8 },
                ["CynoHullSeenColumn"] = new DataGridTextColumn { Width = new DataGridLength(110, DataGridLengthUnitType.Pixel), DisplayIndex = 9 }
            };

            var controller = new BoardColumnLayoutController();
            controller.InitializeColumns(
                ("SigColumn", columns["SigColumn"]),
                ("CharacterColumn", columns["CharacterColumn"]),
                ("AllianceColumn", columns["AllianceColumn"]),
                ("CorpColumn", columns["CorpColumn"]),
                ("KillsColumn", columns["KillsColumn"]),
                ("LossesColumn", columns["LossesColumn"]),
                ("AvgFleetSizeColumn", columns["AvgFleetSizeColumn"]),
                ("LastShipSeenColumn", columns["LastShipSeenColumn"]),
                ("LastSeenColumn", columns["LastSeenColumn"]),
                ("CynoHullSeenColumn", columns["CynoHullSeenColumn"]));
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
