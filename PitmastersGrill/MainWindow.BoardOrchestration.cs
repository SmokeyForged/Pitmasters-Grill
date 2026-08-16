using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System.Collections.Generic;
using System.Diagnostics;

namespace PitmastersGrill
{
    public partial class MainWindow
    {
        private BoardRowStateHydrator _boardRowStateHydrator = null!;

        private void BuildInitialBoard(
            List<string> characterNames,
            Dictionary<string, ResolverCacheEntry> identities,
            Dictionary<string, StatsCacheEntry> stats)
        {
            _diagnostics.InitialBoardBuildStart(characterNames.Count, identities.Count, stats.Count);

            var buildStopwatch = Stopwatch.StartNew();
            var initialRows = _boardRowFactory.CreateRows(characterNames, identities, stats);

            ResetManualBoardSort();
            _boardRowStateHydrator.Hydrate(initialRows);

            _currentBoardSession.ReplaceRows(initialRows);
            ApplyCurrentBoardOrdering();
            ApplyIgnoredAllianceRowsToCurrentBoard();
            RecomputeCorpAllianceCounts();

            PilotBoard.SelectedItem = null;
            _pilotDetailSurface.HideDetailPane();
            _pilotDetailSurface.CloseActiveDetailWindow();
            UpdateLastRefreshed();

            buildStopwatch.Stop();
            _diagnostics.InitialBoardBuildComplete(_currentBoardSession.Count, buildStopwatch.ElapsedMilliseconds);
        }
    }
}
