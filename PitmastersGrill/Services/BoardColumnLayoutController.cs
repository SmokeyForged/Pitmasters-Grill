using PitmastersGrill.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PitmastersGrill.Services
{
    public sealed class BoardColumnLayoutController
    {
        private static readonly string[] CanonicalBoardColumnOrder =
        {
            "SigColumn",
            "CharacterColumn",
            "AllianceColumn",
            "CorpColumn",
            "KillsColumn",
            "LossesColumn",
            "AvgFleetSizeColumn",
            "LastShipSeenColumn",
            "LastSeenColumn",
            "CynoHullSeenColumn"
        };

        private static readonly Dictionary<string, double> CanonicalBoardColumnMinimumWidths = new(StringComparer.OrdinalIgnoreCase)
        {
            ["SigColumn"] = 34,
            ["CharacterColumn"] = 72,
            ["AllianceColumn"] = 72,
            ["CorpColumn"] = 82,
            ["KillsColumn"] = 42,
            ["LossesColumn"] = 42,
            ["AvgFleetSizeColumn"] = 48,
            ["LastShipSeenColumn"] = 64,
            ["LastSeenColumn"] = 54,
            ["CynoHullSeenColumn"] = 70
        };

        private static readonly Dictionary<string, double> CanonicalBoardColumnDefaultWidths = new(StringComparer.OrdinalIgnoreCase)
        {
            ["SigColumn"] = 42,
            ["CharacterColumn"] = 120,
            ["AllianceColumn"] = 120,
            ["CorpColumn"] = 140,
            ["KillsColumn"] = 55,
            ["LossesColumn"] = 55,
            ["AvgFleetSizeColumn"] = 70,
            ["LastShipSeenColumn"] = 100,
            ["LastSeenColumn"] = 90,
            ["CynoHullSeenColumn"] = 110
        };

        private readonly Dictionary<string, DataGridColumn> _boardColumnsByKey = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, BoardColumnLayoutSetting> _defaultBoardColumnLayout = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, DataGridColumn> BoardColumnsByKey => _boardColumnsByKey;

        public void InitializeColumns(params (string Key, DataGridColumn Column)[] columns)
        {
            _boardColumnsByKey.Clear();

            foreach (var (key, column) in columns)
            {
                if (column != null)
                {
                    _boardColumnsByKey[key] = column;
                }
            }
        }

        public void ApplyColumnMinimumWidths()
        {
            foreach (var pair in _boardColumnsByKey)
            {
                pair.Value.MinWidth = GetBoardColumnMinimumWidth(pair.Key);
            }
        }

        public void BuildCanonicalBoardColumnLayout()
        {
            _defaultBoardColumnLayout.Clear();

            for (var index = 0; index < CanonicalBoardColumnOrder.Length; index++)
            {
                var key = CanonicalBoardColumnOrder[index];
                if (_boardColumnsByKey.ContainsKey(key))
                {
                    _defaultBoardColumnLayout[key] = CreateCanonicalBoardColumnLayoutSetting(key, index);
                }
            }
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
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            showSigColumnCheckBox.IsChecked = IsEnabled(settings.ShowSigColumn);
            showAllianceColumnCheckBox.IsChecked = IsEnabled(settings.ShowAllianceColumn);
            showCorpColumnCheckBox.IsChecked = IsEnabled(settings.ShowCorpColumn);
            showKillsColumnCheckBox.IsChecked = IsEnabled(settings.ShowKillsColumn);
            showLossesColumnCheckBox.IsChecked = IsEnabled(settings.ShowLossesColumn);
            showAvgFleetSizeColumnCheckBox.IsChecked = IsEnabled(settings.ShowAvgFleetSizeColumn);
            showLastShipSeenColumnCheckBox.IsChecked = IsEnabled(settings.ShowLastShipSeenColumn);
            showLastSeenColumnCheckBox.IsChecked = IsEnabled(settings.ShowLastSeenColumn);
            showCynoHullSeenColumnCheckBox.IsChecked = IsEnabled(settings.ShowCynoHullSeenColumn);

            if (showCorpAllianceCountsCheckBox != null)
            {
                showCorpAllianceCountsCheckBox.IsChecked = settings.ShowCorpAllianceCounts;
            }
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
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.ShowSigColumn = IsChecked(showSigColumnCheckBox);
            settings.ShowAllianceColumn = IsChecked(showAllianceColumnCheckBox);
            settings.ShowCorpColumn = IsChecked(showCorpColumnCheckBox);
            settings.ShowKillsColumn = IsChecked(showKillsColumnCheckBox);
            settings.ShowLossesColumn = IsChecked(showLossesColumnCheckBox);
            settings.ShowAvgFleetSizeColumn = IsChecked(showAvgFleetSizeColumnCheckBox);
            settings.ShowLastShipSeenColumn = IsChecked(showLastShipSeenColumnCheckBox);
            settings.ShowLastSeenColumn = IsChecked(showLastSeenColumnCheckBox);
            settings.ShowCynoHullSeenColumn = IsChecked(showCynoHullSeenColumnCheckBox);
        }

        public void ApplyBoardColumnVisibility(AppSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            SetColumnVisibility(_boardColumnsByKey["SigColumn"], settings.ShowSigColumn);
            _boardColumnsByKey["CharacterColumn"].Visibility = Visibility.Visible;
            SetColumnVisibility(_boardColumnsByKey["AllianceColumn"], settings.ShowAllianceColumn);
            SetColumnVisibility(_boardColumnsByKey["CorpColumn"], settings.ShowCorpColumn);
            SetColumnVisibility(_boardColumnsByKey["KillsColumn"], settings.ShowKillsColumn);
            SetColumnVisibility(_boardColumnsByKey["LossesColumn"], settings.ShowLossesColumn);
            SetColumnVisibility(_boardColumnsByKey["AvgFleetSizeColumn"], settings.ShowAvgFleetSizeColumn);
            SetColumnVisibility(_boardColumnsByKey["LastShipSeenColumn"], settings.ShowLastShipSeenColumn);
            SetColumnVisibility(_boardColumnsByKey["LastSeenColumn"], settings.ShowLastSeenColumn);
            SetColumnVisibility(_boardColumnsByKey["CynoHullSeenColumn"], settings.ShowCynoHullSeenColumn);
        }

        public void SetAllOptionalBoardColumnSettings(AppSettings settings, bool isVisible)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.ShowSigColumn = isVisible;
            settings.ShowAllianceColumn = isVisible;
            settings.ShowCorpColumn = isVisible;
            settings.ShowKillsColumn = isVisible;
            settings.ShowLossesColumn = isVisible;
            settings.ShowAvgFleetSizeColumn = isVisible;
            settings.ShowLastShipSeenColumn = isVisible;
            settings.ShowLastSeenColumn = isVisible;
            settings.ShowCynoHullSeenColumn = isVisible;
        }

        public IReadOnlyList<BoardColumnLayoutSetting> GetCanonicalBoardColumnLayout()
        {
            return CanonicalBoardColumnOrder
                .Where(key => _defaultBoardColumnLayout.ContainsKey(key))
                .Select(key => _defaultBoardColumnLayout[key])
                .ToList();
        }

        public void ApplyBoardColumnLayout(IEnumerable<BoardColumnLayoutSetting> layoutSettings)
        {
            if (layoutSettings == null)
            {
                return;
            }

            var widthSettings = layoutSettings
                .Where(setting => setting != null && !string.IsNullOrWhiteSpace(setting.ColumnKey))
                .Where(setting => _boardColumnsByKey.ContainsKey(setting.ColumnKey))
                .ToList();

            foreach (var setting in widthSettings)
            {
                if (TryBuildDataGridLength(setting, out var width))
                {
                    _boardColumnsByKey[setting.ColumnKey].Width = width;
                }
            }

            var orderedKeys = widthSettings
                .OrderBy(setting => setting.DisplayIndex)
                .ThenBy(setting => setting.ColumnKey, StringComparer.OrdinalIgnoreCase)
                .Select(setting => setting.ColumnKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var defaultKey in CanonicalBoardColumnOrder)
            {
                if (!orderedKeys.Contains(defaultKey, StringComparer.OrdinalIgnoreCase))
                {
                    orderedKeys.Add(defaultKey);
                }
            }

            for (var index = 0; index < orderedKeys.Count; index++)
            {
                if (_boardColumnsByKey.TryGetValue(orderedKeys[index], out var column) &&
                    column.DisplayIndex != index)
                {
                    column.DisplayIndex = index;
                }
            }
        }

        public List<BoardColumnLayoutSetting> CaptureCurrentBoardColumnLayout()
        {
            return _boardColumnsByKey
                .Select(pair => CreateBoardColumnLayoutSetting(pair.Key, pair.Value))
                .OrderBy(setting => setting.DisplayIndex)
                .ThenBy(setting => setting.ColumnKey, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public bool TryValidateSavedBoardColumnLayout(
            IReadOnlyList<BoardColumnLayoutSetting>? layoutSettings,
            out List<BoardColumnLayoutSetting> validLayout,
            out string failureReason)
        {
            validLayout = new List<BoardColumnLayoutSetting>();
            failureReason = string.Empty;

            if (layoutSettings == null || layoutSettings.Count == 0)
            {
                failureReason = "No layout settings were present.";
                return false;
            }

            var knownSettings = layoutSettings
                .Where(setting => setting != null && !string.IsNullOrWhiteSpace(setting.ColumnKey))
                .Where(setting => _boardColumnsByKey.ContainsKey(setting.ColumnKey))
                .GroupBy(setting => setting.ColumnKey, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            if (knownSettings.Count == 0)
            {
                failureReason = "No known board column keys were present.";
                return false;
            }

            var usedDisplayIndexes = new HashSet<int>();
            foreach (var setting in knownSettings)
            {
                if (!string.Equals(setting.WidthUnitType, DataGridLengthUnitType.Pixel.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    failureReason = $"Unsupported width unit '{setting.WidthUnitType}' for column '{setting.ColumnKey}'.";
                    return false;
                }

                if (setting.DisplayIndex < 0 || setting.DisplayIndex >= CanonicalBoardColumnOrder.Length)
                {
                    failureReason = $"DisplayIndex out of range for column '{setting.ColumnKey}'.";
                    return false;
                }

                if (!usedDisplayIndexes.Add(setting.DisplayIndex))
                {
                    failureReason = $"Duplicate DisplayIndex '{setting.DisplayIndex}' detected.";
                    return false;
                }

                if (double.IsNaN(setting.WidthValue) || double.IsInfinity(setting.WidthValue))
                {
                    failureReason = $"Non-finite width detected for column '{setting.ColumnKey}'.";
                    return false;
                }

                if (setting.WidthValue < GetBoardColumnMinimumWidth(setting.ColumnKey))
                {
                    failureReason = $"Width below minimum for column '{setting.ColumnKey}'.";
                    return false;
                }
            }

            var orderedKeys = knownSettings
                .OrderBy(setting => setting.DisplayIndex)
                .ThenBy(setting => setting.ColumnKey, StringComparer.OrdinalIgnoreCase)
                .Select(setting => setting.ColumnKey)
                .ToList();

            foreach (var key in CanonicalBoardColumnOrder)
            {
                if (!orderedKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
                {
                    orderedKeys.Add(key);
                }
            }

            for (var index = 0; index < orderedKeys.Count; index++)
            {
                var key = orderedKeys[index];
                var existing = knownSettings.FirstOrDefault(setting => string.Equals(setting.ColumnKey, key, StringComparison.OrdinalIgnoreCase));
                validLayout.Add(existing != null
                    ? new BoardColumnLayoutSetting
                    {
                        ColumnKey = existing.ColumnKey,
                        DisplayIndex = index,
                        WidthValue = existing.WidthValue,
                        WidthUnitType = existing.WidthUnitType
                    }
                    : CreateCanonicalBoardColumnLayoutSetting(key, index));
            }

            var totalWidth = validLayout.Sum(setting => Math.Max(GetBoardColumnMinimumWidth(setting.ColumnKey), setting.WidthValue));
            if (totalWidth < CanonicalBoardColumnMinimumWidths.Values.Sum() * 0.8d)
            {
                failureReason = $"Total saved width was too small. totalWidth={totalWidth:0.##}";
                return false;
            }

            return true;
        }

        public bool BoardColumnLayoutsMatch(
            IReadOnlyList<BoardColumnLayoutSetting>? left,
            IReadOnlyList<BoardColumnLayoutSetting>? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (var index = 0; index < left.Count; index++)
            {
                var leftItem = left[index];
                var rightItem = right[index];

                if (!string.Equals(leftItem?.ColumnKey, rightItem?.ColumnKey, StringComparison.OrdinalIgnoreCase) ||
                    leftItem?.DisplayIndex != rightItem?.DisplayIndex ||
                    leftItem?.WidthUnitType != rightItem?.WidthUnitType ||
                    Math.Abs((leftItem?.WidthValue ?? 0d) - (rightItem?.WidthValue ?? 0d)) > 0.01d)
                {
                    return false;
                }
            }

            return true;
        }

        public double GetBoardColumnMinimumWidth(string key)
        {
            return CanonicalBoardColumnMinimumWidths.TryGetValue(key, out var width)
                ? width
                : 50;
        }

        public string GetBoardColumnKey(DataGridColumn column)
        {
            foreach (var pair in _boardColumnsByKey)
            {
                if (ReferenceEquals(pair.Value, column))
                {
                    return pair.Key;
                }
            }

            return string.Empty;
        }

        private static bool TryBuildDataGridLength(BoardColumnLayoutSetting setting, out DataGridLength width)
        {
            width = DataGridLength.Auto;

            if (setting == null || string.IsNullOrWhiteSpace(setting.WidthUnitType))
            {
                return false;
            }

            if (double.IsNaN(setting.WidthValue) || double.IsInfinity(setting.WidthValue) || setting.WidthValue <= 0)
            {
                return false;
            }

            width = new DataGridLength(setting.WidthValue, DataGridLengthUnitType.Pixel);
            return true;
        }

        private BoardColumnLayoutSetting CreateBoardColumnLayoutSetting(string key, DataGridColumn column, int? displayIndexOverride = null)
        {
            var width = column.ActualWidth;
            if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
            {
                width = column.Width.DisplayValue;
            }

            if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
            {
                width = GetBoardColumnMinimumWidth(key);
            }

            return new BoardColumnLayoutSetting
            {
                ColumnKey = key,
                DisplayIndex = displayIndexOverride ?? column.DisplayIndex,
                WidthValue = Math.Max(GetBoardColumnMinimumWidth(key), width),
                WidthUnitType = DataGridLengthUnitType.Pixel.ToString()
            };
        }

        private BoardColumnLayoutSetting CreateCanonicalBoardColumnLayoutSetting(string key, int displayIndex)
        {
            return new BoardColumnLayoutSetting
            {
                ColumnKey = key,
                DisplayIndex = displayIndex,
                WidthValue = GetBoardColumnDefaultWidth(key),
                WidthUnitType = DataGridLengthUnitType.Pixel.ToString()
            };
        }

        private double GetBoardColumnDefaultWidth(string key)
        {
            return CanonicalBoardColumnDefaultWidths.TryGetValue(key, out var width)
                ? width
                : Math.Max(GetBoardColumnMinimumWidth(key), 80);
        }

        private static void SetColumnVisibility(DataGridColumn column, bool? isVisible)
        {
            column.Visibility = IsEnabled(isVisible) ? Visibility.Visible : Visibility.Collapsed;
        }

        private static bool IsChecked(CheckBox checkBox)
        {
            return checkBox.IsChecked == true;
        }

        private static bool IsEnabled(bool? value)
        {
            return value != false;
        }
    }
}
