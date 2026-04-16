using Microsoft.Data.Sqlite;
using System;

namespace PitmastersLittleGrill.Persistence
{
    public class ShipTypeNameCacheRepository
    {
        private readonly string _databasePath;

        public ShipTypeNameCacheRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        public (string? TypeName, bool? IsActualShip) GetTypeInfo(int typeId)
        {
            if (typeId <= 0)
            {
                return (null, null);
            }

            var connectionString = $"Data Source={_databasePath}";

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            EnsureTableExists(connection);

            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            SELECT type_name, is_actual_ship
            FROM ship_type_name_cache
            WHERE type_id = $typeId
            LIMIT 1;
            ";
            command.Parameters.AddWithValue("$typeId", typeId);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return (null, null);
            }

            var typeName = reader.IsDBNull(0) ? null : reader.GetString(0);
            bool? isActualShip = null;

            if (!reader.IsDBNull(1))
            {
                isActualShip = reader.GetInt64(1) != 0;
            }

            return (typeName, isActualShip);
        }

        public string? GetTypeName(int typeId)
        {
            return GetTypeInfo(typeId).TypeName;
        }

        public void Upsert(int typeId, string typeName)
        {
            Upsert(typeId, typeName, null);
        }

        public void Upsert(int typeId, string typeName, bool? isActualShip)
        {
            if (typeId <= 0 || string.IsNullOrWhiteSpace(typeName))
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
            INSERT INTO ship_type_name_cache (
                type_id,
                type_name,
                is_actual_ship,
                refreshed_at_utc
            )
            VALUES (
                $typeId,
                $typeName,
                $isActualShip,
                $refreshedAtUtc
            )
            ON CONFLICT(type_id) DO UPDATE SET
                type_name = excluded.type_name,
                is_actual_ship = excluded.is_actual_ship,
                refreshed_at_utc = excluded.refreshed_at_utc;
            ";

            command.Parameters.AddWithValue("$typeId", typeId);
            command.Parameters.AddWithValue("$typeName", typeName);
            command.Parameters.AddWithValue("$isActualShip", (object?)ToDbBool(isActualShip) ?? DBNull.Value);
            command.Parameters.AddWithValue("$refreshedAtUtc", DateTime.UtcNow.ToString("o"));
            command.ExecuteNonQuery();
        }

        private static long? ToDbBool(bool? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            return value.Value ? 1L : 0L;
        }

        private static void EnsureTableExists(SqliteConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                @"
                CREATE TABLE IF NOT EXISTS ship_type_name_cache (
                    type_id INTEGER PRIMARY KEY,
                    type_name TEXT NOT NULL DEFAULT '',
                    is_actual_ship INTEGER NULL,
                    refreshed_at_utc TEXT NOT NULL DEFAULT ''
                );
                ";
                command.ExecuteNonQuery();
            }

            EnsureColumnExists(
                connection,
                "ship_type_name_cache",
                "is_actual_ship",
                "INTEGER NULL");
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
            alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
            alterCommand.ExecuteNonQuery();
        }
    }
}