using PitmastersGrill.Persistence;
using PitmastersGrill.Services;
using System;
using System.Windows;
using System.Windows.Threading;

namespace PitmastersGrill
{
    public partial class MainWindow
    {
        public MainWindow(
            BackgroundIntelUpdateService backgroundIntelUpdateService,
            MainWindowRuntimeDependencies runtime)
        {
            ArgumentNullException.ThrowIfNull(backgroundIntelUpdateService);
            ArgumentNullException.ThrowIfNull(runtime);

            AppLogger.UiInfo("MainWindow constructor begin.");
            _backgroundIntelUpdateService = backgroundIntelUpdateService;
            _backgroundIntelUpdateService.StatusChanged += OnIntelUpdateStatusChanged;

            var appSettingsService = runtime.AppSettingsService;
            _diagnostics = runtime.Diagnostics;
            _mainWindowAppearanceController = runtime.MainWindowAppearanceController;
            _eveSessionContextCoordinator = runtime.EveSessionContextCoordinator;
            _boardDisplaySettingsController = runtime.BoardDisplaySettingsController;
            _boardColumnLayoutController = runtime.BoardColumnLayoutController;
            _boardSortController = runtime.BoardSortController;
            _boardColumnSettingsController = runtime.BoardColumnSettingsController;
            _boardColumnLayoutPersistenceController = runtime.BoardColumnLayoutPersistenceController;
            _settingsTabController = runtime.SettingsTabController;
            _mainWindowSettingsCoordinator = runtime.MainWindowSettingsCoordinator;
            _analysisTabController = runtime.AnalysisTabController;
            _mainWindowShellModeCoordinator = runtime.MainWindowShellModeCoordinator;
            _mainWindowInteropController = runtime.MainWindowInteropController;
            _windowLayoutController = runtime.WindowLayoutController;
            _mainWindowNativeInputController = runtime.MainWindowNativeInputController;
            _boardPopulationStatusController = runtime.BoardPopulationStatusController;
            _pilotDetailActionsPresenter = runtime.PilotDetailActionsPresenter;
            _boardPopulationTimingMarkerTracker = runtime.BoardPopulationTimingMarkerTracker;
            _boardRowFactory = runtime.BoardRowFactory;
            _notesRepository = runtime.NotesRepository;
            _watchedPilotRepository = runtime.WatchedPilotRepository;
            _pilotBoardRowDetailFormatter = runtime.PilotBoardRowDetailFormatter;
            _detailPaneController = runtime.DetailPaneController;
            _boardPopulationRowProcessor = runtime.BoardPopulationRowProcessor;
            _boardPopulationPassController = runtime.BoardPopulationPassController;
            _boardPopulationRetryController = runtime.BoardPopulationRetryController;
            _boardPopulationEntryController = runtime.BoardPopulationEntryController;
            _boardPopulationSurface = runtime.BoardPopulationSurface;
            _ignoreAllianceCoordinator = runtime.IgnoreAllianceCoordinator;
            _ignoreAllianceBoardController = runtime.IgnoreAllianceBoardController;
            _zkillUrlBuilder = runtime.ZkillUrlBuilder;
            _browserLauncher = runtime.BrowserLauncher;
            _boardRowStateHydrator = new BoardRowStateHydrator(
                _notesRepository.GetKnownCynoOverride,
                _notesRepository.GetBaitOverride,
                _notesRepository.HasNotes,
                _watchedPilotRepository.IsWatched,
                _pilotBoardRowDetailFormatter.UpdateConfirmedCynoModuleState);
            _boardInitialSessionAssembler = new BoardInitialSessionAssembler(
                _boardRowFactory.CreateRows,
                rows => _boardRowStateHydrator.Hydrate(rows),
                _currentBoardSession,
                (rows, applyOrderedRows) => _boardSortController.ApplyCurrentBoardOrdering(
                    rows,
                    selectedRow: null,
                    applyOrderedRows,
                    restoreSelectedRow: static _ => { }),
                rows => _ignoreAllianceBoardController.ApplyToCurrentRows(rows, selectedRow: null).RemovedRows,
                rows => _boardAffiliationCountService.ApplyCounts(rows, _appSettings.ShowCorpAllianceCounts));
            _boardRowProcessingCoordinator = new BoardRowProcessingCoordinator(
                _currentBoardSession,
                (row, generation, runOnUiAsync) => _boardPopulationRowProcessor.ProcessAsync(
                    row,
                    generation,
                    () => _currentBoardSession.CurrentGeneration,
                    runOnUiAsync,
                    RefreshDetailWindowIfSelected,
                    UpdateLastRefreshed,
                    (markerKind, message) => HandleRowProcessorMarker(markerKind, generation, message),
                    rowToEvaluate => _ignoreAllianceBoardController.ShouldRemoveResolvedRow(rowToEvaluate)),
                action => Dispatcher.InvokeAsync(action).Task,
                ApplyWatchedState,
                ApplyCurrentBoardOrdering,
                () => _pilotDetailSurface.UpdateWatchPilotDetailActionState(
                    _pilotDetailSurface.GetSelectedOrDisplayedDetailRow(
                        PilotBoard?.SelectedItem as Models.PilotBoardRow,
                        _currentBoardSession.Rows)),
                RefreshDetailWindowIfSelected,
                row => _ignoreAllianceBoardController.ShouldRemoveResolvedRow(row),
                RemoveIgnoredAllianceRowFromCurrentBoard);

            _isApplyingSettings = true;
            AppLogger.UiInfo("MainWindow InitializeComponent begin.");
            InitializeComponent();
            AppLogger.UiInfo("MainWindow InitializeComponent end.");
            WireDiagnosticsSupportView();
            WireIntelSupportView();
            RegisterCompactBoardDragHandlers();
            Loaded += MainWindow_Loaded;

            _clipboardDebounceTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(ClipboardDebounceMilliseconds)
            };
            _clipboardDebounceTimer.Tick += ClipboardDebounceTimer_Tick;
            _compactDragHoldTimer = new DispatcherTimer(DispatcherPriority.Input, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(CompactDragHoldMilliseconds)
            };
            _compactDragHoldTimer.Tick += CompactDragHoldTimer_Tick;
            _boardColumnLayoutSaveTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _boardColumnLayoutSaveTimer.Tick += BoardColumnLayoutSaveTimer_Tick;
            _boardLayoutSurface = new BoardLayoutSurface(
                _boardDisplaySettingsController,
                _boardColumnLayoutController,
                _boardColumnSettingsController,
                _boardColumnLayoutPersistenceController,
                _mainWindowSettingsCoordinator,
                _boardColumnLayoutSaveTimer,
                Dispatcher,
                () => _appSettings,
                () => _isApplyingSettings,
                value => _isApplyingSettings = value,
                () => IsLoaded,
                () => _mainWindowShellSurface.UpdateWindowMinimumSize(),
                RecomputeCorpAllianceCounts,
                PilotBoard,
                Resources,
                ShowBoardGridLinesCheckBox,
                BoardTextSizeComboBox,
                BoardFontFamilyComboBox,
                ShowSigColumnCheckBox,
                ShowAllianceColumnCheckBox,
                ShowCorpColumnCheckBox,
                ShowKillsColumnCheckBox,
                ShowLossesColumnCheckBox,
                ShowAvgFleetSizeColumnCheckBox,
                ShowLastShipSeenColumnCheckBox,
                ShowLastSeenColumnCheckBox,
                ShowCynoHullSeenColumnCheckBox,
                ShowCorpAllianceCountsCheckBox,
                SigColumn,
                CharacterColumn,
                AllianceColumn,
                CorpColumn,
                KillsColumn,
                LossesColumn,
                AvgFleetSizeColumn,
                LastShipSeenColumn,
                LastSeenColumn,
                CynoHullSeenColumn,
                MinimumBoardLayoutHostWidth);
            _boardModeHintTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(BoardModeHintMilliseconds)
            };
            _boardModeHintTimer.Tick += BoardModeHintTimer_Tick;
            _windowLayoutSurface = new WindowLayoutSurface(
                _windowLayoutController,
                settings => _mainWindowAppearanceController.SaveSettings(settings),
                GetMonitorWorkAreasDip);
            _mainWindowShellSurface = new MainWindowShellSurface(
                _mainWindowShellModeCoordinator,
                _mainWindowInteropController,
                _windowLayoutSurface,
                CompactModeToggleButton,
                MainContentGrid,
                TopCommandGrid,
                MainTabControl,
                BoardModeHintOverlay,
                BoardStatusFooter,
                MaximizeRestoreWindowButton,
                PilotBoard,
                _boardModeHintTimer,
                () => _appSettings,
                settings => _mainWindowAppearanceController.SaveSettings(settings),
                () => _isApplyingSettings,
                () => WindowState,
                state => WindowState = state,
                () => RestoreBounds,
                () => new Rect(Left, Top, Width, Height),
                bounds =>
                {
                    Left = bounds.Left;
                    Top = bounds.Top;
                    Width = bounds.Width;
                    Height = bounds.Height;
                },
                (minWidth, minHeight) =>
                {
                    MinWidth = minWidth;
                    MinHeight = minHeight;
                },
                () => _pilotDetailSurface.CloseActiveDetailWindow(),
                () => _analysisTabPresenter.UpdateBoardSummary(_currentBoardSession.Rows),
                () => _analysisTabPresenter.UpdateAnalysisTab(_currentBoardSession.Rows),
                (reason, force) => _eveSessionContextSurface.TriggerRefresh(reason, force),
                nowUtc => _eveSessionContextSurface.IsStale(nowUtc),
                force => _boardLayoutSurface.ScheduleFitVisibleBoardColumnsToViewport(force),
                () => _boardPopulationEntryController.InvalidateLastProcessedClipboard(),
                ProcessClipboardIfValidAsync,
                ClearBoard,
                RequestApplicationShutdown,
                (message, title, buttons, image) => MessageBox.Show(this, message, title, buttons, image),
                NormalModeMinimumWindowWidth,
                NormalModeMinimumWindowHeight,
                BoardModeMinimumWindowWidth,
                BoardModeFallbackCommandStripHeight,
                BoardModeFallbackTabHeaderHeight,
                BoardModeFallbackColumnHeaderHeight,
                BoardModeFallbackFooterPaddingHeight,
                BoardModeFallbackRowVerticalPadding,
                MinimumSavedWindowWidth,
                MinimumSavedWindowHeight,
                MinimumVisibleWindowEdge,
                DefaultWindowWidth,
                DefaultWindowHeight,
                TripleEscapeWindowMilliseconds);

            AppLogger.UiInfo("MainWindow InitializeComponent complete.");

            _analysisTabPresenter = new AnalysisTabPresenter(
                _analysisTabController,
                _zkillUrlBuilder,
                AnalysisHyperlink_RequestNavigate,
                BoardSummaryText,
                AnalysisEmptyStateText,
                AnalysisDetailsPanel,
                AnalysisVisibleCountsText,
                AnalysisUniqueCountsText,
                AnalysisAllianceTopText,
                AnalysisCorpTopText,
                AnalysisSignalsText,
                AnalysisHighlightsText,
                _analysisAllianceItems,
                _analysisCorpItems);

            _ignoreAllianceListView = IgnoreAllianceListViewControl;
            _ignoreAllianceListView.Initialize(_ignoreAllianceCoordinator);
            _ignoreAllianceListView.IgnoreListChanged += IgnoreAllianceListView_IgnoreListChanged;

            try
            {
                runtime.DatabaseBootstrap.Initialize();
                DebugTraceWriter.Clear();
                AppLogger.DatabaseInfo($"MainWindow local database initialized. path={runtime.DatabasePath}");
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

            _isApplyingSettings = true;

            _appSettings = appSettingsService.Load();
            _manualUpdateCheckController = new ManualUpdateCheckController(
                this,
                ManualUpdateCheckButton,
                ManualUpdateStatusText,
                _browserLauncher,
                _appSettings,
                _windowShutdownCts.Token,
                () => _isShuttingDown);
            _diagnosticsSupportSurface = DiagnosticsSupportSurface.Create(
                this,
                DiagnosticsSupportViewControl,
                _browserLauncher,
                _providerHealthRows,
                () => _boardPopulationEntryController.IsClipboardProcessing,
                (message, title, buttons, image) => MessageBox.Show(this, message, title, buttons, image),
                DiagnosticTelemetry.GetProviderHealthSnapshots,
                _cacheMaintenanceService.GetStats,
                _cacheMaintenanceService.ClearExpired,
                _cacheMaintenanceService.Vacuum,
                _cacheMaintenanceService.ClearAll);
            _intelSupportSurface = IntelSupportSurface.Create(
                this,
                Dispatcher,
                IntelSupportViewControl,
                _backgroundIntelUpdateService,
                _settingsTabController,
                () => _appSettings,
                () => _mainWindowAppearanceController.SaveSettings(_appSettings),
                () => _isApplyingSettings,
                () => _isShuttingDown,
                _windowShutdownCts.Token,
                () => _currentBoardSession.Snapshot(),
                RefreshCurrentBoardRowsFromLocalIntelAsync,
                () => _boardPopulationEntryController.IsClipboardProcessing,
                message => _diagnosticsSupportSurface.SetStatus(message),
                enabled => DiagnosticsSupportViewControl.SetRebuildKillmailDerivedIntelEnabled(enabled),
                () => _mainWindowAppearanceController.GetMaxKillmailAgeDaysSettingValue(_appSettings),
                cancellationToken => _killmailDerivedIntelRebuildService.RebuildConfirmedCynoModuleObservationsAsync(cancellationToken),
                (seedDays, cancellationToken) => _backgroundIntelUpdateService.EnableKillmailDbPullAsync(seedDays, cancellationToken),
                () => _diagnosticsSupportSurface.RefreshCacheStatsUi(),
                RefreshConfirmedCynoModuleStateForCurrentRows,
                (message, title, buttons, image) => MessageBox.Show(this, message, title, buttons, image));
            _pilotDetailSurface = new PilotDetailSurface(
                this,
                DetailPane,
                SelectedCharacterText,
                FullCorpText,
                FullAllianceText,
                FreshnessText,
                RecentPublicActivityText,
                CynoSignalText,
                CynoConfidenceBar,
                CynoEvidenceText,
                CynoLimitationsText,
                ExplainabilityText,
                NotesTagsBox,
                KnownCynoOverrideCheckBox,
                BaitOverrideCheckBox,
                IgnoreAllianceButton,
                WatchPilotDetailAction,
                _detailPaneController,
                new PilotDetailWindowLifecycleController(),
                new PilotDetailWindowPlacementController(),
                _pilotBoardRowDetailFormatter,
                _pilotDetailActionsPresenter,
                _watchedPilotRepository,
                _notesRepository,
                _settingsTabController,
                () => _appSettings,
                allianceId => _ignoreAllianceCoordinator.ContainsAllianceId(allianceId),
                (selectedRow, currentRows) => _detailPaneController.GetSelectedOrDisplayedDetailRow(
                    selectedRow,
                    DetailPane.Visibility,
                    SelectedCharacterText.Text,
                    currentRows),
                TryIgnoreForRow,
                OpenZkillForRow,
                ApplyCurrentBoardOrdering,
                RefreshDetailWindowIfSelected,
                DetailWindowGap);
            _eveSessionContextSurface = new EveSessionContextSurface(
                Dispatcher,
                _eveSessionContextCoordinator,
                runtime.EveSessionContextService.CaptureAsync,
                () => _isShuttingDown,
                _windowShutdownCts.Token,
                AnalysisCurrentCharacterText,
                AnalysisCurrentSystemText,
                AnalysisEvidenceSourceText,
                AnalysisObservedAtText,
                AnalysisContextStatusText);
            _mainWindowAppearanceController.ApplyPanelModeShell(this, _appSettings, Resources);
            CompactModeToggleButton.IsChecked = _appSettings.CompactModeEnabled;

            _mainWindowSettingsCoordinator.InitializeSettingsUi(
                _appSettings,
                DarkModeCheckBox,
                AlwaysOnTopCheckBox,
                PanelModeCheckBox,
                PanelModeRestartNoticeText,
                WindowOpacitySlider,
                WindowOpacityValueText,
                IntelSupportViewControl.MaxKillmailAgeDaysTextBoxControl,
                IntelSupportViewControl.EffectiveMaxKillmailAgeTextBlock,
                IntelSupportViewControl.KillmailDataRootPathTextBoxControl,
                IntelSupportViewControl.KillmailDataPathModeTextBlock,
                IntelSupportViewControl.EffectiveKillmailDataPathTextBlock,
                VisualThemeComboBox,
                ColorBlindModeComboBox,
                DiagnosticsSupportViewControl.LogLevelComboBoxControl,
                IntelSupportViewControl.EnableLiveZkillFeedCheckBoxControl,
                IntelSupportViewControl.BackgroundHistoricalRepairEnabledCheckBoxControl,
                IntelSupportViewControl.PilotDetailPlacementComboBoxControl,
                ShowBoardGridLinesCheckBox,
                BoardTextSizeComboBox,
                BoardFontFamilyComboBox);

            _boardLayoutSurface.InitializeBoardColumnLayoutUi();
            _boardLayoutSurface.InitializeBoardColumnVisibilityUi();
            _boardLayoutSurface.ApplyBoardDisplaySettings();

            AppLogger.ConfigureLogLevel(_appSettings.LogLevel);

            _isApplyingSettings = false;

            _mainWindowAppearanceController.ApplyTheme(Resources, _appSettings, this, ApplyBoardPopulationStatusVisual);
            _mainWindowAppearanceController.ApplyWindowSettings(this, _appSettings, WindowOpacityValueText, Resources);
            _mainWindowShellSurface.UpdateWindowMinimumSize();

            PilotBoard.ItemsSource = _currentBoardSession.Rows;
            _currentBoardSession.Changed += CurrentBoardSession_Changed;
            AnalysisAllianceListBox.ItemsSource = _analysisAllianceItems;
            AnalysisCorpListBox.ItemsSource = _analysisCorpItems;
            DiagnosticsSupportViewControl.SetProviderHealthItemsSource(_providerHealthRows);
            _diagnosticsSupportSurface.RefreshProviderHealthUi();
            _diagnosticsSupportSurface.RefreshCacheStatsUi();
            UpdateLastRefreshed();
            UpdateBoardPopulationStatus("Board population idle", BoardPopulationStatusKind.Neutral);
            _pilotDetailSurface.HideDetailPane();
            MainTabControl.SelectedIndex = 1;
            _mainWindowShellSurface.ApplyCompactModeUi();
            _analysisTabPresenter.UpdateBoardSummary(_currentBoardSession.Rows);
            _analysisTabPresenter.UpdateAnalysisTab(_currentBoardSession.Rows);
            _intelSupportSurface.ApplySnapshot(_backgroundIntelUpdateService.GetSnapshot(), _isShuttingDown);
            _eveSessionContextSurface.ApplyPendingContext();
            _isMainWindowInitialized = true;
            AppLogger.DatabaseInfo(
                $"Killmail data path resolved. displayPath={KillmailPaths.GetKillmailDataDirectoryDisplayPath()} source={KillmailPaths.GetKillmailDataDirectorySourceDescription()}");

            AppLogger.UiInfo(
                $"MainWindow ready. darkMode={_appSettings.DarkModeEnabled} alwaysOnTop={_appSettings.AlwaysOnTopEnabled} panelMode={_appSettings.PanelModeEnabled} opacityPercent={_mainWindowAppearanceController.CoerceOpacityPercent(_appSettings.WindowOpacityPercent):0} logLevel={_appSettings.LogLevel}");
            AppLogger.UiInfo("MainWindow constructor end.");
        }
    }
}
