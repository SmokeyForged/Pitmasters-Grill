using PitmastersGrill.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PitmastersGrill.Services
{
    public sealed class IgnoreAllianceBoardApplyResult
    {
        public IgnoreAllianceBoardApplyResult(
            IReadOnlyList<PilotBoardRow> removedRows,
            bool selectedRowRemoved)
        {
            RemovedRows = removedRows ?? throw new ArgumentNullException(nameof(removedRows));
            SelectedRowRemoved = selectedRowRemoved;
        }

        public IReadOnlyList<PilotBoardRow> RemovedRows { get; }
        public bool SelectedRowRemoved { get; }
        public int RemovedCount => RemovedRows.Count;
    }

    public sealed class IgnoreAllianceBoardController
    {
        public bool HasIgnoredAllianceIds => _ignoreAllianceCoordinator.HasIgnoredAllianceIds;
        private readonly IgnoreAllianceCoordinator _ignoreAllianceCoordinator;

        public IgnoreAllianceBoardController(IgnoreAllianceCoordinator ignoreAllianceCoordinator)
        {
            _ignoreAllianceCoordinator = ignoreAllianceCoordinator
                ?? throw new ArgumentNullException(nameof(ignoreAllianceCoordinator));
        }

        public IgnoreAllianceBoardApplyResult ApplyToCurrentRows(
            IEnumerable<PilotBoardRow> currentRows,
            PilotBoardRow? selectedRow)
        {
            if (currentRows == null)
            {
                throw new ArgumentNullException(nameof(currentRows));
            }

            if (!_ignoreAllianceCoordinator.HasIgnoredAllianceIds)
            {
                return new IgnoreAllianceBoardApplyResult(Array.Empty<PilotBoardRow>(), false);
            }

            var filterResult = _ignoreAllianceCoordinator.ApplyToRows(
                currentRows,
                row => TryGetAllianceId(row?.AllianceId));

            var removedRows = filterResult.RemovedItems.ToList();
            var selectedRowRemoved = selectedRow != null && removedRows.Contains(selectedRow);

            return new IgnoreAllianceBoardApplyResult(removedRows, selectedRowRemoved);
        }


        public bool ShouldRemoveResolvedRow(PilotBoardRow row)
        {
            if (row == null || !_ignoreAllianceCoordinator.HasIgnoredAllianceIds)
            {
                return false;
            }

            return ShouldIgnore(row);
        }

        public bool ShouldIgnore(PilotBoardRow row)
        {
            if (row == null)
            {
                return false;
            }

            return _ignoreAllianceCoordinator.ShouldIgnoreAlliance(TryGetAllianceId(row.AllianceId));
        }

        private static long? TryGetAllianceId(string allianceIdText)
        {
            if (string.IsNullOrWhiteSpace(allianceIdText))
            {
                return null;
            }

            if (!long.TryParse(allianceIdText.Trim(), out var allianceId))
            {
                return null;
            }

            if (allianceId <= 0)
            {
                return null;
            }

            return allianceId;
        }
    }
}
