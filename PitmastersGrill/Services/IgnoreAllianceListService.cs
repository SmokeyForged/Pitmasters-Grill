using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PitmastersGrill.Services
{
    public sealed class IgnoreAllianceNormalizationResult
    {
        public IgnoreAllianceNormalizationResult(
            IReadOnlyList<long> normalizedAllianceIds,
            IReadOnlyList<string> invalidEntries)
        {
            NormalizedAllianceIds = normalizedAllianceIds ?? throw new ArgumentNullException(nameof(normalizedAllianceIds));
            InvalidEntries = invalidEntries ?? throw new ArgumentNullException(nameof(invalidEntries));
        }

        public IReadOnlyList<long> NormalizedAllianceIds { get; }
        public IReadOnlyList<string> InvalidEntries { get; }
    }

    public sealed class TypedIgnoreNormalizationResult
    {
        public TypedIgnoreNormalizationResult(
            IReadOnlyList<TypedIgnoreEntry> entries,
            IReadOnlyList<string> invalidEntries)
        {
            Entries = entries ?? throw new ArgumentNullException(nameof(entries));
            InvalidEntries = invalidEntries ?? throw new ArgumentNullException(nameof(invalidEntries));
        }

        public IReadOnlyList<TypedIgnoreEntry> Entries { get; }
        public IReadOnlyList<string> InvalidEntries { get; }
    }

    public sealed class IgnoreAllianceListService
    {
        private const string IgnoreAllianceFileName = "ignore-alliances.json";
        private readonly string _ignoreAllianceListPath;

        public IgnoreAllianceListService()
        {
            _ignoreAllianceListPath = Path.Combine(AppPaths.GetConfigDirectory(), IgnoreAllianceFileName);
        }

        public IgnoreAllianceListState LoadState()
        {
            try
            {
                if (!File.Exists(_ignoreAllianceListPath))
                {
                    AppLogger.AppInfo($"Ignore alliance list not found. Using empty state. path={_ignoreAllianceListPath}");
                    return new IgnoreAllianceListState();
                }

                var json = File.ReadAllText(_ignoreAllianceListPath);
                var state = JsonSerializer.Deserialize<IgnoreAllianceListState>(json);

                var sanitized = SanitizeState(state ?? new IgnoreAllianceListState());

                AppLogger.AppInfo(
                    $"Ignore list loaded successfully. path={_ignoreAllianceListPath} allianceCount={sanitized.AllianceIds.Count} typedCount={sanitized.Entries.Count}");

                return sanitized;
            }
            catch (Exception ex)
            {
                AppLogger.AppWarn($"Failed to load ignore alliance list. Using empty state. path={_ignoreAllianceListPath}");
                AppLogger.ErrorOnly("Ignore alliance list load failure.", ex);
                return new IgnoreAllianceListState();
            }
        }

        public HashSet<long> LoadAllianceIds()
        {
            return new HashSet<long>(LoadTypedEntries()
                .Where(x => x.Type == IgnoreEntryType.Alliance)
                .Select(x => x.Id));
        }

        public List<TypedIgnoreEntry> LoadTypedEntries()
        {
            return LoadState().Entries
                .Select(CloneEntry)
                .ToList();
        }

        public void SaveAllianceIds(IEnumerable<long> allianceIds)
        {
            try
            {
                var sanitizedIds = NormalizeAllianceIds(allianceIds);

                var state = new IgnoreAllianceListState
                {
                    AllianceIds = sanitizedIds,
                    Entries = sanitizedIds
                        .Select(id => CreateEntry(id, IgnoreEntryType.Alliance, "legacy alliance save"))
                        .ToList()
                };

                var directory = Path.GetDirectoryName(_ignoreAllianceListPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(
                    state,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                File.WriteAllText(_ignoreAllianceListPath, json);

                AppLogger.AppInfo(
                    $"Ignore alliance list saved successfully. path={_ignoreAllianceListPath} count={sanitizedIds.Count}");
            }
            catch (Exception ex)
            {
                AppLogger.AppWarn($"Failed to save ignore alliance list. path={_ignoreAllianceListPath}");
                AppLogger.ErrorOnly("Ignore alliance list save failure.", ex);
            }
        }

        public void SaveTypedEntries(IEnumerable<TypedIgnoreEntry> entries)
        {
            try
            {
                var sanitizedEntries = NormalizeTypedEntries(entries);
                var state = new IgnoreAllianceListState
                {
                    AllianceIds = sanitizedEntries
                        .Where(x => x.Type == IgnoreEntryType.Alliance)
                        .Select(x => x.Id)
                        .OrderBy(x => x)
                        .ToList(),
                    Entries = sanitizedEntries
                };

                var directory = Path.GetDirectoryName(_ignoreAllianceListPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(
                    state,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                File.WriteAllText(_ignoreAllianceListPath, json);

                AppLogger.AppInfo(
                    $"Typed ignore list saved successfully. path={_ignoreAllianceListPath} count={sanitizedEntries.Count}");
            }
            catch (Exception ex)
            {
                AppLogger.AppWarn($"Failed to save typed ignore list. path={_ignoreAllianceListPath}");
                AppLogger.ErrorOnly("Typed ignore list save failure.", ex);
            }
        }

        public TypedIgnoreNormalizationResult NormalizeRawTypedEntries(
            IEnumerable<string> rawIds,
            IgnoreEntryType type,
            string source)
        {
            var entries = new List<TypedIgnoreEntry>();
            var invalidEntries = new List<string>();

            if (rawIds == null)
            {
                return new TypedIgnoreNormalizationResult(entries, invalidEntries);
            }

            foreach (var rawEntry in rawIds)
            {
                var trimmed = rawEntry?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                if (TryParsePositiveId(trimmed, out var id))
                {
                    entries.Add(CreateEntry(id, NormalizeType(type), source));
                    continue;
                }

                invalidEntries.Add(trimmed);
            }

            return new TypedIgnoreNormalizationResult(
                NormalizeTypedEntries(entries),
                invalidEntries
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToList());
        }

        public IgnoreAllianceNormalizationResult NormalizeRawAllianceIds(IEnumerable<string> rawAllianceIds)
        {
            var normalizedIds = new List<long>();
            var invalidEntries = new List<string>();

            if (rawAllianceIds == null)
            {
                return new IgnoreAllianceNormalizationResult(normalizedIds, invalidEntries);
            }

            foreach (var rawEntry in rawAllianceIds)
            {
                var trimmed = rawEntry?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

            if (TryParseAllianceId(trimmed, out var allianceId))
                {
                    normalizedIds.Add(allianceId);
                    continue;
                }

                invalidEntries.Add(trimmed);
            }

            return new IgnoreAllianceNormalizationResult(
                NormalizeAllianceIds(normalizedIds),
                invalidEntries
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToList());
        }

        public bool TryParseAllianceId(string rawAllianceId, out long allianceId)
        {
            return TryParsePositiveId(rawAllianceId, out allianceId);
        }

        private static IgnoreAllianceListState SanitizeState(IgnoreAllianceListState state)
        {
            state.AllianceIds = NormalizeAllianceIds(state.AllianceIds);
            state.Entries = NormalizeTypedEntries(
                (state.Entries ?? new List<TypedIgnoreEntry>())
                    .Concat(state.AllianceIds.Select(id => CreateEntry(id, IgnoreEntryType.Alliance, "migrated legacy alliance id"))));
            state.AllianceIds = state.Entries
                .Where(x => x.Type == IgnoreEntryType.Alliance)
                .Select(x => x.Id)
                .OrderBy(x => x)
                .ToList();
            return state;
        }

        private static List<long> NormalizeAllianceIds(IEnumerable<long> allianceIds)
        {
            if (allianceIds == null)
            {
                return new List<long>();
            }

            return allianceIds
                .Where(x => x > 0)
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        }

        private static bool TryParsePositiveId(string rawId, out long id)
        {
            id = 0;

            if (string.IsNullOrWhiteSpace(rawId))
            {
                return false;
            }

            var trimmed = rawId.Trim();

            if (!long.TryParse(trimmed, out var parsed) || parsed <= 0)
            {
                return false;
            }

            id = parsed;
            return true;
        }

        private static TypedIgnoreEntry CreateEntry(long id, IgnoreEntryType type, string source)
        {
            var entry = new TypedIgnoreEntry
            {
                Id = id,
                Type = NormalizeType(type),
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
                Type = NormalizeType(entry.Type),
                ResolvedName = string.IsNullOrWhiteSpace(entry.ResolvedName) ? "Unresolved" : entry.ResolvedName,
                Source = entry.Source ?? "",
                CreatedAtUtc = entry.CreatedAtUtc ?? "",
                UpdatedAtUtc = entry.UpdatedAtUtc ?? ""
            };
        }

        private static List<TypedIgnoreEntry> NormalizeTypedEntries(IEnumerable<TypedIgnoreEntry> entries)
        {
            if (entries == null)
            {
                return new List<TypedIgnoreEntry>();
            }

            return entries
                .Where(x => x != null && x.Id > 0)
                .Select(CloneEntry)
                .GroupBy(x => new { x.Id, x.Type })
                .Select(group =>
                {
                    var entry = group
                        .OrderByDescending(x => x.UpdatedAtUtc ?? "")
                        .First();
                    if (string.IsNullOrWhiteSpace(entry.ResolvedName))
                    {
                        entry.ResolvedName = "Unresolved";
                    }

                    entry.Type = NormalizeType(entry.Type);
                    return entry;
                })
                .OrderBy(x => x.Type)
                .ThenBy(x => x.Id)
                .ToList();
        }

        private static IgnoreEntryType NormalizeType(IgnoreEntryType type)
        {
            return type == IgnoreEntryType.Pilot ||
                   type == IgnoreEntryType.Corporation ||
                   type == IgnoreEntryType.Alliance
                ? type
                : IgnoreEntryType.Unknown;
        }
    }
}
