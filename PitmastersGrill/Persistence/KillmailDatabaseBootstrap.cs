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

            SetMetadataValue(connection, "schema_version", "6");
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
