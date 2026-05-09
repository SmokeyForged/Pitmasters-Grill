using PitmastersGrill.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;

namespace PitmastersGrill.Services
{
    public sealed class BoardSortController
    {
        private string? _activeBoardSortMemberPath = nameof(PilotBoardRow.CharacterName);
        private ListSortDirection? _activeBoardSortDirection = ListSortDirection.Ascending;

        public bool TryHandleSorting(
            DataGrid? pilotBoard,
            DataGridColumn? column,
            ObservableCollection<PilotBoardRow> currentRows,
            PilotBoardRow? selectedRow,
            out string sortMemberPath,
            out ListSortDirection nextDirection)
        {
            sortMemberPath = string.Empty;
            nextDirection = ListSortDirection.Ascending;

            if (pilotBoard == null || column == null)
            {
                return false;
            }

            sortMemberPath = column.SortMemberPath;
            if (string.IsNullOrWhiteSpace(sortMemberPath))
            {
                sortMemberPath = GetSortMemberPathFromColumn(column) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(sortMemberPath))
                {
                    return false;
                }
            }

            nextDirection = _activeBoardSortMemberPath == sortMemberPath &&
                            _activeBoardSortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            _activeBoardSortMemberPath = sortMemberPath;
            _activeBoardSortDirection = nextDirection;

            ApplySortIndicatorState(pilotBoard, column, nextDirection);
            ApplyCurrentBoardOrdering(currentRows, selectedRow, row => pilotBoard.SelectedItem = row);
            return true;
        }

        public void ApplyCurrentBoardOrdering(
            ObservableCollection<PilotBoardRow> currentRows,
            PilotBoardRow? selectedRow,
            Action<PilotBoardRow?> restoreSelectedRow)
        {
            if (currentRows == null)
            {
                throw new ArgumentNullException(nameof(currentRows));
            }

            if (restoreSelectedRow == null)
            {
                throw new ArgumentNullException(nameof(restoreSelectedRow));
            }

            if (currentRows.Count <= 1)
            {
                return;
            }

            var baseOrderIndexes = currentRows
                .Select((row, index) => new KeyValuePair<PilotBoardRow, int>(row, index))
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            var reorderedRows = currentRows
                .OrderBy(row => row, Comparer<PilotBoardRow>.Create((leftRow, rightRow) =>
                    CompareBoardRows(
                        leftRow,
                        baseOrderIndexes[leftRow],
                        rightRow,
                        baseOrderIndexes[rightRow])))
                .ToList();

            var changed = false;
            for (var index = 0; index < reorderedRows.Count; index++)
            {
                if (!ReferenceEquals(currentRows[index], reorderedRows[index]))
                {
                    changed = true;
                    break;
                }
            }

            if (!changed)
            {
                return;
            }

            currentRows.Clear();
            foreach (var row in reorderedRows)
            {
                currentRows.Add(row);
            }

            if (selectedRow != null && currentRows.Contains(selectedRow))
            {
                restoreSelectedRow(selectedRow);
            }
        }

        public void ResetManualBoardSort(DataGrid? pilotBoard, DataGridColumn? defaultColumn)
        {
            _activeBoardSortMemberPath = nameof(PilotBoardRow.CharacterName);
            _activeBoardSortDirection = ListSortDirection.Ascending;

            if (pilotBoard == null)
            {
                return;
            }

            if (defaultColumn != null)
            {
                ApplySortIndicatorState(pilotBoard, defaultColumn, ListSortDirection.Ascending);
                return;
            }

            ClearBoardSortIndicators(pilotBoard);
        }

        private int CompareBoardRows(PilotBoardRow leftRow, int leftIndex, PilotBoardRow rightRow, int rightIndex)
        {
            var watchedCompare = Comparer<bool>.Default.Compare(rightRow.IsWatched, leftRow.IsWatched);
            if (watchedCompare != 0)
            {
                return watchedCompare;
            }

            if (!string.IsNullOrWhiteSpace(_activeBoardSortMemberPath) && _activeBoardSortDirection.HasValue)
            {
                var valueCompare = CompareSortValues(
                    GetBoardSortValue(leftRow, _activeBoardSortMemberPath),
                    GetBoardSortValue(rightRow, _activeBoardSortMemberPath));

                if (valueCompare != 0)
                {
                    return _activeBoardSortDirection == ListSortDirection.Descending
                        ? -valueCompare
                        : valueCompare;
                }
            }

            return leftIndex.CompareTo(rightIndex);
        }

        private static int CompareSortValues(object? leftValue, object? rightValue)
        {
            if (leftValue == null && rightValue == null)
            {
                return 0;
            }

            if (leftValue == null)
            {
                return -1;
            }

            if (rightValue == null)
            {
                return 1;
            }

            if (leftValue is string leftString && rightValue is string rightString)
            {
                return StringComparer.OrdinalIgnoreCase.Compare(leftString, rightString);
            }

            if (leftValue is IComparable comparable)
            {
                try
                {
                    return comparable.CompareTo(rightValue);
                }
                catch (ArgumentException)
                {
                    return StringComparer.OrdinalIgnoreCase.Compare(
                        leftValue.ToString(),
                        rightValue.ToString());
                }
            }

            return StringComparer.OrdinalIgnoreCase.Compare(
                leftValue.ToString(),
                rightValue.ToString());
        }

        private static object? GetBoardSortValue(PilotBoardRow row, string? sortMemberPath)
        {
            if (row == null || string.IsNullOrWhiteSpace(sortMemberPath))
            {
                return null;
            }

            return sortMemberPath switch
            {
                nameof(PilotBoardRow.CharacterName) => row.CharacterName,
                nameof(PilotBoardRow.AllianceNameDisplay) => row.AllianceNameDisplay,
                nameof(PilotBoardRow.CorpNameDisplay) => row.CorpNameDisplay,
                nameof(PilotBoardRow.KillCount) => row.KillCount,
                nameof(PilotBoardRow.LossCount) => row.LossCount,
                nameof(PilotBoardRow.AvgAttackersWhenAttacking) => row.AvgAttackersWhenAttacking,
                nameof(PilotBoardRow.LastShipSeenName) => row.LastShipSeenName,
                nameof(PilotBoardRow.LastShipSeenDateDisplay) => row.LastShipSeenAtUtc,
                nameof(PilotBoardRow.LastShipSeenAtUtc) => row.LastShipSeenAtUtc,
                nameof(PilotBoardRow.LastPublicCynoCapableHull) => row.LastPublicCynoCapableHull,
                _ => GetBoardSortValueByReflection(row, sortMemberPath)
            };
        }

        private static object? GetBoardSortValueByReflection(PilotBoardRow row, string sortMemberPath)
        {
            var property = typeof(PilotBoardRow).GetProperty(sortMemberPath);
            return property?.GetValue(row);
        }

        private static void ApplySortIndicatorState(DataGrid pilotBoard, DataGridColumn activeColumn, ListSortDirection direction)
        {
            foreach (var column in pilotBoard.Columns)
            {
                column.SortDirection = ReferenceEquals(column, activeColumn)
                    ? direction
                    : null;
            }
        }

        private static void ClearBoardSortIndicators(DataGrid pilotBoard)
        {
            foreach (var column in pilotBoard.Columns)
            {
                column.SortDirection = null;
            }
        }

        private static string? GetSortMemberPathFromColumn(DataGridColumn column)
        {
            if (column is DataGridBoundColumn boundColumn &&
                boundColumn.Binding is Binding binding &&
                binding.Path != null)
            {
                return binding.Path.Path;
            }

            return null;
        }
    }
}
