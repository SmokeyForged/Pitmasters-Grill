using PitmastersGrill.Diagnostics;
using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace PitmastersGrill.Services
{
    public sealed class BoardPopulationSurface
    {
        private readonly BoardPopulationEntryController _boardPopulationEntryController;
        private readonly BoardPopulationPassController _boardPopulationPassController;
        private readonly BoardPopulationRetryController _boardPopulationRetryController;
        private readonly MainWindowDiagnostics _diagnostics;

        public BoardPopulationSurface(
            BoardPopulationEntryController boardPopulationEntryController,
            BoardPopulationPassController boardPopulationPassController,
            BoardPopulationRetryController boardPopulationRetryController,
            MainWindowDiagnostics diagnostics)
        {
            _boardPopulationEntryController = boardPopulationEntryController ?? throw new ArgumentNullException(nameof(boardPopulationEntryController));
            _boardPopulationPassController = boardPopulationPassController ?? throw new ArgumentNullException(nameof(boardPopulationPassController));
            _boardPopulationRetryController = boardPopulationRetryController ?? throw new ArgumentNullException(nameof(boardPopulationRetryController));
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        public void ScheduleClipboardProcessing(
            DispatcherTimer clipboardDebounceTimer,
            int clipboardDebounceMilliseconds)
        {
            clipboardDebounceTimer.Stop();
            clipboardDebounceTimer.Start();
            _diagnostics.ClipboardChangeDebounced(clipboardDebounceMilliseconds);
        }

        public Task ProcessClipboardIfValidAsync(
            Func<bool> clipboardContainsText,
            Func<string> clipboardGetText,
            Action<bool> setBoardButtonsEnabled,
            Func<IDisposable> beginForegroundPriority,
            Action cancelBoardPopulationRetry,
            Action<bool> resetBoardPopulationTracking,
            Action<string, BoardPopulationStatusKind> updateClipboardStatus,
            Func<List<string>, bool, Task> processNamesAsync)
        {
            return _boardPopulationEntryController.ProcessClipboardIfValidAsync(
                clipboardContainsText,
                clipboardGetText,
                setBoardButtonsEnabled,
                beginForegroundPriority,
                cancelBoardPopulationRetry,
                resetBoardPopulationTracking,
                updateClipboardStatus,
                processNamesAsync);
        }

        public Task ProcessNamesAsync(
            List<string> characterNames,
            bool isRetryPass,
            Action<string, bool> triggerSessionContextRefresh,
            Action saveCurrentNotesAndTags,
            Action<List<string>, Dictionary<string, ResolverCacheEntry>, Dictionary<string, StatsCacheEntry>> buildInitialBoard,
            Func<int> beginProcessingGeneration,
            Func<int> getCurrentGeneration,
            Func<int> getCurrentRowCount,
            Func<int, Task> processCurrentRowsAsync,
            Action<string, BoardPopulationStatusKind> updateBoardPopulationStatus,
            Action updateLastRefreshed,
            Action<int> finalizeBoardPopulationPass)
        {
            triggerSessionContextRefresh(
                isRetryPass ? "board retry pass" : "accepted local clipboard",
                !isRetryPass);

            return _boardPopulationEntryController.ProcessNamesAsync(
                characterNames,
                isRetryPass,
                saveCurrentNotesAndTags,
                buildInitialBoard,
                beginProcessingGeneration,
                getCurrentGeneration,
                getCurrentRowCount,
                processCurrentRowsAsync,
                updateBoardPopulationStatus,
                updateLastRefreshed,
                finalizeBoardPopulationPass);
        }

        public void FinalizeBoardPopulationPass(
            int generation,
            int currentGeneration,
            IReadOnlyCollection<PilotBoardRow> currentRows,
            int maxBoardPopulationRetryAttempts,
            Action<string, BoardPopulationStatusKind> updateBoardPopulationStatus,
            Action scheduleBoardPopulationRetry)
        {
            if (generation != currentGeneration)
            {
                _diagnostics.FinalizeSkipped(generation, currentGeneration);
                return;
            }

            var decision = _boardPopulationPassController.BuildFinalizeDecision(
                currentRows,
                _boardPopulationRetryController.RetryAttempt,
                maxBoardPopulationRetryAttempts);

            if (decision.IsComplete)
            {
                _boardPopulationRetryController.MarkComplete();

                _diagnostics.BoardProcessFinalizedComplete(
                    generation,
                    decision.CompleteCount,
                    decision.PartialCount,
                    decision.RetryableCount);

                updateBoardPopulationStatus(decision.StatusText, decision.StatusKind);
                return;
            }

            _boardPopulationRetryController.MarkIncomplete();
            _boardPopulationEntryController.InvalidateLastProcessedClipboard();

            if (decision.RetryLimitReached)
            {
                _diagnostics.BoardProcessRetryLimitReached(
                    generation,
                    decision.RetryableCount,
                    decision.PartialCount,
                    _boardPopulationRetryController.RetryAttempt);

                updateBoardPopulationStatus(decision.StatusText, decision.StatusKind);
                return;
            }

            _diagnostics.BoardProcessRequiresRetry(
                generation,
                decision.RetryableCount,
                decision.PartialCount,
                _boardPopulationRetryController.RetryAttempt);

            updateBoardPopulationStatus(decision.StatusText, decision.StatusKind);

            if (decision.ShouldScheduleRetry)
            {
                scheduleBoardPopulationRetry();
            }
        }

        public void ScheduleBoardPopulationRetry(
            IReadOnlyCollection<PilotBoardRow> currentRows,
            Dispatcher dispatcher,
            Action<string, BoardPopulationStatusKind> updateBoardPopulationStatus,
            Func<Task> processRetryPassAsync)
        {
            _boardPopulationRetryController.ScheduleRetry(
                currentRows,
                dispatcher,
                updateBoardPopulationStatus,
                processRetryPassAsync);
        }

        public Task ProcessRetryPassAsync(
            IReadOnlyCollection<PilotBoardRow> currentRows,
            Func<IDisposable> beginForegroundPriority,
            Func<IReadOnlyCollection<PilotBoardRow>, int, Task> processRowBatchAsync,
            Func<int> getProcessingGeneration,
            Action updateLastRefreshed,
            Action<int> finalizeBoardPopulationPass)
        {
            return _boardPopulationRetryController.ProcessRetryPassAsync(
                currentRows,
                beginForegroundPriority,
                processRowBatchAsync,
                getProcessingGeneration,
                updateLastRefreshed,
                finalizeBoardPopulationPass);
        }

        public void CancelBoardPopulationRetry()
        {
            _boardPopulationRetryController.CancelRetry();
        }

        public void ResetBoardPopulationTracking(bool preserveLastProcessedClipboardText = false)
        {
            _boardPopulationEntryController.ResetTracking(preserveLastProcessedClipboardText);
            _boardPopulationRetryController.ResetTracking();
        }

        public void ClearBoard(
            string reason,
            Func<int> getCurrentRowCount,
            Action saveCurrentNotesAndTags,
            Action cancelBoardPopulationRetry,
            Action resetTracking,
            Action resetManualBoardSort,
            Action clearAndInvalidateBoardSession,
            Action recomputeCorpAllianceCounts,
            Action closeActiveDetailWindow,
            Action updateOpenDetailsButtonState,
            Action updateLastRefreshed,
            Action<string, BoardPopulationStatusKind> updateBoardPopulationStatus)
        {
            ArgumentNullException.ThrowIfNull(getCurrentRowCount);
            ArgumentNullException.ThrowIfNull(clearAndInvalidateBoardSession);

            var clearedRowCount = getCurrentRowCount();

            _diagnostics.ClearBoardStart(clearedRowCount);

            saveCurrentNotesAndTags();
            cancelBoardPopulationRetry();
            resetTracking();
            resetManualBoardSort();
            clearAndInvalidateBoardSession();
            recomputeCorpAllianceCounts();
            closeActiveDetailWindow();
            updateOpenDetailsButtonState();

            updateLastRefreshed();
            updateBoardPopulationStatus("Board cleared", BoardPopulationStatusKind.Neutral);

            AppLogger.UiInfo($"Board cleared. reason='{reason}' removedRows={clearedRowCount}");
            _diagnostics.ClearBoardComplete();
        }

        // Temporary Phase 3 compatibility overload. Remove after MainWindow is cut over to CurrentBoardSession.
        public void ClearBoard(
            string reason,
            ObservableCollection<PilotBoardRow> currentRows,
            Action saveCurrentNotesAndTags,
            Action cancelBoardPopulationRetry,
            Action resetTracking,
            Action resetManualBoardSort,
            Action unsubscribeFromAllBoardRows,
            Action recomputeCorpAllianceCounts,
            Action closeActiveDetailWindow,
            Action updateOpenDetailsButtonState,
            Action updateLastRefreshed,
            Action<string, BoardPopulationStatusKind> updateBoardPopulationStatus,
            Action incrementProcessingGeneration)
        {
            var clearedRowCount = currentRows.Count;

            _diagnostics.ClearBoardStart(clearedRowCount);

            saveCurrentNotesAndTags();
            cancelBoardPopulationRetry();
            incrementProcessingGeneration();
            resetTracking();
            resetManualBoardSort();
            unsubscribeFromAllBoardRows();

            currentRows.Clear();
            recomputeCorpAllianceCounts();
            closeActiveDetailWindow();
            updateOpenDetailsButtonState();

            updateLastRefreshed();
            updateBoardPopulationStatus("Board cleared", BoardPopulationStatusKind.Neutral);

            AppLogger.UiInfo($"Board cleared. reason='{reason}' removedRows={clearedRowCount}");
            _diagnostics.ClearBoardComplete();
        }
    }
}
