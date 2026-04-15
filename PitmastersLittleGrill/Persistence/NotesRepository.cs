using System;
using Microsoft.Data.Sqlite;

namespace PitmastersLittleGrill.Persistence
{
    public class NotesRepository
    {
        private readonly string _databasePath;

        public NotesRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        public string GetNotes(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return string.Empty;
            }

            var connectionString = $"Data Source={_databasePath}";

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            EnsureTableAndColumnsExist(connection);

            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            SELECT notes_tags
            FROM notes
            WHERE character_name = $characterName
            LIMIT 1;
            ";
            command.Parameters.AddWithValue("$characterName", characterName);

            var result = command.ExecuteScalar();

            return result?.ToString() ?? string.Empty;
        }

        public bool GetKnownCynoOverride(string characterName)
        {
            return GetBooleanColumn(characterName, "known_cyno_override");
        }

        public bool GetBaitOverride(string characterName)
        {
            return GetBooleanColumn(characterName, "bait_override");
        }

        public void SaveNotes(string characterName, string notesTags)
        {
            SaveNotesAndTags(
                characterName,
                notesTags,
                GetKnownCynoOverride(characterName),
                GetBaitOverride(characterName));
        }

        public void SaveNotesAndTags(string characterName, string notesTags, bool knownCynoOverride, bool baitOverride)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return;
            }

            var safeNotes = notesTags ?? string.Empty;
            var utcNow = DateTime.UtcNow.ToString("o");
            var connectionString = $"Data Source={_databasePath}";

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            EnsureTableAndColumnsExist(connection);

            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            INSERT INTO notes (character_name, notes_tags, known_cyno_override, bait_override, updated_at_utc)
            VALUES ($characterName, $notesTags, $knownCynoOverride, $baitOverride, $updatedAtUtc)
            ON CONFLICT(character_name) DO UPDATE SET
                notes_tags = excluded.notes_tags,
                known_cyno_override = excluded.known_cyno_override,
                bait_override = excluded.bait_override,
                updated_at_utc = excluded.updated_at_utc;
            ";
            command.Parameters.AddWithValue("$characterName", characterName);
            command.Parameters.AddWithValue("$notesTags", safeNotes);
            command.Parameters.AddWithValue("$knownCynoOverride", knownCynoOverride ? 1 : 0);
            command.Parameters.AddWithValue("$baitOverride", baitOverride ? 1 : 0);
            command.Parameters.AddWithValue("$updatedAtUtc", utcNow);

            command.ExecuteNonQuery();
        }

        private bool GetBooleanColumn(string characterName, string columnName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return false;
            }

            var connectionString = $"Data Source={_databasePath}";

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            EnsureTableAndColumnsExist(connection);

            using var command = connection.CreateCommand();
            command.CommandText =
            $@"
            SELECT {columnName}
            FROM notes
            WHERE character_name = $characterName
            LIMIT 1;
            ";
            command.Parameters.AddWithValue("$characterName", characterName);

            var result = command.ExecuteScalar();

            if (result == null || result == DBNull.Value)
            {
                return false;
            }

            if (result is long longValue)
            {
                return longValue != 0;
            }

            if (int.TryParse(result.ToString(), out var intValue))
            {
                return intValue != 0;
            }

            return false;
        }

        private static void EnsureTableAndColumnsExist(SqliteConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                @"
                CREATE TABLE IF NOT EXISTS notes (
                    character_name TEXT PRIMARY KEY,
                    notes_tags TEXT NOT NULL DEFAULT '',
                    known_cyno_override INTEGER NOT NULL DEFAULT 0,
                    bait_override INTEGER NOT NULL DEFAULT 0,
                    updated_at_utc TEXT NOT NULL
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
        }

        private static void EnsureColumnExists(
            SqliteConnection connection,
            string tableName,
            string columnName,
            string columnDefinition)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({tableName});";

            using var reader = command.ExecuteReader();

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
    }
}