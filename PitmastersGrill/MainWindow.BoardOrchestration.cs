using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System.Collections.Generic;
using System.Diagnostics;

namespace PitmastersGrill
{
    public partial class MainWindow
    {
        private BoardRowStateHydrator _boardRowStateHydrator = null!;
        private BoardInitialSessionAssembler _boardInitialSessionAssembler = null!;

        private void BuildInitialBoard(
            List<string> characterNames,
            Dictionary<string, ResolverCacheEntry> identities,
            Dictionary<string, StatsCacheEntry> stats)
        {
            _diagnostics.InitialBoardBuildStart(characterNames.Count, identities.Count, stats.Count);

            var buildStopwatch = Stopwatch.StartNew();
            ResetManualBoardSort();

            var result = _boardInitialSessionAssembler.Assemble(characterNames, identities, stats);
            if (result.RemovedIgnoredRowCount > 0)
            {
                AppLogger.UiInfo($"Ignored alliance filter removed rows from initial board. removedRows={result.RemovedIgnoredRowCount}");
            }

            _analysisTabPresenter.UpdateBoardSummary(_currentBoardSession.Rows);
            _analysisTabPresenter.UpdateAnalysisTab(_currentBoardSession.Rows);

            PilotBoard.SelectedItem = null;
            _pilotDetailSurface.HideDetailPane();
            _pilotDetailSurface.CloseActiveDetailWindow();
            UpdateLastRefreshed();

            buildStopwatch.Stop();
            _diagnostics.InitialBoardBuildComplete(result.FinalRowCount, buildStopwatch.ElapsedMilliseconds);
        }
    }
}
