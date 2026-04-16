using Microsoft.Data.Sqlite;
using PitmastersLittleGrill.Models;
using System.Collections.Generic;

namespace PitmastersLittleGrill.Persistence
{
    public class PilotRegistryDayRepository
    {
        private readonly string _databasePath;

        public PilotRegistryDayRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        public void ReplaceDay(string dayUtc, IReadOnlyList<PilotRegistryDayRecord> records)
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
                DELETE FROM pilot_registry_day
                WHERE day_utc = $dayUtc;
                ";
                deleteCommand.Parameters.AddWithValue("$dayUtc", dayUtc);
                deleteCommand.ExecuteNonQuery();
            }

            using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText =
            @"
            INSERT INTO pilot_registry_day (
                day_utc,
                character_id,
                first_seen_killmail_time_utc,
                last_seen_killmail_time_utc,
                seen_count,
                updated_at_utc
            )
            VALUES (
                $dayUtc,
                $characterId,
                $firstSeenKillmailTimeUtc,
                $lastSeenKillmailTimeUtc,
                $seenCount,
                $updatedAtUtc
            );
            ";

            var dayParameter = insertCommand.CreateParameter();
            dayParameter.ParameterName = "$dayUtc";
            insertCommand.Parameters.Add(dayParameter);

            var characterIdParameter = insertCommand.CreateParameter();
            characterIdParameter.ParameterName = "$characterId";
            insertCommand.Parameters.Add(characterIdParameter);

            var firstSeenParameter = insertCommand.CreateParameter();
            firstSeenParameter.ParameterName = "$firstSeenKillmailTimeUtc";
            insertCommand.Parameters.Add(firstSeenParameter);

            var lastSeenParameter = insertCommand.CreateParameter();
            lastSeenParameter.ParameterName = "$lastSeenKillmailTimeUtc";
            insertCommand.Parameters.Add(lastSeenParameter);

            var seenCountParameter = insertCommand.CreateParameter();
            seenCountParameter.ParameterName = "$seenCount";
            insertCommand.Parameters.Add(seenCountParameter);

            var updatedAtParameter = insertCommand.CreateParameter();
            updatedAtParameter.ParameterName = "$updatedAtUtc";
            insertCommand.Parameters.Add(updatedAtParameter);

            insertCommand.Prepare();

            foreach (var record in records)
            {
                dayParameter.Value = record.DayUtc;
                characterIdParameter.Value = record.CharacterId;
                firstSeenParameter.Value = record.FirstSeenKillmailTimeUtc;
                lastSeenParameter.Value = record.LastSeenKillmailTimeUtc;
                seenCountParameter.Value = record.SeenCount;
                updatedAtParameter.Value = record.UpdatedAtUtc;

                insertCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        public Dictionary<string, PilotRegistryAggregate> GetAggregatesByCharacterIds(
            IEnumerable<string> characterIds)
        {
            var results = new Dictionary<string, PilotRegistryAggregate>(System.StringComparer.OrdinalIgnoreCase);
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
                MIN(first_seen_killmail_time_utc) AS first_seen_killmail_time_utc,
                MAX(last_seen_killmail_time_utc) AS last_seen_killmail_time_utc,
                SUM(seen_count) AS seen_count
            FROM pilot_registry_day
            WHERE character_id IN ({string.Join(", ", placeholders)})
            GROUP BY character_id;
            ";

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var aggregate = new PilotRegistryAggregate
                {
                    CharacterId = reader.GetString(0),
                    FirstSeenKillmailTimeUtc = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    LastSeenKillmailTimeUtc = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    SeenCount = reader.GetInt32(3)
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