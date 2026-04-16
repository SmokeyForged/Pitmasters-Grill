using Microsoft.Data.Sqlite;
using PitmastersGrill.Models;
using System;

namespace PitmastersGrill.Persistence
{
    public class StatsCacheRepository
    {
        private readonly string _databasePath;

        public StatsCacheRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        public StatsCacheEntry? GetByCharacterId(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return null;
            }

            var connectionString = $"Data Source={_databasePath}";

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            EnsureTableExists(connection);

            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            SELECT
                character_id,
                kill_count,
                loss_count,
                avg_attackers_when_attacking,
                last_public_cyno_capable_hull,
                last_ship_seen_name,
                last_ship_seen_at_utc,
                refreshed_at_utc,
                expires_at_utc
            FROM stats_cache
            WHERE character_id = $characterId
            LIMIT 1;
            ";
            command.Parameters.AddWithValue("$characterId", characterId);

            using var reader = command.ExecuteReader();

            if (!reader.Read())
            {
                return null;
            }

            return new StatsCacheEntry
            {
                CharacterId = reader.GetString(0),
                KillCount = reader.GetInt32(1),
                LossCount = reader.GetInt32(2),
                AvgAttackersWhenAttacking = reader.IsDBNull(3) ? 0 : reader.GetDouble(3),
                LastPublicCynoCapableHull = reader.IsDBNull(4) ? "" : reader.GetString(4),
                LastShipSeenName = reader.IsDBNull(5) ? "" : reader.GetString(5),
                LastShipSeenAtUtc = reader.IsDBNull(6) ? "" : reader.GetString(6),
                RefreshedAtUtc = reader.IsDBNull(7) ? "" : reader.GetString(7),
                ExpiresAtUtc = reader.IsDBNull(8) ? "" : reader.GetString(8)
            };
        }

        public void Upsert(StatsCacheEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.CharacterId))
            {
                return;
            }

            var connectionString = $"Data Source={_databasePath}";

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            EnsureTableExists(connection);

            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            INSERT INTO stats_cache (
                character_id,
                kill_count,
                loss_count,
                avg_attackers_when_attacking,
                last_public_cyno_capable_hull,
                last_ship_seen_name,
                last_ship_seen_at_utc,
                refreshed_at_utc,
                expires_at_utc
            )
            VALUES (
                $characterId,
                $killCount,
                $lossCount,
                $avgAttackersWhenAttacking,
                $lastPublicCynoCapableHull,
                $lastShipSeenName,
                $lastShipSeenAtUtc,
                $refreshedAtUtc,
                $expiresAtUtc
            )
            ON CONFLICT(character_id) DO UPDATE SET
                kill_count = excluded.kill_count,
                loss_count = excluded.loss_count,
                avg_attackers_when_attacking = excluded.avg_attackers_when_attacking,
                last_public_cyno_capable_hull = excluded.last_public_cyno_capable_hull,
                last_ship_seen_name = excluded.last_ship_seen_name,
                last_ship_seen_at_utc = excluded.last_ship_seen_at_utc,
                refreshed_at_utc = excluded.refreshed_at_utc,
                expires_at_utc = excluded.expires_at_utc;
            ";

            command.Parameters.AddWithValue("$characterId", entry.CharacterId);
            command.Parameters.AddWithValue("$killCount", entry.KillCount);
            command.Parameters.AddWithValue("$lossCount", entry.LossCount);
            command.Parameters.AddWithValue("$avgAttackersWhenAttacking", entry.AvgAttackersWhenAttacking);
            command.Parameters.AddWithValue("$lastPublicCynoCapableHull", entry.LastPublicCynoCapableHull ?? "");
            command.Parameters.AddWithValue("$lastShipSeenName", entry.LastShipSeenName ?? "");
            command.Parameters.AddWithValue("$lastShipSeenAtUtc", entry.LastShipSeenAtUtc ?? "");
            command.Parameters.AddWithValue("$refreshedAtUtc", entry.RefreshedAtUtc ?? "");
            command.Parameters.AddWithValue("$expiresAtUtc", entry.ExpiresAtUtc ?? "");

            command.ExecuteNonQuery();
        }

        private static void EnsureTableExists(SqliteConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                @"
                CREATE TABLE IF NOT EXISTS stats_cache (
                    character_id TEXT PRIMARY KEY,
                    kill_count INTEGER NOT NULL DEFAULT 0,
                    loss_count INTEGER NOT NULL DEFAULT 0,
                    avg_attackers_when_attacking REAL NOT NULL DEFAULT 0,
                    last_public_cyno_capable_hull TEXT NOT NULL DEFAULT '',
                    last_ship_seen_name TEXT NOT NULL DEFAULT '',
                    last_ship_seen_at_utc TEXT NOT NULL DEFAULT '',
                    refreshed_at_utc TEXT NOT NULL DEFAULT '',
                    expires_at_utc TEXT NOT NULL DEFAULT ''
                );
                ";
                command.ExecuteNonQuery();
            }

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