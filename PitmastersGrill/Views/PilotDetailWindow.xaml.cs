using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using PitmastersGrill.Services;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PitmastersGrill.Views
{
    public partial class PilotDetailWindow : Window
    {
        private readonly PilotBoardRow _row;
        private readonly PilotBoardRowDetailFormatter _formatter;
        private readonly NotesRepository _notesRepository;
        private readonly Func<PilotBoardRow, IgnoreEntryType, bool> _ignoreAction;
        private readonly Action<PilotBoardRow> _toggleWatchAction;
        private readonly Action<PilotBoardRow> _openZkillAction;
        private bool _isApplyingState;

        public PilotDetailWindow(
            PilotBoardRow row,
            PilotBoardRowDetailFormatter formatter,
            NotesRepository notesRepository,
            Func<PilotBoardRow, IgnoreEntryType, bool> ignoreAction,
            Action<PilotBoardRow> toggleWatchAction,
            Action<PilotBoardRow> openZkillAction)
        {
            _row = row ?? throw new ArgumentNullException(nameof(row));
            _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
            _notesRepository = notesRepository ?? throw new ArgumentNullException(nameof(notesRepository));
            _ignoreAction = ignoreAction ?? throw new ArgumentNullException(nameof(ignoreAction));
            _toggleWatchAction = toggleWatchAction ?? throw new ArgumentNullException(nameof(toggleWatchAction));
            _openZkillAction = openZkillAction ?? throw new ArgumentNullException(nameof(openZkillAction));

            InitializeComponent();
            Title = $"PMG Details - {_row.CharacterName}";
            ApplyRow();
        }

        public string CharacterName => _row.CharacterName;

        public void ApplyThemeResources(ResourceDictionary sourceResources)
        {
            if (sourceResources == null)
            {
                return;
            }

            foreach (var key in sourceResources.Keys)
            {
                Resources[key] = sourceResources[key];
            }

            ApplyRow();
        }

        public void RefreshRow()
        {
            ApplyRow();
        }

        public void SaveCurrentState()
        {
            if (_isApplyingState)
            {
                return;
            }

            _notesRepository.SaveNotesAndTags(
                _row.CharacterName,
                _notesRepository.GetNotes(_row.CharacterName),
                _row.KnownCynoOverride,
                _row.BaitOverride);

            _row.HasNotes = _notesRepository.HasNotes(_row.CharacterName);
            _formatter.UpdateConfirmedCynoModuleState(_row);
        }

        private void ApplyRow()
        {
            _isApplyingState = true;
            try
            {
                PilotNameText.Text = _row.CharacterName;
                AffiliationText.Text = _formatter.GetCompactPilotAffiliationText(_row);
                ActivityText.Text = _formatter.GetCompactPilotActivityText(_row);
                KillLossText.Text = _formatter.GetCompactKillLossText(_row);

                var cynoSignal = _formatter.GetCynoSignal(_row);
                BaitSignalText.Text = _formatter.GetCompactBaitStatusText(_row);
                CynoSignalText.Text = _formatter.GetCompactCynoSignalHeadlineText(cynoSignal);
                EvidenceText.Text = _formatter.GetPrimaryCompactEvidenceText(_row, cynoSignal);

                var limitations = _formatter.GetCompactLimitationsText(_row, cynoSignal);
                LimitationsText.Text = limitations;
                LimitationsText.Visibility = string.IsNullOrWhiteSpace(limitations)
                    ? Visibility.Collapsed
                    : Visibility.Visible;

                BottomFreshnessText.Text = _formatter.GetBottomFreshnessText(_row);

                var brushKey = GetCynoSignalBrushKey(cynoSignal);
                CynoSignalText.SetResourceReference(TextBlock.ForegroundProperty, brushKey);

                SetStateLinkVisual(KnownCynoOverrideLink, _row.KnownCynoOverride, "Toggle known-cyno override for this pilot.");
                SetStateLinkVisual(BaitOverrideLink, _row.BaitOverride, "Toggle bait override for this pilot.");

                UpdateActionLinkStates();

                AppLogger.UiInfo(
                    $"Details window row loaded. pilot='{_row.CharacterName}' pilotId='{_row.CharacterId}' corp='{_row.CorpName}' corpId='{_row.CorpId}' alliance='{_row.AllianceName}' allianceId='{_row.AllianceId}' manualBait={_row.BaitOverride} derivedBaitEvidenceCount={_row.DerivedBaitEvidenceCount} boardSignal={_row.BoardSignalKind} boardSignalReason='{_row.BoardSignalToolTip}'");
            }
            finally
            {
                _isApplyingState = false;
            }
        }

        private void KnownCynoOverrideLink_Click(object sender, RoutedEventArgs e)
        {
            _row.KnownCynoOverride = !_row.KnownCynoOverride;
            SaveCurrentState();
            ApplyRow();
        }

        private void BaitOverrideLink_Click(object sender, RoutedEventArgs e)
        {
            _row.BaitOverride = !_row.BaitOverride;
            SaveCurrentState();
            ApplyRow();
        }

        private void WatchPilotLink_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentState();
            _toggleWatchAction(_row);
            ApplyRow();
        }

        private void IgnorePilotLink_Click(object sender, RoutedEventArgs e)
        {
            Ignore(IgnoreEntryType.Pilot, IgnorePilotLink);
        }

        private void IgnoreCorpLink_Click(object sender, RoutedEventArgs e)
        {
            Ignore(IgnoreEntryType.Corporation, IgnoreCorpLink);
        }

        private void IgnoreAllianceLink_Click(object sender, RoutedEventArgs e)
        {
            Ignore(IgnoreEntryType.Alliance, IgnoreAllianceLink);
        }

        private void Ignore(IgnoreEntryType type, Button link)
        {
            SaveCurrentState();
            if (_ignoreAction(_row, type))
            {
                link.IsEnabled = false;
                SetActionLinkVisual(link, false, "Already ignored or unavailable.");
            }
        }

        private void OpenZkillLink_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentState();
            _openZkillAction(_row);
        }

        private void CloseLink_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentState();
            Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
            {
                return;
            }

            SaveCurrentState();
            Close();
            e.Handled = true;
        }

        protected override void OnClosed(EventArgs e)
        {
            SaveCurrentState();
            base.OnClosed(e);
        }

        private void UpdateActionLinkStates()
        {
            UpdateWatchLinkState();
            SetActionLinkVisual(IgnorePilotLink, TryGetId(_row.CharacterId).HasValue, "Pilot ID unavailable.");
            SetActionLinkVisual(IgnoreCorpLink, TryGetId(_row.CorpId).HasValue, "Corporation ID unavailable.");
            SetActionLinkVisual(IgnoreAllianceLink, TryGetId(_row.AllianceId).HasValue, "Alliance ID unavailable.");
            SetActionLinkVisual(OpenZkillLink, CanOpenZkill(_row), "zKill link unavailable.");
            SetActionLinkVisual(CloseLink, true, "Close this detail window.");
        }

        private void UpdateWatchLinkState()
        {
            var canWatch = TryGetId(_row.CharacterId).HasValue;
            WatchPilotLink.Content = _row.IsWatched ? "Unwatch" : "Watch";
            WatchPilotLink.IsEnabled = canWatch;
            WatchPilotLink.ToolTip = canWatch
                ? (_row.IsWatched ? "Stop watching this pilot." : "Mark this pilot as watched.")
                : "Pilot ID unavailable until identity resolves.";
            WatchPilotLink.SetResourceReference(
                Control.ForegroundProperty,
                _row.IsWatched ? "WatchedPilotMarkerBrush" : "SuccessGreenBrush");
        }

        private void SetStateLinkVisual(Button button, bool enabledState, string tooltip)
        {
            button.IsEnabled = true;
            button.ToolTip = tooltip;
            button.SetResourceReference(Control.ForegroundProperty, enabledState ? "SuccessGreenBrush" : "ErrorRedBrush");
        }

        private void SetActionLinkVisual(Button button, bool canClick, string unavailableText)
        {
            button.IsEnabled = canClick;
            button.ToolTip = canClick ? "Click to run this action." : unavailableText;
            button.SetResourceReference(Control.ForegroundProperty, canClick ? "SuccessGreenBrush" : "ErrorRedBrush");
        }

        private static long? TryGetId(string idText)
        {
            return long.TryParse(idText, out var id) && id > 0
                ? id
                : null;
        }

        private static bool CanOpenZkill(PilotBoardRow row)
        {
            return row != null &&
                   (!string.IsNullOrWhiteSpace(row.CharacterId) ||
                    !string.IsNullOrWhiteSpace(row.CharacterName));
        }

        private static string GetCynoSignalBrushKey(CynoSignalResult result)
        {
            if (result == null)
            {
                return "AccentAshBrush";
            }

            var confirmedTypes = result.Evidence
                .Where(x => x.IsConfirmedModuleEvidence)
                .Select(x => x.SignalType)
                .Distinct()
                .ToList();

            if (result.Status == CynoSignalStatus.Confirmed)
            {
                if (confirmedTypes.Contains(CynoSignalType.Covert))
                {
                    return "BoardSignalConfirmedCovertBrush";
                }

                if (confirmedTypes.Contains(CynoSignalType.Normal))
                {
                    return "BoardSignalConfirmedNormalBrush";
                }
            }

            return result.Status switch
            {
                CynoSignalStatus.Likely => result.SignalType == CynoSignalType.Industrial
                    ? "AccentAshBrush"
                    : "BoardSignalInferredCynoBrush",
                CynoSignalStatus.Possible => "BoardSignalPossibleBrush",
                CynoSignalStatus.Inferred => result.SignalType == CynoSignalType.Industrial
                    ? "AccentAshBrush"
                    : "BoardSignalInferredCynoBrush",
                _ => "AccentAshBrush"
            };
        }
    }
}
