using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System.Windows;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class MainWindowShellModeCoordinatorTests
    {
        [Fact]
        public void BuildCompactModeTransition_WhenSwitchingToBoardMode_RequestsSaveRestoreHintAndPersistence()
        {
            var coordinator = new MainWindowShellModeCoordinator();

            var result = coordinator.BuildCompactModeTransition(
                compactMode: true,
                previousCompactMode: false,
                isApplyingSettings: false,
                isRestoringWindowLayout: false,
                currentSettingCompactEnabled: false,
                currentSelectedTabIndex: 0);

            Assert.True(result.ShouldSaveOutgoingLayout);
            Assert.Equal(WindowLayoutMode.Normal, result.OutgoingLayoutMode);
            Assert.True(result.ShouldRestoreIncomingLayout);
            Assert.Equal(WindowLayoutMode.Board, result.IncomingLayoutMode);
            Assert.True(result.ShouldSelectBoardTab);
            Assert.Equal(1, result.TargetSelectedTabIndex);
            Assert.True(result.ShouldCloseActiveDetailWindow);
            Assert.True(result.ShouldPersistCompactModeSetting);
            Assert.True(result.ShouldLogDisplayModeChanged);
            Assert.True(result.ShouldShowBoardModeHint);
            Assert.False(result.ShouldHideBoardModeHint);
            Assert.Equal(Visibility.Collapsed, result.TopCommandVisibility);
            Assert.Equal(Visibility.Collapsed, result.BoardStatusFooterVisibility);
            Assert.Equal(new Thickness(1), result.MainContentMargin);
        }

        [Fact]
        public void BuildCompactModeTransition_WhenApplyingSettings_DoesNotPersistSaveOrLog()
        {
            var coordinator = new MainWindowShellModeCoordinator();

            var result = coordinator.BuildCompactModeTransition(
                compactMode: false,
                previousCompactMode: true,
                isApplyingSettings: true,
                isRestoringWindowLayout: false,
                currentSettingCompactEnabled: true,
                currentSelectedTabIndex: 1);

            Assert.False(result.ShouldSaveOutgoingLayout);
            Assert.False(result.ShouldRestoreIncomingLayout);
            Assert.False(result.ShouldPersistCompactModeSetting);
            Assert.False(result.ShouldLogDisplayModeChanged);
            Assert.False(result.ShouldShowBoardModeHint);
            Assert.True(result.ShouldHideBoardModeHint);
            Assert.Equal(Visibility.Visible, result.TopCommandVisibility);
            Assert.Equal(Visibility.Visible, result.BoardStatusFooterVisibility);
        }

        [Theory]
        [InlineData(true, 1, Visibility.Collapsed)]
        [InlineData(false, 0, Visibility.Collapsed)]
        [InlineData(false, 2, Visibility.Visible)]
        public void BuildBoardStatusFooterVisibility_MatchesBoardAndAnalysisRules(
            bool compactMode,
            int selectedTabIndex,
            Visibility expectedVisibility)
        {
            var coordinator = new MainWindowShellModeCoordinator();

            var result = coordinator.BuildBoardStatusFooterVisibility(compactMode, selectedTabIndex);

            Assert.Equal(expectedVisibility, result);
        }

        [Fact]
        public void BuildMinimumWindowSize_ForBoardMode_UsesMeasurementsAndFallbacks()
        {
            var coordinator = new MainWindowShellModeCoordinator();

            var result = coordinator.BuildMinimumWindowSize(
                compactMode: true,
                normalModeMinimumWindowWidth: 420,
                normalModeMinimumWindowHeight: 300,
                boardModeMinimumWindowWidth: 420,
                contentMarginHeight: 2,
                commandStripHeight: 0,
                tabHeaderHeight: 10,
                boardColumnHeaderHeight: 0,
                boardRowHeight: 0,
                boardFontSize: 12,
                boardModeFallbackCommandStripHeight: 38,
                boardModeFallbackTabHeaderHeight: 32,
                boardModeFallbackColumnHeaderHeight: 28,
                boardModeFallbackFooterPaddingHeight: 18,
                boardModeFallbackRowVerticalPadding: 16);

            Assert.Equal(420, result.MinWidth);
            Assert.Equal(146, result.MinHeight);
        }

        [Theory]
        [InlineData(WindowState.Normal, "[]", "Maximize PMG")]
        [InlineData(WindowState.Maximized, "O", "Restore PMG")]
        public void BuildMaximizeRestoreWindowButtonState_ReflectsWindowState(
            WindowState windowState,
            string expectedContent,
            string expectedToolTip)
        {
            var coordinator = new MainWindowShellModeCoordinator();

            var result = coordinator.BuildMaximizeRestoreWindowButtonState(windowState);

            Assert.Equal(expectedContent, result.Content);
            Assert.Equal(expectedToolTip, result.ToolTip);
        }
    }
}
