using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersGrill.Providers
{
    public class EsiPublicAffiliationProvider
    {
        private readonly HttpClient _httpClient;

        public EsiPublicAffiliationProvider()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20)
            };

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PitmastersGrill/0.6.1");
        }

        public async Task<ProviderOutcome<EsiPublicAffiliationResult>> TryGetAffiliationAsync(
            string characterId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return ProviderOutcome<EsiPublicAffiliationResult>.PermanentFailure(
                    "esi_affiliation",
                    "Character id was empty");
            }

            try
            {
                var characterUrl =
                    $"https://esi.evetech.net/latest/characters/{characterId}/?datasource=tranquility";

                using var characterResponse = await _httpClient.GetAsync(characterUrl, cancellationToken);
                if (characterResponse.StatusCode == (HttpStatusCode)429)
                {
                    var retryAfterUtc = TryGetRetryAfterUtc(characterResponse);
                    DebugTraceWriter.WriteLine(
                        $"esi affiliation character throttled: characterId='{characterId}', retryAfterUtc='{retryAfterUtc:O}'");
                    return ProviderOutcome<EsiPublicAffiliationResult>.Throttled(
                        "esi_affiliation_character",
                        "ESI character affiliation lookup throttled",
                        retryAfterUtc);
                }

                if (characterResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    DebugTraceWriter.WriteLine(
                        $"esi affiliation character not found: characterId='{characterId}'");
                    return ProviderOutcome<EsiPublicAffiliationResult>.NotFound(
                        "esi_affiliation_character",
                        "ESI character affiliation lookup returned not found");
                }

                if ((int)characterResponse.StatusCode >= 500)
                {
                    DebugTraceWriter.WriteLine(
                        $"esi affiliation character transient failure: characterId='{characterId}', status={(int)characterResponse.StatusCode}");
                    return ProviderOutcome<EsiPublicAffiliationResult>.TemporaryFailure(
                        "esi_affiliation_character",
                        $"ESI character affiliation lookup returned {(int)characterResponse.StatusCode}");
                }

                if (!characterResponse.IsSuccessStatusCode)
                {
                    DebugTraceWriter.WriteLine(
                        $"esi affiliation character lookup failed: characterId='{characterId}', status={(int)characterResponse.StatusCode}");
                    return ProviderOutcome<EsiPublicAffiliationResult>.TemporaryFailure(
                        "esi_affiliation_character",
                        $"ESI character affiliation lookup returned {(int)characterResponse.StatusCode}");
                }

                await using var characterStream = await characterResponse.Content.ReadAsStreamAsync(cancellationToken);
                using var characterDocument = await JsonDocument.ParseAsync(characterStream, cancellationToken: cancellationToken);

                var root = characterDocument.RootElement;

                var result = new EsiPublicAffiliationResult
                {
                    CharacterId = characterId,
                    CharacterName = TryGetString(root, "name"),
                    CorporationId = TryGetInt64String(root, "corporation_id"),
                    AllianceId = TryGetOptionalInt64String(root, "alliance_id")
                };

                var deferredThrottled = false;
                DateTime? deferredRetryAfterUtc = null;
                string deferredDetail = "";
                string deferredProviderName = "";

                if (!string.IsNullOrWhiteSpace(result.CorporationId))
                {
                    var corporationUrl =
                        $"https://esi.evetech.net/latest/corporations/{result.CorporationId}/?datasource=tranquility";

                    using var corporationResponse = await _httpClient.GetAsync(corporationUrl, cancellationToken);
                    if (corporationResponse.IsSuccessStatusCode)
                    {
                        await using var corporationStream = await corporationResponse.Content.ReadAsStreamAsync(cancellationToken);
                        using var corporationDocument = await JsonDocument.ParseAsync(corporationStream, cancellationToken: cancellationToken);

                        var corporationRoot = corporationDocument.RootElement;
                        result.CorpName = TryGetString(corporationRoot, "name");
                        result.CorpTicker = TryGetString(corporationRoot, "ticker");
                    }
                    else if (corporationResponse.StatusCode == (HttpStatusCode)429)
                    {
                        deferredThrottled = true;
                        deferredRetryAfterUtc = ChooseEarlierRetryAfterUtc(deferredRetryAfterUtc, TryGetRetryAfterUtc(corporationResponse));
                        deferredDetail = "ESI corporation lookup throttled";
                        deferredProviderName = "esi_affiliation_corporation";
                        DebugTraceWriter.WriteLine(
                            $"esi affiliation corporation throttled: corporationId='{result.CorporationId}'");
                    }
                    else if ((int)corporationResponse.StatusCode >= 500)
                    {
                        deferredDetail = "ESI corporation lookup temporarily failed";
                        deferredProviderName = "esi_affiliation_corporation";
                        DebugTraceWriter.WriteLine(
                            $"esi affiliation corporation transient failure: corporationId='{result.CorporationId}', status={(int)corporationResponse.StatusCode}");
                    }
                    else
                    {
                        DebugTraceWriter.WriteLine(
                            $"esi affiliation corporation lookup failed: corporationId='{result.CorporationId}', status={(int)corporationResponse.StatusCode}");
                    }
                }

                if (!string.IsNullOrWhiteSpace(result.AllianceId))
                {
                    var allianceUrl =
                        $"https://esi.evetech.net/latest/alliances/{result.AllianceId}/?datasource=tranquility";

                    using var allianceResponse = await _httpClient.GetAsync(allianceUrl, cancellationToken);
                    if (allianceResponse.IsSuccessStatusCode)
                    {
                        await using var allianceStream = await allianceResponse.Content.ReadAsStreamAsync(cancellationToken);
                        using var allianceDocument = await JsonDocument.ParseAsync(allianceStream, cancellationToken: cancellationToken);

                        var allianceRoot = allianceDocument.RootElement;
                        result.AllianceName = TryGetString(allianceRoot, "name");
                        result.AllianceTicker = TryGetString(allianceRoot, "ticker");
                    }
                    else if (allianceResponse.StatusCode == (HttpStatusCode)429)
                    {
                        deferredThrottled = true;
                        deferredRetryAfterUtc = ChooseEarlierRetryAfterUtc(deferredRetryAfterUtc, TryGetRetryAfterUtc(allianceResponse));
                        deferredDetail = string.IsNullOrWhiteSpace(deferredDetail)
                            ? "ESI alliance lookup throttled"
                            : deferredDetail;
                        deferredProviderName = string.IsNullOrWhiteSpace(deferredProviderName)
                            ? "esi_affiliation_alliance"
                            : deferredProviderName;
                        DebugTraceWriter.WriteLine(
                            $"esi affiliation alliance throttled: allianceId='{result.AllianceId}'");
                    }
                    else if ((int)allianceResponse.StatusCode >= 500)
                    {
                        deferredDetail = string.IsNullOrWhiteSpace(deferredDetail)
                            ? "ESI alliance lookup temporarily failed"
                            : deferredDetail;
                        deferredProviderName = string.IsNullOrWhiteSpace(deferredProviderName)
                            ? "esi_affiliation_alliance"
                            : deferredProviderName;
                        DebugTraceWriter.WriteLine(
                            $"esi affiliation alliance transient failure: allianceId='{result.AllianceId}', status={(int)allianceResponse.StatusCode}");
                    }
                    else
                    {
                        DebugTraceWriter.WriteLine(
                            $"esi affiliation alliance lookup failed: allianceId='{result.AllianceId}', status={(int)allianceResponse.StatusCode}");
                    }
                }

                DebugTraceWriter.WriteLine(
                    $"esi affiliation success: characterId='{characterId}', corp='{result.CorpName}', alliance='{result.AllianceName}'");

                if (deferredThrottled)
                {
                    return ProviderOutcome<EsiPublicAffiliationResult>.Throttled(
                        string.IsNullOrWhiteSpace(deferredProviderName) ? "esi_affiliation" : deferredProviderName,
                        string.IsNullOrWhiteSpace(deferredDetail) ? "ESI supplemental affiliation lookup throttled" : deferredDetail,
                        deferredRetryAfterUtc,
                        result);
                }

                if (!string.IsNullOrWhiteSpace(deferredDetail))
                {
                    return ProviderOutcome<EsiPublicAffiliationResult>.TemporaryFailure(
                        string.IsNullOrWhiteSpace(deferredProviderName) ? "esi_affiliation" : deferredProviderName,
                        deferredDetail,
                        value: result);
                }

                return ProviderOutcome<EsiPublicAffiliationResult>.Success(
                    result,
                    "esi_affiliation",
                    "ESI affiliation enrichment succeeded");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                DebugTraceWriter.WriteLine(
                    $"esi affiliation timeout: characterId='{characterId}'");
                return ProviderOutcome<EsiPublicAffiliationResult>.TemporaryFailure(
                    "esi_affiliation",
                    "ESI affiliation lookup timed out");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                DebugTraceWriter.WriteLine(
                    $"esi affiliation exception: characterId='{characterId}', error={ex.Message}");
                return ProviderOutcome<EsiPublicAffiliationResult>.TemporaryFailure(
                    "esi_affiliation",
                    $"ESI affiliation exception: {ex.Message}");
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

        private static DateTime? ChooseEarlierRetryAfterUtc(DateTime? existing, DateTime? candidate)
        {
            if (!existing.HasValue)
            {
                return candidate;
            }

            if (!candidate.HasValue)
            {
                return existing;
            }

            return candidate.Value < existing.Value ? candidate : existing;
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

        private static string TryGetInt64String(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property))
            {
                return "";
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var longValue))
            {
                return longValue.ToString();
            }

            if (property.ValueKind == JsonValueKind.String)
            {
                return property.GetString() ?? "";
            }

            return "";
        }

        private static string TryGetOptionalInt64String(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property))
            {
                return "";
            }

            if (property.ValueKind == JsonValueKind.Null)
            {
                return "";
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var longValue))
            {
                return longValue.ToString();
            }

            if (property.ValueKind == JsonValueKind.String)
            {
                return property.GetString() ?? "";
            }

            return "";
        }
    }

    public class EsiPublicAffiliationResult
    {
        public string CharacterId { get; set; } = "";
        public string CharacterName { get; set; } = "";
        public string CorporationId { get; set; } = "";
        public string AllianceId { get; set; } = "";
        public string CorpName { get; set; } = "";
        public string CorpTicker { get; set; } = "";
        public string AllianceName { get; set; } = "";
        public string AllianceTicker { get; set; } = "";
    }
}