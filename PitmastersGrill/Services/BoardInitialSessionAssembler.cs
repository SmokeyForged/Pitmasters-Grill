using PitmastersGrill.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PitmastersGrill.Services
{
    public sealed class BoardInitialSessionAssemblyResult
    {
        public BoardInitialSessionAssemblyResult(int createdRowCount, int removedIgnoredRowCount, int finalRowCount)
        {
            CreatedRowCount = createdRowCount;
            RemovedIgnoredRowCount = removedIgnoredRowCount;
            FinalRowCount = finalRowCount;
        }

        public int CreatedRowCount { get; }
        public int RemovedIgnoredRowCount { get; }
        public int FinalRowCount { get; }
    }

    public sealed class BoardInitialSessionAssembler
    {
        private readonly Func<
            List<string>,
            Dictionary<string, ResolverCacheEntry>,
            Dictionary<string, StatsCacheEntry>,
            List<PilotBoardRow>> _createRows;
        private readonly Action<IReadOnlyList<PilotBoardRow>> _hydrateRows;
        private readonly CurrentBoardSession _currentBoardSession;
        private readonly Action<IReadOnlyList<PilotBoardRow>, Action<IReadOnlyList<PilotBoardRow>>> _applyOrdering;
        private readonly Func<IReadOnlyList<PilotBoardRow>, IReadOnlyList<PilotBoardRow>> _findIgnoredRows;
        private readonly Action<IReadOnlyList<PilotBoardRow>> _applyAffiliationCounts;

        public BoardInitialSessionAssembler(
            Func<
                List<string>,
                Dictionary<string, ResolverCacheEntry>,
                Dictionary<string, StatsCacheEntry>,
                List<PilotBoardRow>> createRows,
            Action<IReadOnlyList<PilotBoardRow>> hydrateRows,
            CurrentBoardSession currentBoardSession,
            Action<IReadOnlyList<PilotBoardRow>, Action<IReadOnlyList<PilotBoardRow>>> applyOrdering,
            Func<IReadOnlyList<PilotBoardRow>, IReadOnlyList<PilotBoardRow>> findIgnoredRows,
            Action<IReadOnlyList<PilotBoardRow>> applyAffiliationCounts)
        {
            _createRows = createRows ?? throw new ArgumentNullException(nameof(createRows));
            _hydrateRows = hydrateRows ?? throw new ArgumentNullException(nameof(hydrateRows));
            _currentBoardSession = currentBoardSession ?? throw new ArgumentNullException(nameof(currentBoardSession));
            _applyOrdering = applyOrdering ?? throw new ArgumentNullException(nameof(applyOrdering));
            _findIgnoredRows = findIgnoredRows ?? throw new ArgumentNullException(nameof(findIgnoredRows));
            _applyAffiliationCounts = applyAffiliationCounts ?? throw new ArgumentNullException(nameof(applyAffiliationCounts));
        }

        public BoardInitialSessionAssemblyResult Assemble(
            List<string> characterNames,
            Dictionary<string, ResolverCacheEntry> identities,
            Dictionary<string, StatsCacheEntry> stats)
        {
            ArgumentNullException.ThrowIfNull(characterNames);
            ArgumentNullException.ThrowIfNull(identities);
            ArgumentNullException.ThrowIfNull(stats);

            var initialRows = _createRows(characterNames, identities, stats);
            _hydrateRows(initialRows);

            _currentBoardSession.ReplaceRows(initialRows);
            _applyOrdering(_currentBoardSession.Snapshot(), _currentBoardSession.ReorderRows);

            var ignoredRows = _findIgnoredRows(_currentBoardSession.Snapshot())
                .Where(row => row != null)
                .Distinct()
                .ToList();
            var removedIgnoredRowCount = ignoredRows.Count == 0
                ? 0
                : _currentBoardSession.RemoveRows(ignoredRows);

            _applyAffiliationCounts(_currentBoardSession.Rows);

            return new BoardInitialSessionAssemblyResult(
                initialRows.Count,
                removedIgnoredRowCount,
                _currentBoardSession.Count);
        }
    }
}
