using PitmastersGrill.Diagnostics;
using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using PitmastersGrill.Providers;
using PitmastersGrill.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Navigation;
using PitmastersGrill.Views;

namespace PitmastersGrill
{
    public partial class MainWindow : Window
    {
        private const int WmClipboardUpdate = 0x031D;
        private const int DefaultBoardPopulationRetryDelaySeconds = 12;
        private const int MaxBoardPopulationRetryAttempts = 5;

        private readonly BackgroundIntelUpdateService _backgroundIntelUpdateService;
        private AppSettings _appSettings = new();

        private readonly BoardRowFactory _boardRowFactory;
        private readonly DetailPaneController _detailPaneController;
        private readonly MainWindowAppearanceController _mainWindowAppearanceController;
        private readonly BoardPopulationStatusController _boardPopulationStatusController;
        private readonly BoardPopulationRowProcessor _boardPopulationRowProcessor;
        private readonly BoardPopulationPassController _boardPopulationPassController;
        private readonly BoardPopulationRetryController _boardPopulationRetryController;
        private readonly BoardPopulationEntryController _boardPopulationEntryController;
        private readonly NotesRepository _notesRepository;
        private readonly ZkillUrlBuilder _zkillUrlBuilder;
        private readonly BrowserLauncher _browserLauncher;
        private readonly MainWindowDiagnostics _diagnostics;
        private readonly IntelUpdateBannerController _intelUpdateBannerController;
        private readonly BoardPopulationTimingMarkerTracker _boardPopulationTimingMarkerTracker;
        private readonly IgnoreAllianceCoordinator _ignoreAllianceCoordinator;
        private readonly IgnoreAllianceBoardController _ignoreAllianceBoardController;
        private IgnoreAllianceListView? _ignoreAllianceListView;


        private readonly ObservableCollection<PilotBoardRow> _currentRows = new();
        private bool _isApplyingSettings;
        private int _processingGeneration;

        public MainWindow(BackgroundIntelUpdateService backgroundIntelUpdateService)
        {
            _backgroundIntelUpdateService = backgroundIntelUpdateService;
            _backgroundIntelUpdateService.StatusChanged += OnIntelUpdateStatusChanged;

            var appSettingsService = new AppSettingsService();
            _mainWindowAppearanceController = new MainWindowAppearanceController(appSettingsService);
            _boardPopulationStatusController = new BoardPopulationStatusController();

            _isApplyingSettings = true;
            InitializeComponent();

            _diagnostics = new MainWindowDiagnostics(Dispatcher);
            _intelUpdateBannerController = new IntelUpdateBannerController(Dispatcher);
            _boardPopulationTimingMarkerTracker = new BoardPopulationTimingMarkerTracker();

            AppLogger.UiInfo("MainWindow InitializeComponent complete.");

            var composed = MainWindowCompositionRoot.Compose(
                _diagnostics,
                appSettingsService,
                _mainWindowAppearanceController,
                _boardPopulationStatusController,
                DefaultBoardPopulationRetryDelaySeconds);

            _boardRowFactory = composed.BoardRowFactory;
            _notesRepository = composed.NotesRepository;
            _detailPaneController = composed.DetailPaneController;
            _boardPopulationRowProcessor = composed.BoardPopulationRowProcessor;
            _boardPopulationPassController = composed.BoardPopulationPassController;
            _boardPopulationRetryController = composed.BoardPopulationRetryController;
            _boardPopulationEntryController = composed.BoardPopulationEntryController;
            _ignoreAllianceCoordinator = composed.IgnoreAllianceCoordinator;
            _ignoreAllianceBoardController = composed.IgnoreAllianceBoardController;
            _zkillUrlBuilder = composed.ZkillUrlBuilder;
            _browserLauncher = composed.BrowserLauncher;

            _ignoreAllianceListView = IgnoreAllianceListViewControl;
            _ignoreAllianceListView.Initialize(_ignoreAllianceCoordinator);
            _ignoreAllianceListView.IgnoreListChanged += IgnoreAllianceListView_IgnoreListChanged;

            try
            {
                composed.DatabaseBootstrap.Initialize();
                DebugTraceWriter.Clear();
                AppLogger.DatabaseInfo($"MainWindow local database initialized. path={composed.DatabasePath}");
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

            _appSettings = appSettingsService.Load();

            _mainWindowAppearanceController.InitializeSettingsUi(
                _appSettings,
                DarkModeCheckBox,
                AlwaysOnTopCheckBox,
                WindowOpacitySlider,
                WindowOpacityValueText,
                MaxKillmailAgeDaysTextBox,
                EffectiveMaxKillmailAgeText,
                KillmailDataRootPathTextBox,
                KillmailDataPathModeText,
                EffectiveKillmailDataPathText,
                LogLevelComboBox);

            AppLogger.ConfigureLogLevel(_appSettings.LogLevel);

            _isApplyingSettings = false;

            _mainWindowAppearanceController.ApplyTheme(Resources, _appSettings, this, ApplyBoardPopulationStatusVisual);
            _mainWindowAppearanceController.ApplyWindowSettings(this, _appSettings, WindowOpacityValueText, Resources);

            PilotBoard.ItemsSource = _currentRows;
            UpdateLastRefreshed();
            UpdateBoardPopulationStatus("Board population idle", BoardPopulationStatusKind.Neutral);
            HideDetailPane();
            ApplyIntelUpdateSnapshot(_backgroundIntelUpdateService.GetSnapshot());

            AppLogger.DatabaseInfo(
                $"Killmail data path resolved. displayPath={KillmailPaths.GetKillmailDataDirectoryDisplayPath()} source={KillmailPaths.GetKillmailDataDirectorySourceDescription()}");

            AppLogger.UiInfo(
                $"MainWindow ready. darkMode={_appSettings.DarkModeEnabled} alwaysOnTop={_appSettings.AlwaysOnTopEnabled} opacityPercent={_mainWindowAppearanceController.CoerceOpacityPercent(_appSettings.WindowOpacityPercent):0} logLevel={_appSettings.LogLevel}");
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwnd = new WindowInteropHelper(this).Handle;
            AddClipboardFormatListener(hwnd);

            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);

            _mainWindowAppearanceController.ApplyTitleBarTheme(this, _appSettings.DarkModeEnabled);

            AppLogger.UiInfo("MainWindow source initialized. Clipboard listener attached and title bar theme applied.");
        }

        protected override void OnClosed(EventArgs e)
        {
            AppLogger.UiInfo("MainWindow closing requested.");

            SaveCurrentNotesAndTags();
            CancelBoardPopulationRetry();
            if (_ignoreAllianceListView != null)
            {
                _ignoreAllianceListView.IgnoreListChanged -= IgnoreAllianceListView_IgnoreListChanged;
            }
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

            _mainWindowAppearanceController.HandleDarkModeChanged(
                _appSettings,
                DarkModeCheckBox.IsChecked == true,
                Resources,
                this,
                ApplyBoardPopulationStatusVisual);
        }

        private void AlwaysOnTopCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isApplyingSettings)
            {
                return;
            }

            _mainWindowAppearanceController.HandleAlwaysOnTopChanged(
                _appSettings,
                AlwaysOnTopCheckBox.IsChecked == true,
                this,
                WindowOpacityValueText,
                Resources);
        }

        private void WindowOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_mainWindowAppearanceController == null)
            {
                return;
            }

            var opacityPercent = _mainWindowAppearanceController.CoerceOpacityPercent(WindowOpacitySlider.Value);

            if (WindowOpacityValueText != null)
            {
                WindowOpacityValueText.Text = $"{opacityPercent:0}%";
            }

            if (_isApplyingSettings)
            {
                return;
            }

            _mainWindowAppearanceController.HandleWindowOpacityChanged(
                _appSettings,
                WindowOpacitySlider.Value,
                this,
                WindowOpacityValueText,
                Resources);
        }

        private void LogLevelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isApplyingSettings || LogLevelComboBox == null)
            {
                return;
            }

            _mainWindowAppearanceController.HandleLogLevelChanged(_appSettings, LogLevelComboBox);
        }

        private void KnownCynoOverrideCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            var selectedRow = PilotBoard.SelectedItem as PilotBoardRow;
            var applied = _detailPaneController.TryApplyKnownCynoOverrideChange(
                KnownCynoOverrideCheckBox.IsChecked == true,
                NotesTagsBox.Text,
                BaitOverrideCheckBox.IsChecked == true,
                selectedRow);

            if (applied && selectedRow != null)
            {
                AppLogger.UiInfo(
                    $"Known cyno override changed. character='{selectedRow.CharacterName}' enabled={selectedRow.KnownCynoOverride}");
            }
        }

        private void BaitOverrideCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            var selectedRow = PilotBoard.SelectedItem as PilotBoardRow;
            var applied = _detailPaneController.TryApplyBaitOverrideChange(
                KnownCynoOverrideCheckBox.IsChecked == true,
                NotesTagsBox.Text,
                BaitOverrideCheckBox.IsChecked == true,
                selectedRow);

            if (applied && selectedRow != null)
            {
                AppLogger.UiInfo(
                    $"Bait override changed. character='{selectedRow.CharacterName}' enabled={selectedRow.BaitOverride}");
            }
        }

        private void OnIntelUpdateStatusChanged(IntelUpdateStatusSnapshot snapshot)
        {
            _intelUpdateBannerController.HandleStatusChanged(
                snapshot,
                IntelUpdateBanner,
                IntelUpdateStatusText,
                IntelUpdateDetailText);
        }

        private void ApplyIntelUpdateSnapshot(IntelUpdateStatusSnapshot snapshot)
        {
            _intelUpdateBannerController.ApplySnapshot(
                snapshot,
                IntelUpdateBanner,
                IntelUpdateStatusText,
                IntelUpdateDetailText);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmClipboardUpdate)
            {
                _ = ProcessClipboardIfValidAsync();
            }

            return IntPtr.Zero;
        }

        private Task ProcessClipboardIfValidAsync()
        {
            return _boardPopulationEntryController.ProcessClipboardIfValidAsync(
                clipboardContainsText: () => Clipboard.ContainsText(),
                clipboardGetText: () => Clipboard.GetText(),
                setBoardButtonsEnabled: enabled =>
                {
                    EnableKillmailDbPullButton.IsEnabled = enabled;
                    ClearBoardButton.IsEnabled = enabled;
                },
                beginForegroundPriority: () => _backgroundIntelUpdateService.BeginForegroundPriority(),
                cancelBoardPopulationRetry: CancelBoardPopulationRetry,
                resetBoardPopulationTracking: preserveLastProcessedClipboardText => ResetBoardPopulationTracking(preserveLastProcessedClipboardText),
                processNamesAsync: ProcessNamesAsync);
        }

        private Task ProcessNamesAsync(List<string> characterNames, bool isRetryPass)
        {
            return _boardPopulationEntryController.ProcessNamesAsync(
                characterNames,
                isRetryPass,
                SaveCurrentNotesAndTags,
                BuildInitialBoard,
                beginProcessingGeneration: () => ++_processingGeneration,
                getCurrentGeneration: () => _processingGeneration,
                getCurrentRowCount: () => _currentRows.Count,
                processCurrentRowsAsync: generation => ProcessRowBatchAsync(_currentRows.ToList(), generation),
                updateBoardPopulationStatus: UpdateBoardPopulationStatus,
                updateLastRefreshed: UpdateLastRefreshed,
                finalizeBoardPopulationPass: FinalizeBoardPopulationPass);
        }

        private Task ProcessRowBatchAsync(List<PilotBoardRow> rows, int generation)
        {
            return _boardPopulationPassController.ProcessRowBatchAsync(
                rows,
                generation,
                ProcessSingleRowAsync);
        }

        private void FinalizeBoardPopulationPass(int generation)
        {
            if (generation != _processingGeneration)
            {
                _diagnostics.FinalizeSkipped(generation, _processingGeneration);
                return;
            }

            var decision = _boardPopulationPassController.BuildFinalizeDecision(
                _currentRows,
                _boardPopulationRetryController.RetryAttempt,
                MaxBoardPopulationRetryAttempts);

            if (decision.IsComplete)
            {
                _boardPopulationRetryController.MarkComplete();

                _diagnostics.BoardProcessFinalizedComplete(
                    generation,
                    decision.CompleteCount,
                    decision.PartialCount,
                    decision.RetryableCount);

                UpdateBoardPopulationStatus(decision.StatusText, decision.StatusKind);
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

                UpdateBoardPopulationStatus(decision.StatusText, decision.StatusKind);
                return;
            }

            _diagnostics.BoardProcessRequiresRetry(
                generation,
                decision.RetryableCount,
                decision.PartialCount,
                _boardPopulationRetryController.RetryAttempt);

            UpdateBoardPopulationStatus(decision.StatusText, decision.StatusKind);

            if (decision.ShouldScheduleRetry)
            {
                ScheduleBoardPopulationRetry();
            }
        }

        private void ScheduleBoardPopulationRetry()
        {
            _boardPopulationRetryController.ScheduleRetry(
                _currentRows,
                Dispatcher,
                UpdateBoardPopulationStatus,
                ProcessRetryPassAsync);
        }

        private Task ProcessRetryPassAsync()
        {
            return _boardPopulationRetryController.ProcessRetryPassAsync(
                _currentRows,
                () => _backgroundIntelUpdateService.BeginForegroundPriority(),
                (rows, generation) => ProcessRowBatchAsync(rows.ToList(), generation),
                () => _processingGeneration,
                UpdateLastRefreshed,
                FinalizeBoardPopulationPass);
        }

        private void CancelBoardPopulationRetry()
        {
            _boardPopulationRetryController.CancelRetry();
        }

        private void ResetBoardPopulationTracking(bool preserveLastProcessedClipboardText = false)
        {
            ResetEntryAndRetryTracking(preserveLastProcessedClipboardText);
            UpdateBoardPopulationStatus("Board population in progress", BoardPopulationStatusKind.Neutral);
        }

        private void ResetEntryAndRetryTracking(bool preserveLastProcessedClipboardText = false)
        {
            _boardPopulationEntryController.ResetTracking(preserveLastProcessedClipboardText);
            _boardPopulationRetryController.ResetTracking();
        }

        private void UpdateBoardPopulationStatus(string statusText, BoardPopulationStatusKind kind)
        {
            _boardPopulationStatusController.UpdateStatus(
                statusText,
                kind,
                BoardPopulationStatusText,
                Resources);
        }

        private void ApplyBoardPopulationStatusVisual()
        {
            _boardPopulationStatusController.ApplyStatusVisual(
                BoardPopulationStatusText,
                Resources);
        }

        private async Task ProcessSingleRowAsync(PilotBoardRow row, SemaphoreSlim semaphore, int generation)
        {
            await semaphore.WaitAsync();

            try
            {
                await _boardPopulationRowProcessor.ProcessAsync(
                    row,
                    generation,
                    () => _processingGeneration,
                    action => Dispatcher.InvokeAsync(() =>
                    {
                        if (generation != _processingGeneration)
                        {
                            return;
                        }

                        action();
                    }).Task,
                    RefreshDetailPaneIfSelected,
                    UpdateLastRefreshed,
                    (markerKind, message) => HandleRowProcessorMarker(markerKind, generation, message));

                if (_ignoreAllianceBoardController.ShouldRemoveResolvedRow(row))
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (generation != _processingGeneration)
                        {
                            return;
                        }

                        RemoveIgnoredAllianceRowFromCurrentBoard(row);
                    });
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        private void HandleRowProcessorMarker(BoardRowProcessMarkerKind markerKind, int generation, string message)
        {
            _boardPopulationTimingMarkerTracker.HandleMarker(markerKind, generation, message);
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

            ApplyIgnoredAllianceRowsToCurrentBoard();

            PilotBoard.SelectedItem = null;
            HideDetailPane();
            UpdateLastRefreshed();

            buildStopwatch.Stop();
            _diagnostics.InitialBoardBuildComplete(_currentRows.Count, buildStopwatch.ElapsedMilliseconds);
        }


        private void RemoveIgnoredAllianceRowFromCurrentBoard(PilotBoardRow row)
        {
            if (row == null)
            {
                return;
            }

            var removed = _currentRows.Remove(row);
            if (!removed)
            {
                return;
            }

            if (ReferenceEquals(PilotBoard.SelectedItem, row))
            {
                PilotBoard.SelectedItem = null;
                HideDetailPane();
            }

            AppLogger.UiInfo($"Ignored alliance filter removed a resolved row from current board. character='{row.CharacterName}' allianceId='{row.AllianceId}'");
        }

        private void ApplyIgnoredAllianceRowsToCurrentBoard()
        {
            var selectedRow = PilotBoard.SelectedItem as PilotBoardRow;
            var applyResult = _ignoreAllianceBoardController.ApplyToCurrentRows(_currentRows, selectedRow);

            if (applyResult.RemovedCount == 0)
            {
                return;
            }

            foreach (var removedRow in applyResult.RemovedRows)
            {
                _currentRows.Remove(removedRow);
            }

            if (applyResult.SelectedRowRemoved)
            {
                PilotBoard.SelectedItem = null;
                HideDetailPane();
            }
            else
            {
                UpdateIgnoreAllianceButtonState(PilotBoard.SelectedItem as PilotBoardRow);
            }

            AppLogger.UiInfo($"Ignored alliance filter removed rows from current board. removedRows={applyResult.RemovedCount}");
        }

        private void RefreshDetailPaneIfSelected(PilotBoardRow row)
        {
            _detailPaneController.RefreshDetailPaneIfSelected(
                row,
                DetailPane.Visibility,
                SelectedCharacterText,
                FullCorpText,
                FullAllianceText,
                FreshnessText);

            if (PilotBoard.SelectedItem == row)
            {
                UpdateIgnoreAllianceButtonState(row);
            }
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
            ResetEntryAndRetryTracking();

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
            _mainWindowAppearanceController.SaveMaxKillmailAge(
                _appSettings,
                MaxKillmailAgeDaysTextBox,
                EffectiveMaxKillmailAgeText);
        }

        private void UseDefaultMaxKillmailAgeButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindowAppearanceController.ResetMaxKillmailAgeToDefault(
                _appSettings,
                MaxKillmailAgeDaysTextBox,
                EffectiveMaxKillmailAgeText);
        }

        private void SaveKillmailPathButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindowAppearanceController.SaveKillmailPath(
                _appSettings,
                KillmailDataRootPathTextBox,
                KillmailDataPathModeText,
                EffectiveKillmailDataPathText);
        }

        private void UseDefaultKillmailPathButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindowAppearanceController.ResetKillmailPathToDefault(
                _appSettings,
                KillmailDataRootPathTextBox,
                KillmailDataPathModeText,
                EffectiveKillmailDataPathText);
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
            _detailPaneController.ShowDetailPane(
                row,
                DetailPane,
                SelectedCharacterText,
                FullCorpText,
                FullAllianceText,
                FreshnessText,
                NotesTagsBox,
                KnownCynoOverrideCheckBox,
                BaitOverrideCheckBox);

            UpdateIgnoreAllianceButtonState(row);
        }

        private void HideDetailPane()
        {
            _detailPaneController.HideDetailPane(
                DetailPane,
                NotesTagsBox,
                KnownCynoOverrideCheckBox,
                BaitOverrideCheckBox);

            UpdateIgnoreAllianceButtonState(null);
        }

        private void SaveCurrentNotesAndTags()
        {
            _detailPaneController.SaveCurrentNotesAndTags(
                NotesTagsBox.Text,
                KnownCynoOverrideCheckBox.IsChecked == true,
                BaitOverrideCheckBox.IsChecked == true,
                PilotBoard.SelectedItem as PilotBoardRow);
        }

        private void IgnoreAllianceListView_IgnoreListChanged(object? sender, EventArgs e)
        {
            ApplyIgnoredAllianceRowsToCurrentBoard();
        }

        private void IgnoreAllianceButton_Click(object sender, RoutedEventArgs e)
        {
            if (PilotBoard.SelectedItem is not PilotBoardRow selectedRow)
            {
                AppLogger.UiWarn("Ignore alliance requested with no selected row.");
                return;
            }

            var allianceId = TryGetAllianceId(selectedRow.AllianceId);
            if (!allianceId.HasValue)
            {
                AppLogger.UiWarn($"Ignore alliance requested without a valid alliance ID. character='{selectedRow.CharacterName}' allianceId='{selectedRow.AllianceId ?? ""}'");
                return;
            }

            var added = _ignoreAllianceCoordinator.AddAllianceIdAndPersist(allianceId.Value);
            if (!added)
            {
                AppLogger.UiInfo($"Ignore alliance requested for existing entry. character='{selectedRow.CharacterName}' allianceId='{allianceId.Value}'");
                UpdateIgnoreAllianceButtonState(selectedRow);
                _ignoreAllianceListView?.RefreshFromCoordinator();
                return;
            }

            AppLogger.UiInfo($"Alliance added to ignore list from detail pane. character='{selectedRow.CharacterName}' allianceId='{allianceId.Value}'");

            _ignoreAllianceListView?.RefreshFromCoordinator();
            ApplyIgnoredAllianceRowsToCurrentBoard();
        }

        private void UpdateIgnoreAllianceButtonState(PilotBoardRow? row)
        {
            if (IgnoreAllianceButton == null)
            {
                return;
            }

            if (row == null)
            {
                IgnoreAllianceButton.IsEnabled = false;
                IgnoreAllianceButton.ToolTip = "Select a pilot to ignore their alliance.";
                return;
            }

            var allianceId = TryGetAllianceId(row.AllianceId);
            if (!allianceId.HasValue)
            {
                IgnoreAllianceButton.IsEnabled = false;
                IgnoreAllianceButton.ToolTip = "Selected pilot does not have a known alliance ID yet.";
                return;
            }

            if (_ignoreAllianceCoordinator.ContainsAllianceId(allianceId.Value))
            {
                IgnoreAllianceButton.IsEnabled = false;
                IgnoreAllianceButton.ToolTip = "This alliance is already on the ignore list.";
                return;
            }

            IgnoreAllianceButton.IsEnabled = true;
            IgnoreAllianceButton.ToolTip = string.IsNullOrWhiteSpace(row.AllianceName)
                ? $"Ignore alliance ID {allianceId.Value}."
                : $"Ignore alliance '{row.AllianceName}' ({allianceId.Value}).";
        }

        private void GitHubRepoLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                var url = e.Uri?.AbsoluteUri ?? "https://github.com/SmokeyForged/Pitmasters-Grill";

                AppLogger.UiInfo($"Opening GitHub repo. url='{url}'");

                _browserLauncher.OpenUrl(url);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Failed to open GitHub repo link.", ex);

                MessageBox.Show(
                    $"Failed to open browser.\n\n{ex.Message}",
                    "PMG Browser Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                e.Handled = true;
            }
        }

        private static long? TryGetAllianceId(string? allianceIdText)
        {
            if (string.IsNullOrWhiteSpace(allianceIdText))
            {
                return null;
            }

            if (!long.TryParse(allianceIdText.Trim(), out var allianceId))
            {
                return null;
            }

            if (allianceId <= 0)
            {
                return null;
            }

            return allianceId;
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

                var seedDays = _mainWindowAppearanceController.GetMaxKillmailAgeDaysSettingValue(_appSettings);

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


        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    }
}
