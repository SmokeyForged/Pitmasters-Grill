using PitmastersLittleGrill.Models;
using PitmastersLittleGrill.Persistence;
using PitmastersLittleGrill.Providers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersLittleGrill.Services
{
    public class KillmailDayImportService
    {
        private readonly DayImportStateRepository _dayImportStateRepository;
        private readonly KillmailDatasetMetadataRepository _metadataRepository;
        private readonly KillmailDayArchiveProvider _killmailDayArchiveProvider;
        private readonly PilotRegistryDayRepository _pilotRegistryDayRepository;
        private readonly PilotFleetObservationDayRepository _pilotFleetObservationDayRepository;
        private readonly PilotShipObservationDayRepository _pilotShipObservationDayRepository;
        private readonly CynoShipCatalog _cynoShipCatalog;

        public KillmailDayImportService(
            DayImportStateRepository dayImportStateRepository,
            KillmailDatasetMetadataRepository metadataRepository,
            KillmailDayArchiveProvider killmailDayArchiveProvider)
        {
            _dayImportStateRepository = dayImportStateRepository;
            _metadataRepository = metadataRepository;
            _killmailDayArchiveProvider = killmailDayArchiveProvider;

            var killmailDbPath = KillmailPaths.GetKillmailDatabasePath();
            _pilotRegistryDayRepository = new PilotRegistryDayRepository(killmailDbPath);
            _pilotFleetObservationDayRepository = new PilotFleetObservationDayRepository(killmailDbPath);
            _pilotShipObservationDayRepository = new PilotShipObservationDayRepository(killmailDbPath);
            _cynoShipCatalog = new CynoShipCatalog();
        }

        public async Task<KillmailDayImportResult> ImportSingleDayAsync(
            KillmailRemoteDayInfo remoteDay,
            CancellationToken cancellationToken = default)
        {
            var totalStopwatch = Stopwatch.StartNew();
            var utcNow = DateTime.UtcNow.ToString("o");

            DebugTraceWriter.WriteLine(
                $"killmail import start: day={remoteDay.DayUtc}, remoteTotalCount={remoteDay.RemoteTotalCount}");

            var dayState = _dayImportStateRepository.GetByDay(remoteDay.DayUtc) ?? new DayImportState
            {
                DayUtc = remoteDay.DayUtc
            };

            dayState.RemoteTotalCount = remoteDay.RemoteTotalCount;
            dayState.CheckedAtUtc = utcNow;
            dayState.State = "checked";
            dayState.LastError = "";
            _dayImportStateRepository.Upsert(dayState);

            var downloadResult = await _killmailDayArchiveProvider.DownloadDayArchiveAsync(
                remoteDay.DayUtc,
                cancellationToken);

            if (!downloadResult.Success)
            {
                var isNotPublishedYet = IsArchiveNotPublishedYet(downloadResult.Error);

                if (isNotPublishedYet)
                {
                    dayState.State = "not_published_yet";
                    dayState.LastError = "";
                    _dayImportStateRepository.Upsert(dayState);

                    DebugTraceWriter.WriteLine(
                        $"killmail import not-published-yet: day={remoteDay.DayUtc}, error={downloadResult.Error}, elapsedMs={totalStopwatch.ElapsedMilliseconds}");

                    return new KillmailDayImportResult
                    {
                        Success = false,
                        DayUtc = remoteDay.DayUtc,
                        ArchiveUnavailableNotPublishedYet = true,
                        ArchiveUnavailableDayUtc = remoteDay.DayUtc,
                        Error = downloadResult.Error
                    };
                }

                dayState.State = "failed";
                dayState.LastError = downloadResult.Error;
                _dayImportStateRepository.Upsert(dayState);

                DebugTraceWriter.WriteLine(
                    $"killmail import failed-download: day={remoteDay.DayUtc}, error={downloadResult.Error}, elapsedMs={totalStopwatch.ElapsedMilliseconds}");

                return new KillmailDayImportResult
                {
                    Success = false,
                    DayUtc = remoteDay.DayUtc,
                    Error = downloadResult.Error
                };
            }

            dayState.ArchiveEtag = downloadResult.ArchiveEtag;
            dayState.ArchiveLastModified = downloadResult.ArchiveLastModified;
            dayState.DownloadedAtUtc = DateTime.UtcNow.ToString("o");
            dayState.State = "downloaded";
            dayState.LastError = "";
            _dayImportStateRepository.Upsert(dayState);

            var extractResult = await _killmailDayArchiveProvider.EnsureDayExtractedAsync(
                remoteDay.DayUtc,
                downloadResult.ArchivePath,
                cancellationToken);

            if (!extractResult.Success)
            {
                var error = string.IsNullOrWhiteSpace(extractResult.Error)
                    ? "Archive extraction failed."
                    : extractResult.Error;

                dayState.State = "failed";
                dayState.LastError = error;
                _dayImportStateRepository.Upsert(dayState);

                DebugTraceWriter.WriteLine(
                    $"killmail import failed-extract: day={remoteDay.DayUtc}, error={error}, elapsedMs={totalStopwatch.ElapsedMilliseconds}");

                return new KillmailDayImportResult
                {
                    Success = false,
                    DayUtc = remoteDay.DayUtc,
                    Error = error
                };
            }

            var relativePaths = _killmailDayArchiveProvider.GetExtractedJsonRelativePaths(remoteDay.DayUtc);

            DebugTraceWriter.WriteLine(
                $"killmail archive file scan ok: day={remoteDay.DayUtc}, jsonFiles={relativePaths.Count}");

            var registryAccumulators = new Dictionary<string, PilotRegistryDayRecord>(StringComparer.OrdinalIgnoreCase);
            var fleetAccumulators = new Dictionary<string, PilotFleetObservationDayRecord>(StringComparer.OrdinalIgnoreCase);
            var shipAccumulators = new Dictionary<string, PilotShipObservationDayRecord>(StringComparer.OrdinalIgnoreCase);

            var importedKillmailCount = 0;
            var parseStopwatch = Stopwatch.StartNew();

            foreach (var relativePath in relativePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var jsonContent = await _killmailDayArchiveProvider.ReadExtractedJsonAsync(
                    remoteDay.DayUtc,
                    relativePath,
                    cancellationToken);

                var parsed = ParseKillmailEntry(jsonContent);

                if (parsed == null)
                {
                    continue;
                }

                importedKillmailCount++;

                foreach (var pilotSeen in parsed.RegistryPilots)
                {
                    if (!registryAccumulators.TryGetValue(pilotSeen.CharacterId, out var existingRegistry))
                    {
                        registryAccumulators[pilotSeen.CharacterId] = new PilotRegistryDayRecord
                        {
                            DayUtc = remoteDay.DayUtc,
                            CharacterId = pilotSeen.CharacterId,
                            FirstSeenKillmailTimeUtc = pilotSeen.FirstSeenKillmailTimeUtc,
                            LastSeenKillmailTimeUtc = pilotSeen.LastSeenKillmailTimeUtc,
                            SeenCount = 1,
                            UpdatedAtUtc = utcNow
                        };
                    }
                    else
                    {
                        if (string.CompareOrdinal(pilotSeen.FirstSeenKillmailTimeUtc, existingRegistry.FirstSeenKillmailTimeUtc) < 0)
                        {
                            existingRegistry.FirstSeenKillmailTimeUtc = pilotSeen.FirstSeenKillmailTimeUtc;
                        }

                        if (string.CompareOrdinal(pilotSeen.LastSeenKillmailTimeUtc, existingRegistry.LastSeenKillmailTimeUtc) > 0)
                        {
                            existingRegistry.LastSeenKillmailTimeUtc = pilotSeen.LastSeenKillmailTimeUtc;
                        }

                        existingRegistry.SeenCount += 1;
                        existingRegistry.UpdatedAtUtc = utcNow;
                    }
                }

                foreach (var fleetSeen in parsed.FleetPilots)
                {
                    if (!fleetAccumulators.TryGetValue(fleetSeen.CharacterId, out var existingFleet))
                    {
                        fleetAccumulators[fleetSeen.CharacterId] = new PilotFleetObservationDayRecord
                        {
                            DayUtc = remoteDay.DayUtc,
                            CharacterId = fleetSeen.CharacterId,
                            AttackerSampleCount = 1,
                            AttackerCountSum = fleetSeen.AttackerCountForThisKillmail,
                            DerivedAtUtc = utcNow
                        };
                    }
                    else
                    {
                        existingFleet.AttackerSampleCount += 1;
                        existingFleet.AttackerCountSum += fleetSeen.AttackerCountForThisKillmail;
                        existingFleet.DerivedAtUtc = utcNow;
                    }
                }

                foreach (var shipSeen in parsed.ShipPilots)
                {
                    var isCynoCapable = _cynoShipCatalog.TryGetCynoShipName(
                        shipSeen.LastSeenShipTypeId,
                        out var cynoShipName);

                    if (!shipAccumulators.TryGetValue(shipSeen.CharacterId, out var existingShip))
                    {
                        shipAccumulators[shipSeen.CharacterId] = new PilotShipObservationDayRecord
                        {
                            DayUtc = remoteDay.DayUtc,
                            CharacterId = shipSeen.CharacterId,
                            LastSeenShipTypeId = shipSeen.LastSeenShipTypeId,
                            LastSeenShipTimeUtc = shipSeen.LastSeenShipTimeUtc,
                            LastSeenCynoShipTypeId = isCynoCapable ? shipSeen.LastSeenShipTypeId : null,
                            LastSeenCynoShipName = isCynoCapable ? cynoShipName : "",
                            LastSeenCynoShipTimeUtc = isCynoCapable ? shipSeen.LastSeenShipTimeUtc : "",
                            UpdatedAtUtc = utcNow
                        };
                    }
                    else
                    {
                        if (string.CompareOrdinal(shipSeen.LastSeenShipTimeUtc, existingShip.LastSeenShipTimeUtc) > 0)
                        {
                            existingShip.LastSeenShipTypeId = shipSeen.LastSeenShipTypeId;
                            existingShip.LastSeenShipTimeUtc = shipSeen.LastSeenShipTimeUtc;
                        }

                        if (isCynoCapable &&
                            string.CompareOrdinal(shipSeen.LastSeenShipTimeUtc, existingShip.LastSeenCynoShipTimeUtc) > 0)
                        {
                            existingShip.LastSeenCynoShipTypeId = shipSeen.LastSeenShipTypeId;
                            existingShip.LastSeenCynoShipName = cynoShipName;
                            existingShip.LastSeenCynoShipTimeUtc = shipSeen.LastSeenShipTimeUtc;
                        }

                        existingShip.UpdatedAtUtc = utcNow;
                    }
                }
            }

            parseStopwatch.Stop();

            DebugTraceWriter.WriteLine(
                $"killmail import aggregate summary: day={remoteDay.DayUtc}, jsonFiles={relativePaths.Count}, killmailsImported={importedKillmailCount}, uniquePilots={registryAccumulators.Count}, fleetPilots={fleetAccumulators.Count}, shipPilots={shipAccumulators.Count}, parseElapsedMs={parseStopwatch.ElapsedMilliseconds}");

            var writeStopwatch = Stopwatch.StartNew();

            _pilotRegistryDayRepository.ReplaceDay(remoteDay.DayUtc, new List<PilotRegistryDayRecord>(registryAccumulators.Values));
            _pilotFleetObservationDayRepository.ReplaceDay(remoteDay.DayUtc, new List<PilotFleetObservationDayRecord>(fleetAccumulators.Values));
            _pilotShipObservationDayRepository.ReplaceDay(remoteDay.DayUtc, new List<PilotShipObservationDayRecord>(shipAccumulators.Values));

            writeStopwatch.Stop();

            DebugTraceWriter.WriteLine(
                $"killmail import write summary: day={remoteDay.DayUtc}, uniquePilotsWritten={registryAccumulators.Count}, fleetPilotsWritten={fleetAccumulators.Count}, shipPilotsWritten={shipAccumulators.Count}, writeElapsedMs={writeStopwatch.ElapsedMilliseconds}");

            dayState.LocalImportedCount = importedKillmailCount;
            dayState.ImportedAtUtc = DateTime.UtcNow.ToString("o");
            dayState.State = "imported";
            dayState.LastError = "";
            _dayImportStateRepository.Upsert(dayState);

            dayState.NormalizedAtUtc = DateTime.UtcNow.ToString("o");
            dayState.State = "normalized";
            _dayImportStateRepository.Upsert(dayState);

            if (importedKillmailCount > 0 || remoteDay.RemoteTotalCount == 0)
            {
                dayState.CompletedAtUtc = DateTime.UtcNow.ToString("o");
                dayState.State = "complete";
                dayState.LastError = "";
                _dayImportStateRepository.Upsert(dayState);

                var currentLatestCompleteDay = _metadataRepository.GetValue("latest_complete_day_utc");

                if (string.IsNullOrWhiteSpace(currentLatestCompleteDay) ||
                    string.CompareOrdinal(remoteDay.DayUtc, currentLatestCompleteDay) > 0)
                {
                    _metadataRepository.SetValue("latest_complete_day_utc", remoteDay.DayUtc);
                }

                _metadataRepository.SetUtcNow("last_successful_update_at_utc");

                totalStopwatch.Stop();

                DebugTraceWriter.WriteLine(
                    $"killmail import complete: day={remoteDay.DayUtc}, archiveBytes={downloadResult.ArchiveLengthBytes}, jsonFiles={relativePaths.Count}, killmailsImported={importedKillmailCount}, uniquePilots={registryAccumulators.Count}, fleetPilots={fleetAccumulators.Count}, shipPilots={shipAccumulators.Count}, extractElapsedMs={extractResult.ExtractElapsedMs}, parseElapsedMs={parseStopwatch.ElapsedMilliseconds}, writeElapsedMs={writeStopwatch.ElapsedMilliseconds}, totalElapsedMs={totalStopwatch.ElapsedMilliseconds}");

                return new KillmailDayImportResult
                {
                    Success = true,
                    DayUtc = remoteDay.DayUtc,
                    ImportedKillmailCount = importedKillmailCount,
                    ImportedParticipantCount = registryAccumulators.Count,
                    UniquePilotCount = registryAccumulators.Count,
                    FleetObservationPilotCount = fleetAccumulators.Count,
                    ShipObservationPilotCount = shipAccumulators.Count,
                    CompletedDay = true
                };
            }

            dayState.State = "failed";
            dayState.LastError =
                $"Imported killmail count {importedKillmailCount} did not satisfy completion for day {remoteDay.DayUtc}.";
            _dayImportStateRepository.Upsert(dayState);

            totalStopwatch.Stop();

            DebugTraceWriter.WriteLine(
                $"killmail import failed-completion: day={remoteDay.DayUtc}, killmailsImported={importedKillmailCount}, totalElapsedMs={totalStopwatch.ElapsedMilliseconds}");

            return new KillmailDayImportResult
            {
                Success = false,
                DayUtc = remoteDay.DayUtc,
                ImportedKillmailCount = importedKillmailCount,
                ImportedParticipantCount = registryAccumulators.Count,
                UniquePilotCount = registryAccumulators.Count,
                FleetObservationPilotCount = fleetAccumulators.Count,
                ShipObservationPilotCount = shipAccumulators.Count,
                CompletedDay = false,
                Error = dayState.LastError
            };
        }

        private static bool IsArchiveNotPublishedYet(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                return false;
            }

            return error.Contains("404", StringComparison.OrdinalIgnoreCase);
        }

        private static ParsedKillmailEntry? ParseKillmailEntry(string jsonContent)
        {
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                return null;
            }

            using var document = JsonDocument.Parse(jsonContent);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var killmailTimeUtc = TryReadDateTime(root, "killmail_time");

            if (!killmailTimeUtc.HasValue)
            {
                return null;
            }

            var killmailTimeText = killmailTimeUtc.Value.ToString("o");

            var registryPilots = new Dictionary<string, RegistryPilotSeen>(StringComparer.OrdinalIgnoreCase);
            var fleetPilots = new Dictionary<string, FleetPilotSeen>(StringComparer.OrdinalIgnoreCase);
            var shipPilots = new Dictionary<string, ShipPilotSeen>(StringComparer.OrdinalIgnoreCase);

            var attackerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var attackerShipUpdates = new List<(string CharacterId, int? ShipTypeId)>();

            JsonElement attackersElement = default;
            var hasAttackers = root.TryGetProperty("attackers", out attackersElement) &&
                               attackersElement.ValueKind == JsonValueKind.Array;

            if (hasAttackers)
            {
                foreach (var attacker in attackersElement.EnumerateArray())
                {
                    var attackerCharacterId = TryReadLongAsString(attacker, "character_id");
                    if (string.IsNullOrWhiteSpace(attackerCharacterId))
                    {
                        continue;
                    }

                    attackerIds.Add(attackerCharacterId);
                    attackerShipUpdates.Add((attackerCharacterId, TryReadInt(attacker, "ship_type_id")));

                    registryPilots[attackerCharacterId] = new RegistryPilotSeen
                    {
                        CharacterId = attackerCharacterId,
                        FirstSeenKillmailTimeUtc = killmailTimeText,
                        LastSeenKillmailTimeUtc = killmailTimeText
                    };
                }
            }

            var playerAttackerCount = attackerIds.Count;

            foreach (var attackerId in attackerIds)
            {
                fleetPilots[attackerId] = new FleetPilotSeen
                {
                    CharacterId = attackerId,
                    AttackerCountForThisKillmail = playerAttackerCount
                };
            }

            foreach (var shipUpdate in attackerShipUpdates)
            {
                shipPilots[shipUpdate.CharacterId] = new ShipPilotSeen
                {
                    CharacterId = shipUpdate.CharacterId,
                    LastSeenShipTypeId = shipUpdate.ShipTypeId,
                    LastSeenShipTimeUtc = killmailTimeText
                };
            }

            if (root.TryGetProperty("victim", out var victim) && victim.ValueKind == JsonValueKind.Object)
            {
                var victimCharacterId = TryReadLongAsString(victim, "character_id");
                if (!string.IsNullOrWhiteSpace(victimCharacterId))
                {
                    registryPilots[victimCharacterId] = new RegistryPilotSeen
                    {
                        CharacterId = victimCharacterId,
                        FirstSeenKillmailTimeUtc = killmailTimeText,
                        LastSeenKillmailTimeUtc = killmailTimeText
                    };

                    shipPilots[victimCharacterId] = new ShipPilotSeen
                    {
                        CharacterId = victimCharacterId,
                        LastSeenShipTypeId = TryReadInt(victim, "ship_type_id"),
                        LastSeenShipTimeUtc = killmailTimeText
                    };
                }
            }

            return new ParsedKillmailEntry
            {
                RegistryPilots = new List<RegistryPilotSeen>(registryPilots.Values),
                FleetPilots = new List<FleetPilotSeen>(fleetPilots.Values),
                ShipPilots = new List<ShipPilotSeen>(shipPilots.Values)
            };
        }

        private static int? TryReadInt(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue))
            {
                return intValue;
            }

            if (value.ValueKind == JsonValueKind.String &&
                int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static long? TryReadLong(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var longValue))
            {
                return longValue;
            }

            if (value.ValueKind == JsonValueKind.String &&
                long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static string TryReadLongAsString(JsonElement element, string propertyName)
        {
            var longValue = TryReadLong(element, propertyName);
            return longValue?.ToString(CultureInfo.InvariantCulture) ?? "";
        }

        private static DateTime? TryReadDateTime(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            if (value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            if (DateTime.TryParse(
                value.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private class ParsedKillmailEntry
        {
            public List<RegistryPilotSeen> RegistryPilots { get; set; } = new();
            public List<FleetPilotSeen> FleetPilots { get; set; } = new();
            public List<ShipPilotSeen> ShipPilots { get; set; } = new();
        }

        private class RegistryPilotSeen
        {
            public string CharacterId { get; set; } = "";
            public string FirstSeenKillmailTimeUtc { get; set; } = "";
            public string LastSeenKillmailTimeUtc { get; set; } = "";
        }

        private class FleetPilotSeen
        {
            public string CharacterId { get; set; } = "";
            public int AttackerCountForThisKillmail { get; set; }
        }

        private class ShipPilotSeen
        {
            public string CharacterId { get; set; } = "";
            public int? LastSeenShipTypeId { get; set; }
            public string LastSeenShipTimeUtc { get; set; } = "";
        }
    }

    public class KillmailDayImportResult
    {
        public bool Success { get; set; }
        public string DayUtc { get; set; } = "";
        public int ImportedKillmailCount { get; set; }
        public int ImportedParticipantCount { get; set; }
        public int UniquePilotCount { get; set; }
        public int FleetObservationPilotCount { get; set; }
        public int ShipObservationPilotCount { get; set; }
        public bool CompletedDay { get; set; }
        public bool ArchiveUnavailableNotPublishedYet { get; set; }
        public string ArchiveUnavailableDayUtc { get; set; } = "";
        public string Error { get; set; } = "";
    }
}