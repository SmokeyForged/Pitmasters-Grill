using Microsoft.Data.Sqlite;
using System;

namespace PitmastersGrill.Persistence
{
    public class KillmailDatabaseBootstrap
    {
        private readonly string _databasePath;

        public KillmailDatabaseBootstrap(string databasePath)
        {
            _databasePath = databasePath;
        }

        public void Initialize()
        {
            var connectionString = $"Data Source={_databasePath}";

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            CreateBaseTables(connection);

            EnsureColumnExists(connection, "pilot_ship_observations_day", "last_seen_cyno_ship_type_id", "INTEGER NULL");
            EnsureColumnExists(connection, "pilot_ship_observations_day", "last_seen_cyno_ship_name", "TEXT NOT NULL DEFAULT ''");
            EnsureColumnExists(connection, "pilot_ship_observations_day", "last_seen_cyno_ship_time_utc", "TEXT NOT NULL DEFAULT ''");

            CreateIndexes(connection);

            SetMetadataValue(connection, "schema_version", "10");
            SetMetadataValueIfMissing(connection, "seed_version", "");
            SetMetadataValueIfMissing(connection, "seed_built_at_utc", "");
            SetMetadataValueIfMissing(connection, "last_startup_check_at_utc", "");
            SetMetadataValueIfMissing(connection, "last_successful_update_at_utc", "");
            SetMetadataValueIfMissing(connection, "latest_complete_day_utc", "");
        }

        private static void CreateBaseTables(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            CREATE TABLE IF NOT EXISTS dataset_metadata (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS day_import_state (
                day_utc TEXT PRIMARY KEY,
                remote_total_count INTEGER NOT NULL DEFAULT 0,
                local_imported_count INTEGER NOT NULL DEFAULT 0,
                state TEXT NOT NULL DEFAULT 'not_present',
                archive_etag TEXT NOT NULL DEFAULT '',
                archive_last_modified TEXT NOT NULL DEFAULT '',
                checked_at_utc TEXT NOT NULL DEFAULT '',
                downloaded_at_utc TEXT NOT NULL DEFAULT '',
                imported_at_utc TEXT NOT NULL DEFAULT '',
                normalized_at_utc TEXT NOT NULL DEFAULT '',
                completed_at_utc TEXT NOT NULL DEFAULT '',
                last_error TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS pilot_registry_day (
                day_utc TEXT NOT NULL,
                character_id TEXT NOT NULL,
                first_seen_killmail_time_utc TEXT NOT NULL DEFAULT '',
                last_seen_killmail_time_utc TEXT NOT NULL DEFAULT '',
                seen_count INTEGER NOT NULL DEFAULT 0,
                updated_at_utc TEXT NOT NULL DEFAULT '',
                PRIMARY KEY (day_utc, character_id)
            );

            CREATE TABLE IF NOT EXISTS pilot_fleet_observations_day (
                day_utc TEXT NOT NULL,
                character_id TEXT NOT NULL,
                attacker_sample_count INTEGER NOT NULL DEFAULT 0,
                attacker_count_sum INTEGER NOT NULL DEFAULT 0,
                derived_at_utc TEXT NOT NULL DEFAULT '',
                PRIMARY KEY (day_utc, character_id)
            );

            CREATE TABLE IF NOT EXISTS pilot_ship_observations_day (
                day_utc TEXT NOT NULL,
                character_id TEXT NOT NULL,
                last_seen_ship_type_id INTEGER NULL,
                last_seen_ship_time_utc TEXT NOT NULL DEFAULT '',
                updated_at_utc TEXT NOT NULL DEFAULT '',
                PRIMARY KEY (day_utc, character_id)
            );

            CREATE TABLE IF NOT EXISTS pilot_cyno_module_observations_day (
                day_utc TEXT NOT NULL,
                character_id TEXT NOT NULL,
                killmail_id TEXT NOT NULL DEFAULT '',
                killmail_time_utc TEXT NOT NULL DEFAULT '',
                victim_ship_type_id INTEGER NULL,
                module_type_id INTEGER NOT NULL,
                module_name TEXT NOT NULL DEFAULT '',
                quantity_destroyed INTEGER NOT NULL DEFAULT 0,
                quantity_dropped INTEGER NOT NULL DEFAULT 0,
                item_state TEXT NOT NULL DEFAULT '',
                source TEXT NOT NULL DEFAULT '',
                updated_at_utc TEXT NOT NULL DEFAULT '',
                PRIMARY KEY (day_utc, character_id, killmail_id, module_type_id)
            );

            CREATE TABLE IF NOT EXISTS pilot_bait_observations_day (
                day_utc TEXT NOT NULL,
                character_id TEXT NOT NULL,
                killmail_id TEXT NOT NULL DEFAULT '',
                killmail_time_utc TEXT NOT NULL DEFAULT '',
                victim_ship_type_id INTEGER NULL,
                victim_ship_name TEXT NOT NULL DEFAULT '',
                solar_system_id INTEGER NULL,
                solar_system_name TEXT NOT NULL DEFAULT '',
                industrial_cyno_module_type_id INTEGER NOT NULL,
                industrial_cyno_module_name TEXT NOT NULL DEFAULT '',
                tackle_module_type_id INTEGER NOT NULL,
                tackle_module_name TEXT NOT NULL DEFAULT '',
                tackle_type TEXT NOT NULL DEFAULT '',
                quantity_destroyed INTEGER NOT NULL DEFAULT 0,
                quantity_dropped INTEGER NOT NULL DEFAULT 0,
                source TEXT NOT NULL DEFAULT '',
                updated_at_utc TEXT NOT NULL DEFAULT '',
                PRIMARY KEY (day_utc, character_id, killmail_id, tackle_module_type_id)
            );

            CREATE TABLE IF NOT EXISTS pilot_cyno_tackle_observations_day (
                day_utc TEXT NOT NULL,
                character_id TEXT NOT NULL,
                killmail_id TEXT NOT NULL DEFAULT '',
                killmail_time_utc TEXT NOT NULL DEFAULT '',
                victim_ship_type_id INTEGER NULL,
                victim_ship_name TEXT NOT NULL DEFAULT '',
                tackle_module_type_id INTEGER NOT NULL,
                tackle_module_name TEXT NOT NULL DEFAULT '',
                tackle_type TEXT NOT NULL DEFAULT '',
                quantity_destroyed INTEGER NOT NULL DEFAULT 0,
                quantity_dropped INTEGER NOT NULL DEFAULT 0,
                source TEXT NOT NULL DEFAULT '',
                updated_at_utc TEXT NOT NULL DEFAULT '',
                PRIMARY KEY (day_utc, character_id, killmail_id, tackle_module_type_id)
            );

            CREATE TABLE IF NOT EXISTS live_killmail_feed_state (
                feed_name TEXT PRIMARY KEY,
                enabled INTEGER NOT NULL DEFAULT 0,
                next_sequence_id INTEGER NULL,
                last_processed_sequence_id INTEGER NULL,
                last_success_at_utc TEXT NOT NULL DEFAULT '',
                last_404_at_utc TEXT NOT NULL DEFAULT '',
                last_error_at_utc TEXT NOT NULL DEFAULT '',
                last_error TEXT NOT NULL DEFAULT '',
                status TEXT NOT NULL DEFAULT 'disabled',
                updated_at_utc TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS live_killmail_seen (
                killmail_id INTEGER PRIMARY KEY,
                killmail_hash TEXT NOT NULL DEFAULT '',
                first_sequence_id INTEGER NOT NULL,
                last_sequence_id INTEGER NOT NULL,
                killmail_time_utc TEXT NOT NULL DEFAULT '',
                day_utc TEXT NOT NULL DEFAULT '',
                uploaded_at_utc TEXT NOT NULL DEFAULT '',
                processed_at_utc TEXT NOT NULL DEFAULT '',
                source TEXT NOT NULL DEFAULT 'r2z2',
                processing_status TEXT NOT NULL DEFAULT 'processed',
                last_error TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS historical_freshness_checkpoint (
                character_id INTEGER NOT NULL,
                window_start_day_utc TEXT NOT NULL,
                window_end_day_utc TEXT NOT NULL,
                last_checked_at_utc TEXT NOT NULL DEFAULT '',
                last_status TEXT NOT NULL DEFAULT '',
                last_imported_count INTEGER NOT NULL DEFAULT 0,
                last_known_count INTEGER NOT NULL DEFAULT 0,
                last_failed_count INTEGER NOT NULL DEFAULT 0,
                last_error TEXT NOT NULL DEFAULT '',
                PRIMARY KEY (character_id, window_start_day_utc, window_end_day_utc)
            );
            ";
            command.ExecuteNonQuery();
        }

        private static void CreateIndexes(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            CREATE INDEX IF NOT EXISTS idx_day_import_state_state
                ON day_import_state(state);

            CREATE INDEX IF NOT EXISTS idx_pilot_registry_day_last_seen
                ON pilot_registry_day(day_utc, last_seen_killmail_time_utc);

            CREATE INDEX IF NOT EXISTS idx_pilot_fleet_observations_day_samples
                ON pilot_fleet_observations_day(day_utc, attacker_sample_count);

            CREATE INDEX IF NOT EXISTS idx_pilot_ship_observations_day_last_seen
                ON pilot_ship_observations_day(day_utc, last_seen_ship_time_utc);

            CREATE INDEX IF NOT EXISTS idx_pilot_ship_observations_day_last_seen_cyno
                ON pilot_ship_observations_day(day_utc, last_seen_cyno_ship_time_utc);

            CREATE INDEX IF NOT EXISTS idx_pilot_cyno_module_observations_character_time
                ON pilot_cyno_module_observations_day(character_id, killmail_time_utc);

            CREATE INDEX IF NOT EXISTS idx_pilot_cyno_module_observations_day
                ON pilot_cyno_module_observations_day(day_utc);

            CREATE INDEX IF NOT EXISTS idx_pilot_cyno_module_observations_module
                ON pilot_cyno_module_observations_day(module_type_id);

            CREATE INDEX IF NOT EXISTS idx_pilot_bait_observations_character_time
                ON pilot_bait_observations_day(character_id, killmail_time_utc);

            CREATE INDEX IF NOT EXISTS idx_pilot_bait_observations_day
                ON pilot_bait_observations_day(day_utc);

            CREATE INDEX IF NOT EXISTS idx_pilot_bait_observations_tackle_type
                ON pilot_bait_observations_day(tackle_type);

            CREATE INDEX IF NOT EXISTS idx_pilot_bait_observations_killmail
                ON pilot_bait_observations_day(killmail_id);

            CREATE INDEX IF NOT EXISTS idx_pilot_cyno_tackle_observations_character_time
                ON pilot_cyno_tackle_observations_day(character_id, killmail_time_utc);

            CREATE INDEX IF NOT EXISTS idx_pilot_cyno_tackle_observations_day
                ON pilot_cyno_tackle_observations_day(day_utc);

            CREATE INDEX IF NOT EXISTS idx_pilot_cyno_tackle_observations_tackle_type
                ON pilot_cyno_tackle_observations_day(tackle_type);

            CREATE INDEX IF NOT EXISTS idx_pilot_cyno_tackle_observations_killmail
                ON pilot_cyno_tackle_observations_day(killmail_id);

            CREATE INDEX IF NOT EXISTS idx_live_killmail_seen_sequence
                ON live_killmail_seen(last_sequence_id);

            CREATE INDEX IF NOT EXISTS idx_live_killmail_seen_day
                ON live_killmail_seen(day_utc);

            CREATE INDEX IF NOT EXISTS idx_historical_freshness_checkpoint_checked
                ON historical_freshness_checkpoint(last_checked_at_utc);
            ";
            command.ExecuteNonQuery();
        }

        private static void EnsureColumnExists(
            SqliteConnection connection,
            string tableName,
            string columnName,
            string columnDefinition)
        {
            using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = $"PRAGMA table_info({tableName});";

            using var reader = checkCommand.ExecuteReader();
            while (reader.Read())
            {
                var existingColumnName = reader.GetString(1);
                if (string.Equals(existingColumnName, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText =
                $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
            alterCommand.ExecuteNonQuery();
        }

        private static void SetMetadataValue(SqliteConnection connection, string key, string value)
        {
            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            INSERT INTO dataset_metadata (key, value)
            VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            ";
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", value);
            command.ExecuteNonQuery();
        }

        private static void SetMetadataValueIfMissing(SqliteConnection connection, string key, string value)
        {
            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            INSERT OR IGNORE INTO dataset_metadata (key, value)
            VALUES ($key, $value);
            ";
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", value);
            command.ExecuteNonQuery();
        }
    }
}
