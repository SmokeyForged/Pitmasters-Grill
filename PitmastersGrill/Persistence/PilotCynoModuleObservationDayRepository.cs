using Microsoft.Data.Sqlite;
using PitmastersGrill.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PitmastersGrill.Persistence
{
    public sealed class PilotCynoModuleObservationDayRepository
    {
        private readonly string _databasePath;

        public PilotCynoModuleObservationDayRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        public string DatabasePath => _databasePath;

        public void ReplaceDay(string dayUtc, IReadOnlyList<PilotCynoModuleObservationDayRecord> records)
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var transaction = connection.BeginTransaction();

            using (var deleteCommand = connection.CreateCommand())
            {
                deleteCommand.Transaction = transaction;
                deleteCommand.CommandText = "DELETE FROM pilot_cyno_module_observations_day WHERE day_utc = $dayUtc;";
                deleteCommand.Parameters.AddWithValue("$dayUtc", dayUtc);
                deleteCommand.ExecuteNonQuery();
            }

            using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText =
            @"
            INSERT INTO pilot_cyno_module_observations_day (
                day_utc,
                character_id,
                killmail_id,
                killmail_time_utc,
                victim_ship_type_id,
                module_type_id,
                module_name,
                quantity_destroyed,
                quantity_dropped,
                item_state,
                source,
                updated_at_utc
            )
            VALUES (
                $dayUtc,
                $characterId,
                $killmailId,
                $killmailTimeUtc,
                $victimShipTypeId,
                $moduleTypeId,
                $moduleName,
                $quantityDestroyed,
                $quantityDropped,
                $itemState,
                $source,
                $updatedAtUtc
            )
            ON CONFLICT(day_utc, character_id, killmail_id, module_type_id)
            DO UPDATE SET
                killmail_time_utc = excluded.killmail_time_utc,
                victim_ship_type_id = excluded.victim_ship_type_id,
                module_name = excluded.module_name,
                quantity_destroyed = excluded.quantity_destroyed,
                quantity_dropped = excluded.quantity_dropped,
                item_state = excluded.item_state,
                source = excluded.source,
                updated_at_utc = excluded.updated_at_utc;
            ";

            var dayParam = insertCommand.CreateParameter();
            dayParam.ParameterName = "$dayUtc";
            insertCommand.Parameters.Add(dayParam);

            var characterParam = insertCommand.CreateParameter();
            characterParam.ParameterName = "$characterId";
            insertCommand.Parameters.Add(characterParam);

            var killmailParam = insertCommand.CreateParameter();
            killmailParam.ParameterName = "$killmailId";
            insertCommand.Parameters.Add(killmailParam);

            var timeParam = insertCommand.CreateParameter();
            timeParam.ParameterName = "$killmailTimeUtc";
            insertCommand.Parameters.Add(timeParam);

            var victimShipParam = insertCommand.CreateParameter();
            victimShipParam.ParameterName = "$victimShipTypeId";
            insertCommand.Parameters.Add(victimShipParam);

            var moduleTypeParam = insertCommand.CreateParameter();
            moduleTypeParam.ParameterName = "$moduleTypeId";
            insertCommand.Parameters.Add(moduleTypeParam);

            var moduleNameParam = insertCommand.CreateParameter();
            moduleNameParam.ParameterName = "$moduleName";
            insertCommand.Parameters.Add(moduleNameParam);

            var quantityDestroyedParam = insertCommand.CreateParameter();
            quantityDestroyedParam.ParameterName = "$quantityDestroyed";
            insertCommand.Parameters.Add(quantityDestroyedParam);

            var quantityDroppedParam = insertCommand.CreateParameter();
            quantityDroppedParam.ParameterName = "$quantityDropped";
            insertCommand.Parameters.Add(quantityDroppedParam);

            var itemStateParam = insertCommand.CreateParameter();
            itemStateParam.ParameterName = "$itemState";
            insertCommand.Parameters.Add(itemStateParam);

            var sourceParam = insertCommand.CreateParameter();
            sourceParam.ParameterName = "$source";
            insertCommand.Parameters.Add(sourceParam);

            var updatedParam = insertCommand.CreateParameter();
            updatedParam.ParameterName = "$updatedAtUtc";
            insertCommand.Parameters.Add(updatedParam);

            insertCommand.Prepare();

            foreach (var record in records)
            {
                dayParam.Value = record.DayUtc;
                characterParam.Value = NormalizeCharacterId(record.CharacterId);
                killmailParam.Value = record.KillmailId;
                timeParam.Value = record.KillmailTimeUtc;
                victimShipParam.Value = (object?)record.VictimShipTypeId ?? DBNull.Value;
                moduleTypeParam.Value = record.ModuleTypeId;
                moduleNameParam.Value = record.ModuleName;
                quantityDestroyedParam.Value = record.QuantityDestroyed;
                quantityDroppedParam.Value = record.QuantityDropped;
                itemStateParam.Value = record.ItemState;
                sourceParam.Value = record.Source;
                updatedParam.Value = record.UpdatedAtUtc;

                insertCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        public int ClearAll()
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM pilot_cyno_module_observations_day;";
            return command.ExecuteNonQuery();
        }

        public int CountAll()
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM pilot_cyno_module_observations_day;";
            return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        public int CountByCharacterId(string characterId)
        {
            var normalizedCharacterId = NormalizeCharacterId(characterId);
            if (string.IsNullOrWhiteSpace(normalizedCharacterId))
            {
                return 0;
            }

            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM pilot_cyno_module_observations_day WHERE character_id = $characterId;";
            command.Parameters.AddWithValue("$characterId", normalizedCharacterId);
            return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        public List<CynoModuleEvidence> GetRecentCynoModuleEvidenceByCharacterId(string characterId, int maxResults = 10)
        {
            var normalizedCharacterId = NormalizeCharacterId(characterId);
            if (string.IsNullOrWhiteSpace(normalizedCharacterId))
            {
                return new List<CynoModuleEvidence>();
            }

            return QueryRecent(
                "WHERE character_id = $characterId",
                command => command.Parameters.AddWithValue("$characterId", normalizedCharacterId),
                maxResults);
        }

        public List<CynoModuleEvidence> GetRecentCynoModuleEvidenceNearKillmailTimeUtc(string killmailTimeUtc, int maxResults = 10)
        {
            var targetTime = TryParseUtc(killmailTimeUtc);
            if (!targetTime.HasValue)
            {
                return new List<CynoModuleEvidence>();
            }

            // Keep the initial SQL broad and do precise time comparison in C# because archived
            // killmail timestamps may contain different ISO fractions/offset spellings.
            var dayPrefix = targetTime.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var candidates = QueryRecent(
                "WHERE killmail_time_utc LIKE $dayPrefix",
                command => command.Parameters.AddWithValue("$dayPrefix", dayPrefix + "%"),
                5000);

            var groupedByKillmail = candidates
                .Where(x => x.KillmailTimeUtc.HasValue)
                .Where(x => Math.Abs((x.KillmailTimeUtc!.Value - targetTime.Value).TotalMinutes) <= 5)
                .GroupBy(x => string.IsNullOrWhiteSpace(x.KillmailId)
                    ? x.KillmailTimeUtc!.Value.ToString("o", CultureInfo.InvariantCulture)
                    : x.KillmailId,
                    StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(x => x.Max(y => y.KillmailTimeUtc ?? DateTime.MinValue))
                .ToList();

            if (groupedByKillmail.Count != 1)
            {
                return new List<CynoModuleEvidence>();
            }

            return groupedByKillmail[0]
                .Take(Math.Max(1, maxResults))
                .Select(x =>
                {
                    x.Source = string.IsNullOrWhiteSpace(x.Source)
                        ? "public loss victim item list (matched by selected row loss time)"
                        : x.Source + " (matched by selected row loss time)";
                    return x;
                })
                .ToList();
        }

        private List<CynoModuleEvidence> QueryRecent(
            string whereClause,
            Action<SqliteCommand> bindParameters,
            int maxResults)
        {
            var results = new List<CynoModuleEvidence>();
            var limit = maxResults <= 0 ? 10 : maxResults;

            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
            $@"
            SELECT
                character_id,
                killmail_id,
                killmail_time_utc,
                victim_ship_type_id,
                module_type_id,
                module_name,
                quantity_destroyed,
                quantity_dropped,
                item_state,
                source
            FROM pilot_cyno_module_observations_day
            {whereClause}
            ORDER BY killmail_time_utc DESC
            LIMIT $maxResults;
            ";
            bindParameters(command);
            command.Parameters.AddWithValue("$maxResults", limit);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new CynoModuleEvidence
                {
                    CharacterId = reader.IsDBNull(0) ? "" : reader.GetString(0),
                    KillmailId = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    KillmailTimeUtc = TryParseUtc(reader.IsDBNull(2) ? "" : reader.GetString(2)),
                    ShipTypeId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                    TypeId = reader.GetInt32(4),
                    TypeName = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    QuantityDestroyed = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                    QuantityDropped = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                    ItemState = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    Source = reader.IsDBNull(9) ? "" : reader.GetString(9)
                });
            }

            return results;
        }

        private static string NormalizeCharacterId(string characterId)
        {
            return string.IsNullOrWhiteSpace(characterId)
                ? string.Empty
                : characterId.Trim();
        }

        private static DateTime? TryParseUtc(string value)
        {
            if (DateTime.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsed))
            {
                return parsed;
            }

            return null;
        }
    }
}
