using PitmastersLittleGrill.Models;
using PitmastersLittleGrill.Persistence;
using System;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersLittleGrill.Providers
{
    public class ZkillSearchResolverProvider
    {
        private readonly HttpClient _httpClient;

        public ZkillSearchResolverProvider()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20)
            };

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PitmastersGrill/0.6.1");
        }

        public async Task<ProviderOutcome<ResolverCacheEntry>> TryResolveCharacterAsync(
            string characterName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return ProviderOutcome<ResolverCacheEntry>.PermanentFailure(
                    "zkill_search",
                    "Character name was empty");
            }

            var trimmedName = characterName.Trim();
            var nowUtc = DateTime.UtcNow;
            var searchText = Uri.EscapeDataString(trimmedName);
            var url = $"https://zkillboard.com/search/{searchText}/";

            try
            {
                using var response = await _httpClient.GetAsync(url, cancellationToken);

                if (response.StatusCode == (HttpStatusCode)429)
                {
                    var retryAfterUtc = TryGetRetryAfterUtc(response);
                    DebugTraceWriter.WriteLine(
                        $"zkill resolver throttled: name='{trimmedName}', retryAfterUtc='{retryAfterUtc:O}'");
                    return ProviderOutcome<ResolverCacheEntry>.Throttled(
                        "zkill_search",
                        "zKill search throttled",
                        retryAfterUtc);
                }

                if ((int)response.StatusCode >= 500)
                {
                    DebugTraceWriter.WriteLine(
                        $"zkill resolver transient failure: name='{trimmedName}', status={(int)response.StatusCode}");
                    return ProviderOutcome<ResolverCacheEntry>.TemporaryFailure(
                        "zkill_search",
                        $"zKill search returned {(int)response.StatusCode}");
                }

                if (!response.IsSuccessStatusCode)
                {
                    DebugTraceWriter.WriteLine(
                        $"zkill resolver lookup failed: name='{trimmedName}', status={(int)response.StatusCode}");
                    return ProviderOutcome<ResolverCacheEntry>.TemporaryFailure(
                        "zkill_search",
                        $"zKill search returned {(int)response.StatusCode}");
                }

                var html = await response.Content.ReadAsStringAsync(cancellationToken);

                if (IsTerminalNotFound(html))
                {
                    DebugTraceWriter.WriteLine(
                        $"zkill resolver terminal miss: name='{trimmedName}'");

                    return ProviderOutcome<ResolverCacheEntry>.NotFound(
                        "zkill_search",
                        "zKill search returned a terminal miss");
                }

                var characterId = TryExtractCharacterId(html);
                if (string.IsNullOrWhiteSpace(characterId))
                {
                    DebugTraceWriter.WriteLine(
                        $"zkill resolver no-match-without-terminal-miss: name='{trimmedName}'");
                    return ProviderOutcome<ResolverCacheEntry>.TemporaryFailure(
                        "zkill_search",
                        "zKill search returned no usable character id");
                }

                DebugTraceWriter.WriteLine(
                    $"zkill resolver success: name='{trimmedName}', characterId='{characterId}'");

                return ProviderOutcome<ResolverCacheEntry>.Success(
                    new ResolverCacheEntry
                    {
                        CharacterName = trimmedName,
                        CharacterId = characterId,
                        AllianceName = "",
                        AllianceTicker = "",
                        CorpName = "",
                        CorpTicker = "",
                        ResolverConfidence = "search",
                        ResolvedAtUtc = nowUtc.ToString("o"),
                        ExpiresAtUtc = nowUtc.AddDays(30).ToString("o"),
                        AffiliationCheckedAtUtc = ""
                    },
                    "zkill_search",
                    "zKill search resolved the character");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                DebugTraceWriter.WriteLine(
                    $"zkill resolver timeout: name='{trimmedName}'");
                return ProviderOutcome<ResolverCacheEntry>.TemporaryFailure(
                    "zkill_search",
                    "zKill search timed out");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                DebugTraceWriter.WriteLine(
                    $"zkill resolver exception: name='{trimmedName}', error={ex.Message}");
                return ProviderOutcome<ResolverCacheEntry>.TemporaryFailure(
                    "zkill_search",
                    $"zKill search exception: {ex.Message}");
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

        private static bool IsTerminalNotFound(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return false;
            }

            var normalized = Regex.Replace(html, @"\s+", " ").Trim();

            if (normalized.Contains("No Character/Corp/Alliance by that name could be found", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (normalized.Contains("Nothing by that name could be found", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (normalized.Contains("Please search again with the search box at the top", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (normalized.Contains("No results found", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (normalized.Contains("nothing matched your query", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static string TryExtractCharacterId(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return "";
            }

            var characterMatch = Regex.Match(
                html,
                @"/character/(\d+)/",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (characterMatch.Success)
            {
                return characterMatch.Groups[1].Value;
            }

            return "";
        }
    }
}