using PitmastersGrill.Diagnostics;
using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PitmastersGrill.Services
{
    public sealed class BoardPopulationEntryController
    {
        private readonly ClipboardIngestService _clipboardIngestService;
        private readonly ResolverService _resolverService;
        private readonly StatsService _statsService;
        private readonly MainWindowDiagnostics _diagnostics;
        private readonly BoardPopulationRetryController _boardPopulationRetryController;

        private string _lastProcessedClipboardText = string.Empty;
        private bool _isClipboardProcessing;
        private List<string> _activeBoardNames = new();

        public BoardPopulationEntryController(
            ClipboardIngestService clipboardIngestService,
            ResolverService resolverService,
            StatsService statsService,
            MainWindowDiagnostics diagnostics,
            BoardPopulationRetryController boardPopulationRetryController)
        {
            _clipboardIngestService = clipboardIngestService ?? throw new ArgumentNullException(nameof(clipboardIngestService));
            _resolverService = resolverService ?? throw new ArgumentNullException(nameof(resolverService));
            _statsService = statsService ?? throw new ArgumentNullException(nameof(statsService));
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            _boardPopulationRetryController = boardPopulationRetryController ?? throw new ArgumentNullException(nameof(boardPopulationRetryController));
        }

        public IReadOnlyList<string> ActiveBoardNames => _activeBoardNames;
        public bool IsClipboardProcessing => _isClipboardProcessing;

        public void InvalidateLastProcessedClipboard()
        {
            _lastProcessedClipboardText = string.Empty;
        }

        public void ResetTracking(bool preserveLastProcessedClipboardText = false)
        {
            _activeBoardNames = new List<string>();

            if (!preserveLastProcessedClipboardText)
            {
                _lastProcessedClipboardText = string.Empty;
            }
        }

        public async Task ProcessClipboardIfValidAsync(
            Func<bool> clipboardContainsText,
            Func<string> clipboardGetText,
            Action<bool> setBoardButtonsEnabled,
            Func<IDisposable> beginForegroundPriority,
            Action cancelBoardPopulationRetry,
            Action<bool> resetBoardPopulationTracking,
            Action<string, BoardPopulationStatusKind> updateClipboardStatus,
            Func<List<string>, bool, Task> processNamesAsync)
        {
            var clipboardStopwatch = Stopwatch.StartNew();
            if (_isClipboardProcessing)
            {
                return;
            }

            if (clipboardContainsText == null)
            {
                throw new ArgumentNullException(nameof(clipboardContainsText));
            }

            if (clipboardGetText == null)
            {
                throw new ArgumentNullException(nameof(clipboardGetText));
            }

            if (setBoardButtonsEnabled == null)
            {
                throw new ArgumentNullException(nameof(setBoardButtonsEnabled));
            }

            if (beginForegroundPriority == null)
            {
                throw new ArgumentNullException(nameof(beginForegroundPriority));
            }

            if (cancelBoardPopulationRetry == null)
            {
                throw new ArgumentNullException(nameof(cancelBoardPopulationRetry));
            }

            if (resetBoardPopulationTracking == null)
            {
                throw new ArgumentNullException(nameof(resetBoardPopulationTracking));
            }

            if (updateClipboardStatus == null)
            {
                throw new ArgumentNullException(nameof(updateClipboardStatus));
            }

            if (processNamesAsync == null)
            {
                throw new ArgumentNullException(nameof(processNamesAsync));
            }

            _diagnostics.ClipboardProcessStart();

            _isClipboardProcessing = true;
            setBoardButtonsEnabled(false);

            using var foregroundPriority = beginForegroundPriority();

            try
            {
                string? rawClipboardText;

                try
                {
                    if (!clipboardContainsText())
                    {
                        _diagnostics.ClipboardNoText();
                        return;
                    }

                    rawClipboardText = clipboardGetText();
                    _diagnostics.ClipboardTextRead(rawClipboardText);
                }
                catch (Exception ex)
                {
                    _diagnostics.ClipboardReadFailed(ex.Message);
                    AppLogger.ClipboardWarn($"Clipboard read failed. message={ex.Message}");
                    return;
                }

                var comparisonText = _boardPopulationRetryController.GetClipboardComparisonText(_lastProcessedClipboardText);
                var classifyStopwatch = Stopwatch.StartNew();
                var result = _clipboardIngestService.Process(rawClipboardText, comparisonText);
                classifyStopwatch.Stop();
                DiagnosticTelemetry.RecordTiming("clipboard classification", classifyStopwatch.ElapsedMilliseconds, $"chars={result.CharacterCount} lines={result.NonEmptyLineCount}");
                DiagnosticTelemetry.RecordTiming("local-list parse", classifyStopwatch.ElapsedMilliseconds, $"parsedNames={result.ParsedNames.Count} plausibleNames={result.PlausibleNameCount}");
                DiagnosticTelemetry.RecordClipboardSummary(
                    $"shouldProcess={result.ShouldProcess}; reason={result.IgnoreReason}; parsedNames={result.ParsedNames.Count}; chars={result.CharacterCount}; nonEmptyLines={result.NonEmptyLineCount}; plausibleNames={result.PlausibleNameCount}; suspiciousLines={result.SuspiciousLineCount}");

                if (!result.ShouldProcess)
                {
                    _diagnostics.ClipboardIntakeIgnored(result.IgnoreReason);
                    AppLogger.ClipboardInfo(
                        $"Ignored clipboard board. reason={result.IgnoreReason} charCount={result.CharacterCount} nonEmptyLines={result.NonEmptyLineCount} plausibleNames={result.PlausibleNameCount} suspiciousLines={result.SuspiciousLineCount}");

                    if (ShouldSurfaceClipboardIgnore(result.IgnoreReason))
                    {
                        updateClipboardStatus(
                            $"Clipboard ignored: {result.IgnoreReason}",
                            BoardPopulationStatusKind.Warning);
                    }

                    return;
                }

                _lastProcessedClipboardText = result.AcceptedClipboardText;
                cancelBoardPopulationRetry();
                resetBoardPopulationTracking(true);
                _lastProcessedClipboardText = result.AcceptedClipboardText;

                _diagnostics.ClipboardIntakeAccepted(result.ParsedNames.Count, true);

                AppLogger.ClipboardInfo(
                    $"Accepted clipboard board. parsedNames={result.ParsedNames.Count} nonEmptyLines={result.NonEmptyLineCount} plausibleNames={result.PlausibleNameCount} suspiciousLines={result.SuspiciousLineCount} retryReset=true");

                await processNamesAsync(result.ParsedNames, false);
            }
            catch (Exception ex)
            {
                _diagnostics.ClipboardUnhandledException(ex.Message);
                AppLogger.ClipboardError("Clipboard processing failed.", ex);

                var diagnosticBundlePath = DiagnosticBundleService.TryCreateBundle(
                    "clipboard-processing-failed",
                    ex);

                var diagnosticFileName = string.IsNullOrWhiteSpace(diagnosticBundlePath)
                    ? "diagnostic bundle unavailable"
                    : Path.GetFileName(diagnosticBundlePath);

                updateClipboardStatus(
                    $"Clipboard processing failed. {diagnosticFileName}",
                    BoardPopulationStatusKind.Error);
            }
            finally
            {
                setBoardButtonsEnabled(true);
                _isClipboardProcessing = false;
                clipboardStopwatch.Stop();
                DiagnosticTelemetry.RecordTiming("clipboard-to-board total", clipboardStopwatch.ElapsedMilliseconds, "clipboard handler");
                _diagnostics.ClipboardProcessEnd();
            }
        }

        private static bool ShouldSurfaceClipboardIgnore(string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return false;
            }

            if (reason.Contains("last processed payload", StringComparison.OrdinalIgnoreCase) ||
                reason.Contains("Clipboard was empty", StringComparison.OrdinalIgnoreCase) ||
                reason.Contains("not contain enough non-empty lines", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        public async Task ProcessNamesAsync(
            List<string> characterNames,
            bool isRetryPass,
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
            if (saveCurrentNotesAndTags == null)
            {
                throw new ArgumentNullException(nameof(saveCurrentNotesAndTags));
            }

            if (buildInitialBoard == null)
            {
                throw new ArgumentNullException(nameof(buildInitialBoard));
            }

            if (beginProcessingGeneration == null)
            {
                throw new ArgumentNullException(nameof(beginProcessingGeneration));
            }

            if (getCurrentGeneration == null)
            {
                throw new ArgumentNullException(nameof(getCurrentGeneration));
            }

            if (getCurrentRowCount == null)
            {
                throw new ArgumentNullException(nameof(getCurrentRowCount));
            }

            if (processCurrentRowsAsync == null)
            {
                throw new ArgumentNullException(nameof(processCurrentRowsAsync));
            }

            if (updateBoardPopulationStatus == null)
            {
                throw new ArgumentNullException(nameof(updateBoardPopulationStatus));
            }

            if (updateLastRefreshed == null)
            {
                throw new ArgumentNullException(nameof(updateLastRefreshed));
            }

            if (finalizeBoardPopulationPass == null)
            {
                throw new ArgumentNullException(nameof(finalizeBoardPopulationPass));
            }

            saveCurrentNotesAndTags();

            var sourceNames = characterNames ?? new List<string>();
            _diagnostics.BoardProcessRequested(isRetryPass, sourceNames.Count);

            var normalizationStopwatch = Stopwatch.StartNew();
            var cleanedNames = sourceNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            normalizationStopwatch.Stop();
            DiagnosticTelemetry.RecordTiming("pilot normalization/deduplication", normalizationStopwatch.ElapsedMilliseconds, $"incoming={sourceNames.Count} unique={cleanedNames.Count}");

            if (cleanedNames.Count == 0)
            {
                _diagnostics.BoardProcessAbortedNoCleanNames();
                updateBoardPopulationStatus("Board population idle", BoardPopulationStatusKind.Neutral);
                return;
            }

            if (!isRetryPass)
            {
                _activeBoardNames = new List<string>(cleanedNames);
            }

            // Keep initial board construction lightweight so the UI can show the copied names immediately.
            // Cache/database hydration is intentionally deferred into the row processor path, which now
            // runs on background workers and updates the UI one row/stage at a time.
            var cachedIdentities = new Dictionary<string, ResolverCacheEntry>(StringComparer.OrdinalIgnoreCase);
            var cachedStats = new Dictionary<string, StatsCacheEntry>(StringComparer.OrdinalIgnoreCase);

            _diagnostics.CacheHydrateComplete(
                isRetryPass,
                cleanedNames.Count,
                cachedIdentities.Count,
                cachedStats.Count,
                0);

            DebugTraceWriter.WriteLine(
                $"initial board cache hydrate deferred: retryPass={isRetryPass}, names={cleanedNames.Count}");

            var initialBoardStopwatch = Stopwatch.StartNew();
            buildInitialBoard(cleanedNames, cachedIdentities, cachedStats);
            initialBoardStopwatch.Stop();
            DiagnosticTelemetry.RecordTiming("UI board population/render", initialBoardStopwatch.ElapsedMilliseconds, $"initial rows={cleanedNames.Count}");

            var generation = beginProcessingGeneration();
            var boardStopwatch = Stopwatch.StartNew();

            updateBoardPopulationStatus(
                isRetryPass ? "Board population retrying unresolved rows" : "Board population in progress",
                isRetryPass ? BoardPopulationStatusKind.Warning : BoardPopulationStatusKind.Neutral);

            _diagnostics.BoardProcessStart(generation, getCurrentRowCount(), isRetryPass);

            DebugTraceWriter.WriteLine(
                $"board process start: generation={generation}, rowCount={getCurrentRowCount()}, retryPass={isRetryPass}");

            await processCurrentRowsAsync(generation);

            boardStopwatch.Stop();

            if (generation == getCurrentGeneration())
            {
                _diagnostics.BoardProcessSettled(generation, boardStopwatch.ElapsedMilliseconds);

                DebugTraceWriter.WriteLine(
                    $"board process settled: generation={generation}, elapsedMs={boardStopwatch.ElapsedMilliseconds}");
            }
            else
            {
                _diagnostics.BoardProcessSuperseded(generation, boardStopwatch.ElapsedMilliseconds);

                DebugTraceWriter.WriteLine(
                    $"board process superseded: generation={generation}, elapsedMs={boardStopwatch.ElapsedMilliseconds}");
            }

            updateLastRefreshed();
            finalizeBoardPopulationPass(generation);
        }
    }
}
