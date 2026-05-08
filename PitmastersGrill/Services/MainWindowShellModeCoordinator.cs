using PitmastersGrill.Models;
using System.Windows;

namespace PitmastersGrill.Services
{
    public sealed record MainWindowShellModeTransition(
        bool CompactMode,
        bool? PreviousCompactMode,
        bool ShouldSaveOutgoingLayout,
        WindowLayoutMode OutgoingLayoutMode,
        bool ShouldRestoreIncomingLayout,
        WindowLayoutMode IncomingLayoutMode,
        bool ShouldSelectBoardTab,
        int TargetSelectedTabIndex,
        bool ShouldCloseActiveDetailWindow,
        bool ShouldPersistCompactModeSetting,
        bool ShouldLogDisplayModeChanged,
        bool ShouldShowBoardModeHint,
        bool ShouldHideBoardModeHint,
        Thickness MainContentMargin,
        Thickness MainTabBorderThickness,
        Thickness MainTabMargin,
        Thickness BoardStatusFooterPadding,
        Visibility TopCommandVisibility,
        Visibility BoardStatusFooterVisibility);

    public sealed record MainWindowMinimumSize(double MinWidth, double MinHeight);

    public sealed record MaximizeRestoreWindowButtonState(string Content, string ToolTip);

    public sealed class MainWindowShellModeCoordinator
    {
        public MainWindowShellModeTransition BuildCompactModeTransition(
            bool compactMode,
            bool? previousCompactMode,
            bool isApplyingSettings,
            bool isRestoringWindowLayout,
            bool currentSettingCompactEnabled,
            int currentSelectedTabIndex)
        {
            var displayModeChanged = !isApplyingSettings &&
                                     !isRestoringWindowLayout &&
                                     previousCompactMode.HasValue &&
                                     previousCompactMode.Value != compactMode;
            var shouldLogDisplayModeChanged = !isApplyingSettings &&
                                              (!previousCompactMode.HasValue || previousCompactMode.Value != compactMode);
            var targetSelectedTabIndex = compactMode ? 1 : currentSelectedTabIndex;

            return new MainWindowShellModeTransition(
                CompactMode: compactMode,
                PreviousCompactMode: previousCompactMode,
                ShouldSaveOutgoingLayout: displayModeChanged && previousCompactMode.HasValue,
                OutgoingLayoutMode: previousCompactMode == true ? WindowLayoutMode.Board : WindowLayoutMode.Normal,
                ShouldRestoreIncomingLayout: displayModeChanged,
                IncomingLayoutMode: compactMode ? WindowLayoutMode.Board : WindowLayoutMode.Normal,
                ShouldSelectBoardTab: compactMode,
                TargetSelectedTabIndex: targetSelectedTabIndex,
                ShouldCloseActiveDetailWindow: compactMode,
                ShouldPersistCompactModeSetting: !isApplyingSettings && currentSettingCompactEnabled != compactMode,
                ShouldLogDisplayModeChanged: shouldLogDisplayModeChanged,
                ShouldShowBoardModeHint: compactMode && shouldLogDisplayModeChanged,
                ShouldHideBoardModeHint: !compactMode,
                MainContentMargin: compactMode ? new Thickness(1) : new Thickness(12),
                MainTabBorderThickness: compactMode ? new Thickness(0) : new Thickness(1),
                MainTabMargin: new Thickness(0),
                BoardStatusFooterPadding: new Thickness(8, 5, 8, 5),
                TopCommandVisibility: compactMode ? Visibility.Collapsed : Visibility.Visible,
                BoardStatusFooterVisibility: BuildBoardStatusFooterVisibility(compactMode, targetSelectedTabIndex));
        }

        public Visibility BuildBoardStatusFooterVisibility(bool compactMode, int selectedTabIndex)
        {
            return compactMode || selectedTabIndex == 0
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        public MainWindowMinimumSize BuildMinimumWindowSize(
            bool compactMode,
            double normalModeMinimumWindowWidth,
            double normalModeMinimumWindowHeight,
            double boardModeMinimumWindowWidth,
            double contentMarginHeight,
            double commandStripHeight,
            double tabHeaderHeight,
            double boardColumnHeaderHeight,
            double boardRowHeight,
            double boardFontSize,
            double boardModeFallbackCommandStripHeight,
            double boardModeFallbackTabHeaderHeight,
            double boardModeFallbackColumnHeaderHeight,
            double boardModeFallbackFooterPaddingHeight,
            double boardModeFallbackRowVerticalPadding)
        {
            if (!compactMode)
            {
                return new MainWindowMinimumSize(normalModeMinimumWindowWidth, normalModeMinimumWindowHeight);
            }

            var effectiveCommandStripHeight = Math.Max(commandStripHeight, boardModeFallbackCommandStripHeight);
            var effectiveTabHeaderHeight = Math.Max(tabHeaderHeight, boardModeFallbackTabHeaderHeight);
            var effectiveBoardColumnHeaderHeight = Math.Max(boardColumnHeaderHeight, boardModeFallbackColumnHeaderHeight);
            var effectiveBoardRowHeight = Math.Max(boardRowHeight, Math.Ceiling(boardFontSize + boardModeFallbackRowVerticalPadding));

            return new MainWindowMinimumSize(
                boardModeMinimumWindowWidth,
                Math.Ceiling(
                    contentMarginHeight +
                    effectiveCommandStripHeight +
                    effectiveTabHeaderHeight +
                    effectiveBoardColumnHeaderHeight +
                    effectiveBoardRowHeight +
                    boardModeFallbackFooterPaddingHeight));
        }

        public MaximizeRestoreWindowButtonState BuildMaximizeRestoreWindowButtonState(WindowState windowState)
        {
            var isMaximized = windowState == WindowState.Maximized;
            return new MaximizeRestoreWindowButtonState(
                isMaximized ? "O" : "[]",
                isMaximized ? "Restore PMG" : "Maximize PMG");
        }
    }
}
