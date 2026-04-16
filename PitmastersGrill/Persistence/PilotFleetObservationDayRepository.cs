using Microsoft.Data.Sqlite;
using PitmastersLittleGrill.Models;
using System.Collections.Generic;

namespace PitmastersLittleGrill.Persistence
{
    public class PilotFleetObservationDayRepository
    {
        private readonly string _databasePath;

        public PilotFleetObservationDayRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        public void ReplaceDay(string dayUtc, IReadOnlyList<PilotFleetObservationDayRecord> records)
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
                DELETE FROM pilot_fleet_observations_day
                WHERE day_utc = $dayUtc;
                ";
                deleteCommand.Parameters.AddWithValue("$dayUtc", dayUtc);
                deleteCommand.ExecuteNonQuery();
            }

            using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText =
            @"
            INSERT INTO pilot_fleet_observations_day (
                day_utc,
                character_id,
                attacker_sample_count,
                attacker_count_sum,
                derived_at_utc
            )
            VALUES (
                $dayUtc,
                $characterId,
                $attackerSampleCount,
                $attackerCountSum,
                $derivedAtUtc
            );
            ";

            var dayParameter = insertCommand.CreateParameter();
            dayParameter.ParameterName = "$dayUtc";
            insertCommand.Parameters.Add(dayParameter);

            var characterIdParameter = insertCommand.CreateParameter();
            characterIdParameter.ParameterName = "$characterId";
            insertCommand.Parameters.Add(characterIdParameter);

            var attackerSampleCountParameter = insertCommand.CreateParameter();
            attackerSampleCountParameter.ParameterName = "$attackerSampleCount";
            insertCommand.Parameters.Add(attackerSampleCountParameter);

            var attackerCountSumParameter = insertCommand.CreateParameter();
            attackerCountSumParameter.ParameterName = "$attackerCountSum";
            insertCommand.Parameters.Add(attackerCountSumParameter);

            var derivedAtParameter = insertCommand.CreateParameter();
            derivedAtParameter.ParameterName = "$derivedAtUtc";
            insertCommand.Parameters.Add(derivedAtParameter);

            insertCommand.Prepare();

            foreach (var record in records)
            {
                dayParameter.Value = record.DayUtc;
                characterIdParameter.Value = record.CharacterId;
                attackerSampleCountParameter.Value = record.AttackerSampleCount;
                attackerCountSumParameter.Value = record.AttackerCountSum;
                derivedAtParameter.Value = record.DerivedAtUtc;

                insertCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        public Dictionary<string, PilotFleetObservationAggregate> GetAggregatesByCharacterIds(
            IEnumerable<string> characterIds)
        {
            var results = new Dictionary<string, PilotFleetObservationAggregate>(System.StringComparer.OrdinalIgnoreCase);
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
                character_id,
                SUM(attacker_sample_count) AS attacker_sample_count,
                SUM(attacker_count_sum) AS attacker_count_sum
            FROM pilot_fleet_observations_day
            WHERE character_id IN ({string.Join(", ", placeholders)})
            GROUP BY character_id;
            ";

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var aggregate = new PilotFleetObservationAggregate
                {
                    CharacterId = reader.GetString(0),
                    AttackerSampleCount = reader.GetInt32(1),
                    AttackerCountSum = reader.GetInt32(2)
                };

                results[aggregate.CharacterId] = aggregate;
            }

            return results;
        }

        private static List<string> NormalizeIds(IEnumerable<string> characterIds)
        {
            var results = new List<string>();
            var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

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