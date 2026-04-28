using Microsoft.Data.Sqlite;
using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PitmastersGrill.Services
{
    public sealed class CacheMaintenanceService
    {
        private static DateTime? _lastMaintenanceUtc;

        public CacheStatsSnapshot GetStats()
        {
            var databasePath = AppPaths.GetDatabasePath();
            var snapshot = new CacheStatsSnapshot
            {
                DatabasePathDisplay = RedactPath(databasePath),
                DatabaseSizeBytes = File.Exists(databasePath) ? new FileInfo(databasePath).Length : 0,
                LastMaintenanceUtc = _lastMaintenanceUtc,
                Status = "Cache stats loaded."
            };

            if (!File.Exists(databasePath))
            {
                snapshot.Status = "Database does not exist yet.";
                return snapshot;
            }

            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();

            foreach (var table in GetUserTables(connection))
            {
                snapshot.TableCounts[table] = CountRows(connection, table);
            }

            snapshot.ExpiredCounts["resolver_cache"] = CountExpired(connection, "resolver_cache", "expires_at_utc");
            snapshot.ExpiredCounts["stats_cache"] = CountExpired(connection, "stats_cache", "expires_at_utc");
            snapshot.OldestCachedRecordUtc = GetBoundaryDate(connection, min: true);
            snapshot.NewestCachedRecordUtc = GetBoundaryDate(connection, min: false);
            return snapshot;
        }

        public int ClearExpired()
        {
            var databasePath = AppPaths.GetDatabasePath();
            if (!File.Exists(databasePath))
            {
                return 0;
            }

            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();

            var removed = DeleteExpired(connection, "resolver_cache", "expires_at_utc");
            removed += DeleteExpired(connection, "stats_cache", "expires_at_utc");
            _lastMaintenanceUtc = DateTime.UtcNow;
            AppLogger.DatabaseInfo($"Expired cache cleanup complete. removedRows={removed}");
            return removed;
        }

        public int ClearAll()
        {
            var databasePath = AppPaths.GetDatabasePath();
            if (!File.Exists(databasePath))
            {
                return 0;
            }

            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();

            var removed = 0;
            foreach (var table in new[] { "resolver_cache", "stats_cache" })
            {
                if (!TableExists(connection, table))
                {
                    continue;
                }

                using var command = connection.CreateCommand();
                command.CommandText = $"DELETE FROM {table};";
                removed += command.ExecuteNonQuery();
            }

            _lastMaintenanceUtc = DateTime.UtcNow;
            AppLogger.DatabaseWarn($"All resolver/stat cache rows cleared. removedRows={removed}");
            return removed;
        }

        public void Vacuum()
        {
            var databasePath = AppPaths.GetDatabasePath();
            if (!File.Exists(databasePath))
            {
                return;
            }

            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "VACUUM;";
            command.ExecuteNonQuery();
            _lastMaintenanceUtc = DateTime.UtcNow;
            AppLogger.DatabaseInfo("SQLite VACUUM completed.");
        }

        public static string FormatStats(CacheStatsSnapshot stats)
        {
            var lines = new List<string>
            {
                $"Database: {stats.DatabasePathDisplay}",
                $"Size: {stats.DatabaseSizeBytes:N0} bytes",
                $"Status: {stats.Status}",
                $"Last maintenance UTC: {stats.LastMaintenanceUtc?.ToString("O") ?? "Never"}",
                $"Oldest cached UTC: {stats.OldestCachedRecordUtc?.ToString("O") ?? "Unknown"}",
                $"Newest cached UTC: {stats.NewestCachedRecordUtc?.ToString("O") ?? "Unknown"}",
                "Table counts:"
            };

            lines.AddRange(stats.TableCounts.Select(x => $"  {x.Key}: {x.Value:N0}"));
            lines.Add("Expired counts:");
            lines.AddRange(stats.ExpiredCounts.Select(x => $"  {x.Key}: {x.Value:N0}"));
            return string.Join(Environment.NewLine, lines);
        }

        private static List<string> GetUserTables(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
            using var reader = command.ExecuteReader();
            var tables = new List<string>();
            while (reader.Read())
            {
                tables.Add(reader.GetString(0));
            }

            return tables;
        }

        private static bool TableExists(SqliteConnection connection, string table)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";
            command.Parameters.AddWithValue("$name", table);
            return command.ExecuteScalar() != null;
        }

        private static long CountRows(SqliteConnection connection, string table)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {table};";
            return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        private static long CountExpired(SqliteConnection connection, string table, string column)
        {
            if (!TableExists(connection, table))
            {
                return 0;
            }

            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {table} WHERE {column} <> '' AND {column} < $now;";
            command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
            return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        private static int DeleteExpired(SqliteConnection connection, string table, string column)
        {
            if (!TableExists(connection, table))
            {
                return 0;
            }

            using var command = connection.CreateCommand();
            command.CommandText = $"DELETE FROM {table} WHERE {column} <> '' AND {column} < $now;";
            command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
            return command.ExecuteNonQuery();
        }

        private static DateTime? GetBoundaryDate(SqliteConnection connection, bool min)
        {
            var candidates = new List<DateTime>();
            foreach (var tableAndColumn in new[] { ("resolver_cache", "resolved_at_utc"), ("stats_cache", "refreshed_at_utc") })
            {
                if (!TableExists(connection, tableAndColumn.Item1))
                {
                    continue;
                }

                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT {(min ? "MIN" : "MAX")}({tableAndColumn.Item2}) FROM {tableAndColumn.Item1} WHERE {tableAndColumn.Item2} <> '';";
                var value = command.ExecuteScalar()?.ToString();
                if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
                {
                    candidates.Add(parsed);
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            return min ? candidates.Min() : candidates.Max();
        }

        private static string RedactPath(string path)
        {
            return KillmailPaths.CollapsePathTokens(path);
        }
    }
}
