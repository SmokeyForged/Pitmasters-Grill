using PitmastersGrill.Models;
using PitmastersGrill.Services;

namespace PitmastersGrill
{
    public partial class MainWindow
    {
        private readonly TypedIgnoreActionCoordinator _typedIgnoreActionCoordinator;

        private bool TryIgnoreForRow(PilotBoardRow selectedRow, IgnoreEntryType type)
        {
            var result = _typedIgnoreActionCoordinator.TryAdd(selectedRow, type);
            switch (result.Outcome)
            {
                case TypedIgnoreActionOutcome.InvalidId:
                    AppLogger.UiWarn($"Ignore requested without a valid ID. character='{selectedRow.CharacterName}' type={type}");
                    return false;

                case TypedIgnoreActionOutcome.AlreadyPresent:
                    AppLogger.UiInfo($"Ignore requested for existing entry. character='{selectedRow.CharacterName}' type={type} id='{result.Id}'");
                    _pilotDetailSurface.UpdateIgnoreAllianceButtonState(selectedRow);
                    _ignoreAllianceListView?.RefreshFromCoordinator();
                    return false;

                case TypedIgnoreActionOutcome.Added:
                    AppLogger.UiInfo($"Typed ignore added from details. character='{selectedRow.CharacterName}' type={type} id='{result.Id}' name='{result.DisplayName}'");
                    _ignoreAllianceListView?.RefreshFromCoordinator();
                    ApplyIgnoredAllianceRowsToCurrentBoard();
                    RecomputeCorpAllianceCounts();
                    return true;

                default:
                    return false;
            }
        }
    }
}
