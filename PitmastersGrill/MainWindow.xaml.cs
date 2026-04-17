using PitmastersGrill.Diagnostics;
using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using PitmastersGrill.Providers;
using PitmastersGrill.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace PitmastersGrill
{
    public partial class MainWindow : Window
    {
        private const int WmClipboardUpdate = 0x031D;
        private const int DefaultBoardPopulationRetryDelaySeconds = 12;
        private const int MaxBoardPopulationRetryAttempts = 5;
        private const int DwMWaUseImmersiveDarkMode = 20;

        private readonly BackgroundIntelUpdateService _backgroundIntelUpdateService;
        private readonly AppSettingsService _appSettingsService;
        private AppSettings _appSettings = new();

        private readonly LocalListParser _localListParser;
        private readonly ClipboardPayloadInspector _clipboardPayloadInspector;
        private readonly ClipboardIngestService _clipboardIngestService;
        private readonly BoardRowFactory _boardRowFactory;
        private readonly DatabaseBootstrap _databaseBootstrap;
        private readonly NotesRepository _notesRepository;
        private readonly ResolverCacheRepository _resolverCacheRepository;
        private readonly StatsCacheRepository _statsCacheRepository;
        private readonly ZkillSearchResolverProvider _zkillSearchResolverProvider;
        private readonly EsiExactNameResolverProvider _esiExactNameResolverProvider;
        private readonly EsiPublicAffiliationProvider _esiPublicAffiliationProvider;
        private readonly ZkillStatsProvider _zkillStatsProvider;
        private readonly ResolverService _resolverService;
        private readonly StatsService _statsService;
        private readonly ZkillUrlBuilder _zkillUrlBuilder;
        private readonly BrowserLauncher _browserLauncher;
        private readonly MainWindowDiagnostics _diagnostics;

        private readonly object _timingMarkerSync = new();

        private string _lastProcessedClipboardText = string.Empty;
        private readonly ObservableCollection<PilotBoardRow> _currentRows = new();
        private string _activeDetailCharacterName = string.Empty;
        private bool _isClipboardProcessing;
        private bool _isApplyingSettings;
        private bool _isApplyingDetailPaneState;
        private int _processingGeneration;

        private bool _activeBoardPopulationIncomplete;
        private bool _activeBoardPopulationRetryScheduled;
        private int _activeBoardPopulationRetryAttempt;
        private CancellationTokenSource? _boardPopulationRetryCts;
        private List<string> _activeBoardNames = new();

        private BoardPopulationStatusKind _currentBoardPopulationStatusKind = BoardPopulationStatusKind.Neutral;

        private int _firstResolverLoggedGeneration = -1;
        private int _firstIdentityUiLoggedGeneration = -1;
        private int _firstAffiliationLoggedGeneration = -1;
        private int _firstStatsLoggedGeneration = -1;

        public MainWindow(BackgroundIntelUpdateService backgroundIntelUpdateService)
        {
            _backgroundIntelUpdateService = backgroundIntelUpdateService;
            _backgroundIntelUpdateService.StatusChanged += OnIntelUpdateStatusChanged;

            _appSettingsService = new AppSettingsService();

            _isApplyingSettings = true;
            InitializeComponent();

            _diagnostics = new MainWindowDiagnostics(Dispatcher);

            AppLogger.UiInfo("MainWindow InitializeComponent complete.");

            _localListParser = new LocalListParser();
            _clipboardPayloadInspector = new ClipboardPayloadInspector();
            _clipboardIngestService = new ClipboardIngestService(_localListParser, _clipboardPayloadInspector);
            _boardRowFactory = new BoardRowFactory();

            var databasePath = AppPaths.GetDatabasePath();

            _databaseBootstrap = new DatabaseBootstrap(databasePath);
            _notesRepository = new NotesRepository(databasePath);
            _resolverCacheRepository = new ResolverCacheRepository(databasePath);
            _statsCacheRepository = new StatsCacheRepository(databasePath);
            _zkillSearchResolverProvider = new ZkillSearchResolverProvider();
            _esiExactNameResolverProvider = new EsiExactNameResolverProvider();
            _esiPublicAffiliationProvider = new EsiPublicAffiliationProvider();
            _zkillStatsProvider = new ZkillStatsProvider();
            _resolverService = new ResolverService(
                _resolverCacheRepository,
                _zkillSearchResolverProvider,
                _esiExactNameResolverProvider,
                _esiPublicAffiliationProvider);
            _statsService = new StatsService(
                _statsCacheRepository,
                _zkillStatsProvider);
            _zkillUrlBuilder = new ZkillUrlBuilder();
            _browserLauncher = new BrowserLauncher();

            try
            {
                _databaseBootstrap.Initialize();
                DebugTraceWriter.Clear();
                AppLogger.DatabaseInfo($"MainWindow local database initialized. path={databasePath}");
            }
            catch (Exception ex)
            {
                AppLogger.DatabaseError("MainWindow failed to initialize local database.", ex);

                MessageBox.Show(
                    $"Failed to initialize local database.\n\n{ex.Message}",
                    "PMG Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Close();
                return;
            }

            _appSettings = _appSettingsService.Load();

            DarkModeCheckBox.IsChecked = _appSettings.DarkModeEnabled;
            AlwaysOnTopCheckBox.IsChecked = _appSettings.AlwaysOnTopEnabled;
            WindowOpacitySlider.Value = CoerceOpacityPercent(_appSettings.WindowOpacityPercent);
            MaxKillmailAgeDaysTextBox.Text = GetMaxKillmailAgeTextBoxText();
            KillmailDataRootPathTextBox.Text = GetKillmailPathEditorText();
            ApplyLogLevelSelection();

            AppLogger.ConfigureLogLevel(_appSettings.LogLevel);

            _isApplyingSettings = false;

            ApplyTheme(_appSettings.DarkModeEnabled);
            ApplyWindowSettings();
            UpdateMaxKillmailAgeUi();
            UpdateKillmailPathUi();

            PilotBoard.ItemsSource = _currentRows;
            UpdateLastRefreshed();
            UpdateBoardPopulationStatus("Board population idle", BoardPopulationStatusKind.Neutral);
            HideDetailPane();
            ApplyIntelUpdateSnapshot(_backgroundIntelUpdateService.GetSnapshot());

            AppLogger.DatabaseInfo(
                $"Killmail data path resolved. displayPath={KillmailPaths.GetKillmailDataDirectoryDisplayPath()} source={KillmailPaths.GetKillmailDataDirectorySourceDescription()}");

            AppLogger.UiInfo(
                $"MainWindow ready. darkMode={_appSettings.DarkModeEnabled} alwaysOnTop={_appSettings.AlwaysOnTopEnabled} opacityPercent={CoerceOpacityPercent(_appSettings.WindowOpacityPercent):0} logLevel={_appSettings.LogLevel}");
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwnd = new WindowInteropHelper(this).Handle;
            AddClipboardFormatListener(hwnd);

            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);

            ApplyTitleBarTheme();

            AppLogger.UiInfo("MainWindow source initialized. Clipboard listener attached and title bar theme applied.");
        }

        protected override void OnClosed(EventArgs e)
        {
            AppLogger.UiInfo("MainWindow closing requested.");

            SaveCurrentNotesAndTags();
            CancelBoardPopulationRetry();
            _backgroundIntelUpdateService.StatusChanged -= OnIntelUpdateStatusChanged;
            _diagnostics.Dispose();

            var hwnd = new WindowInteropHelper(this).Handle;
            RemoveClipboardFormatListener(hwnd);

            AppLogger.UiInfo("MainWindow closed. Clipboard listener removed and retry state cancelled.");

            base.OnClosed(e);
        }

        private void DarkModeCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isApplyingSettings)
            {
                return;
            }

            _appSettings.DarkModeEnabled = DarkModeCheckBox.IsChecked == true;
            _appSettingsService.Save(_appSettings);
            ApplyTheme(_appSettings.DarkModeEnabled);

            AppLogger.UiInfo($"Dark mode changed. enabled={_appSettings.DarkModeEnabled}");
        }

        private void AlwaysOnTopCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isApplyingSettings)
            {
                return;
            }

            _appSettings.AlwaysOnTopEnabled = AlwaysOnTopCheckBox.IsChecked == true;
            _appSettingsService.Save(_appSettings);
            ApplyWindowSettings();

            AppLogger.UiInfo($"Always on top changed. enabled={_appSettings.AlwaysOnTopEnabled}");
        }

        private void WindowOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var opacityPercent = CoerceOpacityPercent(WindowOpacitySlider.Value);

            if (WindowOpacityValueText != null)
            {
                WindowOpacityValueText.Text = $"{opacityPercent:0}%";
            }

            if (_isApplyingSettings)
            {
                return;
            }

            _appSettings.WindowOpacityPercent = opacityPercent;
            _appSettingsService.Save(_appSettings);
            ApplyWindowSettings();

            AppLogger.UiInfo($"Window opacity changed. opacityPercent={opacityPercent:0}");
        }

        private void LogLevelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isApplyingSettings || LogLevelComboBox == null)
            {
                return;
            }

            var selectedLogLevel = GetSelectedLogLevel();

            if (_appSettings.LogLevel == selectedLogLevel)
            {
                return;
            }

            _appSettings.LogLevel = selectedLogLevel;
            _appSettingsService.Save(_appSettings);
            AppLogger.ConfigureLogLevel(selectedLogLevel);

            AppLogger.AppInfo($"Log level changed. level={selectedLogLevel}");
        }

        private void KnownCynoOverrideCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isApplyingDetailPaneState)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_activeDetailCharacterName))
            {
                return;
            }

            SaveCurrentNotesAndTags();

            if (PilotBoard.SelectedItem is PilotBoardRow selectedRow)
            {
                selectedRow.KnownCynoOverride = KnownCynoOverrideCheckBox.IsChecked == true;

                AppLogger.UiInfo(
                    $"Known cyno override changed. character='{selectedRow.CharacterName}' enabled={selectedRow.KnownCynoOverride}");
            }
        }

        private void BaitOverrideCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isApplyingDetailPaneState)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_activeDetailCharacterName))
            {
                return;
            }

            SaveCurrentNotesAndTags();

            if (PilotBoard.SelectedItem is PilotBoardRow selectedRow)
            {
                selectedRow.BaitOverride = BaitOverrideCheckBox.IsChecked == true;

                AppLogger.UiInfo(
                    $"Bait override changed. character='{selectedRow.CharacterName}' enabled={selectedRow.BaitOverride}");
            }
        }

        private void ApplyTheme(bool darkModeEnabled)
        {
            if (darkModeEnabled)
            {
                SetBrushResource("HeaderTextBrush", "#FFFFFF");
                SetBrushResource("BodyTextBrush", "#F3F3F3");
                SetBrushResource("MutedTextBrush", "#DDDDDD");
                SetBrushResource("GridLineBrush", "#4A4A4A");
                SetBrushResource("PanelBorderBrush", "#3A3A3A");
            }
            else
            {
                SetBrushResource("HeaderTextBrush", "#111111");
                SetBrushResource("BodyTextBrush", "#222222");
                SetBrushResource("MutedTextBrush", "#444444");
                SetBrushResource("GridLineBrush", "#CFCFCF");
                SetBrushResource("PanelBorderBrush", "#D0D0D0");
            }

            ApplySurfaceOpacity();
            ApplyBoardPopulationStatusVisual();
            ApplyTitleBarTheme();
        }

        private void ApplyWindowSettings()
        {
            Topmost = _appSettings.AlwaysOnTopEnabled;
            ApplySurfaceOpacity();

            var opacityPercent = CoerceOpacityPercent(_appSettings.WindowOpacityPercent);
            if (WindowOpacityValueText != null)
            {
                WindowOpacityValueText.Text = $"{opacityPercent:0}%";
            }
        }

        private static double CoerceOpacityPercent(double value)
        {
            if (value < 35)
            {
                return 35;
            }

            if (value > 100)
            {
                return 100;
            }

            return Math.Round(value, 0);
        }

        private void ApplySurfaceOpacity()
        {
            var alpha = (byte)Math.Round(255 * (CoerceOpacityPercent(_appSettings.WindowOpacityPercent) / 100.0));

            if (_appSettings.DarkModeEnabled)
            {
                SetBrushResource("WindowBackgroundBrush", "#1E1E1E", alpha);
                SetBrushResource("SurfaceBrush", "#1F1F1F", alpha);
                SetBrushResource("SurfaceAltBrush", "#252525", alpha);
                SetBrushResource("GridBackgroundBrush", "#2B2B2B", alpha);
                SetBrushResource("GridAlternateBrush", "#323232", alpha);
                SetBrushResource("GridHeaderBrush", "#202020", alpha);
            }
            else
            {
                SetBrushResource("WindowBackgroundBrush", "#F5F5F5", alpha);
                SetBrushResource("SurfaceBrush", "#FFFFFF", alpha);
                SetBrushResource("SurfaceAltBrush", "#FAFAFA", alpha);
                SetBrushResource("GridBackgroundBrush", "#FFFFFF", alpha);
                SetBrushResource("GridAlternateBrush", "#F2F2F2", alpha);
                SetBrushResource("GridHeaderBrush", "#E8E8E8", alpha);
            }
        }

        private void SetBrushResource(string resourceKey, string hexColor)
        {
            var color = (Color)ColorConverter.ConvertFromString(hexColor);
            Resources[resourceKey] = new SolidColorBrush(color);
        }

        private void SetBrushResource(string resourceKey, string hexColor, byte alpha)
        {
            var baseColor = (Color)ColorConverter.ConvertFromString(hexColor);
            Resources[resourceKey] = new SolidColorBrush(Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B));
        }

        private void ApplyTitleBarTheme()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            var useDarkMode = _appSettings.DarkModeEnabled ? 1 : 0;
            try
            {
                DwmSetWindowAttribute(hwnd, DwMWaUseImmersiveDarkMode, ref useDarkMode, sizeof(int));
            }
            catch
            {
            }
        }

        private void OnIntelUpdateStatusChanged(IntelUpdateStatusSnapshot snapshot)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => ApplyIntelUpdateSnapshot(snapshot));
                return;
            }

            ApplyIntelUpdateSnapshot(snapshot);
        }

        private void ApplyIntelUpdateSnapshot(IntelUpdateStatusSnapshot snapshot)
        {
            IntelUpdateStatusText.Text = NormalizeKillmailIntelText(snapshot.StatusText);
            IntelUpdateDetailText.Text = NormalizeKillmailIntelText(snapshot.DetailText);

            if (snapshot.HasError)
            {
                IntelUpdateBanner.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6E1111"));
                return;
            }

            if (snapshot.IsRunning || !snapshot.IsCurrentThroughYesterday)
            {
                IntelUpdateBanner.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5A1111"));
                return;
            }

            IntelUpdateBanner.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#155724"));
        }

        private static string NormalizeKillmailIntelText(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return input ?? "";
            }

            return input
                .Replace("LOCAL INTEL", "KILLMAIL INTEL", StringComparison.OrdinalIgnoreCase)
                .Replace("Local intel", "Killmail intel", StringComparison.OrdinalIgnoreCase);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmClipboardUpdate)
            {
                _ = ProcessClipboardIfValidAsync();
            }

            return IntPtr.Zero;
        }

        private async Task ProcessClipboardIfValidAsync()
        {
            if (_isClipboardProcessing)
            {
                return;
            }

            _diagnostics.ClipboardProcessStart();

            _isClipboardProcessing = true;
            EnableKillmailDbPullButton.IsEnabled = false;
            ClearBoardButton.IsEnabled = false;

            using var foregroundPriority = _backgroundIntelUpdateService.BeginForegroundPriority();

            try
            {
                string? rawClipboardText;

                try
                {
                    if (!Clipboard.ContainsText())
                    {
                        _diagnostics.ClipboardNoText();
                        return;
                    }

                    rawClipboardText = Clipboard.GetText();
                    _diagnostics.ClipboardTextRead(rawClipboardText);
                }
                catch (Exception ex)
                {
                    _diagnostics.ClipboardReadFailed(ex.Message);
                    AppLogger.ClipboardWarn($"Clipboard read failed. message={ex.Message}");
                    return;
                }

                var comparisonText = _activeBoardPopulationIncomplete ? string.Empty : _lastProcessedClipboardText;

                var result = _clipboardIngestService.Process(rawClipboardText, comparisonText);

                if (!result.ShouldProcess)
                {
                    _diagnostics.ClipboardIntakeIgnored(result.IgnoreReason);
                    AppLogger.ClipboardInfo($"Ignored clipboard board. reason={result.IgnoreReason}");
                    return;
                }

                _lastProcessedClipboardText = result.AcceptedClipboardText;
                CancelBoardPopulationRetry();
                ResetBoardPopulationTracking();

                _diagnostics.ClipboardIntakeAccepted(result.ParsedNames.Count, true);

                AppLogger.ClipboardInfo(
                    $"Accepted clipboard board. parsedNames={result.ParsedNames.Count} retryReset=true");

                await ProcessNamesAsync(result.ParsedNames, false);
            }
            finally
            {
                EnableKillmailDbPullButton.IsEnabled = true;
                ClearBoardButton.IsEnabled = true;
                _isClipboardProcessing = false;
                _diagnostics.ClipboardProcessEnd();
            }
        }

        private async Task ProcessNamesAsync(List<string> characterNames, bool isRetryPass)
        {
            SaveCurrentNotesAndTags();

            _diagnostics.BoardProcessRequested(isRetryPass, characterNames?.Count ?? 0);

            var cleanedNames = characterNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (cleanedNames.Count == 0)
            {
                _diagnostics.BoardProcessAbortedNoCleanNames();
                UpdateBoardPopulationStatus("Board population idle", BoardPopulationStatusKind.Neutral);
                return;
            }

            if (!isRetryPass)
            {
                _activeBoardNames = new List<string>(cleanedNames);
            }

            var cacheStopwatch = Stopwatch.StartNew();
            var cachedIdentities = _resolverService.GetCached(cleanedNames);
            var cachedStats = _statsService.GetCachedForResolvedRows(cachedIdentities);
            cacheStopwatch.Stop();

            _diagnostics.CacheHydrateComplete(
                isRetryPass,
                cleanedNames.Count,
                cachedIdentities.Count,
                cachedStats.Count,
                cacheStopwatch.ElapsedMilliseconds);

            BuildInitialBoard(cleanedNames, cachedIdentities, cachedStats);

            var generation = ++_processingGeneration;
            var boardStopwatch = Stopwatch.StartNew();

            UpdateBoardPopulationStatus(
                isRetryPass ? "Board population retrying unresolved rows" : "Board population in progress",
                isRetryPass ? BoardPopulationStatusKind.Warning : BoardPopulationStatusKind.Neutral);

            _diagnostics.BoardProcessStart(generation, _currentRows.Count, isRetryPass);

            DebugTraceWriter.WriteLine(
                $"board process start: generation={generation}, rowCount={_currentRows.Count}, retryPass={isRetryPass}");

            await ProcessRowBatchAsync(_currentRows.ToList(), generation);

            boardStopwatch.Stop();

            if (generation == _processingGeneration)
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

            UpdateLastRefreshed();
            FinalizeBoardPopulationPass(generation);
        }

        private async Task ProcessRowBatchAsync(List<PilotBoardRow> rows, int generation)
        {
            using var semaphore = new SemaphoreSlim(6);

            var tasks = rows
                .Select(row => ProcessSingleRowAsync(row, semaphore, generation))
                .ToList();

            await Task.WhenAll(tasks);
        }

        private void FinalizeBoardPopulationPass(int generation)
        {
            if (generation != _processingGeneration)
            {
                _diagnostics.FinalizeSkipped(generation, _processingGeneration);
                return;
            }

            var retryableCount = CountRowsNeedingRetry();
            var completeCount = _currentRows.Count(IsCompleteRow);
            var partialCount = _currentRows.Count(IsPartialRow);

            if (retryableCount <= 0)
            {
                _activeBoardPopulationIncomplete = false;
                _activeBoardPopulationRetryScheduled = false;
                _activeBoardPopulationRetryAttempt = 0;

                var completionText = partialCount > 0
                    ? $"Board population complete ({completeCount} complete, {partialCount} partial)"
                    : "Board population complete";

                _diagnostics.BoardProcessFinalizedComplete(generation, completeCount, partialCount, retryableCount);

                UpdateBoardPopulationStatus(completionText, BoardPopulationStatusKind.Success);
                return;
            }

            _activeBoardPopulationIncomplete = true;
            _lastProcessedClipboardText = string.Empty;

            if (_activeBoardPopulationRetryAttempt >= MaxBoardPopulationRetryAttempts)
            {
                _diagnostics.BoardProcessRetryLimitReached(
                    generation,
                    retryableCount,
                    partialCount,
                    _activeBoardPopulationRetryAttempt);

                UpdateBoardPopulationStatus(
                    $"Board population incomplete — retry limit reached ({retryableCount} retryable, {partialCount} partial)",
                    BoardPopulationStatusKind.Error);
                return;
            }

            _diagnostics.BoardProcessRequiresRetry(
                generation,
                retryableCount,
                partialCount,
                _activeBoardPopulationRetryAttempt);

            UpdateBoardPopulationStatus(
                $"Board population delayed by source throttling or temporary failures ({retryableCount} retryable, {partialCount} partial)",
                BoardPopulationStatusKind.Warning);

            ScheduleBoardPopulationRetry();
        }

        private int CountRowsNeedingRetry()
        {
            return _currentRows.Count(HasRetryableStage);
        }

        private static bool HasRetryableStage(PilotBoardRow row)
        {
            if (row == null)
            {
                return false;
            }

            return IsRetryableStage(row.IdentityStage)
                || IsRetryableStage(row.AffiliationStage)
                || IsRetryableStage(row.StatsStage);
        }

        private static bool IsRetryableStage(EnrichmentStageState stage)
        {
            return stage == EnrichmentStageState.Throttled || stage == EnrichmentStageState.TemporaryFailure;
        }

        private static bool IsRetryReady(PilotBoardRow row, DateTime nowUtc)
        {
            if (!HasRetryableStage(row))
            {
                return false;
            }

            return !row.NextRetryAtUtc.HasValue || row.NextRetryAtUtc.Value <= nowUtc;
        }

        private static bool IsCompleteRow(PilotBoardRow row)
        {
            if (row == null)
            {
                return false;
            }

            return row.IdentityStage == EnrichmentStageState.Success
                && row.AffiliationStage == EnrichmentStageState.Success
                && row.StatsStage == EnrichmentStageState.Success;
        }

        private static bool IsPartialRow(PilotBoardRow row)
        {
            if (row == null)
            {
                return false;
            }

            if (IsCompleteRow(row) || HasRetryableStage(row))
            {
                return false;
            }

            return row.IdentityStage == EnrichmentStageState.Success
                || row.AffiliationStage == EnrichmentStageState.Success
                || row.StatsStage == EnrichmentStageState.Success
                || row.IdentityStage == EnrichmentStageState.NotFound;
        }

        private void ScheduleBoardPopulationRetry()
        {
            if (_activeBoardPopulationRetryScheduled)
            {
                _diagnostics.RetryScheduleIgnoredAlreadyScheduled();
                return;
            }

            var retryableRows = _currentRows.Where(HasRetryableStage).ToList();
            if (retryableRows.Count == 0)
            {
                _diagnostics.RetryScheduleIgnoredNoRows();
                return;
            }

            _activeBoardPopulationRetryScheduled = true;
            _activeBoardPopulationRetryAttempt++;

            var nowUtc = DateTime.UtcNow;
            var earliestRetryAtUtc = retryableRows
                .Select(row => row.NextRetryAtUtc ?? nowUtc.AddSeconds(DefaultBoardPopulationRetryDelaySeconds))
                .OrderBy(value => value)
                .First();

            var delay = earliestRetryAtUtc - nowUtc;
            if (delay < TimeSpan.Zero)
            {
                delay = TimeSpan.Zero;
            }

            _boardPopulationRetryCts?.Cancel();
            _boardPopulationRetryCts = new CancellationTokenSource();
            var retryToken = _boardPopulationRetryCts.Token;

            _diagnostics.RetryScheduled(
                _activeBoardPopulationRetryAttempt,
                (int)delay.TotalMilliseconds,
                retryableRows.Count);

            DebugTraceWriter.WriteLine(
                $"board population retry scheduled: attempt={_activeBoardPopulationRetryAttempt}, delayMs={(int)delay.TotalMilliseconds}, rowCount={retryableRows.Count}");

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delay, retryToken);

                    if (retryToken.IsCancellationRequested)
                    {
                        _diagnostics.RetryDelayCancelledBeforeDispatch();
                        return;
                    }

                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (retryToken.IsCancellationRequested)
                        {
                            _diagnostics.RetryDispatchSkippedBecauseCancelled();
                            return;
                        }

                        _diagnostics.RetryDispatchFired(_activeBoardPopulationRetryAttempt);

                        UpdateBoardPopulationStatus(
                            "Board population retrying delayed rows",
                            BoardPopulationStatusKind.Warning);

                        _ = ProcessRetryPassAsync();
                    });
                }
                catch (OperationCanceledException)
                {
                    _diagnostics.RetryDelayTaskCanceled();
                }
                finally
                {
                    _activeBoardPopulationRetryScheduled = false;
                }
            });
        }

        private async Task ProcessRetryPassAsync()
        {
            using var foregroundPriority = _backgroundIntelUpdateService.BeginForegroundPriority();

            var generation = _processingGeneration;
            var nowUtc = DateTime.UtcNow;
            var retryRows = _currentRows.Where(row => IsRetryReady(row, nowUtc)).ToList();

            if (retryRows.Count == 0)
            {
                _diagnostics.RetryPassSkipped(generation, 0);

                FinalizeBoardPopulationPass(generation);
                return;
            }

            _diagnostics.RetryPassStart(generation, retryRows.Count);

            DebugTraceWriter.WriteLine(
                $"board retry pass start: generation={generation}, rowCount={retryRows.Count}");

            var retryStopwatch = Stopwatch.StartNew();
            await ProcessRowBatchAsync(retryRows, generation);
            retryStopwatch.Stop();

            _diagnostics.RetryPassComplete(generation, retryRows.Count, retryStopwatch.ElapsedMilliseconds);

            UpdateLastRefreshed();
            FinalizeBoardPopulationPass(generation);
        }

        private void CancelBoardPopulationRetry()
        {
            try
            {
                _boardPopulationRetryCts?.Cancel();
                _diagnostics.RetryCancellationRequested();
            }
            catch
            {
            }

            _activeBoardPopulationRetryScheduled = false;
        }

        private void ResetBoardPopulationTracking()
        {
            _activeBoardPopulationIncomplete = false;
            _activeBoardPopulationRetryScheduled = false;
            _activeBoardPopulationRetryAttempt = 0;
            _activeBoardNames = new List<string>();
            _lastProcessedClipboardText = string.Empty;
            _diagnostics.BoardPopulationTrackingReset();
            UpdateBoardPopulationStatus("Board population in progress", BoardPopulationStatusKind.Neutral);
        }

        private void UpdateBoardPopulationStatus(string statusText, BoardPopulationStatusKind kind)
        {
            BoardPopulationStatusText.Text = statusText;
            _currentBoardPopulationStatusKind = kind;
            ApplyBoardPopulationStatusVisual();
        }

        private void ApplyBoardPopulationStatusVisual()
        {
            Brush brush = Resources["MutedTextBrush"] as Brush ?? Brushes.LightGray;

            switch (_currentBoardPopulationStatusKind)
            {
                case BoardPopulationStatusKind.Success:
                    brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));
                    break;
                case BoardPopulationStatusKind.Warning:
                    brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
                    break;
                case BoardPopulationStatusKind.Error:
                    brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
                    break;
            }

            BoardPopulationStatusText.Foreground = brush;
        }

        private async Task ProcessSingleRowAsync(PilotBoardRow row, SemaphoreSlim semaphore, int generation)
        {
            await semaphore.WaitAsync();

            try
            {
                if (generation != _processingGeneration)
                {
                    return;
                }

                var rowStopwatch = Stopwatch.StartNew();
                DebugTraceWriter.WriteLine(
                    $"row process start: generation={generation}, name='{row.CharacterName}'");

                ResolverCacheEntry? existingIdentity = null;

                if (!string.IsNullOrWhiteSpace(row.CharacterId) ||
                    string.Equals(row.ResolverConfidence, "not_found", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(row.ResolverConfidence, "esi_exact_fallback", StringComparison.OrdinalIgnoreCase))
                {
                    existingIdentity = new ResolverCacheEntry
                    {
                        CharacterName = row.CharacterName,
                        CharacterId = row.CharacterId,
                        AllianceName = row.AllianceName,
                        AllianceTicker = row.AllianceTicker,
                        CorpName = row.CorpName,
                        CorpTicker = row.CorpTicker,
                        ResolverConfidence = row.ResolverConfidence,
                        ResolvedAtUtc = row.ResolvedAtUtc,
                        ExpiresAtUtc = DateTime.UtcNow.AddDays(30).ToString("o"),
                        AffiliationCheckedAtUtc = row.AffiliationCheckedAtUtc
                    };
                }

                var identityOutcome = await _resolverService.ResolveCharacterAsync(row.CharacterName, existingIdentity);

                if (generation != _processingGeneration)
                {
                    return;
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    if (generation != _processingGeneration)
                    {
                        return;
                    }

                    ApplyIdentityOutcomeToRow(row, identityOutcome);
                });

                if (identityOutcome.Value != null)
                {
                    TryWriteFirstGenerationMarker(
                        ref _firstResolverLoggedGeneration,
                        generation,
                        $"first resolver value: generation={generation}, name='{row.CharacterName}', outcome={identityOutcome.Kind}, elapsedMs={rowStopwatch.ElapsedMilliseconds}");

                    TryWriteFirstGenerationMarker(
                        ref _firstIdentityUiLoggedGeneration,
                        generation,
                        $"first identity UI update: generation={generation}, name='{row.CharacterName}', outcome={identityOutcome.Kind}, elapsedMs={rowStopwatch.ElapsedMilliseconds}");
                }

                var effectiveIdentity = identityOutcome.Value;

                if (identityOutcome.Kind == ProviderOutcomeKind.NotFound ||
                    effectiveIdentity == null ||
                    string.IsNullOrWhiteSpace(effectiveIdentity.CharacterId))
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (generation != _processingGeneration)
                        {
                            return;
                        }

                        RecalculateRetryMetadata(row);
                        RefreshDetailPaneIfSelected(row);
                        UpdateLastRefreshed();
                    });
                    return;
                }

                var affiliationOutcome = await _resolverService.EnrichAffiliationIfNeededAsync(effectiveIdentity);

                if (generation != _processingGeneration)
                {
                    return;
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    if (generation != _processingGeneration)
                    {
                        return;
                    }

                    ApplyAffiliationOutcomeToRow(row, affiliationOutcome);
                });

                if (affiliationOutcome.Value != null && HasIdentityStageChange(effectiveIdentity, affiliationOutcome.Value))
                {
                    TryWriteFirstGenerationMarker(
                        ref _firstAffiliationLoggedGeneration,
                        generation,
                        $"first affiliation UI update: generation={generation}, name='{row.CharacterName}', outcome={affiliationOutcome.Kind}, elapsedMs={rowStopwatch.ElapsedMilliseconds}");
                }

                var statsIdentity = affiliationOutcome.Value ?? effectiveIdentity;

                if (statsIdentity == null || string.IsNullOrWhiteSpace(statsIdentity.CharacterId))
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (generation != _processingGeneration)
                        {
                            return;
                        }

                        RecalculateRetryMetadata(row);
                        RefreshDetailPaneIfSelected(row);
                        UpdateLastRefreshed();
                    });
                    return;
                }

                StatsCacheEntry? existingStats = null;

                if (row.KillCount.HasValue || row.LossCount.HasValue)
                {
                    existingStats = new StatsCacheEntry
                    {
                        CharacterId = statsIdentity.CharacterId,
                        KillCount = row.KillCount ?? 0,
                        LossCount = row.LossCount ?? 0,
                        AvgAttackersWhenAttacking = row.AvgAttackersWhenAttacking ?? 0,
                        LastPublicCynoCapableHull = row.LastPublicCynoCapableHull ?? "",
                        LastShipSeenName = row.LastShipSeenName ?? "",
                        LastShipSeenAtUtc = row.LastShipSeenAtUtc ?? "",
                        RefreshedAtUtc = DateTime.UtcNow.ToString("o"),
                        ExpiresAtUtc = DateTime.UtcNow.AddHours(12).ToString("o")
                    };
                }

                var statsOutcome = await _statsService.ResolveSingleAsync(statsIdentity.CharacterId, existingStats);

                if (generation != _processingGeneration)
                {
                    return;
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    if (generation != _processingGeneration)
                    {
                        return;
                    }

                    ApplyStatsOutcomeToRow(row, statsOutcome);
                });

                if (statsOutcome.Value != null)
                {
                    TryWriteFirstGenerationMarker(
                        ref _firstStatsLoggedGeneration,
                        generation,
                        $"first stats UI update: generation={generation}, name='{row.CharacterName}', outcome={statsOutcome.Kind}, elapsedMs={rowStopwatch.ElapsedMilliseconds}");
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        private void BuildInitialBoard(
            List<string> characterNames,
            Dictionary<string, ResolverCacheEntry> identities,
            Dictionary<string, StatsCacheEntry> stats)
        {
            _diagnostics.InitialBoardBuildStart(characterNames.Count, identities.Count, stats.Count);

            var buildStopwatch = Stopwatch.StartNew();
            var initialRows = _boardRowFactory.CreateRows(characterNames, identities, stats);

            _currentRows.Clear();

            foreach (var row in initialRows)
            {
                row.KnownCynoOverride = _notesRepository.GetKnownCynoOverride(row.CharacterName);
                row.BaitOverride = _notesRepository.GetBaitOverride(row.CharacterName);
                _currentRows.Add(row);
            }

            PilotBoard.SelectedItem = null;
            HideDetailPane();
            UpdateLastRefreshed();

            buildStopwatch.Stop();
            _diagnostics.InitialBoardBuildComplete(_currentRows.Count, buildStopwatch.ElapsedMilliseconds);
        }

        private void ApplyIdentityToRow(PilotBoardRow row, ResolverCacheEntry identity)
        {
            row.CharacterId = identity.CharacterId;
            row.AllianceName = identity.AllianceName;
            row.AllianceTicker = identity.AllianceTicker;
            row.CorpName = identity.CorpName;
            row.CorpTicker = identity.CorpTicker;
            row.IsResolved = !string.IsNullOrWhiteSpace(identity.CharacterId);
            row.ResolverConfidence = identity.ResolverConfidence;
            row.ResolvedAtUtc = identity.ResolvedAtUtc;
            row.AffiliationCheckedAtUtc = identity.AffiliationCheckedAtUtc;
        }

        private void ApplyStatsToRow(PilotBoardRow row, StatsCacheEntry stats)
        {
            row.KillCount = stats.KillCount;
            row.LossCount = stats.LossCount;
            row.AvgAttackersWhenAttacking = stats.AvgAttackersWhenAttacking > 0
                ? Math.Round(stats.AvgAttackersWhenAttacking, 0, MidpointRounding.AwayFromZero)
                : null;
            row.LastPublicCynoCapableHull = stats.LastPublicCynoCapableHull;
            row.LastShipSeenName = stats.LastShipSeenName;
            row.LastShipSeenAtUtc = stats.LastShipSeenAtUtc;
            row.LastShipSeenDateDisplay = FormatLastSeenDate(stats.LastShipSeenAtUtc);
        }

        private static string FormatLastSeenDate(string utcValue)
        {
            if (string.IsNullOrWhiteSpace(utcValue))
            {
                return "";
            }

            if (!DateTime.TryParse(
                    utcValue,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsed))
            {
                return "";
            }

            return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private void ApplyIdentityOutcomeToRow(PilotBoardRow row, ProviderOutcome<ResolverCacheEntry> outcome)
        {
            if (outcome.Value != null)
            {
                ApplyIdentityToRow(row, outcome.Value);
            }

            row.IdentityStage = MapOutcomeToStageState(outcome.Kind);
            row.IdentityStatusDetail = BuildOutcomeDetail(outcome, "identity");
            row.IdentityRetryAtUtc = GetRetryAtUtc(outcome);

            if (outcome.Kind == ProviderOutcomeKind.Throttled)
            {
                row.LastThrottleProvider = outcome.ProviderName;
            }

            if (outcome.Kind == ProviderOutcomeKind.NotFound)
            {
                row.AffiliationStage = EnrichmentStageState.Skipped;
                row.AffiliationStatusDetail = "Affiliation skipped after terminal miss";
                row.AffiliationRetryAtUtc = null;
                row.StatsStage = EnrichmentStageState.Skipped;
                row.StatsStatusDetail = "Stats skipped after terminal miss";
                row.StatsRetryAtUtc = null;
            }

            RecalculateRetryMetadata(row);
            RefreshDetailPaneIfSelected(row);
            UpdateLastRefreshed();
        }

        private void ApplyAffiliationOutcomeToRow(PilotBoardRow row, ProviderOutcome<ResolverCacheEntry> outcome)
        {
            if (outcome.Value != null)
            {
                ApplyIdentityToRow(row, outcome.Value);
            }

            row.AffiliationStage = MapOutcomeToStageState(outcome.Kind);
            row.AffiliationStatusDetail = BuildOutcomeDetail(outcome, "affiliation");
            row.AffiliationRetryAtUtc = GetRetryAtUtc(outcome);

            if (outcome.Kind == ProviderOutcomeKind.Throttled)
            {
                row.LastThrottleProvider = outcome.ProviderName;
            }

            RecalculateRetryMetadata(row);
            RefreshDetailPaneIfSelected(row);
            UpdateLastRefreshed();
        }

        private void ApplyStatsOutcomeToRow(PilotBoardRow row, ProviderOutcome<StatsCacheEntry> outcome)
        {
            if (outcome.Value != null)
            {
                ApplyStatsToRow(row, outcome.Value);
            }

            row.StatsStage = MapOutcomeToStageState(outcome.Kind);
            row.StatsStatusDetail = BuildOutcomeDetail(outcome, "stats");
            row.StatsRetryAtUtc = GetRetryAtUtc(outcome);

            if (outcome.Kind == ProviderOutcomeKind.Throttled)
            {
                row.LastThrottleProvider = outcome.ProviderName;
            }

            RecalculateRetryMetadata(row);
            RefreshDetailPaneIfSelected(row);
            UpdateLastRefreshed();
        }

        private void RefreshDetailPaneIfSelected(PilotBoardRow row)
        {
            if (DetailPane.Visibility != Visibility.Visible)
            {
                return;
            }

            if (!string.Equals(_activeDetailCharacterName, row.CharacterName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            SelectedCharacterText.Text = row.CharacterName;
            FullCorpText.Text = GetCorpDisplayText(row);
            FullAllianceText.Text = GetAllianceDisplayText(row);
            FreshnessText.Text = GetFreshnessDisplayText(row);
        }

        private void PilotBoard_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SaveCurrentNotesAndTags();

            if (PilotBoard.SelectedItem is PilotBoardRow selectedRow)
            {
                ShowDetailPane(selectedRow);
                AppLogger.UiInfo($"Board selection changed. character='{selectedRow.CharacterName}'");
                return;
            }

            HideDetailPane();
            AppLogger.UiInfo("Board selection cleared.");
        }

        private void PilotBoard_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PilotBoard.SelectedItem is not PilotBoardRow selectedRow)
            {
                return;
            }

            OpenZkillForRow(selectedRow);
            e.Handled = true;
        }

        private void CloseDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentNotesAndTags();

            AppLogger.UiInfo("Detail pane close requested.");

            PilotBoard.SelectedItem = null;
            HideDetailPane();
        }

        private void ClearBoardButton_Click(object sender, RoutedEventArgs e)
        {
            var clearedRowCount = _currentRows.Count;

            _diagnostics.ClearBoardStart(clearedRowCount);

            SaveCurrentNotesAndTags();
            CancelBoardPopulationRetry();
            _processingGeneration++;
            _activeBoardPopulationIncomplete = false;
            _activeBoardPopulationRetryScheduled = false;
            _activeBoardPopulationRetryAttempt = 0;
            _activeBoardNames = new List<string>();
            _lastProcessedClipboardText = string.Empty;

            PilotBoard.SelectedItem = null;
            _currentRows.Clear();
            HideDetailPane();

            UpdateLastRefreshed();
            UpdateBoardPopulationStatus("Board cleared", BoardPopulationStatusKind.Neutral);

            AppLogger.UiInfo($"Board cleared. removedRows={clearedRowCount}");
            _diagnostics.ClearBoardComplete();
        }

        private void OpenZkillButton_Click(object sender, RoutedEventArgs e)
        {
            if (PilotBoard.SelectedItem is not PilotBoardRow selectedRow)
            {
                AppLogger.UiWarn("Open zKill requested with no selected row.");
                return;
            }

            OpenZkillForRow(selectedRow);
        }

        private async void EnableKillmailDbPullButton_Click(object sender, RoutedEventArgs e)
        {
            await RunEnableKillmailDbPullAsync();
        }

        private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logsRootPath = AppPaths.GetLogsRootDirectory();

                AppLogger.UiInfo($"Open logs requested. path={logsRootPath}");
                _browserLauncher.OpenPath(logsRootPath);
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Open logs failed.", ex);

                MessageBox.Show(
                    $"Failed to open logs folder.\n\n{ex.Message}",
                    "PMG Logs Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        private void SaveMaxKillmailAgeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var rawValue = MaxKillmailAgeDaysTextBox.Text?.Trim() ?? string.Empty;

                if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDays))
                {
                    MessageBox.Show(
                        $"Enter a whole number between {KillmailDatasetFreshnessService.MinimumMaxKillmailAgeDays} and {KillmailDatasetFreshnessService.MaximumMaxKillmailAgeDays}.",
                        "PMG Max Killmail Age",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    MaxKillmailAgeDaysTextBox.Text = GetMaxKillmailAgeTextBoxText();
                    return;
                }

                var normalizedDays = KillmailDatasetFreshnessService.NormalizeMaxKillmailAgeDays(parsedDays);
                _appSettings.MaxKillmailAgeDays = normalizedDays;
                _appSettingsService.Save(_appSettings);
                MaxKillmailAgeDaysTextBox.Text = GetMaxKillmailAgeTextBoxText();
                UpdateMaxKillmailAgeUi();

                AppLogger.UiInfo($"Max killmail age saved. days={normalizedDays}");

                MessageBox.Show(
                    $"Max killmail age saved as {normalizedDays} day{(normalizedDays == 1 ? "" : "s")}. The new value will apply the next time you use Enable KillMail DB Pull.",
                    "PMG Max Killmail Age",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Failed to save max killmail age.", ex);

                MessageBox.Show(
                    $"Failed to save max killmail age.\n\n{ex.Message}",
                    "PMG Max Killmail Age Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void UseDefaultMaxKillmailAgeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _appSettings.MaxKillmailAgeDays = KillmailDatasetFreshnessService.DefaultMaxKillmailAgeDays;
                _appSettingsService.Save(_appSettings);
                MaxKillmailAgeDaysTextBox.Text = GetMaxKillmailAgeTextBoxText();
                UpdateMaxKillmailAgeUi();

                AppLogger.UiInfo($"Max killmail age reset to default. days={_appSettings.MaxKillmailAgeDays}");

                MessageBox.Show(
                    $"Max killmail age reset to the default of {_appSettings.MaxKillmailAgeDays} days.",
                    "PMG Max Killmail Age",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Failed to reset max killmail age to default.", ex);

                MessageBox.Show(
                    $"Failed to reset max killmail age.\n\n{ex.Message}",
                    "PMG Max Killmail Age Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ApplyLogLevelSelection()
        {
            if (LogLevelComboBox == null)
            {
                return;
            }

            LogLevelComboBox.SelectedIndex = _appSettings.LogLevel == AppLogLevel.Debug ? 1 : 0;
        }

        private AppLogLevel GetSelectedLogLevel()
        {
            if (LogLevelComboBox == null)
            {
                return AppLogLevel.Normal;
            }

            return LogLevelComboBox.SelectedIndex == 1
                ? AppLogLevel.Debug
                : AppLogLevel.Normal;
        }

        private int GetMaxKillmailAgeDaysSettingValue()
        {
            return KillmailDatasetFreshnessService.NormalizeMaxKillmailAgeDays(_appSettings.MaxKillmailAgeDays);
        }

        private string GetMaxKillmailAgeTextBoxText()
        {
            return GetMaxKillmailAgeDaysSettingValue().ToString(CultureInfo.InvariantCulture);
        }

        private void UpdateMaxKillmailAgeUi()
        {
            var days = GetMaxKillmailAgeDaysSettingValue();
            var suffix = days == 1 ? "day" : "days";

            EffectiveMaxKillmailAgeText.Text = $"Effective max killmail age: {days} {suffix}";
        }

        private void SaveKillmailPathButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var rawValue = KillmailDataRootPathTextBox.Text?.Trim() ?? string.Empty;
                var normalizedDefaultPath = KillmailPaths.NormalizeForComparison(KillmailPaths.GetDefaultKillmailDataDirectoryDisplayPath());

                if (string.IsNullOrWhiteSpace(rawValue))
                {
                    _appSettings.KillmailDataRootPath = string.Empty;
                    _appSettingsService.Save(_appSettings);
                    KillmailDataRootPathTextBox.Text = GetKillmailPathEditorText();
                    UpdateKillmailPathUi();

                    AppLogger.UiInfo("Killmail data path override cleared via blank save. Restart required.");

                    MessageBox.Show(
                        "Killmail data path reset to the default %LOCALAPPDATA% location. Restart PMG to apply the new path fully.",
                        "PMG Killmail Data Path",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                var normalizedPath = KillmailPaths.NormalizeForComparison(rawValue);

                if (string.Equals(normalizedPath, normalizedDefaultPath, StringComparison.OrdinalIgnoreCase))
                {
                    _appSettings.KillmailDataRootPath = string.Empty;
                }
                else
                {
                    var expandedPath = KillmailPaths.ExpandPathTokens(rawValue);
                    Directory.CreateDirectory(expandedPath);

                    _appSettings.KillmailDataRootPath = rawValue;
                }

                _appSettingsService.Save(_appSettings);
                KillmailDataRootPathTextBox.Text = GetKillmailPathEditorText();
                UpdateKillmailPathUi();

                AppLogger.UiInfo(
                    $"Killmail data path saved. configuredValue='{_appSettings.KillmailDataRootPath ?? string.Empty}' displayPath='{KillmailPaths.GetKillmailDataDirectoryDisplayPath()}' source={KillmailPaths.GetKillmailDataDirectorySourceDescription()} restartRequired=true");

                MessageBox.Show(
                    "Killmail data path saved. Restart PMG to apply the new path fully. Existing killmail data is not migrated automatically.",
                    "PMG Killmail Data Path",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Failed to save killmail data path.", ex);

                MessageBox.Show(
                    $"Failed to save killmail data path.\n\n{ex.Message}",
                    "PMG Killmail Data Path Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void UseDefaultKillmailPathButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _appSettings.KillmailDataRootPath = string.Empty;
                _appSettingsService.Save(_appSettings);
                KillmailDataRootPathTextBox.Text = GetKillmailPathEditorText();
                UpdateKillmailPathUi();

                AppLogger.UiInfo("Killmail data path reset to default %LOCALAPPDATA% location. Restart required.");

                MessageBox.Show(
                    "Killmail data path reset to the default %LOCALAPPDATA% location. Restart PMG to apply the new path fully.",
                    "PMG Killmail Data Path",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Failed to reset killmail data path to default.", ex);

                MessageBox.Show(
                    $"Failed to reset killmail data path.\n\n{ex.Message}",
                    "PMG Killmail Data Path Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void UpdateKillmailPathUi()
        {
            var displayPath = KillmailPaths.GetKillmailDataDirectoryDisplayPath();
            var sourceDescription = KillmailPaths.GetKillmailDataDirectorySourceDescription();

            KillmailDataPathModeText.Text = $"Source: {sourceDescription}";
            EffectiveKillmailDataPathText.Text = $"Effective path: {displayPath}";
        }

        private string GetKillmailPathEditorText()
        {
            if (!string.IsNullOrWhiteSpace(_appSettings.KillmailDataRootPath))
            {
                return _appSettings.KillmailDataRootPath;
            }

            return KillmailPaths.GetDefaultKillmailDataDirectoryDisplayPath();
        }

        private void OpenZkillForRow(PilotBoardRow selectedRow)
        {
            try
            {
                var url = string.IsNullOrWhiteSpace(selectedRow.CharacterId)
                    ? _zkillUrlBuilder.BuildSearchUrl(selectedRow.CharacterName)
                    : _zkillUrlBuilder.BuildCharacterUrl(selectedRow.CharacterId);

                AppLogger.UiInfo(
                    $"Opening zKill. character='{selectedRow.CharacterName}' characterId='{selectedRow.CharacterId ?? ""}'");

                _browserLauncher.OpenUrl(url);
            }
            catch (Exception ex)
            {
                AppLogger.UiError(
                    $"Failed to open zKill. character='{selectedRow?.CharacterName ?? ""}'",
                    ex);

                MessageBox.Show(
                    $"Failed to open browser.\n\n{ex.Message}",
                    "PMG Browser Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && DetailPane.Visibility == Visibility.Visible)
            {
                SaveCurrentNotesAndTags();

                AppLogger.UiInfo("Escape pressed. Closing detail pane.");

                PilotBoard.SelectedItem = null;
                HideDetailPane();
                e.Handled = true;
            }
        }

        private void ShowDetailPane(PilotBoardRow row)
        {
            _activeDetailCharacterName = row.CharacterName;
            SelectedCharacterText.Text = row.CharacterName;
            FullCorpText.Text = GetCorpDisplayText(row);
            FullAllianceText.Text = GetAllianceDisplayText(row);
            FreshnessText.Text = GetFreshnessDisplayText(row);

            _isApplyingDetailPaneState = true;
            NotesTagsBox.Text = _notesRepository.GetNotes(row.CharacterName);
            KnownCynoOverrideCheckBox.IsChecked = row.KnownCynoOverride;
            BaitOverrideCheckBox.IsChecked = row.BaitOverride;
            _isApplyingDetailPaneState = false;

            DetailPane.Visibility = Visibility.Visible;
        }

        private string GetCorpDisplayText(PilotBoardRow row)
        {
            if (row.IdentityStage == EnrichmentStageState.NotFound)
            {
                return "Full Corp: not found on zKill or ESI exact match";
            }

            if (!string.IsNullOrWhiteSpace(row.CorpName))
            {
                return $"Full Corp: {row.CorpName}";
            }

            if (row.AffiliationStage == EnrichmentStageState.Success || row.AffiliationStage == EnrichmentStageState.NotFound)
            {
                return "Full Corp: unavailable after affiliation check";
            }

            if (row.AffiliationStage == EnrichmentStageState.Throttled || row.AffiliationStage == EnrichmentStageState.TemporaryFailure)
            {
                return $"Full Corp: delayed ({row.AffiliationStatusDetail})";
            }

            if (row.AffiliationStage == EnrichmentStageState.PermanentFailure)
            {
                return $"Full Corp: unavailable ({row.AffiliationStatusDetail})";
            }

            if (!string.IsNullOrWhiteSpace(row.CharacterId))
            {
                return "Full Corp: resolved, enrichment pending";
            }

            return "Full Corp: unresolved";
        }

        private string GetAllianceDisplayText(PilotBoardRow row)
        {
            if (row.IdentityStage == EnrichmentStageState.NotFound)
            {
                return "Full Alliance: not found on zKill or ESI exact match";
            }

            if (!string.IsNullOrWhiteSpace(row.AllianceName))
            {
                return $"Full Alliance: {row.AllianceName}";
            }

            if (row.AffiliationStage == EnrichmentStageState.Success || row.AffiliationStage == EnrichmentStageState.NotFound)
            {
                return "Full Alliance: none";
            }

            if (row.AffiliationStage == EnrichmentStageState.Throttled || row.AffiliationStage == EnrichmentStageState.TemporaryFailure)
            {
                return $"Full Alliance: delayed ({row.AffiliationStatusDetail})";
            }

            if (row.AffiliationStage == EnrichmentStageState.PermanentFailure)
            {
                return $"Full Alliance: unavailable ({row.AffiliationStatusDetail})";
            }

            if (!string.IsNullOrWhiteSpace(row.CharacterId))
            {
                return "Full Alliance: resolved, enrichment pending";
            }

            return "Full Alliance: unresolved";
        }

        private string GetFreshnessDisplayText(PilotBoardRow row)
        {
            if (row.KnownCynoOverride)
            {
                return "Freshness: known-cyno override applied";
            }

            if (row.BaitOverride)
            {
                return "Freshness: bait override applied";
            }

            if (row.IdentityStage == EnrichmentStageState.NotFound)
            {
                return "Freshness: terminal miss cached";
            }

            if (HasRetryableStage(row) && row.NextRetryAtUtc.HasValue)
            {
                return $"Freshness: retry scheduled for {row.NextRetryAtUtc.Value:O}";
            }

            if (string.Equals(row.ResolverConfidence, "esi_exact_fallback", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(row.ResolvedAtUtc))
                {
                    return $"Freshness: resolved by ESI exact fallback at {row.ResolvedAtUtc}";
                }

                return "Freshness: resolved by ESI exact fallback";
            }

            if (row.StatsStage == EnrichmentStageState.NotFound)
            {
                return "Freshness: identity resolved; stats unavailable from current sources";
            }

            if (!string.IsNullOrWhiteSpace(row.ResolvedAtUtc))
            {
                return $"Freshness: {row.ResolvedAtUtc}";
            }

            return "Freshness: unresolved";
        }

        private void HideDetailPane()
        {
            _activeDetailCharacterName = string.Empty;
            _isApplyingDetailPaneState = true;
            NotesTagsBox.Text = string.Empty;
            KnownCynoOverrideCheckBox.IsChecked = false;
            BaitOverrideCheckBox.IsChecked = false;
            _isApplyingDetailPaneState = false;
            DetailPane.Visibility = Visibility.Collapsed;
        }

        private void SaveCurrentNotesAndTags()
        {
            if (string.IsNullOrWhiteSpace(_activeDetailCharacterName))
            {
                return;
            }

            var knownCynoOverride = KnownCynoOverrideCheckBox.IsChecked == true;
            var baitOverride = BaitOverrideCheckBox.IsChecked == true;

            _notesRepository.SaveNotesAndTags(
                _activeDetailCharacterName,
                NotesTagsBox.Text,
                knownCynoOverride,
                baitOverride);

            if (PilotBoard.SelectedItem is PilotBoardRow selectedRow &&
                string.Equals(selectedRow.CharacterName, _activeDetailCharacterName, StringComparison.OrdinalIgnoreCase))
            {
                selectedRow.KnownCynoOverride = knownCynoOverride;
                selectedRow.BaitOverride = baitOverride;
            }
        }

        private static EnrichmentStageState MapOutcomeToStageState(ProviderOutcomeKind outcomeKind)
        {
            return outcomeKind switch
            {
                ProviderOutcomeKind.Success => EnrichmentStageState.Success,
                ProviderOutcomeKind.NotFound => EnrichmentStageState.NotFound,
                ProviderOutcomeKind.Throttled => EnrichmentStageState.Throttled,
                ProviderOutcomeKind.TemporaryFailure => EnrichmentStageState.TemporaryFailure,
                ProviderOutcomeKind.PermanentFailure => EnrichmentStageState.PermanentFailure,
                ProviderOutcomeKind.Skipped => EnrichmentStageState.Skipped,
                _ => EnrichmentStageState.NotStarted
            };
        }

        private static DateTime? GetRetryAtUtc<T>(ProviderOutcome<T> outcome)
        {
            if (outcome.Kind == ProviderOutcomeKind.Throttled)
            {
                return outcome.RetryAfterUtc ?? DateTime.UtcNow.AddSeconds(DefaultBoardPopulationRetryDelaySeconds);
            }

            if (outcome.Kind == ProviderOutcomeKind.TemporaryFailure)
            {
                return outcome.RetryAfterUtc ?? DateTime.UtcNow.AddSeconds(DefaultBoardPopulationRetryDelaySeconds);
            }

            return null;
        }

        private static string BuildOutcomeDetail<T>(ProviderOutcome<T> outcome, string stageName)
        {
            if (!string.IsNullOrWhiteSpace(outcome.Detail))
            {
                return outcome.Detail;
            }

            return outcome.Kind switch
            {
                ProviderOutcomeKind.Success => $"{stageName} succeeded",
                ProviderOutcomeKind.NotFound => $"{stageName} returned not found",
                ProviderOutcomeKind.Throttled => $"{stageName} delayed by throttling",
                ProviderOutcomeKind.TemporaryFailure => $"{stageName} temporarily failed",
                ProviderOutcomeKind.PermanentFailure => $"{stageName} permanently failed",
                ProviderOutcomeKind.Skipped => $"{stageName} skipped",
                _ => $"{stageName} not started"
            };
        }

        private static DateTime? GetEarlierRetryAtUtc(params DateTime?[] retryAtCandidates)
        {
            return retryAtCandidates
                .Where(candidate => candidate.HasValue)
                .Select(candidate => candidate!.Value)
                .OrderBy(candidate => candidate)
                .Cast<DateTime?>()
                .FirstOrDefault();
        }

        private void RecalculateRetryMetadata(PilotBoardRow row)
        {
            row.NextRetryAtUtc = GetEarlierRetryAtUtc(row.IdentityRetryAtUtc, row.AffiliationRetryAtUtc, row.StatsRetryAtUtc);

            if (!HasRetryableStage(row))
            {
                row.NextRetryAtUtc = null;
                if (row.IdentityStage != EnrichmentStageState.Throttled &&
                    row.AffiliationStage != EnrichmentStageState.Throttled &&
                    row.StatsStage != EnrichmentStageState.Throttled)
                {
                    row.LastThrottleProvider = string.Empty;
                }
            }
        }

        private void UpdateLastRefreshed()
        {
            LastRefreshedText.Text = $"Last Refreshed: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        }

        private async Task RunEnableKillmailDbPullAsync()
        {
            try
            {
                EnableKillmailDbPullButton.IsEnabled = false;

                var seedDays = GetMaxKillmailAgeDaysSettingValue();

                AppLogger.UiInfo(
                    $"Enable KillMail DB Pull requested. seedDays={seedDays} displayKillmailPath={KillmailPaths.GetKillmailDataDirectoryDisplayPath()} source={KillmailPaths.GetKillmailDataDirectorySourceDescription()}");

                await _backgroundIntelUpdateService.EnableKillmailDbPullAsync(seedDays, CancellationToken.None);

                AppLogger.UiInfo($"Enable KillMail DB Pull completed successfully. seedDays={seedDays}");
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Enable KillMail DB Pull failed.", ex);

                MessageBox.Show(
                    $"Failed to enable killmail DB pull.\n\n{ex.Message}",
                    "PMG Killmail DB Pull Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                EnableKillmailDbPullButton.IsEnabled = true;
            }
        }

        private void TryWriteFirstGenerationMarker(ref int markerField, int generation, string message)
        {
            lock (_timingMarkerSync)
            {
                if (markerField == generation)
                {
                    return;
                }

                markerField = generation;
                DebugTraceWriter.WriteLine(message);
            }
        }

        private static bool HasIdentityStageChange(ResolverCacheEntry before, ResolverCacheEntry after)
        {
            return !string.Equals(before.CharacterId, after.CharacterId, StringComparison.Ordinal)
                || !string.Equals(before.CorpName, after.CorpName, StringComparison.Ordinal)
                || !string.Equals(before.CorpTicker, after.CorpTicker, StringComparison.Ordinal)
                || !string.Equals(before.AllianceName, after.AllianceName, StringComparison.Ordinal)
                || !string.Equals(before.AllianceTicker, after.AllianceTicker, StringComparison.Ordinal)
                || !string.Equals(before.ResolverConfidence, after.ResolverConfidence, StringComparison.Ordinal)
                || !string.Equals(before.AffiliationCheckedAtUtc, after.AffiliationCheckedAtUtc, StringComparison.Ordinal);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        private enum BoardPopulationStatusKind
        {
            Neutral,
            Success,
            Warning,
            Error
        }
    }
}
