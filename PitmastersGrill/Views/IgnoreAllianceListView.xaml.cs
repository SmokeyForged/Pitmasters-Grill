using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PitmastersGrill.Views
{
    public partial class IgnoreAllianceListView : UserControl
    {
        private readonly ObservableCollection<TypedIgnoreEntry> _draftEntries = new();
        private IgnoreAllianceCoordinator? _ignoreAllianceCoordinator;

        public IgnoreAllianceListView()
        {
            InitializeComponent();
            IgnoredEntriesGrid.ItemsSource = _draftEntries;
            IgnoreTypeComboBox.SelectedIndex = 2;
        }

        public event EventHandler? IgnoreListChanged;

        public void Initialize(IgnoreAllianceCoordinator ignoreAllianceCoordinator)
        {
            _ignoreAllianceCoordinator = ignoreAllianceCoordinator
                ?? throw new ArgumentNullException(nameof(ignoreAllianceCoordinator));

            ReloadDraftFromCoordinator();
            SetStatus($"Loaded {_draftEntries.Count} ignored ID(s).");
        }

        private void AddIgnoreIdsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_ignoreAllianceCoordinator == null)
            {
                return;
            }

            var rawEntries = SplitRawIds(IgnoreIdsInputTextBox.Text);
            var type = GetSelectedType();
            var mergeResult = _ignoreAllianceCoordinator.MergeWithExisting(
                _draftEntries,
                rawEntries,
                type,
                "ignore list manual entry");

            ReplaceDraftEntries(mergeResult.Entries);
            IgnoreIdsInputTextBox.Clear();

            if (mergeResult.InvalidEntries.Count > 0)
            {
                SetStatus($"Added valid IDs. Ignored invalid entries: {string.Join(", ", mergeResult.InvalidEntries)}");
                return;
            }

            SetStatus($"Draft ignore list now contains {_draftEntries.Count} ID(s). Click Save List to persist.");
        }

        private void SaveIgnoreListButton_Click(object sender, RoutedEventArgs e)
        {
            if (_ignoreAllianceCoordinator == null)
            {
                return;
            }

            _ignoreAllianceCoordinator.ReplaceAndPersist(_draftEntries);
            ReloadDraftFromCoordinator();
            SetStatus($"Saved {_draftEntries.Count} ignored ID(s).");
            IgnoreListChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RemoveSelectedIgnoreButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = IgnoredEntriesGrid.SelectedItems
                .OfType<TypedIgnoreEntry>()
                .ToList();

            if (selected.Count == 0)
            {
                SetStatus("Select one or more ignore entries to remove.");
                return;
            }

            foreach (var entry in selected)
            {
                _draftEntries.Remove(entry);
            }

            SetStatus($"Removed {selected.Count} draft ignore entr{(selected.Count == 1 ? "y" : "ies")}. Click Save List to persist.");
        }

        private void ClearIgnoreListButton_Click(object sender, RoutedEventArgs e)
        {
            if (_ignoreAllianceCoordinator == null)
            {
                return;
            }

            _ignoreAllianceCoordinator.ClearAndPersist();
            ReloadDraftFromCoordinator();
            IgnoreIdsInputTextBox.Clear();
            SetStatus("Cleared all ignored IDs.");
            IgnoreListChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RefreshNamesButton_Click(object sender, RoutedEventArgs e)
        {
            SetStatus("Name refresh is non-blocking and currently uses saved names from PMG observations. Unresolved entries still suppress by ID.");
        }

        public void RefreshFromCoordinator()
        {
            ReloadDraftFromCoordinator();
            SetStatus($"Loaded {_draftEntries.Count} ignored ID(s).");
        }

        private void ReloadDraftFromCoordinator()
        {
            if (_ignoreAllianceCoordinator == null)
            {
                _draftEntries.Clear();
                return;
            }

            ReplaceDraftEntries(_ignoreAllianceCoordinator.GetIgnoredEntries());
        }

        private void ReplaceDraftEntries(IEnumerable<TypedIgnoreEntry> entries)
        {
            _draftEntries.Clear();

            foreach (var entry in entries
                         .Where(x => x.Id > 0)
                         .OrderBy(x => x.Type)
                         .ThenBy(x => x.Id))
            {
                _draftEntries.Add(entry);
            }
        }

        private IgnoreEntryType GetSelectedType()
        {
            return IgnoreTypeComboBox.SelectedIndex switch
            {
                0 => IgnoreEntryType.Pilot,
                1 => IgnoreEntryType.Corporation,
                _ => IgnoreEntryType.Alliance
            };
        }

        private static IReadOnlyList<string> SplitRawIds(string input)
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
