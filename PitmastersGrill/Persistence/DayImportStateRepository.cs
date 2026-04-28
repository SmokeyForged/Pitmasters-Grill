using Microsoft.Data.Sqlite;
using PitmastersGrill.Models;
using System;
using System.Collections.Generic;

namespace PitmastersGrill.Persistence
{
    public class DayImportStateRepository
    {
        private readonly string _databasePath;

        public DayImportStateRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        public DayImportState? GetByDay(string dayUtc)
        {
            if (string.IsNullOrWhiteSpace(dayUtc))
            {
                return null;
            }

            var connectionString = $"Data Source={_databasePath}";

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            SELECT
                day_utc,
                remote_total_count,
                local_imported_count,
                state,
                archive_etag,
                archive_last_modified,
                checked_at_utc,
                downloaded_at_utc,
                imported_at_utc,
                normalized_at_utc,
                completed_at_utc,
                last_error
            FROM day_import_state
            WHERE day_utc = $dayUtc
            LIMIT 1;
            ";
            command.Parameters.AddWithValue("$dayUtc", dayUtc);

            using var reader = command.ExecuteReader();

            if (!reader.Read())
            {
                return null;
            }

            return Map(reader);
        }

        public void Upsert(DayImportState state)
        {
            var connectionString = $"Data Source={_databasePath}";

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            INSERT INTO day_import_state (
                day_utc,
                remote_total_count,
                local_imported_count,
                state,
                archive_etag,
                archive_last_modified,
                checked_at_utc,
                downloaded_at_utc,
                imported_at_utc,
                normalized_at_utc,
                completed_at_utc,
                last_error
            )
            VALUES (
                $dayUtc,
                $remoteTotalCount,
                $localImportedCount,
                $state,
                $archiveEtag,
                $archiveLastModified,
                $checkedAtUtc,
                $downloadedAtUtc,
                $importedAtUtc,
                $normalizedAtUtc,
                $completedAtUtc,
                $lastError
            )
            ON CONFLICT(day_utc) DO UPDATE SET
                remote_total_count = excluded.remote_total_count,
                local_imported_count = excluded.local_imported_count,
                state = excluded.state,
                archive_etag = excluded.archive_etag,
                archive_last_modified = excluded.archive_last_modified,
                checked_at_utc = excluded.checked_at_utc,
                downloaded_at_utc = excluded.downloaded_at_utc,
                imported_at_utc = excluded.imported_at_utc,
                normalized_at_utc = excluded.normalized_at_utc,
                completed_at_utc = excluded.completed_at_utc,
                last_error = excluded.last_error;
            ";

            command.Parameters.AddWithValue("$dayUtc", state.DayUtc);
            command.Parameters.AddWithValue("$remoteTotalCount", state.RemoteTotalCount);
            command.Parameters.AddWithValue("$localImportedCount", state.LocalImportedCount);
            command.Parameters.AddWithValue("$state", state.State);
            command.Parameters.AddWithValue("$archiveEtag", state.ArchiveEtag);
            command.Parameters.AddWithValue("$archiveLastModified", state.ArchiveLastModified);
            command.Parameters.AddWithValue("$checkedAtUtc", state.CheckedAtUtc);
            command.Parameters.AddWithValue("$downloadedAtUtc", state.DownloadedAtUtc);
            command.Parameters.AddWithValue("$importedAtUtc", state.ImportedAtUtc);
            command.Parameters.AddWithValue("$normalizedAtUtc", state.NormalizedAtUtc);
            command.Parameters.AddWithValue("$completedAtUtc", state.CompletedAtUtc);
            command.Parameters.AddWithValue("$lastError", state.LastError);

            command.ExecuteNonQuery();
        }

        public string GetLatestCompleteDayUtc()
        {
            var connectionString = $"Data Source={_databasePath}";

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            SELECT COALESCE(MAX(day_utc), '')
            FROM day_import_state
            WHERE state = 'complete';
            ";

            var result = command.ExecuteScalar();
            return result?.ToString() ?? "";
        }

        public string GetEarliestCompleteDayUtc()
        {
            var connectionString = $"Data Source={_databasePath}";

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            SELECT COALESCE(MIN(day_utc), '')
            FROM day_import_state
            WHERE state = 'complete';
            ";

            var result = command.ExecuteScalar();
            return result?.ToString() ?? "";
        }


        public List<string> GetCompleteDays(int maxDays = 0)
        {
            var results = new List<string>();
            var connectionString = $"Data Source={_databasePath}";

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = maxDays > 0
                ? @"
            SELECT day_utc
            FROM day_import_state
            WHERE state = 'complete'
            ORDER BY day_utc DESC
            LIMIT $maxDays;
            "
                : @"
            SELECT day_utc
            FROM day_import_state
            WHERE state = 'complete'
            ORDER BY day_utc DESC;
            ";

            if (maxDays > 0)
            {
                command.Parameters.AddWithValue("$maxDays", maxDays);
            }

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var day = reader.IsDBNull(0) ? "" : reader.GetString(0);
                if (!string.IsNullOrWhiteSpace(day))
                {
                    results.Add(day);
                }
            }

            return results;
        }

        public List<DayImportState> GetIncompleteDays()
        {
            var results = new List<DayImportState>();
            var connectionString = $"Data Source={_databasePath}";

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            SELECT
                day_utc,
                remote_total_count,
                local_imported_count,
                state,
                archive_etag,
                archive_last_modified,
                checked_at_utc,
                downloaded_at_utc,
                imported_at_utc,
                normalized_at_utc,
                completed_at_utc,
                last_error
            FROM day_import_state
            WHERE state <> 'complete'
            ORDER BY day_utc ASC;
            ";

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                results.Add(Map(reader));
            }

            return results;
        }

        private static DayImportState Map(SqliteDataReader reader)
        {
            return new DayImportState
            {
                DayUtc = reader.GetString(0),
                RemoteTotalCount = reader.GetInt32(1),
                LocalImportedCount = reader.GetInt32(2),
                State = reader.GetString(3),
                ArchiveEtag = reader.GetString(4),
                ArchiveLastModified = reader.GetString(5),
                CheckedAtUtc = reader.GetString(6),
                DownloadedAtUtc = reader.GetString(7),
                ImportedAtUtc = reader.GetString(8),
                NormalizedAtUtc = reader.GetString(9),
                CompletedAtUtc = reader.GetString(10),
                LastError = reader.GetString(11)
            };
        }
    }
}
