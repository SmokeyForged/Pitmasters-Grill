using PitmastersGrill.Models;
using System;
using System.Collections.Generic;
using System.Windows;

namespace PitmastersGrill.Services
{
    public sealed class WindowLayoutSurface
    {
        private readonly WindowLayoutController _windowLayoutController;
        private readonly Action<AppSettings> _saveSettings;
        private readonly Func<IReadOnlyList<Rect>> _getMonitorWorkAreasDip;

        public WindowLayoutSurface(
            WindowLayoutController windowLayoutController,
            Action<AppSettings> saveSettings,
            Func<IReadOnlyList<Rect>> getMonitorWorkAreasDip)
        {
            _windowLayoutController = windowLayoutController ?? throw new ArgumentNullException(nameof(windowLayoutController));
            _saveSettings = saveSettings ?? throw new ArgumentNullException(nameof(saveSettings));
            _getMonitorWorkAreasDip = getMonitorWorkAreasDip ?? throw new ArgumentNullException(nameof(getMonitorWorkAreasDip));
        }

        public bool IsRestoringWindowLayout { get; private set; }

        public WindowState LastNonMinimizedWindowState { get; private set; } = WindowState.Normal;

        public Rect LastKnownNormalBounds { get; private set; } = Rect.Empty;

        public void HandleWindowStateChanged(WindowState windowState, Rect restoreBounds, Rect currentBounds)
        {
            if (windowState != WindowState.Minimized)
            {
                LastNonMinimizedWindowState = windowState;
            }

            if (windowState == WindowState.Maximized && _windowLayoutController.IsUsableWindowBounds(restoreBounds))
            {
                LastKnownNormalBounds = restoreBounds;
            }
            else
            {
                TrackCurrentNormalWindowBounds(windowState, currentBounds);
            }
        }

        public void TrackCurrentNormalWindowBounds(WindowState windowState, Rect currentBounds)
        {
            if (windowState != WindowState.Normal)
            {
                return;
            }

            if (!_windowLayoutController.IsUsableWindowBounds(currentBounds))
            {
                return;
            }

            LastKnownNormalBounds = currentBounds;
        }

        public void RestoreFromSettings(
            AppSettings settings,
            WindowLayoutMode mode,
            double minWidth,
            double minHeight,
            double minimumSavedWindowWidth,
            double minimumSavedWindowHeight,
            double minimumVisibleWindowEdge,
            double defaultWindowWidth,
            double defaultWindowHeight,
            Action<Rect> applyTargetBounds,
            Action<WindowState> applyWindowState,
            Action<string> logInfo)
        {
            var workAreas = _getMonitorWorkAreasDip();
            var virtualDesktopSummary = _windowLayoutController.BuildVirtualDesktopSummary(workAreas);
            var restoreResult = _windowLayoutController.BuildRestoreResult(
                settings,
                mode,
                minWidth,
                minHeight,
                minimumSavedWindowWidth,
                minimumSavedWindowHeight,
                minimumVisibleWindowEdge,
                defaultWindowWidth,
                defaultWindowHeight,
                workAreas);

            logInfo(
                $"Window layout restore decision={restoreResult.RestoreDecision} mode={mode} savedBounds={_windowLayoutController.DescribeRect(restoreResult.SavedBounds)} fallbackReason='{restoreResult.RestoreReason}' wasMaximized={restoreResult.ShouldRestoreMaximized} virtualWorkAreas={virtualDesktopSummary}");

            IsRestoringWindowLayout = true;
            try
            {
                applyWindowState(WindowState.Normal);
                applyTargetBounds(restoreResult.TargetBounds);
                LastKnownNormalBounds = restoreResult.TargetBounds;

                if (restoreResult.ShouldRestoreMaximized)
                {
                    applyWindowState(WindowState.Maximized);
                }

                LastNonMinimizedWindowState = restoreResult.LastNonMinimizedWindowState;
            }
            finally
            {
                IsRestoringWindowLayout = false;
            }

            logInfo(
                $"Window layout restore applied mode={mode} finalBounds={_windowLayoutController.DescribeRect(restoreResult.TargetBounds)} finalWindowState={(restoreResult.ShouldRestoreMaximized ? WindowState.Maximized : WindowState.Normal)}");
        }

        public void SaveToSettings(
            AppSettings settings,
            string reason,
            WindowLayoutMode mode,
            WindowState windowState,
            Rect restoreBounds,
            Rect currentBounds,
            double minWidth,
            double minHeight,
            double minimumSavedWindowWidth,
            double minimumSavedWindowHeight,
            double minimumVisibleWindowEdge,
            Action<string> logInfo,
            Action<string> logWarn)
        {
            var effectiveState = windowState == WindowState.Minimized
                ? LastNonMinimizedWindowState
                : windowState;

            if (effectiveState == WindowState.Maximized && _windowLayoutController.IsUsableWindowBounds(restoreBounds))
            {
                LastKnownNormalBounds = restoreBounds;
            }
            else
            {
                TrackCurrentNormalWindowBounds(windowState, currentBounds);
            }

            var bounds = _windowLayoutController.IsUsableWindowBounds(LastKnownNormalBounds)
                ? LastKnownNormalBounds
                : effectiveState == WindowState.Maximized ? restoreBounds : currentBounds;

            var workAreas = _getMonitorWorkAreasDip();
            if (!_windowLayoutController.TryBuildLayoutSnapshot(
                    bounds,
                    effectiveState,
                    minWidth,
                    minHeight,
                    minimumSavedWindowWidth,
                    minimumSavedWindowHeight,
                    minimumVisibleWindowEdge,
                    workAreas,
                    out var snapshot,
                    out var failureReason))
            {
                logWarn(
                    $"Window layout save skipped.\nreason='{reason}' mode={mode} bounds={_windowLayoutController.DescribeRect(bounds)} failureReason='{failureReason}' virtualWorkAreas={_windowLayoutController.BuildVirtualDesktopSummary(workAreas)}");
                return;
            }

            _windowLayoutController.ApplySnapshot(settings, snapshot, mode);
            _saveSettings(settings);

            logInfo(
                $"Window layout saved.\nreason='{reason}' mode={mode} bounds={_windowLayoutController.DescribeRect(bounds)} maximized={snapshot.IsMaximized} virtualWorkAreas={_windowLayoutController.BuildVirtualDesktopSummary(workAreas)}");
        }

        public void ClearSavedLayouts(AppSettings settings)
        {
            _windowLayoutController.ClearAllSavedLayouts(settings);
            _saveSettings(settings);
        }

        public Rect GetDefaultWindowBounds(
            double minimumSavedWindowWidth,
            double minimumSavedWindowHeight,
            double defaultWindowWidth,
            double defaultWindowHeight)
        {
            return _windowLayoutController.GetDefaultWindowBounds(
                _getMonitorWorkAreasDip(),
                minimumSavedWindowWidth,
                minimumSavedWindowHeight,
                defaultWindowWidth,
                defaultWindowHeight);
        }
    }
}
