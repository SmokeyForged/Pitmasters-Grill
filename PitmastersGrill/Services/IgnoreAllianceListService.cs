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
                    $"Ignore alliance list loaded successfully. path={_ignoreAllianceListPath} count={sanitized.AllianceIds.Count}");

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
            return new HashSet<long>(LoadState().AllianceIds);
        }

        public void SaveAllianceIds(IEnumerable<long> allianceIds)
        {
            try
            {
                var sanitizedIds = NormalizeAllianceIds(allianceIds);

                var state = new IgnoreAllianceListState
                {
                    AllianceIds = sanitizedIds
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
            allianceId = 0;

            if (string.IsNullOrWhiteSpace(rawAllianceId))
            {
                return false;
            }

            var trimmed = rawAllianceId.Trim();

            if (!long.TryParse(trimmed, out var parsed))
            {
                return false;
            }

            if (parsed <= 0)
            {
                return false;
            }

            allianceId = parsed;
            return true;
        }

        private static IgnoreAllianceListState SanitizeState(IgnoreAllianceListState state)
        {
            state.AllianceIds = NormalizeAllianceIds(state.AllianceIds);
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
    }
}