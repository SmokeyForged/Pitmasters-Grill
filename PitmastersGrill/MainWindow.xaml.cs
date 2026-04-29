using PitmastersGrill.Diagnostics;
using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using PitmastersGrill.Providers;
using PitmastersGrill.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
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
        private const int ClipboardDebounceMilliseconds = 250;
        private const int DefaultBoardPopulationRetryDelaySeconds = 12;
        private const int MaxBoardPopulationRetryAttempts = 5;
        private const int CompactDragHoldMilliseconds = 300;
        private const int TripleEscapeWindowMilliseconds = 1500;
        private const double DetailWindowGap = 8;

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
        private readonly MainWindowDiagnostics _diagnostics;
        private readonly IntelUpdateBannerController _intelUpdateBannerController;
        private readonly BoardPopulationTimingMarkerTracker _boardPopulationTimingMarkerTracker;
        private readonly IgnoreAllianceCoordinator _ignoreAllianceCoordinator;
        private readonly IgnoreAllianceBoardController _ignoreAllianceBoardController;
        private readonly DispatcherTimer _clipboardDebounceTimer;
        private readonly DispatcherTimer _compactDragHoldTimer;
        private readonly CancellationTokenSource _windowShutdownCts = new();
        private readonly SystemTrayIconService _systemTrayIconService;
        private IgnoreAllianceListView? _ignoreAllianceListView;


        private readonly ObservableCollection<PilotBoardRow> _currentRows = new();
        private readonly ObservableCollection<ProviderHealthSnapshot> _providerHealthRows = new();
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
        private string? _activeBoardSortMemberPath;
        private ListSortDirection? _activeBoardSortDirection;

        public MainWindow(BackgroundIntelUpdateService backgroundIntelUpdateService)
        {
            _backgroundIntelUpdateService = backgroundIntelUpdateService;
            _backgroundIntelUpdateService.StatusChanged += OnIntelUpdateStatusChanged;

            var appSettingsService = new AppSettingsService();
            _mainWindowAppearanceController = new MainWindowAppearanceController(appSettingsService);
            _boardPopulationStatusController = new BoardPopulationStatusController();

            _isApplyingSettings = true;
            InitializeComponent();
            RegisterCompactBoardDragHandlers();

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
            _mainWindowAppearanceController.ApplyPanelModeShell(this, _appSettings, Resources);

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

            InitializeBoardColumnVisibilityUi();
            InitializePilotDetailPlacementUi();

            AppLogger.ConfigureLogLevel(_appSettings.LogLevel);

            _isApplyingSettings = false;

            _mainWindowAppearanceController.ApplyTheme(Resources, _appSettings, this, ApplyBoardPopulationStatusVisual);
            _mainWindowAppearanceController.ApplyWindowSettings(this, _appSettings, WindowOpacityValueText, Resources);

            PilotBoard.ItemsSource = _currentRows;
            ProviderHealthGrid.ItemsSource = _providerHealthRows;
            RefreshProviderHealthUi();
            RefreshCacheStatsUi();
            UpdateLastRefreshed();
            UpdateBoardPopulationStatus("Board population idle", BoardPopulationStatusKind.Neutral);
            HideDetailPane();
            UpdateOpenDetailsButtonState();
            ApplyCompactModeUi();
            ApplyIntelUpdateSnapshot(_backgroundIntelUpdateService.GetSnapshot());

            AppLogger.DatabaseInfo(
                $"Killmail data path resolved. displayPath={KillmailPaths.GetKillmailDataDirectoryDisplayPath()} source={KillmailPaths.GetKillmailDataDirectorySourceDescription()}");

            AppLogger.UiInfo(
                $"MainWindow ready. darkMode={_appSettings.DarkModeEnabled} alwaysOnTop={_appSettings.AlwaysOnTopEnabled} panelMode={_appSettings.PanelModeEnabled} opacityPercent={_mainWindowAppearanceController.CoerceOpacityPercent(_appSettings.WindowOpacityPercent):0} logLevel={_appSettings.LogLevel}");
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwnd = new WindowInteropHelper(this).Handle;
            AddClipboardFormatListener(hwnd);

            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);

            _mainWindowAppearanceController.ApplyTitleBarTheme(this, _appSettings.DarkModeEnabled);
            UpdateWindowStateUi();

            AppLogger.UiInfo("MainWindow source initialized. Clipboard listener attached and title bar theme applied.");
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
            _clipboardDebounceTimer.Stop();
            _clipboardDebounceTimer.Tick -= ClipboardDebounceTimer_Tick;
            _compactDragHoldTimer.Stop();
            _compactDragHoldTimer.Tick -= CompactDragHoldTimer_Tick;
            _systemTrayIconService.Dispose();
            _diagnostics.Dispose();

            var hwnd = new WindowInteropHelper(this).Handle;
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

            if (compact)
            {
                MainTabControl.SelectedIndex = 0;
                CloseActiveDetailWindow();
            }

            TopCommandGrid.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            BoardStatusFooter.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            MainContentGrid.Margin = compact ? new Thickness(1) : new Thickness(12);
            MainTabControl.BorderThickness = compact ? new Thickness(0) : new Thickness(1);
            MainTabControl.Margin = compact ? new Thickness(0) : new Thickness(0);

            AppLogger.UiInfo($"Compact mode changed. enabled={compact}");
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
            UpdateWindowStateUi();
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
                ScheduleClipboardProcessing();
            }

            return IntPtr.Zero;
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
            _currentRows.Clear();

            foreach (var row in initialRows)
            {
                row.KnownCynoOverride = _notesRepository.GetKnownCynoOverride(row.CharacterName);
                row.BaitOverride = _notesRepository.GetBaitOverride(row.CharacterName);
                row.HasNotes = _notesRepository.HasNotes(row.CharacterName);
                ApplyWatchedState(row);
                _pilotBoardRowDetailFormatter.UpdateConfirmedCynoModuleState(row);
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

        private void OpenDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            if (PilotBoard.SelectedItem is not PilotBoardRow selectedRow)
            {
                AppLogger.UiWarn("Open details requested with no selected row.");
                return;
            }

            OpenDetailsWindow(selectedRow);
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
            if (OpenDetailsButton == null)
            {
                return;
            }

            OpenDetailsButton.IsEnabled = PilotBoard?.SelectedItem is PilotBoardRow;
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
                nameof(PilotBoardRow.LastShipSeenDateDisplay) => row.LastShipSeenDateDisplay,
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
            _activeBoardSortMemberPath = null;
            _activeBoardSortDirection = null;
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

    }
}
