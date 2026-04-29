using System;
using Microsoft.Data.Sqlite;

namespace PitmastersGrill.Persistence
{
    public sealed class WatchedPilotRepository
    {
        private readonly string _databasePath;

        public WatchedPilotRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        public bool IsWatched(string characterId)
        {
            var normalizedCharacterId = NormalizeCharacterId(characterId);
            if (normalizedCharacterId == null)
            {
                return false;
            }

            using var connection = OpenConnection();
            EnsureTableExists(connection);

            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            SELECT 1
            FROM watched_pilots
            WHERE character_id = $characterId
            LIMIT 1;
            ";
            command.Parameters.AddWithValue("$characterId", normalizedCharacterId);

            return command.ExecuteScalar() != null;
        }

        public bool SetWatched(string characterId, bool isWatched)
        {
            var normalizedCharacterId = NormalizeCharacterId(characterId);
            if (normalizedCharacterId == null)
            {
                return false;
            }

            using var connection = OpenConnection();
            EnsureTableExists(connection);

            using var command = connection.CreateCommand();
            if (isWatched)
            {
                command.CommandText =
                @"
                INSERT INTO watched_pilots (character_id, watched_at_utc)
                VALUES ($characterId, $watchedAtUtc)
                ON CONFLICT(character_id) DO UPDATE SET
                    watched_at_utc = excluded.watched_at_utc;
                ";
                command.Parameters.AddWithValue("$watchedAtUtc", DateTime.UtcNow.ToString("o"));
            }
            else
            {
                command.CommandText =
                @"
                DELETE FROM watched_pilots
                WHERE character_id = $characterId;
                ";
            }

            command.Parameters.AddWithValue("$characterId", normalizedCharacterId);
            command.ExecuteNonQuery();
            return true;
        }

        private SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();
            return connection;
        }

        private static void EnsureTableExists(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            CREATE TABLE IF NOT EXISTS watched_pilots (
                character_id TEXT PRIMARY KEY,
                watched_at_utc TEXT NOT NULL
            );
            ";
            command.ExecuteNonQuery();
        }

        private static string? NormalizeCharacterId(string characterId)
        {
            return long.TryParse(characterId, out var parsedCharacterId) && parsedCharacterId > 0
                ? parsedCharacterId.ToString()
                : null;
        }
    }
}
