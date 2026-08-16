using PitmastersGrill.Models;
using System;
using System.Collections.Generic;

namespace PitmastersGrill.Services
{
    public sealed class BoardRowStateHydrator
    {
        private readonly Func<string, bool> _getKnownCynoOverride;
        private readonly Func<string, bool> _getBaitOverride;
        private readonly Func<string, bool> _hasNotes;
        private readonly Func<string, bool> _isWatched;
        private readonly Action<PilotBoardRow> _updateConfirmedCynoModuleState;

        public BoardRowStateHydrator(
            Func<string, bool> getKnownCynoOverride,
            Func<string, bool> getBaitOverride,
            Func<string, bool> hasNotes,
            Func<string, bool> isWatched,
            Action<PilotBoardRow> updateConfirmedCynoModuleState)
        {
            _getKnownCynoOverride = getKnownCynoOverride ?? throw new ArgumentNullException(nameof(getKnownCynoOverride));
            _getBaitOverride = getBaitOverride ?? throw new ArgumentNullException(nameof(getBaitOverride));
            _hasNotes = hasNotes ?? throw new ArgumentNullException(nameof(hasNotes));
            _isWatched = isWatched ?? throw new ArgumentNullException(nameof(isWatched));
            _updateConfirmedCynoModuleState = updateConfirmedCynoModuleState ?? throw new ArgumentNullException(nameof(updateConfirmedCynoModuleState));
        }

        public void Hydrate(IEnumerable<PilotBoardRow> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);

            foreach (var row in rows)
            {
                Hydrate(row);
            }
        }

        public void Hydrate(PilotBoardRow row)
        {
            ArgumentNullException.ThrowIfNull(row);

            row.KnownCynoOverride = _getKnownCynoOverride(row.CharacterName);
            row.BaitOverride = _getBaitOverride(row.CharacterName);
            row.HasNotes = _hasNotes(row.CharacterName);
            row.IsWatched = _isWatched(row.CharacterId);
            _updateConfirmedCynoModuleState(row);
        }
    }
}
