using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersGrill.Providers
{
    public class EsiExactNameResolverProvider
    {
        private readonly HttpClient _httpClient;

        public EsiExactNameResolverProvider()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20)
            };

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PitmastersGrill/0.6.1");
        }

        public async Task<ProviderOutcome<string>> TryResolveCharacterIdExactAsync(
            string characterName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return ProviderOutcome<string>.PermanentFailure(
                    "esi_exact_name",
                    "Character name was empty");
            }

            var trimmedName = characterName.Trim();
            var url = "https://esi.evetech.net/v1/universe/ids/?datasource=tranquility";

            try
            {
                var jsonBody = JsonSerializer.Serialize(new[] { trimmedName });
                using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                using var response = await _httpClient.PostAsync(url, content, cancellationToken);

                if (response.StatusCode == (HttpStatusCode)429)
                {
                    var retryAfterUtc = TryGetRetryAfterUtc(response);
                    DebugTraceWriter.WriteLine(
                        $"esi exact resolver throttled: name='{trimmedName}', retryAfterUtc='{retryAfterUtc:O}'");
                    return ProviderOutcome<string>.Throttled(
                        "esi_exact_name",
                        "ESI exact-name lookup throttled",
                        retryAfterUtc);
                }

                if ((int)response.StatusCode >= 500)
                {
                    DebugTraceWriter.WriteLine(
                        $"esi exact resolver transient failure: name='{trimmedName}', status={(int)response.StatusCode}");
                    return ProviderOutcome<string>.TemporaryFailure(
                        "esi_exact_name",
                        $"ESI exact-name lookup returned {(int)response.StatusCode}");
                }

                if (!response.IsSuccessStatusCode)
                {
                    DebugTraceWriter.WriteLine(
                        $"esi exact resolver ids lookup failed: name='{trimmedName}', status={(int)response.StatusCode}, url='{url}'");
                    return ProviderOutcome<string>.TemporaryFailure(
                        "esi_exact_name",
                        $"ESI exact-name lookup returned {(int)response.StatusCode}");
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                var root = document.RootElement;

                if (!root.TryGetProperty("characters", out var charactersElement))
                {
                    DebugTraceWriter.WriteLine(
                        $"esi exact resolver ids lookup returned no characters array: name='{trimmedName}'");
                    return ProviderOutcome<string>.NotFound(
                        "esi_exact_name",
                        "ESI exact-name lookup returned no characters array");
                }

                if (charactersElement.ValueKind != JsonValueKind.Array || charactersElement.GetArrayLength() == 0)
                {
                    DebugTraceWriter.WriteLine(
                        $"esi exact resolver ids lookup found no exact character: name='{trimmedName}'");
                    return ProviderOutcome<string>.NotFound(
                        "esi_exact_name",
                        "ESI exact-name lookup found no exact character");
                }

                foreach (var entry in charactersElement.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var entryName = TryGetString(entry, "name");
                    var entryId = TryGetIdString(entry, "id");

                    if (!string.IsNullOrWhiteSpace(entryId) &&
                        string.Equals(entryName, trimmedName, StringComparison.OrdinalIgnoreCase))
                    {
                        DebugTraceWriter.WriteLine(
                            $"esi exact resolver ids success: name='{trimmedName}', characterId='{entryId}'");
                        return ProviderOutcome<string>.Success(
                            entryId,
                            "esi_exact_name",
                            "ESI exact-name lookup resolved the character");
                    }
                }

                var first = charactersElement[0];
                var firstId = TryGetIdString(first, "id");
                if (!string.IsNullOrWhiteSpace(firstId))
                {
                    DebugTraceWriter.WriteLine(
                        $"esi exact resolver ids fallback-first-hit: name='{trimmedName}', characterId='{firstId}'");
                    return ProviderOutcome<string>.Success(
                        firstId,
                        "esi_exact_name",
                        "ESI exact-name lookup returned a fallback first hit");
                }

                DebugTraceWriter.WriteLine(
                    $"esi exact resolver ids lookup returned characters but no usable id: name='{trimmedName}'");
                return ProviderOutcome<string>.NotFound(
                    "esi_exact_name",
                    "ESI exact-name lookup returned no usable id");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                DebugTraceWriter.WriteLine(
                    $"esi exact resolver timeout: name='{trimmedName}', url='{url}'");
                return ProviderOutcome<string>.TemporaryFailure(
                    "esi_exact_name",
                    "ESI exact-name lookup timed out");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                DebugTraceWriter.WriteLine(
                    $"esi exact resolver ids exception: name='{trimmedName}', error={ex.Message}, url='{url}'");
                return ProviderOutcome<string>.TemporaryFailure(
                    "esi_exact_name",
                    $"ESI exact-name exception: {ex.Message}");
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

        private static string TryGetString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property))
            {
                return "";
            }

            if (property.ValueKind != JsonValueKind.String)
            {
                return "";
            }

            return property.GetString() ?? "";
        }

        private static string TryGetIdString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property))
            {
                return "";
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var numberValue))
            {
                return numberValue.ToString();
            }

            if (property.ValueKind == JsonValueKind.String)
            {
                return property.GetString() ?? "";
            }

            return "";
        }
    }
}