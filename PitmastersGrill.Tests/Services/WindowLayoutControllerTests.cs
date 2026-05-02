using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System.Collections.Generic;
using System.Windows;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public class WindowLayoutControllerTests
    {
        private const double MinWidth = 200;
        private const double MinHeight = 120;
        private const double MinimumSavedWindowWidth = 420;
        private const double MinimumSavedWindowHeight = 300;
        private const double MinimumVisibleWindowEdge = 80;
        private const double DefaultWindowWidth = 760;
        private const double DefaultWindowHeight = 571;

        [Fact]
        public void BuildRestoreResult_AppliesSaneSavedBounds()
        {
            var controller = new WindowLayoutController();
            var settings = new AppSettings
            {
                SavedWindowLeft = 100,
                SavedWindowTop = 120,
                SavedWindowWidth = 800,
                SavedWindowHeight = 600,
                SavedWindowIsMaximized = true
            };

            var result = controller.BuildRestoreResult(
                settings,
                MinWidth,
                MinHeight,
                MinimumSavedWindowWidth,
                MinimumSavedWindowHeight,
                MinimumVisibleWindowEdge,
                DefaultWindowWidth,
                DefaultWindowHeight,
                new List<Rect> { new(0, 0, 1920, 1080) });

            Assert.Equal("Applied", result.RestoreDecision);
            Assert.Equal(new Rect(100, 120, 800, 600), result.TargetBounds);
            Assert.True(result.ShouldRestoreMaximized);
            Assert.Equal(WindowState.Maximized, result.LastNonMinimizedWindowState);
        }

        [Fact]
        public void BuildRestoreResult_FallsBackWhenSavedBoundsAreTooSmall()
        {
            var controller = new WindowLayoutController();
            var settings = new AppSettings
            {
                SavedWindowLeft = 5,
                SavedWindowTop = 5,
                SavedWindowWidth = 200,
                SavedWindowHeight = 100
            };

            var result = controller.BuildRestoreResult(
                settings,
                MinWidth,
                MinHeight,
                MinimumSavedWindowWidth,
                MinimumSavedWindowHeight,
                MinimumVisibleWindowEdge,
                DefaultWindowWidth,
                DefaultWindowHeight,
                new List<Rect> { new(0, 0, 1600, 900) });

            Assert.Equal("Fallback", result.RestoreDecision);
            Assert.Equal("Bounds were smaller than the minimum safe size.", result.RestoreReason);
            Assert.Equal(new Rect(420, 164.5, 760, 571), result.TargetBounds);
        }

        [Fact]
        public void BuildRestoreResult_FallsBackWhenSavedBoundsAreOffScreen()
        {
            var controller = new WindowLayoutController();
            var settings = new AppSettings
            {
                SavedWindowLeft = 3000,
                SavedWindowTop = 100,
                SavedWindowWidth = 800,
                SavedWindowHeight = 600
            };

            var result = controller.BuildRestoreResult(
                settings,
                MinWidth,
                MinHeight,
                MinimumSavedWindowWidth,
                MinimumSavedWindowHeight,
                MinimumVisibleWindowEdge,
                DefaultWindowWidth,
                DefaultWindowHeight,
                new List<Rect> { new(0, 0, 1600, 900) });

            Assert.Equal("Fallback", result.RestoreDecision);
            Assert.Equal("Bounds were outside the current monitor work areas.", result.RestoreReason);
        }

        [Fact]
        public void GetDefaultWindowBounds_CentersWithinWorkAreaAndClampsToAvailableSize()
        {
            var controller = new WindowLayoutController();

            var result = controller.GetDefaultWindowBounds(
                new List<Rect> { new(100, 50, 600, 400) },
                MinimumSavedWindowWidth,
                MinimumSavedWindowHeight,
                DefaultWindowWidth,
                DefaultWindowHeight);

            Assert.Equal(new Rect(100, 50, 600, 400), result);
        }

        [Fact]
        public void TryBuildLayoutSnapshot_CapturesMaximizedFlagForValidBounds()
        {
            var controller = new WindowLayoutController();

            var success = controller.TryBuildLayoutSnapshot(
                new Rect(40, 60, 900, 700),
                WindowState.Maximized,
                MinWidth,
                MinHeight,
                MinimumSavedWindowWidth,
                MinimumSavedWindowHeight,
                MinimumVisibleWindowEdge,
                new List<Rect> { new(0, 0, 1920, 1080) },
                out var snapshot,
                out var failureReason);

            Assert.True(success, failureReason);
            Assert.True(snapshot.IsMaximized);
            Assert.Equal(40, snapshot.Left);
            Assert.Equal(700, snapshot.Height);
        }

        [Fact]
        public void TryBuildLayoutSnapshot_RejectsBoundsOutsideUsableWorkAreas()
        {
            var controller = new WindowLayoutController();

            var success = controller.TryBuildLayoutSnapshot(
                new Rect(-900, -700, 500, 400),
                WindowState.Normal,
                MinWidth,
                MinHeight,
                MinimumSavedWindowWidth,
                MinimumSavedWindowHeight,
                MinimumVisibleWindowEdge,
                new List<Rect> { new(0, 0, 1920, 1080) },
                out _,
                out var failureReason);

            Assert.False(success);
            Assert.Equal("Bounds were outside the current monitor work areas.", failureReason);
        }

        [Fact]
        public void ClearSavedLayout_RemovesPersistedWindowFields()
        {
            var controller = new WindowLayoutController();
            var settings = new AppSettings
            {
                SavedWindowLeft = 1,
                SavedWindowTop = 2,
                SavedWindowWidth = 3,
                SavedWindowHeight = 4,
                SavedWindowIsMaximized = true
            };

            controller.ClearSavedLayout(settings);

            Assert.Null(settings.SavedWindowLeft);
            Assert.Null(settings.SavedWindowTop);
            Assert.Null(settings.SavedWindowWidth);
            Assert.Null(settings.SavedWindowHeight);
            Assert.False(settings.SavedWindowIsMaximized);
        }

        [Fact]
        public void ApplySnapshot_PersistsSnapshotValuesBackToSettings()
        {
            var controller = new WindowLayoutController();
            var settings = new AppSettings();

            controller.ApplySnapshot(
                settings,
                new WindowLayoutSnapshot
                {
                    Left = 12,
                    Top = 34,
                    Width = 640,
                    Height = 480,
                    IsMaximized = false
                });

            Assert.Equal(12, settings.SavedWindowLeft);
            Assert.Equal(34, settings.SavedWindowTop);
            Assert.Equal(640, settings.SavedWindowWidth);
            Assert.Equal(480, settings.SavedWindowHeight);
            Assert.False(settings.SavedWindowIsMaximized);
        }
    }
}
