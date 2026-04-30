using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersGrill.Services
{
    public sealed class ZkillFreshnessClient
    {
        private const string ZkillKillsPastSecondsUrlFormat = "https://zkillboard.com/api/kills/characterID/{0}/pastSeconds/{1}/";
        private const string ZkillLossesPastSecondsUrlFormat = "https://zkillboard.com/api/losses/characterID/{0}/pastSeconds/{1}/";
        private const string ZkillKillsMonthUrlFormat = "https://zkillboard.com/api/kills/characterID/{0}/year/{1}/month/{2:D2}/";
        private const string ZkillLossesMonthUrlFormat = "https://zkillboard.com/api/losses/characterID/{0}/year/{1}/month/{2:D2}/";
        private const string EsiKillmailUrlFormat = "https://esi.evetech.net/latest/killmails/{0}/{1}/?datasource=tranquility";
        private static readonly TimeSpan RateLimitBaseDelay = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan RateLimitJitterMaxDelay = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan RateLimitMaxDelay = TimeSpan.FromMinutes(5);

        private readonly HttpClient _httpClient;
        private readonly Random _random = new();

        public ZkillFreshnessClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(AppHttpDefaults.GenericUserAgent);
            _httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");
        }

        internal Task<ZkillCharacterQueryResult> QueryPastSecondsCharacterAsync(
            long characterId,
            bool losses,
            int pastSeconds,
            CancellationToken cancellationToken)
        {
            var url = string.Format(
                CultureInfo.InvariantCulture,
                losses ? ZkillLossesPastSecondsUrlFormat : ZkillKillsPastSecondsUrlFormat,
                characterId,
                pastSeconds);

            return QueryCharacterAsync(url, cancellationToken);
        }

        internal Task<ZkillCharacterQueryResult> QueryCharacterMonthAsync(
            long characterId,
            bool losses,
            int year,
            int month,
            CancellationToken cancellationToken)
        {
            var url = string.Format(
                CultureInfo.InvariantCulture,
                losses ? ZkillLossesMonthUrlFormat : ZkillKillsMonthUrlFormat,
                characterId,
                year,
                month);

            return QueryCharacterAsync(url, cancellationToken);
        }

        internal async Task<EsiKillmailFetchResult> FetchFullKillmailAsync(
            ZkillKillmailRef killmailRef,
            CancellationToken cancellationToken)
        {
            var url = string.Format(
                CultureInfo.InvariantCulture,
                EsiKillmailUrlFormat,
                killmailRef.KillmailId,
                killmailRef.KillmailHash);

            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return EsiKillmailFetchResult.Failure(
                    $"ESI killmail fetch returned HTTP {(int)response.StatusCode} {response.ReasonPhrase} for killmail {killmailRef.KillmailId}.");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return EsiKillmailFetchResult.Successful(json);
        }

        internal TimeSpan GetRetryDelay(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("Retry-After", out var values))
            {
                foreach (var value in values)
                {
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0)
                    {
                        return TimeSpan.FromSeconds(seconds) + NextJitter(RateLimitJitterMaxDelay);
                    }

                    if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var retryAt))
                    {
                        var delta = retryAt - DateTimeOffset.UtcNow;
                        if (delta > TimeSpan.Zero)
                        {
                            return delta + NextJitter(RateLimitJitterMaxDelay);
                        }
                    }
                }
            }

            var retryDelay = RateLimitBaseDelay + NextJitter(RateLimitJitterMaxDelay);
            return retryDelay > RateLimitMaxDelay
                ? RateLimitMaxDelay
                : retryDelay;
        }

        private async Task<ZkillCharacterQueryResult> QueryCharacterAsync(string url, CancellationToken cancellationToken)
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (response.StatusCode == (HttpStatusCode)420 || response.StatusCode == (HttpStatusCode)429)
            {
                var retryDelay = GetRetryDelay(response);
                var retryAtUtc = DateTime.UtcNow.Add(retryDelay).ToString("o");
                return ZkillCharacterQueryResult.RateLimited(
                    $"zKill rate limited freshness repair. Retry at {retryAtUtc}.",
                    retryDelay,
                    retryAtUtc);
            }

            if (!response.IsSuccessStatusCode)
            {
                return ZkillCharacterQueryResult.Failure(
                    $"zKill entity query returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ZkillCharacterQueryResult.Failure("zKill entity query returned an unexpected JSON payload.");
            }

            var results = new List<ZkillKillmailRef>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!TryReadKillmailRef(item, out var killmailRef))
                {
                    continue;
                }

                results.Add(killmailRef);
            }

            return ZkillCharacterQueryResult.Successful(results);
        }

        private TimeSpan NextJitter(TimeSpan maxDelay)
        {
            if (maxDelay <= TimeSpan.Zero)
            {
                return TimeSpan.Zero;
            }

            lock (_random)
            {
                return TimeSpan.FromMilliseconds(_random.NextDouble() * maxDelay.TotalMilliseconds);
            }
        }

        private static bool TryReadKillmailRef(JsonElement item, out ZkillKillmailRef killmailRef)
        {
            killmailRef = default;

            if (!TryReadInt64(item, "killmail_id", out var killmailId))
            {
                return false;
            }

            if (!item.TryGetProperty("zkb", out var zkb) || zkb.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var hash = TryReadString(zkb, "hash");
            if (string.IsNullOrWhiteSpace(hash))
            {
                return false;
            }

            var killmailTimeUtc = TryReadKillmailTimeUtc(item);
            var dayUtc = killmailTimeUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";

            killmailRef = new ZkillKillmailRef(
                killmailId,
                hash,
                killmailTimeUtc?.ToString("o", CultureInfo.InvariantCulture) ?? "",
                dayUtc);
            return true;
        }

        private static DateTime? TryReadKillmailTimeUtc(JsonElement element)
        {
            var text = TryReadString(element, "killmail_time");
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (!DateTime.TryParse(
                    text,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out var parsed))
            {
                return null;
            }

            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        private static bool TryReadInt64(JsonElement element, string propertyName, out long value)
        {
            value = 0;
            if (!element.TryGetProperty(propertyName, out var property))
            {
                return false;
            }

            if (property.ValueKind == JsonValueKind.Number)
            {
                return property.TryGetInt64(out value);
            }

            if (property.ValueKind == JsonValueKind.String)
            {
                return long.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            }

            return false;
        }

        private static string TryReadString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            {
                return string.Empty;
            }

            return property.GetString() ?? string.Empty;
        }
    }

    internal readonly record struct ZkillKillmailRef(long KillmailId, string KillmailHash, string KillmailTimeUtc, string DayUtc);

    internal sealed class ZkillCharacterQueryResult
    {
        public bool Success { get; private set; }
        public bool IsRateLimited { get; private set; }
        public string Error { get; private set; } = "";
        public string NextRetryAtUtc { get; private set; } = "";
        public TimeSpan? RetryDelay { get; private set; }
        public List<ZkillKillmailRef> Killmails { get; } = new();

        public static ZkillCharacterQueryResult Successful(List<ZkillKillmailRef> killmails)
        {
            var result = new ZkillCharacterQueryResult
            {
                Success = true
            };
            result.Killmails.AddRange(killmails);
            return result;
        }

        public static ZkillCharacterQueryResult Failure(string error)
        {
            return new ZkillCharacterQueryResult
            {
                Success = false,
                Error = error ?? "Query failed."
            };
        }

        public static ZkillCharacterQueryResult RateLimited(string error, TimeSpan retryDelay, string nextRetryAtUtc)
        {
            return new ZkillCharacterQueryResult
            {
                Success = false,
                IsRateLimited = true,
                Error = error ?? "Rate limited.",
                RetryDelay = retryDelay,
                NextRetryAtUtc = nextRetryAtUtc ?? ""
            };
        }
    }

    internal sealed class EsiKillmailFetchResult
    {
        public bool Success { get; private set; }
        public string KillmailJson { get; private set; } = "";
        public string Error { get; private set; } = "";

        public static EsiKillmailFetchResult Successful(string killmailJson)
        {
            return new EsiKillmailFetchResult
            {
                Success = true,
                KillmailJson = killmailJson ?? ""
            };
        }

        public static EsiKillmailFetchResult Failure(string error)
        {
            return new EsiKillmailFetchResult
            {
                Success = false,
                Error = error ?? "Killmail fetch failed."
            };
        }
    }
}
