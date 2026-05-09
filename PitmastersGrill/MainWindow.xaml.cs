using PitmastersGrill.Diagnostics;
using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using PitmastersGrill.Services;
using PitmastersGrill.Views;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using FormsScreen = System.Windows.Forms.Screen;

namespace PitmastersGrill
{
    public partial class MainWindow : Window
    {
        private const int WmClipboardUpdate = 0x031D;
        private const int WmHotKey = 0x0312;
        private const uint ModControl = 0x0002;
        private const int ClipboardDebounceMilliseconds = 250;
        private const int DefaultBoardPopulationRetryDelaySeconds = 12;
        private const int MaxBoardPopulationRetryAttempts = 5;
        private const int CompactDragHoldMilliseconds = 300;
        private const int BoardModeHintMilliseconds = 5000;
        private const int TripleEscapeWindowMilliseconds = 1500;
        private const int GlobalResetWindowHotKeyId = 0x504D47;
        private const int GlobalClearBoardHotKeyId = 0x504D48;
        private const int GlobalToggleBoardModeHotKeyId = 0x504D49;
        private const double DetailWindowGap = 8;
        private const double NormalModeMinimumWindowWidth = 420;
        private const double NormalModeMinimumWindowHeight = 300;
        private const double BoardModeMinimumWindowWidth = 420;
        private const double BoardModeFallbackCommandStripHeight = 38;
        private const double BoardModeFallbackTabHeaderHeight = 32;
        private const double BoardModeFallbackColumnHeaderHeight = 28;
        private const double BoardModeFallbackFooterPaddingHeight = 18;
        private const double BoardModeFallbackRowVerticalPadding = 16;
        private const double DefaultWindowWidth = 760;
        private const double DefaultWindowHeight = 571;
        private const double MinimumSavedWindowWidth = NormalModeMinimumWindowWidth;
        private const double MinimumSavedWindowHeight = NormalModeMinimumWindowHeight;
        private const double MinimumVisibleWindowEdge = 80;
        private const double MinimumBoardLayoutHostWidth = 400;

        private readonly BackgroundIntelUpdateService _backgroundIntelUpdateService;
        private AppSettings _appSettings = new();

        private readonly BoardRowFactory _boardRowFactory;
        private readonly PilotBoardRowDetailFormatter _pilotBoardRowDetailFormatter;
        private readonly DetailPaneController _detailPaneController;
        private readonly MainWindowAppearanceController _mainWindowAppearanceController;
        private readonly MainWindowSettingsCoordinator _mainWindowSettingsCoordinator;
        private readonly EveSessionContextCoordinator _eveSessionContextCoordinator;
        private readonly BoardDisplaySettingsController _boardDisplaySettingsController;
        private readonly BoardColumnLayoutController _boardColumnLayoutController;
        private readonly BoardColumnSettingsController _boardColumnSettingsController;
        private readonly BoardColumnLayoutPersistenceController _boardColumnLayoutPersistenceController;
        private readonly BoardLayoutSurface _boardLayoutSurface = null!;
        private readonly BoardSortController _boardSortController;
        private readonly SettingsTabController _settingsTabController;
        private readonly AnalysisTabController _analysisTabController;
        private readonly AnalysisTabPresenter _analysisTabPresenter = null!;
        private readonly MainWindowShellModeCoordinator _mainWindowShellModeCoordinator;
        private readonly MainWindowInteropController _mainWindowInteropController;
        private readonly MainWindowShellSurface _mainWindowShellSurface = null!;
        private readonly WindowLayoutController _windowLayoutController;
        private readonly WindowLayoutSurface _windowLayoutSurface;
        private readonly MainWindowNativeInputController _mainWindowNativeInputController;
        private readonly BoardPopulationStatusController _boardPopulationStatusController;
        private readonly BoardPopulationRowProcessor _boardPopulationRowProcessor;
        private readonly BoardPopulationPassController _boardPopulationPassController;
        private readonly BoardPopulationRetryController _boardPopulationRetryController;
        private readonly BoardPopulationEntryController _boardPopulationEntryController;
        private readonly BoardPopulationSurface _boardPopulationSurface;
        private readonly NotesRepository _notesRepository;
        private readonly WatchedPilotRepository _watchedPilotRepository;
        private readonly ZkillUrlBuilder _zkillUrlBuilder;
        private readonly BrowserLauncher _browserLauncher;
        private ManualUpdateCheckController? _manualUpdateCheckController;
        private readonly DiagnosticsSupportSurface _diagnosticsSupportSurface = null!;
        private readonly MainWindowDiagnostics _diagnostics;
        private readonly IntelSupportSurface _intelSupportSurface = null!;
        private readonly PilotDetailActionsPresenter _pilotDetailActionsPresenter;
        private readonly PilotDetailSurface _pilotDetailSurface = null!;
        private readonly EveSessionContextSurface _eveSessionContextSurface = null!;
        private readonly BoardPopulationTimingMarkerTracker _boardPopulationTimingMarkerTracker;
        private readonly IgnoreAllianceCoordinator _ignoreAllianceCoordinator;
        private readonly IgnoreAllianceBoardController _ignoreAllianceBoardController;
        private readonly DispatcherTimer _clipboardDebounceTimer;
        private readonly DispatcherTimer _compactDragHoldTimer;
        private readonly DispatcherTimer _boardColumnLayoutSaveTimer;
        private readonly DispatcherTimer _boardModeHintTimer;
        private readonly CancellationTokenSource _windowShutdownCts = new();
        private readonly SystemTrayIconService _systemTrayIconService;
        private IgnoreAllianceListView? _ignoreAllianceListView;


        private readonly ObservableCollection<PilotBoardRow> _currentRows = new();
        private readonly ObservableCollection<ProviderHealthSnapshot> _providerHealthRows = new();
        private readonly ObservableCollection<AnalysisAffiliationListItem> _analysisAllianceItems = new();
        private readonly ObservableCollection<AnalysisAffiliationListItem> _analysisCorpItems = new();
        private readonly CacheMaintenanceService _cacheMaintenanceService = new();
        private readonly KillmailDerivedIntelRebuildService _killmailDerivedIntelRebuildService = new();
        private bool _isApplyingSettings;
        private bool _isShuttingDown;
        private bool _compactDragPending;
        private Point _compactDragStartPoint;
        private int _processingGeneration;
        private bool _isMainWindowInitialized;
        public MainWindow(BackgroundIntelUpdateService backgroundIntelUpdateService)
        {
            AppLogger.UiInfo("MainWindow constructor begin.");
            _backgroundIntelUpdateService = backgroundIntelUpdateService;
            _backgroundIntelUpdateService.StatusChanged += OnIntelUpdateStatusChanged;

            var appSettingsService = new AppSettingsService();
            _mainWindowAppearanceController = new MainWindowAppearanceController(appSettingsService);
            _eveSessionContextCoordinator = new EveSessionContextCoordinator();
            _boardDisplaySettingsController = new BoardDisplaySettingsController();
            _boardColumnLayoutController = new BoardColumnLayoutController();
            _boardSortController = new BoardSortController();
            _boardColumnSettingsController = new BoardColumnSettingsController(
                _boardColumnLayoutController,
                settings => _mainWindowAppearanceController.SaveSettings(settings));
            _boardColumnLayoutPersistenceController = new BoardColumnLayoutPersistenceController(
                _boardColumnLayoutController,
                settings => _mainWindowAppearanceController.SaveSettings(settings));
            _settingsTabController = new SettingsTabController();
            _mainWindowSettingsCoordinator = new MainWindowSettingsCoordinator(
                _mainWindowAppearanceController,
                _settingsTabController,
                _boardDisplaySettingsController,
                settings => _mainWindowAppearanceController.SaveSettings(settings));
            _analysisTabController = new AnalysisTabController();
            _mainWindowShellModeCoordinator = new MainWindowShellModeCoordinator();
            _mainWindowInteropController = new MainWindowInteropController();
            _windowLayoutController = new WindowLayoutController();
            _mainWindowNativeInputController = new MainWindowNativeInputController();
            _boardPopulationStatusController = new BoardPopulationStatusController();
            _pilotDetailActionsPresenter = new PilotDetailActionsPresenter();

            _isApplyingSettings = true;
            AppLogger.UiInfo("MainWindow InitializeComponent begin.");
            InitializeComponent();
            AppLogger.UiInfo("MainWindow InitializeComponent end.");
            WireDiagnosticsSupportView();
            WireIntelSupportView();
            RegisterCompactBoardDragHandlers();
            Loaded += MainWindow_Loaded;

            _diagnostics = new MainWindowDiagnostics(Dispatcher);
            _systemTrayIconService = new SystemTrayIconService(
                this,
                () => RequestApplicationShutdown("Tray icon Exit"));
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
            _boardPopulationTimingMarkerTracker = new BoardPopulationTimingMarkerTracker();
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
                CloseActiveDetailWindow,
                UpdateBoardSummaryBanner,
                UpdateAnalysisTab,
                (reason, force) => _eveSessionContextSurface.TriggerRefresh(reason, force),
                nowUtc => _eveSessionContextSurface.IsStale(nowUtc),
                force => ScheduleFitVisibleBoardColumnsToViewport(force),
                () => _boardPopulationEntryController!.InvalidateLastProcessedClipboard(),
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

            var composed = MainWindowCompositionRoot.Compose(
                _diagnostics,
                appSettingsService,
                _mainWindowAppearanceController,
                _boardPopulationStatusController,
                DefaultBoardPopulationRetryDelaySeconds);

            _boardRowFactory = composed.BoardRowFactory;
            _notesRepository = composed.NotesRepository;
            _watchedPilotRepository = composed.WatchedPilotRepository;
            _pilotBoardRowDetailFormatter = composed.PilotBoardRowDetailFormatter;
            _detailPaneController = composed.DetailPaneController;
            _boardPopulationRowProcessor = composed.BoardPopulationRowProcessor;
            _boardPopulationPassController = composed.BoardPopulationPassController;
            _boardPopulationRetryController = composed.BoardPopulationRetryController;
            _boardPopulationEntryController = composed.BoardPopulationEntryController;
            _boardPopulationSurface = new BoardPopulationSurface(
                _boardPopulationEntryController,
                _boardPopulationPassController,
                _boardPopulationRetryController,
                _diagnostics);
            _ignoreAllianceCoordinator = composed.IgnoreAllianceCoordinator;
            _ignoreAllianceBoardController = composed.IgnoreAllianceBoardController;
            _zkillUrlBuilder = composed.ZkillUrlBuilder;
            _browserLauncher = composed.BrowserLauncher;
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
            var eveSessionContextService = new EveSessionContextService();

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
                () => _currentRows.ToList(),
                RefreshCurrentBoardRowsFromLocalIntelAsync,
                () => _boardPopulationEntryController.IsClipboardProcessing,
                SetDiagnosticsStatus,
                enabled => DiagnosticsSupportViewControl.SetRebuildKillmailDerivedIntelEnabled(enabled),
                () => _mainWindowAppearanceController.GetMaxKillmailAgeDaysSettingValue(_appSettings),
                cancellationToken => _killmailDerivedIntelRebuildService.RebuildConfirmedCynoModuleObservationsAsync(cancellationToken),
                (seedDays, cancellationToken) => _backgroundIntelUpdateService.EnableKillmailDbPullAsync(seedDays, cancellationToken),
                RefreshCacheStatsUi,
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
                eveSessionContextService.CaptureAsync,
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

            InitializeBoardColumnLayoutUi();
            InitializeBoardColumnVisibilityUi();
            ApplyBoardDisplaySettings();

            AppLogger.ConfigureLogLevel(_appSettings.LogLevel);

            _isApplyingSettings = false;

            _mainWindowAppearanceController.ApplyTheme(Resources, _appSettings, this, ApplyBoardPopulationStatusVisual);
            _mainWindowAppearanceController.ApplyWindowSettings(this, _appSettings, WindowOpacityValueText, Resources);
            _mainWindowShellSurface.UpdateWindowMinimumSize();

            PilotBoard.ItemsSource = _currentRows;
            _currentRows.CollectionChanged += CurrentRows_CollectionChanged;
            AnalysisAllianceListBox.ItemsSource = _analysisAllianceItems;
            AnalysisCorpListBox.ItemsSource = _analysisCorpItems;
            DiagnosticsSupportViewControl.SetProviderHealthItemsSource(_providerHealthRows);
            RefreshProviderHealthUi();
            RefreshCacheStatsUi();
            UpdateLastRefreshed();
            UpdateBoardPopulationStatus("Board population idle", BoardPopulationStatusKind.Neutral);
            HideDetailPane();
            UpdateOpenDetailsButtonState();
            MainTabControl.SelectedIndex = 1;
            _mainWindowShellSurface.ApplyCompactModeUi();
            UpdateBoardSummaryBanner();
            UpdateAnalysisTab();
            _intelSupportSurface.ApplySnapshot(_backgroundIntelUpdateService.GetSnapshot(), _isShuttingDown);
            _eveSessionContextSurface.ApplyPendingContext();
            _isMainWindowInitialized = true;

            AppLogger.DatabaseInfo(
                $"Killmail data path resolved. displayPath={KillmailPaths.GetKillmailDataDirectoryDisplayPath()} source={KillmailPaths.GetKillmailDataDirectorySourceDescription()}");

            AppLogger.UiInfo(
                $"MainWindow ready. darkMode={_appSettings.DarkModeEnabled} alwaysOnTop={_appSettings.AlwaysOnTopEnabled} panelMode={_appSettings.PanelModeEnabled} opacityPercent={_mainWindowAppearanceController.CoerceOpacityPercent(_appSettings.WindowOpacityPercent):0} logLevel={_appSettings.LogLevel}");
            AppLogger.UiInfo("MainWindow constructor end.");
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwnd = new WindowInteropHelper(this).Handle;
            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);

            _mainWindowAppearanceController.ApplyTitleBarTheme(this, _appSettings.DarkModeEnabled);
            _mainWindowShellSurface.RestoreWindowLayoutFromSettings();
            _mainWindowNativeInputController.Attach(
                hwnd,
                AddClipboardFormatListener,
                RegisterHotKey,
                ModControl,
                GlobalResetWindowHotKeyId,
                GlobalClearBoardHotKeyId,
                GlobalToggleBoardModeHotKeyId,
                AppLogger.UiInfo,
                AppLogger.UiWarn,
                Marshal.GetLastWin32Error);
            _mainWindowShellSurface.UpdateWindowStateUi();
            _eveSessionContextSurface.TriggerRefresh("startup", force: false);

            AppLogger.UiInfo("MainWindow source initialized. Clipboard listener attached and title bar theme applied.");
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainWindow_Loaded;
            AppLogger.UiInfo("MainWindow loaded.");
            _mainWindowShellSurface.UpdateWindowMinimumSize();
            Dispatcher.BeginInvoke(new Action(FinalizeBoardColumnLayoutInitialization), DispatcherPriority.Loaded);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _mainWindowShellSurface.SaveWindowLayoutToSettings("Window closing");
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            AppLogger.UiInfo("MainWindow closing requested.");
            _isShuttingDown = true;

            if (!_isMainWindowInitialized)
            {
                AppLogger.UiWarn("MainWindow closed before initialization completed. Skipping full shutdown cleanup.");
                base.OnClosed(e);
                return;
            }

            SaveCurrentNotesAndTags();
            CancelBoardPopulationRetry();
            RequestOwnedBackgroundWorkStop("MainWindow closed");

            if (_ignoreAllianceListView != null)
            {
                _ignoreAllianceListView.IgnoreListChanged -= IgnoreAllianceListView_IgnoreListChanged;
            }

            _backgroundIntelUpdateService.StatusChanged -= OnIntelUpdateStatusChanged;
            _currentRows.CollectionChanged -= CurrentRows_CollectionChanged;
            UnsubscribeFromAllBoardRows();
            _clipboardDebounceTimer.Stop();
            _clipboardDebounceTimer.Tick -= ClipboardDebounceTimer_Tick;
            _compactDragHoldTimer.Stop();
            _compactDragHoldTimer.Tick -= CompactDragHoldTimer_Tick;
            _boardColumnLayoutSaveTimer.Stop();
            _boardColumnLayoutSaveTimer.Tick -= BoardColumnLayoutSaveTimer_Tick;
            _boardModeHintTimer.Stop();
            _boardModeHintTimer.Tick -= BoardModeHintTimer_Tick;
            _systemTrayIconService.Dispose();
            _diagnostics.Dispose();

            var hwnd = new WindowInteropHelper(this).Handle;
            _mainWindowNativeInputController.Detach(
                hwnd,
                RemoveClipboardFormatListener,
                UnregisterHotKey,
                GlobalResetWindowHotKeyId,
                GlobalClearBoardHotKeyId,
                GlobalToggleBoardModeHotKeyId,
                AppLogger.UiInfo,
                AppLogger.UiWarn,
                Marshal.GetLastWin32Error);

            AppLogger.UiInfo("MainWindow closed. Clipboard listener removed, retry state cancelled, and background work stop requested.");

            base.OnClosed(e);
        }

        private void ExitApplicationButton_Click(object sender, RoutedEventArgs e)
        {
            RequestApplicationShutdown("Exit button");
        }


        private void CompactModeToggleButton_Changed(object sender, RoutedEventArgs e)
        {
            _mainWindowShellSurface.ApplyCompactModeUi();
        }
        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(sender, MainTabControl))
            {
                return;
            }

            _mainWindowShellSurface.HandleMainTabSelectionChanged();
        }

        private void BoardModeHintTimer_Tick(object? sender, EventArgs e) => _mainWindowShellSurface.HandleBoardModeHintTimerTick();

        private void RequestApplicationShutdown(string reason)
        {
            if (_isShuttingDown)
            {
                return;
            }

            _isShuttingDown = true;
            AppLogger.UiInfo($"Application exit requested from MainWindow. reason='{reason}'");

            if (ExitApplicationButton != null)
            {
                ExitApplicationButton.IsEnabled = false;
                ExitApplicationButton.Content = "Exiting...";
            }

            RequestOwnedBackgroundWorkStop(reason);
            Close();
        }

        private void RequestOwnedBackgroundWorkStop(string reason)
        {
            try
            {
                _windowShutdownCts.Cancel();
                _backgroundIntelUpdateService.Stop();
                AppLogger.UiInfo($"PMG-owned background work stop requested. reason='{reason}'");
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Failed while requesting PMG-owned background work stop.", ex);
            }
        }

        private void WindowHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed ||
                FindVisualParent<ButtonBase>(e.OriginalSource as DependencyObject) != null)
            {
                return;
            }

            try
            {
                if (e.ClickCount == 2)
                {
                    WindowState = WindowState == WindowState.Maximized
                        ? WindowState.Normal
                        : WindowState.Maximized;
                    return;
                }

                DragMove();
            }
            catch (InvalidOperationException ex)
            {
                AppLogger.UiWarn($"Window header drag ignored. reason={ex.Message}");
            }
        }

        private void CompactWindowDragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed)
            {
                return;
            }

            try
            {
                DragMove();
            }
            catch (InvalidOperationException ex)
            {
                AppLogger.UiWarn($"Compact drag handle ignored. reason={ex.Message}");
            }
        }

        private void MinimizeWindowButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindowShellSurface.HandleMinimizeWindow();
        }

        private void MaximizeRestoreWindowButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindowShellSurface.HandleMaximizeRestoreWindow();
        }

        private void CloseWindowButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindowShellSurface.HandleCloseWindow();
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            _mainWindowShellSurface.HandleWindowStateChanged();
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            _mainWindowShellSurface.HandleWindowLocationChanged();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _mainWindowShellSurface.HandleWindowSizeChanged();
        }

        private void DarkModeCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _mainWindowSettingsCoordinator.HandleDarkModeChanged(
                _isApplyingSettings,
                _appSettings,
                DarkModeCheckBox.IsChecked == true,
                Resources,
                this,
                ApplyBoardPopulationStatusVisual,
                () => _pilotDetailSurface.ApplyThemeToActiveWindow(Resources));
        }

        private void AlwaysOnTopCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _mainWindowSettingsCoordinator.HandleAlwaysOnTopChanged(
                _isApplyingSettings,
                _appSettings,
                AlwaysOnTopCheckBox.IsChecked == true,
                this,
                WindowOpacityValueText,
                Resources);
        }

        private void PanelModeCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _mainWindowSettingsCoordinator.HandlePanelModeChanged(
                _isApplyingSettings,
                _appSettings,
                PanelModeCheckBox.IsChecked == true,
                PanelModeRestartNoticeText);
        }

        private void WindowOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _mainWindowSettingsCoordinator.HandleWindowOpacityChanged(
                _isApplyingSettings,
                _appSettings,
                WindowOpacitySlider.Value,
                this,
                WindowOpacityValueText,
                Resources,
                () => _pilotDetailSurface.ApplyThemeToActiveWindow(Resources));
        }

        private void ResetWindowLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindowShellSurface.HandleResetWindowLayoutButton();
        }

        private void RequestWindowLayoutResetFromHotkey(string source)
        {
            _mainWindowShellSurface.HandleRequestWindowLayoutResetFromHotkey(source);
        }

        private void LogLevelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _mainWindowSettingsCoordinator.HandleLogLevelChanged(
                _isApplyingSettings,
                _appSettings,
                DiagnosticsSupportViewControl?.LogLevelComboBoxControl);
        }

        private void WireDiagnosticsSupportView()
        {
            DiagnosticsSupportViewControl.OpenLogsRequested += OpenLogsButton_Click;
            DiagnosticsSupportViewControl.PackageDiagnosticsRequested += PackageDiagnosticsButton_Click;
            DiagnosticsSupportViewControl.OpenDiagnosticsFolderRequested += OpenDiagnosticsFolderButton_Click;
            DiagnosticsSupportViewControl.LogLevelSelectionChanged += LogLevelComboBox_SelectionChanged;
            DiagnosticsSupportViewControl.RefreshProviderHealthRequested += RefreshProviderHealthButton_Click;
            DiagnosticsSupportViewControl.RefreshCacheStatsRequested += RefreshCacheStatsButton_Click;
            DiagnosticsSupportViewControl.ClearExpiredCacheRequested += ClearExpiredCacheButton_Click;
            DiagnosticsSupportViewControl.VacuumCacheRequested += VacuumCacheButton_Click;
            DiagnosticsSupportViewControl.ClearAllCacheRequested += ClearAllCacheButton_Click;
            DiagnosticsSupportViewControl.RebuildKillmailDerivedIntelRequested += RebuildKillmailDerivedIntelButton_Click;
        }

        private void WireIntelSupportView()
        {
            IntelSupportViewControl.SaveMaxKillmailAgeRequested += SaveMaxKillmailAgeButton_Click;
            IntelSupportViewControl.UseDefaultMaxKillmailAgeRequested += UseDefaultMaxKillmailAgeButton_Click;
            IntelSupportViewControl.EnableKillmailDbPullRequested += EnableKillmailDbPullButton_Click;
            IntelSupportViewControl.EnableLiveZkillFeedToggled += EnableLiveZkillFeedCheckBox_Checked;
            IntelSupportViewControl.BackgroundHistoricalRepairToggled += BackgroundHistoricalRepairEnabledCheckBox_Checked;
            IntelSupportViewControl.PilotDetailPlacementSelectionChanged += PilotDetailPlacementComboBox_SelectionChanged;
            IntelSupportViewControl.SaveKillmailPathRequested += SaveKillmailPathButton_Click;
            IntelSupportViewControl.UseDefaultKillmailPathRequested += UseDefaultKillmailPathButton_Click;
            IntelSupportViewControl.RunTodaysFreshnessRequested += RunTodaysFreshnessButton_Click;
            IntelSupportViewControl.RunHistoricalFreshnessRequested += RunHistoricalFreshnessButton_Click;
        }

        private void VisualThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _mainWindowSettingsCoordinator.HandleVisualThemeChanged(
                _isApplyingSettings,
                _appSettings,
                VisualThemeComboBox,
                Resources,
                this,
                ApplyBoardPopulationStatusVisual,
                () => _pilotDetailSurface.ApplyThemeToActiveWindow(Resources));
        }

        private void ColorBlindModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _mainWindowSettingsCoordinator.HandleColorBlindModeChanged(
                _isApplyingSettings,
                _appSettings,
                ColorBlindModeComboBox,
                Resources,
                this,
                ApplyBoardPopulationStatusVisual,
                () => _pilotDetailSurface.ApplyThemeToActiveWindow(Resources),
                () => PilotBoard?.Items.Refresh());
        }

        private void PilotDetailPlacementComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _mainWindowSettingsCoordinator.HandlePilotDetailPlacementPreferenceChanged(
                _isApplyingSettings,
                _appSettings,
                IntelSupportViewControl?.PilotDetailPlacementComboBoxControl);
        }

        private void InitializeBoardColumnVisibilityUi() => _boardLayoutSurface.InitializeBoardColumnVisibilityUi();

        private void InitializeBoardColumnLayoutUi() => _boardLayoutSurface.InitializeBoardColumnLayoutUi();

        private void ApplyBoardDisplaySettings() => _boardLayoutSurface.ApplyBoardDisplaySettings();

        private void ShowBoardGridLinesCheckBox_Changed(object sender, RoutedEventArgs e) => _boardLayoutSurface.HandleShowBoardGridLinesChanged();

        private void BoardTextSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => _boardLayoutSurface.HandleBoardTextSizeChanged();

        private void BoardFontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => _boardLayoutSurface.HandleBoardFontFamilyChanged();

        private void BoardColumnVisibilityCheckBox_Changed(object sender, RoutedEventArgs e) => _boardLayoutSurface.HandleBoardColumnVisibilityChanged();

        private void ShowCorpAllianceCountsCheckBox_Changed(object sender, RoutedEventArgs e) => _boardLayoutSurface.HandleShowCorpAllianceCountsChanged();

        private void ShowAllBoardColumnsButton_Click(object sender, RoutedEventArgs e) => _boardLayoutSurface.HandleShowAllBoardColumns();

        private void ResetBoardColumnsButton_Click(object sender, RoutedEventArgs e) => _boardLayoutSurface.HandleResetBoardColumns();

        private void ResetBoardLayoutButton_Click(object sender, RoutedEventArgs e) => _boardLayoutSurface.HandleResetBoardLayout();

        private void ApplyBoardColumnSettingsToCheckBoxes() => _boardLayoutSurface.ApplyBoardColumnSettingsToCheckBoxes();

        private void SaveBoardColumnSettingsFromCheckBoxes() => _boardLayoutSurface.SaveBoardColumnSettingsFromCheckBoxes();

        private void ApplyBoardColumnVisibility() => _boardLayoutSurface.ApplyBoardColumnVisibility();

        private void ApplySavedBoardColumnLayout() => _boardLayoutSurface.ApplySavedBoardColumnLayout();

        private void ApplyCanonicalBoardColumnLayout(string reason) => _boardLayoutSurface.ApplyCanonicalBoardColumnLayout(reason);

        private void ApplyBoardColumnLayout(IEnumerable<BoardColumnLayoutSetting> layoutSettings, string reason) => _boardLayoutSurface.ApplyBoardColumnLayout(layoutSettings, reason);

        private void PilotBoard_ColumnReordered(object sender, DataGridColumnEventArgs e) => _boardLayoutSurface.HandlePilotBoardColumnReordered();

        private void PilotBoard_SizeChanged(object sender, SizeChangedEventArgs e) => _boardLayoutSurface.HandlePilotBoardSizeChanged();

        private void BoardColumnWidth_ValueChanged(object? sender, EventArgs e) => _boardLayoutSurface.HandleBoardColumnWidthChanged();

        private void ScheduleBoardColumnLayoutSave(string reason) => _boardLayoutSurface.ScheduleBoardColumnLayoutSave(reason);

        private void BoardColumnLayoutSaveTimer_Tick(object? sender, EventArgs e) => _boardLayoutSurface.HandleBoardColumnLayoutSaveTimerTick();

        private void SaveCurrentBoardColumnLayout(string reason) => _boardLayoutSurface.SaveCurrentBoardColumnLayout(reason);

        private void FinalizeBoardColumnLayoutInitialization() => _boardLayoutSurface.FinalizeBoardColumnLayoutInitialization();

        private bool IsBoardLayoutHostReady() => _boardLayoutSurface.IsBoardLayoutHostReady();

        private void ScheduleFitVisibleBoardColumnsToViewport(bool force = false) => _boardLayoutSurface.ScheduleFitVisibleBoardColumnsToViewport(force);

        private void FitVisibleBoardColumnsToViewport() => _boardLayoutSurface.FitVisibleBoardColumnsToViewport();

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
                _pilotBoardRowDetailFormatter.UpdateConfirmedCynoModuleState(selectedRow);
                PilotBoard.Items.Refresh();
                RefreshDetailWindowIfSelected(selectedRow);
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
                _pilotBoardRowDetailFormatter.UpdateConfirmedCynoModuleState(selectedRow);
                PilotBoard.Items.Refresh();
                RefreshDetailWindowIfSelected(selectedRow);
                AppLogger.UiInfo(
                    $"Bait override changed. character='{selectedRow.CharacterName}' enabled={selectedRow.BaitOverride}");
            }
        }

        private void OnIntelUpdateStatusChanged(IntelUpdateStatusSnapshot snapshot)
        {
            _intelSupportSurface.HandleStatusChanged(snapshot, _isShuttingDown);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            var route = _mainWindowInteropController.RouteWindowMessage(
                msg,
                wParam,
                IsActive,
                WmClipboardUpdate,
                WmHotKey,
                GlobalResetWindowHotKeyId,
                GlobalClearBoardHotKeyId,
                GlobalToggleBoardModeHotKeyId);

            handled = route.Handled;

            switch (route.Action)
            {
                case MainWindowMessageAction.ScheduleClipboardProcessing:
                    ScheduleClipboardProcessing();
                    break;

                case MainWindowMessageAction.RequestWindowLayoutReset:
                    RequestWindowLayoutResetFromHotkey("global Ctrl+Home hotkey");
                    break;

                case MainWindowMessageAction.ClearBoard:
                    ClearBoard("global Delete hotkey");
                    break;

                case MainWindowMessageAction.ToggleCompactMode:
                    _mainWindowShellSurface.ToggleCompactModeFromHotkey();
                    break;
            }

            return IntPtr.Zero;
        }
        private void ScheduleClipboardProcessing()
        {
            _boardPopulationSurface.ScheduleClipboardProcessing(
                _clipboardDebounceTimer,
                ClipboardDebounceMilliseconds);
        }

        private void ClipboardDebounceTimer_Tick(object? sender, EventArgs e)
        {
            _clipboardDebounceTimer.Stop();
            _diagnostics.ClipboardDebounceElapsed();
            _ = ProcessClipboardIfValidAsync();
        }

        private Task ProcessClipboardIfValidAsync()
        {
            return _boardPopulationSurface.ProcessClipboardIfValidAsync(
                clipboardContainsText: () => Clipboard.ContainsText(),
                clipboardGetText: () => Clipboard.GetText(),
                setBoardButtonsEnabled: enabled =>
                {
                    IntelSupportViewControl.EnableKillmailDbPullButtonControl.IsEnabled = enabled;
                    ClearBoardButton.IsEnabled = enabled;
                },
                beginForegroundPriority: () => _backgroundIntelUpdateService.BeginForegroundPriority(),
                cancelBoardPopulationRetry: CancelBoardPopulationRetry,
                resetBoardPopulationTracking: preserveLastProcessedClipboardText => ResetBoardPopulationTracking(preserveLastProcessedClipboardText),
                updateClipboardStatus: UpdateBoardPopulationStatus,
                processNamesAsync: ProcessNamesAsync);
        }

        private Task ProcessNamesAsync(List<string> characterNames, bool isRetryPass)
        {
            return _boardPopulationSurface.ProcessNamesAsync(
                characterNames,
                isRetryPass,
                (reason, force) => _eveSessionContextSurface.TriggerRefresh(reason, force),
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
            _boardPopulationSurface.FinalizeBoardPopulationPass(
                generation,
                _processingGeneration,
                _currentRows,
                MaxBoardPopulationRetryAttempts,
                UpdateBoardPopulationStatus,
                ScheduleBoardPopulationRetry);
        }

        private void ScheduleBoardPopulationRetry()
        {
            _boardPopulationSurface.ScheduleBoardPopulationRetry(
                _currentRows,
                Dispatcher,
                UpdateBoardPopulationStatus,
                ProcessRetryPassAsync);
        }

        private Task ProcessRetryPassAsync()
        {
            return _boardPopulationSurface.ProcessRetryPassAsync(
                _currentRows,
                () => _backgroundIntelUpdateService.BeginForegroundPriority(),
                (rows, generation) => ProcessRowBatchAsync(rows.ToList(), generation),
                () => _processingGeneration,
                UpdateLastRefreshed,
                FinalizeBoardPopulationPass);
        }

        private void CancelBoardPopulationRetry()
        {
            _boardPopulationSurface.CancelBoardPopulationRetry();
        }

        private void ResetBoardPopulationTracking(bool preserveLastProcessedClipboardText = false)
        {
            ResetEntryAndRetryTracking(preserveLastProcessedClipboardText);
            UpdateBoardPopulationStatus("Board population in progress", BoardPopulationStatusKind.Neutral);
        }

        private void ResetEntryAndRetryTracking(bool preserveLastProcessedClipboardText = false)
        {
            _boardPopulationSurface.ResetBoardPopulationTracking(preserveLastProcessedClipboardText);
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
                    RefreshDetailWindowIfSelected,
                    UpdateLastRefreshed,
                    (markerKind, message) => HandleRowProcessorMarker(markerKind, generation, message),
                    rowToEvaluate => _ignoreAllianceBoardController.ShouldRemoveResolvedRow(rowToEvaluate));

                await Dispatcher.InvokeAsync(() =>
                {
                    if (generation != _processingGeneration)
                    {
                        return;
                    }

                    ApplyWatchedState(row);
                    ApplyCurrentBoardOrdering();
                    UpdateWatchPilotDetailActionState(GetSelectedOrDisplayedDetailRow());
                    RefreshDetailWindowIfSelected(row);
                });

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

            ResetManualBoardSort();
            UnsubscribeFromAllBoardRows();
            _currentRows.Clear();

            foreach (var row in initialRows)
            {
                row.KnownCynoOverride = _notesRepository.GetKnownCynoOverride(row.CharacterName);
                row.BaitOverride = _notesRepository.GetBaitOverride(row.CharacterName);
                row.HasNotes = _notesRepository.HasNotes(row.CharacterName);
                ApplyWatchedState(row);
                _pilotBoardRowDetailFormatter.UpdateConfirmedCynoModuleState(row);
                SubscribeToBoardRow(row);
                _currentRows.Add(row);
            }

            ApplyCurrentBoardOrdering();
            ApplyIgnoredAllianceRowsToCurrentBoard();
            RecomputeCorpAllianceCounts();

            PilotBoard.SelectedItem = null;
            HideDetailPane();
            CloseActiveDetailWindow();
            UpdateOpenDetailsButtonState();
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
                CloseActiveDetailWindow();
                UpdateOpenDetailsButtonState();
            }

            UnsubscribeFromBoardRow(row);
            AppLogger.UiInfo($"Ignored alliance filter removed a resolved row from current board. character='{row.CharacterName}' allianceId='{row.AllianceId}'");
            RecomputeCorpAllianceCounts();
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
                UnsubscribeFromBoardRow(removedRow);
                _currentRows.Remove(removedRow);
            }

            RecomputeCorpAllianceCounts();

            if (applyResult.SelectedRowRemoved)
            {
                PilotBoard.SelectedItem = null;
                HideDetailPane();
                CloseActiveDetailWindow();
                UpdateOpenDetailsButtonState();
            }
            else
            {
                UpdateIgnoreAllianceButtonState(PilotBoard.SelectedItem as PilotBoardRow);
            }

            AppLogger.UiInfo($"Ignored alliance filter removed rows from current board. removedRows={applyResult.RemovedCount}");
        }

        private void RefreshDetailWindowIfSelected(PilotBoardRow row)
        {
            _pilotBoardRowDetailFormatter.UpdateConfirmedCynoModuleState(row);
            RecomputeCorpAllianceCounts();
            _pilotDetailSurface.RefreshActiveDetailWindowIfSelected(row);
        }

        private void RefreshConfirmedCynoModuleStateForCurrentRows()
        {
            foreach (var row in _currentRows)
            {
                _pilotBoardRowDetailFormatter.UpdateConfirmedCynoModuleState(row);
            }

            PilotBoard?.Items.Refresh();

            if (PilotBoard?.SelectedItem is PilotBoardRow selectedRow)
            {
                RefreshDetailWindowIfSelected(selectedRow);
            }
        }

        private void RecomputeCorpAllianceCounts()
        {
            var showCounts = _appSettings.ShowCorpAllianceCounts;

            var corpCounts = _currentRows
                .Select(row => new { Row = row, Key = GetCorpCountKey(row) })
                .Where(item => !string.IsNullOrWhiteSpace(item.Key))
                .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

            var allianceCounts = _currentRows
                .Select(row => new { Row = row, Key = GetAllianceCountKey(row) })
                .Where(item => !string.IsNullOrWhiteSpace(item.Key))
                .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

            foreach (var row in _currentRows)
            {
                row.ShowCorpAllianceCounts = showCounts;

                var corpKey = GetCorpCountKey(row);
                row.CorpLocalCount = !string.IsNullOrWhiteSpace(corpKey) && corpCounts.TryGetValue(corpKey, out var corpCount)
                    ? corpCount
                    : 0;

                var allianceKey = GetAllianceCountKey(row);
                row.AllianceLocalCount = !string.IsNullOrWhiteSpace(allianceKey) && allianceCounts.TryGetValue(allianceKey, out var allianceCount)
                    ? allianceCount
                    : 0;
            }

            UpdateBoardSummaryBanner();
            UpdateAnalysisTab();
        }

        private static string GetCorpCountKey(PilotBoardRow row)
        {
            return BuildAffiliationCountKey(row.CorpId, row.CorpName);
        }

        private static string GetAllianceCountKey(PilotBoardRow row)
        {
            return BuildAffiliationCountKey(row.AllianceId, row.AllianceName);
        }

        private static string BuildAffiliationCountKey(string id, string name)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                return $"id:{id.Trim()}";
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                return $"name:{name.Trim().ToUpperInvariant()}";
            }

            return string.Empty;
        }

        private void PilotBoard_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SaveCurrentNotesAndTags();
            UpdateOpenDetailsButtonState();

            if (PilotBoard.SelectedItem is PilotBoardRow selectedRow)
            {
                AppLogger.UiInfo($"Board selection changed. character='{selectedRow.CharacterName}'");
                return;
            }

            AppLogger.UiInfo("Board selection cleared.");
        }

        private void PilotBoard_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            if (_boardSortController.TryHandleSorting(
                PilotBoard,
                e.Column,
                _currentRows,
                PilotBoard.SelectedItem as PilotBoardRow,
                out var sortMemberPath,
                out var nextDirection))
            {
                AppLogger.UiInfo($"Board sort changed. member='{sortMemberPath}' direction={nextDirection}");
            }
        }

        private void PilotBoard_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            if (FindVisualParent<ButtonBase>(e.OriginalSource as DependencyObject) != null)
            {
                return;
            }

            var rowContainer = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
            if (rowContainer?.Item is not PilotBoardRow selectedRow)
            {
                return;
            }

            CancelCompactBoardDrag();
            PilotBoard.SelectedItem = selectedRow;
            OpenZkillForRow(selectedRow);
            e.Handled = true;
        }

        private void PilotBoard_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (FindVisualParent<ButtonBase>(e.OriginalSource as DependencyObject) != null)
            {
                return;
            }

            var rowContainer = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
            if (rowContainer?.Item is not PilotBoardRow selectedRow)
            {
                return;
            }

            PilotBoard.SelectedItem = selectedRow;
            OpenDetailsWindow(selectedRow);
            e.Handled = true;
        }

        private void RegisterCompactBoardDragHandlers()
        {
            // DataGrid rows and column headers can mark normal mouse events as handled for
            // selection/sorting before our XAML handlers see them. Register handledEventsToo
            // so compact-mode click-hold dragging works even when the board is full.
            PilotBoard.AddHandler(UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler(PilotBoard_PreviewMouseDownHandledToo), true);
            PilotBoard.AddHandler(UIElement.PreviewMouseUpEvent, new MouseButtonEventHandler(PilotBoard_PreviewMouseUpHandledToo), true);
            PilotBoard.AddHandler(UIElement.PreviewMouseMoveEvent, new MouseEventHandler(PilotBoard_PreviewMouseMoveHandledToo), true);
        }

        private void PilotBoard_PreviewMouseDownHandledToo(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                BeginCompactBoardDragIfAllowed(e);
            }
        }

        private void PilotBoard_PreviewMouseUpHandledToo(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                CancelCompactBoardDrag();
            }
        }

        private void PilotBoard_PreviewMouseMoveHandledToo(object sender, MouseEventArgs e)
        {
            if (_compactDragPending && e.LeftButton != MouseButtonState.Pressed)
            {
                CancelCompactBoardDrag();
            }
        }

        private void PilotBoard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            BeginCompactBoardDragIfAllowed(e);
        }

        private void PilotBoard_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            CancelCompactBoardDrag();
        }

        private void PilotBoard_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_compactDragPending && e.LeftButton != MouseButtonState.Pressed)
            {
                CancelCompactBoardDrag();
            }
        }

        private void BeginCompactBoardDragIfAllowed(MouseButtonEventArgs e)
        {
            if (_compactDragPending || CompactModeToggleButton?.IsChecked != true)
            {
                return;
            }

            if (e.ClickCount > 1)
            {
                CancelCompactBoardDrag();
                return;
            }

            if (IsFromCompactDragBlockedElement(e.OriginalSource as DependencyObject))
            {
                return;
            }

            _compactDragPending = true;
            _compactDragStartPoint = e.GetPosition(this);
            _compactDragHoldTimer.Stop();
            _compactDragHoldTimer.Start();
        }

        private void CancelCompactBoardDrag()
        {
            _compactDragPending = false;
            _compactDragHoldTimer.Stop();
        }

        private void CompactDragHoldTimer_Tick(object? sender, EventArgs e)
        {
            _compactDragHoldTimer.Stop();

            if (!_compactDragPending || CompactModeToggleButton?.IsChecked != true || Mouse.LeftButton != MouseButtonState.Pressed)
            {
                _compactDragPending = false;
                return;
            }

            _compactDragPending = false;

            try
            {
                Mouse.Capture(null);
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // DragMove can throw if the mouse button is released during the hold boundary.
            }
        }

        private void PilotNoteButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not PilotBoardRow row)
            {
                return;
            }

            OpenPilotNotesWindow(row);
            e.Handled = true;
        }

        private void OpenPilotNotesWindow(PilotBoardRow row)
        {
            var notesWindow = new PilotNotesWindow(row, _notesRepository)
            {
                Owner = this,
                Topmost = Topmost
            };

            notesWindow.Resources.MergedDictionaries.Clear();
            foreach (var key in Resources.Keys)
            {
                notesWindow.Resources[key] = Resources[key];
            }
            notesWindow.ShowDialog();

            row.HasNotes = _notesRepository.HasNotes(row.CharacterName);
            PilotBoard.Items.Refresh();
            RefreshDetailWindowIfSelected(row);

            AppLogger.UiInfo($"Pilot notes window closed. character='{row.CharacterName}' hasNotes={row.HasNotes}");
        }

        private void WatchPilotDetailAction_Click(object sender, RoutedEventArgs e)
        {
            var selectedRow = GetSelectedOrDisplayedDetailRow();
            if (selectedRow == null)
            {
                AppLogger.UiWarn("Watch requested with no selected or displayed detail row.");
                return;
            }

            ToggleWatchForRow(selectedRow);
        }

        private void CloseDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentNotesAndTags();

            AppLogger.UiInfo("Detail pane close requested.");

            PilotBoard.SelectedItem = null;
            CloseActiveDetailWindow();
            UpdateOpenDetailsButtonState();
        }

        private void ClearBoardButton_Click(object sender, RoutedEventArgs e)
        {
            ClearBoard("Clear button");
        }

        private void ClearBoard(string reason)
        {
            PilotBoard.SelectedItem = null;
            _boardPopulationSurface.ClearBoard(
                reason,
                _currentRows,
                SaveCurrentNotesAndTags,
                CancelBoardPopulationRetry,
                () => ResetEntryAndRetryTracking(),
                ResetManualBoardSort,
                UnsubscribeFromAllBoardRows,
                RecomputeCorpAllianceCounts,
                CloseActiveDetailWindow,
                UpdateOpenDetailsButtonState,
                UpdateLastRefreshed,
                UpdateBoardPopulationStatus,
                () => _processingGeneration++);
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
            await _intelSupportSurface.RunEnableKillmailDbPullAsync();
        }


        private async void ManualUpdateCheckButton_Click(object sender, RoutedEventArgs e)
        {
            if (_manualUpdateCheckController == null)
            {
                return;
            }

            await _manualUpdateCheckController.RunAsync();
        }

        private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
        {
            _diagnosticsSupportSurface.OpenLogs();
        }

        private void PackageDiagnosticsButton_Click(object sender, RoutedEventArgs e)
        {
            _diagnosticsSupportSurface.PackageDiagnostics();
        }

        private void OpenDiagnosticsFolderButton_Click(object sender, RoutedEventArgs e)
        {
            _diagnosticsSupportSurface.OpenDiagnosticsFolder();
        }

        private void SetDiagnosticsStatus(string message)
        {
            _diagnosticsSupportSurface.SetStatus(message);
        }

        private void RefreshProviderHealthButton_Click(object sender, RoutedEventArgs e)
        {
            _diagnosticsSupportSurface.RefreshProviderHealth();
        }

        private void RefreshProviderHealthUi()
        {
            _diagnosticsSupportSurface.RefreshProviderHealthUi();
        }

        private void RefreshCacheStatsButton_Click(object sender, RoutedEventArgs e)
        {
            _diagnosticsSupportSurface.RefreshCacheStats();
        }

        private void ClearExpiredCacheButton_Click(object sender, RoutedEventArgs e)
        {
            _diagnosticsSupportSurface.ClearExpiredCache();
        }

        private void VacuumCacheButton_Click(object sender, RoutedEventArgs e)
        {
            _diagnosticsSupportSurface.VacuumCache();
        }

        private void ClearAllCacheButton_Click(object sender, RoutedEventArgs e)
        {
            _diagnosticsSupportSurface.ClearAllCache();
        }

        private async void RebuildKillmailDerivedIntelButton_Click(object sender, RoutedEventArgs e)
        {
            await _intelSupportSurface.RunRebuildKillmailDerivedIntelAsync();
        }

        private void RefreshCacheStatsUi()
        {
            _diagnosticsSupportSurface.RefreshCacheStatsUi();
        }


        private void SaveMaxKillmailAgeButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindowAppearanceController.SaveMaxKillmailAge(
                _appSettings,
                IntelSupportViewControl.MaxKillmailAgeDaysTextBoxControl,
                IntelSupportViewControl.EffectiveMaxKillmailAgeTextBlock);
        }

        private void UseDefaultMaxKillmailAgeButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindowAppearanceController.ResetMaxKillmailAgeToDefault(
                _appSettings,
                IntelSupportViewControl.MaxKillmailAgeDaysTextBoxControl,
                IntelSupportViewControl.EffectiveMaxKillmailAgeTextBlock);
        }

        private void SaveKillmailPathButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindowAppearanceController.SaveKillmailPath(
                _appSettings,
                IntelSupportViewControl.KillmailDataRootPathTextBoxControl,
                IntelSupportViewControl.KillmailDataPathModeTextBlock,
                IntelSupportViewControl.EffectiveKillmailDataPathTextBlock);
        }

        private void UseDefaultKillmailPathButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindowAppearanceController.ResetKillmailPathToDefault(
                _appSettings,
                IntelSupportViewControl.KillmailDataRootPathTextBoxControl,
                IntelSupportViewControl.KillmailDataPathModeTextBlock,
                IntelSupportViewControl.EffectiveKillmailDataPathTextBlock);
        }

        private async void EnableLiveZkillFeedCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var enabled = IntelSupportViewControl.EnableLiveZkillFeedCheckBoxControl.IsChecked == true;
            await _intelSupportSurface.HandleLiveFeedToggleAsync(enabled);
        }

        private void BackgroundHistoricalRepairEnabledCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _mainWindowSettingsCoordinator.HandleBackgroundHistoricalRepairChanged(
                _isApplyingSettings,
                _appSettings,
                IntelSupportViewControl.BackgroundHistoricalRepairEnabledCheckBoxControl.IsChecked == true);
        }

        private async void RunTodaysFreshnessButton_Click(object sender, RoutedEventArgs e)
        {
            await _intelSupportSurface.RunTodaysFreshnessAsync();
        }

        private async void RunHistoricalFreshnessButton_Click(object sender, RoutedEventArgs e)
        {
            await _intelSupportSurface.RunHistoricalFreshnessAsync();
        }

        public List<long> GetVisibleCharacterIdsForBackgroundHistoricalRepair()
        {
            return _intelSupportSurface.GetVisibleCharacterIdsForBackgroundHistoricalRepair();
        }

        private async Task RefreshCurrentBoardRowsFromLocalIntelAsync(string reason)
        {
            if (_currentRows.Count == 0)
            {
                return;
            }

            AppLogger.UiInfo($"Refreshing current Grill rows from local intel. reason='{reason}' rowCount={_currentRows.Count}");
            CancelBoardPopulationRetry();

            var generation = ++_processingGeneration;
            UpdateBoardPopulationStatus("Refreshing Grill from local intel", BoardPopulationStatusKind.Neutral);
            await ProcessRowBatchAsync(_currentRows.ToList(), generation);
            FinalizeBoardPopulationPass(generation);
            UpdateLastRefreshed();
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
            if (_mainWindowShellSurface.HandlePreviewKey(
                e.Key,
                (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control,
                IsTextEditingElement(e.OriginalSource as DependencyObject)))
            {
                e.Handled = true;
            }
        }

        private void OpenDetailsWindow(PilotBoardRow row)
        {
            _pilotDetailSurface.OpenDetailsWindow(row);
        }

        private void CloseActiveDetailWindow()
        {
            _pilotDetailSurface.CloseActiveDetailWindow();
        }

        private void ShowDetailPane(PilotBoardRow row)
        {
            _pilotDetailSurface.ShowDetailPane(row);
        }

        private void HideDetailPane()
        {
            _pilotDetailSurface.HideDetailPane();
        }

        private void SaveCurrentNotesAndTags()
        {
            _pilotDetailSurface.SaveCurrentNotesAndTags(PilotBoard?.SelectedItem as PilotBoardRow);
        }

        private void IgnoreAllianceListView_IgnoreListChanged(object? sender, EventArgs e)
        {
            ApplyIgnoredAllianceRowsToCurrentBoard();
            RecomputeCorpAllianceCounts();
        }

        private void IgnoreAllianceButton_Click(object sender, RoutedEventArgs e)
        {
            _pilotDetailSurface.TryIgnoreAllianceForSelectedOrDisplayedRow(
                PilotBoard?.SelectedItem as PilotBoardRow,
                _currentRows);
        }

        private bool TryIgnoreAllianceForRow(PilotBoardRow selectedRow)
        {
            return TryIgnoreForRow(selectedRow, IgnoreEntryType.Alliance);
        }

        private bool TryIgnoreForRow(PilotBoardRow selectedRow, IgnoreEntryType type)
        {
            var id = GetIgnoreId(selectedRow, type);
            if (!id.HasValue)
            {
                AppLogger.UiWarn($"Ignore requested without a valid ID. character='{selectedRow.CharacterName}' type={type}");
                return false;
            }

            var displayName = GetIgnoreDisplayName(selectedRow, type);
            var added = _ignoreAllianceCoordinator.AddEntryAndPersist(
                type,
                id.Value,
                $"detail window ignore {type}",
                displayName);

            if (!added)
            {
                AppLogger.UiInfo($"Ignore requested for existing entry. character='{selectedRow.CharacterName}' type={type} id='{id.Value}'");
                UpdateIgnoreAllianceButtonState(selectedRow);
                _ignoreAllianceListView?.RefreshFromCoordinator();
                return false;
            }

            AppLogger.UiInfo($"Typed ignore added from details. character='{selectedRow.CharacterName}' type={type} id='{id.Value}' name='{displayName}'");

            _ignoreAllianceListView?.RefreshFromCoordinator();
            ApplyIgnoredAllianceRowsToCurrentBoard();
            RecomputeCorpAllianceCounts();
            return true;
        }

        private PilotBoardRow? GetSelectedOrDisplayedDetailRow()
        {
            return _pilotDetailSurface.GetSelectedOrDisplayedDetailRow(
                PilotBoard?.SelectedItem as PilotBoardRow,
                _currentRows);
        }

        private void UpdateIgnoreAllianceButtonState(PilotBoardRow? row)
        {
            _pilotDetailSurface.UpdateIgnoreAllianceButtonState(row);
        }

        private void UpdateWatchPilotDetailActionState(PilotBoardRow? row)
        {
            _pilotDetailSurface.UpdateWatchPilotDetailActionState(row);
        }

        private void UpdateOpenDetailsButtonState()
        {
        }


        private static bool IsTextEditingElement(DependencyObject? source)
        {
            return FindVisualParent<TextBox>(source) != null ||
                   FindVisualParent<ComboBox>(source) != null;
        }

        private static bool IsFromCompactDragBlockedElement(DependencyObject? source)
        {
            // Rows and column headers are valid compact-mode drag surfaces.
            // Only block elements where click/hold has a separate interactive meaning.
            // DataGridColumnHeader derives from ButtonBase in WPF, so allow it before the generic button check.
            if (FindVisualParent<DataGridColumnHeader>(source) != null)
            {
                return FindVisualParent<Thumb>(source) != null;
            }

            return FindVisualParent<ButtonBase>(source) != null ||
                   FindVisualParent<ScrollBar>(source) != null ||
                   FindVisualParent<TextBox>(source) != null ||
                   FindVisualParent<ComboBox>(source) != null ||
                   FindVisualParent<Thumb>(source) != null;
        }

        private static T? FindVisualDescendant<T>(DependencyObject? root)
            where T : DependencyObject
        {
            if (root == null)
            {
                return null;
            }

            var childCount = VisualTreeHelper.GetChildrenCount(root);
            for (var index = 0; index < childCount; index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);
                if (child is T match)
                {
                    return match;
                }

                var nested = FindVisualDescendant<T>(child);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static T? FindVisualParent<T>(DependencyObject? source)
            where T : DependencyObject
        {
            while (source != null)
            {
                if (source is T match)
                {
                    return match;
                }

                source = GetParentObject(source);
            }

            return null;
        }

        private static DependencyObject? GetParentObject(DependencyObject source)
        {
            if (source is FrameworkElement frameworkElement && frameworkElement.Parent != null)
            {
                return frameworkElement.Parent;
            }

            if (source is FrameworkContentElement frameworkContentElement && frameworkContentElement.Parent != null)
            {
                return frameworkContentElement.Parent;
            }

            try
            {
                return VisualTreeHelper.GetParent(source);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
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

        private void ApplyWatchedState(PilotBoardRow row)
        {
            if (row == null)
            {
                return;
            }

            row.IsWatched = _watchedPilotRepository.IsWatched(row.CharacterId);
        }

        private void ToggleWatchForRow(PilotBoardRow row)
        {
            _pilotDetailSurface.ToggleWatchForRow(row);
        }

        private void ApplyCurrentBoardOrdering()
        {
            _boardSortController.ApplyCurrentBoardOrdering(
                _currentRows,
                PilotBoard?.SelectedItem as PilotBoardRow,
                row =>
                {
                    if (PilotBoard != null)
                    {
                        PilotBoard.SelectedItem = row;
                    }
                });
        }

        private void ResetManualBoardSort()
        {
            _boardSortController.ResetManualBoardSort(PilotBoard, CharacterColumn);
        }

        private long? GetIgnoreId(PilotBoardRow row, IgnoreEntryType type)
        {
            return type switch
            {
                IgnoreEntryType.Pilot => _pilotDetailActionsPresenter.TryGetPilotId(row.CharacterId),
                IgnoreEntryType.Corporation => _pilotDetailActionsPresenter.TryGetAllianceId(row.CorpId),
                IgnoreEntryType.Alliance => _pilotDetailActionsPresenter.TryGetAllianceId(row.AllianceId),
                _ => null
            };
        }

        private static string GetIgnoreDisplayName(PilotBoardRow row, IgnoreEntryType type)
        {
            return type switch
            {
                IgnoreEntryType.Pilot => string.IsNullOrWhiteSpace(row.CharacterName) ? "Unresolved" : row.CharacterName,
                IgnoreEntryType.Corporation => string.IsNullOrWhiteSpace(row.CorpName) ? "Unresolved" : row.CorpName,
                IgnoreEntryType.Alliance => string.IsNullOrWhiteSpace(row.AllianceName) ? "Unresolved" : row.AllianceName,
                _ => "Unresolved"
            };
        }

        private void UpdateLastRefreshed()
        {
            LastRefreshedText.Text = $"Last Refreshed: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        }

        private void UpdateBoardSummaryBanner()
        {
            _analysisTabPresenter.UpdateBoardSummary(_currentRows);
        }

        private void CurrentRows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateBoardSummaryBanner();
            UpdateAnalysisTab();
        }

        private void SubscribeToBoardRow(PilotBoardRow row)
        {
            row.PropertyChanged -= BoardRow_PropertyChanged;
            row.PropertyChanged += BoardRow_PropertyChanged;
        }

        private void UnsubscribeFromBoardRow(PilotBoardRow row)
        {
            row.PropertyChanged -= BoardRow_PropertyChanged;
        }

        private void UnsubscribeFromAllBoardRows()
        {
            foreach (var row in _currentRows)
            {
                row.PropertyChanged -= BoardRow_PropertyChanged;
            }
        }

        private void BoardRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(PilotBoardRow.IsWatched) or
                nameof(PilotBoardRow.BaitOverride) or
                nameof(PilotBoardRow.HasDerivedBaitEvidence) or
                nameof(PilotBoardRow.BoardSignalKind) or
                nameof(PilotBoardRow.CorpName) or
                nameof(PilotBoardRow.AllianceName))
            {
                UpdateBoardSummaryBanner();
                UpdateAnalysisTab();
            }
        }

        private void UpdateAnalysisTab()
        {
            _analysisTabPresenter.UpdateAnalysisTab(_currentRows);
        }

        private void AnalysisHyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            e.Handled = true;

            var url = e.Uri?.AbsoluteUri;
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            try
            {
                _browserLauncher.OpenUrl(url);
            }
            catch (Exception ex)
            {
                AppLogger.UiError($"Failed to open analysis hyperlink. url='{url}'", ex);
            }
        }

        private void AnalysisAllianceListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OpenAnalysisAffiliationItem(AnalysisAllianceListBox?.SelectedItem as AnalysisAffiliationListItem);
        }

        private void AnalysisCorpListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OpenAnalysisAffiliationItem(AnalysisCorpListBox?.SelectedItem as AnalysisAffiliationListItem);
        }

        private void OpenAnalysisAffiliationItem(AnalysisAffiliationListItem? item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id) || !long.TryParse(item.Id, out _))
            {
                return;
            }

            var url = string.Equals(item.EntityType, "alliance", StringComparison.OrdinalIgnoreCase)
                ? _analysisTabPresenter.BuildAllianceZkillUrl(item.Id)
                : _analysisTabPresenter.BuildCorporationZkillUrl(item.Id);

            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            try
            {
                _browserLauncher.OpenUrl(url);
            }
            catch (Exception ex)
            {
                AppLogger.UiError($"Failed to open analysis affiliation item. type='{item.EntityType}' id='{item.Id}'", ex);
            }
        }

        private IReadOnlyList<Rect> GetMonitorWorkAreasDip()
        {
            return FormsScreen.AllScreens
                .Select(GetScreenWorkAreaDip)
                .Where(_windowLayoutController.IsUsableWindowBounds)
                .ToList();
        }

        private Rect GetScreenWorkAreaDip(FormsScreen screen)
        {
            var workArea = screen.WorkingArea;
            var topLeft = DevicePixelsToDip(new Point(workArea.Left, workArea.Top));
            var bottomRight = DevicePixelsToDip(new Point(workArea.Right, workArea.Bottom));
            return new Rect(topLeft, bottomRight);
        }

        private Point DevicePixelsToDip(Point devicePoint)
        {
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                return source.CompositionTarget.TransformFromDevice.Transform(devicePoint);
            }

            return devicePoint;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

        private void NestedScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (sender is not System.Windows.Controls.ScrollViewer scrollViewer)
            {
                return;
            }

            if (e.Handled)
            {
                return;
            }

            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }


    }
}
