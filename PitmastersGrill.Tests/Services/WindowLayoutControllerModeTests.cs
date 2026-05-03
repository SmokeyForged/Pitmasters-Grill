using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System.Windows;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class WindowLayoutControllerModeTests
    {
        private static readonly IReadOnlyList<Rect> WorkAreas = [new Rect(0, 0, 1920, 1080)];

        [Fact]
        public void ApplySnapshot_StoresNormalAndBoardLayoutsIndependently()
        {
            var settings = new AppSettings();
            var controller = new WindowLayoutController();

            controller.ApplySnapshot(
                settings,
                new WindowLayoutSnapshot { Left = 10, Top = 20, Width = 800, Height = 600, IsMaximized = false },
                WindowLayoutMode.Normal);

            controller.ApplySnapshot(
                settings,
                new WindowLayoutSnapshot { Left = 100, Top = 120, Width = 500, Height = 420, IsMaximized = true },
                WindowLayoutMode.Board);

            Assert.True(controller.TryGetSavedWindowBounds(settings, WindowLayoutMode.Normal, out var normalBounds));
            Assert.True(controller.TryGetSavedWindowBounds(settings, WindowLayoutMode.Board, out var boardBounds));

            Assert.Equal(new Rect(10, 20, 800, 600), normalBounds);
            Assert.Equal(new Rect(100, 120, 500, 420), boardBounds);
            Assert.False(controller.IsSavedLayoutMaximized(settings, WindowLayoutMode.Normal));
            Assert.True(controller.IsSavedLayoutMaximized(settings, WindowLayoutMode.Board));
        }

        [Fact]
        public void TryGetSavedWindowBounds_NormalFallsBackToLegacyLayout()
        {
            var settings = new AppSettings
            {
                SavedWindowLeft = 30,
                SavedWindowTop = 40,
                SavedWindowWidth = 900,
                SavedWindowHeight = 700
            };

            var controller = new WindowLayoutController();

            var found = controller.TryGetSavedWindowBounds(settings, WindowLayoutMode.Normal, out var bounds);

            Assert.True(found);
            Assert.Equal(new Rect(30, 40, 900, 700), bounds);
        }

        [Fact]
        public void BuildRestoreResult_UsesRequestedMode()
        {
            var settings = new AppSettings
            {
                SavedNormalWindowLeft = 10,
                SavedNormalWindowTop = 20,
                SavedNormalWindowWidth = 800,
                SavedNormalWindowHeight = 600,
                SavedBoardWindowLeft = 100,
                SavedBoardWindowTop = 120,
                SavedBoardWindowWidth = 500,
                SavedBoardWindowHeight = 420
            };

            var controller = new WindowLayoutController();

            var boardResult = controller.BuildRestoreResult(
                settings,
                WindowLayoutMode.Board,
                420,
                300,
                420,
                300,
                80,
                760,
                571,
                WorkAreas);

            Assert.Equal(new Rect(100, 120, 500, 420), boardResult.TargetBounds);
            Assert.Equal("Applied", boardResult.RestoreDecision);
        }
    }
}
