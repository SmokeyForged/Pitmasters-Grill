using PitmastersGrill.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace PitmastersGrill.Services
{
    public sealed class WindowLayoutRestoreResult
    {
        public required Rect TargetBounds { get; init; }

        public required string RestoreDecision { get; init; }

        public required string RestoreReason { get; init; }

        public required bool HasSavedBounds { get; init; }

        public required Rect SavedBounds { get; init; }

        public required bool ShouldRestoreMaximized { get; init; }

        public required WindowState LastNonMinimizedWindowState { get; init; }
    }

    public sealed class WindowLayoutController
    {
        public WindowLayoutRestoreResult BuildRestoreResult(
            AppSettings settings,
            double minWidth,
            double minHeight,
            double minimumSavedWindowWidth,
            double minimumSavedWindowHeight,
            double minimumVisibleWindowEdge,
            double defaultWindowWidth,
            double defaultWindowHeight,
            IReadOnlyList<Rect> workAreas)
        {
            return BuildRestoreResult(
                settings,
                WindowLayoutMode.Normal,
                minWidth,
                minHeight,
                minimumSavedWindowWidth,
                minimumSavedWindowHeight,
                minimumVisibleWindowEdge,
                defaultWindowWidth,
                defaultWindowHeight,
                workAreas);
        }

        public WindowLayoutRestoreResult BuildRestoreResult(
            AppSettings settings,
            WindowLayoutMode mode,
            double minWidth,
            double minHeight,
            double minimumSavedWindowWidth,
            double minimumSavedWindowHeight,
            double minimumVisibleWindowEdge,
            double defaultWindowWidth,
            double defaultWindowHeight,
            IReadOnlyList<Rect> workAreas)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var fallbackBounds = GetDefaultWindowBounds(
                workAreas,
                minimumSavedWindowWidth,
                minimumSavedWindowHeight,
                defaultWindowWidth,
                defaultWindowHeight);

            var hasSavedBounds = TryGetSavedWindowBounds(settings, mode, out var savedBounds);
            Rect targetBounds;
            string restoreDecision;
            string restoreReason;

            if (!hasSavedBounds)
            {
                targetBounds = fallbackBounds;
                restoreDecision = "Fallback";
                restoreReason = $"No saved {mode} bounds were available.";
            }
            else if (!TryValidateWindowBounds(
                savedBounds,
                minWidth,
                minHeight,
                minimumSavedWindowWidth,
                minimumSavedWindowHeight,
                minimumVisibleWindowEdge,
                workAreas,
                out var failureReason))
            {
                targetBounds = fallbackBounds;
                restoreDecision = "Fallback";
                restoreReason = failureReason;
            }
            else
            {
                targetBounds = savedBounds;
                restoreDecision = "Applied";
                restoreReason = $"Saved {mode} bounds are visible on the current monitor layout.";
            }

            var shouldRestoreMaximized = IsSavedLayoutMaximized(settings, mode);

            return new WindowLayoutRestoreResult
            {
                TargetBounds = targetBounds,
                RestoreDecision = restoreDecision,
                RestoreReason = restoreReason,
                HasSavedBounds = hasSavedBounds,
                SavedBounds = hasSavedBounds ? savedBounds : Rect.Empty,
                ShouldRestoreMaximized = shouldRestoreMaximized,
                LastNonMinimizedWindowState = shouldRestoreMaximized ? WindowState.Maximized : WindowState.Normal
            };
        }

        public bool TryBuildLayoutSnapshot(
            Rect bounds,
            WindowState effectiveState,
            double minWidth,
            double minHeight,
            double minimumSavedWindowWidth,
            double minimumSavedWindowHeight,
            double minimumVisibleWindowEdge,
            IReadOnlyList<Rect> workAreas,
            out WindowLayoutSnapshot snapshot,
            out string failureReason)
        {
            snapshot = new WindowLayoutSnapshot();

            if (!TryValidateWindowBounds(
                bounds,
                minWidth,
                minHeight,
                minimumSavedWindowWidth,
                minimumSavedWindowHeight,
                minimumVisibleWindowEdge,
                workAreas,
                out failureReason))
            {
                return false;
            }

            snapshot = new WindowLayoutSnapshot
            {
                Left = bounds.Left,
                Top = bounds.Top,
                Width = bounds.Width,
                Height = bounds.Height,
                IsMaximized = effectiveState == WindowState.Maximized
            };

            return true;
        }

        public void ApplySnapshot(AppSettings settings, WindowLayoutSnapshot snapshot)
        {
            ApplySnapshot(settings, snapshot, WindowLayoutMode.Normal);
        }

        public void ApplySnapshot(AppSettings settings, WindowLayoutSnapshot snapshot, WindowLayoutMode mode)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (mode == WindowLayoutMode.Board)
            {
                settings.SavedBoardWindowLeft = snapshot.Left;
                settings.SavedBoardWindowTop = snapshot.Top;
                settings.SavedBoardWindowWidth = snapshot.Width;
                settings.SavedBoardWindowHeight = snapshot.Height;
                settings.SavedBoardWindowIsMaximized = snapshot.IsMaximized;
                return;
            }

            settings.SavedNormalWindowLeft = snapshot.Left;
            settings.SavedNormalWindowTop = snapshot.Top;
            settings.SavedNormalWindowWidth = snapshot.Width;
            settings.SavedNormalWindowHeight = snapshot.Height;
            settings.SavedNormalWindowIsMaximized = snapshot.IsMaximized;

            // Keep legacy fields synchronized so existing code or older settings readers still see the normal layout.
            settings.SavedWindowLeft = snapshot.Left;
            settings.SavedWindowTop = snapshot.Top;
            settings.SavedWindowWidth = snapshot.Width;
            settings.SavedWindowHeight = snapshot.Height;
            settings.SavedWindowIsMaximized = snapshot.IsMaximized;
        }

        public void ClearSavedLayout(AppSettings settings)
        {
            ClearSavedLayout(settings, WindowLayoutMode.Normal);
        }

        public void ClearSavedLayout(AppSettings settings, WindowLayoutMode mode)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (mode == WindowLayoutMode.Board)
            {
                settings.SavedBoardWindowLeft = null;
                settings.SavedBoardWindowTop = null;
                settings.SavedBoardWindowWidth = null;
                settings.SavedBoardWindowHeight = null;
                settings.SavedBoardWindowIsMaximized = false;
                return;
            }

            settings.SavedNormalWindowLeft = null;
            settings.SavedNormalWindowTop = null;
            settings.SavedNormalWindowWidth = null;
            settings.SavedNormalWindowHeight = null;
            settings.SavedNormalWindowIsMaximized = false;

            settings.SavedWindowLeft = null;
            settings.SavedWindowTop = null;
            settings.SavedWindowWidth = null;
            settings.SavedWindowHeight = null;
            settings.SavedWindowIsMaximized = false;
        }

        public void ClearAllSavedLayouts(AppSettings settings)
        {
            ClearSavedLayout(settings, WindowLayoutMode.Normal);
            ClearSavedLayout(settings, WindowLayoutMode.Board);
        }

        public bool TryGetSavedWindowBounds(AppSettings settings, out Rect bounds)
        {
            return TryGetSavedWindowBounds(settings, WindowLayoutMode.Normal, out bounds);
        }

        public bool TryGetSavedWindowBounds(AppSettings settings, WindowLayoutMode mode, out Rect bounds)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            bounds = Rect.Empty;

            var left = mode == WindowLayoutMode.Board ? settings.SavedBoardWindowLeft : settings.SavedNormalWindowLeft;
            var top = mode == WindowLayoutMode.Board ? settings.SavedBoardWindowTop : settings.SavedNormalWindowTop;
            var width = mode == WindowLayoutMode.Board ? settings.SavedBoardWindowWidth : settings.SavedNormalWindowWidth;
            var height = mode == WindowLayoutMode.Board ? settings.SavedBoardWindowHeight : settings.SavedNormalWindowHeight;

            if (mode == WindowLayoutMode.Normal &&
                (!left.HasValue || !top.HasValue || !width.HasValue || !height.HasValue) &&
                settings.SavedWindowLeft.HasValue &&
                settings.SavedWindowTop.HasValue &&
                settings.SavedWindowWidth.HasValue &&
                settings.SavedWindowHeight.HasValue)
            {
                left = settings.SavedWindowLeft;
                top = settings.SavedWindowTop;
                width = settings.SavedWindowWidth;
                height = settings.SavedWindowHeight;
            }

            if (!left.HasValue || !top.HasValue || !width.HasValue || !height.HasValue)
            {
                return false;
            }

            bounds = new Rect(left.Value, top.Value, width.Value, height.Value);
            return true;
        }

        public bool IsSavedLayoutMaximized(AppSettings settings, WindowLayoutMode mode)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            return mode == WindowLayoutMode.Board
                ? settings.SavedBoardWindowIsMaximized
                : settings.SavedNormalWindowIsMaximized || settings.SavedWindowIsMaximized;
        }

        public bool TryValidateWindowBounds(
            Rect bounds,
            double minWidth,
            double minHeight,
            double minimumSavedWindowWidth,
            double minimumSavedWindowHeight,
            double minimumVisibleWindowEdge,
            IReadOnlyList<Rect> workAreas,
            out string failureReason)
        {
            if (!IsUsableWindowBounds(bounds))
            {
                failureReason = "Bounds contained NaN or Infinity.";
                return false;
            }

            if (bounds.Width < Math.Max(minWidth, minimumSavedWindowWidth) ||
                bounds.Height < Math.Max(minHeight, minimumSavedWindowHeight))
            {
                failureReason = "Bounds were smaller than the minimum safe size.";
                return false;
            }

            foreach (var workArea in workAreas ?? Array.Empty<Rect>())
            {
                var intersection = Rect.Intersect(bounds, workArea);

                if (!intersection.IsEmpty &&
                    intersection.Width >= minimumVisibleWindowEdge &&
                    intersection.Height >= minimumVisibleWindowEdge)
                {
                    failureReason = string.Empty;
                    return true;
                }
            }

            failureReason = "Bounds were outside the current monitor work areas.";
            return false;
        }

        public Rect GetDefaultWindowBounds(
            IReadOnlyList<Rect> workAreas,
            double minimumSavedWindowWidth,
            double minimumSavedWindowHeight,
            double defaultWindowWidth,
            double defaultWindowHeight)
        {
            var workArea = workAreas != null && workAreas.Count > 0 ? workAreas[0] : new Rect(0, 0, 1280, 800);
            var width = Math.Max(minimumSavedWindowWidth, defaultWindowWidth);
            var height = Math.Max(minimumSavedWindowHeight, defaultWindowHeight);

            width = Math.Min(width, workArea.Width);
            height = Math.Min(height, workArea.Height);

            var left = workArea.Left + Math.Max(0, (workArea.Width - width) / 2);
            var top = workArea.Top + Math.Max(0, (workArea.Height - height) / 2);

            return new Rect(left, top, width, height);
        }

        public bool IsUsableWindowBounds(Rect bounds)
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

        public string BuildVirtualDesktopSummary(IReadOnlyList<Rect> workAreas)
        {
            if (workAreas == null || workAreas.Count == 0)
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

        public string DescribeRect(Rect bounds)
        {
            return bounds.IsEmpty
                ? string.Empty
                : $"[{bounds.Left:0.##},{bounds.Top:0.##},{bounds.Width:0.##},{bounds.Height:0.##}]";
        }
    }
}
