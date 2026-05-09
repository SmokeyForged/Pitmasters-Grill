using PitmastersGrill.Diagnostics;
using PitmastersGrill.Models;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

using PitmastersGrill.Persistence;
namespace PitmastersGrill.Services
{
    public sealed class BoardLayoutSurface
    {
        private readonly BoardDisplaySettingsController _boardDisplaySettingsController;
        private readonly BoardColumnLayoutController _boardColumnLayoutController;
        private readonly BoardColumnSettingsController _boardColumnSettingsController;
        private readonly BoardColumnLayoutPersistenceController _boardColumnLayoutPersistenceController;
        private readonly MainWindowSettingsCoordinator _mainWindowSettingsCoordinator;
        private readonly DispatcherTimer _boardColumnLayoutSaveTimer;
        private readonly Dispatcher _dispatcher;
        private readonly Func<AppSettings> _getSettings;
        private readonly Func<bool> _isApplyingSettings;
        private readonly Action<bool> _setApplyingSettings;
        private readonly Func<bool> _isLoaded;
        private readonly Action _updateWindowMinimumSize;
        private readonly Action _recomputeCorpAllianceCounts;
        private readonly DataGrid _pilotBoard;
        private readonly ResourceDictionary _resources;
        private readonly CheckBox _showBoardGridLinesCheckBox;
        private readonly ComboBox _boardTextSizeComboBox;
        private readonly ComboBox _boardFontFamilyComboBox;
        private readonly CheckBox _showSigColumnCheckBox;
        private readonly CheckBox _showAllianceColumnCheckBox;
        private readonly CheckBox _showCorpColumnCheckBox;
        private readonly CheckBox _showKillsColumnCheckBox;
        private readonly CheckBox _showLossesColumnCheckBox;
        private readonly CheckBox _showAvgFleetSizeColumnCheckBox;
        private readonly CheckBox _showLastShipSeenColumnCheckBox;
        private readonly CheckBox _showLastSeenColumnCheckBox;
        private readonly CheckBox _showCynoHullSeenColumnCheckBox;
        private readonly CheckBox _showCorpAllianceCountsCheckBox;
        private readonly DataGridColumn _sigColumn;
        private readonly DataGridColumn _characterColumn;
        private readonly DataGridColumn _allianceColumn;
        private readonly DataGridColumn _corpColumn;
        private readonly DataGridColumn _killsColumn;
        private readonly DataGridColumn _lossesColumn;
        private readonly DataGridColumn _avgFleetSizeColumn;
        private readonly DataGridColumn _lastShipSeenColumn;
        private readonly DataGridColumn _lastSeenColumn;
        private readonly DataGridColumn _cynoHullSeenColumn;
        private readonly double _minimumBoardLayoutHostWidth;

        public BoardLayoutSurface(
            BoardDisplaySettingsController boardDisplaySettingsController,
            BoardColumnLayoutController boardColumnLayoutController,
            BoardColumnSettingsController boardColumnSettingsController,
            BoardColumnLayoutPersistenceController boardColumnLayoutPersistenceController,
            MainWindowSettingsCoordinator mainWindowSettingsCoordinator,
            DispatcherTimer boardColumnLayoutSaveTimer,
            Dispatcher dispatcher,
            Func<AppSettings> getSettings,
            Func<bool> isApplyingSettings,
            Action<bool> setApplyingSettings,
            Func<bool> isLoaded,
            Action updateWindowMinimumSize,
            Action recomputeCorpAllianceCounts,
            DataGrid pilotBoard,
            ResourceDictionary resources,
            CheckBox showBoardGridLinesCheckBox,
            ComboBox boardTextSizeComboBox,
            ComboBox boardFontFamilyComboBox,
            CheckBox showSigColumnCheckBox,
            CheckBox showAllianceColumnCheckBox,
            CheckBox showCorpColumnCheckBox,
            CheckBox showKillsColumnCheckBox,
            CheckBox showLossesColumnCheckBox,
            CheckBox showAvgFleetSizeColumnCheckBox,
            CheckBox showLastShipSeenColumnCheckBox,
            CheckBox showLastSeenColumnCheckBox,
            CheckBox showCynoHullSeenColumnCheckBox,
            CheckBox showCorpAllianceCountsCheckBox,
            DataGridColumn sigColumn,
            DataGridColumn characterColumn,
            DataGridColumn allianceColumn,
            DataGridColumn corpColumn,
            DataGridColumn killsColumn,
            DataGridColumn lossesColumn,
            DataGridColumn avgFleetSizeColumn,
            DataGridColumn lastShipSeenColumn,
            DataGridColumn lastSeenColumn,
            DataGridColumn cynoHullSeenColumn,
            double minimumBoardLayoutHostWidth)
        {
            _boardDisplaySettingsController = boardDisplaySettingsController ?? throw new ArgumentNullException(nameof(boardDisplaySettingsController));
            _boardColumnLayoutController = boardColumnLayoutController ?? throw new ArgumentNullException(nameof(boardColumnLayoutController));
            _boardColumnSettingsController = boardColumnSettingsController ?? throw new ArgumentNullException(nameof(boardColumnSettingsController));
            _boardColumnLayoutPersistenceController = boardColumnLayoutPersistenceController ?? throw new ArgumentNullException(nameof(boardColumnLayoutPersistenceController));
            _mainWindowSettingsCoordinator = mainWindowSettingsCoordinator ?? throw new ArgumentNullException(nameof(mainWindowSettingsCoordinator));
            _boardColumnLayoutSaveTimer = boardColumnLayoutSaveTimer ?? throw new ArgumentNullException(nameof(boardColumnLayoutSaveTimer));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _getSettings = getSettings ?? throw new ArgumentNullException(nameof(getSettings));
            _isApplyingSettings = isApplyingSettings ?? throw new ArgumentNullException(nameof(isApplyingSettings));
            _setApplyingSettings = setApplyingSettings ?? throw new ArgumentNullException(nameof(setApplyingSettings));
            _isLoaded = isLoaded ?? throw new ArgumentNullException(nameof(isLoaded));
            _updateWindowMinimumSize = updateWindowMinimumSize ?? throw new ArgumentNullException(nameof(updateWindowMinimumSize));
            _recomputeCorpAllianceCounts = recomputeCorpAllianceCounts ?? throw new ArgumentNullException(nameof(recomputeCorpAllianceCounts));
            _pilotBoard = pilotBoard ?? throw new ArgumentNullException(nameof(pilotBoard));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _showBoardGridLinesCheckBox = showBoardGridLinesCheckBox ?? throw new ArgumentNullException(nameof(showBoardGridLinesCheckBox));
            _boardTextSizeComboBox = boardTextSizeComboBox ?? throw new ArgumentNullException(nameof(boardTextSizeComboBox));
            _boardFontFamilyComboBox = boardFontFamilyComboBox ?? throw new ArgumentNullException(nameof(boardFontFamilyComboBox));
            _showSigColumnCheckBox = showSigColumnCheckBox ?? throw new ArgumentNullException(nameof(showSigColumnCheckBox));
            _showAllianceColumnCheckBox = showAllianceColumnCheckBox ?? throw new ArgumentNullException(nameof(showAllianceColumnCheckBox));
            _showCorpColumnCheckBox = showCorpColumnCheckBox ?? throw new ArgumentNullException(nameof(showCorpColumnCheckBox));
            _showKillsColumnCheckBox = showKillsColumnCheckBox ?? throw new ArgumentNullException(nameof(showKillsColumnCheckBox));
            _showLossesColumnCheckBox = showLossesColumnCheckBox ?? throw new ArgumentNullException(nameof(showLossesColumnCheckBox));
            _showAvgFleetSizeColumnCheckBox = showAvgFleetSizeColumnCheckBox ?? throw new ArgumentNullException(nameof(showAvgFleetSizeColumnCheckBox));
            _showLastShipSeenColumnCheckBox = showLastShipSeenColumnCheckBox ?? throw new ArgumentNullException(nameof(showLastShipSeenColumnCheckBox));
            _showLastSeenColumnCheckBox = showLastSeenColumnCheckBox ?? throw new ArgumentNullException(nameof(showLastSeenColumnCheckBox));
            _showCynoHullSeenColumnCheckBox = showCynoHullSeenColumnCheckBox ?? throw new ArgumentNullException(nameof(showCynoHullSeenColumnCheckBox));
            _showCorpAllianceCountsCheckBox = showCorpAllianceCountsCheckBox ?? throw new ArgumentNullException(nameof(showCorpAllianceCountsCheckBox));
            _sigColumn = sigColumn ?? throw new ArgumentNullException(nameof(sigColumn));
            _characterColumn = characterColumn ?? throw new ArgumentNullException(nameof(characterColumn));
            _allianceColumn = allianceColumn ?? throw new ArgumentNullException(nameof(allianceColumn));
            _corpColumn = corpColumn ?? throw new ArgumentNullException(nameof(corpColumn));
            _killsColumn = killsColumn ?? throw new ArgumentNullException(nameof(killsColumn));
            _lossesColumn = lossesColumn ?? throw new ArgumentNullException(nameof(lossesColumn));
            _avgFleetSizeColumn = avgFleetSizeColumn ?? throw new ArgumentNullException(nameof(avgFleetSizeColumn));
            _lastShipSeenColumn = lastShipSeenColumn ?? throw new ArgumentNullException(nameof(lastShipSeenColumn));
            _lastSeenColumn = lastSeenColumn ?? throw new ArgumentNullException(nameof(lastSeenColumn));
            _cynoHullSeenColumn = cynoHullSeenColumn ?? throw new ArgumentNullException(nameof(cynoHullSeenColumn));
            _minimumBoardLayoutHostWidth = minimumBoardLayoutHostWidth;
        }

        public void InitializeBoardColumnVisibilityUi()
        {
            ApplyBoardColumnSettingsToCheckBoxes();
            ApplyBoardColumnVisibility();
        }

        public void InitializeBoardColumnLayoutUi()
        {
            _boardColumnSettingsController.InitializeBoardColumnLayoutUi(
                ApplyBoardColumnLayout,
                ("SigColumn", _sigColumn),
                ("CharacterColumn", _characterColumn),
                ("AllianceColumn", _allianceColumn),
                ("CorpColumn", _corpColumn),
                ("KillsColumn", _killsColumn),
                ("LossesColumn", _lossesColumn),
                ("AvgFleetSizeColumn", _avgFleetSizeColumn),
                ("LastShipSeenColumn", _lastShipSeenColumn),
                ("LastSeenColumn", _lastSeenColumn),
                ("CynoHullSeenColumn", _cynoHullSeenColumn));
        }

        public void ApplyBoardDisplaySettings()
        {
            _boardDisplaySettingsController.ApplySettingsToBoard(_getSettings(), _pilotBoard, _resources);
        }

        public void HandleShowBoardGridLinesChanged()
        {
            _mainWindowSettingsCoordinator.HandleShowBoardGridLinesChanged(
                _isApplyingSettings(),
                _getSettings(),
                _showBoardGridLinesCheckBox,
                ApplyBoardDisplaySettings);
        }

        public void HandleBoardTextSizeChanged()
        {
            _mainWindowSettingsCoordinator.HandleBoardTextSizeChanged(
                _isApplyingSettings(),
                _getSettings(),
                _boardTextSizeComboBox,
                ApplyBoardDisplaySettings,
                _updateWindowMinimumSize);
        }

        public void HandleBoardFontFamilyChanged()
        {
            _mainWindowSettingsCoordinator.HandleBoardFontFamilyChanged(
                _isApplyingSettings(),
                _getSettings(),
                _boardFontFamilyComboBox,
                ApplyBoardDisplaySettings,
                _updateWindowMinimumSize);
        }

        public void HandleBoardColumnVisibilityChanged()
        {
            _boardColumnSettingsController.HandleBoardColumnVisibilityChanged(
                _isApplyingSettings(),
                _getSettings(),
                _showSigColumnCheckBox,
                _showAllianceColumnCheckBox,
                _showCorpColumnCheckBox,
                _showKillsColumnCheckBox,
                _showLossesColumnCheckBox,
                _showAvgFleetSizeColumnCheckBox,
                _showLastShipSeenColumnCheckBox,
                _showLastSeenColumnCheckBox,
                _showCynoHullSeenColumnCheckBox,
                ApplyBoardColumnVisibility);
        }

        public void HandleShowCorpAllianceCountsChanged()
        {
            _boardColumnSettingsController.HandleShowCorpAllianceCountsChanged(
                _isApplyingSettings(),
                _getSettings(),
                _showCorpAllianceCountsCheckBox.IsChecked == true,
                _recomputeCorpAllianceCounts);
        }

        public void HandleShowAllBoardColumns()
        {
            _boardColumnSettingsController.HandleShowAllBoardColumns(
                _getSettings(),
                ApplyBoardColumnSettingsToCheckBoxes,
                ApplyBoardColumnVisibility);
        }

        public void HandleResetBoardColumns()
        {
            _boardColumnSettingsController.HandleResetBoardColumns(
                _getSettings(),
                ApplyBoardColumnSettingsToCheckBoxes,
                ApplyBoardColumnVisibility);
        }

        public void HandleResetBoardLayout()
        {
            _boardColumnSettingsController.HandleResetBoardLayout(
                _getSettings(),
                ApplyCanonicalBoardColumnLayout,
                SaveCurrentBoardColumnLayout);
        }

        public void ApplyBoardColumnSettingsToCheckBoxes()
        {
            var wasApplyingSettings = _isApplyingSettings();
            _setApplyingSettings(true);

            try
            {
                _boardColumnSettingsController.ApplyBoardColumnSettingsToCheckBoxes(
                    _getSettings(),
                    _showSigColumnCheckBox,
                    _showAllianceColumnCheckBox,
                    _showCorpColumnCheckBox,
                    _showKillsColumnCheckBox,
                    _showLossesColumnCheckBox,
                    _showAvgFleetSizeColumnCheckBox,
                    _showLastShipSeenColumnCheckBox,
                    _showLastSeenColumnCheckBox,
                    _showCynoHullSeenColumnCheckBox,
                    _showCorpAllianceCountsCheckBox);
            }
            finally
            {
                _setApplyingSettings(wasApplyingSettings);
            }
        }

        public void SaveBoardColumnSettingsFromCheckBoxes()
        {
            _boardColumnSettingsController.SaveBoardColumnSettingsFromCheckBoxes(
                _getSettings(),
                _showSigColumnCheckBox,
                _showAllianceColumnCheckBox,
                _showCorpColumnCheckBox,
                _showKillsColumnCheckBox,
                _showLossesColumnCheckBox,
                _showAvgFleetSizeColumnCheckBox,
                _showLastShipSeenColumnCheckBox,
                _showLastSeenColumnCheckBox,
                _showCynoHullSeenColumnCheckBox);
        }

        public void ApplyBoardColumnVisibility()
        {
            _boardColumnLayoutPersistenceController.RunWhileApplyingBoardColumnLayout(
                () => _boardColumnSettingsController.ApplyBoardColumnVisibility(_getSettings()));
            ScheduleFitVisibleBoardColumnsToViewport(force: true);
        }

        public void ApplySavedBoardColumnLayout()
        {
            _boardColumnLayoutPersistenceController.ApplySavedBoardColumnLayout(
                _getSettings(),
                ApplyBoardColumnLayout,
                ApplyCanonicalBoardColumnLayout);
        }

        public void ApplyCanonicalBoardColumnLayout(string reason)
        {
            ApplyBoardColumnLayout(_boardColumnLayoutController.GetCanonicalBoardColumnLayout(), reason);
        }

        public void ApplyBoardColumnLayout(IEnumerable<BoardColumnLayoutSetting> layoutSettings, string reason)
        {
            _boardColumnLayoutPersistenceController.ApplyBoardColumnLayout(
                layoutSettings,
                () => ScheduleFitVisibleBoardColumnsToViewport(),
                reason);
        }

        public void HandlePilotBoardColumnReordered()
        {
            ScheduleFitVisibleBoardColumnsToViewport();
            ScheduleBoardColumnLayoutSave("Column reordered");
        }

        public void HandlePilotBoardSizeChanged()
        {
            ScheduleFitVisibleBoardColumnsToViewport();
        }

        public void HandleBoardColumnWidthChanged()
        {
            ScheduleBoardColumnLayoutSave("Column width changed");
        }

        public void ScheduleBoardColumnLayoutSave(string reason)
        {
            if (!_boardColumnLayoutPersistenceController.TryQueueBoardColumnLayoutSave(
                    _isApplyingSettings(),
                    IsBoardLayoutHostReady,
                    reason))
            {
                return;
            }

            _boardColumnLayoutSaveTimer.Stop();
            _boardColumnLayoutSaveTimer.Start();
        }

        public void HandleBoardColumnLayoutSaveTimerTick()
        {
            _boardColumnLayoutSaveTimer.Stop();
            SaveCurrentBoardColumnLayout(_boardColumnLayoutPersistenceController.DequeuePendingBoardColumnLayoutSaveReason());
        }

        public void SaveCurrentBoardColumnLayout(string reason)
        {
            _boardColumnLayoutPersistenceController.SaveCurrentBoardColumnLayout(
                _getSettings(),
                IsBoardLayoutHostReady,
                reason);
        }

        public void FinalizeBoardColumnLayoutInitialization()
        {
            ApplyCanonicalBoardColumnLayout("Finalize board layout after load");
            ApplySavedBoardColumnLayout();
            _boardColumnLayoutPersistenceController.EnsureBoardColumnWidthTracking((_, _) => HandleBoardColumnWidthChanged());
            _boardColumnLayoutPersistenceController.MarkBoardColumnLayoutReady();
            AppLogger.UiInfo($"Board column layout initialization complete.\nhostReady={IsBoardLayoutHostReady()} actualWidth={_pilotBoard.ActualWidth:0.##}");
        }

        public bool IsBoardLayoutHostReady()
        {
            return _pilotBoard.IsLoaded
                && _isLoaded()
                && _pilotBoard.ActualWidth >= _minimumBoardLayoutHostWidth;
        }

        public void ScheduleFitVisibleBoardColumnsToViewport(bool force = false)
        {
            if (!_boardColumnLayoutPersistenceController.TryQueueFitVisibleBoardColumnsToViewport(_pilotBoard, force))
            {
                return;
            }

            _dispatcher.BeginInvoke(
                new Action(() =>
                {
                    _boardColumnLayoutPersistenceController.CompleteQueuedFitVisibleBoardColumnsToViewport(_pilotBoard);
                }),
                DispatcherPriority.ContextIdle);
        }

        public void FitVisibleBoardColumnsToViewport()
        {
            _boardColumnLayoutPersistenceController.FitVisibleBoardColumnsToViewport(_pilotBoard);
        }
    }
}
