using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using PitmastersGrill.Providers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace PitmastersGrill.Services
{
    public sealed class KillmailDerivedObservationParser
    {
        public KillmailDerivedParseResult? ParseKillmailEntry(string jsonContent)
        {
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                return null;
            }

            using var document = JsonDocument.Parse(jsonContent);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var killmailTimeUtc = TryReadDateTime(root, "killmail_time");
            if (!killmailTimeUtc.HasValue)
            {
                return null;
            }

            var killmailTimeText = killmailTimeUtc.Value.ToString("o");
            var killmailId = TryReadLongAsString(root, "killmail_id");
            var dayUtc = killmailTimeUtc.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            var registryPilots = new Dictionary<string, KillmailRegistryPilotSeen>(StringComparer.OrdinalIgnoreCase);
            var fleetPilots = new Dictionary<string, KillmailFleetPilotSeen>(StringComparer.OrdinalIgnoreCase);
            var shipPilots = new Dictionary<string, KillmailShipPilotSeen>(StringComparer.OrdinalIgnoreCase);
            var cynoModuleObservations = new List<PilotCynoModuleObservationDayRecord>();
            var baitObservations = new List<PilotBaitObservationDayRecord>();
            var cynoTackleObservations = new List<PilotCynoTackleObservationDayRecord>();

            var attackerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var attackerShipUpdates = new List<(string CharacterId, int? ShipTypeId)>();

            JsonElement attackersElement = default;
            var hasAttackers = root.TryGetProperty("attackers", out attackersElement) &&
                               attackersElement.ValueKind == JsonValueKind.Array;

            if (hasAttackers)
            {
                foreach (var attacker in attackersElement.EnumerateArray())
                {
                    var attackerCharacterId = TryReadLongAsString(attacker, "character_id");
                    if (string.IsNullOrWhiteSpace(attackerCharacterId))
                    {
                        continue;
                    }

                    attackerIds.Add(attackerCharacterId);
                    attackerShipUpdates.Add((attackerCharacterId, TryReadInt(attacker, "ship_type_id")));

                    registryPilots[attackerCharacterId] = new KillmailRegistryPilotSeen
                    {
                        CharacterId = attackerCharacterId,
                        FirstSeenKillmailTimeUtc = killmailTimeText,
                        LastSeenKillmailTimeUtc = killmailTimeText
                    };
                }
            }

            var playerAttackerCount = attackerIds.Count;
            foreach (var attackerId in attackerIds)
            {
                fleetPilots[attackerId] = new KillmailFleetPilotSeen
                {
                    CharacterId = attackerId,
                    AttackerCountForThisKillmail = playerAttackerCount
                };
            }

            foreach (var shipUpdate in attackerShipUpdates)
            {
                shipPilots[shipUpdate.CharacterId] = new KillmailShipPilotSeen
                {
                    CharacterId = shipUpdate.CharacterId,
                    LastSeenShipTypeId = shipUpdate.ShipTypeId,
                    LastSeenShipTimeUtc = killmailTimeText
                };
            }

            if (root.TryGetProperty("victim", out var victim) && victim.ValueKind == JsonValueKind.Object)
            {
                var victimCharacterId = TryReadLongAsString(victim, "character_id");
                if (!string.IsNullOrWhiteSpace(victimCharacterId))
                {
                    var victimShipTypeId = TryReadInt(victim, "ship_type_id");
                    registryPilots[victimCharacterId] = new KillmailRegistryPilotSeen
                    {
                        CharacterId = victimCharacterId,
                        FirstSeenKillmailTimeUtc = killmailTimeText,
                        LastSeenKillmailTimeUtc = killmailTimeText
                    };

                    shipPilots[victimCharacterId] = new KillmailShipPilotSeen
                    {
                        CharacterId = victimCharacterId,
                        LastSeenShipTypeId = victimShipTypeId,
                        LastSeenShipTimeUtc = killmailTimeText
                    };

                    if (victim.TryGetProperty("items", out var victimItems) &&
                        victimItems.ValueKind == JsonValueKind.Array)
                    {
                        var victimShipName = TryReadString(victim, "ship_name") ??
                                             TryReadString(victim, "ship_type_name") ??
                                             TryReadString(victim, "type_name") ??
                                             "";
                        var solarSystemId = TryReadInt(root, "solar_system_id");
                        var solarSystemName = TryReadString(root, "solar_system_name") ?? "";

                        ScanVictimItemsForDerivedIntel(
                            victimItems,
                            victimCharacterId,
                            killmailId,
                            killmailTimeText,
                            victimShipTypeId,
                            victimShipName,
                            solarSystemId,
                            solarSystemName,
                            cynoModuleObservations,
                            baitObservations,
                            cynoTackleObservations);
                    }
                }
            }

            return new KillmailDerivedParseResult
            {
                KillmailId = killmailId,
                KillmailTimeUtc = killmailTimeText,
                DayUtc = dayUtc,
                RegistryPilots = new List<KillmailRegistryPilotSeen>(registryPilots.Values),
                FleetPilots = new List<KillmailFleetPilotSeen>(fleetPilots.Values),
                ShipPilots = new List<KillmailShipPilotSeen>(shipPilots.Values),
                CynoModuleObservations = cynoModuleObservations,
                BaitObservations = baitObservations,
                CynoTackleObservations = cynoTackleObservations
            };
        }

        private static void ScanVictimItemsForDerivedIntel(
            JsonElement items,
            string victimCharacterId,
            string killmailId,
            string killmailTimeUtc,
            int? victimShipTypeId,
            string victimShipName,
            int? solarSystemId,
            string solarSystemName,
            List<PilotCynoModuleObservationDayRecord> cynoModuleObservations,
            List<PilotBaitObservationDayRecord> baitObservations,
            List<PilotCynoTackleObservationDayRecord> cynoTackleObservations)
        {
            var foundModules = new List<VictimItemModule>();
            CollectVictimItemModules(items, foundModules);

            foreach (var module in foundModules.Where(x => x.CynoSignalType != CynoSignalType.Unknown))
            {
                cynoModuleObservations.Add(new PilotCynoModuleObservationDayRecord
                {
                    CharacterId = victimCharacterId,
                    KillmailId = killmailId,
                    KillmailTimeUtc = killmailTimeUtc,
                    VictimShipTypeId = victimShipTypeId,
                    ModuleTypeId = module.TypeId,
                    ModuleName = module.ModuleName,
                    QuantityDestroyed = module.QuantityDestroyed,
                    QuantityDropped = module.QuantityDropped,
                    ItemState = GetItemState(module.QuantityDestroyed, module.QuantityDropped),
                    Source = "public loss victim item list"
                });
            }

            var industrialCynos = foundModules
                .Where(x => x.TypeId == CynoSignalAnalyzer.IndustrialCynoModuleTypeId)
                .ToList();
            var tackleModules = foundModules
                .Where(x => x.TackleType != TackleModuleType.UnknownTackle)
                .ToList();

            if (tackleModules.Count > 0 &&
                CynoShipCatalog.TryGetCynoShipNameByTypeId(victimShipTypeId, out var cynoHullName))
            {
                var displayedVictimShipName = string.IsNullOrWhiteSpace(victimShipName)
                    ? cynoHullName
                    : victimShipName;

                foreach (var tackle in tackleModules)
                {
                    cynoTackleObservations.Add(new PilotCynoTackleObservationDayRecord
                    {
                        CharacterId = victimCharacterId,
                        KillmailId = killmailId,
                        KillmailTimeUtc = killmailTimeUtc,
                        VictimShipTypeId = victimShipTypeId,
                        VictimShipName = displayedVictimShipName,
                        TackleModuleTypeId = tackle.TypeId,
                        TackleModuleName = tackle.ModuleName,
                        TackleType = tackle.TackleType,
                        QuantityDestroyed = tackle.QuantityDestroyed,
                        QuantityDropped = tackle.QuantityDropped,
                        Source = "public loss victim item list"
                    });
                }
            }

            if (industrialCynos.Count == 0 || tackleModules.Count == 0)
            {
                return;
            }

            var industrialCyno = industrialCynos[0];
            foreach (var tackle in tackleModules)
            {
                baitObservations.Add(new PilotBaitObservationDayRecord
                {
                    CharacterId = victimCharacterId,
                    KillmailId = killmailId,
                    KillmailTimeUtc = killmailTimeUtc,
                    VictimShipTypeId = victimShipTypeId,
                    VictimShipName = victimShipName,
                    SolarSystemId = solarSystemId,
                    SolarSystemName = solarSystemName,
                    IndustrialCynoModuleTypeId = industrialCyno.TypeId,
                    IndustrialCynoModuleName = industrialCyno.ModuleName,
                    TackleModuleTypeId = tackle.TypeId,
                    TackleModuleName = tackle.ModuleName,
                    TackleType = tackle.TackleType,
                    QuantityDestroyed = tackle.QuantityDestroyed,
                    QuantityDropped = tackle.QuantityDropped,
                    Source = "public loss victim item list"
                });
            }
        }

        private static void CollectVictimItemModules(JsonElement items, List<VictimItemModule> modules)
        {
            if (items.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var typeId = TryReadInt(item, "item_type_id") ?? TryReadInt(item, "type_id");
                var itemName = TryReadString(item, "type_name") ??
                               TryReadString(item, "item_name") ??
                               TryReadString(item, "name") ??
                               "";

                var signalType = CynoSignalType.Unknown;
                var moduleName = itemName;
                if (typeId.HasValue &&
                    CynoSignalAnalyzer.TryGetModuleSignalType(typeId.Value, out signalType, out var knownModuleName))
                {
                    moduleName = knownModuleName;
                }

                var tackleType = TackleModuleType.UnknownTackle;
                if (typeId.HasValue && TryGetKnownTackleModule(typeId.Value, out tackleType, out var knownTackleName))
                {
                    moduleName = knownTackleName;
                }
                else if (TryGetTackleTypeFromName(itemName, out tackleType))
                {
                    moduleName = itemName;
                    AppLogger.DatabaseInfo(
                        $"Tackle module detected by item-name fallback during victim item scan. item_type_id={typeId?.ToString(CultureInfo.InvariantCulture) ?? ""} item_name='{itemName}' tackle_type={tackleType}");
                }

                if (typeId.HasValue && (signalType != CynoSignalType.Unknown || tackleType != TackleModuleType.UnknownTackle))
                {
                    modules.Add(new VictimItemModule
                    {
                        TypeId = typeId.Value,
                        ModuleName = string.IsNullOrWhiteSpace(moduleName)
                            ? $"type_id {typeId.Value.ToString(CultureInfo.InvariantCulture)}"
                            : moduleName,
                        CynoSignalType = signalType,
                        TackleType = tackleType,
                        QuantityDestroyed = TryReadInt(item, "quantity_destroyed") ?? 0,
                        QuantityDropped = TryReadInt(item, "quantity_dropped") ?? 0
                    });
                }

                if (item.TryGetProperty("items", out var nestedItems) &&
                    nestedItems.ValueKind == JsonValueKind.Array)
                {
                    CollectVictimItemModules(nestedItems, modules);
                }
            }
        }

        private static bool TryGetKnownTackleModule(int typeId, out TackleModuleType tackleType, out string moduleName)
        {
            return TackleModuleCatalog.TryGetKnownTackleModule(typeId, out tackleType, out moduleName);
        }

        private static bool TryGetTackleTypeFromName(string itemName, out TackleModuleType tackleType)
        {
            tackleType = TackleModuleType.UnknownTackle;
            if (string.IsNullOrWhiteSpace(itemName))
            {
                return false;
            }

            if (itemName.Contains("Warp Scrambler", StringComparison.OrdinalIgnoreCase))
            {
                tackleType = TackleModuleType.WarpScrambler;
                return true;
            }

            if (itemName.Contains("Warp Disruptor", StringComparison.OrdinalIgnoreCase))
            {
                tackleType = TackleModuleType.WarpDisruptor;
                return true;
            }

            return false;
        }

        private static string GetItemState(int quantityDestroyed, int quantityDropped)
        {
            if (quantityDestroyed > 0 && quantityDropped > 0)
            {
                return "destroyed/dropped";
            }

            if (quantityDestroyed > 0)
            {
                return "destroyed";
            }

            if (quantityDropped > 0)
            {
                return "dropped";
            }

            return "fitted/unknown";
        }

        private static int? TryReadInt(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue))
            {
                return intValue;
            }

            if (value.ValueKind == JsonValueKind.String &&
                int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static long? TryReadLong(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var longValue))
            {
                return longValue;
            }

            if (value.ValueKind == JsonValueKind.String &&
                long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static string TryReadLongAsString(JsonElement element, string propertyName)
        {
            var longValue = TryReadLong(element, propertyName);
            return longValue?.ToString(CultureInfo.InvariantCulture) ?? "";
        }

        private static string? TryReadString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value) ||
                value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return value.GetString();
        }

        private static DateTime? TryReadDateTime(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            if (value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            if (DateTime.TryParse(
                value.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private sealed class VictimItemModule
        {
            public int TypeId { get; set; }
            public string ModuleName { get; set; } = "";
            public CynoSignalType CynoSignalType { get; set; } = CynoSignalType.Unknown;
            public TackleModuleType TackleType { get; set; } = TackleModuleType.UnknownTackle;
            public int QuantityDestroyed { get; set; }
            public int QuantityDropped { get; set; }
        }
    }
}
