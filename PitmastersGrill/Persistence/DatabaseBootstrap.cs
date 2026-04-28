using Microsoft.Data.Sqlite;

namespace PitmastersGrill.Persistence
{
    public class DatabaseBootstrap
    {
        private readonly string _databasePath;

        public DatabaseBootstrap(string databasePath)
        {
            _databasePath = databasePath;
        }

        public void Initialize()
        {
            var connectionString = $"Data Source={_databasePath}";

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                @"
                CREATE TABLE IF NOT EXISTS app_metadata (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS notes (
                    character_name TEXT PRIMARY KEY,
                    notes_tags TEXT NOT NULL DEFAULT '',
                    known_cyno_override INTEGER NOT NULL DEFAULT 0,
                    bait_override INTEGER NOT NULL DEFAULT 0,
                    updated_at_utc TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS resolver_cache (
                    character_name TEXT PRIMARY KEY,
                    character_id TEXT NOT NULL DEFAULT '',
                    alliance_id TEXT NOT NULL DEFAULT '',
                    alliance_name TEXT NOT NULL DEFAULT '',
                    alliance_ticker TEXT NOT NULL DEFAULT '',
                    corp_id TEXT NOT NULL DEFAULT '',
                    corp_name TEXT NOT NULL DEFAULT '',
                    corp_ticker TEXT NOT NULL DEFAULT '',
                    resolver_confidence TEXT NOT NULL DEFAULT '',
                    resolved_at_utc TEXT NOT NULL,
                    expires_at_utc TEXT NOT NULL,
                    affiliation_checked_at_utc TEXT NOT NULL DEFAULT ''
                );

                CREATE TABLE IF NOT EXISTS stats_cache (
                    character_id TEXT PRIMARY KEY,
                    kill_count INTEGER NOT NULL DEFAULT 0,
                    loss_count INTEGER NOT NULL DEFAULT 0,
                    avg_attackers_when_attacking REAL NOT NULL DEFAULT 0,
                    last_public_cyno_capable_hull TEXT NOT NULL DEFAULT '',
                    last_ship_seen_name TEXT NOT NULL DEFAULT '',
                    last_ship_seen_at_utc TEXT NOT NULL DEFAULT '',
                    refreshed_at_utc TEXT NOT NULL,
                    expires_at_utc TEXT NOT NULL
                );
                ";
                command.ExecuteNonQuery();
            }

            EnsureColumnExists(
                connection,
                "notes",
                "known_cyno_override",
                "INTEGER NOT NULL DEFAULT 0");

            EnsureColumnExists(
                connection,
                "notes",
                "bait_override",
                "INTEGER NOT NULL DEFAULT 0");

            EnsureColumnExists(
                connection,
                "resolver_cache",
                "alliance_id",
                "TEXT NOT NULL DEFAULT ''");

            EnsureColumnExists(
                connection,
                "resolver_cache",
                "affiliation_checked_at_utc",
                "TEXT NOT NULL DEFAULT ''");

            EnsureColumnExists(
                connection,
                "resolver_cache",
                "corp_id",
                "TEXT NOT NULL DEFAULT ''");

            EnsureColumnExists(
                connection,
                "stats_cache",
                "last_ship_seen_name",
                "TEXT NOT NULL DEFAULT ''");

            EnsureColumnExists(
                connection,
                "stats_cache",
                "last_ship_seen_at_utc",
                "TEXT NOT NULL DEFAULT ''");

            using (var metadataCommand = connection.CreateCommand())
            {
                metadataCommand.CommandText =
                @"
                INSERT OR REPLACE INTO app_metadata (key, value)
                VALUES ('schema_version', '10');
                ";
                metadataCommand.ExecuteNonQuery();
            }
        }

        private static void EnsureColumnExists(
            SqliteConnection connection,
            string tableName,
            string columnName,
            string columnDefinition)
        {
            if (ColumnExists(connection, tableName, columnName))
            {
                return;
            }

            using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
            alterCommand.ExecuteNonQuery();
        }

        private static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({tableName});";

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var existingColumnName = reader.GetString(1);

                if (string.Equals(existingColumnName, columnName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
