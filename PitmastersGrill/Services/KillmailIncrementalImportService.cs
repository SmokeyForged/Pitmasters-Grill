using Microsoft.Data.Sqlite;
using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using PitmastersGrill.Providers;
using System;
using System.Globalization;

namespace PitmastersGrill.Services
{
    public sealed class KillmailIncrementalImportService
    {
        private readonly string _databasePath;
        private readonly KillmailDatasetMetadataRepository _metadataRepository;
        private readonly KillmailDerivedObservationParser _parser = new();
        private readonly CynoShipCatalog _cynoShipCatalog = new();

        public KillmailIncrementalImportService(string databasePath)
        {
            _databasePath = string.IsNullOrWhiteSpace(databasePath)
                ? throw new ArgumentException("Database path is required.", nameof(databasePath))
                : databasePath;
            _metadataRepository = new KillmailDatasetMetadataRepository(databasePath);
        }

        public SeenKillmailRecord? GetSeenRecord(long killmailId)
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();
            return LoadSeenRecord(connection, transaction: null, killmailId);
        }

        public IncrementalKillmailImportResult ImportKillmailJson(IncrementalKillmailImportRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var parsed = _parser.ParseKillmailEntry(request.KillmailJson);
            if (parsed == null)
            {
                RecordFailure(
                    request.KillmailId,
                    request.KillmailHash,
                    request.SequenceId,
                    source: request.Source,
                    uploadedAtUtc: request.UploadedAtUtc,
                    killmailTimeUtc: "",
                    dayUtc: "",
                    error: "Unable to parse killmail JSON.");

                return new IncrementalKillmailImportResult
                {
                    Success = false,
                    Error = "Unable to parse killmail JSON."
                };
            }

            var nowUtc = DateTime.UtcNow.ToString("o");
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var transaction = connection.BeginTransaction();

            var existingSeenRecord = LoadSeenRecord(connection, transaction, request.KillmailId);
            if (existingSeenRecord != null &&
                !string.Equals(existingSeenRecord.ProcessingStatus, "error", StringComparison.OrdinalIgnoreCase))
            {
                UpsertSeenRecord(
                    connection,
                    transaction,
                    request.KillmailId,
                    request.KillmailHash,
                    request.SequenceId,
                    parsed.KillmailTimeUtc,
                    parsed.DayUtc,
                    request.UploadedAtUtc,
                    nowUtc,
                    request.Source,
                    "duplicate",
                    "");

                transaction.Commit();

                return new IncrementalKillmailImportResult
                {
                    Success = true,
                    WasDuplicate = true,
                    KillmailId = request.KillmailId.ToString(CultureInfo.InvariantCulture),
                    DayUtc = parsed.DayUtc,
                    RegistryObservationCount = parsed.RegistryPilots.Count,
                    FleetObservationCount = parsed.FleetPilots.Count,
                    ShipObservationCount = parsed.ShipPilots.Count,
                    CynoObservationCount = parsed.CynoModuleObservations.Count,
                    BaitObservationCount = parsed.BaitObservations.Count,
                    TackleObservationCount = parsed.CynoTackleObservations.Count
                };
            }

            foreach (var registryPilot in parsed.RegistryPilots)
            {
                UpsertRegistryRecord(connection, transaction, parsed.DayUtc, registryPilot, nowUtc);
            }

            foreach (var fleetPilot in parsed.FleetPilots)
            {
                UpsertFleetRecord(connection, transaction, parsed.DayUtc, fleetPilot, nowUtc);
            }

            foreach (var shipPilot in parsed.ShipPilots)
            {
                UpsertShipRecord(connection, transaction, parsed.DayUtc, shipPilot, nowUtc);
            }

            foreach (var observation in parsed.CynoModuleObservations)
            {
                observation.DayUtc = parsed.DayUtc;
                observation.UpdatedAtUtc = nowUtc;
                UpsertCynoModuleRecord(connection, transaction, observation);
            }

            foreach (var observation in parsed.BaitObservations)
            {
                observation.DayUtc = parsed.DayUtc;
                observation.UpdatedAtUtc = nowUtc;
                UpsertBaitRecord(connection, transaction, observation);
            }

            foreach (var observation in parsed.CynoTackleObservations)
            {
                observation.DayUtc = parsed.DayUtc;
                observation.UpdatedAtUtc = nowUtc;
                UpsertCynoTackleRecord(connection, transaction, observation);
            }

            UpsertSeenRecord(
                connection,
                transaction,
                request.KillmailId,
                request.KillmailHash,
                request.SequenceId,
                parsed.KillmailTimeUtc,
                parsed.DayUtc,
                request.UploadedAtUtc,
                nowUtc,
                request.Source,
                "processed",
                "");

            transaction.Commit();
            _metadataRepository.SetUtcNow("last_successful_update_at_utc");

            return new IncrementalKillmailImportResult
            {
                Success = true,
                WasDuplicate = false,
                KillmailId = request.KillmailId.ToString(CultureInfo.InvariantCulture),
                DayUtc = parsed.DayUtc,
                RegistryObservationCount = parsed.RegistryPilots.Count,
                FleetObservationCount = parsed.FleetPilots.Count,
                ShipObservationCount = parsed.ShipPilots.Count,
                CynoObservationCount = parsed.CynoModuleObservations.Count,
                BaitObservationCount = parsed.BaitObservations.Count,
                TackleObservationCount = parsed.CynoTackleObservations.Count
            };
        }

        public void RecordFailure(
            long killmailId,
            string killmailHash,
            long sequenceId,
            string source,
            string uploadedAtUtc,
            string killmailTimeUtc,
            string dayUtc,
            string error)
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var transaction = connection.BeginTransaction();
            UpsertSeenRecord(
                connection,
                transaction,
                killmailId,
                killmailHash,
                sequenceId,
                killmailTimeUtc,
                dayUtc,
                uploadedAtUtc,
                DateTime.UtcNow.ToString("o"),
                source,
                "error",
                error);
            transaction.Commit();
        }

        private void UpsertRegistryRecord(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string dayUtc,
            KillmailRegistryPilotSeen record,
            string updatedAtUtc)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            INSERT INTO pilot_registry_day (
                day_utc,
                character_id,
                first_seen_killmail_time_utc,
                last_seen_killmail_time_utc,
                seen_count,
                updated_at_utc
            )
            VALUES (
                $dayUtc,
                $characterId,
                $firstSeen,
                $lastSeen,
                1,
                $updatedAtUtc
            )
            ON CONFLICT(day_utc, character_id) DO UPDATE SET
                first_seen_killmail_time_utc = CASE
                    WHEN pilot_registry_day.first_seen_killmail_time_utc = '' OR excluded.first_seen_killmail_time_utc < pilot_registry_day.first_seen_killmail_time_utc
                        THEN excluded.first_seen_killmail_time_utc
                    ELSE pilot_registry_day.first_seen_killmail_time_utc
                END,
                last_seen_killmail_time_utc = CASE
                    WHEN excluded.last_seen_killmail_time_utc > pilot_registry_day.last_seen_killmail_time_utc
                        THEN excluded.last_seen_killmail_time_utc
                    ELSE pilot_registry_day.last_seen_killmail_time_utc
                END,
                seen_count = pilot_registry_day.seen_count + 1,
                updated_at_utc = excluded.updated_at_utc;
            ";
            command.Parameters.AddWithValue("$dayUtc", dayUtc);
            command.Parameters.AddWithValue("$characterId", record.CharacterId);
            command.Parameters.AddWithValue("$firstSeen", record.FirstSeenKillmailTimeUtc);
            command.Parameters.AddWithValue("$lastSeen", record.LastSeenKillmailTimeUtc);
            command.Parameters.AddWithValue("$updatedAtUtc", updatedAtUtc);
            command.ExecuteNonQuery();
        }

        private static void UpsertFleetRecord(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string dayUtc,
            KillmailFleetPilotSeen record,
            string derivedAtUtc)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            INSERT INTO pilot_fleet_observations_day (
                day_utc,
                character_id,
                attacker_sample_count,
                attacker_count_sum,
                derived_at_utc
            )
            VALUES (
                $dayUtc,
                $characterId,
                1,
                $attackerCountSum,
                $derivedAtUtc
            )
            ON CONFLICT(day_utc, character_id) DO UPDATE SET
                attacker_sample_count = pilot_fleet_observations_day.attacker_sample_count + 1,
                attacker_count_sum = pilot_fleet_observations_day.attacker_count_sum + excluded.attacker_count_sum,
                derived_at_utc = excluded.derived_at_utc;
            ";
            command.Parameters.AddWithValue("$dayUtc", dayUtc);
            command.Parameters.AddWithValue("$characterId", record.CharacterId);
            command.Parameters.AddWithValue("$attackerCountSum", record.AttackerCountForThisKillmail);
            command.Parameters.AddWithValue("$derivedAtUtc", derivedAtUtc);
            command.ExecuteNonQuery();
        }

        private string UpsertShipRecord(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string dayUtc,
            KillmailShipPilotSeen record,
            string updatedAtUtc)
        {
            var isCynoCapable = _cynoShipCatalog.TryGetCynoShipName(record.LastSeenShipTypeId, out var cynoShipName);
            var action = DetermineShipUpsertAction(connection, transaction, dayUtc, record);

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            INSERT INTO pilot_ship_observations_day (
                day_utc,
                character_id,
                last_seen_ship_type_id,
                last_seen_ship_time_utc,
                last_seen_cyno_ship_type_id,
                last_seen_cyno_ship_name,
                last_seen_cyno_ship_time_utc,
                updated_at_utc
            )
            VALUES (
                $dayUtc,
                $characterId,
                $lastSeenShipTypeId,
                $lastSeenShipTimeUtc,
                $lastSeenCynoShipTypeId,
                $lastSeenCynoShipName,
                $lastSeenCynoShipTimeUtc,
                $updatedAtUtc
            )
            ON CONFLICT(day_utc, character_id) DO UPDATE SET
                last_seen_ship_type_id = CASE
                    WHEN excluded.last_seen_ship_time_utc > pilot_ship_observations_day.last_seen_ship_time_utc
                        THEN excluded.last_seen_ship_type_id
                    ELSE pilot_ship_observations_day.last_seen_ship_type_id
                END,
                last_seen_ship_time_utc = CASE
                    WHEN excluded.last_seen_ship_time_utc > pilot_ship_observations_day.last_seen_ship_time_utc
                        THEN excluded.last_seen_ship_time_utc
                    ELSE pilot_ship_observations_day.last_seen_ship_time_utc
                END,
                last_seen_cyno_ship_type_id = CASE
                    WHEN excluded.last_seen_cyno_ship_time_utc <> ''
                         AND (pilot_ship_observations_day.last_seen_cyno_ship_time_utc = ''
                              OR excluded.last_seen_cyno_ship_time_utc > pilot_ship_observations_day.last_seen_cyno_ship_time_utc)
                        THEN excluded.last_seen_cyno_ship_type_id
                    ELSE pilot_ship_observations_day.last_seen_cyno_ship_type_id
                END,
                last_seen_cyno_ship_name = CASE
                    WHEN excluded.last_seen_cyno_ship_time_utc <> ''
                         AND (pilot_ship_observations_day.last_seen_cyno_ship_time_utc = ''
                              OR excluded.last_seen_cyno_ship_time_utc > pilot_ship_observations_day.last_seen_cyno_ship_time_utc)
                        THEN excluded.last_seen_cyno_ship_name
                    ELSE pilot_ship_observations_day.last_seen_cyno_ship_name
                END,
                last_seen_cyno_ship_time_utc = CASE
                    WHEN excluded.last_seen_cyno_ship_time_utc <> ''
                         AND (pilot_ship_observations_day.last_seen_cyno_ship_time_utc = ''
                              OR excluded.last_seen_cyno_ship_time_utc > pilot_ship_observations_day.last_seen_cyno_ship_time_utc)
                        THEN excluded.last_seen_cyno_ship_time_utc
                    ELSE pilot_ship_observations_day.last_seen_cyno_ship_time_utc
                END,
                updated_at_utc = excluded.updated_at_utc;
            ";
            command.Parameters.AddWithValue("$dayUtc", dayUtc);
            command.Parameters.AddWithValue("$characterId", record.CharacterId);
            command.Parameters.AddWithValue("$lastSeenShipTypeId", (object?)record.LastSeenShipTypeId ?? DBNull.Value);
            command.Parameters.AddWithValue("$lastSeenShipTimeUtc", record.LastSeenShipTimeUtc);
            command.Parameters.AddWithValue("$lastSeenCynoShipTypeId", isCynoCapable ? (object?)record.LastSeenShipTypeId ?? DBNull.Value : DBNull.Value);
            command.Parameters.AddWithValue("$lastSeenCynoShipName", isCynoCapable ? cynoShipName : "");
            command.Parameters.AddWithValue("$lastSeenCynoShipTimeUtc", isCynoCapable ? record.LastSeenShipTimeUtc : "");
            command.Parameters.AddWithValue("$updatedAtUtc", updatedAtUtc);
            command.ExecuteNonQuery();
            return action;
        }

        private static string DetermineShipUpsertAction(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string dayUtc,
            KillmailShipPilotSeen record)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            SELECT last_seen_ship_time_utc
            FROM pilot_ship_observations_day
            WHERE day_utc = $dayUtc
              AND character_id = $characterId
            LIMIT 1;
            ";
            command.Parameters.AddWithValue("$dayUtc", dayUtc);
            command.Parameters.AddWithValue("$characterId", record.CharacterId);

            var existingLastSeen = command.ExecuteScalar()?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(existingLastSeen))
            {
                return "inserted";
            }

            return string.CompareOrdinal(record.LastSeenShipTimeUtc ?? "", existingLastSeen) > 0
                ? "updated"
                : "skipped";
        }

        private static void UpsertCynoModuleRecord(
            SqliteConnection connection,
            SqliteTransaction transaction,
            PilotCynoModuleObservationDayRecord record)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            INSERT INTO pilot_cyno_module_observations_day (
                day_utc,
                character_id,
                killmail_id,
                killmail_time_utc,
                victim_ship_type_id,
                module_type_id,
                module_name,
                quantity_destroyed,
                quantity_dropped,
                item_state,
                source,
                updated_at_utc
            )
            VALUES (
                $dayUtc,
                $characterId,
                $killmailId,
                $killmailTimeUtc,
                $victimShipTypeId,
                $moduleTypeId,
                $moduleName,
                $quantityDestroyed,
                $quantityDropped,
                $itemState,
                $source,
                $updatedAtUtc
            )
            ON CONFLICT(day_utc, character_id, killmail_id, module_type_id) DO UPDATE SET
                killmail_time_utc = excluded.killmail_time_utc,
                victim_ship_type_id = excluded.victim_ship_type_id,
                module_name = excluded.module_name,
                quantity_destroyed = excluded.quantity_destroyed,
                quantity_dropped = excluded.quantity_dropped,
                item_state = excluded.item_state,
                source = excluded.source,
                updated_at_utc = excluded.updated_at_utc;
            ";
            command.Parameters.AddWithValue("$dayUtc", record.DayUtc);
            command.Parameters.AddWithValue("$characterId", record.CharacterId);
            command.Parameters.AddWithValue("$killmailId", record.KillmailId);
            command.Parameters.AddWithValue("$killmailTimeUtc", record.KillmailTimeUtc);
            command.Parameters.AddWithValue("$victimShipTypeId", (object?)record.VictimShipTypeId ?? DBNull.Value);
            command.Parameters.AddWithValue("$moduleTypeId", record.ModuleTypeId);
            command.Parameters.AddWithValue("$moduleName", record.ModuleName);
            command.Parameters.AddWithValue("$quantityDestroyed", record.QuantityDestroyed);
            command.Parameters.AddWithValue("$quantityDropped", record.QuantityDropped);
            command.Parameters.AddWithValue("$itemState", record.ItemState);
            command.Parameters.AddWithValue("$source", record.Source);
            command.Parameters.AddWithValue("$updatedAtUtc", record.UpdatedAtUtc);
            command.ExecuteNonQuery();
        }

        private static void UpsertBaitRecord(
            SqliteConnection connection,
            SqliteTransaction transaction,
            PilotBaitObservationDayRecord record)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            INSERT INTO pilot_bait_observations_day (
                day_utc,
                character_id,
                killmail_id,
                killmail_time_utc,
                victim_ship_type_id,
                victim_ship_name,
                solar_system_id,
                solar_system_name,
                industrial_cyno_module_type_id,
                industrial_cyno_module_name,
                tackle_module_type_id,
                tackle_module_name,
                tackle_type,
                quantity_destroyed,
                quantity_dropped,
                source,
                updated_at_utc
            )
            VALUES (
                $dayUtc,
                $characterId,
                $killmailId,
                $killmailTimeUtc,
                $victimShipTypeId,
                $victimShipName,
                $solarSystemId,
                $solarSystemName,
                $industrialCynoModuleTypeId,
                $industrialCynoModuleName,
                $tackleModuleTypeId,
                $tackleModuleName,
                $tackleType,
                $quantityDestroyed,
                $quantityDropped,
                $source,
                $updatedAtUtc
            )
            ON CONFLICT(day_utc, character_id, killmail_id, tackle_module_type_id) DO UPDATE SET
                killmail_time_utc = excluded.killmail_time_utc,
                victim_ship_type_id = excluded.victim_ship_type_id,
                victim_ship_name = excluded.victim_ship_name,
                solar_system_id = excluded.solar_system_id,
                solar_system_name = excluded.solar_system_name,
                industrial_cyno_module_type_id = excluded.industrial_cyno_module_type_id,
                industrial_cyno_module_name = excluded.industrial_cyno_module_name,
                tackle_module_name = excluded.tackle_module_name,
                tackle_type = excluded.tackle_type,
                quantity_destroyed = excluded.quantity_destroyed,
                quantity_dropped = excluded.quantity_dropped,
                source = excluded.source,
                updated_at_utc = excluded.updated_at_utc;
            ";
            command.Parameters.AddWithValue("$dayUtc", record.DayUtc);
            command.Parameters.AddWithValue("$characterId", record.CharacterId);
            command.Parameters.AddWithValue("$killmailId", record.KillmailId);
            command.Parameters.AddWithValue("$killmailTimeUtc", record.KillmailTimeUtc);
            command.Parameters.AddWithValue("$victimShipTypeId", (object?)record.VictimShipTypeId ?? DBNull.Value);
            command.Parameters.AddWithValue("$victimShipName", record.VictimShipName);
            command.Parameters.AddWithValue("$solarSystemId", (object?)record.SolarSystemId ?? DBNull.Value);
            command.Parameters.AddWithValue("$solarSystemName", record.SolarSystemName);
            command.Parameters.AddWithValue("$industrialCynoModuleTypeId", record.IndustrialCynoModuleTypeId);
            command.Parameters.AddWithValue("$industrialCynoModuleName", record.IndustrialCynoModuleName);
            command.Parameters.AddWithValue("$tackleModuleTypeId", record.TackleModuleTypeId);
            command.Parameters.AddWithValue("$tackleModuleName", record.TackleModuleName);
            command.Parameters.AddWithValue("$tackleType", record.TackleType.ToString());
            command.Parameters.AddWithValue("$quantityDestroyed", record.QuantityDestroyed);
            command.Parameters.AddWithValue("$quantityDropped", record.QuantityDropped);
            command.Parameters.AddWithValue("$source", record.Source);
            command.Parameters.AddWithValue("$updatedAtUtc", record.UpdatedAtUtc);
            command.ExecuteNonQuery();
        }

        private static void UpsertCynoTackleRecord(
            SqliteConnection connection,
            SqliteTransaction transaction,
            PilotCynoTackleObservationDayRecord record)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            INSERT INTO pilot_cyno_tackle_observations_day (
                day_utc,
                character_id,
                killmail_id,
                killmail_time_utc,
                victim_ship_type_id,
                victim_ship_name,
                tackle_module_type_id,
                tackle_module_name,
                tackle_type,
                quantity_destroyed,
                quantity_dropped,
                source,
                updated_at_utc
            )
            VALUES (
                $dayUtc,
                $characterId,
                $killmailId,
                $killmailTimeUtc,
                $victimShipTypeId,
                $victimShipName,
                $tackleModuleTypeId,
                $tackleModuleName,
                $tackleType,
                $quantityDestroyed,
                $quantityDropped,
                $source,
                $updatedAtUtc
            )
            ON CONFLICT(day_utc, character_id, killmail_id, tackle_module_type_id) DO UPDATE SET
                killmail_time_utc = excluded.killmail_time_utc,
                victim_ship_type_id = excluded.victim_ship_type_id,
                victim_ship_name = excluded.victim_ship_name,
                tackle_module_name = excluded.tackle_module_name,
                tackle_type = excluded.tackle_type,
                quantity_destroyed = excluded.quantity_destroyed,
                quantity_dropped = excluded.quantity_dropped,
                source = excluded.source,
                updated_at_utc = excluded.updated_at_utc;
            ";
            command.Parameters.AddWithValue("$dayUtc", record.DayUtc);
            command.Parameters.AddWithValue("$characterId", record.CharacterId);
            command.Parameters.AddWithValue("$killmailId", record.KillmailId);
            command.Parameters.AddWithValue("$killmailTimeUtc", record.KillmailTimeUtc);
            command.Parameters.AddWithValue("$victimShipTypeId", (object?)record.VictimShipTypeId ?? DBNull.Value);
            command.Parameters.AddWithValue("$victimShipName", record.VictimShipName);
            command.Parameters.AddWithValue("$tackleModuleTypeId", record.TackleModuleTypeId);
            command.Parameters.AddWithValue("$tackleModuleName", record.TackleModuleName);
            command.Parameters.AddWithValue("$tackleType", record.TackleType.ToString());
            command.Parameters.AddWithValue("$quantityDestroyed", record.QuantityDestroyed);
            command.Parameters.AddWithValue("$quantityDropped", record.QuantityDropped);
            command.Parameters.AddWithValue("$source", record.Source);
            command.Parameters.AddWithValue("$updatedAtUtc", record.UpdatedAtUtc);
            command.ExecuteNonQuery();
        }

        private static void UpsertSeenRecord(
            SqliteConnection connection,
            SqliteTransaction transaction,
            long killmailId,
            string killmailHash,
            long sequenceId,
            string killmailTimeUtc,
            string dayUtc,
            string uploadedAtUtc,
            string processedAtUtc,
            string source,
            string processingStatus,
            string lastError)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            INSERT INTO live_killmail_seen (
                killmail_id,
                killmail_hash,
                first_sequence_id,
                last_sequence_id,
                killmail_time_utc,
                day_utc,
                uploaded_at_utc,
                processed_at_utc,
                source,
                processing_status,
                last_error
            )
            VALUES (
                $killmailId,
                $killmailHash,
                $firstSequenceId,
                $lastSequenceId,
                $killmailTimeUtc,
                $dayUtc,
                $uploadedAtUtc,
                $processedAtUtc,
                $source,
                $processingStatus,
                $lastError
            )
            ON CONFLICT(killmail_id) DO UPDATE SET
                killmail_hash = excluded.killmail_hash,
                last_sequence_id = excluded.last_sequence_id,
                killmail_time_utc = CASE
                    WHEN excluded.killmail_time_utc <> '' THEN excluded.killmail_time_utc
                    ELSE live_killmail_seen.killmail_time_utc
                END,
                day_utc = CASE
                    WHEN excluded.day_utc <> '' THEN excluded.day_utc
                    ELSE live_killmail_seen.day_utc
                END,
                uploaded_at_utc = CASE
                    WHEN excluded.uploaded_at_utc <> '' THEN excluded.uploaded_at_utc
                    ELSE live_killmail_seen.uploaded_at_utc
                END,
                processed_at_utc = excluded.processed_at_utc,
                source = excluded.source,
                processing_status = excluded.processing_status,
                last_error = excluded.last_error;
            ";
            command.Parameters.AddWithValue("$killmailId", killmailId);
            command.Parameters.AddWithValue("$killmailHash", killmailHash ?? "");
            command.Parameters.AddWithValue("$firstSequenceId", sequenceId);
            command.Parameters.AddWithValue("$lastSequenceId", sequenceId);
            command.Parameters.AddWithValue("$killmailTimeUtc", killmailTimeUtc ?? "");
            command.Parameters.AddWithValue("$dayUtc", dayUtc ?? "");
            command.Parameters.AddWithValue("$uploadedAtUtc", uploadedAtUtc ?? "");
            command.Parameters.AddWithValue("$processedAtUtc", processedAtUtc ?? "");
            command.Parameters.AddWithValue("$source", source ?? "r2z2");
            command.Parameters.AddWithValue("$processingStatus", processingStatus ?? "processed");
            command.Parameters.AddWithValue("$lastError", lastError ?? "");
            command.ExecuteNonQuery();
        }

        private static SeenKillmailRecord? LoadSeenRecord(
            SqliteConnection connection,
            SqliteTransaction? transaction,
            long killmailId)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            SELECT
                killmail_id,
                killmail_hash,
                first_sequence_id,
                last_sequence_id,
                killmail_time_utc,
                day_utc,
                uploaded_at_utc,
                processed_at_utc,
                source,
                processing_status,
                last_error
            FROM live_killmail_seen
            WHERE killmail_id = $killmailId
            LIMIT 1;
            ";
            command.Parameters.AddWithValue("$killmailId", killmailId);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            return new SeenKillmailRecord
            {
                KillmailId = reader.IsDBNull(0) ? 0 : reader.GetInt64(0),
                KillmailHash = reader.IsDBNull(1) ? "" : reader.GetString(1),
                FirstSequenceId = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
                LastSequenceId = reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                KillmailTimeUtc = reader.IsDBNull(4) ? "" : reader.GetString(4),
                DayUtc = reader.IsDBNull(5) ? "" : reader.GetString(5),
                UploadedAtUtc = reader.IsDBNull(6) ? "" : reader.GetString(6),
                ProcessedAtUtc = reader.IsDBNull(7) ? "" : reader.GetString(7),
                Source = reader.IsDBNull(8) ? "" : reader.GetString(8),
                ProcessingStatus = reader.IsDBNull(9) ? "" : reader.GetString(9),
                LastError = reader.IsDBNull(10) ? "" : reader.GetString(10)
            };
        }
    }

    public sealed class IncrementalKillmailImportRequest
    {
        public long KillmailId { get; set; }
        public string KillmailHash { get; set; } = "";
        public string KillmailJson { get; set; } = "";
        public string Source { get; set; } = "r2z2";
        public long SequenceId { get; set; }
        public string UploadedAtUtc { get; set; } = "";
    }

    public sealed class IncrementalKillmailImportResult
    {
        public bool Success { get; set; }
        public bool WasDuplicate { get; set; }
        public string KillmailId { get; set; } = "";
        public string DayUtc { get; set; } = "";
        public string Error { get; set; } = "";
        public int RegistryObservationCount { get; set; }
        public int FleetObservationCount { get; set; }
        public int ShipObservationCount { get; set; }
        public int CynoObservationCount { get; set; }
        public int BaitObservationCount { get; set; }
        public int TackleObservationCount { get; set; }
    }

    public sealed class SeenKillmailRecord
    {
        public long KillmailId { get; set; }
        public string KillmailHash { get; set; } = "";
        public long FirstSequenceId { get; set; }
        public long LastSequenceId { get; set; }
        public string KillmailTimeUtc { get; set; } = "";
        public string DayUtc { get; set; } = "";
        public string UploadedAtUtc { get; set; } = "";
        public string ProcessedAtUtc { get; set; } = "";
        public string Source { get; set; } = "";
        public string ProcessingStatus { get; set; } = "";
        public string LastError { get; set; } = "";
    }
}
