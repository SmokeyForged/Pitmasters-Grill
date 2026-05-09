using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class MainWindowShellSurfaceTests
    {
        [Fact]
        public void ApplyCompactModeUi_WhenSwitchingToBoardMode_SelectsBoardTabAndPersistsSetting()
        {
            RunOnStaThread(() =>
            {
                var harness = CreateHarness();
                harness.CompactModeToggleButton.IsChecked = false;
                harness.MainTabControl.SelectedIndex = 2;

                harness.Surface.ApplyCompactModeUi();

                harness.CompactModeToggleButton.IsChecked = true;
                harness.MainTabControl.SelectedIndex = 0;
                harness.Surface.ApplyCompactModeUi();

                Assert.True(harness.Settings.CompactModeEnabled);
                Assert.Equal(1, harness.MainTabControl.SelectedIndex);
                Assert.Equal(Visibility.Collapsed, harness.TopCommandGrid.Visibility);
                Assert.Equal(Visibility.Collapsed, harness.BoardStatusFooter.Visibility);
                Assert.Equal(1, harness.CloseActiveDetailWindowCalls);
                Assert.True(harness.SaveSettingsCalls >= 1);
            });
        }

        [Fact]
        public void HandlePreviewKey_ForClipboardRefresh_InvalidatesClipboardAndStartsProcessing()
        {
            RunOnStaThread(() =>
            {
                var harness = CreateHarness();

                var handled = harness.Surface.HandlePreviewKey(
                    System.Windows.Input.Key.Home,
                    controlModifierPressed: false,
                    isTextEditing: false);

                Assert.True(handled);
                Assert.Equal(1, harness.InvalidateClipboardCalls);
                Assert.Equal(1, harness.ProcessClipboardCalls);
            });
        }

        [Fact]
        public void HandlePreviewKey_OnThirdEscape_RequestsShutdown()
        {
            RunOnStaThread(() =>
            {
                var harness = CreateHarness();

                harness.Surface.HandlePreviewKey(System.Windows.Input.Key.Escape, false, false);
                harness.Surface.HandlePreviewKey(System.Windows.Input.Key.Escape, false, false);
                var handled = harness.Surface.HandlePreviewKey(System.Windows.Input.Key.Escape, false, false);

                Assert.True(handled);
                Assert.Contains("Triple Escape hotkey", harness.ShutdownReasons);
            });
        }

        [Fact]
        public void HandleResetWindowLayoutButton_AppliesSafeBoundsAndShowsConfirmation()
        {
            RunOnStaThread(() =>
            {
                var harness = CreateHarness();
                harness.WindowState = WindowState.Maximized;
                harness.CurrentBounds = new Rect(999, 999, 200, 200);

                harness.Surface.HandleResetWindowLayoutButton();

                Assert.Equal(WindowState.Normal, harness.WindowState);
                Assert.Equal(new Rect(580, 254.5, 760, 571), harness.CurrentBounds);
                Assert.Equal(1, harness.DialogCallCount);
                Assert.True(harness.SaveSettingsCalls >= 1);
            });
        }

        private static Harness CreateHarness()
        {
            var settings = new AppSettings();
            var saveSettingsCalls = 0;
            var windowState = WindowState.Normal;
            var restoreBounds = new Rect(40, 60, 900, 700);
            var currentBounds = new Rect(40, 60, 900, 700);
            var closeActiveDetailWindowCalls = 0;
            var updateBoardSummaryCalls = 0;
            var updateAnalysisCalls = 0;
            var sessionRefreshReasons = new List<string>();
            var fitCalls = new List<bool>();
            var invalidateClipboardCalls = 0;
            var processClipboardCalls = 0;
            var clearBoardReasons = new List<string>();
            var shutdownReasons = new List<string>();
            var dialogCallCount = 0;

            var compactModeToggleButton = new ToggleButton();
            var mainContentGrid = new Grid { Margin = new Thickness(12) };
            var topCommandGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            var mainTabControl = new TabControl();
            mainTabControl.Items.Add(new TabItem { Header = "Analysis" });
            mainTabControl.Items.Add(new TabItem { Header = "Grill" });
            mainTabControl.Items.Add(new TabItem { Header = "Settings" });
            mainTabControl.SelectedIndex = 0;
            var boardModeHintOverlay = new Border { Visibility = Visibility.Collapsed };
            var boardStatusFooter = new Border { Visibility = Visibility.Visible };
            var maximizeRestoreWindowButton = new Button();
            var pilotBoard = new DataGrid { FontSize = 12 };
            var boardModeHintTimer = new DispatcherTimer();

            var windowLayoutSurface = new WindowLayoutSurface(
                new WindowLayoutController(),
                _ => saveSettingsCalls++,
                () => new List<Rect> { new(0, 0, 1920, 1080) });

            var surface = new MainWindowShellSurface(
                new MainWindowShellModeCoordinator(),
                new MainWindowInteropController(),
                windowLayoutSurface,
                compactModeToggleButton,
                mainContentGrid,
                topCommandGrid,
                mainTabControl,
                boardModeHintOverlay,
                boardStatusFooter,
                maximizeRestoreWindowButton,
                pilotBoard,
                boardModeHintTimer,
                () => settings,
                _ => saveSettingsCalls++,
                () => false,
                () => windowState,
                state => windowState = state,
                () => restoreBounds,
                () => currentBounds,
                bounds => currentBounds = bounds,
                (_, _) => { },
                () => closeActiveDetailWindowCalls++,
                () => updateBoardSummaryCalls++,
                () => updateAnalysisCalls++,
                (reason, _) => sessionRefreshReasons.Add(reason),
                _ => true,
                force => fitCalls.Add(force),
                () => invalidateClipboardCalls++,
                () =>
                {
                    processClipboardCalls++;
                    return Task.CompletedTask;
                },
                reason => clearBoardReasons.Add(reason),
                reason => shutdownReasons.Add(reason),
                (_, _, _, _) =>
                {
                    dialogCallCount++;
                    return MessageBoxResult.OK;
                },
                420,
                300,
                420,
                38,
                32,
                28,
                18,
                16,
                420,
                300,
                80,
                760,
                571,
                1500);

            return new Harness(
                surface,
                settings,
                compactModeToggleButton,
                mainTabControl,
                topCommandGrid,
                boardStatusFooter,
                () => saveSettingsCalls,
                () => closeActiveDetailWindowCalls,
                () => updateBoardSummaryCalls,
                () => updateAnalysisCalls,
                sessionRefreshReasons,
                fitCalls,
                () => invalidateClipboardCalls,
                () => processClipboardCalls,
                clearBoardReasons,
                shutdownReasons,
                () => dialogCallCount,
                () => windowState,
                state => windowState = state,
                () => currentBounds,
                bounds => currentBounds = bounds);
        }

        private sealed class Harness
        {
            private readonly Func<int> _saveSettingsCalls;
            private readonly Func<int> _closeActiveDetailWindowCalls;
            private readonly Func<int> _updateBoardSummaryCalls;
            private readonly Func<int> _updateAnalysisCalls;
            private readonly Func<int> _invalidateClipboardCalls;
            private readonly Func<int> _processClipboardCalls;
            private readonly Func<int> _dialogCallCount;
            private readonly Func<WindowState> _getWindowState;
            private readonly Action<WindowState> _setWindowState;
            private readonly Func<Rect> _getCurrentBounds;
            private readonly Action<Rect> _setCurrentBounds;

            public Harness(
                MainWindowShellSurface surface,
                AppSettings settings,
                ToggleButton compactModeToggleButton,
                TabControl mainTabControl,
                Grid topCommandGrid,
                Border boardStatusFooter,
                Func<int> saveSettingsCalls,
                Func<int> closeActiveDetailWindowCalls,
                Func<int> updateBoardSummaryCalls,
                Func<int> updateAnalysisCalls,
                List<string> sessionRefreshReasons,
                List<bool> fitCalls,
                Func<int> invalidateClipboardCalls,
                Func<int> processClipboardCalls,
                List<string> clearBoardReasons,
                List<string> shutdownReasons,
                Func<int> dialogCallCount,
                Func<WindowState> getWindowState,
                Action<WindowState> setWindowState,
                Func<Rect> getCurrentBounds,
                Action<Rect> setCurrentBounds)
            {
                Surface = surface;
                Settings = settings;
                CompactModeToggleButton = compactModeToggleButton;
                MainTabControl = mainTabControl;
                TopCommandGrid = topCommandGrid;
                BoardStatusFooter = boardStatusFooter;
                _saveSettingsCalls = saveSettingsCalls;
                _closeActiveDetailWindowCalls = closeActiveDetailWindowCalls;
                _updateBoardSummaryCalls = updateBoardSummaryCalls;
                _updateAnalysisCalls = updateAnalysisCalls;
                SessionRefreshReasons = sessionRefreshReasons;
                FitCalls = fitCalls;
                _invalidateClipboardCalls = invalidateClipboardCalls;
                _processClipboardCalls = processClipboardCalls;
                ClearBoardReasons = clearBoardReasons;
                ShutdownReasons = shutdownReasons;
                _dialogCallCount = dialogCallCount;
                _getWindowState = getWindowState;
                _setWindowState = setWindowState;
                _getCurrentBounds = getCurrentBounds;
                _setCurrentBounds = setCurrentBounds;
            }

            public MainWindowShellSurface Surface { get; }
            public AppSettings Settings { get; }
            public ToggleButton CompactModeToggleButton { get; }
            public TabControl MainTabControl { get; }
            public Grid TopCommandGrid { get; }
            public Border BoardStatusFooter { get; }
            public List<string> SessionRefreshReasons { get; }
            public List<bool> FitCalls { get; }
            public List<string> ClearBoardReasons { get; }
            public List<string> ShutdownReasons { get; }

            public int SaveSettingsCalls => _saveSettingsCalls();
            public int CloseActiveDetailWindowCalls => _closeActiveDetailWindowCalls();
            public int UpdateBoardSummaryCalls => _updateBoardSummaryCalls();
            public int UpdateAnalysisCalls => _updateAnalysisCalls();
            public int InvalidateClipboardCalls => _invalidateClipboardCalls();
            public int ProcessClipboardCalls => _processClipboardCalls();
            public int DialogCallCount => _dialogCallCount();

            public WindowState WindowState
            {
                get => _getWindowState();
                set => _setWindowState(value);
            }

            public Rect CurrentBounds
            {
                get => _getCurrentBounds();
                set => _setCurrentBounds(value);
            }
        }

        private static void RunOnStaThread(Action action)
        {
            Exception? captured = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    captured = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (captured != null)
            {
                throw new Xunit.Sdk.XunitException($"STA thread test failed: {captured}");
            }
        }
    }
}
