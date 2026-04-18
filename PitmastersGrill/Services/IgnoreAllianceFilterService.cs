using System;
using System.Collections.Generic;
using System.Linq;

namespace PitmastersGrill.Services
{
    public sealed class IgnoreAllianceFilterResult<T>
    {
        public IgnoreAllianceFilterResult(
            IReadOnlyList<T> keptItems,
            IReadOnlyList<T> removedItems)
        {
            KeptItems = keptItems ?? throw new ArgumentNullException(nameof(keptItems));
            RemovedItems = removedItems ?? throw new ArgumentNullException(nameof(removedItems));
        }

        public IReadOnlyList<T> KeptItems { get; }
        public IReadOnlyList<T> RemovedItems { get; }
        public int RemovedCount => RemovedItems.Count;
    }

    public sealed class IgnoreAllianceFilterService
    {
        public IgnoreAllianceFilterResult<T> Filter<T>(
            IEnumerable<T> items,
            IEnumerable<long> ignoredAllianceIds,
            Func<T, long?> allianceIdSelector)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (allianceIdSelector == null)
            {
                throw new ArgumentNullException(nameof(allianceIdSelector));
            }

            var ignoredSet = ignoredAllianceIds == null
                ? new HashSet<long>()
                : new HashSet<long>(ignoredAllianceIds.Where(x => x > 0));

            var keptItems = new List<T>();
            var removedItems = new List<T>();

            foreach (var item in items)
            {
                var allianceId = allianceIdSelector(item);

                if (ShouldIgnore(allianceId, ignoredSet))
                {
                    removedItems.Add(item);
                    continue;
                }

                keptItems.Add(item);
            }

            return new IgnoreAllianceFilterResult<T>(keptItems, removedItems);
        }

        public bool ShouldIgnore(long? allianceId, IEnumerable<long> ignoredAllianceIds)
        {
            if (!allianceId.HasValue || allianceId.Value <= 0)
            {
                return false;
            }

            if (ignoredAllianceIds == null)
            {
                return false;
            }

            if (ignoredAllianceIds is HashSet<long> ignoredSet)
            {
                return ignoredSet.Contains(allianceId.Value);
            }

            return ignoredAllianceIds.Contains(allianceId.Value);
        }
    }
}