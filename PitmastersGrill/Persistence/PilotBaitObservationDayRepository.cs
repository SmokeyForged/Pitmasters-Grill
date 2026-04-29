using Microsoft.Data.Sqlite;
using PitmastersGrill.Models;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PitmastersGrill.Persistence
{
    public sealed class PilotBaitObservationDayRepository
    {
        private readonly string _databasePath;

        public PilotBaitObservationDayRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        public void ReplaceDay(string dayUtc, IReadOnlyList<PilotBaitObservationDayRecord> records)
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var transaction = connection.BeginTransaction();

            using (var deleteCommand = connection.CreateCommand())
            {
                deleteCommand.Transaction = transaction;
                deleteCommand.CommandText = "DELETE FROM pilot_bait_observations_day WHERE day_utc = $dayUtc;";
                deleteCommand.Parameters.AddWithValue("$dayUtc", dayUtc);
                deleteCommand.ExecuteNonQuery();
            }

            using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText =
            @"
            INSERT INTO pilot_bait_observations_day (
                day_utc,
                character_id,
                killmail_id,
                killmail_time_utc,
                victim_ship_type_id,
                victim_ship_name,
                solar_system_id,
                solar_system_name,
                industrial_cyno_module_type_id,
                industrial_cyno_module_name,
                tackle_module_type_id,
                tackle_module_name,
                tackle_type,
                quantity_destroyed,
                quantity_dropped,
                source,
                updated_at_utc
            )
            VALUES (
                $dayUtc,
                $characterId,
                $killmailId,
                $killmailTimeUtc,
                $victimShipTypeId,
                $victimShipName,
                $solarSystemId,
                $solarSystemName,
                $industrialCynoModuleTypeId,
                $industrialCynoModuleName,
                $tackleModuleTypeId,
                $tackleModuleName,
                $tackleType,
                $quantityDestroyed,
                $quantityDropped,
                $source,
                $updatedAtUtc
            )
            ON CONFLICT(day_utc, character_id, killmail_id, tackle_module_type_id)
            DO UPDATE SET
                killmail_time_utc = excluded.killmail_time_utc,
                victim_ship_type_id = excluded.victim_ship_type_id,
                victim_ship_name = excluded.victim_ship_name,
                solar_system_id = excluded.solar_system_id,
                solar_system_name = excluded.solar_system_name,
                industrial_cyno_module_type_id = excluded.industrial_cyno_module_type_id,
                industrial_cyno_module_name = excluded.industrial_cyno_module_name,
                tackle_module_name = excluded.tackle_module_name,
                tackle_type = excluded.tackle_type,
                quantity_destroyed = excluded.quantity_destroyed,
                quantity_dropped = excluded.quantity_dropped,
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
            var victimShipTypeParam = insertCommand.CreateParameter();
            victimShipTypeParam.ParameterName = "$victimShipTypeId";
            insertCommand.Parameters.Add(victimShipTypeParam);
            var victimShipNameParam = insertCommand.CreateParameter();
            victimShipNameParam.ParameterName = "$victimShipName";
            insertCommand.Parameters.Add(victimShipNameParam);
            var solarSystemIdParam = insertCommand.CreateParameter();
            solarSystemIdParam.ParameterName = "$solarSystemId";
            insertCommand.Parameters.Add(solarSystemIdParam);
            var solarSystemNameParam = insertCommand.CreateParameter();
            solarSystemNameParam.ParameterName = "$solarSystemName";
            insertCommand.Parameters.Add(solarSystemNameParam);
            var industrialCynoTypeParam = insertCommand.CreateParameter();
            industrialCynoTypeParam.ParameterName = "$industrialCynoModuleTypeId";
            insertCommand.Parameters.Add(industrialCynoTypeParam);
            var industrialCynoNameParam = insertCommand.CreateParameter();
            industrialCynoNameParam.ParameterName = "$industrialCynoModuleName";
            insertCommand.Parameters.Add(industrialCynoNameParam);
            var tackleTypeIdParam = insertCommand.CreateParameter();
            tackleTypeIdParam.ParameterName = "$tackleModuleTypeId";
            insertCommand.Parameters.Add(tackleTypeIdParam);
            var tackleNameParam = insertCommand.CreateParameter();
            tackleNameParam.ParameterName = "$tackleModuleName";
            insertCommand.Parameters.Add(tackleNameParam);
            var tackleTypeParam = insertCommand.CreateParameter();
            tackleTypeParam.ParameterName = "$tackleType";
            insertCommand.Parameters.Add(tackleTypeParam);
            var destroyedParam = insertCommand.CreateParameter();
            destroyedParam.ParameterName = "$quantityDestroyed";
            insertCommand.Parameters.Add(destroyedParam);
            var droppedParam = insertCommand.CreateParameter();
            droppedParam.ParameterName = "$quantityDropped";
            insertCommand.Parameters.Add(droppedParam);
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
                victimShipTypeParam.Value = (object?)record.VictimShipTypeId ?? DBNull.Value;
                victimShipNameParam.Value = record.VictimShipName;
                solarSystemIdParam.Value = (object?)record.SolarSystemId ?? DBNull.Value;
                solarSystemNameParam.Value = record.SolarSystemName;
                industrialCynoTypeParam.Value = record.IndustrialCynoModuleTypeId;
                industrialCynoNameParam.Value = record.IndustrialCynoModuleName;
                tackleTypeIdParam.Value = record.TackleModuleTypeId;
                tackleNameParam.Value = record.TackleModuleName;
                tackleTypeParam.Value = record.TackleType.ToString();
                destroyedParam.Value = record.QuantityDestroyed;
                droppedParam.Value = record.QuantityDropped;
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
            command.CommandText = "DELETE FROM pilot_bait_observations_day;";
            return command.ExecuteNonQuery();
        }

        public int CountAll()
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM pilot_bait_observations_day;";
            return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        public List<IndustrialCynoBaitEvidence> GetRecentBaitEvidenceByCharacterId(string characterId, int maxResults = 10)
        {
            var normalizedCharacterId = NormalizeCharacterId(characterId);
            if (string.IsNullOrWhiteSpace(normalizedCharacterId))
            {
                return new List<IndustrialCynoBaitEvidence>();
            }

            var results = new List<IndustrialCynoBaitEvidence>();
            var limit = maxResults <= 0 ? 10 : maxResults;

            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            SELECT
                character_id,
                killmail_id,
                killmail_time_utc,
                victim_ship_type_id,
                victim_ship_name,
                solar_system_id,
                solar_system_name,
                industrial_cyno_module_type_id,
                industrial_cyno_module_name,
                tackle_module_type_id,
                tackle_module_name,
                tackle_type,
                quantity_destroyed,
                quantity_dropped,
                source
            FROM pilot_bait_observations_day
            WHERE character_id = $characterId
            ORDER BY killmail_time_utc DESC
            LIMIT $maxResults;
            ";
            command.Parameters.AddWithValue("$characterId", normalizedCharacterId);
            command.Parameters.AddWithValue("$maxResults", limit);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new IndustrialCynoBaitEvidence
                {
                    CharacterId = reader.IsDBNull(0) ? "" : reader.GetString(0),
                    KillmailId = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    KillmailTimeUtc = TryParseUtc(reader.IsDBNull(2) ? "" : reader.GetString(2)),
                    VictimShipTypeId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                    VictimShipName = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    SolarSystemId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    SolarSystemName = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    IndustrialCynoModuleTypeId = reader.GetInt32(7),
                    IndustrialCynoModuleName = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    TackleModuleTypeId = reader.GetInt32(9),
                    TackleModuleName = reader.IsDBNull(10) ? "" : reader.GetString(10),
                    TackleType = ParseTackleType(reader.IsDBNull(11) ? "" : reader.GetString(11)),
                    QuantityDestroyed = reader.IsDBNull(12) ? 0 : reader.GetInt32(12),
                    QuantityDropped = reader.IsDBNull(13) ? 0 : reader.GetInt32(13),
                    Source = reader.IsDBNull(14) ? "" : reader.GetString(14)
                });
            }

            return results;
        }

        public List<IndustrialCynoBaitEvidence> GetRecentExamples(int maxResults = 20)
        {
            var results = new List<IndustrialCynoBaitEvidence>();
            var limit = maxResults <= 0 ? 20 : maxResults;

            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            SELECT
                character_id,
                killmail_id,
                killmail_time_utc,
                victim_ship_type_id,
                victim_ship_name,
                solar_system_id,
                solar_system_name,
                industrial_cyno_module_type_id,
                industrial_cyno_module_name,
                tackle_module_type_id,
                tackle_module_name,
                tackle_type,
                quantity_destroyed,
                quantity_dropped,
                source
            FROM pilot_bait_observations_day
            ORDER BY killmail_time_utc DESC
            LIMIT $maxResults;
            ";
            command.Parameters.AddWithValue("$maxResults", limit);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new IndustrialCynoBaitEvidence
                {
                    CharacterId = reader.IsDBNull(0) ? "" : reader.GetString(0),
                    KillmailId = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    KillmailTimeUtc = TryParseUtc(reader.IsDBNull(2) ? "" : reader.GetString(2)),
                    VictimShipTypeId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                    VictimShipName = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    SolarSystemId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    SolarSystemName = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    IndustrialCynoModuleTypeId = reader.GetInt32(7),
                    IndustrialCynoModuleName = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    TackleModuleTypeId = reader.GetInt32(9),
                    TackleModuleName = reader.IsDBNull(10) ? "" : reader.GetString(10),
                    TackleType = ParseTackleType(reader.IsDBNull(11) ? "" : reader.GetString(11)),
                    QuantityDestroyed = reader.IsDBNull(12) ? 0 : reader.GetInt32(12),
                    QuantityDropped = reader.IsDBNull(13) ? 0 : reader.GetInt32(13),
                    Source = reader.IsDBNull(14) ? "" : reader.GetString(14)
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

        private static TackleModuleType ParseTackleType(string value)
        {
            return Enum.TryParse<TackleModuleType>(value, ignoreCase: true, out var parsed)
                ? parsed
                : TackleModuleType.UnknownTackle;
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
