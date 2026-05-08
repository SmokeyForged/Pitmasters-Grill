using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class AnalysisTabPresenterTests
    {
        [Fact]
        public void UpdateBoardSummary_SetsExpectedBannerText()
        {
            RunOnStaThread(() =>
            {
                var controls = CreateControls();
                var presenter = CreatePresenter(controls);
                var rows = new List<PilotBoardRow>
                {
                    new() { CharacterName = "Scout One", IsWatched = true, BoardSignalKind = "ConfirmedNormal" },
                    new() { CharacterName = "Scout Two", HasDerivedBaitEvidence = true, BoardSignalKind = "ConfirmedCovert" }
                };

                presenter.UpdateBoardSummary(rows);

                Assert.Equal("Visible 2 | Watched 1 | Bait 1 | Hard Cyno 1 | Covert Cyno 1", controls.BoardSummaryText.Text);
            });
        }

        [Fact]
        public void UpdateAnalysisTab_WithNoRows_ShowsEmptyState()
        {
            RunOnStaThread(() =>
            {
                var controls = CreateControls();
                var presenter = CreatePresenter(controls);

                presenter.UpdateAnalysisTab(new List<PilotBoardRow>());

                Assert.Equal(Visibility.Visible, controls.AnalysisEmptyStateText.Visibility);
                Assert.Equal(Visibility.Collapsed, controls.AnalysisDetailsPanel.Visibility);
                Assert.Equal("No visible pilots yet. Load or refresh the Grill to see aggregate analysis.", controls.AnalysisEmptyStateText.Text);
            });
        }

        [Fact]
        public void UpdateAnalysisTab_WithRows_PopulatesTextsListsAndHyperlinks()
        {
            RunOnStaThread(() =>
            {
                var controls = CreateControls();
                var presenter = CreatePresenter(controls);
                var rows = new List<PilotBoardRow>
                {
                    new()
                    {
                        CharacterName = "Alice",
                        CharacterId = "9001",
                        AllianceName = "Alliance A",
                        AllianceId = "3001",
                        CorpName = "Corp One",
                        CorpId = "2001",
                        IsWatched = true,
                        BoardSignalKind = "ConfirmedNormal",
                        KillCount = 5,
                        LossCount = 0
                    },
                    new()
                    {
                        CharacterName = "Bob",
                        CharacterId = "",
                        AllianceName = "Alliance A",
                        CorpName = "Corp One",
                        HasDerivedBaitEvidence = true,
                        BoardSignalKind = "ConfirmedCovert",
                        KillCount = 1,
                        LossCount = 1
                    },
                    new()
                    {
                        CharacterName = "Cara",
                        CharacterId = "9003",
                        AllianceName = "Alliance B",
                        AllianceId = "3002",
                        CorpName = "Corp Two",
                        CorpId = "2002",
                        BaitOverride = true
                    }
                };

                presenter.UpdateAnalysisTab(rows);

                Assert.Equal(Visibility.Collapsed, controls.AnalysisEmptyStateText.Visibility);
                Assert.Equal(Visibility.Visible, controls.AnalysisDetailsPanel.Visibility);
                Assert.Equal("Visible pilots: 3 | Watched pilots: 1 | Confirmed cynos: Hard 1 | Covert 1 | Bait 2", controls.AnalysisVisibleCountsText.Text);
                Assert.Equal("Unique corps: 2 | Unique alliances: 2", controls.AnalysisUniqueCountsText.Text);
                Assert.Equal(2, controls.AnalysisAllianceItems.Count);
                Assert.Equal(2, controls.AnalysisCorpItems.Count);
                Assert.Equal(string.Empty, controls.AnalysisSignalsText.Text);

                var allianceLink = controls.AnalysisAllianceTopText.Inlines.OfType<Hyperlink>().FirstOrDefault();
                Assert.NotNull(allianceLink);
                Assert.Equal("https://zkillboard.com/alliance/3001/", allianceLink!.NavigateUri?.AbsoluteUri);

                var highlightLink = controls.AnalysisHighlightsText.Inlines.OfType<Hyperlink>().FirstOrDefault();
                Assert.NotNull(highlightLink);
                Assert.Equal("https://zkillboard.com/character/9001/", highlightLink!.NavigateUri?.AbsoluteUri);
                Assert.Equal("Top alliances: ", ((Run)controls.AnalysisAllianceTopText.Inlines.FirstInline!).Text);
                Assert.Equal("Highlights: ", ((Run)controls.AnalysisHighlightsText.Inlines.FirstInline!).Text);
            });
        }

        private static AnalysisTabPresenter CreatePresenter(AnalysisPresenterControls controls)
        {
            return new AnalysisTabPresenter(
                new AnalysisTabController(),
                new ZkillUrlBuilder(),
                (_, _) => { },
                controls.BoardSummaryText,
                controls.AnalysisEmptyStateText,
                controls.AnalysisDetailsPanel,
                controls.AnalysisVisibleCountsText,
                controls.AnalysisUniqueCountsText,
                controls.AnalysisAllianceTopText,
                controls.AnalysisCorpTopText,
                controls.AnalysisSignalsText,
                controls.AnalysisHighlightsText,
                controls.AnalysisAllianceItems,
                controls.AnalysisCorpItems);
        }

        private static AnalysisPresenterControls CreateControls()
        {
            return new AnalysisPresenterControls(
                new TextBlock(),
                new TextBlock(),
                new StackPanel(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new ObservableCollection<AnalysisAffiliationListItem>(),
                new ObservableCollection<AnalysisAffiliationListItem>());
        }

        private sealed record AnalysisPresenterControls(
            TextBlock BoardSummaryText,
            TextBlock AnalysisEmptyStateText,
            StackPanel AnalysisDetailsPanel,
            TextBlock AnalysisVisibleCountsText,
            TextBlock AnalysisUniqueCountsText,
            TextBlock AnalysisAllianceTopText,
            TextBlock AnalysisCorpTopText,
            TextBlock AnalysisSignalsText,
            TextBlock AnalysisHighlightsText,
            ObservableCollection<AnalysisAffiliationListItem> AnalysisAllianceItems,
            ObservableCollection<AnalysisAffiliationListItem> AnalysisCorpItems);

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
