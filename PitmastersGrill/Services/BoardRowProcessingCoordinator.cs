using PitmastersGrill.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersGrill.Services
{
    public delegate Task BoardRowProcessingWork(
        PilotBoardRow row,
        int generation,
        Func<Action, Task> runOnUiAsync);

    public sealed class BoardRowProcessingCoordinator
    {
        private readonly CurrentBoardSession _currentBoardSession;
        private readonly BoardRowProcessingWork _processRowAsync;
        private readonly Func<Action, Task> _dispatchAsync;
        private readonly Action<PilotBoardRow> _applyWatchedState;
        private readonly Action _applyCurrentBoardOrdering;
        private readonly Action _updateWatchActionState;
        private readonly Action<PilotBoardRow> _refreshDetail;
        private readonly Func<PilotBoardRow, bool> _shouldRemoveIgnoredRow;
        private readonly Action<PilotBoardRow> _removeIgnoredRow;

        public BoardRowProcessingCoordinator(
            CurrentBoardSession currentBoardSession,
            BoardRowProcessingWork processRowAsync,
            Func<Action, Task> dispatchAsync,
            Action<PilotBoardRow> applyWatchedState,
            Action applyCurrentBoardOrdering,
            Action updateWatchActionState,
            Action<PilotBoardRow> refreshDetail,
            Func<PilotBoardRow, bool> shouldRemoveIgnoredRow,
            Action<PilotBoardRow> removeIgnoredRow)
        {
            _currentBoardSession = currentBoardSession ?? throw new ArgumentNullException(nameof(currentBoardSession));
            _processRowAsync = processRowAsync ?? throw new ArgumentNullException(nameof(processRowAsync));
            _dispatchAsync = dispatchAsync ?? throw new ArgumentNullException(nameof(dispatchAsync));
            _applyWatchedState = applyWatchedState ?? throw new ArgumentNullException(nameof(applyWatchedState));
            _applyCurrentBoardOrdering = applyCurrentBoardOrdering ?? throw new ArgumentNullException(nameof(applyCurrentBoardOrdering));
            _updateWatchActionState = updateWatchActionState ?? throw new ArgumentNullException(nameof(updateWatchActionState));
            _refreshDetail = refreshDetail ?? throw new ArgumentNullException(nameof(refreshDetail));
            _shouldRemoveIgnoredRow = shouldRemoveIgnoredRow ?? throw new ArgumentNullException(nameof(shouldRemoveIgnoredRow));
            _removeIgnoredRow = removeIgnoredRow ?? throw new ArgumentNullException(nameof(removeIgnoredRow));
        }

        public async Task ProcessSingleRowAsync(PilotBoardRow row, SemaphoreSlim semaphore, int generation)
        {
            ArgumentNullException.ThrowIfNull(row);
            ArgumentNullException.ThrowIfNull(semaphore);

            await semaphore.WaitAsync();

            try
            {
                if (!_currentBoardSession.IsCurrentGeneration(generation))
                {
                    return;
                }

                await _processRowAsync(
                    row,
                    generation,
                    action => RunForGenerationAsync(generation, action));

                if (!_currentBoardSession.IsCurrentGeneration(generation))
                {
                    return;
                }

                await RunForGenerationAsync(generation, () =>
                {
                    _applyWatchedState(row);
                    _applyCurrentBoardOrdering();
                    _updateWatchActionState();
                    _refreshDetail(row);
                });

                if (!_currentBoardSession.IsCurrentGeneration(generation) ||
                    !_shouldRemoveIgnoredRow(row))
                {
                    return;
                }

                await RunForGenerationAsync(generation, () => _removeIgnoredRow(row));
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task RefreshCurrentRowsFromLocalIntelAsync(
            Action cancelBoardPopulationRetry,
            Action showRefreshingStatus,
            Func<IReadOnlyList<PilotBoardRow>, int, Task> processRowsAsync,
            Action<int> finalizeBoardPopulationPass,
            Action updateLastRefreshed)
        {
            ArgumentNullException.ThrowIfNull(cancelBoardPopulationRetry);
            ArgumentNullException.ThrowIfNull(showRefreshingStatus);
            ArgumentNullException.ThrowIfNull(processRowsAsync);
            ArgumentNullException.ThrowIfNull(finalizeBoardPopulationPass);
            ArgumentNullException.ThrowIfNull(updateLastRefreshed);

            if (_currentBoardSession.Count == 0)
            {
                return;
            }

            cancelBoardPopulationRetry();
            var generation = _currentBoardSession.BeginProcessingGeneration();
            showRefreshingStatus();
            await processRowsAsync(_currentBoardSession.Snapshot(), generation);
            finalizeBoardPopulationPass(generation);
            updateLastRefreshed();
        }

        private Task RunForGenerationAsync(int generation, Action action)
        {
            return _dispatchAsync(() =>
            {
                if (!_currentBoardSession.IsCurrentGeneration(generation))
                {
                    return;
                }

                action();
            });
        }
    }
}
