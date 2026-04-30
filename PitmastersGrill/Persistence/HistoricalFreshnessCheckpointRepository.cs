using Microsoft.Data.Sqlite;
using System;
using System.Globalization;

namespace PitmastersGrill.Persistence
{
    public sealed class HistoricalFreshnessCheckpointRepository
    {
        private readonly string _databasePath;

        public HistoricalFreshnessCheckpointRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        public HistoricalFreshnessCheckpointRecord? Get(long characterId, string windowStartDayUtc, string windowEndDayUtc)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            SELECT
                character_id,
                window_start_day_utc,
                window_end_day_utc,
                last_checked_at_utc,
                last_status,
                last_imported_count,
                last_known_count,
                last_failed_count,
                last_error
            FROM historical_freshness_checkpoint
            WHERE character_id = $characterId
              AND window_start_day_utc = $windowStartDayUtc
              AND window_end_day_utc = $windowEndDayUtc
            LIMIT 1;
            ";
            command.Parameters.AddWithValue("$characterId", characterId);
            command.Parameters.AddWithValue("$windowStartDayUtc", windowStartDayUtc ?? "");
            command.Parameters.AddWithValue("$windowEndDayUtc", windowEndDayUtc ?? "");

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            return new HistoricalFreshnessCheckpointRecord
            {
                CharacterId = reader.IsDBNull(0) ? 0 : reader.GetInt64(0),
                WindowStartDayUtc = reader.IsDBNull(1) ? "" : reader.GetString(1),
                WindowEndDayUtc = reader.IsDBNull(2) ? "" : reader.GetString(2),
                LastCheckedAtUtc = reader.IsDBNull(3) ? "" : reader.GetString(3),
                LastStatus = reader.IsDBNull(4) ? "" : reader.GetString(4),
                LastImportedCount = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                LastKnownCount = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                LastFailedCount = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                LastError = reader.IsDBNull(8) ? "" : reader.GetString(8)
            };
        }

        public void Upsert(HistoricalFreshnessCheckpointRecord record)
        {
            if (record == null || record.CharacterId <= 0)
            {
                return;
            }

            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            INSERT INTO historical_freshness_checkpoint (
                character_id,
                window_start_day_utc,
                window_end_day_utc,
                last_checked_at_utc,
                last_status,
                last_imported_count,
                last_known_count,
                last_failed_count,
                last_error
            )
            VALUES (
                $characterId,
                $windowStartDayUtc,
                $windowEndDayUtc,
                $lastCheckedAtUtc,
                $lastStatus,
                $lastImportedCount,
                $lastKnownCount,
                $lastFailedCount,
                $lastError
            )
            ON CONFLICT(character_id, window_start_day_utc, window_end_day_utc) DO UPDATE SET
                last_checked_at_utc = excluded.last_checked_at_utc,
                last_status = excluded.last_status,
                last_imported_count = excluded.last_imported_count,
                last_known_count = excluded.last_known_count,
                last_failed_count = excluded.last_failed_count,
                last_error = excluded.last_error;
            ";
            command.Parameters.AddWithValue("$characterId", record.CharacterId);
            command.Parameters.AddWithValue("$windowStartDayUtc", record.WindowStartDayUtc ?? "");
            command.Parameters.AddWithValue("$windowEndDayUtc", record.WindowEndDayUtc ?? "");
            command.Parameters.AddWithValue("$lastCheckedAtUtc", record.LastCheckedAtUtc ?? "");
            command.Parameters.AddWithValue("$lastStatus", record.LastStatus ?? "");
            command.Parameters.AddWithValue("$lastImportedCount", record.LastImportedCount);
            command.Parameters.AddWithValue("$lastKnownCount", record.LastKnownCount);
            command.Parameters.AddWithValue("$lastFailedCount", record.LastFailedCount);
            command.Parameters.AddWithValue("$lastError", record.LastError ?? "");
            command.ExecuteNonQuery();
        }

        public static bool WasCheckedWithinCooldown(
            HistoricalFreshnessCheckpointRecord? record,
            DateTime utcNow,
            int cooldownHours)
        {
            if (record == null || cooldownHours <= 0)
            {
                return false;
            }

            if (!DateTime.TryParse(
                    record.LastCheckedAtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out var lastCheckedUtc))
            {
                return false;
            }

            return utcNow - DateTime.SpecifyKind(lastCheckedUtc, DateTimeKind.Utc) < TimeSpan.FromHours(cooldownHours);
        }

        private SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();
            return connection;
        }
    }

    public sealed class HistoricalFreshnessCheckpointRecord
    {
        public long CharacterId { get; set; }
        public string WindowStartDayUtc { get; set; } = "";
        public string WindowEndDayUtc { get; set; } = "";
        public string LastCheckedAtUtc { get; set; } = "";
        public string LastStatus { get; set; } = "";
        public int LastImportedCount { get; set; }
        public int LastKnownCount { get; set; }
        public int LastFailedCount { get; set; }
        public string LastError { get; set; } = "";
    }
}
