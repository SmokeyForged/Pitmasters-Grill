using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PitmastersGrill.Services
{
    public sealed class MainWindowShellSurface
    {
        private readonly MainWindowShellModeCoordinator _mainWindowShellModeCoordinator;
        private readonly MainWindowInteropController _mainWindowInteropController;
        private readonly WindowLayoutSurface _windowLayoutSurface;
        private readonly ToggleButton _compactModeToggleButton;
        private readonly Grid _mainContentGrid;
        private readonly Grid _topCommandGrid;
        private readonly TabControl _mainTabControl;
        private readonly Border _boardModeHintOverlay;
        private readonly Border _boardStatusFooter;
        private readonly Button _maximizeRestoreWindowButton;
        private readonly DataGrid _pilotBoard;
        private readonly DispatcherTimer _boardModeHintTimer;
        private readonly Func<AppSettings> _getSettings;
        private readonly Action<AppSettings> _saveSettings;
        private readonly Func<bool> _isApplyingSettings;
        private readonly Func<WindowState> _getWindowState;
        private readonly Action<WindowState> _setWindowState;
        private readonly Func<Rect> _getRestoreBounds;
        private readonly Func<Rect> _getCurrentBounds;
        private readonly Action<Rect> _applyWindowBounds;
        private readonly Action<double, double> _setMinimumWindowSize;
        private readonly Action _closeActiveDetailWindow;
        private readonly Action _updateBoardSummaryBanner;
        private readonly Action _updateAnalysisTab;
        private readonly Action<string, bool> _triggerSessionContextRefresh;
        private readonly Func<DateTime, bool> _isSessionContextStale;
        private readonly Action<bool> _scheduleFitVisibleBoardColumnsToViewport;
        private readonly Action _invalidateLastProcessedClipboard;
        private readonly Func<Task> _processClipboardIfValidAsync;
        private readonly Action<string> _clearBoard;
        private readonly Action<string> _requestApplicationShutdown;
        private readonly Func<string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult> _showDialog;
        private readonly double _normalModeMinimumWindowWidth;
        private readonly double _normalModeMinimumWindowHeight;
        private readonly double _boardModeMinimumWindowWidth;
        private readonly double _boardModeFallbackCommandStripHeight;
        private readonly double _boardModeFallbackTabHeaderHeight;
        private readonly double _boardModeFallbackColumnHeaderHeight;
        private readonly double _boardModeFallbackFooterPaddingHeight;
        private readonly double _boardModeFallbackRowVerticalPadding;
        private readonly double _minimumSavedWindowWidth;
        private readonly double _minimumSavedWindowHeight;
        private readonly double _minimumVisibleWindowEdge;
        private readonly double _defaultWindowWidth;
        private readonly double _defaultWindowHeight;
        private readonly int _tripleEscapeWindowMilliseconds;

        private bool? _lastAppliedCompactMode;
        private DateTime _lastEscapeTapUtc = DateTime.MinValue;
        private int _escapeTapCount;

        public MainWindowShellSurface(
            MainWindowShellModeCoordinator mainWindowShellModeCoordinator,
            MainWindowInteropController mainWindowInteropController,
            WindowLayoutSurface windowLayoutSurface,
            ToggleButton compactModeToggleButton,
            Grid mainContentGrid,
            Grid topCommandGrid,
            TabControl mainTabControl,
            Border boardModeHintOverlay,
            Border boardStatusFooter,
            Button maximizeRestoreWindowButton,
            DataGrid pilotBoard,
            DispatcherTimer boardModeHintTimer,
            Func<AppSettings> getSettings,
            Action<AppSettings> saveSettings,
            Func<bool> isApplyingSettings,
            Func<WindowState> getWindowState,
            Action<WindowState> setWindowState,
            Func<Rect> getRestoreBounds,
            Func<Rect> getCurrentBounds,
            Action<Rect> applyWindowBounds,
            Action<double, double> setMinimumWindowSize,
            Action closeActiveDetailWindow,
            Action updateBoardSummaryBanner,
            Action updateAnalysisTab,
            Action<string, bool> triggerSessionContextRefresh,
            Func<DateTime, bool> isSessionContextStale,
            Action<bool> scheduleFitVisibleBoardColumnsToViewport,
            Action invalidateLastProcessedClipboard,
            Func<Task> processClipboardIfValidAsync,
            Action<string> clearBoard,
            Action<string> requestApplicationShutdown,
            Func<string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult> showDialog,
            double normalModeMinimumWindowWidth,
            double normalModeMinimumWindowHeight,
            double boardModeMinimumWindowWidth,
            double boardModeFallbackCommandStripHeight,
            double boardModeFallbackTabHeaderHeight,
            double boardModeFallbackColumnHeaderHeight,
            double boardModeFallbackFooterPaddingHeight,
            double boardModeFallbackRowVerticalPadding,
            double minimumSavedWindowWidth,
            double minimumSavedWindowHeight,
            double minimumVisibleWindowEdge,
            double defaultWindowWidth,
            double defaultWindowHeight,
            int tripleEscapeWindowMilliseconds)
        {
            _mainWindowShellModeCoordinator = mainWindowShellModeCoordinator ?? throw new ArgumentNullException(nameof(mainWindowShellModeCoordinator));
            _mainWindowInteropController = mainWindowInteropController ?? throw new ArgumentNullException(nameof(mainWindowInteropController));
            _windowLayoutSurface = windowLayoutSurface ?? throw new ArgumentNullException(nameof(windowLayoutSurface));
            _compactModeToggleButton = compactModeToggleButton ?? throw new ArgumentNullException(nameof(compactModeToggleButton));
            _mainContentGrid = mainContentGrid ?? throw new ArgumentNullException(nameof(mainContentGrid));
            _topCommandGrid = topCommandGrid ?? throw new ArgumentNullException(nameof(topCommandGrid));
            _mainTabControl = mainTabControl ?? throw new ArgumentNullException(nameof(mainTabControl));
            _boardModeHintOverlay = boardModeHintOverlay ?? throw new ArgumentNullException(nameof(boardModeHintOverlay));
            _boardStatusFooter = boardStatusFooter ?? throw new ArgumentNullException(nameof(boardStatusFooter));
            _maximizeRestoreWindowButton = maximizeRestoreWindowButton ?? throw new ArgumentNullException(nameof(maximizeRestoreWindowButton));
            _pilotBoard = pilotBoard ?? throw new ArgumentNullException(nameof(pilotBoard));
            _boardModeHintTimer = boardModeHintTimer ?? throw new ArgumentNullException(nameof(boardModeHintTimer));
            _getSettings = getSettings ?? throw new ArgumentNullException(nameof(getSettings));
            _saveSettings = saveSettings ?? throw new ArgumentNullException(nameof(saveSettings));
            _isApplyingSettings = isApplyingSettings ?? throw new ArgumentNullException(nameof(isApplyingSettings));
            _getWindowState = getWindowState ?? throw new ArgumentNullException(nameof(getWindowState));
            _setWindowState = setWindowState ?? throw new ArgumentNullException(nameof(setWindowState));
            _getRestoreBounds = getRestoreBounds ?? throw new ArgumentNullException(nameof(getRestoreBounds));
            _getCurrentBounds = getCurrentBounds ?? throw new ArgumentNullException(nameof(getCurrentBounds));
            _applyWindowBounds = applyWindowBounds ?? throw new ArgumentNullException(nameof(applyWindowBounds));
            _setMinimumWindowSize = setMinimumWindowSize ?? throw new ArgumentNullException(nameof(setMinimumWindowSize));
            _closeActiveDetailWindow = closeActiveDetailWindow ?? throw new ArgumentNullException(nameof(closeActiveDetailWindow));
            _updateBoardSummaryBanner = updateBoardSummaryBanner ?? throw new ArgumentNullException(nameof(updateBoardSummaryBanner));
            _updateAnalysisTab = updateAnalysisTab ?? throw new ArgumentNullException(nameof(updateAnalysisTab));
            _triggerSessionContextRefresh = triggerSessionContextRefresh ?? throw new ArgumentNullException(nameof(triggerSessionContextRefresh));
            _isSessionContextStale = isSessionContextStale ?? throw new ArgumentNullException(nameof(isSessionContextStale));
            _scheduleFitVisibleBoardColumnsToViewport = scheduleFitVisibleBoardColumnsToViewport ?? throw new ArgumentNullException(nameof(scheduleFitVisibleBoardColumnsToViewport));
            _invalidateLastProcessedClipboard = invalidateLastProcessedClipboard ?? throw new ArgumentNullException(nameof(invalidateLastProcessedClipboard));
            _processClipboardIfValidAsync = processClipboardIfValidAsync ?? throw new ArgumentNullException(nameof(processClipboardIfValidAsync));
            _clearBoard = clearBoard ?? throw new ArgumentNullException(nameof(clearBoard));
            _requestApplicationShutdown = requestApplicationShutdown ?? throw new ArgumentNullException(nameof(requestApplicationShutdown));
            _showDialog = showDialog ?? throw new ArgumentNullException(nameof(showDialog));
            _normalModeMinimumWindowWidth = normalModeMinimumWindowWidth;
            _normalModeMinimumWindowHeight = normalModeMinimumWindowHeight;
            _boardModeMinimumWindowWidth = boardModeMinimumWindowWidth;
            _boardModeFallbackCommandStripHeight = boardModeFallbackCommandStripHeight;
            _boardModeFallbackTabHeaderHeight = boardModeFallbackTabHeaderHeight;
            _boardModeFallbackColumnHeaderHeight = boardModeFallbackColumnHeaderHeight;
            _boardModeFallbackFooterPaddingHeight = boardModeFallbackFooterPaddingHeight;
            _boardModeFallbackRowVerticalPadding = boardModeFallbackRowVerticalPadding;
            _minimumSavedWindowWidth = minimumSavedWindowWidth;
            _minimumSavedWindowHeight = minimumSavedWindowHeight;
            _minimumVisibleWindowEdge = minimumVisibleWindowEdge;
            _defaultWindowWidth = defaultWindowWidth;
            _defaultWindowHeight = defaultWindowHeight;
            _tripleEscapeWindowMilliseconds = tripleEscapeWindowMilliseconds;
        }

        public void ApplyCompactModeUi()
        {
            var settings = _getSettings();
            var transition = _mainWindowShellModeCoordinator.BuildCompactModeTransition(
                _compactModeToggleButton.IsChecked == true,
                _lastAppliedCompactMode,
                _isApplyingSettings(),
                _windowLayoutSurface.IsRestoringWindowLayout,
                settings.CompactModeEnabled,
                _mainTabControl.SelectedIndex);

            if (transition.ShouldSaveOutgoingLayout)
            {
                SaveWindowLayoutToSettings(
                    $"Before display mode change to {(transition.CompactMode ? "Board" : "Normal")}",
                    transition.OutgoingLayoutMode);
            }

            _lastAppliedCompactMode = transition.CompactMode;

            if (transition.ShouldSelectBoardTab)
            {
                _mainTabControl.SelectedIndex = transition.TargetSelectedTabIndex;
            }

            if (transition.ShouldCloseActiveDetailWindow)
            {
                _closeActiveDetailWindow();
            }

            _topCommandGrid.Visibility = transition.TopCommandVisibility;
            _topCommandGrid.Margin = new Thickness(0, 0, 0, 6);
            _boardStatusFooter.Padding = transition.BoardStatusFooterPadding;
            _mainContentGrid.Margin = transition.MainContentMargin;
            _mainTabControl.BorderThickness = transition.MainTabBorderThickness;
            _mainTabControl.Margin = transition.MainTabMargin;

            if (transition.ShouldPersistCompactModeSetting)
            {
                settings.CompactModeEnabled = transition.CompactMode;
                _saveSettings(settings);
            }

            if (transition.ShouldLogDisplayModeChanged)
            {
                AppLogger.UiInfo($"Display mode changed.\nboardMode={transition.CompactMode}");
            }

            if (transition.ShouldShowBoardModeHint)
            {
                ShowBoardModeHint();
            }
            else if (transition.ShouldHideBoardModeHint)
            {
                HideBoardModeHint();
            }

            UpdateWindowMinimumSize();

            if (transition.ShouldRestoreIncomingLayout)
            {
                RestoreWindowLayoutFromSettings(transition.IncomingLayoutMode);
            }

            _boardStatusFooter.Visibility = transition.BoardStatusFooterVisibility;
            _updateBoardSummaryBanner();
            _updateAnalysisTab();
        }

        public void HandleMainTabSelectionChanged()
        {
            UpdateBoardFooterVisibility();

            if (_mainTabControl.SelectedIndex == 0)
            {
                _triggerSessionContextRefresh(
                    "analysis tab selection",
                    _isSessionContextStale(DateTime.UtcNow));
            }
            else if (_mainTabControl.SelectedIndex == 1)
            {
                _scheduleFitVisibleBoardColumnsToViewport(true);
            }
        }

        public void UpdateBoardFooterVisibility()
        {
            _boardStatusFooter.Visibility = _mainWindowShellModeCoordinator.BuildBoardStatusFooterVisibility(
                _compactModeToggleButton.IsChecked == true,
                _mainTabControl.SelectedIndex);
        }

        public void ToggleCompactModeFromHotkey()
        {
            _compactModeToggleButton.IsChecked = _compactModeToggleButton.IsChecked != true;
            ApplyCompactModeUi();
        }

        public void ShowBoardModeHint()
        {
            _boardModeHintOverlay.Visibility = Visibility.Visible;
            _boardModeHintTimer.Stop();
            _boardModeHintTimer.Start();
        }

        public void HideBoardModeHint()
        {
            _boardModeHintTimer.Stop();
            _boardModeHintOverlay.Visibility = Visibility.Collapsed;
        }

        public void HandleBoardModeHintTimerTick()
        {
            HideBoardModeHint();
        }

        public void UpdateWindowMinimumSize()
        {
            var minimumSize = _mainWindowShellModeCoordinator.BuildMinimumWindowSize(
                _compactModeToggleButton.IsChecked == true,
                _normalModeMinimumWindowWidth,
                _normalModeMinimumWindowHeight,
                _boardModeMinimumWindowWidth,
                _mainContentGrid.Margin.Top + _mainContentGrid.Margin.Bottom,
                _topCommandGrid.ActualHeight,
                GetTabHeaderHeight(),
                GetBoardColumnHeaderHeight(),
                GetBoardRowHeight(),
                _pilotBoard.FontSize,
                _boardModeFallbackCommandStripHeight,
                _boardModeFallbackTabHeaderHeight,
                _boardModeFallbackColumnHeaderHeight,
                _boardModeFallbackFooterPaddingHeight,
                _boardModeFallbackRowVerticalPadding);

            _setMinimumWindowSize(minimumSize.MinWidth, minimumSize.MinHeight);
        }

        public void HandleMinimizeWindow()
        {
            _setWindowState(WindowState.Minimized);
        }

        public void HandleMaximizeRestoreWindow()
        {
            _setWindowState(_getWindowState() == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized);
        }

        public void HandleCloseWindow()
        {
            _requestApplicationShutdown("Window close button");
        }

        public void HandleWindowStateChanged()
        {
            _windowLayoutSurface.HandleWindowStateChanged(
                _getWindowState(),
                _getRestoreBounds(),
                _getCurrentBounds());
            UpdateWindowStateUi();
        }

        public void HandleWindowLocationChanged()
        {
            TrackCurrentNormalWindowBounds();
        }

        public void HandleWindowSizeChanged()
        {
            TrackCurrentNormalWindowBounds();
            UpdateWindowMinimumSize();
        }

        public void HandleResetWindowLayoutButton()
        {
            ResetWindowLayout(showConfirmation: true, reason: "Reset window layout button");
        }

        public void HandleRequestWindowLayoutResetFromHotkey(string source)
        {
            AppLogger.UiInfo($"Window layout reset requested from {source}.");
            ResetWindowLayout(showConfirmation: false, reason: source);
        }

        public bool HandlePreviewKey(Key key, bool controlModifierPressed, bool isTextEditing)
        {
            var action = _mainWindowInteropController.RoutePreviewKey(
                key,
                controlModifierPressed,
                isTextEditing);

            switch (action)
            {
                case MainWindowKeyboardAction.RequestWindowLayoutReset:
                    HandleRequestWindowLayoutResetFromHotkey("Ctrl+Home hotkey");
                    return true;

                case MainWindowKeyboardAction.ToggleCompactMode:
                    ToggleCompactModeFromHotkey();
                    return true;

                case MainWindowKeyboardAction.ClearBoard:
                    _clearBoard("Delete hotkey");
                    return true;

                case MainWindowKeyboardAction.RefreshClipboard:
                    AppLogger.UiInfo("Manual clipboard refresh requested from Home hotkey.");
                    _invalidateLastProcessedClipboard();
                    _ = _processClipboardIfValidAsync();
                    return true;

                case MainWindowKeyboardAction.HandleEscape:
                    HandleEscapeHotkey();
                    return true;

                default:
                    return false;
            }
        }

        public void RestoreWindowLayoutFromSettings()
        {
            RestoreWindowLayoutFromSettings(GetCurrentWindowLayoutMode());
        }

        public void RestoreWindowLayoutFromSettings(WindowLayoutMode mode)
        {
            _windowLayoutSurface.RestoreFromSettings(
                _getSettings(),
                mode,
                _normalModeMinimumWindowWidth,
                _normalModeMinimumWindowHeight,
                _minimumSavedWindowWidth,
                _minimumSavedWindowHeight,
                _minimumVisibleWindowEdge,
                _defaultWindowWidth,
                _defaultWindowHeight,
                _applyWindowBounds,
                _setWindowState,
                AppLogger.UiInfo);
        }

        public void SaveWindowLayoutToSettings(string reason)
        {
            SaveWindowLayoutToSettings(reason, GetCurrentWindowLayoutMode());
        }

        public void SaveWindowLayoutToSettings(string reason, WindowLayoutMode mode)
        {
            _windowLayoutSurface.SaveToSettings(
                _getSettings(),
                reason,
                mode,
                _getWindowState(),
                _getRestoreBounds(),
                _getCurrentBounds(),
                _normalModeMinimumWindowWidth,
                _normalModeMinimumWindowHeight,
                _minimumSavedWindowWidth,
                _minimumSavedWindowHeight,
                _minimumVisibleWindowEdge,
                AppLogger.UiInfo,
                AppLogger.UiWarn);
        }

        private WindowLayoutMode GetCurrentWindowLayoutMode() => _compactModeToggleButton.IsChecked == true
            ? WindowLayoutMode.Board
            : WindowLayoutMode.Normal;

        public void UpdateWindowStateUi()
        {
            var buttonState = _mainWindowShellModeCoordinator.BuildMaximizeRestoreWindowButtonState(_getWindowState());
            _maximizeRestoreWindowButton.Content = buttonState.Content;
            _maximizeRestoreWindowButton.ToolTip = buttonState.ToolTip;
        }

        private void TrackCurrentNormalWindowBounds()
        {
            _windowLayoutSurface.TrackCurrentNormalWindowBounds(
                _getWindowState(),
                _getCurrentBounds());
        }

        private void ResetWindowLayout(bool showConfirmation, string reason)
        {
            _windowLayoutSurface.ClearSavedLayouts(_getSettings());

            var resetBounds = _windowLayoutSurface.GetDefaultWindowBounds(
                _minimumSavedWindowWidth,
                _minimumSavedWindowHeight,
                _defaultWindowWidth,
                _defaultWindowHeight);

            _setWindowState(WindowState.Normal);
            _applyWindowBounds(resetBounds);
            _windowLayoutSurface.HandleWindowStateChanged(WindowState.Normal, Rect.Empty, resetBounds);

            SaveWindowLayoutToSettings(reason);

            AppLogger.UiInfo(
                $"Window layout reset. reason='{reason}' left={resetBounds.Left:0.##} top={resetBounds.Top:0.##} width={resetBounds.Width:0.##} height={resetBounds.Height:0.##}");

            if (showConfirmation)
            {
                _showDialog(
                    "Window layout reset to a safe default position and size.",
                    "PMG Window Layout",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void HandleEscapeHotkey()
        {
            var result = _mainWindowInteropController.HandleEscapeTap(
                DateTime.UtcNow,
                _lastEscapeTapUtc,
                _escapeTapCount,
                _tripleEscapeWindowMilliseconds);

            _lastEscapeTapUtc = result.LastEscapeTapUtc;
            _escapeTapCount = result.EscapeTapCount;

            if (result.ShouldRequestShutdown)
            {
                _requestApplicationShutdown("Triple Escape hotkey");
            }
        }

        private double GetTabHeaderHeight()
        {
            return FindVisualDescendant<TabPanel>(_mainTabControl)?.ActualHeight ?? 0;
        }

        private double GetBoardColumnHeaderHeight()
        {
            return FindVisualDescendant<DataGridColumnHeadersPresenter>(_pilotBoard)?.ActualHeight ?? 0;
        }

        private double GetBoardRowHeight()
        {
            for (var index = 0; index < Math.Min(_pilotBoard.Items.Count, 3); index++)
            {
                if (_pilotBoard.ItemContainerGenerator.ContainerFromIndex(index) is DataGridRow row &&
                    row.ActualHeight > 0)
                {
                    return row.ActualHeight;
                }
            }

            return 0;
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
    }
}
