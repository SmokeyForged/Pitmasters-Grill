using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Collections.Generic;
using System.Windows.Controls;

namespace PitmastersGrill.Services
{
    public sealed class BoardColumnSettingsController
    {
        private readonly BoardColumnLayoutController _boardColumnLayoutController;
        private readonly Action<AppSettings> _saveSettings;

        public BoardColumnSettingsController(
            BoardColumnLayoutController boardColumnLayoutController,
            Action<AppSettings> saveSettings)
        {
            _boardColumnLayoutController = boardColumnLayoutController ?? throw new ArgumentNullException(nameof(boardColumnLayoutController));
            _saveSettings = saveSettings ?? throw new ArgumentNullException(nameof(saveSettings));
        }

        public void InitializeBoardColumnLayoutUi(
            Action<IEnumerable<BoardColumnLayoutSetting>, string> applyBoardColumnLayout,
            params (string Key, DataGridColumn Column)[] columns)
        {
            if (applyBoardColumnLayout == null)
            {
                throw new ArgumentNullException(nameof(applyBoardColumnLayout));
            }

            _boardColumnLayoutController.InitializeColumns(columns);
            _boardColumnLayoutController.ApplyColumnMinimumWidths();
            _boardColumnLayoutController.BuildCanonicalBoardColumnLayout();
            applyBoardColumnLayout(_boardColumnLayoutController.GetCanonicalBoardColumnLayout(), "Apply canonical default board layout");
        }

        public void ApplyBoardColumnSettingsToCheckBoxes(
            AppSettings settings,
            CheckBox showSigColumnCheckBox,
            CheckBox showAllianceColumnCheckBox,
            CheckBox showCorpColumnCheckBox,
            CheckBox showKillsColumnCheckBox,
            CheckBox showLossesColumnCheckBox,
            CheckBox showAvgFleetSizeColumnCheckBox,
            CheckBox showLastShipSeenColumnCheckBox,
            CheckBox showLastSeenColumnCheckBox,
            CheckBox showCynoHullSeenColumnCheckBox,
            CheckBox? showCorpAllianceCountsCheckBox)
        {
            _boardColumnLayoutController.ApplyBoardColumnSettingsToCheckBoxes(
                settings,
                showSigColumnCheckBox,
                showAllianceColumnCheckBox,
                showCorpColumnCheckBox,
                showKillsColumnCheckBox,
                showLossesColumnCheckBox,
                showAvgFleetSizeColumnCheckBox,
                showLastShipSeenColumnCheckBox,
                showLastSeenColumnCheckBox,
                showCynoHullSeenColumnCheckBox,
                showCorpAllianceCountsCheckBox);
        }

        public void SaveBoardColumnSettingsFromCheckBoxes(
            AppSettings settings,
            CheckBox showSigColumnCheckBox,
            CheckBox showAllianceColumnCheckBox,
            CheckBox showCorpColumnCheckBox,
            CheckBox showKillsColumnCheckBox,
            CheckBox showLossesColumnCheckBox,
            CheckBox showAvgFleetSizeColumnCheckBox,
            CheckBox showLastShipSeenColumnCheckBox,
            CheckBox showLastSeenColumnCheckBox,
            CheckBox showCynoHullSeenColumnCheckBox)
        {
            _boardColumnLayoutController.SaveBoardColumnSettingsFromCheckBoxes(
                settings,
                showSigColumnCheckBox,
                showAllianceColumnCheckBox,
                showCorpColumnCheckBox,
                showKillsColumnCheckBox,
                showLossesColumnCheckBox,
                showAvgFleetSizeColumnCheckBox,
                showLastShipSeenColumnCheckBox,
                showLastSeenColumnCheckBox,
                showCynoHullSeenColumnCheckBox);
        }

        public void ApplyBoardColumnVisibility(AppSettings settings)
        {
            _boardColumnLayoutController.ApplyBoardColumnVisibility(settings);
        }

        public void HandleBoardColumnVisibilityChanged(
            bool isApplyingSettings,
            AppSettings settings,
            CheckBox showSigColumnCheckBox,
            CheckBox showAllianceColumnCheckBox,
            CheckBox showCorpColumnCheckBox,
            CheckBox showKillsColumnCheckBox,
            CheckBox showLossesColumnCheckBox,
            CheckBox showAvgFleetSizeColumnCheckBox,
            CheckBox showLastShipSeenColumnCheckBox,
            CheckBox showLastSeenColumnCheckBox,
            CheckBox showCynoHullSeenColumnCheckBox,
            Action applyBoardColumnVisibility)
        {
            if (isApplyingSettings)
            {
                return;
            }

            SaveBoardColumnSettingsFromCheckBoxes(
                settings,
                showSigColumnCheckBox,
                showAllianceColumnCheckBox,
                showCorpColumnCheckBox,
                showKillsColumnCheckBox,
                showLossesColumnCheckBox,
                showAvgFleetSizeColumnCheckBox,
                showLastShipSeenColumnCheckBox,
                showLastSeenColumnCheckBox,
                showCynoHullSeenColumnCheckBox);
            applyBoardColumnVisibility();
            _saveSettings(settings);

            AppLogger.UiInfo(
                $"Board column visibility changed. sig={IsChecked(showSigColumnCheckBox)} alliance={IsChecked(showAllianceColumnCheckBox)} corp={IsChecked(showCorpColumnCheckBox)} kills={IsChecked(showKillsColumnCheckBox)} losses={IsChecked(showLossesColumnCheckBox)} avgFleet={IsChecked(showAvgFleetSizeColumnCheckBox)} lastShip={IsChecked(showLastShipSeenColumnCheckBox)} lastSeen={IsChecked(showLastSeenColumnCheckBox)} cynoHull={IsChecked(showCynoHullSeenColumnCheckBox)}");
        }

        public void HandleShowCorpAllianceCountsChanged(
            bool isApplyingSettings,
            AppSettings settings,
            bool enabled,
            Action recomputeCorpAllianceCounts)
        {
            if (isApplyingSettings)
            {
                return;
            }

            settings.ShowCorpAllianceCounts = enabled;
            recomputeCorpAllianceCounts();
            _saveSettings(settings);

            AppLogger.UiInfo($"Corp/alliance board counts changed. enabled={settings.ShowCorpAllianceCounts}");
        }

        public void HandleShowAllBoardColumns(
            AppSettings settings,
            Action applyBoardColumnSettingsToCheckBoxes,
            Action applyBoardColumnVisibility)
        {
            _boardColumnLayoutController.SetAllOptionalBoardColumnSettings(settings, true);
            applyBoardColumnSettingsToCheckBoxes();
            applyBoardColumnVisibility();
            _saveSettings(settings);

            AppLogger.UiInfo("Board column visibility reset to show all optional columns.");
        }

        public void HandleResetBoardColumns(
            AppSettings settings,
            Action applyBoardColumnSettingsToCheckBoxes,
            Action applyBoardColumnVisibility)
        {
            _boardColumnLayoutController.SetAllOptionalBoardColumnSettings(settings, true);
            applyBoardColumnSettingsToCheckBoxes();
            applyBoardColumnVisibility();
            _saveSettings(settings);

            AppLogger.UiInfo("Board column visibility reset to defaults.");
        }

        public void HandleResetBoardLayout(
            AppSettings settings,
            Action<string> applyCanonicalBoardColumnLayout,
            Action<string> saveCurrentBoardColumnLayout)
        {
            settings.BoardColumnLayout.Clear();
            applyCanonicalBoardColumnLayout("Reset board layout to canonical defaults");
            saveCurrentBoardColumnLayout("Reset layout");

            AppLogger.UiInfo("Board column layout reset to canonical defaults.");
        }

        private static bool IsChecked(CheckBox checkBox)
        {
            return checkBox.IsChecked == true;
        }
    }
}
