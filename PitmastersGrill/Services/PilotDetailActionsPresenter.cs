using PitmastersGrill.Models;

namespace PitmastersGrill.Services
{
    public sealed record WatchPilotActionState(
        bool IsEnabled,
        string Content,
        string ToolTip,
        string ForegroundResourceKey);

    public sealed record IgnoreAllianceActionState(
        bool IsEnabled,
        string ToolTip);

    public sealed class PilotDetailActionsPresenter
    {
        public WatchPilotActionState BuildWatchPilotActionState(PilotBoardRow? row)
        {
            if (row == null)
            {
                return new WatchPilotActionState(
                    false,
                    "Watch",
                    "Select a resolved pilot to watch.",
                    "SuccessGreenBrush");
            }

            var canWatch = TryGetPositiveId(row.CharacterId).HasValue;

            return new WatchPilotActionState(
                canWatch,
                row.IsWatched ? "Unwatch" : "Watch",
                canWatch
                    ? (row.IsWatched ? "Stop watching this pilot." : "Mark this pilot as watched.")
                    : "Selected pilot does not have a known character ID yet.",
                row.IsWatched ? "WatchedPilotMarkerBrush" : "SuccessGreenBrush");
        }

        public IgnoreAllianceActionState BuildIgnoreAllianceActionState(PilotBoardRow? row, bool allianceAlreadyIgnored)
        {
            if (row == null)
            {
                return new IgnoreAllianceActionState(
                    false,
                    "Select a pilot to ignore their alliance.");
            }

            var allianceId = TryGetPositiveId(row.AllianceId);
            if (!allianceId.HasValue)
            {
                return new IgnoreAllianceActionState(
                    false,
                    "Selected pilot does not have a known alliance ID yet.");
            }

            if (allianceAlreadyIgnored)
            {
                return new IgnoreAllianceActionState(
                    false,
                    "This alliance is already on the ignore list.");
            }

            var toolTip = string.IsNullOrWhiteSpace(row.AllianceName)
                ? $"Ignore alliance ID {allianceId.Value}."
                : $"Ignore alliance '{row.AllianceName}' ({allianceId.Value}).";

            return new IgnoreAllianceActionState(true, toolTip);
        }

        public long? TryGetPilotId(string? characterIdText)
        {
            return TryGetPositiveId(characterIdText);
        }

        public long? TryGetAllianceId(string? allianceIdText)
        {
            return TryGetPositiveId(allianceIdText);
        }

        private static long? TryGetPositiveId(string? idText)
        {
            if (string.IsNullOrWhiteSpace(idText) ||
                !long.TryParse(idText.Trim(), out var id) ||
                id <= 0)
            {
                return null;
            }

            return id;
        }
    }
}
