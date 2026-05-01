using PitmastersGrill.Diagnostics;
using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using PitmastersGrill.Providers;
using PitmastersGrill.Services;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using PitmastersGrill.Views;
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
        private static readonly string[] CanonicalBoardColumnOrder =
        {
            "SigColumn",
            "CharacterColumn",
            "AllianceColumn",
            "CorpColumn",
            "KillsColumn",
            "LossesColumn",
            "AvgFleetSizeColumn",
            "LastShipSeenColumn",
            "LastSeenColumn",
            "CynoHullSeenColumn"
        };
        private static readonly Dictionary<string, double> CanonicalBoardColumnMinimumWidths = new(StringComparer.OrdinalIgnoreCase)
        {
            ["SigColumn"] = 34,
            ["CharacterColumn"] = 72,
            ["AllianceColumn"] = 72,
            ["CorpColumn"] = 82,
            ["KillsColumn"] = 42,
            ["LossesColumn"] = 42,
            ["AvgFleetSizeColumn"] = 48,
            ["LastShipSeenColumn"] = 64,
            ["LastSeenColumn"] = 54,
            ["CynoHullSeenColumn"] = 70
        };
        private static readonly Dictionary<string, double> CanonicalBoardColumnDefaultWidths = new(StringComparer.OrdinalIgnoreCase)
        {
            ["SigColumn"] = 42,
            ["CharacterColumn"] = 120,
            ["AllianceColumn"] = 120,
            ["CorpColumn"] = 140,
            ["KillsColumn"] = 55,
            ["LossesColumn"] = 55,
            ["AvgFleetSizeColumn"] = 70,
            ["LastShipSeenColumn"] = 100,
            ["LastSeenColumn"] = 90,
            ["CynoHullSeenColumn"] = 110
        };

        private readonly BackgroundIntelUpdateService _backgroundIntelUpdateService;
        private AppSettings _appSettings = new();

        private readonly BoardRowFactory _boardRowFactory;
        private readonly PilotBoardRowDetailFormatter _pilotBoardRowDetailFormatter;
        private readonly DetailPaneController _detailPaneController;
        private readonly MainWindowAppearanceController _mainWindowAppearanceController;
        private readonly BoardPopulationStatusController _boardPopulationStatusController;
        private readonly BoardPopulationRowProcessor _boardPopulationRowProcessor;
        private readonly BoardPopulationPassController _boardPopulationPassController;
        private readonly BoardPopulationRetryController _boardPopulationRetryController;
        private readonly BoardPopulationEntryController _boardPopulationEntryController;
        private readonly NotesRepository _notesRepository;
        private readonly WatchedPilotRepository _watchedPilotRepository;
        private readonly ZkillUrlBuilder _zkillUrlBuilder;
        private readonly BrowserLauncher _browserLauncher;
        private readonly EveSessionContextService _eveSessionContextService;
        private readonly MainWindowDiagnostics _diagnostics;
        private readonly IntelUpdateBannerController _intelUpdateBannerController;
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
        private PilotDetailWindow? _activePilotDetailWindow;
        private bool _isApplyingSettings;
        private bool _isShuttingDown;
        private bool _compactDragPending;
        private Point _compactDragStartPoint;
        private DateTime _lastEscapeTapUtc = DateTime.MinValue;
        private int _escapeTapCount;
        private int _processingGeneration;
        private WindowState _lastNonMinimizedWindowState = WindowState.Normal;
        private Rect _lastKnownNormalBounds = Rect.Empty;
        private string? _activeBoardSortMemberPath;
        private ListSortDirection? _activeBoardSortDirection;
        private string _pendingBoardColumnLayoutSaveReason = string.Empty;
        private readonly Dictionary<string, DataGridColumn> _boardColumnsByKey = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, BoardColumnLayoutSetting> _defaultBoardColumnLayout = new(StringComparer.OrdinalIgnoreCase);
        private DependencyPropertyDescriptor? _boardColumnWidthDescriptor;
        private bool? _lastAppliedCompactMode;
        private bool _isApplyingBoardColumnLayout;
        private bool _isBoardColumnAutoFitPending;
        private bool _isBoardColumnLayoutReadyForPersistence;
        private bool _globalResetWindowHotKeyRegistered;
        private EveSessionContext? _currentEveSessionContext;
        private DateTime _lastSessionContextRefreshUtc = DateTime.MinValue;
        private bool _isSessionContextRefreshInFlight;

        public MainWindow(BackgroundIntelUpdateService backgroundIntelUpdateService)
        {
            AppLogger.UiInfo("MainWindow constructor begin.");
            _backgroundIntelUpdateService = backgroundIntelUpdateService;
            _backgroundIntelUpdateService.StatusChanged += OnIntelUpdateStatusChanged;

            var appSettingsService = new AppSettingsService();
            _mainWindowAppearanceController = new MainWindowAppearanceController(appSettingsService);
            _boardPopulationStatusController = new BoardPopulationStatusController();

            _isApplyingSettings = true;
            AppLogger.UiInfo("MainWindow InitializeComponent begin.");
            InitializeComponent();
            AppLogger.UiInfo("MainWindow InitializeComponent end.");
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
            _boardModeHintTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(BoardModeHintMilliseconds)
            };
            _boardModeHintTimer.Tick += BoardModeHintTimer_Tick;
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
            _watchedPilotRepository = composed.WatchedPilotRepository;
            _pilotBoardRowDetailFormatter = composed.PilotBoardRowDetailFormatter;
            _detailPaneController = composed.DetailPaneController;
            _boardPopulationRowProcessor = composed.BoardPopulationRowProcessor;
            _boardPopulationPassController = composed.BoardPopulationPassController;
            _boardPopulationRetryController = composed.BoardPopulationRetryController;
            _boardPopulationEntryController = composed.BoardPopulationEntryController;
            _ignoreAllianceCoordinator = composed.IgnoreAllianceCoordinator;
            _ignoreAllianceBoardController = composed.IgnoreAllianceBoardController;
            _zkillUrlBuilder = composed.ZkillUrlBuilder;
            _browserLauncher = composed.BrowserLauncher;
            _eveSessionContextService = new EveSessionContextService();

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
            _mainWindowAppearanceController.ApplyPanelModeShell(this, _appSettings, Resources);
            CompactModeToggleButton.IsChecked = _appSettings.CompactModeEnabled;

            _mainWindowAppearanceController.InitializeSettingsUi(
                _appSettings,
                DarkModeCheckBox,
                AlwaysOnTopCheckBox,
                PanelModeCheckBox,
                PanelModeRestartNoticeText,
                WindowOpacitySlider,
                WindowOpacityValueText,
                MaxKillmailAgeDaysTextBox,
                EffectiveMaxKillmailAgeText,
                KillmailDataRootPathTextBox,
                KillmailDataPathModeText,
                EffectiveKillmailDataPathText,
                VisualThemeComboBox,
                ColorBlindModeComboBox,
                LogLevelComboBox);
            EnableLiveZkillFeedCheckBox.IsChecked = _appSettings.LiveZkillFeedEnabled;
            BackgroundHistoricalRepairEnabledCheckBox.IsChecked = _appSettings.BackgroundHistoricalRepairEnabled;

            InitializeBoardColumnVisibilityUi();
            InitializeBoardColumnLayoutUi();
            InitializeBoardDisplaySettingsUi();
            InitializePilotDetailPlacementUi();

            AppLogger.ConfigureLogLevel(_appSettings.LogLevel);

            _isApplyingSettings = false;

            _mainWindowAppearanceController.ApplyTheme(Resources, _appSettings, this, ApplyBoardPopulationStatusVisual);
            _mainWindowAppearanceController.ApplyWindowSettings(this, _appSettings, WindowOpacityValueText, Resources);
            UpdateWindowMinimumSize();

            PilotBoard.ItemsSource = _currentRows;
            _currentRows.CollectionChanged += CurrentRows_CollectionChanged;
            AnalysisAllianceListBox.ItemsSource = _analysisAllianceItems;
            AnalysisCorpListBox.ItemsSource = _analysisCorpItems;
            ProviderHealthGrid.ItemsSource = _providerHealthRows;
            RefreshProviderHealthUi();
            RefreshCacheStatsUi();
            UpdateLastRefreshed();
            UpdateBoardPopulationStatus("Board population idle", BoardPopulationStatusKind.Neutral);
            HideDetailPane();
            UpdateOpenDetailsButtonState();
            MainTabControl.SelectedIndex = 1;
            ApplyCompactModeUi();
            UpdateBoardSummaryBanner();
            UpdateAnalysisTab();
            ApplyIntelUpdateSnapshot(_backgroundIntelUpdateService.GetSnapshot());
            ApplyEveSessionContext(new EveSessionContext());

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
            AddClipboardFormatListener(hwnd);

            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);

            _mainWindowAppearanceController.ApplyTitleBarTheme(this, _appSettings.DarkModeEnabled);
            RestoreWindowLayoutFromSettings();
            TryRegisterGlobalResetWindowHotKey(hwnd);
            UpdateWindowStateUi();
            TriggerSessionContextRefresh("startup", force: false);

            AppLogger.UiInfo("MainWindow source initialized. Clipboard listener attached and title bar theme applied.");
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainWindow_Loaded;
            AppLogger.UiInfo("MainWindow loaded.");
            UpdateWindowMinimumSize();
            Dispatcher.BeginInvoke(new Action(FinalizeBoardColumnLayoutInitialization), DispatcherPriority.Loaded);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            SaveWindowLayoutToSettings("Window closing");
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            AppLogger.UiInfo("MainWindow closing requested.");
            _isShuttingDown = true;

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
            TryUnregisterGlobalResetWindowHotKey(hwnd);
            RemoveClipboardFormatListener(hwnd);

            AppLogger.UiInfo("MainWindow closed. Clipboard listener removed, retry state cancelled, and background work stop requested.");

            base.OnClosed(e);
        }

        private void ExitApplicationButton_Click(object sender, RoutedEventArgs e)
        {
            RequestApplicationShutdown("Exit button");
        }


        private void CompactModeToggleButton_Changed(object sender, RoutedEventArgs e)
        {
            ApplyCompactModeUi();
        }

        private void ApplyCompactModeUi()
        {
            if (CompactModeToggleButton == null || MainContentGrid == null || TopCommandGrid == null || MainTabControl == null || BoardStatusFooter == null)
            {
                return;
            }

            var compact = CompactModeToggleButton.IsChecked == true;
            var previousCompactMode = _lastAppliedCompactMode;
            _lastAppliedCompactMode = compact;

            if (compact)
            {
                MainTabControl.SelectedIndex = 1;
                CloseActiveDetailWindow();
            }

            TopCommandGrid.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            TopCommandGrid.Margin = new Thickness(0, 0, 0, 6);
            BoardStatusFooter.Padding = new Thickness(8, 5, 8, 5);
            MainContentGrid.Margin = compact ? new Thickness(1) : new Thickness(12);
            MainTabControl.BorderThickness = compact ? new Thickness(0) : new Thickness(1);
            MainTabControl.Margin = compact ? new Thickness(0) : new Thickness(0);

            if (!_isApplyingSettings && _appSettings.CompactModeEnabled != compact)
            {
                _appSettings.CompactModeEnabled = compact;
                _mainWindowAppearanceController.SaveSettings(_appSettings);
            }

            if (!_isApplyingSettings &&
                (!previousCompactMode.HasValue || previousCompactMode.Value != compact))
            {
                AppLogger.UiInfo($"Display mode changed. boardMode={compact}");
            }

            if (compact && !_isApplyingSettings &&
                (!previousCompactMode.HasValue || previousCompactMode.Value != compact))
            {
                ShowBoardModeHint();
            }
            else if (!compact)
            {
                HideBoardModeHint();
            }

            UpdateWindowMinimumSize();
            UpdateBoardFooterVisibility();
            UpdateBoardSummaryBanner();
            UpdateAnalysisTab();
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(sender, MainTabControl))
            {
                return;
            }

            UpdateBoardFooterVisibility();

            if (MainTabControl.SelectedIndex == 0)
            {
                TriggerSessionContextRefresh("analysis tab selection", force: IsSessionContextStale());
            }
            else if (MainTabControl.SelectedIndex == 1)
            {
                ScheduleFitVisibleBoardColumnsToViewport(force: true);
            }
        }

        private void UpdateBoardFooterVisibility()
        {
            if (CompactModeToggleButton == null || MainTabControl == null || BoardStatusFooter == null)
            {
                return;
            }

            var boardMode = CompactModeToggleButton.IsChecked == true;
            var analysisTabSelected = MainTabControl.SelectedIndex == 0;
            BoardStatusFooter.Visibility = boardMode || analysisTabSelected
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void ToggleCompactModeFromHotkey()
        {
            if (CompactModeToggleButton == null)
            {
                return;
            }

            CompactModeToggleButton.IsChecked = CompactModeToggleButton.IsChecked != true;
            ApplyCompactModeUi();
        }

        private void ShowBoardModeHint()
        {
            if (BoardModeHintOverlay == null)
            {
                return;
            }

            BoardModeHintOverlay.Visibility = Visibility.Visible;
            _boardModeHintTimer.Stop();
            _boardModeHintTimer.Start();
        }

        private void HideBoardModeHint()
        {
            if (BoardModeHintOverlay == null)
            {
                return;
            }

            _boardModeHintTimer.Stop();
            BoardModeHintOverlay.Visibility = Visibility.Collapsed;
        }

        private void BoardModeHintTimer_Tick(object? sender, EventArgs e)
        {
            HideBoardModeHint();
        }

        private void UpdateWindowMinimumSize()
        {
            if (CompactModeToggleButton?.IsChecked == true)
            {
                MinWidth = BoardModeMinimumWindowWidth;
                MinHeight = GetBoardModeMinimumWindowHeight();
                return;
            }

            MinWidth = NormalModeMinimumWindowWidth;
            MinHeight = NormalModeMinimumWindowHeight;
        }

        private double GetBoardModeMinimumWindowHeight()
        {
            var contentMarginHeight = MainContentGrid?.Margin.Top + MainContentGrid?.Margin.Bottom ?? 0;
            var commandStripHeight = Math.Max(TopCommandGrid?.ActualHeight ?? 0, BoardModeFallbackCommandStripHeight);
            var tabHeaderHeight = Math.Max(GetTabHeaderHeight(), BoardModeFallbackTabHeaderHeight);
            var boardColumnHeaderHeight = Math.Max(GetBoardColumnHeaderHeight(), BoardModeFallbackColumnHeaderHeight);
            var boardRowHeight = Math.Max(GetBoardRowHeight(), Math.Ceiling((PilotBoard?.FontSize ?? 12) + BoardModeFallbackRowVerticalPadding));

            return Math.Ceiling(
                contentMarginHeight +
                commandStripHeight +
                tabHeaderHeight +
                boardColumnHeaderHeight +
                boardRowHeight +
                BoardModeFallbackFooterPaddingHeight);
        }

        private double GetTabHeaderHeight()
        {
            return FindVisualDescendant<TabPanel>(MainTabControl)?.ActualHeight ?? 0;
        }

        private double GetBoardColumnHeaderHeight()
        {
            return FindVisualDescendant<DataGridColumnHeadersPresenter>(PilotBoard)?.ActualHeight ?? 0;
        }

        private double GetBoardRowHeight()
        {
            if (PilotBoard == null)
            {
                return 0;
            }

            for (var index = 0; index < Math.Min(PilotBoard.Items.Count, 3); index++)
            {
                if (PilotBoard.ItemContainerGenerator.ContainerFromIndex(index) is DataGridRow row &&
                    row.ActualHeight > 0)
                {
                    return row.ActualHeight;
                }
            }

            return 0;
        }

        private void ToggleMaximizeRestore()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void UpdateWindowStateUi()
        {
            if (MaximizeRestoreWindowButton == null)
            {
                return;
            }

            var isMaximized = WindowState == WindowState.Maximized;
            MaximizeRestoreWindowButton.Content = isMaximized ? "O" : "[]";
            MaximizeRestoreWindowButton.ToolTip = isMaximized ? "Restore PMG" : "Maximize PMG";
        }

        private void TrackCurrentNormalWindowBounds(string reason)
        {
            if (WindowState != WindowState.Normal)
            {
                return;
            }

            var currentBounds = new Rect(Left, Top, Width, Height);
            if (!IsUsableWindowBounds(currentBounds))
            {
                return;
            }

            _lastKnownNormalBounds = currentBounds;
        }

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
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreWindowButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximizeRestore();
        }

        private void CloseWindowButton_Click(object sender, RoutedEventArgs e)
        {
            RequestApplicationShutdown("Window close button");
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState != WindowState.Minimized)
            {
                _lastNonMinimizedWindowState = WindowState;
            }

            if (WindowState == WindowState.Maximized && IsUsableWindowBounds(RestoreBounds))
            {
                _lastKnownNormalBounds = RestoreBounds;
            }
            else if (WindowState == WindowState.Normal)
            {
                TrackCurrentNormalWindowBounds("StateChanged");
            }

            UpdateWindowStateUi();
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            TrackCurrentNormalWindowBounds("LocationChanged");
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            TrackCurrentNormalWindowBounds("SizeChanged");
            UpdateWindowMinimumSize();
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
            _activePilotDetailWindow?.ApplyThemeResources(Resources);
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

        private void PanelModeCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isApplyingSettings)
            {
                return;
            }

            _mainWindowAppearanceController.HandlePanelModeChanged(
                _appSettings,
                PanelModeCheckBox.IsChecked == true,
                PanelModeRestartNoticeText);
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
            _activePilotDetailWindow?.ApplyThemeResources(Resources);
        }

        private void ResetWindowLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            ResetWindowLayout(showConfirmation: true, reason: "Reset window layout button");
        }

        private void ResetWindowLayout(bool showConfirmation, string reason)
        {
            ClearSavedWindowLayoutSettings();

            var resetBounds = GetDefaultWindowBoundsForCurrentDisplay();
            WindowState = WindowState.Normal;
            Left = resetBounds.Left;
            Top = resetBounds.Top;
            Width = resetBounds.Width;
            Height = resetBounds.Height;
            _lastKnownNormalBounds = resetBounds;
            _lastNonMinimizedWindowState = WindowState.Normal;

            SaveWindowLayoutToSettings(reason);

            AppLogger.UiInfo(
                $"Window layout reset. reason='{reason}' left={Left:0.##} top={Top:0.##} width={Width:0.##} height={Height:0.##}");

            if (showConfirmation)
            {
                MessageBox.Show(
                    "Window layout reset to a safe default position and size.",
                    "PMG Window Layout",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void RequestWindowLayoutResetFromHotkey(string source)
        {
            AppLogger.UiInfo($"Window layout reset requested from {source}.");
            ResetWindowLayout(showConfirmation: false, reason: source);
        }

        private void LogLevelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isApplyingSettings || LogLevelComboBox == null)
            {
                return;
            }

            _mainWindowAppearanceController.HandleLogLevelChanged(_appSettings, LogLevelComboBox);
        }

        private void VisualThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isApplyingSettings || VisualThemeComboBox == null)
            {
                return;
            }

            _mainWindowAppearanceController.HandleVisualThemeChanged(
                _appSettings,
                VisualThemeComboBox,
                Resources,
                this,
                ApplyBoardPopulationStatusVisual);
            _activePilotDetailWindow?.ApplyThemeResources(Resources);
        }

        private void ColorBlindModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isApplyingSettings || ColorBlindModeComboBox == null)
            {
                return;
            }

            _mainWindowAppearanceController.HandleColorBlindModeChanged(
                _appSettings,
                ColorBlindModeComboBox,
                Resources,
                this,
                ApplyBoardPopulationStatusVisual);
            _activePilotDetailWindow?.ApplyThemeResources(Resources);
            PilotBoard?.Items.Refresh();
        }


        private void InitializePilotDetailPlacementUi()
        {
            if (PilotDetailPlacementComboBox == null)
            {
                return;
            }

            var preference = GetPilotDetailPlacementPreference();
            PilotDetailPlacementComboBox.SelectedIndex = preference == PilotDetailPlacementPreference.AutoPreferLeft ? 1 : 0;
        }

        private void PilotDetailPlacementComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isApplyingSettings || PilotDetailPlacementComboBox == null)
            {
                return;
            }

            _appSettings.PilotDetailPlacementPreference =
                PilotDetailPlacementComboBox.SelectedIndex == 1
                    ? PilotDetailPlacementPreference.AutoPreferLeft.ToString()
                    : PilotDetailPlacementPreference.AutoPreferRight.ToString();

            _mainWindowAppearanceController.SaveSettings(_appSettings);
            AppLogger.UiInfo($"Pilot detail placement preference changed. preference={_appSettings.PilotDetailPlacementPreference}");
        }

        private void InitializeBoardColumnVisibilityUi()
        {
            ApplyBoardColumnSettingsToCheckBoxes();
            ApplyBoardColumnVisibility();
        }

        private void InitializeBoardColumnLayoutUi()
        {
            CacheBoardColumnsByKey();
            ApplyBoardColumnMinimumWidths();
            BuildCanonicalBoardColumnLayout();
            ApplyCanonicalBoardColumnLayout("Apply canonical default board layout");
        }

        private void InitializeBoardDisplaySettingsUi()
        {
            ApplyBoardDisplaySettingsToControls();
            ApplyBoardDisplaySettings();
        }

        private void ApplyBoardDisplaySettingsToControls()
        {
            var wasApplyingSettings = _isApplyingSettings;
            _isApplyingSettings = true;

            try
            {
                if (ShowBoardGridLinesCheckBox != null)
                {
                    ShowBoardGridLinesCheckBox.IsChecked = _appSettings.ShowBoardGridLines;
                }

                if (BoardTextSizeComboBox != null)
                {
                    BoardTextSizeComboBox.SelectedIndex = Math.Max(0, Math.Min(6, _appSettings.BoardTextSize - 10));
                }

                if (BoardFontFamilyComboBox != null)
                {
                    BoardFontFamilyComboBox.SelectedIndex = string.IsNullOrWhiteSpace(_appSettings.BoardFontFamily)
                        ? 0
                        : string.Equals(_appSettings.BoardFontFamily, "Consolas", StringComparison.Ordinal)
                            ? 2
                            : string.Equals(_appSettings.BoardFontFamily, "Bahnschrift", StringComparison.Ordinal)
                                ? 3
                                : 1;
                }
            }
            finally
            {
                _isApplyingSettings = wasApplyingSettings;
            }
        }

        private void ApplyBoardDisplaySettings()
        {
            if (PilotBoard == null)
            {
                return;
            }

            var showGridLines = _appSettings.ShowBoardGridLines;
            PilotBoard.GridLinesVisibility = showGridLines ? DataGridGridLinesVisibility.All : DataGridGridLinesVisibility.None;
            PilotBoard.HorizontalGridLinesBrush = showGridLines
                ? (Brush)Resources["GridLineBrush"]
                : Brushes.Transparent;
            PilotBoard.VerticalGridLinesBrush = showGridLines
                ? (Brush)Resources["GridLineBrush"]
                : Brushes.Transparent;
            PilotBoard.ColumnHeaderStyle = (Style)Resources[showGridLines
                ? "PilotBoardColumnHeaderStyle"
                : "PilotBoardColumnHeaderNoGridStyle"];
            PilotBoard.CellStyle = (Style)Resources[showGridLines
                ? "PilotBoardCellStyle"
                : "PilotBoardCellNoGridStyle"];
            PilotBoard.FontSize = _appSettings.BoardTextSize;
            PilotBoard.FontFamily = string.IsNullOrWhiteSpace(_appSettings.BoardFontFamily)
                ? SystemFonts.MessageFontFamily
                : new FontFamily(_appSettings.BoardFontFamily);
        }

        private void ShowBoardGridLinesCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isApplyingSettings || ShowBoardGridLinesCheckBox == null)
            {
                return;
            }

            _appSettings.ShowBoardGridLines = ShowBoardGridLinesCheckBox.IsChecked == true;
            ApplyBoardDisplaySettings();
            _mainWindowAppearanceController.SaveSettings(_appSettings);

            AppLogger.UiInfo($"Board grid lines changed. enabled={_appSettings.ShowBoardGridLines}");
        }

        private void BoardTextSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isApplyingSettings || BoardTextSizeComboBox == null)
            {
                return;
            }

            _appSettings.BoardTextSize = BoardTextSizeComboBox.SelectedIndex + 10;

            ApplyBoardDisplaySettings();
            UpdateWindowMinimumSize();
            _mainWindowAppearanceController.SaveSettings(_appSettings);

            AppLogger.UiInfo($"Board text size changed. size={_appSettings.BoardTextSize}");
        }

        private void BoardFontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isApplyingSettings || BoardFontFamilyComboBox == null)
            {
                return;
            }

            _appSettings.BoardFontFamily = BoardFontFamilyComboBox.SelectedIndex switch
            {
                1 => "Segoe UI",
                2 => "Consolas",
                3 => "Bahnschrift",
                _ => string.Empty
            };

            ApplyBoardDisplaySettings();
            UpdateWindowMinimumSize();
            _mainWindowAppearanceController.SaveSettings(_appSettings);

            AppLogger.UiInfo(
                $"Board font family changed. family='{(_appSettings.BoardFontFamily.Length == 0 ? "Default" : _appSettings.BoardFontFamily)}'");
        }

        private void BoardColumnVisibilityCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isApplyingSettings)
            {
                return;
            }

            SaveBoardColumnSettingsFromCheckBoxes();
            ApplyBoardColumnVisibility();
            _mainWindowAppearanceController.SaveSettings(_appSettings);

            AppLogger.UiInfo(
                $"Board column visibility changed. sig={IsEnabled(ShowSigColumnCheckBox)} alliance={IsEnabled(ShowAllianceColumnCheckBox)} corp={IsEnabled(ShowCorpColumnCheckBox)} kills={IsEnabled(ShowKillsColumnCheckBox)} losses={IsEnabled(ShowLossesColumnCheckBox)} avgFleet={IsEnabled(ShowAvgFleetSizeColumnCheckBox)} lastShip={IsEnabled(ShowLastShipSeenColumnCheckBox)} lastSeen={IsEnabled(ShowLastSeenColumnCheckBox)} cynoHull={IsEnabled(ShowCynoHullSeenColumnCheckBox)}");
        }

        private void ShowCorpAllianceCountsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isApplyingSettings)
            {
                return;
            }

            _appSettings.ShowCorpAllianceCounts = ShowCorpAllianceCountsCheckBox.IsChecked == true;
            RecomputeCorpAllianceCounts();
            _mainWindowAppearanceController.SaveSettings(_appSettings);

            AppLogger.UiInfo($"Corp/alliance board counts changed. enabled={_appSettings.ShowCorpAllianceCounts}");
        }

        private void ShowAllBoardColumnsButton_Click(object sender, RoutedEventArgs e)
        {
            SetAllOptionalBoardColumnSettings(true);
            ApplyBoardColumnSettingsToCheckBoxes();
            ApplyBoardColumnVisibility();
            _mainWindowAppearanceController.SaveSettings(_appSettings);

            AppLogger.UiInfo("Board column visibility reset to show all optional columns.");
        }

        private void ResetBoardColumnsButton_Click(object sender, RoutedEventArgs e)
        {
            SetAllOptionalBoardColumnSettings(true);
            ApplyBoardColumnSettingsToCheckBoxes();
            ApplyBoardColumnVisibility();
            _mainWindowAppearanceController.SaveSettings(_appSettings);

            AppLogger.UiInfo("Board column visibility reset to defaults.");
        }

        private void ResetBoardLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            _appSettings.BoardColumnLayout.Clear();
            ApplyCanonicalBoardColumnLayout("Reset board layout to canonical defaults");
            SaveCurrentBoardColumnLayout("Reset layout");

            AppLogger.UiInfo("Board column layout reset to canonical defaults.");
        }

        private void ApplyBoardColumnSettingsToCheckBoxes()
        {
            if (ShowSigColumnCheckBox == null)
            {
                return;
            }

            var wasApplyingSettings = _isApplyingSettings;
            _isApplyingSettings = true;

            try
            {
                ShowSigColumnCheckBox.IsChecked = IsEnabled(_appSettings.ShowSigColumn);
                ShowAllianceColumnCheckBox.IsChecked = IsEnabled(_appSettings.ShowAllianceColumn);
                ShowCorpColumnCheckBox.IsChecked = IsEnabled(_appSettings.ShowCorpColumn);
                ShowKillsColumnCheckBox.IsChecked = IsEnabled(_appSettings.ShowKillsColumn);
                ShowLossesColumnCheckBox.IsChecked = IsEnabled(_appSettings.ShowLossesColumn);
                ShowAvgFleetSizeColumnCheckBox.IsChecked = IsEnabled(_appSettings.ShowAvgFleetSizeColumn);
                ShowLastShipSeenColumnCheckBox.IsChecked = IsEnabled(_appSettings.ShowLastShipSeenColumn);
                ShowLastSeenColumnCheckBox.IsChecked = IsEnabled(_appSettings.ShowLastSeenColumn);
                ShowCynoHullSeenColumnCheckBox.IsChecked = IsEnabled(_appSettings.ShowCynoHullSeenColumn);
                ShowCorpAllianceCountsCheckBox.IsChecked = _appSettings.ShowCorpAllianceCounts;
            }
            finally
            {
                _isApplyingSettings = wasApplyingSettings;
            }
        }

        private void SaveBoardColumnSettingsFromCheckBoxes()
        {
            _appSettings.ShowSigColumn = IsEnabled(ShowSigColumnCheckBox);
            _appSettings.ShowAllianceColumn = IsEnabled(ShowAllianceColumnCheckBox);
            _appSettings.ShowCorpColumn = IsEnabled(ShowCorpColumnCheckBox);
            _appSettings.ShowKillsColumn = IsEnabled(ShowKillsColumnCheckBox);
            _appSettings.ShowLossesColumn = IsEnabled(ShowLossesColumnCheckBox);
            _appSettings.ShowAvgFleetSizeColumn = IsEnabled(ShowAvgFleetSizeColumnCheckBox);
            _appSettings.ShowLastShipSeenColumn = IsEnabled(ShowLastShipSeenColumnCheckBox);
            _appSettings.ShowLastSeenColumn = IsEnabled(ShowLastSeenColumnCheckBox);
            _appSettings.ShowCynoHullSeenColumn = IsEnabled(ShowCynoHullSeenColumnCheckBox);
        }

        private void ApplyBoardColumnVisibility()
        {
            _isApplyingBoardColumnLayout = true;

            try
            {
                SetColumnVisibility(SigColumn, _appSettings.ShowSigColumn);
                CharacterColumn.Visibility = Visibility.Visible;
                SetColumnVisibility(AllianceColumn, _appSettings.ShowAllianceColumn);
                SetColumnVisibility(CorpColumn, _appSettings.ShowCorpColumn);
                SetColumnVisibility(KillsColumn, _appSettings.ShowKillsColumn);
                SetColumnVisibility(LossesColumn, _appSettings.ShowLossesColumn);
                SetColumnVisibility(AvgFleetSizeColumn, _appSettings.ShowAvgFleetSizeColumn);
                SetColumnVisibility(LastShipSeenColumn, _appSettings.ShowLastShipSeenColumn);
                SetColumnVisibility(LastSeenColumn, _appSettings.ShowLastSeenColumn);
                SetColumnVisibility(CynoHullSeenColumn, _appSettings.ShowCynoHullSeenColumn);
            }
            finally
            {
                _isApplyingBoardColumnLayout = false;
            }

            ScheduleFitVisibleBoardColumnsToViewport(force: true);
        }

        private void ApplyBoardColumnMinimumWidths()
        {
            foreach (var pair in _boardColumnsByKey)
            {
                pair.Value.MinWidth = GetBoardColumnMinimumWidth(pair.Key);
            }
        }

        private void CacheBoardColumnsByKey()
        {
            _boardColumnsByKey.Clear();
            RegisterBoardColumn("SigColumn", SigColumn);
            RegisterBoardColumn("CharacterColumn", CharacterColumn);
            RegisterBoardColumn("AllianceColumn", AllianceColumn);
            RegisterBoardColumn("CorpColumn", CorpColumn);
            RegisterBoardColumn("KillsColumn", KillsColumn);
            RegisterBoardColumn("LossesColumn", LossesColumn);
            RegisterBoardColumn("AvgFleetSizeColumn", AvgFleetSizeColumn);
            RegisterBoardColumn("LastShipSeenColumn", LastShipSeenColumn);
            RegisterBoardColumn("LastSeenColumn", LastSeenColumn);
            RegisterBoardColumn("CynoHullSeenColumn", CynoHullSeenColumn);
        }

        private void RegisterBoardColumn(string key, DataGridColumn column)
        {
            if (column == null)
            {
                return;
            }

            _boardColumnsByKey[key] = column;
        }

        private void BuildCanonicalBoardColumnLayout()
        {
            _defaultBoardColumnLayout.Clear();

            for (var index = 0; index < CanonicalBoardColumnOrder.Length; index++)
            {
                var key = CanonicalBoardColumnOrder[index];
                if (!_boardColumnsByKey.TryGetValue(key, out var column))
                {
                    continue;
                }

                _defaultBoardColumnLayout[key] = CreateCanonicalBoardColumnLayoutSetting(key, index);
            }
        }

        private void HookBoardColumnWidthTracking()
        {
            if (_boardColumnWidthDescriptor != null)
            {
                return;
            }

            _boardColumnWidthDescriptor = DependencyPropertyDescriptor.FromProperty(
                DataGridColumn.WidthProperty,
                typeof(DataGridColumn));

            if (_boardColumnWidthDescriptor == null)
            {
                AppLogger.UiWarn("Board column width tracking could not be initialized.");
                return;
            }

            foreach (var column in _boardColumnsByKey.Values)
            {
                _boardColumnWidthDescriptor.AddValueChanged(column, BoardColumnWidth_ValueChanged);
            }
        }

        private void ApplySavedBoardColumnLayout()
        {
            if (_appSettings.BoardColumnLayout == null || _appSettings.BoardColumnLayout.Count == 0)
            {
                return;
            }

            if (!TryValidateSavedBoardColumnLayout(_appSettings.BoardColumnLayout, out var validSavedSettings, out var validationFailureReason))
            {
                AppLogger.UiWarn($"Saved board column layout discarded. reason='{validationFailureReason}'");
                _appSettings.BoardColumnLayout.Clear();
                _mainWindowAppearanceController.SaveSettings(_appSettings);
                ApplyCanonicalBoardColumnLayout("Discard invalid saved board layout");
                return;
            }

            ApplyBoardColumnLayout(validSavedSettings, "Restore saved board layout");
        }

        private void ApplyCanonicalBoardColumnLayout(string reason)
        {
            ApplyBoardColumnLayout(
                CanonicalBoardColumnOrder
                    .Where(key => _defaultBoardColumnLayout.ContainsKey(key))
                    .Select(key => _defaultBoardColumnLayout[key])
                    .ToList(),
                reason);
        }

        private void ApplyBoardColumnLayout(IEnumerable<BoardColumnLayoutSetting> layoutSettings, string reason)
        {
            if (layoutSettings == null)
            {
                return;
            }

            _isApplyingBoardColumnLayout = true;

            try
            {
                var widthSettings = layoutSettings
                    .Where(setting => setting != null && !string.IsNullOrWhiteSpace(setting.ColumnKey))
                    .Where(setting => _boardColumnsByKey.ContainsKey(setting.ColumnKey))
                    .ToList();

                foreach (var setting in widthSettings)
                {
                    if (TryBuildDataGridLength(setting, out var width))
                    {
                        _boardColumnsByKey[setting.ColumnKey].Width = width;
                    }
                }

                var orderedKeys = widthSettings
                    .OrderBy(setting => setting.DisplayIndex)
                    .ThenBy(setting => setting.ColumnKey, StringComparer.OrdinalIgnoreCase)
                    .Select(setting => setting.ColumnKey)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var defaultKey in CanonicalBoardColumnOrder)
                {
                    if (!orderedKeys.Contains(defaultKey, StringComparer.OrdinalIgnoreCase))
                    {
                        orderedKeys.Add(defaultKey);
                    }
                }

                for (var index = 0; index < orderedKeys.Count; index++)
                {
                    if (_boardColumnsByKey.TryGetValue(orderedKeys[index], out var column) &&
                        column.DisplayIndex != index)
                    {
                        column.DisplayIndex = index;
                    }
                }

                ScheduleFitVisibleBoardColumnsToViewport();

                AppLogger.UiInfo($"Board column layout applied. reason='{reason}'");
            }
            finally
            {
                _isApplyingBoardColumnLayout = false;
            }
        }

        private static bool TryBuildDataGridLength(BoardColumnLayoutSetting setting, out DataGridLength width)
        {
            width = DataGridLength.Auto;

            if (setting == null || string.IsNullOrWhiteSpace(setting.WidthUnitType))
            {
                return false;
            }

            if (double.IsNaN(setting.WidthValue) || double.IsInfinity(setting.WidthValue) || setting.WidthValue <= 0)
            {
                return false;
            }

            width = new DataGridLength(setting.WidthValue, DataGridLengthUnitType.Pixel);
            return true;
        }

        private void PilotBoard_ColumnReordered(object sender, DataGridColumnEventArgs e)
        {
            ScheduleFitVisibleBoardColumnsToViewport();
            ScheduleBoardColumnLayoutSave("Column reordered");
        }

        private void PilotBoard_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ScheduleFitVisibleBoardColumnsToViewport();
        }

        private void BoardColumnWidth_ValueChanged(object? sender, EventArgs e)
        {
            ScheduleBoardColumnLayoutSave("Column width changed");
        }

        private void ScheduleBoardColumnLayoutSave(string reason)
        {
            if (_isApplyingSettings || _isApplyingBoardColumnLayout || !CanPersistBoardColumnLayout())
            {
                return;
            }

            _boardColumnLayoutSaveTimer.Stop();
            _pendingBoardColumnLayoutSaveReason = reason;
            _boardColumnLayoutSaveTimer.Start();
        }

        private void BoardColumnLayoutSaveTimer_Tick(object? sender, EventArgs e)
        {
            _boardColumnLayoutSaveTimer.Stop();
            var reason = string.IsNullOrWhiteSpace(_pendingBoardColumnLayoutSaveReason)
                ? "Board layout changed"
                : _pendingBoardColumnLayoutSaveReason;
            _pendingBoardColumnLayoutSaveReason = string.Empty;
            SaveCurrentBoardColumnLayout(reason);
        }

        private void SaveCurrentBoardColumnLayout(string reason)
        {
            if (!CanPersistBoardColumnLayout())
            {
                AppLogger.UiDebug($"Board column layout save skipped. reason='{reason}' hostReady=false");
                return;
            }

            var currentLayout = _boardColumnsByKey
                .Select(pair => CreateBoardColumnLayoutSetting(pair.Key, pair.Value))
                .OrderBy(setting => setting.DisplayIndex)
                .ThenBy(setting => setting.ColumnKey, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!TryValidateSavedBoardColumnLayout(currentLayout, out var sanitizedLayout, out var validationFailureReason))
            {
                AppLogger.UiWarn($"Board column layout save skipped. reason='{reason}' validationFailure='{validationFailureReason}'");
                return;
            }

            if (BoardColumnLayoutsMatch(_appSettings.BoardColumnLayout, sanitizedLayout))
            {
                return;
            }

            _appSettings.BoardColumnLayout = sanitizedLayout;
            _mainWindowAppearanceController.SaveSettings(_appSettings);
            AppLogger.UiInfo($"Board column layout saved. reason='{reason}'");
        }

        private static bool BoardColumnLayoutsMatch(
            IReadOnlyList<BoardColumnLayoutSetting>? left,
            IReadOnlyList<BoardColumnLayoutSetting>? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (var index = 0; index < left.Count; index++)
            {
                var leftItem = left[index];
                var rightItem = right[index];

                if (!string.Equals(leftItem?.ColumnKey, rightItem?.ColumnKey, StringComparison.OrdinalIgnoreCase) ||
                    leftItem?.DisplayIndex != rightItem?.DisplayIndex ||
                    leftItem?.WidthUnitType != rightItem?.WidthUnitType ||
                    Math.Abs((leftItem?.WidthValue ?? 0d) - (rightItem?.WidthValue ?? 0d)) > 0.01d)
                {
                    return false;
                }
            }

            return true;
        }

        private static BoardColumnLayoutSetting CreateBoardColumnLayoutSetting(string key, DataGridColumn column, int? displayIndexOverride = null)
        {
            var width = column.ActualWidth;
            if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
            {
                width = column.Width.DisplayValue;
            }

            if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
            {
                width = GetBoardColumnMinimumWidth(key);
            }

            return new BoardColumnLayoutSetting
            {
                ColumnKey = key,
                DisplayIndex = displayIndexOverride ?? column.DisplayIndex,
                WidthValue = Math.Max(GetBoardColumnMinimumWidth(key), width),
                WidthUnitType = DataGridLengthUnitType.Pixel.ToString()
            };
        }

        private static BoardColumnLayoutSetting CreateCanonicalBoardColumnLayoutSetting(string key, int displayIndex)
        {
            return new BoardColumnLayoutSetting
            {
                ColumnKey = key,
                DisplayIndex = displayIndex,
                WidthValue = GetBoardColumnDefaultWidth(key),
                WidthUnitType = DataGridLengthUnitType.Pixel.ToString()
            };
        }

        private void FinalizeBoardColumnLayoutInitialization()
        {
            ApplyCanonicalBoardColumnLayout("Finalize board layout after load");
            ApplySavedBoardColumnLayout();
            HookBoardColumnWidthTracking();
            _isBoardColumnLayoutReadyForPersistence = true;
            AppLogger.UiInfo($"Board column layout initialization complete. hostReady={IsBoardLayoutHostReady()} actualWidth={PilotBoard?.ActualWidth ?? 0:0.##}");
        }

        private bool CanPersistBoardColumnLayout()
        {
            return _isBoardColumnLayoutReadyForPersistence && IsBoardLayoutHostReady();
        }

        private bool IsBoardLayoutHostReady()
        {
            return PilotBoard != null &&
                   IsLoaded &&
                   PilotBoard.IsLoaded &&
                   PilotBoard.ActualWidth >= MinimumBoardLayoutHostWidth;
        }

        private bool TryValidateSavedBoardColumnLayout(
            IReadOnlyList<BoardColumnLayoutSetting>? layoutSettings,
            out List<BoardColumnLayoutSetting> validLayout,
            out string failureReason)
        {
            validLayout = new List<BoardColumnLayoutSetting>();
            failureReason = string.Empty;

            if (layoutSettings == null || layoutSettings.Count == 0)
            {
                failureReason = "No layout settings were present.";
                return false;
            }

            var knownSettings = layoutSettings
                .Where(setting => setting != null && !string.IsNullOrWhiteSpace(setting.ColumnKey))
                .Where(setting => _boardColumnsByKey.ContainsKey(setting.ColumnKey))
                .GroupBy(setting => setting.ColumnKey, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            if (knownSettings.Count == 0)
            {
                failureReason = "No known board column keys were present.";
                return false;
            }

            var usedDisplayIndexes = new HashSet<int>();
            foreach (var setting in knownSettings)
            {
                if (!string.Equals(setting.WidthUnitType, DataGridLengthUnitType.Pixel.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    failureReason = $"Unsupported width unit '{setting.WidthUnitType}' for column '{setting.ColumnKey}'.";
                    return false;
                }

                if (setting.DisplayIndex < 0 || setting.DisplayIndex >= CanonicalBoardColumnOrder.Length)
                {
                    failureReason = $"DisplayIndex out of range for column '{setting.ColumnKey}'.";
                    return false;
                }

                if (!usedDisplayIndexes.Add(setting.DisplayIndex))
                {
                    failureReason = $"Duplicate DisplayIndex '{setting.DisplayIndex}' detected.";
                    return false;
                }

                if (double.IsNaN(setting.WidthValue) || double.IsInfinity(setting.WidthValue))
                {
                    failureReason = $"Non-finite width detected for column '{setting.ColumnKey}'.";
                    return false;
                }

                if (setting.WidthValue < GetBoardColumnMinimumWidth(setting.ColumnKey))
                {
                    failureReason = $"Width below minimum for column '{setting.ColumnKey}'.";
                    return false;
                }
            }

            var orderedKeys = knownSettings
                .OrderBy(setting => setting.DisplayIndex)
                .ThenBy(setting => setting.ColumnKey, StringComparer.OrdinalIgnoreCase)
                .Select(setting => setting.ColumnKey)
                .ToList();

            foreach (var key in CanonicalBoardColumnOrder)
            {
                if (!orderedKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
                {
                    orderedKeys.Add(key);
                }
            }

            for (var index = 0; index < orderedKeys.Count; index++)
            {
                var key = orderedKeys[index];
                var existing = knownSettings.FirstOrDefault(setting => string.Equals(setting.ColumnKey, key, StringComparison.OrdinalIgnoreCase));
                validLayout.Add(existing != null
                    ? new BoardColumnLayoutSetting
                    {
                        ColumnKey = existing.ColumnKey,
                        DisplayIndex = index,
                        WidthValue = existing.WidthValue,
                        WidthUnitType = existing.WidthUnitType
                    }
                    : CreateCanonicalBoardColumnLayoutSetting(key, index));
            }

            var totalWidth = validLayout.Sum(setting => Math.Max(GetBoardColumnMinimumWidth(setting.ColumnKey), setting.WidthValue));
            if (totalWidth < CanonicalBoardColumnMinimumWidths.Values.Sum() * 0.8d)
            {
                failureReason = $"Total saved width was too small. totalWidth={totalWidth:0.##}";
                return false;
            }

            return true;
        }

        private static double GetBoardColumnMinimumWidth(string key)
        {
            return CanonicalBoardColumnMinimumWidths.TryGetValue(key, out var width)
                ? width
                : 50;
        }

        private static double GetBoardColumnDefaultWidth(string key)
        {
            return CanonicalBoardColumnDefaultWidths.TryGetValue(key, out var width)
                ? width
                : Math.Max(GetBoardColumnMinimumWidth(key), 80);
        }

        private void ScheduleFitVisibleBoardColumnsToViewport(bool force = false)
        {
            if (PilotBoard == null)
            {
                return;
            }

            if (_isBoardColumnAutoFitPending && !force)
            {
                return;
            }

            _isBoardColumnAutoFitPending = true;
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    _isBoardColumnAutoFitPending = false;
                    FitVisibleBoardColumnsToViewport();
                }),
                DispatcherPriority.ContextIdle);
        }

        private double GetPilotBoardViewportWidth()
        {
            if (PilotBoard == null)
            {
                return 0d;
            }

            try
            {
                var scrollViewer = FindVisualDescendant<ScrollViewer>(PilotBoard);
                if (scrollViewer != null && scrollViewer.ViewportWidth > 0d)
                {
                    // Subtract a single device-independent pixel as a safety margin so WPF
                    // does not decide a horizontal scrollbar is required from rounding.
                    return Math.Max(0d, scrollViewer.ViewportWidth - 1d);
                }
            }
            catch (InvalidOperationException)
            {
                // Visual tree may not be ready during early layout passes. Fall back below.
            }

            return Math.Max(0d, PilotBoard.ActualWidth - 1d);
        }

        private void FitVisibleBoardColumnsToViewport()
        {
            if (PilotBoard == null || _boardColumnsByKey.Count == 0 || PilotBoard.ActualWidth <= 0)
            {
                return;
            }

            PilotBoard.UpdateLayout();

            var visibleColumns = _boardColumnsByKey.Values
                .Where(column => column.Visibility == Visibility.Visible)
                .OrderBy(column => column.DisplayIndex)
                .ToList();

            if (visibleColumns.Count == 0)
            {
                return;
            }

            var availableWidth = GetPilotBoardViewportWidth();
            if (double.IsNaN(availableWidth) || double.IsInfinity(availableWidth) || availableWidth <= 40d)
            {
                return;
            }

            var columnPlans = visibleColumns
                .Select(column =>
                {
                    var key = GetBoardColumnKey(column);
                    var minimum = Math.Max(12d, GetBoardColumnMinimumWidth(key));
                    var current = Math.Max(minimum, GetEffectiveBoardColumnWidth(column));
                    return new BoardColumnFitPlan(column, minimum, current);
                })
                .ToList();

            var minimumTotal = columnPlans.Sum(plan => plan.MinimumWidth);
            var preferredTotal = columnPlans.Sum(plan => plan.CurrentWidth);

            if (minimumTotal <= 0d || preferredTotal <= 0d)
            {
                return;
            }

            var wasApplyingLayout = _isApplyingBoardColumnLayout;
            _isApplyingBoardColumnLayout = true;

            try
            {
                if (minimumTotal >= availableWidth)
                {
                    var scale = Math.Max(0.6d, availableWidth / minimumTotal);
                    foreach (var plan in columnPlans)
                    {
                        SetBoardColumnPixelWidth(plan.Column, Math.Max(18d, plan.MinimumWidth * scale));
                    }

                    return;
                }

                if (preferredTotal > availableWidth)
                {
                    var shortage = preferredTotal - availableWidth;
                    var shrinkCapacity = columnPlans.Sum(plan => Math.Max(0d, plan.CurrentWidth - plan.MinimumWidth));

                    foreach (var plan in columnPlans)
                    {
                        var targetWidth = plan.CurrentWidth;
                        if (shrinkCapacity > 0d)
                        {
                            var share = Math.Max(0d, plan.CurrentWidth - plan.MinimumWidth) / shrinkCapacity;
                            targetWidth = Math.Max(plan.MinimumWidth, plan.CurrentWidth - shortage * share);
                        }

                        SetBoardColumnPixelWidth(plan.Column, targetWidth);
                    }

                    return;
                }

                var extra = availableWidth - preferredTotal;
                var expandableTotal = columnPlans.Sum(plan => Math.Max(plan.MinimumWidth, plan.CurrentWidth));
                foreach (var plan in columnPlans)
                {
                    var share = expandableTotal > 0d
                        ? Math.Max(plan.MinimumWidth, plan.CurrentWidth) / expandableTotal
                        : 1d / columnPlans.Count;
                    SetBoardColumnPixelWidth(plan.Column, plan.CurrentWidth + extra * share);
                }
            }
            finally
            {
                _isApplyingBoardColumnLayout = wasApplyingLayout;
            }
        }

        private static void SetBoardColumnPixelWidth(DataGridColumn column, double width)
        {
            if (column == null || double.IsNaN(width) || double.IsInfinity(width) || width <= 0d)
            {
                return;
            }

            var roundedWidth = Math.Round(width, 1);
            if (Math.Abs(GetEffectiveBoardColumnWidth(column) - roundedWidth) < 0.5d &&
                column.Width.UnitType == DataGridLengthUnitType.Pixel)
            {
                return;
            }

            column.Width = new DataGridLength(roundedWidth, DataGridLengthUnitType.Pixel);
        }

        private static double GetEffectiveBoardColumnWidth(DataGridColumn column)
        {
            if (column == null)
            {
                return 0d;
            }

            var width = column.ActualWidth;
            if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
            {
                width = column.Width.DisplayValue;
            }

            if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
            {
                width = column.MinWidth;
            }

            return double.IsNaN(width) || double.IsInfinity(width) || width <= 0
                ? 0d
                : width;
        }

        private sealed record BoardColumnFitPlan(DataGridColumn Column, double MinimumWidth, double CurrentWidth);

        private string GetBoardColumnKey(DataGridColumn column)
        {
            foreach (var pair in _boardColumnsByKey)
            {
                if (ReferenceEquals(pair.Value, column))
                {
                    return pair.Key;
                }
            }

            return string.Empty;
        }

        private void SetAllOptionalBoardColumnSettings(bool isVisible)
        {
            _appSettings.ShowSigColumn = isVisible;
            _appSettings.ShowAllianceColumn = isVisible;
            _appSettings.ShowCorpColumn = isVisible;
            _appSettings.ShowKillsColumn = isVisible;
            _appSettings.ShowLossesColumn = isVisible;
            _appSettings.ShowAvgFleetSizeColumn = isVisible;
            _appSettings.ShowLastShipSeenColumn = isVisible;
            _appSettings.ShowLastSeenColumn = isVisible;
            _appSettings.ShowCynoHullSeenColumn = isVisible;
        }

        private static void SetColumnVisibility(DataGridColumn column, bool? isVisible)
        {
            column.Visibility = IsEnabled(isVisible) ? Visibility.Visible : Visibility.Collapsed;
        }

        private static bool IsEnabled(CheckBox checkBox)
        {
            return checkBox.IsChecked == true;
        }

        private static bool IsEnabled(bool? value)
        {
            return value != false;
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
            _intelUpdateBannerController.HandleStatusChanged(
                snapshot,
                IntelUpdateBanner,
                IntelUpdateStatusText,
                IntelUpdateDetailText);

            if (Dispatcher.CheckAccess())
            {
                ApplyIntelStatusDetails(snapshot);
            }
            else
            {
                Dispatcher.Invoke(() => ApplyIntelStatusDetails(snapshot));
            }
        }

        private void ApplyIntelUpdateSnapshot(IntelUpdateStatusSnapshot snapshot)
        {
            _intelUpdateBannerController.ApplySnapshot(
                snapshot,
                IntelUpdateBanner,
                IntelUpdateStatusText,
                IntelUpdateDetailText);

            ApplyIntelStatusDetails(snapshot);
        }

        private void ApplyIntelStatusDetails(IntelUpdateStatusSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            if (IntelLastUpdatedText != null)
            {
                IntelLastUpdatedText.Text = FormatIntelTimestamp(snapshot.LastSuccessfulUpdateAtUtc, "No successful local intel update recorded yet.");
            }

            if (IntelOldestKillmailDayText != null)
            {
                IntelOldestKillmailDayText.Text = FormatIntelDay(snapshot.EarliestCompleteDayUtc, "No local killmail days recorded yet.");
            }

            if (IntelNewestKillmailDayText != null)
            {
                IntelNewestKillmailDayText.Text = FormatIntelDay(snapshot.LatestCompleteDayUtc, "No local killmail days recorded yet.");
            }

            if (IntelCurrentUpdateStatusText != null)
            {
                IntelCurrentUpdateStatusText.Text = BuildIntelCurrentUpdateStatusText(snapshot);
            }

            if (IntelTotalProgressBar != null)
            {
                IntelTotalProgressBar.IsIndeterminate = snapshot.TotalProgressIsIndeterminate;
                IntelTotalProgressBar.Value = snapshot.TotalProgressIsIndeterminate
                    ? 0
                    : Math.Max(0, Math.Min(100, snapshot.TotalProgressPercent));
            }

            if (IntelTotalProgressText != null)
            {
                IntelTotalProgressText.Text = string.IsNullOrWhiteSpace(snapshot.TotalProgressText)
                    ? "No update currently running."
                    : snapshot.TotalProgressText;
            }

            if (IntelCurrentDayProgressBar != null)
            {
                IntelCurrentDayProgressBar.IsIndeterminate = snapshot.CurrentDayProgressIsIndeterminate;
                IntelCurrentDayProgressBar.Value = snapshot.CurrentDayProgressIsIndeterminate
                    ? 0
                    : Math.Max(0, Math.Min(100, snapshot.CurrentDayProgressPercent));
            }

            if (IntelCurrentDayProgressText != null)
            {
                IntelCurrentDayProgressText.Text = string.IsNullOrWhiteSpace(snapshot.CurrentDayProgressText)
                    ? "No update currently running."
                    : snapshot.CurrentDayProgressText;
            }

            var liveFeed = snapshot.LiveFeed ?? new R2Z2LiveFeedSnapshot();

            if (IntelLiveFeedSourceText != null)
            {
                IntelLiveFeedSourceText.Text = string.IsNullOrWhiteSpace(liveFeed.Source)
                    ? "R2Z2"
                    : liveFeed.Source;
            }

            if (IntelLiveFeedStatusText != null)
            {
                var statusText = string.IsNullOrWhiteSpace(liveFeed.Status)
                    ? "Disabled"
                    : liveFeed.Status;

                var nextRetryText = FormatIntelTimestamp(liveFeed.NextRetryAtUtc, "");
                if (!string.IsNullOrWhiteSpace(nextRetryText) &&
                    (statusText.Contains("wait", StringComparison.OrdinalIgnoreCase) ||
                     statusText.Contains("backing off", StringComparison.OrdinalIgnoreCase)))
                {
                    statusText = $"{statusText} (retry {nextRetryText})";
                }

                IntelLiveFeedStatusText.Text = statusText;
            }

            if (IntelLiveFeedEnabledText != null)
            {
                IntelLiveFeedEnabledText.Text = liveFeed.Enabled ? "Yes" : "No";
            }

            if (IntelLiveFeedRecentImportsText != null)
            {
                IntelLiveFeedRecentImportsText.Text = liveFeed.RecentLiveImportsCount.ToString(CultureInfo.InvariantCulture);
            }

            if (IntelLiveFeedNextSequenceText != null)
            {
                IntelLiveFeedNextSequenceText.Text = liveFeed.NextSequenceId.HasValue
                    ? liveFeed.NextSequenceId.Value.ToString(CultureInfo.InvariantCulture)
                    : "Not initialized";
            }

            if (IntelLiveFeedLastProcessedSequenceText != null)
            {
                IntelLiveFeedLastProcessedSequenceText.Text = liveFeed.LastProcessedSequenceId.HasValue
                    ? liveFeed.LastProcessedSequenceId.Value.ToString(CultureInfo.InvariantCulture)
                    : "None";
            }

            if (IntelLiveFeedLastSuccessText != null)
            {
                IntelLiveFeedLastSuccessText.Text = FormatIntelTimestamp(
                    liveFeed.LastSuccessAtUtc,
                    "No live imports recorded yet.");
            }

            if (IntelLiveFeedLastCaughtUpText != null)
            {
                IntelLiveFeedLastCaughtUpText.Text = FormatIntelTimestamp(
                    liveFeed.LastCaughtUpAtUtc,
                    "No caught-up wait recorded yet.");
            }

            if (IntelLiveFeedLastErrorText != null)
            {
                var lastErrorTime = FormatIntelTimestamp(liveFeed.LastErrorAtUtc, "");
                IntelLiveFeedLastErrorText.Text = string.IsNullOrWhiteSpace(liveFeed.LastError)
                    ? "No live-feed errors recorded."
                    : string.IsNullOrWhiteSpace(lastErrorTime)
                        ? liveFeed.LastError
                        : $"{lastErrorTime} - {liveFeed.LastError}";
            }

            var todaysFreshness = snapshot.TodaysFreshness ?? new TodaysFreshnessSnapshot();
            if (TodaysFreshnessStatusText != null)
            {
                var statusText = string.IsNullOrWhiteSpace(todaysFreshness.Status)
                    ? "Idle"
                    : todaysFreshness.Status;

                var nextRetryText = FormatIntelTimestamp(todaysFreshness.NextRetryAtUtc, "");
                if (!string.IsNullOrWhiteSpace(nextRetryText) &&
                    statusText.Contains("rate limited", StringComparison.OrdinalIgnoreCase))
                {
                    statusText = $"{statusText} (retry {nextRetryText})";
                }

                TodaysFreshnessStatusText.Text = statusText;
            }

            if (TodaysFreshnessVisiblePilotsText != null)
            {
                TodaysFreshnessVisiblePilotsText.Text = todaysFreshness.VisiblePilotsTargeted.ToString(CultureInfo.InvariantCulture);
            }

            if (TodaysFreshnessEntitiesQueriedText != null)
            {
                TodaysFreshnessEntitiesQueriedText.Text = todaysFreshness.EntitiesQueried.ToString(CultureInfo.InvariantCulture);
            }

            if (TodaysFreshnessResultsFoundText != null)
            {
                TodaysFreshnessResultsFoundText.Text = todaysFreshness.ZkillResultsFound.ToString(CultureInfo.InvariantCulture);
            }

            if (TodaysFreshnessKnownSkippedText != null)
            {
                TodaysFreshnessKnownSkippedText.Text = todaysFreshness.AlreadyKnownCount.ToString(CultureInfo.InvariantCulture);
            }

            if (TodaysFreshnessImportedText != null)
            {
                TodaysFreshnessImportedText.Text = todaysFreshness.NewKillmailsImported.ToString(CultureInfo.InvariantCulture);
            }

            if (TodaysFreshnessFailedText != null)
            {
                TodaysFreshnessFailedText.Text = todaysFreshness.FailedCount.ToString(CultureInfo.InvariantCulture);
            }

            if (TodaysFreshnessLastRunText != null)
            {
                TodaysFreshnessLastRunText.Text = FormatIntelTimestamp(
                    todaysFreshness.LastRunAtUtc,
                    "No Today's Freshness run recorded yet.");
            }

            if (TodaysFreshnessDetailText != null)
            {
                TodaysFreshnessDetailText.Text = string.IsNullOrWhiteSpace(todaysFreshness.DetailText)
                    ? "Today's Freshness is idle."
                    : todaysFreshness.DetailText;
            }

            if (TodaysFreshnessLastErrorText != null)
            {
                TodaysFreshnessLastErrorText.Text = string.IsNullOrWhiteSpace(todaysFreshness.LastError)
                    ? "No Today's Freshness errors recorded."
                    : todaysFreshness.LastError;
            }

            var historicalFreshness = snapshot.HistoricalFreshness ?? new HistoricalFreshnessSnapshot();

            if (RunTodaysFreshnessButton != null)
            {
                var isRunning = string.Equals(todaysFreshness.Status, "Running", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(todaysFreshness.Status, "Backing off / rate limited", StringComparison.OrdinalIgnoreCase);
                var historicalIsRunning = string.Equals(historicalFreshness.Status, "Running", StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(historicalFreshness.Status, "Backing off / rate limited", StringComparison.OrdinalIgnoreCase);
                RunTodaysFreshnessButton.IsEnabled = !isRunning && !historicalIsRunning && !_isShuttingDown;
                RunTodaysFreshnessButton.Content = isRunning
                    ? "Today's Freshness Running..."
                    : historicalIsRunning
                        ? "Historical Freshness Running..."
                    : "Refresh Today's zKill Intel";
            }

            if (HistoricalFreshnessStatusText != null)
            {
                var statusText = string.IsNullOrWhiteSpace(historicalFreshness.Status)
                    ? "Idle"
                    : historicalFreshness.Status;

                var nextRetryText = FormatIntelTimestamp(historicalFreshness.NextRetryAtUtc, "");
                if (!string.IsNullOrWhiteSpace(nextRetryText) &&
                    statusText.Contains("rate limited", StringComparison.OrdinalIgnoreCase))
                {
                    statusText = $"{statusText} (retry {nextRetryText})";
                }

                HistoricalFreshnessStatusText.Text = statusText;
            }

            if (HistoricalFreshnessModeText != null)
            {
                HistoricalFreshnessModeText.Text = string.IsNullOrWhiteSpace(historicalFreshness.Mode)
                    ? "Not run yet"
                    : historicalFreshness.Mode;
            }

            if (HistoricalFreshnessVisiblePilotsText != null)
            {
                HistoricalFreshnessVisiblePilotsText.Text = historicalFreshness.VisiblePilotsTargeted.ToString(CultureInfo.InvariantCulture);
            }

            if (HistoricalFreshnessCandidatesConsideredText != null)
            {
                HistoricalFreshnessCandidatesConsideredText.Text = historicalFreshness.CandidatePilotsConsidered.ToString(CultureInfo.InvariantCulture);
            }

            if (HistoricalFreshnessCandidatesSkippedCooldownText != null)
            {
                HistoricalFreshnessCandidatesSkippedCooldownText.Text = historicalFreshness.CandidatePilotsSkippedCooldown.ToString(CultureInfo.InvariantCulture);
            }

            if (HistoricalFreshnessPilotsCheckedText != null)
            {
                HistoricalFreshnessPilotsCheckedText.Text = historicalFreshness.PilotsChecked.ToString(CultureInfo.InvariantCulture);
            }

            if (HistoricalFreshnessDaysCheckedText != null)
            {
                HistoricalFreshnessDaysCheckedText.Text = historicalFreshness.HistoricalDaysChecked.ToString(CultureInfo.InvariantCulture);
            }

            if (HistoricalFreshnessEntitiesQueriedText != null)
            {
                HistoricalFreshnessEntitiesQueriedText.Text = historicalFreshness.EntitiesQueried.ToString(CultureInfo.InvariantCulture);
            }

            if (HistoricalFreshnessResultsFoundText != null)
            {
                HistoricalFreshnessResultsFoundText.Text = historicalFreshness.ZkillResultsFound.ToString(CultureInfo.InvariantCulture);
            }

            if (HistoricalFreshnessKnownSkippedText != null)
            {
                HistoricalFreshnessKnownSkippedText.Text = historicalFreshness.AlreadyKnownCount.ToString(CultureInfo.InvariantCulture);
            }

            if (HistoricalFreshnessImportedText != null)
            {
                HistoricalFreshnessImportedText.Text = historicalFreshness.MissingImportedCount.ToString(CultureInfo.InvariantCulture);
            }

            if (HistoricalFreshnessFailedText != null)
            {
                HistoricalFreshnessFailedText.Text = historicalFreshness.FailedCount.ToString(CultureInfo.InvariantCulture);
            }

            if (HistoricalFreshnessLastRunText != null)
            {
                HistoricalFreshnessLastRunText.Text = FormatIntelTimestamp(
                    historicalFreshness.LastRunAtUtc,
                    "No Historical Freshness run recorded yet.");
            }

            if (HistoricalFreshnessDetailText != null)
            {
                HistoricalFreshnessDetailText.Text = string.IsNullOrWhiteSpace(historicalFreshness.DetailText)
                    ? "Historical Freshness is idle."
                    : historicalFreshness.DetailText;
            }

            if (HistoricalFreshnessLastErrorText != null)
            {
                HistoricalFreshnessLastErrorText.Text = string.IsNullOrWhiteSpace(historicalFreshness.LastError)
                    ? "No Historical Freshness errors recorded."
                    : historicalFreshness.LastError;
            }

            if (RunHistoricalFreshnessButton != null)
            {
                var isRunning = string.Equals(historicalFreshness.Status, "Running", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(historicalFreshness.Status, "Backing off / rate limited", StringComparison.OrdinalIgnoreCase);
                var todaysIsRunning = string.Equals(todaysFreshness.Status, "Running", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(todaysFreshness.Status, "Backing off / rate limited", StringComparison.OrdinalIgnoreCase);
                RunHistoricalFreshnessButton.IsEnabled = !isRunning && !todaysIsRunning && !_isShuttingDown;
                RunHistoricalFreshnessButton.Content = isRunning
                    ? "Historical Freshness Running..."
                    : todaysIsRunning
                        ? "Today's Freshness Running..."
                    : "Repair Recent Historical Intel";
            }
        }

        private static string BuildIntelCurrentUpdateStatusText(IntelUpdateStatusSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "No update currently running.";
            }

            if (snapshot.HasError)
            {
                return string.IsNullOrWhiteSpace(snapshot.ErrorText)
                    ? "Killmail intel update failed."
                    : $"Killmail intel update failed: {snapshot.ErrorText}";
            }

            if (snapshot.IsRunning)
            {
                if (snapshot.IsForegroundPriorityActive)
                {
                    return "Updating killmail intel is paused for foreground activity.";
                }

                return string.IsNullOrWhiteSpace(snapshot.CurrentImportDayUtc)
                    ? "Updating killmail intel…"
                    : $"Updating killmail intel… Current day {snapshot.CurrentImportDayUtc}.";
            }

            if (snapshot.HasRequestedCoverageWindow && snapshot.RequestedCoverageDays > 0)
            {
                if (!snapshot.IsRequestedCoverageComplete)
                {
                    return $"Killmail intel partially populated. Local coverage is {snapshot.LocalCoverageDays} of {snapshot.RequestedCoverageDays} requested days.";
                }
            }

            if (snapshot.IsCurrentThroughYesterday)
            {
                return "Killmail intel is current.";
            }

            if (snapshot.MissingDayCount > 0)
            {
                return $"Killmail intel is waiting to catch up {snapshot.MissingDayCount} day(s).";
            }

            return "No update currently running.";
        }

        private static string FormatIntelTimestamp(string value, string emptyText)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return emptyText;
            }

            return DateTime.TryParse(value, out var parsed)
                ? parsed.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                : value;
        }

        private static string FormatIntelDay(string value, string emptyText)
        {
            return string.IsNullOrWhiteSpace(value) ? emptyText : value;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmClipboardUpdate)
            {
                ScheduleClipboardProcessing();
            }
            else if (msg == WmHotKey && wParam.ToInt32() == GlobalResetWindowHotKeyId)
            {
                handled = true;

                if (!IsActive)
                {
                    RequestWindowLayoutResetFromHotkey("global Ctrl+Home hotkey");
                }
            }

            return IntPtr.Zero;
        }

        private void TryRegisterGlobalResetWindowHotKey(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            _globalResetWindowHotKeyRegistered = RegisterHotKey(
                hwnd,
                GlobalResetWindowHotKeyId,
                ModControl,
                (uint)KeyInterop.VirtualKeyFromKey(Key.Home));

            if (_globalResetWindowHotKeyRegistered)
            {
                AppLogger.UiInfo("Global Ctrl+Home reset-window hotkey registered.");
                return;
            }

            AppLogger.UiWarn($"Global Ctrl+Home hotkey registration failed. win32Error={Marshal.GetLastWin32Error()}");
        }

        private void TryUnregisterGlobalResetWindowHotKey(IntPtr hwnd)
        {
            if (!_globalResetWindowHotKeyRegistered || hwnd == IntPtr.Zero)
            {
                return;
            }

            if (!UnregisterHotKey(hwnd, GlobalResetWindowHotKeyId))
            {
                AppLogger.UiWarn($"Global Ctrl+Home hotkey unregistration failed. win32Error={Marshal.GetLastWin32Error()}");
            }

            _globalResetWindowHotKeyRegistered = false;
        }

        private void ScheduleClipboardProcessing()
        {
            _clipboardDebounceTimer.Stop();
            _clipboardDebounceTimer.Start();
            _diagnostics.ClipboardChangeDebounced(ClipboardDebounceMilliseconds);
        }

        private void ClipboardDebounceTimer_Tick(object? sender, EventArgs e)
        {
            _clipboardDebounceTimer.Stop();
            _diagnostics.ClipboardDebounceElapsed();
            _ = ProcessClipboardIfValidAsync();
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
                updateClipboardStatus: UpdateBoardPopulationStatus,
                processNamesAsync: ProcessNamesAsync);
        }

        private Task ProcessNamesAsync(List<string> characterNames, bool isRetryPass)
        {
            TriggerSessionContextRefresh(
                isRetryPass ? "board retry pass" : "accepted local clipboard",
                force: !isRetryPass);

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

            if (_activePilotDetailWindow != null &&
                string.Equals(_activePilotDetailWindow.CharacterName, row.CharacterName, StringComparison.OrdinalIgnoreCase))
            {
                _activePilotDetailWindow.RefreshRow();
            }
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
            if (PilotBoard == null)
            {
                return;
            }

            e.Handled = true;

            var sortMemberPath = e.Column.SortMemberPath;
            if (string.IsNullOrWhiteSpace(sortMemberPath))
            {
                sortMemberPath = GetSortMemberPathFromColumn(e.Column);
                if (string.IsNullOrWhiteSpace(sortMemberPath))
                {
                    return;
                }
            }

            var nextDirection = _activeBoardSortMemberPath == sortMemberPath &&
                                _activeBoardSortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            _activeBoardSortMemberPath = sortMemberPath;
            _activeBoardSortDirection = nextDirection;

            ApplySortIndicatorState(e.Column, nextDirection);
            ApplyCurrentBoardOrdering();

            AppLogger.UiInfo($"Board sort changed. member='{sortMemberPath}' direction={nextDirection}");
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
            var clearedRowCount = _currentRows.Count;

            _diagnostics.ClearBoardStart(clearedRowCount);

            SaveCurrentNotesAndTags();
            CancelBoardPopulationRetry();
            _processingGeneration++;
            ResetEntryAndRetryTracking();
            ResetManualBoardSort();
            UnsubscribeFromAllBoardRows();

            PilotBoard.SelectedItem = null;
            _currentRows.Clear();
            RecomputeCorpAllianceCounts();
            CloseActiveDetailWindow();
            UpdateOpenDetailsButtonState();

            UpdateLastRefreshed();
            UpdateBoardPopulationStatus("Board cleared", BoardPopulationStatusKind.Neutral);

            AppLogger.UiInfo($"Board cleared. reason='{reason}' removedRows={clearedRowCount}");
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
                SetDiagnosticsStatus("Opening logs folder.");
                _browserLauncher.OpenPath(logsRootPath);
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Open logs failed.", ex);
                SetDiagnosticsStatus("Failed to open logs folder.");

                MessageBox.Show(
                    $"Failed to open logs folder.\n\n{ex.Message}",
                    "PMG Logs Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void PackageDiagnosticsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var bundlePath = DiagnosticBundleService.TryCreateBundle("manual-diagnostics-package");

                if (string.IsNullOrWhiteSpace(bundlePath))
                {
                    SetDiagnosticsStatus("Diagnostic package failed.");
                    MessageBox.Show(
                        "PMG could not create a diagnostic package. Check the active logs for details.",
                        "PMG Diagnostics",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var bundleFileName = Path.GetFileName(bundlePath);
                SetDiagnosticsStatus($"Created diagnostic package: {bundleFileName}");
                AppLogger.UiInfo($"Manual diagnostic package created. path={bundlePath}");

                var diagnosticsDirectory = Path.GetDirectoryName(bundlePath);
                if (!string.IsNullOrWhiteSpace(diagnosticsDirectory))
                {
                    _browserLauncher.OpenPath(diagnosticsDirectory);
                }
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Manual diagnostic package failed.", ex);
                SetDiagnosticsStatus("Diagnostic package failed.");

                MessageBox.Show(
                    $"Failed to create diagnostic package.\n\n{ex.Message}",
                    "PMG Diagnostics Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OpenDiagnosticsFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var diagnosticsDirectory = DiagnosticBundleService.GetDiagnosticsDirectory();

                AppLogger.UiInfo($"Open diagnostics folder requested. path={diagnosticsDirectory}");
                SetDiagnosticsStatus("Opening diagnostics folder.");
                _browserLauncher.OpenPath(diagnosticsDirectory);
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Open diagnostics folder failed.", ex);
                SetDiagnosticsStatus("Failed to open diagnostics folder.");

                MessageBox.Show(
                    $"Failed to open diagnostics folder.\n\n{ex.Message}",
                    "PMG Diagnostics Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SetDiagnosticsStatus(string message)
        {
            if (DiagnosticsStatusText == null)
            {
                return;
            }

            DiagnosticsStatusText.Text = string.IsNullOrWhiteSpace(message)
                ? "Diagnostics ready."
                : message.Trim();
        }

        private void RefreshProviderHealthButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshProviderHealthUi();
            SetDiagnosticsStatus("Provider health refreshed.");
        }

        private void RefreshProviderHealthUi()
        {
            if (ProviderHealthGrid == null)
            {
                return;
            }

            _providerHealthRows.Clear();
            foreach (var snapshot in DiagnosticTelemetry.GetProviderHealthSnapshots())
            {
                _providerHealthRows.Add(snapshot);
            }
        }

        private void RefreshCacheStatsButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshCacheStatsUi();
            SetDiagnosticsStatus("Cache stats refreshed.");
        }

        private void ClearExpiredCacheButton_Click(object sender, RoutedEventArgs e)
        {
            RunCacheMaintenanceAction(
                "Clear expired cache",
                requiresConfirmation: true,
                action: () =>
                {
                    var removed = _cacheMaintenanceService.ClearExpired();
                    SetDiagnosticsStatus($"Expired cache cleanup removed {removed:N0} rows.");
                    AppLogger.DatabaseInfo($"Cache maintenance UI cleared expired rows. removedRows={removed}");
                });
        }

        private void VacuumCacheButton_Click(object sender, RoutedEventArgs e)
        {
            RunCacheMaintenanceAction(
                "Compact cache database",
                requiresConfirmation: true,
                action: () =>
                {
                    _cacheMaintenanceService.Vacuum();
                    SetDiagnosticsStatus("Cache database compacted.");
                    AppLogger.DatabaseInfo("Cache maintenance UI compacted SQLite database.");
                });
        }

        private void ClearAllCacheButton_Click(object sender, RoutedEventArgs e)
        {
            RunCacheMaintenanceAction(
                "Clear all resolver/stat cache rows",
                requiresConfirmation: true,
                action: () =>
                {
                    var removed = _cacheMaintenanceService.ClearAll();
                    SetDiagnosticsStatus($"All resolver/stat cache cleanup removed {removed:N0} rows.");
                    AppLogger.DatabaseWarn($"Cache maintenance UI cleared all resolver/stat cache rows. removedRows={removed}");
                });
        }

        private async void RebuildKillmailDerivedIntelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_boardPopulationEntryController.IsClipboardProcessing)
            {
                SetDiagnosticsStatus("Derived intel rebuild blocked while a lookup is active.");
                MessageBox.Show(
                    "A board lookup is currently running. Let it finish before rebuilding derived killmail intel.",
                    "PMG Killmail Derived Intel",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                "Rebuild killmail derived intel from local extracted killmail archives?\n\nThis only rebuilds derived confirmed cyno-module and industrial-cyno bait observations. It does not clear notes, settings, themes, ignore lists, manual overrides, or unrelated cache data.",
                "PMG Killmail Derived Intel",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (confirm != MessageBoxResult.Yes)
            {
                SetDiagnosticsStatus("Derived intel rebuild cancelled.");
                return;
            }

            try
            {
                RebuildKillmailDerivedIntelButton.IsEnabled = false;
                SetDiagnosticsStatus("Rebuilding killmail derived intel...");
                var result = await _killmailDerivedIntelRebuildService.RebuildConfirmedCynoModuleObservationsAsync(_windowShutdownCts.Token);
                RefreshCacheStatsUi();
                RefreshConfirmedCynoModuleStateForCurrentRows();

                SetDiagnosticsStatus(result.Message);
                MessageBox.Show(
                    result.Message,
                    result.NoLocalSourceAvailable ? "PMG Killmail Derived Intel Source Missing" : "PMG Killmail Derived Intel",
                    MessageBoxButton.OK,
                    result.NoLocalSourceAvailable ? MessageBoxImage.Information : MessageBoxImage.None);
            }
            catch (OperationCanceledException)
            {
                SetDiagnosticsStatus("Derived intel rebuild cancelled.");
            }
            catch (Exception ex)
            {
                AppLogger.DatabaseError("Killmail derived intel rebuild failed.", ex);
                SetDiagnosticsStatus("Derived intel rebuild failed.");
                MessageBox.Show(
                    $"Failed to rebuild killmail derived intel.\n\n{ex.Message}",
                    "PMG Killmail Derived Intel Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                RebuildKillmailDerivedIntelButton.IsEnabled = true;
            }
        }

        private void RunCacheMaintenanceAction(string title, bool requiresConfirmation, Action action)
        {
            if (_boardPopulationEntryController.IsClipboardProcessing)
            {
                SetDiagnosticsStatus("Cache maintenance blocked while a lookup is active.");
                MessageBox.Show(
                    "A board lookup is currently running. Let it finish before changing the local cache.",
                    "PMG Cache Maintenance",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (requiresConfirmation)
            {
                var result = MessageBox.Show(
                    $"{title}?\n\nThis only affects PMG local cache tables and does not delete unrelated files.",
                    "PMG Cache Maintenance",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    SetDiagnosticsStatus("Cache maintenance cancelled.");
                    return;
                }
            }

            try
            {
                action();
                RefreshCacheStatsUi();
            }
            catch (Exception ex)
            {
                AppLogger.DatabaseError($"Cache maintenance failed. action='{title}'", ex);
                SetDiagnosticsStatus("Cache maintenance failed.");
                MessageBox.Show(
                    $"Cache maintenance failed.\n\n{ex.Message}",
                    "PMG Cache Maintenance Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RefreshCacheStatsUi()
        {
            if (CacheStatsText == null)
            {
                return;
            }

            try
            {
                CacheStatsText.Text = CacheMaintenanceService.FormatStats(_cacheMaintenanceService.GetStats());
            }
            catch (Exception ex)
            {
                AppLogger.DatabaseError("Cache stats refresh failed.", ex);
                CacheStatsText.Text = $"Cache stats failed: {ex.Message}";
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

        private async void EnableLiveZkillFeedCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isApplyingSettings)
            {
                return;
            }

            var enabled = EnableLiveZkillFeedCheckBox.IsChecked == true;
            _appSettings.LiveZkillFeedEnabled = enabled;
            _mainWindowAppearanceController.SaveSettings(_appSettings);
            AppLogger.UiInfo($"Live zKill feed setting changed. enabled={enabled}");

            try
            {
                await _backgroundIntelUpdateService.SetLiveFeedEnabledAsync(enabled, _windowShutdownCts.Token);
            }
            catch (OperationCanceledException) when (_isShuttingDown || _windowShutdownCts.IsCancellationRequested)
            {
                AppLogger.UiInfo("Live zKill feed toggle cancelled during shutdown.");
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Live zKill feed toggle failed.", ex);

                MessageBox.Show(
                    $"Failed to update the live zKill feed setting.\n\n{ex.Message}",
                    "PMG Live Feed Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BackgroundHistoricalRepairEnabledCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isApplyingSettings)
            {
                return;
            }

            var enabled = BackgroundHistoricalRepairEnabledCheckBox.IsChecked == true;
            _appSettings.BackgroundHistoricalRepairEnabled = enabled;
            _mainWindowAppearanceController.SaveSettings(_appSettings);
            AppLogger.UiInfo($"Background historical repair setting changed. enabled={enabled}");
        }

        private async void RunTodaysFreshnessButton_Click(object sender, RoutedEventArgs e)
        {
            var visibleCharacterIds = GetVisibleCharacterIdsForTodaysFreshness();
            if (visibleCharacterIds.Count == 0)
            {
                MessageBox.Show(
                    "Today's Freshness needs at least one visible Grill pilot with a resolved character ID.",
                    "PMG Today's Freshness",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                using var foregroundPriority = _backgroundIntelUpdateService.BeginForegroundPriority();
                var result = await _backgroundIntelUpdateService.RunTodaysFreshnessAsync(visibleCharacterIds, _windowShutdownCts.Token);

                if (!result.Success &&
                    string.Equals(result.LastError, "Another freshness operation is already running.", StringComparison.Ordinal))
                {
                    MessageBox.Show(
                        "Another freshness operation is already running.",
                        "PMG Today's Freshness",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                if (!_isShuttingDown && result.NewKillmailsImported > 0)
                {
                    await RefreshCurrentBoardRowsFromLocalIntelAsync("Today's Freshness");
                }
            }
            catch (OperationCanceledException) when (_isShuttingDown || _windowShutdownCts.IsCancellationRequested)
            {
                AppLogger.UiInfo("Today's Freshness cancelled during shutdown.");
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Today's Freshness failed from the Intel UI.", ex);

                MessageBox.Show(
                    $"Today's Freshness failed.\n\n{ex.Message}",
                    "PMG Today's Freshness",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void RunHistoricalFreshnessButton_Click(object sender, RoutedEventArgs e)
        {
            var visibleCharacterIds = GetVisibleCharacterIdsForTodaysFreshness();
            if (visibleCharacterIds.Count == 0)
            {
                MessageBox.Show(
                    "Historical Freshness needs at least one visible Grill pilot with a resolved character ID.",
                    "PMG Historical Freshness",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                using var foregroundPriority = _backgroundIntelUpdateService.BeginForegroundPriority();
                var result = await _backgroundIntelUpdateService.RunHistoricalFreshnessAsync(visibleCharacterIds, _windowShutdownCts.Token);

                if (!result.Success &&
                    string.Equals(result.LastError, "Another freshness operation is already running.", StringComparison.Ordinal))
                {
                    MessageBox.Show(
                        "Another freshness operation is already running.",
                        "PMG Historical Freshness",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                if (!result.Success &&
                    string.Equals(result.LastError, "Historical Freshness already running.", StringComparison.Ordinal))
                {
                    MessageBox.Show(
                        "Historical Freshness is already running.",
                        "PMG Historical Freshness",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                if (!_isShuttingDown && result.MissingImportedCount > 0)
                {
                    await RefreshCurrentBoardRowsFromLocalIntelAsync("Historical Freshness");
                }
            }
            catch (OperationCanceledException) when (_isShuttingDown || _windowShutdownCts.IsCancellationRequested)
            {
                AppLogger.UiInfo("Historical Freshness cancelled during shutdown.");
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Historical Freshness failed from the Intel UI.", ex);

                MessageBox.Show(
                    $"Historical Freshness failed.\n\n{ex.Message}",
                    "PMG Historical Freshness",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public List<long> GetVisibleCharacterIdsForBackgroundHistoricalRepair()
        {
            return GetVisibleCharacterIdsForTodaysFreshness();
        }

        private List<long> GetVisibleCharacterIdsForTodaysFreshness()
        {
            return _currentRows
                .Select(row => row.CharacterId)
                .Where(characterId => long.TryParse(characterId, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                .Select(characterId => long.Parse(characterId!, CultureInfo.InvariantCulture))
                .Distinct()
                .ToList();
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
            if (e.Key == Key.Home &&
                (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                RequestWindowLayoutResetFromHotkey("Ctrl+Home hotkey");
                e.Handled = true;
                return;
            }

            if (IsTextEditingElement(e.OriginalSource as DependencyObject))
            {
                return;
            }

            switch (e.Key)
            {
                case Key.Insert:
                    ToggleCompactModeFromHotkey();
                    e.Handled = true;
                    return;

                case Key.Delete:
                    ClearBoard("Delete hotkey");
                    e.Handled = true;
                    return;

                case Key.Home:
                    AppLogger.UiInfo("Manual clipboard refresh requested from Home hotkey.");
                    _boardPopulationEntryController.InvalidateLastProcessedClipboard();
                    _ = ProcessClipboardIfValidAsync();
                    e.Handled = true;
                    return;

                case Key.Escape:
                    HandleEscapeHotkey();
                    e.Handled = true;
                    return;
            }
        }

        private void HandleEscapeHotkey()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastEscapeTapUtc).TotalMilliseconds <= TripleEscapeWindowMilliseconds)
            {
                _escapeTapCount++;
            }
            else
            {
                _escapeTapCount = 1;
            }

            _lastEscapeTapUtc = now;

            if (_escapeTapCount >= 3)
            {
                RequestApplicationShutdown("Triple Escape hotkey");
            }
        }

        private void OpenDetailsWindow(PilotBoardRow row)
        {
            if (_activePilotDetailWindow != null)
            {
                if (string.Equals(_activePilotDetailWindow.CharacterName, row.CharacterName, StringComparison.OrdinalIgnoreCase))
                {
                    _activePilotDetailWindow.Activate();
                    return;
                }

                CloseActiveDetailWindow();
            }

            _activePilotDetailWindow = new PilotDetailWindow(
                row,
                _pilotBoardRowDetailFormatter,
                _notesRepository,
                TryIgnoreForRow,
                ToggleWatchForRow,
                OpenZkillForRow)
            {
                Owner = this
            };
            _activePilotDetailWindow.ApplyThemeResources(Resources);
            _activePilotDetailWindow.Topmost = Topmost;
            PositionDetailWindow(_activePilotDetailWindow);
            _activePilotDetailWindow.Closed += ActivePilotDetailWindow_Closed;
            _activePilotDetailWindow.Show();
            AppLogger.UiInfo($"Details window opened. character='{row.CharacterName}'");
        }


        private void PositionDetailWindow(PilotDetailWindow detailWindow)
        {
            if (detailWindow == null)
            {
                return;
            }

            detailWindow.WindowStartupLocation = WindowStartupLocation.Manual;

            var detailWidth = detailWindow.Width > 0 ? detailWindow.Width : 430;
            var detailHeight = detailWindow.Height > 0 ? detailWindow.Height : 360;
            var ownerWidth = ActualWidth > 0 ? ActualWidth : Width;
            var ownerHeight = ActualHeight > 0 ? ActualHeight : Height;
            var ownerLeft = double.IsNaN(Left) ? 0 : Left;
            var ownerTop = double.IsNaN(Top) ? 0 : Top;
            var ownerHandle = new WindowInteropHelper(this).Handle;
            var monitor = ownerHandle != IntPtr.Zero
                ? FormsScreen.FromHandle(ownerHandle)
                : FormsScreen.FromPoint(new System.Drawing.Point(
                    (int)Math.Round(ownerLeft),
                    (int)Math.Round(ownerTop)));

            var presentationSource = PresentationSource.FromVisual(this);
            var transformFromDevice = presentationSource?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            var workAreaPixels = monitor.WorkingArea;
            var workTopLeft = transformFromDevice.Transform(new Point(workAreaPixels.Left, workAreaPixels.Top));
            var workBottomRight = transformFromDevice.Transform(new Point(workAreaPixels.Right, workAreaPixels.Bottom));
            var workLeft = workTopLeft.X;
            var workTop = workTopLeft.Y;
            var workRight = workBottomRight.X;
            var workBottom = workBottomRight.Y;

            var rightX = ownerLeft + ownerWidth + DetailWindowGap;
            var leftX = ownerLeft - detailWidth - DetailWindowGap;
            var canRight = rightX + detailWidth <= workRight;
            var canLeft = leftX >= workLeft;

            var preferLeft = GetPilotDetailPlacementPreference() == PilotDetailPlacementPreference.AutoPreferLeft;
            var preferredSide = preferLeft ? "left" : "right";
            var finalSide = preferredSide;

            if (preferLeft)
            {
                if (!canLeft && canRight)
                {
                    finalSide = "right";
                }
            }
            else if (!canRight && canLeft)
            {
                finalSide = "left";
            }

            var targetLeft = finalSide == "left" ? leftX : rightX;
            var targetTop = ownerTop;
            var clampedLeft = Clamp(targetLeft, workLeft, Math.Max(workLeft, workRight - detailWidth));
            var clampedTop = Clamp(targetTop, workTop, Math.Max(workTop, workBottom - detailHeight));
            var wasClamped = !AreClose(clampedLeft, targetLeft) || !AreClose(clampedTop, targetTop);

            detailWindow.Left = clampedLeft;
            detailWindow.Top = clampedTop;

            if (!string.Equals(finalSide, preferredSide, StringComparison.Ordinal) || wasClamped)
            {
                AppLogger.UiInfo(
                    $"Detail window placement adjusted. ownerBounds=({ownerLeft:0.##},{ownerTop:0.##},{ownerWidth:0.##},{ownerHeight:0.##}) workArea=({workLeft:0.##},{workTop:0.##},{workRight - workLeft:0.##},{workBottom - workTop:0.##}) preferredSide={preferredSide} finalSide={finalSide} finalBounds=({clampedLeft:0.##},{clampedTop:0.##},{detailWidth:0.##},{detailHeight:0.##})");
            }
        }

        private PilotDetailPlacementPreference GetPilotDetailPlacementPreference()
        {
            return Enum.TryParse<PilotDetailPlacementPreference>(
                _appSettings.PilotDetailPlacementPreference,
                ignoreCase: true,
                out var parsed)
                ? parsed
                : PilotDetailPlacementPreference.AutoPreferRight;
        }

        private void ActivePilotDetailWindow_Closed(object? sender, EventArgs e)
        {
            if (_activePilotDetailWindow != null)
            {
                _activePilotDetailWindow.Closed -= ActivePilotDetailWindow_Closed;
                _activePilotDetailWindow = null;
            }
        }

        private void CloseActiveDetailWindow()
        {
            if (_activePilotDetailWindow == null)
            {
                return;
            }

            var window = _activePilotDetailWindow;
            _activePilotDetailWindow = null;
            window.Closed -= ActivePilotDetailWindow_Closed;
            window.SaveCurrentState();
            window.Close();
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
                RecentPublicActivityText,
                CynoSignalText,
                CynoConfidenceBar,
                CynoEvidenceText,
                CynoLimitationsText,
                ExplainabilityText,
                NotesTagsBox,
                KnownCynoOverrideCheckBox,
                BaitOverrideCheckBox);

            UpdateIgnoreAllianceButtonState(row);
            UpdateWatchPilotDetailActionState(row);
        }

        private void HideDetailPane()
        {
            _detailPaneController.HideDetailPane(
                DetailPane,
                NotesTagsBox,
                KnownCynoOverrideCheckBox,
                BaitOverrideCheckBox);

            if (ExplainabilityText != null)
            {
                ExplainabilityText.Text = "Explainability: --";
            }

            if (RecentPublicActivityText != null)
            {
                RecentPublicActivityText.Text = "Recent Public Kill/Loss Activity: --";
            }

            if (CynoSignalText != null)
            {
                CynoSignalText.Text = "Cyno Signal: Unknown";
            }

            if (CynoConfidenceBar != null)
            {
                CynoConfidenceBar.Value = 0;
            }

            if (CynoEvidenceText != null)
            {
                CynoEvidenceText.Text = "Evidence: --";
            }

            if (CynoLimitationsText != null)
            {
                CynoLimitationsText.Text = "Limitations: --";
            }

            UpdateIgnoreAllianceButtonState(null);
            UpdateWatchPilotDetailActionState(null);
        }

        private void SaveCurrentNotesAndTags()
        {
            if (_activePilotDetailWindow != null)
            {
                _activePilotDetailWindow.SaveCurrentState();
                return;
            }

            _detailPaneController.SaveCurrentNotesAndTags(
                NotesTagsBox.Text,
                KnownCynoOverrideCheckBox.IsChecked == true,
                BaitOverrideCheckBox.IsChecked == true,
                PilotBoard.SelectedItem as PilotBoardRow);
        }

        private void IgnoreAllianceListView_IgnoreListChanged(object? sender, EventArgs e)
        {
            ApplyIgnoredAllianceRowsToCurrentBoard();
            RecomputeCorpAllianceCounts();
        }

        private void IgnoreAllianceButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedRow = GetSelectedOrDisplayedDetailRow();

            if (selectedRow == null)
            {
                AppLogger.UiWarn("Ignore alliance requested with no selected or displayed detail row.");
                return;
            }

            TryIgnoreAllianceForRow(selectedRow);
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
            if (PilotBoard.SelectedItem is PilotBoardRow selectedRow)
            {
                return selectedRow;
            }

            if (DetailPane.Visibility != Visibility.Visible)
            {
                return null;
            }

            var displayedCharacterName = SelectedCharacterText.Text;

            if (string.IsNullOrWhiteSpace(displayedCharacterName))
            {
                return null;
            }

            return _currentRows.FirstOrDefault(row =>
                string.Equals(
                    row.CharacterName,
                    displayedCharacterName.Trim(),
                    StringComparison.OrdinalIgnoreCase));
        }

        private bool IsRowDisplayedInDetailPane(PilotBoardRow row)
        {
            if (row == null || DetailPane.Visibility != Visibility.Visible)
            {
                return false;
            }

            if (PilotBoard.SelectedItem is PilotBoardRow selectedRow && ReferenceEquals(selectedRow, row))
            {
                return true;
            }

            return string.Equals(
                SelectedCharacterText.Text,
                row.CharacterName,
                StringComparison.OrdinalIgnoreCase);
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

        private void UpdateWatchPilotDetailActionState(PilotBoardRow? row)
        {
            if (WatchPilotDetailAction == null)
            {
                return;
            }

            if (row == null)
            {
                WatchPilotDetailAction.IsEnabled = false;
                WatchPilotDetailAction.Content = "Watch";
                WatchPilotDetailAction.ToolTip = "Select a resolved pilot to watch.";
                WatchPilotDetailAction.SetResourceReference(Control.ForegroundProperty, "SuccessGreenBrush");
                return;
            }

            var canWatch = TryGetPilotId(row.CharacterId).HasValue;
            WatchPilotDetailAction.IsEnabled = canWatch;
            WatchPilotDetailAction.Content = row.IsWatched ? "Unwatch" : "Watch";
            WatchPilotDetailAction.ToolTip = canWatch
                ? (row.IsWatched ? "Stop watching this pilot." : "Mark this pilot as watched.")
                : "Selected pilot does not have a known character ID yet.";
            WatchPilotDetailAction.SetResourceReference(
                Control.ForegroundProperty,
                row.IsWatched ? "WatchedPilotMarkerBrush" : "SuccessGreenBrush");
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

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private static bool AreClose(double left, double right)
        {
            return Math.Abs(left - right) < 0.5;
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

        private static long? TryGetPilotId(string? characterIdText)
        {
            if (string.IsNullOrWhiteSpace(characterIdText))
            {
                return null;
            }

            if (!long.TryParse(characterIdText.Trim(), out var characterId))
            {
                return null;
            }

            if (characterId <= 0)
            {
                return null;
            }

            return characterId;
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
            var pilotId = TryGetPilotId(row.CharacterId);
            if (!pilotId.HasValue)
            {
                UpdateWatchPilotDetailActionState(row);
                AppLogger.UiWarn($"Watch requested without a valid pilot ID. character='{row.CharacterName}'");
                return;
            }

            var newWatchedState = !row.IsWatched;
            if (!_watchedPilotRepository.SetWatched(row.CharacterId, newWatchedState))
            {
                UpdateWatchPilotDetailActionState(row);
                AppLogger.UiWarn($"Watch state change failed. character='{row.CharacterName}' characterId='{row.CharacterId}'");
                return;
            }

            row.IsWatched = newWatchedState;
            ApplyCurrentBoardOrdering();
            UpdateWatchPilotDetailActionState(row);
            RefreshDetailWindowIfSelected(row);

            AppLogger.UiInfo(
                $"Watch state changed. character='{row.CharacterName}' characterId='{row.CharacterId}' watched={row.IsWatched}");
        }

        private void ApplyCurrentBoardOrdering()
        {
            if (_currentRows.Count <= 1)
            {
                return;
            }

            var selectedRow = PilotBoard.SelectedItem as PilotBoardRow;
            var baseOrderIndexes = _currentRows
                .Select((row, index) => new KeyValuePair<PilotBoardRow, int>(row, index))
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            var reorderedRows = _currentRows
                .OrderBy(row => row, Comparer<PilotBoardRow>.Create((leftRow, rightRow) =>
                    CompareBoardRows(
                        leftRow,
                        baseOrderIndexes[leftRow],
                        rightRow,
                        baseOrderIndexes[rightRow])))
                .ToList();

            var changed = false;
            for (var index = 0; index < reorderedRows.Count; index++)
            {
                if (!ReferenceEquals(_currentRows[index], reorderedRows[index]))
                {
                    changed = true;
                    break;
                }
            }

            if (!changed)
            {
                return;
            }

            _currentRows.Clear();
            foreach (var row in reorderedRows)
            {
                _currentRows.Add(row);
            }

            if (selectedRow != null && _currentRows.Contains(selectedRow))
            {
                PilotBoard.SelectedItem = selectedRow;
            }
        }

        private int CompareBoardRows(PilotBoardRow leftRow, int leftIndex, PilotBoardRow rightRow, int rightIndex)
        {
            var watchedCompare = Comparer<bool>.Default.Compare(rightRow.IsWatched, leftRow.IsWatched);
            if (watchedCompare != 0)
            {
                return watchedCompare;
            }

            if (!string.IsNullOrWhiteSpace(_activeBoardSortMemberPath) && _activeBoardSortDirection.HasValue)
            {
                var valueCompare = CompareSortValues(
                    GetBoardSortValue(leftRow, _activeBoardSortMemberPath),
                    GetBoardSortValue(rightRow, _activeBoardSortMemberPath));

                if (valueCompare != 0)
                {
                    return _activeBoardSortDirection == ListSortDirection.Descending
                        ? -valueCompare
                        : valueCompare;
                }
            }

            return leftIndex.CompareTo(rightIndex);
        }

        private static int CompareSortValues(object? leftValue, object? rightValue)
        {
            if (leftValue == null && rightValue == null)
            {
                return 0;
            }

            if (leftValue == null)
            {
                return -1;
            }

            if (rightValue == null)
            {
                return 1;
            }

            if (leftValue is string leftString && rightValue is string rightString)
            {
                return StringComparer.OrdinalIgnoreCase.Compare(leftString, rightString);
            }

            if (leftValue is IComparable comparable)
            {
                try
                {
                    return comparable.CompareTo(rightValue);
                }
                catch (ArgumentException)
                {
                    return StringComparer.OrdinalIgnoreCase.Compare(
                        leftValue.ToString(),
                        rightValue.ToString());
                }
            }

            return StringComparer.OrdinalIgnoreCase.Compare(
                leftValue.ToString(),
                rightValue.ToString());
        }

        private static object? GetBoardSortValue(PilotBoardRow row, string? sortMemberPath)
        {
            if (row == null || string.IsNullOrWhiteSpace(sortMemberPath))
            {
                return null;
            }

            return sortMemberPath switch
            {
                nameof(PilotBoardRow.CharacterName) => row.CharacterName,
                nameof(PilotBoardRow.AllianceNameDisplay) => row.AllianceNameDisplay,
                nameof(PilotBoardRow.CorpNameDisplay) => row.CorpNameDisplay,
                nameof(PilotBoardRow.KillCount) => row.KillCount,
                nameof(PilotBoardRow.LossCount) => row.LossCount,
                nameof(PilotBoardRow.AvgAttackersWhenAttacking) => row.AvgAttackersWhenAttacking,
                nameof(PilotBoardRow.LastShipSeenName) => row.LastShipSeenName,
                nameof(PilotBoardRow.LastShipSeenDateDisplay) => row.LastShipSeenAtUtc,
                nameof(PilotBoardRow.LastShipSeenAtUtc) => row.LastShipSeenAtUtc,
                nameof(PilotBoardRow.LastPublicCynoCapableHull) => row.LastPublicCynoCapableHull,
                _ => GetBoardSortValueByReflection(row, sortMemberPath)
            };
        }

        private static object? GetBoardSortValueByReflection(PilotBoardRow row, string sortMemberPath)
        {
            var property = typeof(PilotBoardRow).GetProperty(sortMemberPath);
            return property?.GetValue(row);
        }

        private void ResetManualBoardSort()
        {
            _activeBoardSortMemberPath = nameof(PilotBoardRow.CharacterName);
            _activeBoardSortDirection = ListSortDirection.Ascending;

            if (CharacterColumn != null)
            {
                ApplySortIndicatorState(CharacterColumn, ListSortDirection.Ascending);
                return;
            }

            ClearBoardSortIndicators();
        }

        private void ApplySortIndicatorState(DataGridColumn activeColumn, ListSortDirection direction)
        {
            if (PilotBoard == null)
            {
                return;
            }

            foreach (var column in PilotBoard.Columns)
            {
                column.SortDirection = ReferenceEquals(column, activeColumn)
                    ? direction
                    : null;
            }
        }

        private void ClearBoardSortIndicators()
        {
            if (PilotBoard == null)
            {
                return;
            }

            foreach (var column in PilotBoard.Columns)
            {
                column.SortDirection = null;
            }
        }

        private static string? GetSortMemberPathFromColumn(DataGridColumn column)
        {
            if (column is DataGridBoundColumn boundColumn &&
                boundColumn.Binding is Binding binding &&
                binding.Path != null)
            {
                return binding.Path.Path;
            }

            return null;
        }

        private static long? GetIgnoreId(PilotBoardRow row, IgnoreEntryType type)
        {
            return type switch
            {
                IgnoreEntryType.Pilot => TryGetAllianceId(row.CharacterId),
                IgnoreEntryType.Corporation => TryGetAllianceId(row.CorpId),
                IgnoreEntryType.Alliance => TryGetAllianceId(row.AllianceId),
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
            if (BoardSummaryText == null)
            {
                return;
            }

            var visibleRows = _currentRows.ToList();
            var watchedCount = visibleRows.Count(row => row.IsWatched);
            var baitCount = visibleRows.Count(row => row.BaitOverride || row.HasDerivedBaitEvidence);
            var hardCynoCount = visibleRows.Count(row =>
                string.Equals(row.BoardSignalKind, "ConfirmedNormal", StringComparison.OrdinalIgnoreCase));
            var covertCynoCount = visibleRows.Count(row =>
                string.Equals(row.BoardSignalKind, "ConfirmedCovert", StringComparison.OrdinalIgnoreCase));

            BoardSummaryText.Text = string.Join(" | ", new[]
            {
                $"Visible {visibleRows.Count}",
                $"Watched {watchedCount}",
                $"Bait {baitCount}",
                $"Hard Cyno {hardCynoCount}",
                $"Covert Cyno {covertCynoCount}"
            });
        }

        private static int CountDistinctVisibleAffiliations(
            IEnumerable<PilotBoardRow> rows,
            Func<PilotBoardRow, string> selector)
        {
            return rows
                .Select(row => selector(row)?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        }

        private static List<string> BuildTopVisibleAffiliationSummaries(
            IEnumerable<PilotBoardRow> rows,
            Func<PilotBoardRow, string> selector,
            int maxCount)
        {
            return rows
                .Select(row => selector(row)?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .GroupBy(value => value!, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Take(maxCount)
                .Select(group => $"{group.Key} [{group.Count()}]")
                .ToList();
        }

        private static List<AnalysisAffiliationSummary> BuildTopVisibleAffiliationDetails(
            IEnumerable<PilotBoardRow> rows,
            Func<PilotBoardRow, string> nameSelector,
            Func<PilotBoardRow, string> idSelector,
            int maxCount)
        {
            return rows
                .Select(row => new
                {
                    Name = nameSelector(row)?.Trim(),
                    Id = idSelector(row)?.Trim()
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .GroupBy(
                    item => item.Name!,
                    StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Take(maxCount)
                .Select(group => new AnalysisAffiliationSummary(
                    group.Key,
                    group.Count(),
                    group.Select(item => item.Id).FirstOrDefault(id => !string.IsNullOrWhiteSpace(id)) ?? string.Empty))
                .ToList();
        }

        private static List<AnalysisAffiliationSummary> BuildVisibleAffiliationDetails(
            IEnumerable<PilotBoardRow> rows,
            Func<PilotBoardRow, string> nameSelector,
            Func<PilotBoardRow, string> idSelector)
        {
            return rows
                .Select(row => new
                {
                    Name = nameSelector(row)?.Trim(),
                    Id = idSelector(row)?.Trim()
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .GroupBy(
                    item => item.Name!,
                    StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => new AnalysisAffiliationSummary(
                    group.Key,
                    group.Count(),
                    group.Select(item => item.Id).FirstOrDefault(id => !string.IsNullOrWhiteSpace(id)) ?? string.Empty))
                .ToList();
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
            if (AnalysisEmptyStateText == null ||
                AnalysisDetailsPanel == null ||
                AnalysisVisibleCountsText == null ||
                AnalysisUniqueCountsText == null ||
                AnalysisAllianceTopText == null ||
                AnalysisCorpTopText == null ||
                AnalysisSignalsText == null ||
                AnalysisHighlightsText == null)
            {
                return;
            }

            var visibleRows = _currentRows.ToList();
            if (visibleRows.Count == 0)
            {
                AnalysisEmptyStateText.Visibility = Visibility.Visible;
                AnalysisDetailsPanel.Visibility = Visibility.Collapsed;
                AnalysisEmptyStateText.Text = "No visible pilots yet. Load or refresh the Grill to see aggregate analysis.";
                return;
            }

            var watchedCount = visibleRows.Count(row => row.IsWatched);
            var uniqueCorpCount = CountDistinctVisibleAffiliations(visibleRows, row => row.CorpName);
            var uniqueAllianceCount = CountDistinctVisibleAffiliations(visibleRows, row => row.AllianceName);
            var topAllianceDetails = BuildTopVisibleAffiliationDetails(
                visibleRows,
                row => row.AllianceName,
                row => row.AllianceId,
                maxCount: 3);
            var topCorpDetails = BuildTopVisibleAffiliationDetails(
                visibleRows,
                row => row.CorpName,
                row => row.CorpId,
                maxCount: 3);
            var allAllianceDetails = BuildVisibleAffiliationDetails(
                visibleRows,
                row => row.AllianceName,
                row => row.AllianceId);
            var allCorpDetails = BuildVisibleAffiliationDetails(
                visibleRows,
                row => row.CorpName,
                row => row.CorpId);
            var baitCount = visibleRows.Count(row => row.BaitOverride || row.HasDerivedBaitEvidence);
            var hardCynoCount = visibleRows.Count(row =>
                string.Equals(row.BoardSignalKind, "ConfirmedNormal", StringComparison.OrdinalIgnoreCase));
            var covertCynoCount = visibleRows.Count(row =>
                string.Equals(row.BoardSignalKind, "ConfirmedCovert", StringComparison.OrdinalIgnoreCase));

            AnalysisEmptyStateText.Visibility = Visibility.Collapsed;
            AnalysisDetailsPanel.Visibility = Visibility.Visible;
            AnalysisVisibleCountsText.Text = $"Visible pilots: {visibleRows.Count} | Watched pilots: {watchedCount} | Confirmed cynos: Hard {hardCynoCount} | Covert {covertCynoCount} | Bait {baitCount}";
            AnalysisUniqueCountsText.Text = $"Unique corps: {uniqueCorpCount} | Unique alliances: {uniqueAllianceCount}";
            PopulateAnalysisAllianceTopText(topAllianceDetails);
            PopulateAnalysisCorpTopText(topCorpDetails);
            PopulateAnalysisAffiliationList(_analysisAllianceItems, allAllianceDetails, "alliance");
            PopulateAnalysisAffiliationList(_analysisCorpItems, allCorpDetails, "corporation");
            AnalysisSignalsText.Text = string.Empty;
            PopulateAnalysisHighlightsText(visibleRows);
        }

        private static void PopulateAnalysisAffiliationList(
            ObservableCollection<AnalysisAffiliationListItem> target,
            IReadOnlyList<AnalysisAffiliationSummary> summaries,
            string entityType)
        {
            target.Clear();
            foreach (var summary in summaries)
            {
                target.Add(new AnalysisAffiliationListItem(
                    summary.Name,
                    summary.Id,
                    entityType,
                    $"{summary.Name} [{summary.Count}]"));
            }
        }

        private bool IsSessionContextStale()
        {
            return _currentEveSessionContext == null ||
                   (DateTime.UtcNow - _lastSessionContextRefreshUtc) > TimeSpan.FromMinutes(3);
        }

        private void TriggerSessionContextRefresh(string reason, bool force)
        {
            if (_isShuttingDown)
            {
                return;
            }

            if (!force && !IsSessionContextStale())
            {
                return;
            }

            if (_isSessionContextRefreshInFlight)
            {
                return;
            }

            _ = RefreshSessionContextAsync(reason);
        }

        private async Task RefreshSessionContextAsync(string reason)
        {
            if (_isSessionContextRefreshInFlight || _isShuttingDown)
            {
                return;
            }

            _isSessionContextRefreshInFlight = true;
            try
            {
                AppLogger.UiDebug($"EVE session context refresh started. reason='{reason}'");
                var context = await _eveSessionContextService.CaptureAsync(_windowShutdownCts.Token);
                _lastSessionContextRefreshUtc = DateTime.UtcNow;
                _currentEveSessionContext = context;

                await Dispatcher.InvokeAsync(() => ApplyEveSessionContext(context));
            }
            catch (OperationCanceledException)
            {
                // Shutdown or refresh cancellation is expected.
            }
            catch (Exception ex)
            {
                AppLogger.UiWarn($"EVE session context refresh failed. reason='{reason}' message={ex.Message}");
                var fallback = new EveSessionContext
                {
                    CharacterName = "Not detected",
                    SolarSystemName = "Not detected",
                    EvidenceSource = "Unable to read local evidence",
                    EvidenceTimestampUtc = null,
                    Confidence = "None",
                    StatusMessage = "Unable to infer EVE context"
                };

                _lastSessionContextRefreshUtc = DateTime.UtcNow;
                _currentEveSessionContext = fallback;
                await Dispatcher.InvokeAsync(() => ApplyEveSessionContext(fallback));
            }
            finally
            {
                _isSessionContextRefreshInFlight = false;
            }
        }

        private void ApplyEveSessionContext(EveSessionContext context)
        {
            if (AnalysisCurrentCharacterText == null ||
                AnalysisCurrentSystemText == null ||
                AnalysisEvidenceSourceText == null ||
                AnalysisObservedAtText == null ||
                AnalysisContextStatusText == null)
            {
                return;
            }

            AnalysisCurrentCharacterText.Text = string.IsNullOrWhiteSpace(context.CharacterName)
                ? "Not detected"
                : context.CharacterName;
            AnalysisCurrentSystemText.Text = string.IsNullOrWhiteSpace(context.SolarSystemName)
                ? "Not detected"
                : context.SolarSystemName;
            AnalysisEvidenceSourceText.Text = string.IsNullOrWhiteSpace(context.EvidenceSource)
                ? "Not configured"
                : context.EvidenceSource;
            AnalysisObservedAtText.Text = context.EvidenceTimestampUtc.HasValue
                ? context.EvidenceTimestampUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                : "Not detected";

            var statusParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(context.Confidence))
            {
                statusParts.Add(context.Confidence);
            }

            if (!string.IsNullOrWhiteSpace(context.StatusMessage))
            {
                statusParts.Add(context.StatusMessage);
            }

            AnalysisContextStatusText.Text = statusParts.Count > 0
                ? string.Join(" | ", statusParts)
                : "Unable to infer EVE context";
        }

        private void PopulateAnalysisAllianceTopText(IReadOnlyList<AnalysisAffiliationSummary> alliances)
        {
            AnalysisAllianceTopText.Inlines.Clear();
            AnalysisAllianceTopText.Inlines.Add(new Run("Top alliances: "));

            if (alliances.Count == 0)
            {
                AnalysisAllianceTopText.Inlines.Add(new Run("none visible"));
                return;
            }

            for (var index = 0; index < alliances.Count; index++)
            {
                if (index > 0)
                {
                    AnalysisAllianceTopText.Inlines.Add(new Run(" | "));
                }

                var alliance = alliances[index];
                if (!string.IsNullOrWhiteSpace(alliance.Id) &&
                    long.TryParse(alliance.Id, out _))
                {
                    AddHyperlinkInline(
                        AnalysisAllianceTopText,
                        alliance.Name,
                        BuildAllianceZkillUrl(alliance.Id),
                        $"Open {alliance.Name} on zKill");
                }
                else
                {
                    AnalysisAllianceTopText.Inlines.Add(new Run(alliance.Name));
                }

                AnalysisAllianceTopText.Inlines.Add(new Run($" [{alliance.Count}]"));
            }
        }

        private void PopulateAnalysisCorpTopText(IReadOnlyList<AnalysisAffiliationSummary> corps)
        {
            AnalysisCorpTopText.Inlines.Clear();
            AnalysisCorpTopText.Inlines.Add(new Run("Top corps: "));

            if (corps.Count == 0)
            {
                AnalysisCorpTopText.Inlines.Add(new Run("none visible"));
                return;
            }

            for (var index = 0; index < corps.Count; index++)
            {
                if (index > 0)
                {
                    AnalysisCorpTopText.Inlines.Add(new Run(" | "));
                }

                var corp = corps[index];
                if (!string.IsNullOrWhiteSpace(corp.Id) &&
                    long.TryParse(corp.Id, out _))
                {
                    AddHyperlinkInline(
                        AnalysisCorpTopText,
                        corp.Name,
                        BuildCorporationZkillUrl(corp.Id),
                        $"Open {corp.Name} on zKill");
                }
                else
                {
                    AnalysisCorpTopText.Inlines.Add(new Run(corp.Name));
                }

                AnalysisCorpTopText.Inlines.Add(new Run($" [{corp.Count}]"));
            }
        }

        private void PopulateAnalysisHighlightsText(IReadOnlyList<PilotBoardRow> visibleRows)
        {
            AnalysisHighlightsText.Inlines.Clear();
            AnalysisHighlightsText.Inlines.Add(new Run("Highlights: "));

            var topDangerousPilots = visibleRows
                .Where(row => (row.KillCount ?? 0) > 0 || (row.LossCount ?? 0) > 0)
                .OrderByDescending(row => ComputeDangerPercent(row))
                .ThenByDescending(row => row.KillCount ?? 0)
                .ThenBy(row => row.CharacterName, StringComparer.OrdinalIgnoreCase)
                .GroupBy(
                    row => !string.IsNullOrWhiteSpace(row.CharacterId)
                        ? $"id:{row.CharacterId.Trim()}"
                        : $"name:{row.CharacterName.Trim().ToUpperInvariant()}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Take(3)
                .ToList();

            var addedAny = false;
            for (var index = 0; index < topDangerousPilots.Count; index++)
            {
                var pilot = topDangerousPilots[index];
                AddHighlightCharacterLink(
                    AnalysisHighlightsText,
                    index == 0 ? "Top Dangerous" : string.Empty,
                    pilot.CharacterName,
                    pilot.CharacterId,
                    $"{ComputeDangerPercent(pilot):0.#}%",
                    ref addedAny);
            }

            if (!addedAny)
            {
                AnalysisHighlightsText.Inlines.Add(new Run("none visible"));
            }
        }

        private void AddHighlightCharacterLink(
            TextBlock target,
            string label,
            string characterName,
            string characterId,
            string valueText,
            ref bool addedAny)
        {
            if (addedAny)
            {
                target.Inlines.Add(new Run(" | "));
            }

            if (!string.IsNullOrWhiteSpace(label))
            {
                target.Inlines.Add(new Run($"{label}: "));
            }

            var hasCharacterId = !string.IsNullOrWhiteSpace(characterId) && long.TryParse(characterId, out _);
            if (hasCharacterId)
            {
                AddHyperlinkInline(
                    target,
                    characterName,
                    _zkillUrlBuilder.BuildCharacterUrl(characterId),
                    $"Open {characterName} on zKill");
            }
            else
            {
                target.Inlines.Add(new Run(characterName));
            }

            target.Inlines.Add(new Run($" [{valueText}]"));
            addedAny = true;
        }

        private static double ComputeDangerPercent(PilotBoardRow row)
        {
            var kills = Math.Max(0, row.KillCount ?? 0);
            var losses = Math.Max(0, row.LossCount ?? 0);
            var total = kills + losses;

            return total <= 0
                ? 0
                : (double)kills / total * 100d;
        }

        private void AddHyperlinkInline(TextBlock target, string text, string url, string toolTip)
        {
            var hyperlink = new Hyperlink(new Run(text))
            {
                NavigateUri = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null,
                ToolTip = toolTip
            };
            hyperlink.RequestNavigate += AnalysisHyperlink_RequestNavigate;
            target.Inlines.Add(hyperlink);
        }

        private string BuildAllianceZkillUrl(string allianceId)
        {
            return string.IsNullOrWhiteSpace(allianceId)
                ? string.Empty
                : $"https://zkillboard.com/alliance/{Uri.EscapeDataString(allianceId.Trim())}/";
        }

        private string BuildCorporationZkillUrl(string corporationId)
        {
            return string.IsNullOrWhiteSpace(corporationId)
                ? string.Empty
                : $"https://zkillboard.com/corporation/{Uri.EscapeDataString(corporationId.Trim())}/";
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
                ? BuildAllianceZkillUrl(item.Id)
                : BuildCorporationZkillUrl(item.Id);

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

        private sealed record AnalysisAffiliationSummary(string Name, int Count, string Id);
        private sealed record AnalysisAffiliationListItem(string Name, string Id, string EntityType, string DisplayText);

        private void RestoreWindowLayoutFromSettings()
        {
            var virtualDesktopSummary = BuildVirtualDesktopSummary();
            var fallbackBounds = GetDefaultWindowBoundsForCurrentDisplay();
            var hasSavedBounds = TryGetSavedWindowBounds(out var savedBounds);

            Rect targetBounds;
            string restoreDecision;
            string restoreReason;

            if (!hasSavedBounds)
            {
                targetBounds = fallbackBounds;
                restoreDecision = "Fallback";
                restoreReason = "No saved bounds were available.";
            }
            else if (!TryValidateWindowBounds(savedBounds, out var failureReason))
            {
                targetBounds = fallbackBounds;
                restoreDecision = "Fallback";
                restoreReason = failureReason;
            }
            else
            {
                targetBounds = savedBounds;
                restoreDecision = "Applied";
                restoreReason = "Saved bounds are visible on the current monitor layout.";
            }

            AppLogger.UiInfo(
                $"Window layout restore decision={restoreDecision} savedBounds={DescribeRect(hasSavedBounds ? savedBounds : Rect.Empty)} fallbackReason='{restoreReason}' wasMaximized={_appSettings.SavedWindowIsMaximized} virtualWorkAreas={virtualDesktopSummary}");

            WindowState = WindowState.Normal;
            Left = targetBounds.Left;
            Top = targetBounds.Top;
            Width = targetBounds.Width;
            Height = targetBounds.Height;
            _lastKnownNormalBounds = targetBounds;

            if (_appSettings.SavedWindowIsMaximized)
            {
                WindowState = WindowState.Maximized;
            }

            _lastNonMinimizedWindowState = _appSettings.SavedWindowIsMaximized
                ? WindowState.Maximized
                : WindowState.Normal;

            AppLogger.UiInfo(
                $"Window layout restore applied finalBounds={DescribeRect(targetBounds)} finalWindowState={WindowState}");
        }

        private void SaveWindowLayoutToSettings(string reason)
        {
            var effectiveState = WindowState == WindowState.Minimized
                ? _lastNonMinimizedWindowState
                : WindowState;

            if (effectiveState == WindowState.Maximized && IsUsableWindowBounds(RestoreBounds))
            {
                _lastKnownNormalBounds = RestoreBounds;
            }
            else if (WindowState == WindowState.Normal)
            {
                TrackCurrentNormalWindowBounds("Save");
            }

            var bounds = IsUsableWindowBounds(_lastKnownNormalBounds)
                ? _lastKnownNormalBounds
                : effectiveState == WindowState.Maximized
                    ? RestoreBounds
                    : new Rect(Left, Top, Width, Height);

            if (!TryValidateWindowBounds(bounds, out var failureReason))
            {
                AppLogger.UiWarn(
                    $"Window layout save skipped. reason='{reason}' bounds={DescribeRect(bounds)} failureReason='{failureReason}' virtualWorkAreas={BuildVirtualDesktopSummary()}");
                return;
            }

            _appSettings.SavedWindowLeft = bounds.Left;
            _appSettings.SavedWindowTop = bounds.Top;
            _appSettings.SavedWindowWidth = bounds.Width;
            _appSettings.SavedWindowHeight = bounds.Height;
            _appSettings.SavedWindowIsMaximized = effectiveState == WindowState.Maximized;
            _mainWindowAppearanceController.SaveSettings(_appSettings);

            AppLogger.UiInfo(
                $"Window layout saved. reason='{reason}' bounds={DescribeRect(bounds)} maximized={_appSettings.SavedWindowIsMaximized} virtualWorkAreas={BuildVirtualDesktopSummary()}");
        }

        private void ClearSavedWindowLayoutSettings()
        {
            _appSettings.SavedWindowLeft = null;
            _appSettings.SavedWindowTop = null;
            _appSettings.SavedWindowWidth = null;
            _appSettings.SavedWindowHeight = null;
            _appSettings.SavedWindowIsMaximized = false;
            _mainWindowAppearanceController.SaveSettings(_appSettings);
        }

        private bool TryGetSavedWindowBounds(out Rect bounds)
        {
            bounds = Rect.Empty;

            if (!_appSettings.SavedWindowLeft.HasValue ||
                !_appSettings.SavedWindowTop.HasValue ||
                !_appSettings.SavedWindowWidth.HasValue ||
                !_appSettings.SavedWindowHeight.HasValue)
            {
                return false;
            }

            bounds = new Rect(
                _appSettings.SavedWindowLeft.Value,
                _appSettings.SavedWindowTop.Value,
                _appSettings.SavedWindowWidth.Value,
                _appSettings.SavedWindowHeight.Value);

            return true;
        }

        private bool TryValidateWindowBounds(Rect bounds, out string failureReason)
        {
            if (double.IsNaN(bounds.Left) || double.IsInfinity(bounds.Left) ||
                double.IsNaN(bounds.Top) || double.IsInfinity(bounds.Top) ||
                double.IsNaN(bounds.Width) || double.IsInfinity(bounds.Width) ||
                double.IsNaN(bounds.Height) || double.IsInfinity(bounds.Height))
            {
                failureReason = "Bounds contained NaN or Infinity.";
                return false;
            }

            if (bounds.Width < Math.Max(MinWidth, MinimumSavedWindowWidth) ||
                bounds.Height < Math.Max(MinHeight, MinimumSavedWindowHeight))
            {
                failureReason = "Bounds were smaller than the minimum safe size.";
                return false;
            }

            foreach (var workArea in GetMonitorWorkAreasDip())
            {
                var intersection = Rect.Intersect(
                    bounds,
                    workArea);

                if (!intersection.IsEmpty &&
                    intersection.Width >= MinimumVisibleWindowEdge &&
                    intersection.Height >= MinimumVisibleWindowEdge)
                {
                    failureReason = string.Empty;
                    return true;
                }
            }

            failureReason = "Bounds were outside the current monitor work areas.";
            return false;
        }

        private Rect GetDefaultWindowBoundsForCurrentDisplay()
        {
            var fallbackScreen = FormsScreen.PrimaryScreen
                ?? FormsScreen.AllScreens.FirstOrDefault();
            var workArea = fallbackScreen != null
                ? GetScreenWorkAreaDip(fallbackScreen)
                : new Rect(0, 0, 1280, 800);

            var width = Math.Max(MinimumSavedWindowWidth, DefaultWindowWidth);
            var height = Math.Max(MinimumSavedWindowHeight, DefaultWindowHeight);

            width = Math.Min(width, workArea.Width);
            height = Math.Min(height, workArea.Height);

            var left = workArea.Left + Math.Max(0, (workArea.Width - width) / 2);
            var top = workArea.Top + Math.Max(0, (workArea.Height - height) / 2);

            return new Rect(left, top, width, height);
        }

        private bool IsUsableWindowBounds(Rect bounds)
        {
            return !bounds.IsEmpty &&
                   !double.IsNaN(bounds.Left) &&
                   !double.IsNaN(bounds.Top) &&
                   !double.IsNaN(bounds.Width) &&
                   !double.IsNaN(bounds.Height) &&
                   !double.IsInfinity(bounds.Left) &&
                   !double.IsInfinity(bounds.Top) &&
                   !double.IsInfinity(bounds.Width) &&
                   !double.IsInfinity(bounds.Height) &&
                   bounds.Width > 0 &&
                   bounds.Height > 0;
        }

        private IReadOnlyList<Rect> GetMonitorWorkAreasDip()
        {
            return FormsScreen.AllScreens
                .Select(GetScreenWorkAreaDip)
                .Where(IsUsableWindowBounds)
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

        private string BuildVirtualDesktopSummary()
        {
            var workAreas = GetMonitorWorkAreasDip();
            if (workAreas.Count == 0)
            {
                return "none";
            }

            var virtualBounds = workAreas[0];
            foreach (var workArea in workAreas.Skip(1))
            {
                virtualBounds = Rect.Union(virtualBounds, workArea);
            }

            return $"virtual={DescribeRect(virtualBounds)} workAreas={string.Join(";", workAreas.Select(DescribeRect))}";
        }

        private static string DescribeRect(Rect bounds)
        {
            return bounds.IsEmpty
                ? "<empty>"
                : $"[{bounds.Left:0.##},{bounds.Top:0.##},{bounds.Width:0.##},{bounds.Height:0.##}]";
        }

        private async Task RunEnableKillmailDbPullAsync()
        {
            try
            {
                EnableKillmailDbPullButton.IsEnabled = false;

                var seedDays = _mainWindowAppearanceController.GetMaxKillmailAgeDaysSettingValue(_appSettings);

                AppLogger.UiInfo(
                    $"Enable KillMail DB Pull requested. seedDays={seedDays} displayKillmailPath={KillmailPaths.GetKillmailDataDirectoryDisplayPath()} source={KillmailPaths.GetKillmailDataDirectorySourceDescription()}");

                await _backgroundIntelUpdateService.EnableKillmailDbPullAsync(seedDays, _windowShutdownCts.Token);

                AppLogger.UiInfo($"Enable KillMail DB Pull completed successfully. seedDays={seedDays}");
            }
            catch (OperationCanceledException) when (_isShuttingDown || _windowShutdownCts.IsCancellationRequested)
            {
                AppLogger.UiInfo("Enable KillMail DB Pull cancelled during shutdown.");
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
                if (!_isShuttingDown)
                {
                    EnableKillmailDbPullButton.IsEnabled = true;
                }
            }
        }


        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    }
}
