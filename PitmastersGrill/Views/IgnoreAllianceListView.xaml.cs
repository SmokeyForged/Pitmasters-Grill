using PitmastersGrill.Services;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PitmastersGrill.Views
{
    public partial class IgnoreAllianceListView : UserControl
    {
        private readonly ObservableCollection<long> _draftAllianceIds = new();
        private IgnoreAllianceCoordinator? _ignoreAllianceCoordinator;

        public IgnoreAllianceListView()
        {
            InitializeComponent();
            IgnoredAllianceIdsListBox.ItemsSource = _draftAllianceIds;
        }

        public event EventHandler? IgnoreListChanged;

        public void Initialize(IgnoreAllianceCoordinator ignoreAllianceCoordinator)
        {
            _ignoreAllianceCoordinator = ignoreAllianceCoordinator
                ?? throw new ArgumentNullException(nameof(ignoreAllianceCoordinator));

            ReloadDraftFromCoordinator();
            SetStatus($"Loaded {_draftAllianceIds.Count} ignored alliance ID(s).");
        }

        private void AddAllianceIdsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_ignoreAllianceCoordinator == null)
            {
                return;
            }

            var rawEntries = SplitRawAllianceIds(AllianceIdsInputTextBox.Text);
            var mergeResult = _ignoreAllianceCoordinator.MergeWithExisting(_draftAllianceIds, rawEntries);

            ReplaceDraftAllianceIds(mergeResult.NormalizedAllianceIds);
            AllianceIdsInputTextBox.Clear();

            if (mergeResult.InvalidEntries.Count > 0)
            {
                SetStatus($"Added valid IDs. Ignored invalid entries: {string.Join(", ", mergeResult.InvalidEntries)}");
                return;
            }

            SetStatus($"Draft ignore list now contains {_draftAllianceIds.Count} alliance ID(s). Click Save List to persist.");
        }

        private void SaveIgnoreListButton_Click(object sender, RoutedEventArgs e)
        {
            if (_ignoreAllianceCoordinator == null)
            {
                return;
            }

            _ignoreAllianceCoordinator.ReplaceAndPersist(_draftAllianceIds);
            ReloadDraftFromCoordinator();
            SetStatus($"Saved {_draftAllianceIds.Count} ignored alliance ID(s).");
            IgnoreListChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ClearIgnoreListButton_Click(object sender, RoutedEventArgs e)
        {
            if (_ignoreAllianceCoordinator == null)
            {
                return;
            }

            _ignoreAllianceCoordinator.ClearAndPersist();
            ReloadDraftFromCoordinator();
            AllianceIdsInputTextBox.Clear();
            SetStatus("Cleared all ignored alliance IDs.");
            IgnoreListChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RefreshFromCoordinator()
        {
            ReloadDraftFromCoordinator();
            SetStatus($"Loaded {_draftAllianceIds.Count} ignored alliance ID(s).");
        }

        private void ReloadDraftFromCoordinator()
        {
            if (_ignoreAllianceCoordinator == null)
            {
                _draftAllianceIds.Clear();
                return;
            }

            ReplaceDraftAllianceIds(_ignoreAllianceCoordinator.GetIgnoredAllianceIds());
        }

        private void ReplaceDraftAllianceIds(IEnumerable<long> allianceIds)
        {
            _draftAllianceIds.Clear();

            foreach (var allianceId in allianceIds.OrderBy(x => x))
            {
                _draftAllianceIds.Add(allianceId);
            }
        }

        private static IReadOnlyList<string> SplitRawAllianceIds(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return Array.Empty<string>();
            }

            return input
                .Split(new[] { '\r', '\n', ',', ';', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
        }

        private void SetStatus(string message)
        {
            IgnoreListStatusText.Text = message ?? string.Empty;
        }
    }
}
