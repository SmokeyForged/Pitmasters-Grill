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
        private readonly IgnoreAllianceCoordinator _ignoreAllianceCoordinator;

        public IgnoreAllianceBoardController(IgnoreAllianceCoordinator ignoreAllianceCoordinator)
        {
            _ignoreAllianceCoordinator = ignoreAllianceCoordinator
                ?? throw new ArgumentNullException(nameof(ignoreAllianceCoordinator));
        }

        public bool HasIgnoredAllianceIds => _ignoreAllianceCoordinator.HasIgnoredAllianceIds;
        public bool HasIgnoredEntries => _ignoreAllianceCoordinator.HasIgnoredEntries;

        public IgnoreAllianceBoardApplyResult ApplyToCurrentRows(
            IEnumerable<PilotBoardRow> currentRows,
            PilotBoardRow? selectedRow)
        {
            if (currentRows == null)
            {
                throw new ArgumentNullException(nameof(currentRows));
            }

            if (!_ignoreAllianceCoordinator.HasIgnoredEntries)
            {
                return new IgnoreAllianceBoardApplyResult(Array.Empty<PilotBoardRow>(), false);
            }

            var removedRows = currentRows
                .Where(ShouldIgnore)
                .ToList();
            var selectedRowRemoved = selectedRow != null && removedRows.Contains(selectedRow);

            foreach (var row in removedRows.Take(25))
            {
                var match = GetIgnoreMatch(row);
                if (match == null)
                {
                    continue;
                }

                DiagnosticTelemetry.RecordIgnoreSuppression(
                    $"pilot='{row.CharacterName}' pilotId='{row.CharacterId}' corp='{row.CorpName}' corpId='{row.CorpId}' alliance='{row.AllianceName}' allianceId='{row.AllianceId}' matchedType={match.Type} matchedId={match.Id}");
            }

            return new IgnoreAllianceBoardApplyResult(removedRows, selectedRowRemoved);
        }

        public bool ShouldRemoveResolvedRow(PilotBoardRow row)
        {
            if (row == null || !_ignoreAllianceCoordinator.HasIgnoredEntries)
            {
                return false;
            }

            var match = GetIgnoreMatch(row);
            if (match == null)
            {
                return false;
            }

            DiagnosticTelemetry.RecordIgnoreSuppression(
                $"pilot='{row.CharacterName}' pilotId='{row.CharacterId}' corp='{row.CorpName}' corpId='{row.CorpId}' alliance='{row.AllianceName}' allianceId='{row.AllianceId}' matchedType={match.Type} matchedId={match.Id}");
            return true;
        }

        public bool ShouldIgnore(PilotBoardRow row)
        {
            return GetIgnoreMatch(row) != null;
        }

        public TypedIgnoreMatch? GetIgnoreMatch(PilotBoardRow row)
        {
            if (row == null)
            {
                return null;
            }

            return _ignoreAllianceCoordinator.GetIgnoreMatch(
                TryGetId(row.CharacterId),
                TryGetId(row.CorpId),
                TryGetId(row.AllianceId));
        }

        private static long? TryGetId(string idText)
        {
            if (string.IsNullOrWhiteSpace(idText))
            {
                return null;
            }

            if (!long.TryParse(idText.Trim(), out var id) || id <= 0)
            {
                return null;
            }

            return id;
        }
    }
}
