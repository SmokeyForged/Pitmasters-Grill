using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;

namespace PitmastersGrill.Services
{
    public enum TypedIgnoreActionOutcome
    {
        InvalidId = 0,
        AlreadyPresent = 1,
        Added = 2
    }

    public sealed record TypedIgnoreActionResult(
        TypedIgnoreActionOutcome Outcome,
        IgnoreEntryType Type,
        long? Id,
        string DisplayName);

    public sealed class TypedIgnoreActionCoordinator
    {
        private readonly IgnoreAllianceCoordinator _ignoreAllianceCoordinator;

        public TypedIgnoreActionCoordinator(IgnoreAllianceCoordinator ignoreAllianceCoordinator)
        {
            _ignoreAllianceCoordinator = ignoreAllianceCoordinator
                ?? throw new ArgumentNullException(nameof(ignoreAllianceCoordinator));
        }

        public TypedIgnoreActionResult TryAdd(PilotBoardRow row, IgnoreEntryType type)
        {
            ArgumentNullException.ThrowIfNull(row);

            var mapping = type switch
            {
                IgnoreEntryType.Pilot => (IdText: row.CharacterId, DisplayName: row.CharacterName),
                IgnoreEntryType.Corporation => (IdText: row.CorpId, DisplayName: row.CorpName),
                IgnoreEntryType.Alliance => (IdText: row.AllianceId, DisplayName: row.AllianceName),
                _ => (IdText: string.Empty, DisplayName: string.Empty)
            };

            if (!TryParsePositiveId(mapping.IdText, out var id) || type == IgnoreEntryType.Unknown)
            {
                return new TypedIgnoreActionResult(
                    TypedIgnoreActionOutcome.InvalidId,
                    type,
                    null,
                    "Unresolved");
            }

            var displayName = string.IsNullOrWhiteSpace(mapping.DisplayName)
                ? "Unresolved"
                : mapping.DisplayName.Trim();
            var added = _ignoreAllianceCoordinator.AddEntryAndPersist(
                type,
                id,
                $"detail window ignore {type}",
                displayName);

            return new TypedIgnoreActionResult(
                added ? TypedIgnoreActionOutcome.Added : TypedIgnoreActionOutcome.AlreadyPresent,
                type,
                id,
                displayName);
        }

        private static bool TryParsePositiveId(string? rawId, out long id)
        {
            id = 0;
            return !string.IsNullOrWhiteSpace(rawId) &&
                long.TryParse(rawId.Trim(), out id) &&
                id > 0;
        }
    }
}
