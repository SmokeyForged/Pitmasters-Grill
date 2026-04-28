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
        private readonly Action<PilotBoardRow> _openZkillAction;
        private bool _isApplyingState;

        public PilotDetailWindow(
            PilotBoardRow row,
            PilotBoardRowDetailFormatter formatter,
            NotesRepository notesRepository,
            Func<PilotBoardRow, IgnoreEntryType, bool> ignoreAction,
            Action<PilotBoardRow> openZkillAction)
        {
            _row = row ?? throw new ArgumentNullException(nameof(row));
            _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
            _notesRepository = notesRepository ?? throw new ArgumentNullException(nameof(notesRepository));
            _ignoreAction = ignoreAction ?? throw new ArgumentNullException(nameof(ignoreAction));
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
                NotesTagsBox.Text ?? string.Empty,
                KnownCynoOverrideCheckBox.IsChecked == true,
                BaitOverrideCheckBox.IsChecked == true);

            _row.KnownCynoOverride = KnownCynoOverrideCheckBox.IsChecked == true;
            _row.BaitOverride = BaitOverrideCheckBox.IsChecked == true;
            _formatter.UpdateConfirmedCynoModuleState(_row);
        }

        private void ApplyRow()
        {
            _isApplyingState = true;
            try
            {
                PilotNameText.Text = _row.CharacterName;
                PilotSummaryText.Text = _formatter.GetPilotSummaryText(_row);
                RecentActivityText.Text = _formatter.GetConciseRecentPublicActivityText(_row);
                BottomFreshnessText.Text = _formatter.GetBottomFreshnessText(_row);

                var cynoSignal = _formatter.GetCynoSignal(_row);
                CynoSignalText.Text = _formatter.GetCynoSignalHeadlineText(cynoSignal);
                CynoConfidenceBar.Value = cynoSignal.Score;
                var brushKey = GetCynoSignalBrushKey(cynoSignal);
                CynoSignalText.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
                CynoConfidenceBar.SetResourceReference(ProgressBar.ForegroundProperty, brushKey);
                EvidenceText.Text = _formatter.GetConciseEvidenceText(cynoSignal);
                LimitationsText.Text = _formatter.GetConciseLimitationsText(cynoSignal);

                NotesTagsBox.Text = _notesRepository.GetNotes(_row.CharacterName);
                KnownCynoOverrideCheckBox.IsChecked = _row.KnownCynoOverride;
                BaitOverrideCheckBox.IsChecked = _row.BaitOverride;
                UpdateIgnoreButtonStates();
                AppLogger.UiInfo(
                    $"Details window row loaded. pilot='{_row.CharacterName}' pilotId='{_row.CharacterId}' corp='{_row.CorpName}' corpId='{_row.CorpId}' alliance='{_row.AllianceName}' allianceId='{_row.AllianceId}' ignorePilotEnabled={IgnorePilotButton.IsEnabled} ignoreCorpEnabled={IgnoreCorpButton.IsEnabled} ignoreAllianceEnabled={IgnoreAllianceButton.IsEnabled}");
            }
            finally
            {
                _isApplyingState = false;
            }
        }

        private void IgnorePilotButton_Click(object sender, RoutedEventArgs e)
        {
            Ignore(IgnoreEntryType.Pilot, IgnorePilotButton);
        }

        private void IgnoreCorpButton_Click(object sender, RoutedEventArgs e)
        {
            Ignore(IgnoreEntryType.Corporation, IgnoreCorpButton);
        }

        private void IgnoreAllianceButton_Click(object sender, RoutedEventArgs e)
        {
            Ignore(IgnoreEntryType.Alliance, IgnoreAllianceButton);
        }

        private void Ignore(IgnoreEntryType type, Button button)
        {
            SaveCurrentState();
            if (_ignoreAction(_row, type))
            {
                button.IsEnabled = false;
            }
        }

        private void OpenZkillButton_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentState();
            _openZkillAction(_row);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
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

        private void UpdateIgnoreButtonStates()
        {
            SetIgnoreButtonState(IgnorePilotButton, TryGetId(_row.CharacterId), "Pilot ID unavailable");
            SetIgnoreButtonState(IgnoreCorpButton, TryGetId(_row.CorpId), "Corporation ID unavailable");
            SetIgnoreButtonState(IgnoreAllianceButton, TryGetId(_row.AllianceId), "Alliance ID unavailable");
        }

        private static void SetIgnoreButtonState(Button button, long? id, string unavailableText)
        {
            button.IsEnabled = id.HasValue;
            button.ToolTip = id.HasValue ? $"Add typed ignore entry for ID {id.Value}." : unavailableText;
        }

        private static long? TryGetId(string idText)
        {
            return long.TryParse(idText, out var id) && id > 0
                ? id
                : null;
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
