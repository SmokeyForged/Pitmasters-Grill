using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PitmastersGrill.Services
{
    public sealed class IgnoreAllianceCoordinator
    {
        private readonly IgnoreAllianceListService _ignoreAllianceListService;
        private readonly IgnoreAllianceFilterService _ignoreAllianceFilterService;
        private List<TypedIgnoreEntry> _ignoredEntries;

        public IgnoreAllianceCoordinator(
            IgnoreAllianceListService ignoreAllianceListService,
            IgnoreAllianceFilterService ignoreAllianceFilterService)
        {
            _ignoreAllianceListService = ignoreAllianceListService
                ?? throw new ArgumentNullException(nameof(ignoreAllianceListService));
            _ignoreAllianceFilterService = ignoreAllianceFilterService
                ?? throw new ArgumentNullException(nameof(ignoreAllianceFilterService));

            _ignoredEntries = _ignoreAllianceListService.LoadTypedEntries();
        }

        public bool HasIgnoredAllianceIds => _ignoredEntries.Any(x => x.Type == IgnoreEntryType.Alliance);
        public bool HasIgnoredEntries => _ignoredEntries.Count > 0;

        public IReadOnlyList<long> GetIgnoredAllianceIds()
        {
            return GetIgnoredIds(IgnoreEntryType.Alliance)
                .OrderBy(x => x)
                .ToList();
        }

        public IReadOnlyList<TypedIgnoreEntry> GetIgnoredEntries()
        {
            return _ignoredEntries
                .Select(CloneEntry)
                .OrderBy(x => x.Type)
                .ThenBy(x => x.Id)
                .ToList();
        }

        public bool ContainsAllianceId(long allianceId)
        {
            return ContainsEntry(IgnoreEntryType.Alliance, allianceId);
        }

        public bool ContainsEntry(IgnoreEntryType type, long id)
        {
            return id > 0 && _ignoredEntries.Any(x => x.Type == type && x.Id == id);
        }

        public IgnoreAllianceNormalizationResult MergeWithExisting(
            IEnumerable<long> existingAllianceIds,
            IEnumerable<string> rawAllianceIds)
        {
            var normalizationResult = _ignoreAllianceListService.NormalizeRawAllianceIds(rawAllianceIds);

            var mergedAllianceIds = (existingAllianceIds ?? Enumerable.Empty<long>())
                .Concat(normalizationResult.NormalizedAllianceIds)
                .Where(x => x > 0)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            return new IgnoreAllianceNormalizationResult(
                mergedAllianceIds,
                normalizationResult.InvalidEntries);
        }

        public TypedIgnoreNormalizationResult MergeWithExisting(
            IEnumerable<TypedIgnoreEntry> existingEntries,
            IEnumerable<string> rawIds,
            IgnoreEntryType type,
            string source)
        {
            var normalizationResult = _ignoreAllianceListService.NormalizeRawTypedEntries(rawIds, type, source);
            var mergedEntries = NormalizeEntries((existingEntries ?? Enumerable.Empty<TypedIgnoreEntry>())
                .Concat(normalizationResult.Entries));

            return new TypedIgnoreNormalizationResult(mergedEntries, normalizationResult.InvalidEntries);
        }

        public IgnoreAllianceNormalizationResult ReplaceAndPersist(IEnumerable<string> rawAllianceIds)
        {
            var normalizationResult = _ignoreAllianceListService.NormalizeRawAllianceIds(rawAllianceIds);

            _ignoredEntries = normalizationResult.NormalizedAllianceIds
                .Select(id => CreateEntry(id, IgnoreEntryType.Alliance, "replace alliance ids"))
                .ToList();
            _ignoreAllianceListService.SaveTypedEntries(_ignoredEntries);

            return normalizationResult;
        }

        public void ReplaceAndPersist(IEnumerable<long> allianceIds)
        {
            var normalizedAllianceIds = (allianceIds ?? Enumerable.Empty<long>())
                .Where(x => x > 0)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            _ignoredEntries = normalizedAllianceIds
                .Select(id => CreateEntry(id, IgnoreEntryType.Alliance, "replace alliance ids"))
                .ToList();
            _ignoreAllianceListService.SaveTypedEntries(_ignoredEntries);
        }

        public void ReplaceAndPersist(IEnumerable<TypedIgnoreEntry> entries)
        {
            _ignoredEntries = NormalizeEntries(entries);
            _ignoreAllianceListService.SaveTypedEntries(_ignoredEntries);
        }

        public bool AddAllianceIdAndPersist(long allianceId)
        {
            return AddEntryAndPersist(IgnoreEntryType.Alliance, allianceId, "detail window ignore alliance", "Unresolved");
        }

        public bool AddEntryAndPersist(IgnoreEntryType type, long id, string source, string? resolvedName = null)
        {
            if (id <= 0 || type == IgnoreEntryType.Unknown)
            {
                return false;
            }

            if (ContainsEntry(type, id))
            {
                return false;
            }

            var entry = CreateEntry(id, type, source);
            if (!string.IsNullOrWhiteSpace(resolvedName))
            {
                entry.ResolvedName = resolvedName.Trim();
            }

            _ignoredEntries.Add(entry);
            _ignoredEntries = NormalizeEntries(_ignoredEntries);
            _ignoreAllianceListService.SaveTypedEntries(_ignoredEntries);

            AppLogger.UiInfo($"Ignore entry added. type={type} id={id} name='{entry.ResolvedName}' source='{source}'");
            return true;
        }

        public void ClearAndPersist()
        {
            var removedCount = _ignoredEntries.Count;
            _ignoredEntries.Clear();
            _ignoreAllianceListService.SaveTypedEntries(_ignoredEntries);
            AppLogger.UiInfo($"Ignore list cleared. removedEntries={removedCount}");
        }

        public IgnoreAllianceFilterResult<T> ApplyToRows<T>(
            IEnumerable<T> items,
            Func<T, long?> allianceIdSelector)
        {
            return _ignoreAllianceFilterService.Filter(items, GetIgnoredIds(IgnoreEntryType.Alliance), allianceIdSelector);
        }

        public bool ShouldIgnoreAlliance(long? allianceId)
        {
            return _ignoreAllianceFilterService.ShouldIgnore(allianceId, GetIgnoredIds(IgnoreEntryType.Alliance));
        }

        public TypedIgnoreMatch? GetIgnoreMatch(long? pilotId, long? corporationId, long? allianceId)
        {
            if (pilotId.HasValue && ContainsEntry(IgnoreEntryType.Pilot, pilotId.Value))
            {
                return new TypedIgnoreMatch(IgnoreEntryType.Pilot, pilotId.Value);
            }

            if (corporationId.HasValue && ContainsEntry(IgnoreEntryType.Corporation, corporationId.Value))
            {
                return new TypedIgnoreMatch(IgnoreEntryType.Corporation, corporationId.Value);
            }

            if (allianceId.HasValue && ContainsEntry(IgnoreEntryType.Alliance, allianceId.Value))
            {
                return new TypedIgnoreMatch(IgnoreEntryType.Alliance, allianceId.Value);
            }

            return null;
        }

        public void Reload()
        {
            _ignoredEntries = _ignoreAllianceListService.LoadTypedEntries();
        }

        private IEnumerable<long> GetIgnoredIds(IgnoreEntryType type)
        {
            return _ignoredEntries
                .Where(x => x.Type == type && x.Id > 0)
                .Select(x => x.Id);
        }

        private static TypedIgnoreEntry CreateEntry(long id, IgnoreEntryType type, string source)
        {
            var entry = new TypedIgnoreEntry
            {
                Id = id,
                Type = type,
                ResolvedName = "Unresolved"
            };
            entry.Touch(source);
            return entry;
        }

        private static TypedIgnoreEntry CloneEntry(TypedIgnoreEntry entry)
        {
            return new TypedIgnoreEntry
            {
                Id = entry.Id,
                Type = entry.Type,
                ResolvedName = string.IsNullOrWhiteSpace(entry.ResolvedName) ? "Unresolved" : entry.ResolvedName,
                Source = entry.Source ?? "",
                CreatedAtUtc = entry.CreatedAtUtc ?? "",
                UpdatedAtUtc = entry.UpdatedAtUtc ?? ""
            };
        }

        private static List<TypedIgnoreEntry> NormalizeEntries(IEnumerable<TypedIgnoreEntry> entries)
        {
            return (entries ?? Enumerable.Empty<TypedIgnoreEntry>())
                .Where(x => x != null && x.Id > 0 && x.Type != IgnoreEntryType.Unknown)
                .Select(CloneEntry)
                .GroupBy(x => new { x.Id, x.Type })
                .Select(group => group.First())
                .OrderBy(x => x.Type)
                .ThenBy(x => x.Id)
                .ToList();
        }
    }

    public sealed class TypedIgnoreMatch
    {
        public TypedIgnoreMatch(IgnoreEntryType type, long id)
        {
            Type = type;
            Id = id;
        }

        public IgnoreEntryType Type { get; }
        public long Id { get; }
    }
}
