using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using PitmastersGrill.Providers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersGrill.Services
{
    public class ResolverService
    {
        private readonly ResolverCacheRepository _resolverCacheRepository;
        private readonly ZkillSearchResolverProvider _zkillSearchResolverProvider;
        private readonly EsiExactNameResolverProvider _esiExactNameResolverProvider;
        private readonly EsiPublicAffiliationProvider _esiPublicAffiliationProvider;

        public ResolverService(
            ResolverCacheRepository resolverCacheRepository,
            ZkillSearchResolverProvider zkillSearchResolverProvider,
            EsiExactNameResolverProvider esiExactNameResolverProvider,
            EsiPublicAffiliationProvider esiPublicAffiliationProvider)
        {
            _resolverCacheRepository = resolverCacheRepository;
            _zkillSearchResolverProvider = zkillSearchResolverProvider;
            _esiExactNameResolverProvider = esiExactNameResolverProvider;
            _esiPublicAffiliationProvider = esiPublicAffiliationProvider;
        }

        public Dictionary<string, ResolverCacheEntry> GetCached(List<string> characterNames)
        {
            var results = new Dictionary<string, ResolverCacheEntry>(StringComparer.OrdinalIgnoreCase);

            if (characterNames == null || characterNames.Count == 0)
            {
                return results;
            }

            foreach (var characterName in characterNames)
            {
                if (string.IsNullOrWhiteSpace(characterName))
                {
                    continue;
                }

                var trimmedName = characterName.Trim();
                var cached = TryGetCachedByCharacterName(trimmedName);
                if (cached == null)
                {
                    continue;
                }

                if (!IsFresh(cached.ExpiresAtUtc))
                {
                    continue;
                }

                results[trimmedName] = cached;
            }

            return results;
        }

        public async Task<ProviderOutcome<ResolverCacheEntry>> ResolveCharacterAsync(
            string characterName,
            ResolverCacheEntry? existingIdentity,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return ProviderOutcome<ResolverCacheEntry>.PermanentFailure(
                    "resolver_service",
                    "Character name was empty");
            }

            var trimmedName = characterName.Trim();
            var nowUtc = DateTime.UtcNow;

            if (existingIdentity != null && IsFresh(existingIdentity.ExpiresAtUtc))
            {
                var promotedExisting = await TryPromoteNotFoundViaEsiExactFallbackAsync(
                    trimmedName,
                    existingIdentity,
                    cancellationToken);

                if (promotedExisting != null)
                {
                    return ProviderOutcome<ResolverCacheEntry>.Success(
                        promotedExisting,
                        "esi_exact_name",
                        "Promoted cached terminal miss via ESI exact fallback");
                }

                if (string.Equals(existingIdentity.ResolverConfidence, "not_found", StringComparison.OrdinalIgnoreCase))
                {
                    return ProviderOutcome<ResolverCacheEntry>.NotFound(
                        "resolver_cache",
                        "Terminal miss cached",
                        existingIdentity);
                }

                return ProviderOutcome<ResolverCacheEntry>.Success(
                    existingIdentity,
                    "resolver_cache",
                    "Fresh resolver identity already available");
            }

            var cached = TryGetCachedByCharacterName(trimmedName);
            if (cached != null && IsFresh(cached.ExpiresAtUtc))
            {
                var promotedCached = await TryPromoteNotFoundViaEsiExactFallbackAsync(
                    trimmedName,
                    cached,
                    cancellationToken);

                if (promotedCached != null)
                {
                    return ProviderOutcome<ResolverCacheEntry>.Success(
                        promotedCached,
                        "esi_exact_name",
                        "Promoted cached terminal miss via ESI exact fallback");
                }

                if (string.Equals(cached.ResolverConfidence, "not_found", StringComparison.OrdinalIgnoreCase))
                {
                    return ProviderOutcome<ResolverCacheEntry>.NotFound(
                        "resolver_cache",
                        "Terminal miss cached",
                        cached);
                }

                return ProviderOutcome<ResolverCacheEntry>.Success(
                    cached,
                    "resolver_cache",
                    "Fresh resolver cache hit");
            }

            var resolvedFromZkill = await _zkillSearchResolverProvider.TryResolveCharacterAsync(
                trimmedName,
                cancellationToken);

            if (resolvedFromZkill.Kind == ProviderOutcomeKind.Success && resolvedFromZkill.Value != null)
            {
                TryUpsert(resolvedFromZkill.Value);
                return resolvedFromZkill;
            }

            if (resolvedFromZkill.Kind == ProviderOutcomeKind.NotFound)
            {
                var promotedZkillMiss = await TryPromoteToEsiExactFallbackAsync(
                    trimmedName,
                    cancellationToken);

                if (promotedZkillMiss != null)
                {
                    return ProviderOutcome<ResolverCacheEntry>.Success(
                        promotedZkillMiss,
                        "esi_exact_name",
                        "Promoted zKill terminal miss via ESI exact fallback");
                }

                var terminalMiss = new ResolverCacheEntry
                {
                    CharacterName = trimmedName,
                    CharacterId = "",
                    AllianceId = "",
                    AllianceName = "",
                    AllianceTicker = "",
                    CorpName = "",
                    CorpTicker = "",
                    ResolverConfidence = "not_found",
                    ResolvedAtUtc = nowUtc.ToString("o"),
                    ExpiresAtUtc = nowUtc.AddHours(12).ToString("o"),
                    AffiliationCheckedAtUtc = nowUtc.ToString("o")
                };

                TryUpsert(terminalMiss);

                return ProviderOutcome<ResolverCacheEntry>.NotFound(
                    "zkill_search",
                    "Terminal miss cached after zKill search and no ESI exact fallback promotion",
                    terminalMiss);
            }

            var fallbackIdentity = cached ?? existingIdentity;

            if (resolvedFromZkill.Kind == ProviderOutcomeKind.Throttled)
            {
                return ProviderOutcome<ResolverCacheEntry>.Throttled(
                    resolvedFromZkill.ProviderName,
                    resolvedFromZkill.Detail,
                    resolvedFromZkill.RetryAfterUtc,
                    fallbackIdentity);
            }

            if (resolvedFromZkill.Kind == ProviderOutcomeKind.TemporaryFailure)
            {
                return ProviderOutcome<ResolverCacheEntry>.TemporaryFailure(
                    resolvedFromZkill.ProviderName,
                    resolvedFromZkill.Detail,
                    resolvedFromZkill.RetryAfterUtc,
                    fallbackIdentity);
            }

            if (resolvedFromZkill.Kind == ProviderOutcomeKind.PermanentFailure)
            {
                return ProviderOutcome<ResolverCacheEntry>.PermanentFailure(
                    resolvedFromZkill.ProviderName,
                    resolvedFromZkill.Detail,
                    fallbackIdentity);
            }

            return ProviderOutcome<ResolverCacheEntry>.TemporaryFailure(
                "resolver_service",
                "Resolver returned an unknown outcome",
                value: fallbackIdentity);
        }

        public async Task<ProviderOutcome<ResolverCacheEntry>> EnrichAffiliationIfNeededAsync(
            ResolverCacheEntry? resolvedCharacter,
            CancellationToken cancellationToken = default)
        {
            if (resolvedCharacter == null)
            {
                return ProviderOutcome<ResolverCacheEntry>.Skipped(
                    "resolver_service",
                    "Affiliation skipped because identity was unavailable");
            }

            if (string.Equals(
                    resolvedCharacter.ResolverConfidence,
                    "not_found",
                    StringComparison.OrdinalIgnoreCase))
            {
                return ProviderOutcome<ResolverCacheEntry>.Skipped(
                    "resolver_service",
                    "Affiliation skipped for terminal miss",
                    resolvedCharacter);
            }

            if (string.IsNullOrWhiteSpace(resolvedCharacter.CharacterId))
            {
                return ProviderOutcome<ResolverCacheEntry>.Skipped(
                    "resolver_service",
                    "Affiliation skipped because character id was unavailable",
                    resolvedCharacter);
            }

            if (HasFreshAffiliationCheck(resolvedCharacter.AffiliationCheckedAtUtc))
            {
                return ProviderOutcome<ResolverCacheEntry>.Success(
                    resolvedCharacter,
                    "resolver_cache",
                    "Fresh affiliation check already available");
            }

            var affiliationOutcome = await _esiPublicAffiliationProvider.TryGetAffiliationAsync(
                resolvedCharacter.CharacterId,
                cancellationToken);

            var nowUtc = DateTime.UtcNow.ToString("o");

            if (affiliationOutcome.Kind == ProviderOutcomeKind.Success && affiliationOutcome.Value != null)
            {
                var enriched = BuildEnrichedResolverEntry(resolvedCharacter, affiliationOutcome.Value, nowUtc);
                TryUpsert(enriched);
                return ProviderOutcome<ResolverCacheEntry>.Success(
                    enriched,
                    affiliationOutcome.ProviderName,
                    string.IsNullOrWhiteSpace(affiliationOutcome.Detail)
                        ? "Affiliation enrichment succeeded"
                        : affiliationOutcome.Detail);
            }

            if (affiliationOutcome.Kind == ProviderOutcomeKind.NotFound)
            {
                resolvedCharacter.AffiliationCheckedAtUtc = nowUtc;
                TryUpsert(resolvedCharacter);
                return ProviderOutcome<ResolverCacheEntry>.NotFound(
                    affiliationOutcome.ProviderName,
                    string.IsNullOrWhiteSpace(affiliationOutcome.Detail)
                        ? "Affiliation lookup returned not found"
                        : affiliationOutcome.Detail,
                    resolvedCharacter);
            }

            if (affiliationOutcome.Value != null)
            {
                var partiallyEnriched = BuildEnrichedResolverEntry(resolvedCharacter, affiliationOutcome.Value, nowUtc, updateAffiliationCheckedAtUtc: false);

                if (affiliationOutcome.Kind == ProviderOutcomeKind.Throttled)
                {
                    return ProviderOutcome<ResolverCacheEntry>.Throttled(
                        affiliationOutcome.ProviderName,
                        affiliationOutcome.Detail,
                        affiliationOutcome.RetryAfterUtc,
                        partiallyEnriched);
                }

                if (affiliationOutcome.Kind == ProviderOutcomeKind.TemporaryFailure)
                {
                    return ProviderOutcome<ResolverCacheEntry>.TemporaryFailure(
                        affiliationOutcome.ProviderName,
                        affiliationOutcome.Detail,
                        affiliationOutcome.RetryAfterUtc,
                        partiallyEnriched);
                }

                if (affiliationOutcome.Kind == ProviderOutcomeKind.PermanentFailure)
                {
                    return ProviderOutcome<ResolverCacheEntry>.PermanentFailure(
                        affiliationOutcome.ProviderName,
                        affiliationOutcome.Detail,
                        partiallyEnriched);
                }
            }

            if (affiliationOutcome.Kind == ProviderOutcomeKind.Throttled)
            {
                return ProviderOutcome<ResolverCacheEntry>.Throttled(
                    affiliationOutcome.ProviderName,
                    affiliationOutcome.Detail,
                    affiliationOutcome.RetryAfterUtc,
                    resolvedCharacter);
            }

            if (affiliationOutcome.Kind == ProviderOutcomeKind.TemporaryFailure)
            {
                return ProviderOutcome<ResolverCacheEntry>.TemporaryFailure(
                    affiliationOutcome.ProviderName,
                    affiliationOutcome.Detail,
                    affiliationOutcome.RetryAfterUtc,
                    resolvedCharacter);
            }

            if (affiliationOutcome.Kind == ProviderOutcomeKind.PermanentFailure)
            {
                return ProviderOutcome<ResolverCacheEntry>.PermanentFailure(
                    affiliationOutcome.ProviderName,
                    affiliationOutcome.Detail,
                    resolvedCharacter);
            }

            return ProviderOutcome<ResolverCacheEntry>.TemporaryFailure(
                "resolver_service",
                "Affiliation enrichment returned an unknown outcome",
                value: resolvedCharacter);
        }

        private static ResolverCacheEntry BuildEnrichedResolverEntry(
            ResolverCacheEntry resolvedCharacter,
            EsiPublicAffiliationResult affiliation,
            string nowUtc,
            bool updateAffiliationCheckedAtUtc = true)
        {
            var enriched = new ResolverCacheEntry
            {
                CharacterName = string.IsNullOrWhiteSpace(affiliation.CharacterName)
                    ? resolvedCharacter.CharacterName
                    : affiliation.CharacterName,
                CharacterId = resolvedCharacter.CharacterId,
                AllianceId = affiliation.AllianceId,
                AllianceName = affiliation.AllianceName,
                AllianceTicker = affiliation.AllianceTicker,
                CorpName = affiliation.CorpName,
                CorpTicker = affiliation.CorpTicker,
                ResolverConfidence = string.IsNullOrWhiteSpace(resolvedCharacter.ResolverConfidence)
                    ? "search"
                    : resolvedCharacter.ResolverConfidence,
                ResolvedAtUtc = string.IsNullOrWhiteSpace(resolvedCharacter.ResolvedAtUtc)
                    ? nowUtc
                    : resolvedCharacter.ResolvedAtUtc,
                ExpiresAtUtc = string.IsNullOrWhiteSpace(resolvedCharacter.ExpiresAtUtc)
                    ? DateTime.UtcNow.AddDays(30).ToString("o")
                    : resolvedCharacter.ExpiresAtUtc,
                AffiliationCheckedAtUtc = updateAffiliationCheckedAtUtc
                    ? nowUtc
                    : resolvedCharacter.AffiliationCheckedAtUtc
            };

            if (string.IsNullOrWhiteSpace(enriched.CorpName))
            {
                enriched.CorpName = resolvedCharacter.CorpName;
            }

            if (string.IsNullOrWhiteSpace(enriched.CorpTicker))
            {
                enriched.CorpTicker = resolvedCharacter.CorpTicker;
            }

            if (string.IsNullOrWhiteSpace(enriched.AllianceId))
            {
                enriched.AllianceId = resolvedCharacter.AllianceId;
            }

            if (string.IsNullOrWhiteSpace(enriched.AllianceName))
            {
                enriched.AllianceName = resolvedCharacter.AllianceName;
            }

            if (string.IsNullOrWhiteSpace(enriched.AllianceTicker))
            {
                enriched.AllianceTicker = resolvedCharacter.AllianceTicker;
            }

            return enriched;
        }

        private async Task<ResolverCacheEntry?> TryPromoteNotFoundViaEsiExactFallbackAsync(
            string characterName,
            ResolverCacheEntry entry,
            CancellationToken cancellationToken)
        {
            if (entry == null)
            {
                return null;
            }

            if (!string.Equals(entry.ResolverConfidence, "not_found", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(entry.CharacterId))
            {
                return null;
            }

            return await TryPromoteToEsiExactFallbackAsync(characterName, cancellationToken);
        }

        private async Task<ResolverCacheEntry?> TryPromoteToEsiExactFallbackAsync(
            string characterName,
            CancellationToken cancellationToken)
        {
            var exactOutcome = await _esiExactNameResolverProvider.TryResolveCharacterIdExactAsync(
                characterName,
                cancellationToken);

            if (exactOutcome.Kind != ProviderOutcomeKind.Success || string.IsNullOrWhiteSpace(exactOutcome.Value))
            {
                return null;
            }

            var nowUtc = DateTime.UtcNow;

            var fallbackEntry = new ResolverCacheEntry
            {
                CharacterName = characterName,
                CharacterId = exactOutcome.Value,
                AllianceId = "",
                AllianceName = "",
                AllianceTicker = "",
                CorpName = "",
                CorpTicker = "",
                ResolverConfidence = "esi_exact_fallback",
                ResolvedAtUtc = nowUtc.ToString("o"),
                ExpiresAtUtc = nowUtc.AddDays(30).ToString("o"),
                AffiliationCheckedAtUtc = ""
            };

            TryUpsert(fallbackEntry);
            return fallbackEntry;
        }

        private ResolverCacheEntry? TryGetCachedByCharacterName(string characterName)
        {
            try
            {
                var repositoryType = _resolverCacheRepository.GetType();

                foreach (var methodName in new[]
                         {
                             "GetByCharacterName",
                             "GetByName",
                             "FindByCharacterName"
                         })
                {
                    var method = repositoryType.GetMethod(
                        methodName,
                        BindingFlags.Instance | BindingFlags.Public,
                        binder: null,
                        types: new[] { typeof(string) },
                        modifiers: null);

                    if (method == null)
                    {
                        continue;
                    }

                    var result = method.Invoke(_resolverCacheRepository, new object[] { characterName });
                    return result as ResolverCacheEntry;
                }
            }
            catch (Exception ex)
            {
                DebugTraceWriter.WriteLine(
                    $"resolver cache lookup exception: name='{characterName}', error={ex.Message}");
            }

            return null;
        }

        private void TryUpsert(ResolverCacheEntry entry)
        {
            try
            {
                var method = _resolverCacheRepository.GetType().GetMethod(
                    "Upsert",
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    types: new[] { typeof(ResolverCacheEntry) },
                    modifiers: null);

                method?.Invoke(_resolverCacheRepository, new object[] { entry });
            }
            catch (Exception ex)
            {
                DebugTraceWriter.WriteLine(
                    $"resolver cache upsert exception: name='{entry.CharacterName}', error={ex.Message}");
            }
        }

        private static bool HasFreshAffiliationCheck(string checkedAtUtc)
        {
            if (string.IsNullOrWhiteSpace(checkedAtUtc))
            {
                return false;
            }

            if (!DateTime.TryParse(
                    checkedAtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsed))
            {
                return false;
            }

            return parsed > DateTime.UtcNow.AddDays(-7);
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