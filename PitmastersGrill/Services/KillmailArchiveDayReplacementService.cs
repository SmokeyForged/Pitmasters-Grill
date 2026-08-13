using Microsoft.Data.Sqlite;
using PitmastersGrill.Models;
using System;
using System.Collections.Generic;

namespace PitmastersGrill.Services
{
    public sealed class KillmailArchiveDayReplacementService
    {
        private readonly string _databasePath;

        public KillmailArchiveDayReplacementService(string databasePath)
        {
            _databasePath = string.IsNullOrWhiteSpace(databasePath)
                ? throw new ArgumentException("Database path is required.", nameof(databasePath))
                : databasePath;
        }

        public void ReplaceDay(
            string dayUtc,
            IReadOnlyList<PilotRegistryDayRecord> registryRecords,
            IReadOnlyList<PilotFleetObservationDayRecord> fleetRecords,
            IReadOnlyList<PilotShipObservationDayRecord> shipRecords,
            IReadOnlyList<PilotCynoModuleObservationDayRecord> cynoModuleRecords,
            IReadOnlyList<PilotBaitObservationDayRecord> baitRecords,
            IReadOnlyList<PilotCynoTackleObservationDayRecord> cynoTackleRecords)
        {
            if (string.IsNullOrWhiteSpace(dayUtc))
            {
                throw new ArgumentException("Day is required.", nameof(dayUtc));
            }

            registryRecords ??= Array.Empty<PilotRegistryDayRecord>();
            fleetRecords ??= Array.Empty<PilotFleetObservationDayRecord>();
            shipRecords ??= Array.Empty<PilotShipObservationDayRecord>();
            cynoModuleRecords ??= Array.Empty<PilotCynoModuleObservationDayRecord>();
            baitRecords ??= Array.Empty<PilotBaitObservationDayRecord>();
            cynoTackleRecords ??= Array.Empty<PilotCynoTackleObservationDayRecord>();

            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();
            using var transaction = connection.BeginTransaction();

            ReplaceRegistry(connection, transaction, dayUtc, registryRecords);
            ReplaceFleet(connection, transaction, dayUtc, fleetRecords);
            ReplaceShip(connection, transaction, dayUtc, shipRecords);
            ReplaceCynoModules(connection, transaction, dayUtc, cynoModuleRecords);
            ReplaceBait(connection, transaction, dayUtc, baitRecords);
            ReplaceCynoTackle(connection, transaction, dayUtc, cynoTackleRecords);

            transaction.Commit();
        }

        private static void ReplaceRegistry(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string dayUtc,
            IReadOnlyList<PilotRegistryDayRecord> records)
        {
            DeleteDay(connection, transaction, "pilot_registry_day", dayUtc);

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            INSERT INTO pilot_registry_day (
                day_utc, character_id, first_seen_killmail_time_utc,
                last_seen_killmail_time_utc, seen_count, updated_at_utc)
            VALUES ($dayUtc, $characterId, $firstSeen, $lastSeen, $seenCount, $updatedAtUtc);
            ";
            command.Parameters.Add("$dayUtc", SqliteType.Text);
            command.Parameters.Add("$characterId", SqliteType.Text);
            command.Parameters.Add("$firstSeen", SqliteType.Text);
            command.Parameters.Add("$lastSeen", SqliteType.Text);
            command.Parameters.Add("$seenCount", SqliteType.Integer);
            command.Parameters.Add("$updatedAtUtc", SqliteType.Text);
            command.Prepare();

            foreach (var record in records)
            {
                command.Parameters["$dayUtc"].Value = record.DayUtc;
                command.Parameters["$characterId"].Value = record.CharacterId;
                command.Parameters["$firstSeen"].Value = record.FirstSeenKillmailTimeUtc;
                command.Parameters["$lastSeen"].Value = record.LastSeenKillmailTimeUtc;
                command.Parameters["$seenCount"].Value = record.SeenCount;
                command.Parameters["$updatedAtUtc"].Value = record.UpdatedAtUtc;
                command.ExecuteNonQuery();
            }
        }

        private static void ReplaceFleet(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string dayUtc,
            IReadOnlyList<PilotFleetObservationDayRecord> records)
        {
            DeleteDay(connection, transaction, "pilot_fleet_observations_day", dayUtc);

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            INSERT INTO pilot_fleet_observations_day (
                day_utc, character_id, attacker_sample_count, attacker_count_sum, derived_at_utc)
            VALUES ($dayUtc, $characterId, $sampleCount, $countSum, $derivedAtUtc);
            ";
            command.Parameters.Add("$dayUtc", SqliteType.Text);
            command.Parameters.Add("$characterId", SqliteType.Text);
            command.Parameters.Add("$sampleCount", SqliteType.Integer);
            command.Parameters.Add("$countSum", SqliteType.Integer);
            command.Parameters.Add("$derivedAtUtc", SqliteType.Text);
            command.Prepare();

            foreach (var record in records)
            {
                command.Parameters["$dayUtc"].Value = record.DayUtc;
                command.Parameters["$characterId"].Value = record.CharacterId;
                command.Parameters["$sampleCount"].Value = record.AttackerSampleCount;
                command.Parameters["$countSum"].Value = record.AttackerCountSum;
                command.Parameters["$derivedAtUtc"].Value = record.DerivedAtUtc;
                command.ExecuteNonQuery();
            }
        }

        private static void ReplaceShip(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string dayUtc,
            IReadOnlyList<PilotShipObservationDayRecord> records)
        {
            DeleteDay(connection, transaction, "pilot_ship_observations_day", dayUtc);

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            INSERT INTO pilot_ship_observations_day (
                day_utc, character_id, last_seen_ship_type_id, last_seen_ship_time_utc,
                last_seen_cyno_ship_type_id, last_seen_cyno_ship_name,
                last_seen_cyno_ship_time_utc, updated_at_utc)
            VALUES (
                $dayUtc, $characterId, $lastSeenShipTypeId, $lastSeenShipTimeUtc,
                $lastSeenCynoShipTypeId, $lastSeenCynoShipName,
                $lastSeenCynoShipTimeUtc, $updatedAtUtc);
            ";
            command.Parameters.Add("$dayUtc", SqliteType.Text);
            command.Parameters.Add("$characterId", SqliteType.Text);
            command.Parameters.Add("$lastSeenShipTypeId", SqliteType.Integer);
            command.Parameters.Add("$lastSeenShipTimeUtc", SqliteType.Text);
            command.Parameters.Add("$lastSeenCynoShipTypeId", SqliteType.Integer);
            command.Parameters.Add("$lastSeenCynoShipName", SqliteType.Text);
            command.Parameters.Add("$lastSeenCynoShipTimeUtc", SqliteType.Text);
            command.Parameters.Add("$updatedAtUtc", SqliteType.Text);
            command.Prepare();

            foreach (var record in records)
            {
                command.Parameters["$dayUtc"].Value = record.DayUtc;
                command.Parameters["$characterId"].Value = record.CharacterId;
                command.Parameters["$lastSeenShipTypeId"].Value = (object?)record.LastSeenShipTypeId ?? DBNull.Value;
                command.Parameters["$lastSeenShipTimeUtc"].Value = record.LastSeenShipTimeUtc;
                command.Parameters["$lastSeenCynoShipTypeId"].Value = (object?)record.LastSeenCynoShipTypeId ?? DBNull.Value;
                command.Parameters["$lastSeenCynoShipName"].Value = record.LastSeenCynoShipName ?? "";
                command.Parameters["$lastSeenCynoShipTimeUtc"].Value = record.LastSeenCynoShipTimeUtc ?? "";
                command.Parameters["$updatedAtUtc"].Value = record.UpdatedAtUtc;
                command.ExecuteNonQuery();
            }
        }

        private static void ReplaceCynoModules(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string dayUtc,
            IReadOnlyList<PilotCynoModuleObservationDayRecord> records)
        {
            DeleteDay(connection, transaction, "pilot_cyno_module_observations_day", dayUtc);

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            INSERT INTO pilot_cyno_module_observations_day (
                day_utc, character_id, killmail_id, killmail_time_utc, victim_ship_type_id,
                module_type_id, module_name, quantity_destroyed, quantity_dropped,
                item_state, source, updated_at_utc)
            VALUES (
                $dayUtc, $characterId, $killmailId, $killmailTimeUtc, $victimShipTypeId,
                $moduleTypeId, $moduleName, $quantityDestroyed, $quantityDropped,
                $itemState, $source, $updatedAtUtc)
            ON CONFLICT(day_utc, character_id, killmail_id, module_type_id)
            DO UPDATE SET
                killmail_time_utc = excluded.killmail_time_utc,
                victim_ship_type_id = excluded.victim_ship_type_id,
                module_name = excluded.module_name,
                quantity_destroyed = excluded.quantity_destroyed,
                quantity_dropped = excluded.quantity_dropped,
                item_state = excluded.item_state,
                source = excluded.source,
                updated_at_utc = excluded.updated_at_utc;
            ";
            command.Parameters.Add("$dayUtc", SqliteType.Text);
            command.Parameters.Add("$characterId", SqliteType.Text);
            command.Parameters.Add("$killmailId", SqliteType.Text);
            command.Parameters.Add("$killmailTimeUtc", SqliteType.Text);
            command.Parameters.Add("$victimShipTypeId", SqliteType.Integer);
            command.Parameters.Add("$moduleTypeId", SqliteType.Integer);
            command.Parameters.Add("$moduleName", SqliteType.Text);
            command.Parameters.Add("$quantityDestroyed", SqliteType.Integer);
            command.Parameters.Add("$quantityDropped", SqliteType.Integer);
            command.Parameters.Add("$itemState", SqliteType.Text);
            command.Parameters.Add("$source", SqliteType.Text);
            command.Parameters.Add("$updatedAtUtc", SqliteType.Text);
            command.Prepare();

            foreach (var record in records)
            {
                command.Parameters["$dayUtc"].Value = record.DayUtc;
                command.Parameters["$characterId"].Value = record.CharacterId;
                command.Parameters["$killmailId"].Value = record.KillmailId;
                command.Parameters["$killmailTimeUtc"].Value = record.KillmailTimeUtc;
                command.Parameters["$victimShipTypeId"].Value = (object?)record.VictimShipTypeId ?? DBNull.Value;
                command.Parameters["$moduleTypeId"].Value = record.ModuleTypeId;
                command.Parameters["$moduleName"].Value = record.ModuleName;
                command.Parameters["$quantityDestroyed"].Value = record.QuantityDestroyed;
                command.Parameters["$quantityDropped"].Value = record.QuantityDropped;
                command.Parameters["$itemState"].Value = record.ItemState;
                command.Parameters["$source"].Value = record.Source;
                command.Parameters["$updatedAtUtc"].Value = record.UpdatedAtUtc;
                command.ExecuteNonQuery();
            }
        }

        private static void ReplaceBait(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string dayUtc,
            IReadOnlyList<PilotBaitObservationDayRecord> records)
        {
            DeleteDay(connection, transaction, "pilot_bait_observations_day", dayUtc);

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            INSERT INTO pilot_bait_observations_day (
                day_utc, character_id, killmail_id, killmail_time_utc, victim_ship_type_id,
                victim_ship_name, solar_system_id, solar_system_name,
                industrial_cyno_module_type_id, industrial_cyno_module_name,
                tackle_module_type_id, tackle_module_name, tackle_type,
                quantity_destroyed, quantity_dropped, source, updated_at_utc)
            VALUES (
                $dayUtc, $characterId, $killmailId, $killmailTimeUtc, $victimShipTypeId,
                $victimShipName, $solarSystemId, $solarSystemName,
                $industrialCynoModuleTypeId, $industrialCynoModuleName,
                $tackleModuleTypeId, $tackleModuleName, $tackleType,
                $quantityDestroyed, $quantityDropped, $source, $updatedAtUtc)
            ON CONFLICT(day_utc, character_id, killmail_id, tackle_module_type_id)
            DO UPDATE SET
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
            command.Parameters.Add("$dayUtc", SqliteType.Text);
            command.Parameters.Add("$characterId", SqliteType.Text);
            command.Parameters.Add("$killmailId", SqliteType.Text);
            command.Parameters.Add("$killmailTimeUtc", SqliteType.Text);
            command.Parameters.Add("$victimShipTypeId", SqliteType.Integer);
            command.Parameters.Add("$victimShipName", SqliteType.Text);
            command.Parameters.Add("$solarSystemId", SqliteType.Integer);
            command.Parameters.Add("$solarSystemName", SqliteType.Text);
            command.Parameters.Add("$industrialCynoModuleTypeId", SqliteType.Integer);
            command.Parameters.Add("$industrialCynoModuleName", SqliteType.Text);
            command.Parameters.Add("$tackleModuleTypeId", SqliteType.Integer);
            command.Parameters.Add("$tackleModuleName", SqliteType.Text);
            command.Parameters.Add("$tackleType", SqliteType.Text);
            command.Parameters.Add("$quantityDestroyed", SqliteType.Integer);
            command.Parameters.Add("$quantityDropped", SqliteType.Integer);
            command.Parameters.Add("$source", SqliteType.Text);
            command.Parameters.Add("$updatedAtUtc", SqliteType.Text);
            command.Prepare();

            foreach (var record in records)
            {
                command.Parameters["$dayUtc"].Value = record.DayUtc;
                command.Parameters["$characterId"].Value = record.CharacterId;
                command.Parameters["$killmailId"].Value = record.KillmailId;
                command.Parameters["$killmailTimeUtc"].Value = record.KillmailTimeUtc;
                command.Parameters["$victimShipTypeId"].Value = (object?)record.VictimShipTypeId ?? DBNull.Value;
                command.Parameters["$victimShipName"].Value = record.VictimShipName;
                command.Parameters["$solarSystemId"].Value = (object?)record.SolarSystemId ?? DBNull.Value;
                command.Parameters["$solarSystemName"].Value = record.SolarSystemName;
                command.Parameters["$industrialCynoModuleTypeId"].Value = record.IndustrialCynoModuleTypeId;
                command.Parameters["$industrialCynoModuleName"].Value = record.IndustrialCynoModuleName;
                command.Parameters["$tackleModuleTypeId"].Value = record.TackleModuleTypeId;
                command.Parameters["$tackleModuleName"].Value = record.TackleModuleName;
                command.Parameters["$tackleType"].Value = record.TackleType.ToString();
                command.Parameters["$quantityDestroyed"].Value = record.QuantityDestroyed;
                command.Parameters["$quantityDropped"].Value = record.QuantityDropped;
                command.Parameters["$source"].Value = record.Source;
                command.Parameters["$updatedAtUtc"].Value = record.UpdatedAtUtc;
                command.ExecuteNonQuery();
            }
        }

        private static void ReplaceCynoTackle(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string dayUtc,
            IReadOnlyList<PilotCynoTackleObservationDayRecord> records)
        {
            DeleteDay(connection, transaction, "pilot_cyno_tackle_observations_day", dayUtc);

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            INSERT INTO pilot_cyno_tackle_observations_day (
                day_utc, character_id, killmail_id, killmail_time_utc, victim_ship_type_id,
                victim_ship_name, tackle_module_type_id, tackle_module_name, tackle_type,
                quantity_destroyed, quantity_dropped, source, updated_at_utc)
            VALUES (
                $dayUtc, $characterId, $killmailId, $killmailTimeUtc, $victimShipTypeId,
                $victimShipName, $tackleModuleTypeId, $tackleModuleName, $tackleType,
                $quantityDestroyed, $quantityDropped, $source, $updatedAtUtc)
            ON CONFLICT(day_utc, character_id, killmail_id, tackle_module_type_id)
            DO UPDATE SET
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
            command.Parameters.Add("$dayUtc", SqliteType.Text);
            command.Parameters.Add("$characterId", SqliteType.Text);
            command.Parameters.Add("$killmailId", SqliteType.Text);
            command.Parameters.Add("$killmailTimeUtc", SqliteType.Text);
            command.Parameters.Add("$victimShipTypeId", SqliteType.Integer);
            command.Parameters.Add("$victimShipName", SqliteType.Text);
            command.Parameters.Add("$tackleModuleTypeId", SqliteType.Integer);
            command.Parameters.Add("$tackleModuleName", SqliteType.Text);
            command.Parameters.Add("$tackleType", SqliteType.Text);
            command.Parameters.Add("$quantityDestroyed", SqliteType.Integer);
            command.Parameters.Add("$quantityDropped", SqliteType.Integer);
            command.Parameters.Add("$source", SqliteType.Text);
            command.Parameters.Add("$updatedAtUtc", SqliteType.Text);
            command.Prepare();

            foreach (var record in records)
            {
                command.Parameters["$dayUtc"].Value = record.DayUtc;
                command.Parameters["$characterId"].Value = record.CharacterId;
                command.Parameters["$killmailId"].Value = record.KillmailId;
                command.Parameters["$killmailTimeUtc"].Value = record.KillmailTimeUtc;
                command.Parameters["$victimShipTypeId"].Value = (object?)record.VictimShipTypeId ?? DBNull.Value;
                command.Parameters["$victimShipName"].Value = record.VictimShipName;
                command.Parameters["$tackleModuleTypeId"].Value = record.TackleModuleTypeId;
                command.Parameters["$tackleModuleName"].Value = record.TackleModuleName;
                command.Parameters["$tackleType"].Value = record.TackleType.ToString();
                command.Parameters["$quantityDestroyed"].Value = record.QuantityDestroyed;
                command.Parameters["$quantityDropped"].Value = record.QuantityDropped;
                command.Parameters["$source"].Value = record.Source;
                command.Parameters["$updatedAtUtc"].Value = record.UpdatedAtUtc;
                command.ExecuteNonQuery();
            }
        }

        private static void DeleteDay(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string tableName,
            string dayUtc)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"DELETE FROM {tableName} WHERE day_utc = $dayUtc;";
            command.Parameters.AddWithValue("$dayUtc", dayUtc);
            command.ExecuteNonQuery();
        }
    }
}
