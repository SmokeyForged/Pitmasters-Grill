using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using PitmastersGrill.Views;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using FormsScreen = System.Windows.Forms.Screen;

namespace PitmastersGrill.Services
{
    public sealed class PilotDetailSurface
    {
        private readonly Window _owner;
        private readonly Border _detailPane;
        private readonly TextBlock _selectedCharacterText;
        private readonly TextBlock _fullCorpText;
        private readonly TextBlock _fullAllianceText;
        private readonly TextBlock _freshnessText;
        private readonly TextBlock _recentPublicActivityText;
        private readonly TextBlock _cynoSignalText;
        private readonly ProgressBar _cynoConfidenceBar;
        private readonly TextBlock _cynoEvidenceText;
        private readonly TextBlock _cynoLimitationsText;
        private readonly TextBlock _explainabilityText;
        private readonly TextBox _notesTagsBox;
        private readonly CheckBox _knownCynoOverrideCheckBox;
        private readonly CheckBox _baitOverrideCheckBox;
        private readonly Button _ignoreAllianceButton;
        private readonly Button _watchPilotDetailAction;
        private readonly DetailPaneController _detailPaneController;
        private readonly PilotDetailWindowLifecycleController _pilotDetailWindowLifecycleController;
        private readonly PilotDetailWindowPlacementController _pilotDetailWindowPlacementController;
        private readonly PilotBoardRowDetailFormatter _pilotBoardRowDetailFormatter;
        private readonly PilotDetailActionsPresenter _pilotDetailActionsPresenter;
        private readonly WatchedPilotRepository _watchedPilotRepository;
        private readonly NotesRepository _notesRepository;
        private readonly SettingsTabController _settingsTabController;
        private readonly Func<AppSettings> _getAppSettings;
        private readonly Func<long, bool> _allianceIsIgnored;
        private readonly Func<PilotBoardRow?, IReadOnlyCollection<PilotBoardRow>, PilotBoardRow?> _getSelectedOrDisplayedRow;
        private readonly Func<PilotBoardRow, IgnoreEntryType, bool> _tryIgnoreForRow;
        private readonly Action<PilotBoardRow> _openZkillForRow;
        private readonly Action _applyCurrentBoardOrdering;
        private readonly Action<PilotBoardRow> _onWatchedRowChanged;
        private readonly double _detailWindowGap;

        private PilotDetailWindow? _activePilotDetailWindow;

        public PilotDetailSurface(
            Window owner,
            Border detailPane,
            TextBlock selectedCharacterText,
            TextBlock fullCorpText,
            TextBlock fullAllianceText,
            TextBlock freshnessText,
            TextBlock recentPublicActivityText,
            TextBlock cynoSignalText,
            ProgressBar cynoConfidenceBar,
            TextBlock cynoEvidenceText,
            TextBlock cynoLimitationsText,
            TextBlock explainabilityText,
            TextBox notesTagsBox,
            CheckBox knownCynoOverrideCheckBox,
            CheckBox baitOverrideCheckBox,
            Button ignoreAllianceButton,
            Button watchPilotDetailAction,
            DetailPaneController detailPaneController,
            PilotDetailWindowLifecycleController pilotDetailWindowLifecycleController,
            PilotDetailWindowPlacementController pilotDetailWindowPlacementController,
            PilotBoardRowDetailFormatter pilotBoardRowDetailFormatter,
            PilotDetailActionsPresenter pilotDetailActionsPresenter,
            WatchedPilotRepository watchedPilotRepository,
            NotesRepository notesRepository,
            SettingsTabController settingsTabController,
            Func<AppSettings> getAppSettings,
            Func<long, bool> allianceIsIgnored,
            Func<PilotBoardRow?, IReadOnlyCollection<PilotBoardRow>, PilotBoardRow?> getSelectedOrDisplayedRow,
            Func<PilotBoardRow, IgnoreEntryType, bool> tryIgnoreForRow,
            Action<PilotBoardRow> openZkillForRow,
            Action applyCurrentBoardOrdering,
            Action<PilotBoardRow> onWatchedRowChanged,
            double detailWindowGap)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _detailPane = detailPane ?? throw new ArgumentNullException(nameof(detailPane));
            _selectedCharacterText = selectedCharacterText ?? throw new ArgumentNullException(nameof(selectedCharacterText));
            _fullCorpText = fullCorpText ?? throw new ArgumentNullException(nameof(fullCorpText));
            _fullAllianceText = fullAllianceText ?? throw new ArgumentNullException(nameof(fullAllianceText));
            _freshnessText = freshnessText ?? throw new ArgumentNullException(nameof(freshnessText));
            _recentPublicActivityText = recentPublicActivityText ?? throw new ArgumentNullException(nameof(recentPublicActivityText));
            _cynoSignalText = cynoSignalText ?? throw new ArgumentNullException(nameof(cynoSignalText));
            _cynoConfidenceBar = cynoConfidenceBar ?? throw new ArgumentNullException(nameof(cynoConfidenceBar));
            _cynoEvidenceText = cynoEvidenceText ?? throw new ArgumentNullException(nameof(cynoEvidenceText));
            _cynoLimitationsText = cynoLimitationsText ?? throw new ArgumentNullException(nameof(cynoLimitationsText));
            _explainabilityText = explainabilityText ?? throw new ArgumentNullException(nameof(explainabilityText));
            _notesTagsBox = notesTagsBox ?? throw new ArgumentNullException(nameof(notesTagsBox));
            _knownCynoOverrideCheckBox = knownCynoOverrideCheckBox ?? throw new ArgumentNullException(nameof(knownCynoOverrideCheckBox));
            _baitOverrideCheckBox = baitOverrideCheckBox ?? throw new ArgumentNullException(nameof(baitOverrideCheckBox));
            _ignoreAllianceButton = ignoreAllianceButton ?? throw new ArgumentNullException(nameof(ignoreAllianceButton));
            _watchPilotDetailAction = watchPilotDetailAction ?? throw new ArgumentNullException(nameof(watchPilotDetailAction));
            _detailPaneController = detailPaneController ?? throw new ArgumentNullException(nameof(detailPaneController));
            _pilotDetailWindowLifecycleController = pilotDetailWindowLifecycleController ?? throw new ArgumentNullException(nameof(pilotDetailWindowLifecycleController));
            _pilotDetailWindowPlacementController = pilotDetailWindowPlacementController ?? throw new ArgumentNullException(nameof(pilotDetailWindowPlacementController));
            _pilotBoardRowDetailFormatter = pilotBoardRowDetailFormatter ?? throw new ArgumentNullException(nameof(pilotBoardRowDetailFormatter));
            _pilotDetailActionsPresenter = pilotDetailActionsPresenter ?? throw new ArgumentNullException(nameof(pilotDetailActionsPresenter));
            _watchedPilotRepository = watchedPilotRepository ?? throw new ArgumentNullException(nameof(watchedPilotRepository));
            _notesRepository = notesRepository ?? throw new ArgumentNullException(nameof(notesRepository));
            _settingsTabController = settingsTabController ?? throw new ArgumentNullException(nameof(settingsTabController));
            _getAppSettings = getAppSettings ?? throw new ArgumentNullException(nameof(getAppSettings));
            _allianceIsIgnored = allianceIsIgnored ?? throw new ArgumentNullException(nameof(allianceIsIgnored));
            _getSelectedOrDisplayedRow = getSelectedOrDisplayedRow ?? throw new ArgumentNullException(nameof(getSelectedOrDisplayedRow));
            _tryIgnoreForRow = tryIgnoreForRow ?? throw new ArgumentNullException(nameof(tryIgnoreForRow));
            _openZkillForRow = openZkillForRow ?? throw new ArgumentNullException(nameof(openZkillForRow));
            _applyCurrentBoardOrdering = applyCurrentBoardOrdering ?? throw new ArgumentNullException(nameof(applyCurrentBoardOrdering));
            _onWatchedRowChanged = onWatchedRowChanged ?? throw new ArgumentNullException(nameof(onWatchedRowChanged));
            _detailWindowGap = detailWindowGap;
        }

        public void ApplyThemeToActiveWindow(ResourceDictionary resources)
        {
            _activePilotDetailWindow?.ApplyThemeResources(resources);
        }

        public void OpenDetailsWindow(PilotBoardRow row)
        {
            var action = _pilotDetailWindowLifecycleController.DecideOpenAction(row.CharacterName);
            if (action == PilotDetailWindowOpenAction.ActivateExisting)
            {
                _activePilotDetailWindow?.Activate();
                return;
            }

            if (action == PilotDetailWindowOpenAction.ReplaceExisting)
            {
                CloseActiveDetailWindow();
            }

            _activePilotDetailWindow = new PilotDetailWindow(
                row,
                _pilotBoardRowDetailFormatter,
                _notesRepository,
                TryIgnoreForRow,
                ToggleWatchForRow,
                _openZkillForRow)
            {
                Owner = _owner
            };

            _activePilotDetailWindow.ApplyThemeResources(_owner.Resources);
            _activePilotDetailWindow.Topmost = _owner.Topmost;
            PositionDetailWindow(_activePilotDetailWindow);
            _activePilotDetailWindow.Closed += ActivePilotDetailWindow_Closed;
            _pilotDetailWindowLifecycleController.MarkWindowOpened(row.CharacterName);
            _activePilotDetailWindow.Show();
            AppLogger.UiInfo($"Details window opened. character='{row.CharacterName}'");
        }

        public void CloseActiveDetailWindow()
        {
            if (_activePilotDetailWindow == null)
            {
                return;
            }

            var window = _activePilotDetailWindow;
            _activePilotDetailWindow = null;
            window.Closed -= ActivePilotDetailWindow_Closed;
            window.SaveCurrentState();
            _pilotDetailWindowLifecycleController.ClearActiveWindow();
            window.Close();
        }

        public void ShowDetailPane(PilotBoardRow row)
        {
            _detailPaneController.ShowDetailPane(
                row,
                _detailPane,
                _selectedCharacterText,
                _fullCorpText,
                _fullAllianceText,
                _freshnessText,
                _recentPublicActivityText,
                _cynoSignalText,
                _cynoConfidenceBar,
                _cynoEvidenceText,
                _cynoLimitationsText,
                _explainabilityText,
                _notesTagsBox,
                _knownCynoOverrideCheckBox,
                _baitOverrideCheckBox);

            UpdateIgnoreAllianceButtonState(row);
            UpdateWatchPilotDetailActionState(row);
        }

        public void HideDetailPane()
        {
            _detailPaneController.HideDetailPane(
                _detailPane,
                _notesTagsBox,
                _knownCynoOverrideCheckBox,
                _baitOverrideCheckBox);

            _explainabilityText.Text = "Explainability: --";
            _recentPublicActivityText.Text = "Recent Public Kill/Loss Activity: --";
            _cynoSignalText.Text = "Cyno Signal: Unknown";
            _cynoConfidenceBar.Value = 0;
            _cynoEvidenceText.Text = "Evidence: --";
            _cynoLimitationsText.Text = "Limitations: --";

            UpdateIgnoreAllianceButtonState(null);
            UpdateWatchPilotDetailActionState(null);
        }

        public void SaveCurrentNotesAndTags(PilotBoardRow? selectedRow)
        {
            if (_activePilotDetailWindow != null)
            {
                _activePilotDetailWindow.SaveCurrentState();
                return;
            }

            _detailPaneController.SaveCurrentNotesAndTags(
                _notesTagsBox.Text,
                _knownCynoOverrideCheckBox.IsChecked == true,
                _baitOverrideCheckBox.IsChecked == true,
                selectedRow);
        }

        public PilotBoardRow? GetSelectedOrDisplayedDetailRow(PilotBoardRow? selectedRow, IReadOnlyCollection<PilotBoardRow> currentRows)
        {
            return _getSelectedOrDisplayedRow(selectedRow, currentRows);
        }

        public void RefreshActiveDetailWindowIfSelected(PilotBoardRow row)
        {
            if (_activePilotDetailWindow != null &&
                _pilotDetailWindowLifecycleController.ShouldRefreshActiveWindow(row.CharacterName))
            {
                _activePilotDetailWindow.RefreshRow();
            }
        }

        public void UpdateIgnoreAllianceButtonState(PilotBoardRow? row)
        {
            var allianceId = _pilotDetailActionsPresenter.TryGetAllianceId(row?.AllianceId);
            var state = _pilotDetailActionsPresenter.BuildIgnoreAllianceActionState(
                row,
                allianceId.HasValue && _allianceIsIgnored(allianceId.Value));

            _ignoreAllianceButton.IsEnabled = state.IsEnabled;
            _ignoreAllianceButton.ToolTip = state.ToolTip;
        }

        public void UpdateWatchPilotDetailActionState(PilotBoardRow? row)
        {
            var state = _pilotDetailActionsPresenter.BuildWatchPilotActionState(row);
            _watchPilotDetailAction.IsEnabled = state.IsEnabled;
            _watchPilotDetailAction.Content = state.Content;
            _watchPilotDetailAction.ToolTip = state.ToolTip;
            _watchPilotDetailAction.SetResourceReference(
                Control.ForegroundProperty,
                state.ForegroundResourceKey);
        }

        public void ToggleWatchForRow(PilotBoardRow row)
        {
            var pilotId = _pilotDetailActionsPresenter.TryGetPilotId(row.CharacterId);
            if (!pilotId.HasValue)
            {
                UpdateWatchPilotDetailActionState(row);
                AppLogger.UiWarn($"Watch requested without a valid pilot ID. character='{row.CharacterName}'");
                return;
            }

            var newWatchedState = !row.IsWatched;
            if (!_watchedPilotRepository.SetWatched(row.CharacterId, newWatchedState))
            {
                UpdateWatchPilotDetailActionState(row);
                AppLogger.UiWarn($"Watch state change failed. character='{row.CharacterName}' characterId='{row.CharacterId}'");
                return;
            }

            row.IsWatched = newWatchedState;
            _applyCurrentBoardOrdering();
            UpdateWatchPilotDetailActionState(row);
            _onWatchedRowChanged(row);

            AppLogger.UiInfo(
                $"Watch state changed. character='{row.CharacterName}' characterId='{row.CharacterId}' watched={row.IsWatched}");
        }

        public bool TryIgnoreAllianceForSelectedOrDisplayedRow(PilotBoardRow? selectedRow, IReadOnlyCollection<PilotBoardRow> currentRows)
        {
            var row = GetSelectedOrDisplayedDetailRow(selectedRow, currentRows);
            if (row == null)
            {
                AppLogger.UiWarn("Ignore alliance requested with no selected or displayed detail row.");
                return false;
            }

            return TryIgnoreForRow(row, IgnoreEntryType.Alliance);
        }

        private bool TryIgnoreForRow(PilotBoardRow selectedRow, IgnoreEntryType type)
        {
            return _tryIgnoreForRow(selectedRow, type);
        }

        private void PositionDetailWindow(PilotDetailWindow detailWindow)
        {
            detailWindow.WindowStartupLocation = WindowStartupLocation.Manual;

            var detailWidth = detailWindow.Width > 0 ? detailWindow.Width : 430;
            var detailHeight = detailWindow.Height > 0 ? detailWindow.Height : 360;
            var ownerWidth = _owner.ActualWidth > 0 ? _owner.ActualWidth : _owner.Width;
            var ownerHeight = _owner.ActualHeight > 0 ? _owner.ActualHeight : _owner.Height;
            var ownerLeft = double.IsNaN(_owner.Left) ? 0 : _owner.Left;
            var ownerTop = double.IsNaN(_owner.Top) ? 0 : _owner.Top;
            var ownerHandle = new WindowInteropHelper(_owner).Handle;
            var monitor = ownerHandle != IntPtr.Zero
                ? FormsScreen.FromHandle(ownerHandle)
                : FormsScreen.FromPoint(new System.Drawing.Point(
                    (int)Math.Round(ownerLeft),
                    (int)Math.Round(ownerTop)));

            var presentationSource = PresentationSource.FromVisual(_owner);
            var transformFromDevice = presentationSource?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            var workAreaPixels = monitor.WorkingArea;
            var workTopLeft = transformFromDevice.Transform(new Point(workAreaPixels.Left, workAreaPixels.Top));
            var workBottomRight = transformFromDevice.Transform(new Point(workAreaPixels.Right, workAreaPixels.Bottom));
            var workLeft = workTopLeft.X;
            var workTop = workTopLeft.Y;
            var workRight = workBottomRight.X;
            var workBottom = workBottomRight.Y;
            var preferLeft = _settingsTabController.GetPilotDetailPlacementPreference(_getAppSettings()) == PilotDetailPlacementPreference.AutoPreferLeft;
            var placement = _pilotDetailWindowPlacementController.BuildPlacement(
                detailWidth,
                detailHeight,
                ownerLeft,
                ownerTop,
                ownerWidth,
                workLeft,
                workTop,
                workRight,
                workBottom,
                preferLeft,
                _detailWindowGap);

            detailWindow.Left = placement.Left;
            detailWindow.Top = placement.Top;

            if (placement.WasAdjusted)
            {
                AppLogger.UiInfo(
                    $"Detail window placement adjusted. ownerBounds=({ownerLeft:0.##},{ownerTop:0.##},{ownerWidth:0.##},{ownerHeight:0.##}) workArea=({workLeft:0.##},{workTop:0.##},{workRight - workLeft:0.##},{workBottom - workTop:0.##}) preferredSide={placement.PreferredSide} finalSide={placement.FinalSide} finalBounds=({placement.Left:0.##},{placement.Top:0.##},{detailWidth:0.##},{detailHeight:0.##})");
            }
        }

        private void ActivePilotDetailWindow_Closed(object? sender, EventArgs e)
        {
            if (_activePilotDetailWindow != null)
            {
                _activePilotDetailWindow.Closed -= ActivePilotDetailWindow_Closed;
                _activePilotDetailWindow = null;
            }

            _pilotDetailWindowLifecycleController.ClearActiveWindow();
        }
    }
}
