using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using PitmastersGrill.Providers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersGrill.Services
{
    public class KillmailDayImportService
    {
        private readonly DayImportStateRepository _dayImportStateRepository;
        private readonly KillmailDatasetMetadataRepository _metadataRepository;
        private readonly KillmailDayArchiveProvider _killmailDayArchiveProvider;
        private readonly PilotRegistryDayRepository _pilotRegistryDayRepository;
        private readonly PilotFleetObservationDayRepository _pilotFleetObservationDayRepository;
        private readonly PilotShipObservationDayRepository _pilotShipObservationDayRepository;
        private readonly PilotCynoModuleObservationDayRepository _pilotCynoModuleObservationDayRepository;
        private readonly PilotBaitObservationDayRepository _pilotBaitObservationDayRepository;
        private readonly PilotCynoTackleObservationDayRepository _pilotCynoTackleObservationDayRepository;
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
            _pilotCynoModuleObservationDayRepository = new PilotCynoModuleObservationDayRepository(killmailDbPath);
            _pilotBaitObservationDayRepository = new PilotBaitObservationDayRepository(killmailDbPath);
            _pilotCynoTackleObservationDayRepository = new PilotCynoTackleObservationDayRepository(killmailDbPath);
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
            var cynoModuleObservations = new List<PilotCynoModuleObservationDayRecord>();
            var baitObservations = new List<PilotBaitObservationDayRecord>();
            var cynoTackleObservations = new List<PilotCynoTackleObservationDayRecord>();

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

                foreach (var cynoModule in parsed.CynoModuleObservations)
                {
                    cynoModule.DayUtc = remoteDay.DayUtc;
                    cynoModule.UpdatedAtUtc = utcNow;
                    cynoModuleObservations.Add(cynoModule);

                    AppLogger.DatabaseInfo(
                        $"Confirmed cyno module observed on public loss. character_id={cynoModule.CharacterId} killmail_id={cynoModule.KillmailId} killmail_time={cynoModule.KillmailTimeUtc} module_type_id={cynoModule.ModuleTypeId} module_name='{cynoModule.ModuleName}' victim_ship_type_id={cynoModule.VictimShipTypeId?.ToString(CultureInfo.InvariantCulture) ?? ""}");
                }

                foreach (var bait in parsed.BaitObservations)
                {
                    bait.DayUtc = remoteDay.DayUtc;
                    bait.UpdatedAtUtc = utcNow;
                    baitObservations.Add(bait);

                    AppLogger.DatabaseInfo(
                        $"Derived industrial-cyno bait observed on public loss. character_id={bait.CharacterId} killmail_id={bait.KillmailId} killmail_time={bait.KillmailTimeUtc} victim_ship='{bait.VictimShipName}' industrial_cyno='{bait.IndustrialCynoModuleName}' tackle_module='{bait.TackleModuleName}' tackle_type={bait.TackleType}");
                }

                foreach (var tackle in parsed.CynoTackleObservations)
                {
                    tackle.DayUtc = remoteDay.DayUtc;
                    tackle.UpdatedAtUtc = utcNow;
                    cynoTackleObservations.Add(tackle);

                    AppLogger.DatabaseInfo(
                        $"Cyno-capable hull tackle observed on public loss. character_id={tackle.CharacterId} killmail_id={tackle.KillmailId} killmail_time={tackle.KillmailTimeUtc} victim_ship='{tackle.VictimShipName}' tackle_module='{tackle.TackleModuleName}' tackle_type={tackle.TackleType}");
                }
            }

            parseStopwatch.Stop();

            DebugTraceWriter.WriteLine(
                $"killmail import aggregate summary: day={remoteDay.DayUtc}, jsonFiles={relativePaths.Count}, killmailsImported={importedKillmailCount}, uniquePilots={registryAccumulators.Count}, fleetPilots={fleetAccumulators.Count}, shipPilots={shipAccumulators.Count}, cynoModuleObservations={cynoModuleObservations.Count}, baitObservations={baitObservations.Count}, cynoTackleObservations={cynoTackleObservations.Count}, parseElapsedMs={parseStopwatch.ElapsedMilliseconds}");

            var writeStopwatch = Stopwatch.StartNew();

            _pilotRegistryDayRepository.ReplaceDay(remoteDay.DayUtc, new List<PilotRegistryDayRecord>(registryAccumulators.Values));
            _pilotFleetObservationDayRepository.ReplaceDay(remoteDay.DayUtc, new List<PilotFleetObservationDayRecord>(fleetAccumulators.Values));
            _pilotShipObservationDayRepository.ReplaceDay(remoteDay.DayUtc, new List<PilotShipObservationDayRecord>(shipAccumulators.Values));
            _pilotCynoModuleObservationDayRepository.ReplaceDay(remoteDay.DayUtc, cynoModuleObservations);
            _pilotBaitObservationDayRepository.ReplaceDay(remoteDay.DayUtc, baitObservations);
            _pilotCynoTackleObservationDayRepository.ReplaceDay(remoteDay.DayUtc, cynoTackleObservations);

            writeStopwatch.Stop();

            DebugTraceWriter.WriteLine(
                $"killmail import write summary: day={remoteDay.DayUtc}, uniquePilotsWritten={registryAccumulators.Count}, fleetPilotsWritten={fleetAccumulators.Count}, shipPilotsWritten={shipAccumulators.Count}, cynoModuleObservationsWritten={cynoModuleObservations.Count}, baitObservationsWritten={baitObservations.Count}, cynoTackleObservationsWritten={cynoTackleObservations.Count}, writeElapsedMs={writeStopwatch.ElapsedMilliseconds}");

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
                    $"killmail import complete: day={remoteDay.DayUtc}, archiveBytes={downloadResult.ArchiveLengthBytes}, jsonFiles={relativePaths.Count}, killmailsImported={importedKillmailCount}, uniquePilots={registryAccumulators.Count}, fleetPilots={fleetAccumulators.Count}, shipPilots={shipAccumulators.Count}, cynoModuleObservations={cynoModuleObservations.Count}, baitObservations={baitObservations.Count}, cynoTackleObservations={cynoTackleObservations.Count}, extractElapsedMs={extractResult.ExtractElapsedMs}, parseElapsedMs={parseStopwatch.ElapsedMilliseconds}, writeElapsedMs={writeStopwatch.ElapsedMilliseconds}, totalElapsedMs={totalStopwatch.ElapsedMilliseconds}");

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

        internal static List<PilotCynoModuleObservationDayRecord> ParseConfirmedCynoModuleObservations(string jsonContent, string dayUtc = "", string updatedAtUtc = "")
        {
            var parsed = ParseKillmailEntry(jsonContent);
            if (parsed == null)
            {
                return new List<PilotCynoModuleObservationDayRecord>();
            }

            foreach (var observation in parsed.CynoModuleObservations)
            {
                observation.DayUtc = dayUtc;
                observation.UpdatedAtUtc = updatedAtUtc;
            }

            return parsed.CynoModuleObservations;
        }

        internal static List<PilotBaitObservationDayRecord> ParseDerivedBaitObservations(string jsonContent, string dayUtc = "", string updatedAtUtc = "")
        {
            var parsed = ParseKillmailEntry(jsonContent);
            if (parsed == null)
            {
                return new List<PilotBaitObservationDayRecord>();
            }

            foreach (var observation in parsed.BaitObservations)
            {
                observation.DayUtc = dayUtc;
                observation.UpdatedAtUtc = updatedAtUtc;
            }

            return parsed.BaitObservations;
        }

        internal static List<PilotCynoTackleObservationDayRecord> ParseCynoHullTackleObservations(string jsonContent, string dayUtc = "", string updatedAtUtc = "")
        {
            var parsed = ParseKillmailEntry(jsonContent);
            if (parsed == null)
            {
                return new List<PilotCynoTackleObservationDayRecord>();
            }

            foreach (var observation in parsed.CynoTackleObservations)
            {
                observation.DayUtc = dayUtc;
                observation.UpdatedAtUtc = updatedAtUtc;
            }

            return parsed.CynoTackleObservations;
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
            var killmailId = TryReadLongAsString(root, "killmail_id");

            var registryPilots = new Dictionary<string, RegistryPilotSeen>(StringComparer.OrdinalIgnoreCase);
            var fleetPilots = new Dictionary<string, FleetPilotSeen>(StringComparer.OrdinalIgnoreCase);
            var shipPilots = new Dictionary<string, ShipPilotSeen>(StringComparer.OrdinalIgnoreCase);
            var cynoModuleObservations = new List<PilotCynoModuleObservationDayRecord>();
            var baitObservations = new List<PilotBaitObservationDayRecord>();
            var cynoTackleObservations = new List<PilotCynoTackleObservationDayRecord>();

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
                    var victimShipTypeId = TryReadInt(victim, "ship_type_id");
                    registryPilots[victimCharacterId] = new RegistryPilotSeen
                    {
                        CharacterId = victimCharacterId,
                        FirstSeenKillmailTimeUtc = killmailTimeText,
                        LastSeenKillmailTimeUtc = killmailTimeText
                    };

                    shipPilots[victimCharacterId] = new ShipPilotSeen
                    {
                        CharacterId = victimCharacterId,
                        LastSeenShipTypeId = victimShipTypeId,
                        LastSeenShipTimeUtc = killmailTimeText
                    };

                    if (victim.TryGetProperty("items", out var victimItems) &&
                        victimItems.ValueKind == JsonValueKind.Array)
                    {
                        var victimShipName = TryReadString(victim, "ship_name") ??
                                             TryReadString(victim, "ship_type_name") ??
                                             TryReadString(victim, "type_name") ??
                                             "";
                        var solarSystemId = TryReadInt(root, "solar_system_id");
                        var solarSystemName = TryReadString(root, "solar_system_name") ?? "";

                        ScanVictimItemsForDerivedIntel(
                            victimItems,
                            victimCharacterId,
                            killmailId,
                            killmailTimeText,
                            victimShipTypeId,
                            victimShipName,
                            solarSystemId,
                            solarSystemName,
                            cynoModuleObservations,
                            baitObservations,
                            cynoTackleObservations);
                    }
                }
            }

            return new ParsedKillmailEntry
            {
                RegistryPilots = new List<RegistryPilotSeen>(registryPilots.Values),
                FleetPilots = new List<FleetPilotSeen>(fleetPilots.Values),
                ShipPilots = new List<ShipPilotSeen>(shipPilots.Values),
                CynoModuleObservations = cynoModuleObservations,
                BaitObservations = baitObservations,
                CynoTackleObservations = cynoTackleObservations
            };
        }

        private static void ScanVictimItemsForDerivedIntel(
            JsonElement items,
            string victimCharacterId,
            string killmailId,
            string killmailTimeUtc,
            int? victimShipTypeId,
            string victimShipName,
            int? solarSystemId,
            string solarSystemName,
            List<PilotCynoModuleObservationDayRecord> cynoModuleObservations,
            List<PilotBaitObservationDayRecord> baitObservations,
            List<PilotCynoTackleObservationDayRecord> cynoTackleObservations)
        {
            var foundModules = new List<VictimItemModule>();
            CollectVictimItemModules(items, foundModules);

            foreach (var module in foundModules.Where(x => x.CynoSignalType != CynoSignalType.Unknown))
            {
                cynoModuleObservations.Add(new PilotCynoModuleObservationDayRecord
                {
                    CharacterId = victimCharacterId,
                    KillmailId = killmailId,
                    KillmailTimeUtc = killmailTimeUtc,
                    VictimShipTypeId = victimShipTypeId,
                    ModuleTypeId = module.TypeId,
                    ModuleName = module.ModuleName,
                    QuantityDestroyed = module.QuantityDestroyed,
                    QuantityDropped = module.QuantityDropped,
                    ItemState = GetItemState(module.QuantityDestroyed, module.QuantityDropped),
                    Source = "public loss victim item list"
                });
            }

            var industrialCynos = foundModules
                .Where(x => x.TypeId == CynoSignalAnalyzer.IndustrialCynoModuleTypeId)
                .ToList();
            var tackleModules = foundModules
                .Where(x => x.TackleType != TackleModuleType.UnknownTackle)
                .ToList();

            if (tackleModules.Count > 0 &&
                CynoShipCatalog.TryGetCynoShipNameByTypeId(victimShipTypeId, out var cynoHullName))
            {
                var displayedVictimShipName = string.IsNullOrWhiteSpace(victimShipName)
                    ? cynoHullName
                    : victimShipName;

                foreach (var tackle in tackleModules)
                {
                    cynoTackleObservations.Add(new PilotCynoTackleObservationDayRecord
                    {
                        CharacterId = victimCharacterId,
                        KillmailId = killmailId,
                        KillmailTimeUtc = killmailTimeUtc,
                        VictimShipTypeId = victimShipTypeId,
                        VictimShipName = displayedVictimShipName,
                        TackleModuleTypeId = tackle.TypeId,
                        TackleModuleName = tackle.ModuleName,
                        TackleType = tackle.TackleType,
                        QuantityDestroyed = tackle.QuantityDestroyed,
                        QuantityDropped = tackle.QuantityDropped,
                        Source = "public loss victim item list"
                    });
                }
            }

            if (industrialCynos.Count == 0 || tackleModules.Count == 0)
            {
                return;
            }

            var industrialCyno = industrialCynos[0];
            foreach (var tackle in tackleModules)
            {
                baitObservations.Add(new PilotBaitObservationDayRecord
                {
                    CharacterId = victimCharacterId,
                    KillmailId = killmailId,
                    KillmailTimeUtc = killmailTimeUtc,
                    VictimShipTypeId = victimShipTypeId,
                    VictimShipName = victimShipName,
                    SolarSystemId = solarSystemId,
                    SolarSystemName = solarSystemName,
                    IndustrialCynoModuleTypeId = industrialCyno.TypeId,
                    IndustrialCynoModuleName = industrialCyno.ModuleName,
                    TackleModuleTypeId = tackle.TypeId,
                    TackleModuleName = tackle.ModuleName,
                    TackleType = tackle.TackleType,
                    QuantityDestroyed = tackle.QuantityDestroyed,
                    QuantityDropped = tackle.QuantityDropped,
                    Source = "public loss victim item list"
                });
            }
        }

        private static void CollectVictimItemModules(JsonElement items, List<VictimItemModule> modules)
        {
            if (items.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var typeId = TryReadInt(item, "item_type_id") ?? TryReadInt(item, "type_id");
                var itemName = TryReadString(item, "type_name") ??
                               TryReadString(item, "item_name") ??
                               TryReadString(item, "name") ??
                               "";

                var signalType = CynoSignalType.Unknown;
                var moduleName = itemName;
                if (typeId.HasValue &&
                    CynoSignalAnalyzer.TryGetModuleSignalType(typeId.Value, out signalType, out var knownModuleName))
                {
                    moduleName = knownModuleName;
                }

                var tackleType = TackleModuleType.UnknownTackle;
                if (typeId.HasValue && TryGetKnownTackleModule(typeId.Value, out tackleType, out var knownTackleName))
                {
                    moduleName = knownTackleName;
                }
                else if (TryGetTackleTypeFromName(itemName, out tackleType))
                {
                    moduleName = itemName;
                    AppLogger.DatabaseInfo(
                        $"Tackle module detected by item-name fallback during victim item scan. item_type_id={typeId?.ToString(CultureInfo.InvariantCulture) ?? ""} item_name='{itemName}' tackle_type={tackleType}");
                }

                if (typeId.HasValue && (signalType != CynoSignalType.Unknown || tackleType != TackleModuleType.UnknownTackle))
                {
                    modules.Add(new VictimItemModule
                    {
                        TypeId = typeId.Value,
                        ModuleName = string.IsNullOrWhiteSpace(moduleName)
                            ? $"type_id {typeId.Value.ToString(CultureInfo.InvariantCulture)}"
                            : moduleName,
                        CynoSignalType = signalType,
                        TackleType = tackleType,
                        QuantityDestroyed = TryReadInt(item, "quantity_destroyed") ?? 0,
                        QuantityDropped = TryReadInt(item, "quantity_dropped") ?? 0
                    });
                }

                if (item.TryGetProperty("items", out var nestedItems) &&
                    nestedItems.ValueKind == JsonValueKind.Array)
                {
                    CollectVictimItemModules(nestedItems, modules);
                }
            }
        }

        private static bool TryGetKnownTackleModule(int typeId, out TackleModuleType tackleType, out string moduleName)
        {
            return TackleModuleCatalog.TryGetKnownTackleModule(typeId, out tackleType, out moduleName);
        }

        private static bool TryGetTackleTypeFromName(string itemName, out TackleModuleType tackleType)
        {
            tackleType = TackleModuleType.UnknownTackle;
            if (string.IsNullOrWhiteSpace(itemName))
            {
                return false;
            }

            if (itemName.Contains("Warp Scrambler", StringComparison.OrdinalIgnoreCase))
            {
                tackleType = TackleModuleType.WarpScrambler;
                return true;
            }

            if (itemName.Contains("Warp Disruptor", StringComparison.OrdinalIgnoreCase))
            {
                tackleType = TackleModuleType.WarpDisruptor;
                return true;
            }

            return false;
        }

        private static string GetItemState(int quantityDestroyed, int quantityDropped)
        {
            if (quantityDestroyed > 0 && quantityDropped > 0)
            {
                return "destroyed/dropped";
            }

            if (quantityDestroyed > 0)
            {
                return "destroyed";
            }

            if (quantityDropped > 0)
            {
                return "dropped";
            }

            return "fitted/unknown";
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

        private static string? TryReadString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value) ||
                value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return value.GetString();
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
            public List<PilotCynoModuleObservationDayRecord> CynoModuleObservations { get; set; } = new();
            public List<PilotBaitObservationDayRecord> BaitObservations { get; set; } = new();
            public List<PilotCynoTackleObservationDayRecord> CynoTackleObservations { get; set; } = new();
        }

        private class VictimItemModule
        {
            public int TypeId { get; set; }
            public string ModuleName { get; set; } = "";
            public CynoSignalType CynoSignalType { get; set; } = CynoSignalType.Unknown;
            public TackleModuleType TackleType { get; set; } = TackleModuleType.UnknownTackle;
            public int QuantityDestroyed { get; set; }
            public int QuantityDropped { get; set; }
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
