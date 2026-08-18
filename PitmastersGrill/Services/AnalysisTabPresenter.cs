using PitmastersGrill.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace PitmastersGrill.Services
{
    public sealed class AnalysisTabPresenter
    {
        private readonly AnalysisTabController _analysisTabController;
        private readonly ZkillUrlBuilder _zkillUrlBuilder;
        private readonly RequestNavigateEventHandler _analysisHyperlinkRequestNavigate;
        private readonly Action<string>? _setBoardSummaryText;
        private readonly TextBlock? _analysisEmptyStateText;
        private readonly UIElement? _analysisDetailsPanel;
        private readonly TextBlock? _analysisVisibleCountsText;
        private readonly TextBlock? _analysisUniqueCountsText;
        private readonly TextBlock? _analysisAllianceTopText;
        private readonly TextBlock? _analysisCorpTopText;
        private readonly TextBlock? _analysisSignalsText;
        private readonly TextBlock? _analysisHighlightsText;
        private readonly ObservableCollection<AnalysisAffiliationListItem> _analysisAllianceItems;
        private readonly ObservableCollection<AnalysisAffiliationListItem> _analysisCorpItems;

        public AnalysisTabPresenter(
            AnalysisTabController analysisTabController,
            ZkillUrlBuilder zkillUrlBuilder,
            RequestNavigateEventHandler analysisHyperlinkRequestNavigate,
            Action<string>? setBoardSummaryText,
            TextBlock? analysisEmptyStateText,
            UIElement? analysisDetailsPanel,
            TextBlock? analysisVisibleCountsText,
            TextBlock? analysisUniqueCountsText,
            TextBlock? analysisAllianceTopText,
            TextBlock? analysisCorpTopText,
            TextBlock? analysisSignalsText,
            TextBlock? analysisHighlightsText,
            ObservableCollection<AnalysisAffiliationListItem> analysisAllianceItems,
            ObservableCollection<AnalysisAffiliationListItem> analysisCorpItems)
        {
            _analysisTabController = analysisTabController ?? throw new ArgumentNullException(nameof(analysisTabController));
            _zkillUrlBuilder = zkillUrlBuilder ?? throw new ArgumentNullException(nameof(zkillUrlBuilder));
            _analysisHyperlinkRequestNavigate = analysisHyperlinkRequestNavigate ?? throw new ArgumentNullException(nameof(analysisHyperlinkRequestNavigate));
            _setBoardSummaryText = setBoardSummaryText;
            _analysisEmptyStateText = analysisEmptyStateText;
            _analysisDetailsPanel = analysisDetailsPanel;
            _analysisVisibleCountsText = analysisVisibleCountsText;
            _analysisUniqueCountsText = analysisUniqueCountsText;
            _analysisAllianceTopText = analysisAllianceTopText;
            _analysisCorpTopText = analysisCorpTopText;
            _analysisSignalsText = analysisSignalsText;
            _analysisHighlightsText = analysisHighlightsText;
            _analysisAllianceItems = analysisAllianceItems ?? throw new ArgumentNullException(nameof(analysisAllianceItems));
            _analysisCorpItems = analysisCorpItems ?? throw new ArgumentNullException(nameof(analysisCorpItems));
        }

        public void UpdateBoardSummary(IReadOnlyList<PilotBoardRow> currentRows)
        {
            if (_setBoardSummaryText == null)
            {
                return;
            }

            _setBoardSummaryText(BoardSummaryTextBuilder.Build(currentRows));
        }

        public void UpdateAnalysisTab(IReadOnlyList<PilotBoardRow> currentRows)
        {
            if (_analysisEmptyStateText == null ||
                _analysisDetailsPanel == null ||
                _analysisVisibleCountsText == null ||
                _analysisUniqueCountsText == null ||
                _analysisAllianceTopText == null ||
                _analysisCorpTopText == null ||
                _analysisSignalsText == null ||
                _analysisHighlightsText == null)
            {
                return;
            }

            var summary = _analysisTabController.BuildSummary(currentRows);
            if (!summary.HasVisibleRows)
            {
                _analysisEmptyStateText.Visibility = Visibility.Visible;
                _analysisDetailsPanel.Visibility = Visibility.Collapsed;
                _analysisEmptyStateText.Text = summary.EmptyStateText;
                return;
            }

            _analysisEmptyStateText.Visibility = Visibility.Collapsed;
            _analysisDetailsPanel.Visibility = Visibility.Visible;
            _analysisVisibleCountsText.Text = summary.VisibleCountsText;
            _analysisUniqueCountsText.Text = summary.UniqueCountsText;
            PopulateAnalysisAffiliationSummaryText(_analysisAllianceTopText, "Top alliances: ", summary.TopAlliances, BuildAllianceZkillUrl);
            PopulateAnalysisAffiliationSummaryText(_analysisCorpTopText, "Top corps: ", summary.TopCorps, BuildCorporationZkillUrl);
            PopulateAnalysisAffiliationList(_analysisAllianceItems, _analysisTabController.BuildAffiliationListItems(summary.AllAlliances, "alliance"));
            PopulateAnalysisAffiliationList(_analysisCorpItems, _analysisTabController.BuildAffiliationListItems(summary.AllCorps, "corporation"));
            _analysisSignalsText.Text = string.Empty;
            PopulateAnalysisHighlightsText(summary.Highlights);
        }

        public string BuildAllianceZkillUrl(string allianceId)
        {
            return string.IsNullOrWhiteSpace(allianceId)
                ? string.Empty
                : $"https://zkillboard.com/alliance/{Uri.EscapeDataString(allianceId.Trim())}/";
        }

        public string BuildCorporationZkillUrl(string corporationId)
        {
            return string.IsNullOrWhiteSpace(corporationId)
                ? string.Empty
                : $"https://zkillboard.com/corporation/{Uri.EscapeDataString(corporationId.Trim())}/";
        }

        private void PopulateAnalysisAffiliationSummaryText(
            TextBlock target,
            string prefix,
            IReadOnlyList<AnalysisAffiliationSummary> summaries,
            Func<string, string> buildUrl)
        {
            target.Inlines.Clear();
            target.Inlines.Add(new Run(prefix));

            if (summaries.Count == 0)
            {
                target.Inlines.Add(new Run("none visible"));
                return;
            }

            for (var index = 0; index < summaries.Count; index++)
            {
                if (index > 0)
                {
                    target.Inlines.Add(new Run(" | "));
                }

                var summary = summaries[index];
                if (!string.IsNullOrWhiteSpace(summary.Id) &&
                    long.TryParse(summary.Id, out _))
                {
                    AddHyperlinkInline(
                        target,
                        summary.Name,
                        buildUrl(summary.Id),
                        $"Open {summary.Name} on zKill");
                }
                else
                {
                    target.Inlines.Add(new Run(summary.Name));
                }

                target.Inlines.Add(new Run($" [{summary.Count}]"));
            }
        }

        private void PopulateAnalysisHighlightsText(IReadOnlyList<AnalysisHighlightSummary> highlights)
        {
            _analysisHighlightsText!.Inlines.Clear();
            _analysisHighlightsText.Inlines.Add(new Run("Highlights: "));

            var addedAny = false;
            for (var index = 0; index < highlights.Count; index++)
            {
                var highlight = highlights[index];
                AddHighlightCharacterLink(
                    _analysisHighlightsText,
                    highlight.Label,
                    highlight.CharacterName,
                    highlight.CharacterId,
                    highlight.ValueText,
                    ref addedAny);
            }

            if (!addedAny)
            {
                _analysisHighlightsText.Inlines.Add(new Run("none visible"));
            }
        }

        private void AddHighlightCharacterLink(
            TextBlock target,
            string label,
            string characterName,
            string characterId,
            string valueText,
            ref bool addedAny)
        {
            if (addedAny)
            {
                target.Inlines.Add(new Run(" | "));
            }

            if (!string.IsNullOrWhiteSpace(label))
            {
                target.Inlines.Add(new Run($"{label}: "));
            }

            var hasCharacterId = !string.IsNullOrWhiteSpace(characterId) && long.TryParse(characterId, out _);
            if (hasCharacterId)
            {
                AddHyperlinkInline(
                    target,
                    characterName,
                    _zkillUrlBuilder.BuildCharacterUrl(characterId),
                    $"Open {characterName} on zKill");
            }
            else
            {
                target.Inlines.Add(new Run(characterName));
            }

            target.Inlines.Add(new Run($" [{valueText}]"));
            addedAny = true;
        }

        private void AddHyperlinkInline(TextBlock target, string text, string url, string toolTip)
        {
            var hyperlink = new Hyperlink(new Run(text))
            {
                NavigateUri = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null,
                ToolTip = toolTip
            };
            hyperlink.RequestNavigate += _analysisHyperlinkRequestNavigate;
            target.Inlines.Add(hyperlink);
        }

        private static void PopulateAnalysisAffiliationList(
            ObservableCollection<AnalysisAffiliationListItem> target,
            IReadOnlyList<AnalysisAffiliationListItem> items)
        {
            target.Clear();
            foreach (var item in items)
            {
                target.Add(item);
            }
        }
    }
}
