using PitmastersGrill.Diagnostics;
using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using PitmastersGrill.Services;
using PitmastersGrill.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;

namespace PitmastersGrill
{
    public partial class MainWindow : Window
    {
        private const int WmClipboardUpdate = 0x031D;
        private const int WmHotKey = 0x0312;
        private const uint ModControl = 0x0002;
        private const int ClipboardDebounceMilliseconds = 250;
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
        private IgnoreAllianceListView? _ignoreAllianceListView;

        private readonly CurrentBoardSession _currentBoardSession = new();
        private readonly ObservableCollection<ProviderHealthSnapshot> _providerHealthRows = new();
        private readonly ObservableCollection<AnalysisAffiliationListItem> _analysisAllianceItems = new();
        private readonly ObservableCollection<AnalysisAffiliationListItem> _analysisCorpItems = new();
        private readonly CacheMaintenanceService _cacheMaintenanceService = new();
        private readonly KillmailDerivedIntelRebuildService _killmailDerivedIntelRebuildService = new();
        private readonly BoardAffiliationCountService _boardAffiliationCountService = new();
        private bool _isApplyingSettings;
        private bool _isShuttingDown;
        private bool _compactDragPending;
        private Point _compactDragStartPoint;
        private bool _isMainWindowInitialized;
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
                _nativeInputApi,
                ModControl,
                GlobalResetWindowHotKeyId,
                GlobalClearBoardHotKeyId,
                GlobalToggleBoardModeHotKeyId,
                AppLogger.UiInfo,
                AppLogger.UiWarn);
            _mainWindowShellSurface.UpdateWindowStateUi();
            _eveSessionContextSurface.TriggerRefresh("startup", force: false);

            AppLogger.UiInfo("MainWindow source initialized. Clipboard listener attached and title bar theme applied.");
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainWindow_Loaded;
            AppLogger.UiInfo("MainWindow loaded.");
            _mainWindowShellSurface.UpdateWindowMinimumSize();
            Dispatcher.BeginInvoke(new Action(_boardLayoutSurface.FinalizeBoardColumnLayoutInitialization), DispatcherPriority.Loaded);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_isMainWindowInitialized)
            {
                _mainWindowShellSurface.SaveWindowLayoutToSettings("Window closing");
            }

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

            _pilotDetailSurface.SaveCurrentNotesAndTags(PilotBoard?.SelectedItem as PilotBoardRow);
            CancelBoardPopulationRetry();
            RequestOwnedBackgroundWorkStop("MainWindow closed");

            if (_ignoreAllianceListView != null)
            {
                _ignoreAllianceListView.IgnoreListChanged -= IgnoreAllianceListView_IgnoreListChanged;
            }

            _backgroundIntelUpdateService.StatusChanged -= OnIntelUpdateStatusChanged;
            _currentBoardSession.Changed -= CurrentBoardSession_Changed;
            _currentBoardSession.Dispose();
            _clipboardDebounceTimer.Stop();
            _clipboardDebounceTimer.Tick -= ClipboardDebounceTimer_Tick;
            _compactDragHoldTimer.Stop();
            _compactDragHoldTimer.Tick -= CompactDragHoldTimer_Tick;
            _boardColumnLayoutSaveTimer.Stop();
            _boardColumnLayoutSaveTimer.Tick -= BoardColumnLayoutSaveTimer_Tick;
            _boardModeHintTimer.Stop();
            _boardModeHintTimer.Tick -= BoardModeHintTimer_Tick;
            _diagnostics.Dispose();

            var hwnd = new WindowInteropHelper(this).Handle;
            _mainWindowNativeInputController.Detach(
                hwnd,
                _nativeInputApi,
                GlobalResetWindowHotKeyId,
                GlobalClearBoardHotKeyId,
                GlobalToggleBoardModeHotKeyId,
                AppLogger.UiInfo,
                AppLogger.UiWarn);

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

        private void WireDiagnosticsSupportView()
        {
            DiagnosticsSupportViewControl.OpenLogsRequested += (_, _) => _diagnosticsSupportSurface.OpenLogs();
            DiagnosticsSupportViewControl.PackageDiagnosticsRequested += (_, _) => _diagnosticsSupportSurface.PackageDiagnostics();
            DiagnosticsSupportViewControl.OpenDiagnosticsFolderRequested += (_, _) => _diagnosticsSupportSurface.OpenDiagnosticsFolder();
            DiagnosticsSupportViewControl.LogLevelSelectionChanged += (_, _) =>
                _mainWindowSettingsCoordinator.HandleLogLevelChanged(
                    _isApplyingSettings,
                    _appSettings,
                    DiagnosticsSupportViewControl?.LogLevelComboBoxControl);
            DiagnosticsSupportViewControl.RefreshProviderHealthRequested += (_, _) => _diagnosticsSupportSurface.RefreshProviderHealth();
            DiagnosticsSupportViewControl.RefreshCacheStatsRequested += (_, _) => _diagnosticsSupportSurface.RefreshCacheStats();
            DiagnosticsSupportViewControl.ClearExpiredCacheRequested += (_, _) => _diagnosticsSupportSurface.ClearExpiredCache();
            DiagnosticsSupportViewControl.VacuumCacheRequested += (_, _) => _diagnosticsSupportSurface.VacuumCache();
            DiagnosticsSupportViewControl.ClearAllCacheRequested += (_, _) => _diagnosticsSupportSurface.ClearAllCache();
            DiagnosticsSupportViewControl.RebuildKillmailDerivedIntelRequested += async (_, _) => await _intelSupportSurface.RunRebuildKillmailDerivedIntelAsync();
        }

        private void WireIntelSupportView()
        {
            IntelSupportViewControl.SaveMaxKillmailAgeRequested += (_, _) =>
                _mainWindowAppearanceController.SaveMaxKillmailAge(
                    _appSettings,
                    IntelSupportViewControl.MaxKillmailAgeDaysTextBoxControl,
                    IntelSupportViewControl.EffectiveMaxKillmailAgeTextBlock);
            IntelSupportViewControl.UseDefaultMaxKillmailAgeRequested += (_, _) =>
                _mainWindowAppearanceController.ResetMaxKillmailAgeToDefault(
                    _appSettings,
                    IntelSupportViewControl.MaxKillmailAgeDaysTextBoxControl,
                    IntelSupportViewControl.EffectiveMaxKillmailAgeTextBlock);
            IntelSupportViewControl.EnableKillmailDbPullRequested += async (_, _) => await _intelSupportSurface.RunEnableKillmailDbPullAsync();
            IntelSupportViewControl.EnableLiveZkillFeedToggled += async (_, _) =>
                await _intelSupportSurface.HandleLiveFeedToggleAsync(
                    IntelSupportViewControl.EnableLiveZkillFeedCheckBoxControl.IsChecked == true);
            IntelSupportViewControl.BackgroundHistoricalRepairToggled += (_, _) =>
                _mainWindowSettingsCoordinator.HandleBackgroundHistoricalRepairChanged(
                    _isApplyingSettings,
                    _appSettings,
                    IntelSupportViewControl.BackgroundHistoricalRepairEnabledCheckBoxControl.IsChecked == true);
            IntelSupportViewControl.PilotDetailPlacementSelectionChanged += (_, _) =>
                _mainWindowSettingsCoordinator.HandlePilotDetailPlacementPreferenceChanged(
                    _isApplyingSettings,
                    _appSettings,
                    IntelSupportViewControl?.PilotDetailPlacementComboBoxControl);
            IntelSupportViewControl.SaveKillmailPathRequested += (_, _) =>
                _mainWindowAppearanceController.SaveKillmailPath(
                    _appSettings,
                    IntelSupportViewControl.KillmailDataRootPathTextBoxControl,
                    IntelSupportViewControl.KillmailDataPathModeTextBlock,
                    IntelSupportViewControl.EffectiveKillmailDataPathTextBlock);
            IntelSupportViewControl.UseDefaultKillmailPathRequested += (_, _) =>
                _mainWindowAppearanceController.ResetKillmailPathToDefault(
                    _appSettings,
                    IntelSupportViewControl.KillmailDataRootPathTextBoxControl,
                    IntelSupportViewControl.KillmailDataPathModeTextBlock,
                    IntelSupportViewControl.EffectiveKillmailDataPathTextBlock);
            IntelSupportViewControl.RunTodaysFreshnessRequested += async (_, _) => await _intelSupportSurface.RunTodaysFreshnessAsync();
            IntelSupportViewControl.RunHistoricalFreshnessRequested += async (_, _) => await _intelSupportSurface.RunHistoricalFreshnessAsync();
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

        private void ShowBoardGridLinesCheckBox_Changed(object sender, RoutedEventArgs e) => _boardLayoutSurface.HandleShowBoardGridLinesChanged();

        private void BoardTextSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => _boardLayoutSurface.HandleBoardTextSizeChanged();

        private void BoardFontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => _boardLayoutSurface.HandleBoardFontFamilyChanged();

        private void BoardColumnVisibilityCheckBox_Changed(object sender, RoutedEventArgs e) => _boardLayoutSurface.HandleBoardColumnVisibilityChanged();

        private void ShowCorpAllianceCountsCheckBox_Changed(object sender, RoutedEventArgs e) => _boardLayoutSurface.HandleShowCorpAllianceCountsChanged();

        private void ShowAllBoardColumnsButton_Click(object sender, RoutedEventArgs e) => _boardLayoutSurface.HandleShowAllBoardColumns();

        private void ResetBoardColumnsButton_Click(object sender, RoutedEventArgs e) => _boardLayoutSurface.HandleResetBoardColumns();

        private void ResetBoardLayoutButton_Click(object sender, RoutedEventArgs e) => _boardLayoutSurface.HandleResetBoardLayout();

        private void PilotBoard_ColumnReordered(object sender, DataGridColumnEventArgs e) => _boardLayoutSurface.HandlePilotBoardColumnReordered();

        private void PilotBoard_SizeChanged(object sender, SizeChangedEventArgs e) => _boardLayoutSurface.HandlePilotBoardSizeChanged();

        private void BoardColumnWidth_ValueChanged(object? sender, EventArgs e) => _boardLayoutSurface.HandleBoardColumnWidthChanged();

        private void BoardColumnLayoutSaveTimer_Tick(object? sender, EventArgs e) => _boardLayoutSurface.HandleBoardColumnLayoutSaveTimerTick();

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
                    _mainWindowShellSurface.HandleRequestWindowLayoutResetFromHotkey("global Ctrl+Home hotkey");
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
                () => _pilotDetailSurface.SaveCurrentNotesAndTags(PilotBoard?.SelectedItem as PilotBoardRow),
                BuildInitialBoard,
                beginProcessingGeneration: _currentBoardSession.BeginProcessingGeneration,
                getCurrentGeneration: () => _currentBoardSession.CurrentGeneration,
                getCurrentRowCount: () => _currentBoardSession.Count,
                processCurrentRowsAsync: generation => ProcessRowBatchAsync(_currentBoardSession.Snapshot().ToList(), generation),
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
                _currentBoardSession.CurrentGeneration,
                _currentBoardSession.Rows,
                MaxBoardPopulationRetryAttempts,
                UpdateBoardPopulationStatus,
                ScheduleBoardPopulationRetry);
        }

        private void ScheduleBoardPopulationRetry()
        {
            _boardPopulationSurface.ScheduleBoardPopulationRetry(
                _currentBoardSession.Rows,
                Dispatcher,
                UpdateBoardPopulationStatus,
                ProcessRetryPassAsync);
        }

        private Task ProcessRetryPassAsync()
        {
            return _boardPopulationSurface.ProcessRetryPassAsync(
                _currentBoardSession.Rows,
                () => _backgroundIntelUpdateService.BeginForegroundPriority(),
                (rows, generation) => ProcessRowBatchAsync(rows.ToList(), generation),
                () => _currentBoardSession.CurrentGeneration,
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

        private Task ProcessSingleRowAsync(PilotBoardRow row, SemaphoreSlim semaphore, int generation)
        {
            return _boardRowProcessingCoordinator.ProcessSingleRowAsync(row, semaphore, generation);
        }

        private void HandleRowProcessorMarker(BoardRowProcessMarkerKind markerKind, int generation, string message)
        {
            _boardPopulationTimingMarkerTracker.HandleMarker(markerKind, generation, message);
        }

        private void RemoveIgnoredAllianceRowFromCurrentBoard(PilotBoardRow row)
        {
            if (row == null)
            {
                return;
            }

            var removed = _currentBoardSession.RemoveRow(row);
            if (!removed)
            {
                return;
            }

            if (ReferenceEquals(PilotBoard.SelectedItem, row))
            {
                PilotBoard.SelectedItem = null;
                _pilotDetailSurface.HideDetailPane();
                _pilotDetailSurface.CloseActiveDetailWindow();
            }

            AppLogger.UiInfo($"Ignored alliance filter removed a resolved row from current board. character='{row.CharacterName}' allianceId='{row.AllianceId}'");
            RecomputeCorpAllianceCounts();
        }

        private void ApplyIgnoredAllianceRowsToCurrentBoard()
        {
            var selectedRow = PilotBoard.SelectedItem as PilotBoardRow;
            var applyResult = _ignoreAllianceBoardController.ApplyToCurrentRows(_currentBoardSession.Rows, selectedRow);

            if (applyResult.RemovedCount == 0)
            {
                return;
            }

            _currentBoardSession.RemoveRows(applyResult.RemovedRows);
            RecomputeCorpAllianceCounts();

            if (applyResult.SelectedRowRemoved)
            {
                PilotBoard.SelectedItem = null;
                _pilotDetailSurface.HideDetailPane();
                _pilotDetailSurface.CloseActiveDetailWindow();
            }
            else
            {
                _pilotDetailSurface.UpdateIgnoreAllianceButtonState(PilotBoard.SelectedItem as PilotBoardRow);
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
            foreach (var row in _currentBoardSession.Rows)
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
            _boardAffiliationCountService.ApplyCounts(
                _currentBoardSession.Rows,
                _appSettings.ShowCorpAllianceCounts);

            _analysisTabPresenter.UpdateBoardSummary(_currentBoardSession.Rows);
            _analysisTabPresenter.UpdateAnalysisTab(_currentBoardSession.Rows);
        }

        private void PilotBoard_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = PilotBoard?.SelectedItem;
            _pilotDetailSurface.SaveCurrentNotesAndTags(selectedItem as PilotBoardRow);
            if (selectedItem is PilotBoardRow selectedRow)
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
            if (_boardSortController.TryHandleSorting(
                PilotBoard,
                e.Column,
                _currentBoardSession.Snapshot(),
                PilotBoard.SelectedItem as PilotBoardRow,
                _currentBoardSession.ReorderRows,
                row => PilotBoard.SelectedItem = row,
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
            _pilotDetailSurface.OpenDetailsWindow(selectedRow);
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
            var selectedRow = _pilotDetailSurface.GetSelectedOrDisplayedDetailRow(
                PilotBoard?.SelectedItem as PilotBoardRow,
                _currentBoardSession.Rows);
            if (selectedRow == null)
            {
                AppLogger.UiWarn("Watch requested with no selected or displayed detail row.");
                return;
            }
            _pilotDetailSurface.ToggleWatchForRow(selectedRow);
        }

        private void CloseDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            _pilotDetailSurface.SaveCurrentNotesAndTags(PilotBoard?.SelectedItem as PilotBoardRow);
            AppLogger.UiInfo("Detail pane close requested.");
            if (PilotBoard != null)
            {
                PilotBoard.SelectedItem = null;
            }
            _pilotDetailSurface.CloseActiveDetailWindow();
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
                () => _currentBoardSession.Count,
                () => _pilotDetailSurface.SaveCurrentNotesAndTags(PilotBoard?.SelectedItem as PilotBoardRow),
                CancelBoardPopulationRetry,
                () => ResetEntryAndRetryTracking(),
                ResetManualBoardSort,
                () => _currentBoardSession.ClearAndInvalidate(),
                RecomputeCorpAllianceCounts,
                _pilotDetailSurface.CloseActiveDetailWindow,
                static () => { },
                UpdateLastRefreshed,
                UpdateBoardPopulationStatus);
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

        private async void ManualUpdateCheckButton_Click(object sender, RoutedEventArgs e)
        {
            if (_manualUpdateCheckController == null)
            {
                return;
            }

            await _manualUpdateCheckController.RunAsync();
        }

        public List<long> GetVisibleCharacterIdsForBackgroundHistoricalRepair()
        {
            return _intelSupportSurface.GetVisibleCharacterIdsForBackgroundHistoricalRepair();
        }

        private Task RefreshCurrentBoardRowsFromLocalIntelAsync(string reason)
        {
            if (_currentBoardSession.Count > 0)
            {
                AppLogger.UiInfo($"Refreshing current Grill rows from local intel. reason='{reason}' rowCount={_currentBoardSession.Count}");
            }

            return _boardRowProcessingCoordinator.RefreshCurrentRowsFromLocalIntelAsync(
                CancelBoardPopulationRetry,
                () => UpdateBoardPopulationStatus("Refreshing Grill from local intel", BoardPopulationStatusKind.Neutral),
                (rows, generation) => ProcessRowBatchAsync(rows.ToList(), generation),
                FinalizeBoardPopulationPass,
                UpdateLastRefreshed);
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
        private void IgnoreAllianceListView_IgnoreListChanged(object? sender, EventArgs e)
        {
            ApplyIgnoredAllianceRowsToCurrentBoard();
            RecomputeCorpAllianceCounts();
        }
        private void IgnoreAllianceButton_Click(object sender, RoutedEventArgs e)
        {
            _pilotDetailSurface.TryIgnoreAllianceForSelectedOrDisplayedRow(
                PilotBoard?.SelectedItem as PilotBoardRow,
                _currentBoardSession.Rows);
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
        private void ApplyCurrentBoardOrdering()
        {
            _boardSortController.ApplyCurrentBoardOrdering(
                _currentBoardSession.Snapshot(),
                PilotBoard?.SelectedItem as PilotBoardRow,
                _currentBoardSession.ReorderRows,
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

        private void UpdateLastRefreshed()
        {
            LastRefreshedText.Text = $"Last Refreshed: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        }
        private void CurrentBoardSession_Changed(object? sender, CurrentBoardSessionChangedEventArgs e)
        {
            _analysisTabPresenter.UpdateBoardSummary(_currentBoardSession.Rows);
            _analysisTabPresenter.UpdateAnalysisTab(_currentBoardSession.Rows);
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
