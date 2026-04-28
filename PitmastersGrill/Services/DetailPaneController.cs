using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Windows;
using System.Windows.Controls;

namespace PitmastersGrill.Services
{
    public class DetailPaneController
    {
        private readonly NotesRepository _notesRepository;
        private readonly PilotBoardRowDetailFormatter _pilotBoardRowDetailFormatter;

        private string _activeDetailCharacterName = string.Empty;

        public DetailPaneController(
            NotesRepository notesRepository,
            PilotBoardRowDetailFormatter pilotBoardRowDetailFormatter)
        {
            _notesRepository = notesRepository ?? throw new ArgumentNullException(nameof(notesRepository));
            _pilotBoardRowDetailFormatter = pilotBoardRowDetailFormatter ?? throw new ArgumentNullException(nameof(pilotBoardRowDetailFormatter));
        }

        public bool IsApplyingState { get; private set; }

        public void RefreshDetailPaneIfSelected(
            PilotBoardRow row,
            Visibility detailPaneVisibility,
            TextBlock selectedCharacterText,
            TextBlock fullCorpText,
            TextBlock fullAllianceText,
            TextBlock freshnessText,
            TextBlock recentPublicActivityText,
            TextBlock cynoSignalText,
            ProgressBar cynoConfidenceBar,
            TextBlock cynoEvidenceText,
            TextBlock cynoLimitationsText,
            TextBlock explainabilityText)
        {
            if (row == null)
            {
                return;
            }

            if (detailPaneVisibility != Visibility.Visible)
            {
                return;
            }

            if (!string.Equals(_activeDetailCharacterName, row.CharacterName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ApplyDetailPaneText(row, selectedCharacterText, fullCorpText, fullAllianceText, freshnessText, recentPublicActivityText, cynoSignalText, cynoConfidenceBar, cynoEvidenceText, cynoLimitationsText, explainabilityText);
        }

        public void ShowDetailPane(
            PilotBoardRow row,
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
            CheckBox baitOverrideCheckBox)
        {
            if (row == null)
            {
                throw new ArgumentNullException(nameof(row));
            }

            _activeDetailCharacterName = row.CharacterName;
            ApplyDetailPaneText(row, selectedCharacterText, fullCorpText, fullAllianceText, freshnessText, recentPublicActivityText, cynoSignalText, cynoConfidenceBar, cynoEvidenceText, cynoLimitationsText, explainabilityText);

            IsApplyingState = true;

            try
            {
                notesTagsBox.Text = _notesRepository.GetNotes(row.CharacterName);
                knownCynoOverrideCheckBox.IsChecked = row.KnownCynoOverride;
                baitOverrideCheckBox.IsChecked = row.BaitOverride;
            }
            finally
            {
                IsApplyingState = false;
            }

            detailPane.Visibility = Visibility.Visible;
        }

        public void HideDetailPane(
            Border detailPane,
            TextBox notesTagsBox,
            CheckBox knownCynoOverrideCheckBox,
            CheckBox baitOverrideCheckBox)
        {
            _activeDetailCharacterName = string.Empty;
            IsApplyingState = true;

            try
            {
                notesTagsBox.Text = string.Empty;
                knownCynoOverrideCheckBox.IsChecked = false;
                baitOverrideCheckBox.IsChecked = false;
            }
            finally
            {
                IsApplyingState = false;
            }

            detailPane.Visibility = Visibility.Collapsed;
        }

        public void SaveCurrentNotesAndTags(
            string notesText,
            bool knownCynoOverride,
            bool baitOverride,
            PilotBoardRow? selectedRow)
        {
            if (string.IsNullOrWhiteSpace(_activeDetailCharacterName))
            {
                return;
            }

            _notesRepository.SaveNotesAndTags(
                _activeDetailCharacterName,
                notesText ?? string.Empty,
                knownCynoOverride,
                baitOverride);

            if (selectedRow != null &&
                string.Equals(selectedRow.CharacterName, _activeDetailCharacterName, StringComparison.OrdinalIgnoreCase))
            {
                selectedRow.KnownCynoOverride = knownCynoOverride;
                selectedRow.BaitOverride = baitOverride;
            }
        }

        public bool TryApplyKnownCynoOverrideChange(
            bool knownCynoOverride,
            string notesText,
            bool baitOverride,
            PilotBoardRow? selectedRow)
        {
            if (IsApplyingState)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(_activeDetailCharacterName))
            {
                return false;
            }

            SaveCurrentNotesAndTags(notesText, knownCynoOverride, baitOverride, selectedRow);

            if (selectedRow == null)
            {
                return false;
            }

            selectedRow.KnownCynoOverride = knownCynoOverride;
            return true;
        }

        public bool TryApplyBaitOverrideChange(
            bool knownCynoOverride,
            string notesText,
            bool baitOverride,
            PilotBoardRow? selectedRow)
        {
            if (IsApplyingState)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(_activeDetailCharacterName))
            {
                return false;
            }

            SaveCurrentNotesAndTags(notesText, knownCynoOverride, baitOverride, selectedRow);

            if (selectedRow == null)
            {
                return false;
            }

            selectedRow.BaitOverride = baitOverride;
            return true;
        }

        private void ApplyDetailPaneText(
            PilotBoardRow row,
            TextBlock selectedCharacterText,
            TextBlock fullCorpText,
            TextBlock fullAllianceText,
            TextBlock freshnessText,
            TextBlock recentPublicActivityText,
            TextBlock cynoSignalText,
            ProgressBar cynoConfidenceBar,
            TextBlock cynoEvidenceText,
            TextBlock cynoLimitationsText,
            TextBlock explainabilityText)
        {
            selectedCharacterText.Text = row.CharacterName;
            fullCorpText.Text = _pilotBoardRowDetailFormatter.GetCorpDisplayText(row);
            fullAllianceText.Text = _pilotBoardRowDetailFormatter.GetAllianceDisplayText(row);
            freshnessText.Text = _pilotBoardRowDetailFormatter.GetFreshnessDisplayText(row);
            recentPublicActivityText.Text = _pilotBoardRowDetailFormatter.GetRecentPublicActivityText(row);
            var cynoSignal = _pilotBoardRowDetailFormatter.GetCynoSignal(row);
            cynoSignalText.Text = _pilotBoardRowDetailFormatter.GetCynoSignalText(cynoSignal);
            cynoConfidenceBar.Value = cynoSignal.Score;
            var signalBrushKey = GetCynoSignalBrushKey(cynoSignal.Status);
            cynoSignalText.SetResourceReference(TextBlock.ForegroundProperty, signalBrushKey);
            cynoConfidenceBar.SetResourceReference(ProgressBar.ForegroundProperty, signalBrushKey);
            cynoEvidenceText.Text = _pilotBoardRowDetailFormatter.GetCynoEvidenceText(cynoSignal);
            cynoLimitationsText.Text = _pilotBoardRowDetailFormatter.GetCynoLimitationsText(cynoSignal);
            explainabilityText.Text = _pilotBoardRowDetailFormatter.GetExplainabilityText(row);
        }

        private static string GetCynoSignalBrushKey(CynoSignalStatus status)
        {
            return status switch
            {
                CynoSignalStatus.Confirmed => "ThreatCriticalBrush",
                CynoSignalStatus.Likely => "ThreatHighBrush",
                CynoSignalStatus.Possible => "WarningAmberBrush",
                CynoSignalStatus.Inferred => "ThreatLowBrush",
                _ => "AccentAshBrush"
            };
        }
    }
}
