using PitmastersLittleGrill.Models;
using PitmastersLittleGrill.Persistence;
using PitmastersLittleGrill.Providers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersLittleGrill.Services
{
    public class StatsService
    {
        private readonly StatsCacheRepository _statsCacheRepository;
        private readonly ZkillStatsProvider _zkillStatsProvider;
        private readonly FleetObservationReadService _fleetObservationReadService;
        private readonly PilotShipObservationDayRepository _pilotShipObservationDayRepository;
        private readonly ShipTypeNameCacheRepository _shipTypeNameCacheRepository;
        private readonly EsiShipTypeProvider _esiShipTypeProvider;

        public StatsService(
            StatsCacheRepository statsCacheRepository,
            ZkillStatsProvider zkillStatsProvider)
        {
            _statsCacheRepository = statsCacheRepository;
            _zkillStatsProvider = zkillStatsProvider;

            var killmailDbPath = KillmailPaths.GetKillmailDatabasePath();
            var fleetObservationRepo = new PilotFleetObservationDayRepository(killmailDbPath);

            _fleetObservationReadService = new FleetObservationReadService(fleetObservationRepo);
            _pilotShipObservationDayRepository = new PilotShipObservationDayRepository(killmailDbPath);
            _shipTypeNameCacheRepository = new ShipTypeNameCacheRepository(killmailDbPath);
            _esiShipTypeProvider = new EsiShipTypeProvider();
        }

        public Dictionary<string, StatsCacheEntry> GetCachedForResolvedRows(
            Dictionary<string, ResolverCacheEntry> identities)
        {
            var results = new Dictionary<string, StatsCacheEntry>(StringComparer.OrdinalIgnoreCase);

            if (identities == null || identities.Count == 0)
            {
                return results;
            }

            var characterIds = new List<string>();

            foreach (var pair in identities)
            {
                var identity = pair.Value;

                if (identity == null || string.IsNullOrWhiteSpace(identity.CharacterId))
                {
                    continue;
                }

                characterIds.Add(identity.CharacterId);
            }

            var localFleetAggregates = _fleetObservationReadService.GetAggregatesByCharacterIds(characterIds);
            var localCynoAggregates = _pilotShipObservationDayRepository.GetLatestCynoByCharacterIds(characterIds);

            foreach (var pair in identities)
            {
                var identity = pair.Value;

                if (identity == null || string.IsNullOrWhiteSpace(identity.CharacterId))
                {
                    continue;
                }

                var cached = _statsCacheRepository.GetByCharacterId(identity.CharacterId);

                if (cached == null || !IsFresh(cached.ExpiresAtUtc))
                {
                    continue;
                }

                localFleetAggregates.TryGetValue(identity.CharacterId, out var localFleetAggregate);
                localCynoAggregates.TryGetValue(identity.CharacterId, out var localCynoAggregate);

                ApplyLocalDerivedFieldsFromCacheOnly(
                    cached,
                    localFleetAggregate,
                    localCynoAggregate,
                    identity.CharacterId);

                results[pair.Key] = cached;
            }

            return results;
        }

        public async Task<ProviderOutcome<StatsCacheEntry>> ResolveSingleAsync(
            string characterId,
            StatsCacheEntry? existingStats,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return ProviderOutcome<StatsCacheEntry>.PermanentFailure(
                    "stats_service",
                    "Character id was empty");
            }

            var localFleetAggregates = _fleetObservationReadService.GetAggregatesByCharacterIds(new[] { characterId });
            localFleetAggregates.TryGetValue(characterId, out var localFleetAggregate);

            var localCynoAggregates = _pilotShipObservationDayRepository.GetLatestCynoByCharacterIds(new[] { characterId });
            localCynoAggregates.TryGetValue(characterId, out var localCynoAggregate);

            if (existingStats != null && IsFresh(existingStats.ExpiresAtUtc))
            {
                await ApplyLocalDerivedFieldsAsync(
                    existingStats,
                    localFleetAggregate,
                    localCynoAggregate,
                    characterId,
                    cancellationToken);

                return ProviderOutcome<StatsCacheEntry>.Success(
                    existingStats,
                    "stats_existing",
                    "Fresh stats already available on the row");
            }

            var cached = _statsCacheRepository.GetByCharacterId(characterId);

            if (cached != null && IsFresh(cached.ExpiresAtUtc))
            {
                await ApplyLocalDerivedFieldsAsync(
                    cached,
                    localFleetAggregate,
                    localCynoAggregate,
                    characterId,
                    cancellationToken);

                _statsCacheRepository.Upsert(cached);

                return ProviderOutcome<StatsCacheEntry>.Success(
                    cached,
                    "stats_cache",
                    "Fresh stats cache hit");
            }

            var fetchedOutcome = await _zkillStatsProvider.TryGetStatsAsync(characterId, cancellationToken);

            if (fetchedOutcome.Kind == ProviderOutcomeKind.Success && fetchedOutcome.Value != null)
            {
                await ApplyLocalDerivedFieldsAsync(
                    fetchedOutcome.Value,
                    localFleetAggregate,
                    localCynoAggregate,
                    characterId,
                    cancellationToken);

                _statsCacheRepository.Upsert(fetchedOutcome.Value);
                return fetchedOutcome;
            }

            var fallback = cached ?? existingStats;

            if (fallback != null)
            {
                await ApplyLocalDerivedFieldsAsync(
                    fallback,
                    localFleetAggregate,
                    localCynoAggregate,
                    characterId,
                    cancellationToken);
            }

            if (fetchedOutcome.Kind == ProviderOutcomeKind.NotFound)
            {
                return ProviderOutcome<StatsCacheEntry>.NotFound(
                    fetchedOutcome.ProviderName,
                    string.IsNullOrWhiteSpace(fetchedOutcome.Detail)
                        ? "Stats were not found from zKill"
                        : fetchedOutcome.Detail,
                    fallback);
            }

            if (fetchedOutcome.Kind == ProviderOutcomeKind.Throttled)
            {
                return ProviderOutcome<StatsCacheEntry>.Throttled(
                    fetchedOutcome.ProviderName,
                    fetchedOutcome.Detail,
                    fetchedOutcome.RetryAfterUtc,
                    fallback);
            }

            if (fetchedOutcome.Kind == ProviderOutcomeKind.TemporaryFailure)
            {
                return ProviderOutcome<StatsCacheEntry>.TemporaryFailure(
                    fetchedOutcome.ProviderName,
                    fetchedOutcome.Detail,
                    fetchedOutcome.RetryAfterUtc,
                    fallback);
            }

            if (fetchedOutcome.Kind == ProviderOutcomeKind.PermanentFailure)
            {
                return ProviderOutcome<StatsCacheEntry>.PermanentFailure(
                    fetchedOutcome.ProviderName,
                    fetchedOutcome.Detail,
                    fallback);
            }

            return ProviderOutcome<StatsCacheEntry>.TemporaryFailure(
                "stats_service",
                "Stats resolution returned an unknown outcome",
                value: fallback);
        }

        private void ApplyLocalDerivedFieldsFromCacheOnly(
            StatsCacheEntry target,
            PilotFleetObservationAggregate? localFleetAggregate,
            PilotCynoObservationAggregate? localCynoAggregate,
            string characterId)
        {
            if (target == null)
            {
                return;
            }

            ApplyFleetAggregate(target, localFleetAggregate);
            ApplyCynoAggregate(target, localCynoAggregate);
            ApplyLastShipFromHistoryCacheOnly(target, characterId);
        }

        private async Task ApplyLocalDerivedFieldsAsync(
            StatsCacheEntry target,
            PilotFleetObservationAggregate? localFleetAggregate,
            PilotCynoObservationAggregate? localCynoAggregate,
            string characterId,
            CancellationToken cancellationToken)
        {
            if (target == null)
            {
                return;
            }

            ApplyFleetAggregate(target, localFleetAggregate);
            ApplyCynoAggregate(target, localCynoAggregate);
            await ApplyLastShipFromHistoryAsync(target, characterId, cancellationToken);
        }

        private static void ApplyFleetAggregate(
            StatsCacheEntry target,
            PilotFleetObservationAggregate? localFleetAggregate)
        {
            if (localFleetAggregate != null)
            {
                target.AvgAttackersWhenAttacking = localFleetAggregate.GetAverageFleetSize() ?? 0;
            }
            else
            {
                target.AvgAttackersWhenAttacking = 0;
            }
        }

        private static void ApplyCynoAggregate(
            StatsCacheEntry target,
            PilotCynoObservationAggregate? localCynoAggregate)
        {
            if (localCynoAggregate != null && !string.IsNullOrWhiteSpace(localCynoAggregate.LastSeenCynoShipName))
            {
                target.LastPublicCynoCapableHull = localCynoAggregate.LastSeenCynoShipName;
            }
            else
            {
                target.LastPublicCynoCapableHull = "";
            }
        }

        private void ApplyLastShipFromHistoryCacheOnly(StatsCacheEntry target, string characterId)
        {
            var history = _pilotShipObservationDayRepository.GetRecentShipSeenHistoryByCharacterId(characterId);

            foreach (var observation in history)
            {
                if (!observation.LastSeenShipTypeId.HasValue ||
                    string.IsNullOrWhiteSpace(observation.LastSeenShipTimeUtc))
                {
                    continue;
                }

                var cachedInfo = _shipTypeNameCacheRepository.GetTypeInfo(observation.LastSeenShipTypeId.Value);

                if (cachedInfo.IsActualShip == true && !string.IsNullOrWhiteSpace(cachedInfo.TypeName))
                {
                    SetLastShipObservation(target, cachedInfo.TypeName!, observation.LastSeenShipTimeUtc);
                    return;
                }

                if (cachedInfo.IsActualShip == false)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(cachedInfo.TypeName) &&
                    IsDefinitelyNonShipName(cachedInfo.TypeName))
                {
                    _shipTypeNameCacheRepository.Upsert(
                        observation.LastSeenShipTypeId.Value,
                        cachedInfo.TypeName,
                        false);
                    continue;
                }
            }

            ClearLastShipObservation(target);
        }

        private async Task ApplyLastShipFromHistoryAsync(
            StatsCacheEntry target,
            string characterId,
            CancellationToken cancellationToken)
        {
            var history = _pilotShipObservationDayRepository.GetRecentShipSeenHistoryByCharacterId(characterId);

            foreach (var observation in history)
            {
                if (!observation.LastSeenShipTypeId.HasValue ||
                    string.IsNullOrWhiteSpace(observation.LastSeenShipTimeUtc))
                {
                    continue;
                }

                var typeId = observation.LastSeenShipTypeId.Value;
                var cachedInfo = _shipTypeNameCacheRepository.GetTypeInfo(typeId);

                if (cachedInfo.IsActualShip == true && !string.IsNullOrWhiteSpace(cachedInfo.TypeName))
                {
                    SetLastShipObservation(target, cachedInfo.TypeName!, observation.LastSeenShipTimeUtc);
                    return;
                }

                if (cachedInfo.IsActualShip == false)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(cachedInfo.TypeName) &&
                    IsDefinitelyNonShipName(cachedInfo.TypeName))
                {
                    _shipTypeNameCacheRepository.Upsert(typeId, cachedInfo.TypeName, false);
                    continue;
                }

                var resolvedInfo = await _esiShipTypeProvider.TryGetShipTypeInfoAsync(typeId, cancellationToken);

                if (resolvedInfo == null || string.IsNullOrWhiteSpace(resolvedInfo.TypeName))
                {
                    continue;
                }

                _shipTypeNameCacheRepository.Upsert(typeId, resolvedInfo.TypeName, resolvedInfo.IsActualShip);

                if (resolvedInfo.IsActualShip == true)
                {
                    SetLastShipObservation(target, resolvedInfo.TypeName, observation.LastSeenShipTimeUtc);
                    return;
                }

                if (resolvedInfo.IsActualShip == false)
                {
                    continue;
                }

                if (IsDefinitelyNonShipName(resolvedInfo.TypeName))
                {
                    _shipTypeNameCacheRepository.Upsert(typeId, resolvedInfo.TypeName, false);
                    continue;
                }
            }

            ClearLastShipObservation(target);
        }

        private static void SetLastShipObservation(StatsCacheEntry target, string typeName, string seenAtUtc)
        {
            target.LastShipSeenName = typeName;
            target.LastShipSeenAtUtc = seenAtUtc;
        }

        private static void ClearLastShipObservation(StatsCacheEntry target)
        {
            target.LastShipSeenName = "";
            target.LastShipSeenAtUtc = "";
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
                || name.Contains("Siphon", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFresh(string expiresAtUtc)
        {
            if (string.IsNullOrWhiteSpace(expiresAtUtc))
            {
                return false;
            }

            if (!DateTime.TryParse(
                expiresAtUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
            {
                return false;
            }

            return parsed > DateTime.UtcNow;
        }
    }
}