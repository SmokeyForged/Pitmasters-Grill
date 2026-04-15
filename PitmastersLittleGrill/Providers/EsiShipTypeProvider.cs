using PitmastersLittleGrill.Persistence;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersLittleGrill.Providers
{
    public class EsiShipTypeInfo
    {
        public int TypeId { get; set; }
        public string TypeName { get; set; } = "";
        public int? GroupId { get; set; }
        public int? CategoryId { get; set; }
        public bool? IsActualShip { get; set; }
    }

    public class EsiShipTypeProvider
    {
        private const int ShipCategoryId = 6;

        private readonly HttpClient _httpClient;
        private readonly object _cacheSync = new();
        private readonly Dictionary<int, EsiShipTypeInfo> _memoryCache = new();

        public EsiShipTypeProvider()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20)
            };

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PitmastersGrill/0.5.x");
        }

        public async Task<string?> TryGetShipTypeNameAsync(int shipTypeId, CancellationToken cancellationToken = default)
        {
            var info = await TryGetShipTypeInfoAsync(shipTypeId, cancellationToken);
            return info?.TypeName;
        }

        public async Task<EsiShipTypeInfo?> TryGetShipTypeInfoAsync(int shipTypeId, CancellationToken cancellationToken = default)
        {
            if (shipTypeId <= 0)
            {
                return null;
            }

            lock (_cacheSync)
            {
                if (_memoryCache.TryGetValue(shipTypeId, out var cached))
                {
                    return cached;
                }
            }

            var typeUrl = $"https://esi.evetech.net/latest/universe/types/{shipTypeId}/?datasource=tranquility";

            try
            {
                using var typeResponse = await _httpClient.GetAsync(typeUrl, cancellationToken);
                if (!typeResponse.IsSuccessStatusCode)
                {
                    DebugTraceWriter.WriteLine(
                        $"esi ship type lookup failed: shipTypeId={shipTypeId}, status={(int)typeResponse.StatusCode}");
                    return null;
                }

                await using var typeStream = await typeResponse.Content.ReadAsStreamAsync(cancellationToken);
                using var typeDocument = await JsonDocument.ParseAsync(typeStream, cancellationToken: cancellationToken);

                var typeRoot = typeDocument.RootElement;

                if (!typeRoot.TryGetProperty("name", out var nameElement))
                {
                    return null;
                }

                var typeName = nameElement.GetString();
                if (string.IsNullOrWhiteSpace(typeName))
                {
                    return null;
                }

                int? groupId = null;
                if (typeRoot.TryGetProperty("group_id", out var groupElement) &&
                    groupElement.ValueKind == JsonValueKind.Number &&
                    groupElement.TryGetInt32(out var parsedGroupId))
                {
                    groupId = parsedGroupId;
                }

                int? categoryId = null;
                if (groupId.HasValue)
                {
                    categoryId = await TryGetCategoryIdForGroupAsync(groupId.Value, cancellationToken);
                }

                bool? isActualShip = null;

                if (categoryId.HasValue)
                {
                    isActualShip = categoryId.Value == ShipCategoryId;
                }
                else if (IsDefinitelyNonShipName(typeName))
                {
                    isActualShip = false;
                }

                var result = new EsiShipTypeInfo
                {
                    TypeId = shipTypeId,
                    TypeName = typeName.Trim(),
                    GroupId = groupId,
                    CategoryId = categoryId,
                    IsActualShip = isActualShip
                };

                lock (_cacheSync)
                {
                    _memoryCache[shipTypeId] = result;
                }

                return result;
            }
            catch (Exception ex)
            {
                DebugTraceWriter.WriteLine(
                    $"esi ship type lookup exception: shipTypeId={shipTypeId}, error={ex.Message}");
                return null;
            }
        }

        private async Task<int?> TryGetCategoryIdForGroupAsync(int groupId, CancellationToken cancellationToken)
        {
            if (groupId <= 0)
            {
                return null;
            }

            var groupUrl = $"https://esi.evetech.net/latest/universe/groups/{groupId}/?datasource=tranquility";

            try
            {
                using var groupResponse = await _httpClient.GetAsync(groupUrl, cancellationToken);
                if (!groupResponse.IsSuccessStatusCode)
                {
                    DebugTraceWriter.WriteLine(
                        $"esi group lookup failed: groupId={groupId}, status={(int)groupResponse.StatusCode}");
                    return null;
                }

                await using var groupStream = await groupResponse.Content.ReadAsStreamAsync(cancellationToken);
                using var groupDocument = await JsonDocument.ParseAsync(groupStream, cancellationToken: cancellationToken);

                var groupRoot = groupDocument.RootElement;
                if (!groupRoot.TryGetProperty("category_id", out var categoryElement))
                {
                    return null;
                }

                if (categoryElement.ValueKind == JsonValueKind.Number &&
                    categoryElement.TryGetInt32(out var categoryId))
                {
                    return categoryId;
                }

                return null;
            }
            catch (Exception ex)
            {
                DebugTraceWriter.WriteLine(
                    $"esi group lookup exception: groupId={groupId}, error={ex.Message}");
                return null;
            }
        }

        private static bool IsDefinitelyNonShipName(string? typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return false;
            }

            var name = typeName.Trim();

            return name.Contains("Mobile ", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Container", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Tractor Unit", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Depot", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Cynosural Beacon", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Structure", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Control Tower", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Warp Disrupt Probe", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Scanner Probe", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Cargo Rig", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Siphon", StringComparison.OrdinalIgnoreCase);
        }
    }
}