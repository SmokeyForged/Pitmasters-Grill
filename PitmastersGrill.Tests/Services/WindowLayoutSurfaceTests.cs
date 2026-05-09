using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System.Collections.Generic;
using System.Windows;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class WindowLayoutSurfaceTests
    {
        private const double MinWidth = 200;
        private const double MinHeight = 120;
        private const double MinimumSavedWindowWidth = 420;
        private const double MinimumSavedWindowHeight = 300;
        private const double MinimumVisibleWindowEdge = 80;
        private const double DefaultWindowWidth = 760;
        private const double DefaultWindowHeight = 571;

        [Fact]
        public void RestoreFromSettings_AppliesBoundsAndTracksState()
        {
            var settings = new AppSettings
            {
                SavedNormalWindowLeft = 100,
                SavedNormalWindowTop = 120,
                SavedNormalWindowWidth = 800,
                SavedNormalWindowHeight = 600,
                SavedNormalWindowIsMaximized = true
            };
            var surface = new WindowLayoutSurface(
                new WindowLayoutController(),
                _ => { },
                () => new List<Rect> { new(0, 0, 1920, 1080) });
            var appliedBounds = Rect.Empty;
            var appliedStates = new List<WindowState>();

            surface.RestoreFromSettings(
                settings,
                WindowLayoutMode.Normal,
                MinWidth,
                MinHeight,
                MinimumSavedWindowWidth,
                MinimumSavedWindowHeight,
                MinimumVisibleWindowEdge,
                DefaultWindowWidth,
                DefaultWindowHeight,
                bounds => appliedBounds = bounds,
                appliedStates.Add,
                _ => { });

            Assert.Equal(new Rect(100, 120, 800, 600), appliedBounds);
            Assert.Equal(WindowState.Maximized, surface.LastNonMinimizedWindowState);
            Assert.Equal(new Rect(100, 120, 800, 600), surface.LastKnownNormalBounds);
            Assert.False(surface.IsRestoringWindowLayout);
            Assert.Equal(new[] { WindowState.Normal, WindowState.Maximized }, appliedStates);
        }

        [Fact]
        public void SaveToSettings_PersistsSnapshotUsingTrackedBounds()
        {
            var settings = new AppSettings();
            var saveCount = 0;
            var surface = new WindowLayoutSurface(
                new WindowLayoutController(),
                _ => saveCount++,
                () => new List<Rect> { new(0, 0, 1920, 1080) });

            surface.HandleWindowStateChanged(
                WindowState.Normal,
                Rect.Empty,
                new Rect(40, 60, 900, 700));

            surface.SaveToSettings(
                settings,
                "test save",
                WindowLayoutMode.Board,
                WindowState.Normal,
                Rect.Empty,
                new Rect(40, 60, 900, 700),
                MinWidth,
                MinHeight,
                MinimumSavedWindowWidth,
                MinimumSavedWindowHeight,
                MinimumVisibleWindowEdge,
                _ => { },
                _ => { });

            Assert.Equal(1, saveCount);
            Assert.Equal(40, settings.SavedBoardWindowLeft);
            Assert.Equal(700, settings.SavedBoardWindowHeight);
            Assert.False(settings.SavedBoardWindowIsMaximized);
        }
    }
}
