using Microsoft.Data.Sqlite;
using PitmastersGrill.Models;
using PitmastersGrill.Providers;
using System;
using System.Collections.Generic;

namespace PitmastersGrill.Persistence
{
    public class PilotShipObservationDayRepository
    {
        private readonly string _databasePath;

        public PilotShipObservationDayRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        public void ReplaceDay(string dayUtc, IReadOnlyList<PilotShipObservationDayRecord> records)
        {
            var connectionString = $"Data Source={_databasePath}";

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();

            using (var deleteCommand = connection.CreateCommand())
            {
                deleteCommand.Transaction = transaction;
                deleteCommand.CommandText =
                @"
                DELETE FROM pilot_ship_observations_day
                WHERE day_utc = $dayUtc;
                ";
                deleteCommand.Parameters.AddWithValue("$dayUtc", dayUtc);
                deleteCommand.ExecuteNonQuery();
            }

            using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText =
            @"
            INSERT INTO pilot_ship_observations_day (
                day_utc,
                character_id,
                last_seen_ship_type_id,
                last_seen_ship_time_utc,
                last_seen_cyno_ship_type_id,
                last_seen_cyno_ship_name,
                last_seen_cyno_ship_time_utc,
                updated_at_utc
            )
            VALUES (
                $dayUtc,
                $characterId,
                $lastSeenShipTypeId,
                $lastSeenShipTimeUtc,
                $lastSeenCynoShipTypeId,
                $lastSeenCynoShipName,
                $lastSeenCynoShipTimeUtc,
                $updatedAtUtc
            );
            ";

            var dayParameter = insertCommand.CreateParameter();
            dayParameter.ParameterName = "$dayUtc";
            insertCommand.Parameters.Add(dayParameter);

            var characterIdParameter = insertCommand.CreateParameter();
            characterIdParameter.ParameterName = "$characterId";
            insertCommand.Parameters.Add(characterIdParameter);

            var lastSeenShipTypeIdParameter = insertCommand.CreateParameter();
            lastSeenShipTypeIdParameter.ParameterName = "$lastSeenShipTypeId";
            insertCommand.Parameters.Add(lastSeenShipTypeIdParameter);

            var lastSeenShipTimeUtcParameter = insertCommand.CreateParameter();
            lastSeenShipTimeUtcParameter.ParameterName = "$lastSeenShipTimeUtc";
            insertCommand.Parameters.Add(lastSeenShipTimeUtcParameter);

            var lastSeenCynoShipTypeIdParameter = insertCommand.CreateParameter();
            lastSeenCynoShipTypeIdParameter.ParameterName = "$lastSeenCynoShipTypeId";
            insertCommand.Parameters.Add(lastSeenCynoShipTypeIdParameter);

            var lastSeenCynoShipNameParameter = insertCommand.CreateParameter();
            lastSeenCynoShipNameParameter.ParameterName = "$lastSeenCynoShipName";
            insertCommand.Parameters.Add(lastSeenCynoShipNameParameter);

            var lastSeenCynoShipTimeUtcParameter = insertCommand.CreateParameter();
            lastSeenCynoShipTimeUtcParameter.ParameterName = "$lastSeenCynoShipTimeUtc";
            insertCommand.Parameters.Add(lastSeenCynoShipTimeUtcParameter);

            var updatedAtParameter = insertCommand.CreateParameter();
            updatedAtParameter.ParameterName = "$updatedAtUtc";
            insertCommand.Parameters.Add(updatedAtParameter);

            insertCommand.Prepare();

            foreach (var record in records)
            {
                dayParameter.Value = record.DayUtc;
                characterIdParameter.Value = record.CharacterId;
                lastSeenShipTypeIdParameter.Value = (object?)record.LastSeenShipTypeId ?? DBNull.Value;
                lastSeenShipTimeUtcParameter.Value = record.LastSeenShipTimeUtc;
                lastSeenCynoShipTypeIdParameter.Value = (object?)record.LastSeenCynoShipTypeId ?? DBNull.Value;
                lastSeenCynoShipNameParameter.Value = record.LastSeenCynoShipName ?? "";
                lastSeenCynoShipTimeUtcParameter.Value = record.LastSeenCynoShipTimeUtc ?? "";
                updatedAtParameter.Value = record.UpdatedAtUtc;

                insertCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        public Dictionary<string, PilotLastShipObservationAggregate> GetLatestShipSeenByCharacterIds(
            IEnumerable<string> characterIds)
        {
            var results = new Dictionary<string, PilotLastShipObservationAggregate>(StringComparer.OrdinalIgnoreCase);
            var ids = NormalizeIds(characterIds);

            if (ids.Count == 0)
            {
                return results;
            }

            var connectionString = $"Data Source={_databasePath}";
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            var placeholders = new List<string>();

            for (var i = 0; i < ids.Count; i++)
            {
                var parameterName = $"$id{i}";
                placeholders.Add(parameterName);
                command.Parameters.AddWithValue(parameterName, ids[i]);
            }

            command.CommandText =
            $@"
            SELECT
                p.character_id,
                p.last_seen_ship_type_id,
                p.last_seen_ship_time_utc
            FROM pilot_ship_observations_day p
            INNER JOIN (
                SELECT
                    character_id,
                    MAX(last_seen_ship_time_utc) AS max_ship_time
                FROM pilot_ship_observations_day
                WHERE character_id IN ({string.Join(", ", placeholders)})
                  AND last_seen_ship_time_utc <> ''
                GROUP BY character_id
            ) latest
                ON latest.character_id = p.character_id
               AND latest.max_ship_time = p.last_seen_ship_time_utc;
            ";

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var aggregate = new PilotLastShipObservationAggregate
                {
                    CharacterId = reader.GetString(0),
                    LastSeenShipTypeId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                    LastSeenShipTimeUtc = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    LastSeenShipName = ""
                };

                results[aggregate.CharacterId] = aggregate;
            }

            return results;
        }

        public List<PilotLastShipObservationAggregate> GetRecentShipSeenHistoryByCharacterId(
            string characterId,
            int maxResults = 50)
        {
            var results = new List<PilotLastShipObservationAggregate>();

            if (string.IsNullOrWhiteSpace(characterId))
            {
                return results;
            }

            if (maxResults <= 0)
            {
                maxResults = 50;
            }

            var connectionString = $"Data Source={_databasePath}";
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            SELECT
                character_id,
                last_seen_ship_type_id,
                last_seen_ship_time_utc
            FROM pilot_ship_observations_day
            WHERE character_id = $characterId
              AND last_seen_ship_time_utc <> ''
              AND last_seen_ship_type_id IS NOT NULL
            ORDER BY last_seen_ship_time_utc DESC
            LIMIT $maxResults;
            ";
            command.Parameters.AddWithValue("$characterId", characterId);
            command.Parameters.AddWithValue("$maxResults", maxResults);

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                results.Add(new PilotLastShipObservationAggregate
                {
                    CharacterId = reader.GetString(0),
                    LastSeenShipTypeId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                    LastSeenShipTimeUtc = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    LastSeenShipName = ""
                });
            }

            return results;
        }

        public Dictionary<string, PilotCynoObservationAggregate> GetLatestCynoByCharacterIds(
            IEnumerable<string> characterIds)
        {
            var results = new Dictionary<string, PilotCynoObservationAggregate>(StringComparer.OrdinalIgnoreCase);
            var ids = NormalizeIds(characterIds);

            if (ids.Count == 0)
            {
                return results;
            }

            var connectionString = $"Data Source={_databasePath}";
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            var placeholders = new List<string>();

            for (var i = 0; i < ids.Count; i++)
            {
                var parameterName = $"$id{i}";
                placeholders.Add(parameterName);
                command.Parameters.AddWithValue(parameterName, ids[i]);
            }

            command.CommandText =
            $@"
            SELECT
                p.character_id,
                p.last_seen_ship_type_id,
                p.last_seen_ship_time_utc,
                p.last_seen_cyno_ship_type_id,
                p.last_seen_cyno_ship_name,
                p.last_seen_cyno_ship_time_utc
            FROM pilot_ship_observations_day p
            WHERE p.character_id IN ({string.Join(", ", placeholders)})
              AND (
                    (p.last_seen_cyno_ship_time_utc <> '' AND p.last_seen_cyno_ship_name <> '')
                 OR (p.last_seen_ship_time_utc <> '' AND p.last_seen_ship_type_id IS NOT NULL)
              )
            ORDER BY
                p.character_id ASC,
                CASE
                    WHEN p.last_seen_ship_time_utc > p.last_seen_cyno_ship_time_utc THEN p.last_seen_ship_time_utc
                    ELSE p.last_seen_cyno_ship_time_utc
                END DESC,
                p.day_utc DESC;
            ";

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var characterId = reader.GetString(0);
                int? lastSeenShipTypeId = reader.IsDBNull(1) ? null : reader.GetInt32(1);
                var lastSeenShipTimeUtc = reader.IsDBNull(2) ? "" : reader.GetString(2);
                int? persistedCynoShipTypeId = reader.IsDBNull(3) ? null : reader.GetInt32(3);
                var persistedCynoShipName = reader.IsDBNull(4) ? "" : reader.GetString(4);
                var persistedCynoShipTimeUtc = reader.IsDBNull(5) ? "" : reader.GetString(5);

                if (!results.TryGetValue(characterId, out var aggregate))
                {
                    aggregate = new PilotCynoObservationAggregate
                    {
                        CharacterId = characterId
                    };

                    results[characterId] = aggregate;
                }

                if (!string.IsNullOrWhiteSpace(persistedCynoShipName) &&
                    !string.IsNullOrWhiteSpace(persistedCynoShipTimeUtc))
                {
                    TryApplyNewerCynoObservation(
                        aggregate,
                        persistedCynoShipTypeId,
                        persistedCynoShipName,
                        persistedCynoShipTimeUtc);
                }

                if (CynoShipCatalog.TryGetCynoShipNameByTypeId(lastSeenShipTypeId, out var currentCatalogCynoShipName) &&
                    !string.IsNullOrWhiteSpace(lastSeenShipTimeUtc))
                {
                    TryApplyNewerCynoObservation(
                        aggregate,
                        lastSeenShipTypeId,
                        currentCatalogCynoShipName,
                        lastSeenShipTimeUtc);
                }
            }

            return results;
        }

        private static void TryApplyNewerCynoObservation(
            PilotCynoObservationAggregate aggregate,
            int? shipTypeId,
            string shipName,
            string seenAtUtc)
        {
            if (aggregate == null ||
                string.IsNullOrWhiteSpace(shipName) ||
                string.IsNullOrWhiteSpace(seenAtUtc))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(aggregate.LastSeenCynoShipTimeUtc) &&
                string.CompareOrdinal(seenAtUtc, aggregate.LastSeenCynoShipTimeUtc) <= 0)
            {
                return;
            }

            aggregate.LastSeenCynoShipTypeId = shipTypeId;
            aggregate.LastSeenCynoShipName = shipName;
            aggregate.LastSeenCynoShipTimeUtc = seenAtUtc;
        }

        private static List<string> NormalizeIds(IEnumerable<string> characterIds)
        {
            var results = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (characterIds == null)
            {
                return results;
            }

            foreach (var id in characterIds)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                if (!seen.Add(id))
                {
                    continue;
                }

                results.Add(id);
            }

            return results;
        }
    }
}
