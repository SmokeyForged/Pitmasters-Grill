using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public class BoardDisplaySettingsControllerTests
    {
        [Fact]
        public void ApplySettingsToControls_SynchronizesGridTextAndFontControls()
        {
            RunOnStaThread(() =>
            {
                var controller = new BoardDisplaySettingsController();
                var settings = new AppSettings
                {
                    ShowBoardGridLines = false,
                    BoardTextSize = 14,
                    BoardFontFamily = "Consolas"
                };

                var gridLines = new CheckBox();
                var textSize = CreateBoardTextSizeComboBox();
                var fontFamily = CreateBoardFontFamilyComboBox();

                controller.ApplySettingsToControls(settings, gridLines, textSize, fontFamily);

                Assert.False(gridLines.IsChecked);
                Assert.Equal(4, textSize.SelectedIndex);
                Assert.Equal(2, fontFamily.SelectedIndex);
            });
        }

        [Theory]
        [InlineData("", 0)]
        [InlineData("Segoe UI", 1)]
        [InlineData("Consolas", 2)]
        [InlineData("Bahnschrift", 3)]
        [InlineData("Unexpected Font", 1)]
        public void ApplySettingsToControls_MapsFontFamilyToExpectedSelectedIndex(string fontFamilyName, int expectedIndex)
        {
            RunOnStaThread(() =>
            {
                var controller = new BoardDisplaySettingsController();
                var settings = new AppSettings { BoardFontFamily = fontFamilyName };
                var comboBox = CreateBoardFontFamilyComboBox();

                controller.ApplySettingsToControls(settings, null, null, comboBox);

                Assert.Equal(expectedIndex, comboBox.SelectedIndex);
            });
        }

        [Theory]
        [InlineData(4, 0)]
        [InlineData(10, 0)]
        [InlineData(12, 2)]
        [InlineData(16, 6)]
        [InlineData(99, 6)]
        public void ApplySettingsToControls_ClampsBoardTextSizeSelectedIndex(int boardTextSize, int expectedIndex)
        {
            RunOnStaThread(() =>
            {
                var controller = new BoardDisplaySettingsController();
                var settings = new AppSettings { BoardTextSize = boardTextSize };
                var comboBox = CreateBoardTextSizeComboBox();

                controller.ApplySettingsToControls(settings, null, comboBox, null);

                Assert.Equal(expectedIndex, comboBox.SelectedIndex);
            });
        }

        [Fact]
        public void ApplySettingsToBoard_AppliesGridlineStylesAndFontSettings()
        {
            RunOnStaThread(() =>
            {
                var controller = new BoardDisplaySettingsController();
                var settings = new AppSettings
                {
                    ShowBoardGridLines = true,
                    BoardTextSize = 15,
                    BoardFontFamily = "Consolas"
                };

                var board = new DataGrid();
                var resources = CreateBoardResourceDictionary();

                controller.ApplySettingsToBoard(settings, board, resources);

                Assert.Equal(DataGridGridLinesVisibility.All, board.GridLinesVisibility);
                Assert.Same(resources["GridLineBrush"], board.HorizontalGridLinesBrush);
                Assert.Same(resources["GridLineBrush"], board.VerticalGridLinesBrush);
                Assert.Same(resources["PilotBoardColumnHeaderStyle"], board.ColumnHeaderStyle);
                Assert.Same(resources["PilotBoardCellStyle"], board.CellStyle);
                Assert.Equal(15, board.FontSize);
                Assert.Equal("Consolas", board.FontFamily.Source);
            });
        }

        [Fact]
        public void ApplySettingsToBoard_AppliesNoGridlineStylesWhenDisabled()
        {
            RunOnStaThread(() =>
            {
                var controller = new BoardDisplaySettingsController();
                var settings = new AppSettings
                {
                    ShowBoardGridLines = false,
                    BoardTextSize = 11,
                    BoardFontFamily = string.Empty
                };

                var board = new DataGrid();
                var resources = CreateBoardResourceDictionary();

                controller.ApplySettingsToBoard(settings, board, resources);

                Assert.Equal(DataGridGridLinesVisibility.None, board.GridLinesVisibility);
                Assert.Equal(Brushes.Transparent, board.HorizontalGridLinesBrush);
                Assert.Equal(Brushes.Transparent, board.VerticalGridLinesBrush);
                Assert.Same(resources["PilotBoardColumnHeaderNoGridStyle"], board.ColumnHeaderStyle);
                Assert.Same(resources["PilotBoardCellNoGridStyle"], board.CellStyle);
                Assert.Equal(11, board.FontSize);
                Assert.Equal(SystemFonts.MessageFontFamily.Source, board.FontFamily.Source);
            });
        }

        [Fact]
        public void ApplySettingsToBoard_AllowsNullBoard()
        {
            RunOnStaThread(() =>
            {
                var controller = new BoardDisplaySettingsController();

                controller.ApplySettingsToBoard(new AppSettings(), null, CreateBoardResourceDictionary());
            });
        }

        [Fact]
        public void SetShowBoardGridLines_UpdatesSetting()
        {
            var controller = new BoardDisplaySettingsController();
            var settings = new AppSettings { ShowBoardGridLines = true };

            controller.SetShowBoardGridLines(settings, false);

            Assert.False(settings.ShowBoardGridLines);
        }

        [Theory]
        [InlineData(0, 10)]
        [InlineData(2, 12)]
        [InlineData(6, 16)]
        public void SetBoardTextSize_ConvertsSelectedIndexToTextSize(int selectedIndex, int expectedTextSize)
        {
            var controller = new BoardDisplaySettingsController();
            var settings = new AppSettings();

            controller.SetBoardTextSize(settings, selectedIndex);

            Assert.Equal(expectedTextSize, settings.BoardTextSize);
        }

        [Theory]
        [InlineData(0, "")]
        [InlineData(1, "Segoe UI")]
        [InlineData(2, "Consolas")]
        [InlineData(3, "Bahnschrift")]
        [InlineData(99, "")]
        public void SetBoardFontFamily_MapsSelectedIndexToSavedFontFamily(int selectedIndex, string expectedFontFamily)
        {
            var controller = new BoardDisplaySettingsController();
            var settings = new AppSettings();

            controller.SetBoardFontFamily(settings, selectedIndex);

            Assert.Equal(expectedFontFamily, settings.BoardFontFamily);
        }

        private static ComboBox CreateBoardTextSizeComboBox()
        {
            var comboBox = new ComboBox();

            for (var textSize = 10; textSize <= 16; textSize++)
            {
                comboBox.Items.Add(textSize.ToString());
            }

            return comboBox;
        }

        private static ComboBox CreateBoardFontFamilyComboBox()
        {
            var comboBox = new ComboBox();
            comboBox.Items.Add("System Default");
            comboBox.Items.Add("Segoe UI");
            comboBox.Items.Add("Consolas");
            comboBox.Items.Add("Bahnschrift");
            return comboBox;
        }

        private static ResourceDictionary CreateBoardResourceDictionary()
        {
            return new ResourceDictionary
            {
                ["GridLineBrush"] = Brushes.DimGray,
                ["PilotBoardColumnHeaderStyle"] = new Style(typeof(DataGridColumnHeader)),
                ["PilotBoardColumnHeaderNoGridStyle"] = new Style(typeof(DataGridColumnHeader)),
                ["PilotBoardCellStyle"] = new Style(typeof(DataGridCell)),
                ["PilotBoardCellNoGridStyle"] = new Style(typeof(DataGridCell))
            };
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
