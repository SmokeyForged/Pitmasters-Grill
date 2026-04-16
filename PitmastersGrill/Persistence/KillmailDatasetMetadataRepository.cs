using Microsoft.Data.Sqlite;
using System;

namespace PitmastersGrill.Persistence
{
    public class KillmailDatasetMetadataRepository
    {
        private readonly string _databasePath;

        public KillmailDatasetMetadataRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        public string GetValue(string key)
        {
            var connectionString = $"Data Source={_databasePath}";

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            SELECT value
            FROM dataset_metadata
            WHERE key = $key
            LIMIT 1;
            ";
            command.Parameters.AddWithValue("$key", key);

            var result = command.ExecuteScalar();
            return result?.ToString() ?? "";
        }

        public void SetValue(string key, string value)
        {
            var connectionString = $"Data Source={_databasePath}";

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

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

        public void SetUtcNow(string key)
        {
            SetValue(key, DateTime.UtcNow.ToString("o"));
        }
    }
}