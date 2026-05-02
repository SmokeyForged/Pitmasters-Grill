using PitmastersGrill.Models;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PitmastersGrill.Services
{
    public sealed class BoardDisplaySettingsController
    {
        public void ApplySettingsToControls(
            AppSettings settings,
            CheckBox? showBoardGridLinesCheckBox,
            ComboBox? boardTextSizeComboBox,
            ComboBox? boardFontFamilyComboBox)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (showBoardGridLinesCheckBox != null)
            {
                showBoardGridLinesCheckBox.IsChecked = settings.ShowBoardGridLines;
            }

            if (boardTextSizeComboBox != null)
            {
                boardTextSizeComboBox.SelectedIndex = Math.Max(0, Math.Min(6, settings.BoardTextSize - 10));
            }

            if (boardFontFamilyComboBox != null)
            {
                boardFontFamilyComboBox.SelectedIndex = GetFontFamilySelectedIndex(settings.BoardFontFamily);
            }
        }

        public void ApplySettingsToBoard(
            AppSettings settings,
            DataGrid? pilotBoard,
            ResourceDictionary resources)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (resources == null)
            {
                throw new ArgumentNullException(nameof(resources));
            }

            if (pilotBoard == null)
            {
                return;
            }

            var showGridLines = settings.ShowBoardGridLines;
            pilotBoard.GridLinesVisibility = showGridLines ? DataGridGridLinesVisibility.All : DataGridGridLinesVisibility.None;
            pilotBoard.HorizontalGridLinesBrush = showGridLines
                ? (Brush)resources["GridLineBrush"]
                : Brushes.Transparent;
            pilotBoard.VerticalGridLinesBrush = showGridLines
                ? (Brush)resources["GridLineBrush"]
                : Brushes.Transparent;
            pilotBoard.ColumnHeaderStyle = (Style)resources[showGridLines
                ? "PilotBoardColumnHeaderStyle"
                : "PilotBoardColumnHeaderNoGridStyle"];
            pilotBoard.CellStyle = (Style)resources[showGridLines
                ? "PilotBoardCellStyle"
                : "PilotBoardCellNoGridStyle"];
            pilotBoard.FontSize = settings.BoardTextSize;
            pilotBoard.FontFamily = string.IsNullOrWhiteSpace(settings.BoardFontFamily)
                ? SystemFonts.MessageFontFamily
                : new FontFamily(settings.BoardFontFamily);
        }

        public void SetShowBoardGridLines(AppSettings settings, bool enabled)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.ShowBoardGridLines = enabled;
        }

        public void SetBoardTextSize(AppSettings settings, int selectedIndex)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.BoardTextSize = selectedIndex + 10;
        }

        public void SetBoardFontFamily(AppSettings settings, int selectedIndex)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.BoardFontFamily = selectedIndex switch
            {
                1 => "Segoe UI",
                2 => "Consolas",
                3 => "Bahnschrift",
                _ => string.Empty
            };
        }

        private static int GetFontFamilySelectedIndex(string? boardFontFamily)
        {
            if (string.IsNullOrWhiteSpace(boardFontFamily))
            {
                return 0;
            }

            if (string.Equals(boardFontFamily, "Consolas", StringComparison.Ordinal))
            {
                return 2;
            }

            if (string.Equals(boardFontFamily, "Bahnschrift", StringComparison.Ordinal))
            {
                return 3;
            }

            return 1;
        }
    }
}
