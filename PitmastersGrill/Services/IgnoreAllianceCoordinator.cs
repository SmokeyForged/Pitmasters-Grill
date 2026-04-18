using System;
using System.Collections.Generic;
using System.Linq;

namespace PitmastersGrill.Services
{
    public sealed class IgnoreAllianceCoordinator
    {
        private readonly IgnoreAllianceListService _ignoreAllianceListService;
        private readonly IgnoreAllianceFilterService _ignoreAllianceFilterService;
        private HashSet<long> _ignoredAllianceIds;

        public IgnoreAllianceCoordinator(
            IgnoreAllianceListService ignoreAllianceListService,
            IgnoreAllianceFilterService ignoreAllianceFilterService)
        {
            _ignoreAllianceListService = ignoreAllianceListService
                ?? throw new ArgumentNullException(nameof(ignoreAllianceListService));
            _ignoreAllianceFilterService = ignoreAllianceFilterService
                ?? throw new ArgumentNullException(nameof(ignoreAllianceFilterService));

            _ignoredAllianceIds = _ignoreAllianceListService.LoadAllianceIds();
        }

        public bool HasIgnoredAllianceIds => _ignoredAllianceIds.Count > 0;

        public IReadOnlyList<long> GetIgnoredAllianceIds()
        {
            return _ignoredAllianceIds
                .OrderBy(x => x)
                .ToList();
        }

        public bool ContainsAllianceId(long allianceId)
        {
            return allianceId > 0 && _ignoredAllianceIds.Contains(allianceId);
        }

        public IgnoreAllianceNormalizationResult MergeWithExisting(
            IEnumerable<long> existingAllianceIds,
            IEnumerable<string> rawAllianceIds)
        {
            var normalizationResult = _ignoreAllianceListService.NormalizeRawAllianceIds(rawAllianceIds);

            var mergedAllianceIds = (existingAllianceIds ?? Enumerable.Empty<long>())
                .Concat(normalizationResult.NormalizedAllianceIds)
                .Where(x => x > 0)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            return new IgnoreAllianceNormalizationResult(
                mergedAllianceIds,
                normalizationResult.InvalidEntries);
        }

        public IgnoreAllianceNormalizationResult ReplaceAndPersist(IEnumerable<string> rawAllianceIds)
        {
            var normalizationResult = _ignoreAllianceListService.NormalizeRawAllianceIds(rawAllianceIds);

            _ignoredAllianceIds = new HashSet<long>(normalizationResult.NormalizedAllianceIds);
            _ignoreAllianceListService.SaveAllianceIds(_ignoredAllianceIds);

            return normalizationResult;
        }

        public void ReplaceAndPersist(IEnumerable<long> allianceIds)
        {
            var normalizedAllianceIds = (allianceIds ?? Enumerable.Empty<long>())
                .Where(x => x > 0)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            _ignoredAllianceIds = new HashSet<long>(normalizedAllianceIds);
            _ignoreAllianceListService.SaveAllianceIds(_ignoredAllianceIds);
        }

        public bool AddAllianceIdAndPersist(long allianceId)
        {
            if (allianceId <= 0)
            {
                return false;
            }

            if (!_ignoredAllianceIds.Add(allianceId))
            {
                return false;
            }

            _ignoreAllianceListService.SaveAllianceIds(_ignoredAllianceIds);
            return true;
        }

        public void ClearAndPersist()
        {
            _ignoredAllianceIds.Clear();
            _ignoreAllianceListService.SaveAllianceIds(_ignoredAllianceIds);
        }

        public IgnoreAllianceFilterResult<T> ApplyToRows<T>(
            IEnumerable<T> items,
            Func<T, long?> allianceIdSelector)
        {
            return _ignoreAllianceFilterService.Filter(items, _ignoredAllianceIds, allianceIdSelector);
        }

        public bool ShouldIgnoreAlliance(long? allianceId)
        {
            return _ignoreAllianceFilterService.ShouldIgnore(allianceId, _ignoredAllianceIds);
        }

        public void Reload()
        {
            _ignoredAllianceIds = _ignoreAllianceListService.LoadAllianceIds();
        }
    }
}
