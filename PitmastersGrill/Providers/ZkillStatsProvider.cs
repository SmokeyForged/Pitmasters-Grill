using PitmastersLittleGrill.Models;
using PitmastersLittleGrill.Persistence;
using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersLittleGrill.Providers
{
    public class ZkillStatsProvider
    {
        private readonly HttpClient _httpClient;

        public ZkillStatsProvider()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20)
            };

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PitmastersGrill/0.6.x");
        }

        public async Task<ProviderOutcome<StatsCacheEntry>> TryGetStatsAsync(
            string characterId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return ProviderOutcome<StatsCacheEntry>.PermanentFailure(
                    "zkill_stats",
                    "Character id was empty");
            }

            var url = $"https://zkillboard.com/api/stats/characterID/{characterId}/";

            try
            {
                using var response = await _httpClient.GetAsync(url, cancellationToken);

                if (response.StatusCode == (HttpStatusCode)429)
                {
                    var retryAfterUtc = TryGetRetryAfterUtc(response);
                    DebugTraceWriter.WriteLine(
                        $"zkill stats throttled: characterId={characterId}, retryAfterUtc='{retryAfterUtc:O}'");
                    return ProviderOutcome<StatsCacheEntry>.Throttled(
                        "zkill_stats",
                        "zKill stats lookup throttled",
                        retryAfterUtc);
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    DebugTraceWriter.WriteLine(
                        $"zkill stats not found: characterId={characterId}");
                    return ProviderOutcome<StatsCacheEntry>.NotFound(
                        "zkill_stats",
                        "zKill stats returned not found");
                }

                if ((int)response.StatusCode >= 500)
                {
                    DebugTraceWriter.WriteLine(
                        $"zkill stats transient failure: characterId={characterId}, status={(int)response.StatusCode}");
                    return ProviderOutcome<StatsCacheEntry>.TemporaryFailure(
                        "zkill_stats",
                        $"zKill stats returned {(int)response.StatusCode}");
                }

                if (!response.IsSuccessStatusCode)
                {
                    DebugTraceWriter.WriteLine(
                        $"zkill stats fetch failed: characterId={characterId}, status={(int)response.StatusCode}");
                    return ProviderOutcome<StatsCacheEntry>.TemporaryFailure(
                        "zkill_stats",
                        $"zKill stats returned {(int)response.StatusCode}");
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                var root = document.RootElement;

                var kills = TryReadInt(root, "shipsDestroyed");
                var losses = TryReadInt(root, "shipsLost");

                var nowUtc = DateTime.UtcNow;
                var refreshedAtUtc = nowUtc.ToString("o", CultureInfo.InvariantCulture);
                var expiresAtUtc = nowUtc.AddHours(12).ToString("o", CultureInfo.InvariantCulture);

                return ProviderOutcome<StatsCacheEntry>.Success(
                    new StatsCacheEntry
                    {
                        CharacterId = characterId,
                        KillCount = kills,
                        LossCount = losses,
                        AvgAttackersWhenAttacking = 0,
                        LastPublicCynoCapableHull = "",
                        RefreshedAtUtc = refreshedAtUtc,
                        ExpiresAtUtc = expiresAtUtc
                    },
                    "zkill_stats",
                    "zKill stats resolved successfully");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                DebugTraceWriter.WriteLine(
                    $"zkill stats timeout: characterId={characterId}");
                return ProviderOutcome<StatsCacheEntry>.TemporaryFailure(
                    "zkill_stats",
                    "zKill stats lookup timed out");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                DebugTraceWriter.WriteLine(
                    $"zkill stats fetch exception: characterId={characterId}, error={ex.Message}");
                return ProviderOutcome<StatsCacheEntry>.TemporaryFailure(
                    "zkill_stats",
                    $"zKill stats exception: {ex.Message}");
            }
        }

        private static DateTime? TryGetRetryAfterUtc(HttpResponseMessage response)
        {
            if (response.Headers.RetryAfter?.Delta != null)
            {
                return DateTime.UtcNow.Add(response.Headers.RetryAfter.Delta.Value);
            }

            if (response.Headers.RetryAfter?.Date != null)
            {
                return response.Headers.RetryAfter.Date.Value.UtcDateTime;
            }

            return null;
        }

        private static int TryReadInt(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                return 0;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var directValue))
            {
                return directValue;
            }

            if (value.ValueKind == JsonValueKind.String &&
                int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return 0;
        }
    }
}