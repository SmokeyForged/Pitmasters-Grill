using Microsoft.Data.Sqlite;
using PitmastersGrill.Models;
using System;
using System.Globalization;

namespace PitmastersGrill.Persistence
{
    internal sealed class R2Z2FeedStateRepository
    {
        private const string FeedName = "r2z2";
        private readonly string _databasePath;

        public R2Z2FeedStateRepository(string databasePath)
        {
            _databasePath = string.IsNullOrWhiteSpace(databasePath)
                ? throw new ArgumentException("Database path is required.", nameof(databasePath))
                : databasePath;
        }

        public R2Z2FeedState? Load()
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();
            return Load(connection, transaction: null);
        }

        public R2Z2FeedState LoadOrDefault()
        {
            return Load() ?? CreateDefaultState();
        }

        public void Update(Action<R2Z2FeedState> update)
        {
            if (update == null)
            {
                throw new ArgumentNullException(nameof(update));
            }

            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var transaction = connection.BeginTransaction();
            var state = Load(connection, transaction) ?? CreateDefaultState();
            update(state);
            Upsert(connection, transaction, state);
            transaction.Commit();
        }

        public R2Z2LiveFeedSnapshot ReadSnapshot()
        {
            var state = LoadOrDefault();
            return new R2Z2LiveFeedSnapshot
            {
                Source = "R2Z2",
                Enabled = state.Enabled != 0,
                Status = string.IsNullOrWhiteSpace(state.Status) ? "Disabled" : state.Status,
                NextSequenceId = state.NextSequenceId,
                LastProcessedSequenceId = state.LastProcessedSequenceId,
                LastSuccessAtUtc = state.LastSuccessAtUtc,
                LastCaughtUpAtUtc = state.Last404AtUtc,
                LastErrorAtUtc = state.LastErrorAtUtc,
                LastError = state.LastError,
                RecentLiveImportsCount = CountSeenRows()
            };
        }

        private static R2Z2FeedState? Load(
            SqliteConnection connection,
            SqliteTransaction? transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            SELECT
                feed_name,
                enabled,
                next_sequence_id,
                last_processed_sequence_id,
                last_success_at_utc,
                last_404_at_utc,
                last_error_at_utc,
                last_error,
                status,
                updated_at_utc
            FROM live_killmail_feed_state
            WHERE feed_name = $feedName
            LIMIT 1;
            ";
            command.Parameters.AddWithValue("$feedName", FeedName);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            return new R2Z2FeedState
            {
                FeedName = reader.IsDBNull(0) ? FeedName : reader.GetString(0),
                Enabled = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                NextSequenceId = reader.IsDBNull(2) ? null : reader.GetInt64(2),
                LastProcessedSequenceId = reader.IsDBNull(3) ? null : reader.GetInt64(3),
                LastSuccessAtUtc = reader.IsDBNull(4) ? "" : reader.GetString(4),
                Last404AtUtc = reader.IsDBNull(5) ? "" : reader.GetString(5),
                LastErrorAtUtc = reader.IsDBNull(6) ? "" : reader.GetString(6),
                LastError = reader.IsDBNull(7) ? "" : reader.GetString(7),
                Status = reader.IsDBNull(8) ? "Disabled" : reader.GetString(8),
                UpdatedAtUtc = reader.IsDBNull(9) ? "" : reader.GetString(9)
            };
        }

        private static void Upsert(
            SqliteConnection connection,
            SqliteTransaction transaction,
            R2Z2FeedState state)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            INSERT INTO live_killmail_feed_state (
                feed_name,
                enabled,
                next_sequence_id,
                last_processed_sequence_id,
                last_success_at_utc,
                last_404_at_utc,
                last_error_at_utc,
                last_error,
                status,
                updated_at_utc
            )
            VALUES (
                $feedName,
                $enabled,
                $nextSequenceId,
                $lastProcessedSequenceId,
                $lastSuccessAtUtc,
                $last404AtUtc,
                $lastErrorAtUtc,
                $lastError,
                $status,
                $updatedAtUtc
            )
            ON CONFLICT(feed_name) DO UPDATE SET
                enabled = excluded.enabled,
                next_sequence_id = excluded.next_sequence_id,
                last_processed_sequence_id = excluded.last_processed_sequence_id,
                last_success_at_utc = excluded.last_success_at_utc,
                last_404_at_utc = excluded.last_404_at_utc,
                last_error_at_utc = excluded.last_error_at_utc,
                last_error = excluded.last_error,
                status = excluded.status,
                updated_at_utc = excluded.updated_at_utc;
            ";
            command.Parameters.AddWithValue("$feedName", state.FeedName);
            command.Parameters.AddWithValue("$enabled", state.Enabled);
            command.Parameters.AddWithValue("$nextSequenceId", (object?)state.NextSequenceId ?? DBNull.Value);
            command.Parameters.AddWithValue("$lastProcessedSequenceId", (object?)state.LastProcessedSequenceId ?? DBNull.Value);
            command.Parameters.AddWithValue("$lastSuccessAtUtc", state.LastSuccessAtUtc ?? "");
            command.Parameters.AddWithValue("$last404AtUtc", state.Last404AtUtc ?? "");
            command.Parameters.AddWithValue("$lastErrorAtUtc", state.LastErrorAtUtc ?? "");
            command.Parameters.AddWithValue("$lastError", state.LastError ?? "");
            command.Parameters.AddWithValue("$status", state.Status ?? "Disabled");
            command.Parameters.AddWithValue("$updatedAtUtc", state.UpdatedAtUtc ?? "");
            command.ExecuteNonQuery();
        }

        private int CountSeenRows()
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM live_killmail_seen;";
            var scalar = command.ExecuteScalar();
            return scalar == null || scalar == DBNull.Value
                ? 0
                : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
        }

        private static R2Z2FeedState CreateDefaultState()
        {
            return new R2Z2FeedState
            {
                FeedName = FeedName,
                Enabled = 0,
                Status = "Disabled",
                UpdatedAtUtc = DateTime.UtcNow.ToString("o")
            };
        }
    }

    internal sealed class R2Z2FeedState
    {
        public string FeedName { get; set; } = "r2z2";
        public int Enabled { get; set; }
        public long? NextSequenceId { get; set; }
        public long? LastProcessedSequenceId { get; set; }
        public string LastSuccessAtUtc { get; set; } = "";
        public string Last404AtUtc { get; set; } = "";
        public string LastErrorAtUtc { get; set; } = "";
        public string LastError { get; set; } = "";
        public string Status { get; set; } = "Disabled";
        public string UpdatedAtUtc { get; set; } = "";
    }
}
