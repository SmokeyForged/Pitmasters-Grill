using Microsoft.Data.Sqlite;
using PitmastersLittleGrill.Models;
using System;
using System.Collections.Generic;

namespace PitmastersLittleGrill.Persistence
{
    public class ResolverCacheRepository
    {
        private readonly string _databasePath;

        public ResolverCacheRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        public Dictionary<string, ResolverCacheEntry> GetByCharacterNames(List<string> characterNames)
        {
            var results = new Dictionary<string, ResolverCacheEntry>(StringComparer.OrdinalIgnoreCase);

            if (characterNames == null || characterNames.Count == 0)
            {
                return results;
            }

            var utcNow = DateTime.UtcNow;
            var connectionString = $"Data Source={_databasePath}";

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            foreach (var characterName in characterNames)
            {
                if (string.IsNullOrWhiteSpace(characterName))
                {
                    continue;
                }

                using var command = connection.CreateCommand();
                command.CommandText =
                @"
                SELECT
                    character_name,
                    character_id,
                    alliance_name,
                    alliance_ticker,
                    corp_name,
                    corp_ticker,
                    resolver_confidence,
                    resolved_at_utc,
                    expires_at_utc,
                    affiliation_checked_at_utc
                FROM resolver_cache
                WHERE character_name = $characterName
                LIMIT 1;
                ";
                command.Parameters.AddWithValue("$characterName", characterName);

                using var reader = command.ExecuteReader();

                if (!reader.Read())
                {
                    continue;
                }

                var entry = new ResolverCacheEntry
                {
                    CharacterName = reader.GetString(0),
                    CharacterId = reader.GetString(1),
                    AllianceName = reader.GetString(2),
                    AllianceTicker = reader.GetString(3),
                    CorpName = reader.GetString(4),
                    CorpTicker = reader.GetString(5),
                    ResolverConfidence = reader.GetString(6),
                    ResolvedAtUtc = reader.GetString(7),
                    ExpiresAtUtc = reader.GetString(8),
                    AffiliationCheckedAtUtc = reader.GetString(9)
                };

                if (!DateTime.TryParse(entry.ExpiresAtUtc, out var expiresAtUtc))
                {
                    continue;
                }

                if (expiresAtUtc < utcNow)
                {
                    continue;
                }

                results[characterName] = entry;
            }

            return results;
        }

        public void Upsert(ResolverCacheEntry entry)
        {
            var connectionString = $"Data Source={_databasePath}";

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            INSERT INTO resolver_cache (
                character_name,
                character_id,
                alliance_name,
                alliance_ticker,
                corp_name,
                corp_ticker,
                resolver_confidence,
                resolved_at_utc,
                expires_at_utc,
                affiliation_checked_at_utc
            )
            VALUES (
                $characterName,
                $characterId,
                $allianceName,
                $allianceTicker,
                $corpName,
                $corpTicker,
                $resolverConfidence,
                $resolvedAtUtc,
                $expiresAtUtc,
                $affiliationCheckedAtUtc
            )
            ON CONFLICT(character_name) DO UPDATE SET
                character_id = excluded.character_id,
                alliance_name = excluded.alliance_name,
                alliance_ticker = excluded.alliance_ticker,
                corp_name = excluded.corp_name,
                corp_ticker = excluded.corp_ticker,
                resolver_confidence = excluded.resolver_confidence,
                resolved_at_utc = excluded.resolved_at_utc,
                expires_at_utc = excluded.expires_at_utc,
                affiliation_checked_at_utc = excluded.affiliation_checked_at_utc;
            ";

            command.Parameters.AddWithValue("$characterName", entry.CharacterName);
            command.Parameters.AddWithValue("$characterId", entry.CharacterId);
            command.Parameters.AddWithValue("$allianceName", entry.AllianceName);
            command.Parameters.AddWithValue("$allianceTicker", entry.AllianceTicker);
            command.Parameters.AddWithValue("$corpName", entry.CorpName);
            command.Parameters.AddWithValue("$corpTicker", entry.CorpTicker);
            command.Parameters.AddWithValue("$resolverConfidence", entry.ResolverConfidence);
            command.Parameters.AddWithValue("$resolvedAtUtc", entry.ResolvedAtUtc);
            command.Parameters.AddWithValue("$expiresAtUtc", entry.ExpiresAtUtc);
            command.Parameters.AddWithValue("$affiliationCheckedAtUtc", entry.AffiliationCheckedAtUtc);

            command.ExecuteNonQuery();
        }
    }
}