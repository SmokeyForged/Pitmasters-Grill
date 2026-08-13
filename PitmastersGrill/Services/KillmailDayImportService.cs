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
        private readonly KillmailDbWriteGate _writeGate;
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
        private static readonly KillmailDerivedObservationParser SharedKillmailParser = new();

        public KillmailDayImportService(
            KillmailDbWriteGate writeGate,
            DayImportStateRepository dayImportStateRepository,
            KillmailDatasetMetadataRepository metadataRepository,
            KillmailDayArchiveProvider killmailDayArchiveProvider)
        {
            _writeGate = writeGate ?? throw new ArgumentNullException(nameof(writeGate));
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
            using var writeGate = await _writeGate.EnterAsync(
                $"archive day import day={remoteDay.DayUtc}",
                cancellationToken);

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

new KillmailArchiveDayReplacementService(KillmailPaths.GetKillmailDatabasePath())
    .ReplaceDay(
        remoteDay.DayUtc,
        new List<PilotRegistryDayRecord>(registryAccumulators.Values),
        new List<PilotFleetObservationDayRecord>(fleetAccumulators.Values),
        new List<PilotShipObservationDayRecord>(shipAccumulators.Values),
        cynoModuleObservations,
        baitObservations,
        cynoTackleObservations);

            writeStopwatch.Stop();

            DebugTraceWriter.WriteLine(
                $"killmail import write summary: day={remoteDay.DayUtc}, uniquePilotsWritten={registryAccumulators.Count}, fleetPilotsWritten={fleetAccumulators.Count}, shipPilotsWritten={shipAccumulators.Count}, cynoModuleObservationsWritten={cynoModuleObservations.Count}, baitObservationsWritten={baitObservations.Count}, cynoTackleObservationsWritten={cynoTackleObservations.Count}, writeElapsedMs={writeStopwatch.ElapsedMilliseconds}");

            var processedAtUtc = DateTime.UtcNow.ToString("o");
            dayState.LocalImportedCount = importedKillmailCount;
            dayState.ImportedAtUtc = processedAtUtc;
            dayState.NormalizedAtUtc = processedAtUtc;
            dayState.State = "processed";
            dayState.LastError = "";
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

        private static KillmailDerivedParseResult? ParseKillmailEntry(string jsonContent)
        {
            return SharedKillmailParser.ParseKillmailEntry(jsonContent);
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
