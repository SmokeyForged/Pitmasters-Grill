using PitmastersLittleGrill.Models;
using PitmastersLittleGrill.Persistence;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace PitmastersLittleGrill.Providers
{
    public class KillmailDatasetMetadataProvider
    {
        private readonly HttpClient _httpClient;

        public KillmailDatasetMetadataProvider()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(15)
            };

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PitmastersGrill/0.6.x");
        }

        public async Task<List<KillmailRemoteDayInfo>> TryGetRemoteDayTotalsAsync()
        {
            var urls = new[]
            {
                "https://data.everef.net/killmails/totals.json",
                "https://data.everef.net/killmails/totals.json.gz"
            };

            foreach (var url in urls)
            {
                try
                {
                    using var response = await _httpClient.GetAsync(url);

                    if (!response.IsSuccessStatusCode)
                    {
                        DebugTraceWriter.WriteLine(
                            $"killmail metadata fetch failed: url={url}, status={(int)response.StatusCode}");
                        continue;
                    }

                    await using var stream = await response.Content.ReadAsStreamAsync();
                    using var document = await JsonDocument.ParseAsync(stream);

                    var parsed = ParseTotals(document.RootElement);

                    if (parsed.Count > 0)
                    {
                        DebugTraceWriter.WriteLine(
                            $"killmail metadata fetch ok: url={url}, days={parsed.Count}");
                        return parsed;
                    }

                    DebugTraceWriter.WriteLine(
                        $"killmail metadata fetch empty-parse: url={url}");
                }
                catch (Exception ex)
                {
                    DebugTraceWriter.WriteLine(
                        $"killmail metadata fetch exception: url={url}, error={ex.Message}");
                }
            }

            return new List<KillmailRemoteDayInfo>();
        }

        private static List<KillmailRemoteDayInfo> ParseTotals(JsonElement root)
        {
            var results = new List<KillmailRemoteDayInfo>();

            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in root.EnumerateObject())
                {
                    if (TryParseDay(property.Name, out var dayUtc))
                    {
                        var count = TryReadCount(property.Value);
                        results.Add(new KillmailRemoteDayInfo
                        {
                            DayUtc = dayUtc,
                            RemoteTotalCount = count
                        });
                    }
                }
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var dayUtc =
                        TryReadString(item, "day") ??
                        TryReadString(item, "date") ??
                        TryReadString(item, "day_utc");

                    if (!TryParseDay(dayUtc, out var normalizedDayUtc))
                    {
                        continue;
                    }

                    results.Add(new KillmailRemoteDayInfo
                    {
                        DayUtc = normalizedDayUtc,
                        RemoteTotalCount = TryReadCount(item)
                    });
                }
            }

            return results
                .Where(x => !string.IsNullOrWhiteSpace(x.DayUtc))
                .OrderBy(x => x.DayUtc)
                .ToList();
        }

        private static bool TryParseDay(string? input, out string normalizedDayUtc)
        {
            normalizedDayUtc = "";

            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            if (!DateTime.TryParse(
                input,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
            {
                return false;
            }

            normalizedDayUtc = parsed.ToString("yyyy-MM-dd");
            return true;
        }

        private static int TryReadCount(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var directNumber))
            {
                return directNumber;
            }

            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var name in new[] { "count", "total", "kills", "killmails" })
                {
                    if (element.TryGetProperty(name, out var value))
                    {
                        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue))
                        {
                            return intValue;
                        }

                        if (value.ValueKind == JsonValueKind.String &&
                            int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                        {
                            return parsed;
                        }
                    }
                }
            }

            return 0;
        }

        private static string? TryReadString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
    }
}