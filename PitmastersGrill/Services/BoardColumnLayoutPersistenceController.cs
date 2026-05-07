using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PitmastersGrill.Services
{
    public sealed class BoardColumnLayoutPersistenceController
    {
        private readonly BoardColumnLayoutController _boardColumnLayoutController;
        private readonly Action<AppSettings> _saveSettings;
        private string _pendingBoardColumnLayoutSaveReason = string.Empty;
        private DependencyPropertyDescriptor? _boardColumnWidthDescriptor;
        private bool _isBoardColumnAutoFitPending;
        private bool _isBoardColumnLayoutReadyForPersistence;

        public BoardColumnLayoutPersistenceController(
            BoardColumnLayoutController boardColumnLayoutController,
            Action<AppSettings> saveSettings)
        {
            _boardColumnLayoutController = boardColumnLayoutController ?? throw new ArgumentNullException(nameof(boardColumnLayoutController));
            _saveSettings = saveSettings ?? throw new ArgumentNullException(nameof(saveSettings));
        }

        public bool IsApplyingBoardColumnLayout { get; private set; }

        public void RunWhileApplyingBoardColumnLayout(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var wasApplyingLayout = IsApplyingBoardColumnLayout;
            IsApplyingBoardColumnLayout = true;

            try
            {
                action();
            }
            finally
            {
                IsApplyingBoardColumnLayout = wasApplyingLayout;
            }
        }

        public void EnsureBoardColumnWidthTracking(EventHandler widthChangedHandler)
        {
            if (widthChangedHandler == null)
            {
                throw new ArgumentNullException(nameof(widthChangedHandler));
            }

            if (_boardColumnWidthDescriptor != null)
            {
                return;
            }

            _boardColumnWidthDescriptor = DependencyPropertyDescriptor.FromProperty(
                DataGridColumn.WidthProperty,
                typeof(DataGridColumn));

            if (_boardColumnWidthDescriptor == null)
            {
                AppLogger.UiWarn("Board column width tracking could not be initialized.");
                return;
            }

            foreach (var column in _boardColumnLayoutController.BoardColumnsByKey.Values)
            {
                _boardColumnWidthDescriptor.AddValueChanged(column, widthChangedHandler);
            }
        }

        public void ApplySavedBoardColumnLayout(
            AppSettings settings,
            Action<IEnumerable<BoardColumnLayoutSetting>, string> applyBoardColumnLayout,
            Action<string> applyCanonicalBoardColumnLayout)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (applyBoardColumnLayout == null)
            {
                throw new ArgumentNullException(nameof(applyBoardColumnLayout));
            }

            if (applyCanonicalBoardColumnLayout == null)
            {
                throw new ArgumentNullException(nameof(applyCanonicalBoardColumnLayout));
            }

            if (settings.BoardColumnLayout == null || settings.BoardColumnLayout.Count == 0)
            {
                return;
            }

            if (!_boardColumnLayoutController.TryValidateSavedBoardColumnLayout(settings.BoardColumnLayout, out var validSavedSettings, out var validationFailureReason))
            {
                AppLogger.UiWarn($"Saved board column layout discarded. reason='{validationFailureReason}'");
                settings.BoardColumnLayout.Clear();
                _saveSettings(settings);
                applyCanonicalBoardColumnLayout("Discard invalid saved board layout");
                return;
            }

            applyBoardColumnLayout(validSavedSettings, "Restore saved board layout");
        }

        public void ApplyBoardColumnLayout(
            IEnumerable<BoardColumnLayoutSetting> layoutSettings,
            Action scheduleFitVisibleBoardColumnsToViewport,
            string reason)
        {
            if (layoutSettings == null)
            {
                return;
            }

            if (scheduleFitVisibleBoardColumnsToViewport == null)
            {
                throw new ArgumentNullException(nameof(scheduleFitVisibleBoardColumnsToViewport));
            }

            RunWhileApplyingBoardColumnLayout(() =>
            {
                _boardColumnLayoutController.ApplyBoardColumnLayout(layoutSettings);
                scheduleFitVisibleBoardColumnsToViewport();
                AppLogger.UiInfo($"Board column layout applied. reason='{reason}'");
            });
        }

        public void MarkBoardColumnLayoutReady()
        {
            _isBoardColumnLayoutReadyForPersistence = true;
        }

        public bool CanPersistBoardColumnLayout(Func<bool> isBoardLayoutHostReady)
        {
            if (isBoardLayoutHostReady == null)
            {
                throw new ArgumentNullException(nameof(isBoardLayoutHostReady));
            }

            return _isBoardColumnLayoutReadyForPersistence && isBoardLayoutHostReady();
        }

        public bool TryQueueBoardColumnLayoutSave(
            bool isApplyingSettings,
            Func<bool> isBoardLayoutHostReady,
            string reason)
        {
            if (isBoardLayoutHostReady == null)
            {
                throw new ArgumentNullException(nameof(isBoardLayoutHostReady));
            }

            if (isApplyingSettings || IsApplyingBoardColumnLayout || !CanPersistBoardColumnLayout(isBoardLayoutHostReady))
            {
                return false;
            }

            _pendingBoardColumnLayoutSaveReason = reason;
            return true;
        }

        public string DequeuePendingBoardColumnLayoutSaveReason()
        {
            var reason = string.IsNullOrWhiteSpace(_pendingBoardColumnLayoutSaveReason)
                ? "Board layout changed"
                : _pendingBoardColumnLayoutSaveReason;
            _pendingBoardColumnLayoutSaveReason = string.Empty;
            return reason;
        }

        public void SaveCurrentBoardColumnLayout(
            AppSettings settings,
            Func<bool> isBoardLayoutHostReady,
            string reason)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (isBoardLayoutHostReady == null)
            {
                throw new ArgumentNullException(nameof(isBoardLayoutHostReady));
            }

            if (!CanPersistBoardColumnLayout(isBoardLayoutHostReady))
            {
                AppLogger.UiDebug($"Board column layout save skipped. reason='{reason}' hostReady=false");
                return;
            }

            var currentLayout = _boardColumnLayoutController.CaptureCurrentBoardColumnLayout();

            if (!_boardColumnLayoutController.TryValidateSavedBoardColumnLayout(currentLayout, out var sanitizedLayout, out var validationFailureReason))
            {
                AppLogger.UiWarn($"Board column layout save skipped. reason='{reason}' validationFailure='{validationFailureReason}'");
                return;
            }

            if (_boardColumnLayoutController.BoardColumnLayoutsMatch(settings.BoardColumnLayout, sanitizedLayout))
            {
                return;
            }

            settings.BoardColumnLayout = sanitizedLayout;
            _saveSettings(settings);
            AppLogger.UiInfo($"Board column layout saved. reason='{reason}'");
        }

        public bool TryQueueFitVisibleBoardColumnsToViewport(DataGrid? pilotBoard, bool force = false)
        {
            if (pilotBoard == null)
            {
                return false;
            }

            if (_isBoardColumnAutoFitPending && !force)
            {
                return false;
            }

            _isBoardColumnAutoFitPending = true;
            return true;
        }

        public void CompleteQueuedFitVisibleBoardColumnsToViewport(DataGrid? pilotBoard)
        {
            _isBoardColumnAutoFitPending = false;
            FitVisibleBoardColumnsToViewport(pilotBoard);
        }

        public void FitVisibleBoardColumnsToViewport(DataGrid? pilotBoard)
        {
            if (pilotBoard == null || _boardColumnLayoutController.BoardColumnsByKey.Count == 0 || pilotBoard.ActualWidth <= 0)
            {
                return;
            }

            pilotBoard.UpdateLayout();
            FitVisibleBoardColumnsToWidth(GetPilotBoardViewportWidth(pilotBoard));
        }

        public void FitVisibleBoardColumnsToWidth(double availableWidth)
        {
            var visibleColumns = _boardColumnLayoutController.BoardColumnsByKey.Values
                .Where(column => column.Visibility == Visibility.Visible)
                .OrderBy(column => column.DisplayIndex)
                .ToList();

            if (visibleColumns.Count == 0 || double.IsNaN(availableWidth) || double.IsInfinity(availableWidth) || availableWidth <= 40d)
            {
                return;
            }

            var columnPlans = visibleColumns
                .Select(column =>
                {
                    var key = _boardColumnLayoutController.GetBoardColumnKey(column);
                    var minimum = Math.Max(12d, _boardColumnLayoutController.GetBoardColumnMinimumWidth(key));
                    var current = Math.Max(minimum, GetEffectiveBoardColumnWidth(column));
                    return new BoardColumnFitPlan(column, minimum, current);
                })
                .ToList();

            var minimumTotal = columnPlans.Sum(plan => plan.MinimumWidth);
            var preferredTotal = columnPlans.Sum(plan => plan.CurrentWidth);

            if (minimumTotal <= 0d || preferredTotal <= 0d)
            {
                return;
            }

            RunWhileApplyingBoardColumnLayout(() =>
            {
                if (minimumTotal >= availableWidth)
                {
                    var scale = Math.Max(0.6d, availableWidth / minimumTotal);
                    foreach (var plan in columnPlans)
                    {
                        SetBoardColumnPixelWidth(plan.Column, Math.Max(18d, plan.MinimumWidth * scale));
                    }

                    return;
                }

                if (preferredTotal > availableWidth)
                {
                    var shortage = preferredTotal - availableWidth;
                    var shrinkCapacity = columnPlans.Sum(plan => Math.Max(0d, plan.CurrentWidth - plan.MinimumWidth));

                    foreach (var plan in columnPlans)
                    {
                        var targetWidth = plan.CurrentWidth;
                        if (shrinkCapacity > 0d)
                        {
                            var share = Math.Max(0d, plan.CurrentWidth - plan.MinimumWidth) / shrinkCapacity;
                            targetWidth = Math.Max(plan.MinimumWidth, plan.CurrentWidth - shortage * share);
                        }

                        SetBoardColumnPixelWidth(plan.Column, targetWidth);
                    }

                    return;
                }

                var extra = availableWidth - preferredTotal;
                var expandableTotal = columnPlans.Sum(plan => Math.Max(plan.MinimumWidth, plan.CurrentWidth));
                foreach (var plan in columnPlans)
                {
                    var share = expandableTotal > 0d
                        ? Math.Max(plan.MinimumWidth, plan.CurrentWidth) / expandableTotal
                        : 1d / columnPlans.Count;
                    SetBoardColumnPixelWidth(plan.Column, plan.CurrentWidth + extra * share);
                }
            });
        }

        private static double GetPilotBoardViewportWidth(DataGrid pilotBoard)
        {
            try
            {
                var scrollViewer = FindVisualDescendant<ScrollViewer>(pilotBoard);
                if (scrollViewer != null && scrollViewer.ViewportWidth > 0d)
                {
                    return Math.Max(0d, scrollViewer.ViewportWidth - 1d);
                }
            }
            catch (InvalidOperationException)
            {
                // Visual tree may not be ready during early layout passes. Fall back below.
            }

            return Math.Max(0d, pilotBoard.ActualWidth - 1d);
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
                if (child is T typed)
                {
                    return typed;
                }

                var nested = FindVisualDescendant<T>(child);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static void SetBoardColumnPixelWidth(DataGridColumn column, double width)
        {
            if (column == null || double.IsNaN(width) || double.IsInfinity(width) || width <= 0d)
            {
                return;
            }

            var roundedWidth = Math.Round(width, 1);
            if (Math.Abs(GetEffectiveBoardColumnWidth(column) - roundedWidth) < 0.5d &&
                column.Width.UnitType == DataGridLengthUnitType.Pixel)
            {
                return;
            }

            column.Width = new DataGridLength(roundedWidth, DataGridLengthUnitType.Pixel);
        }

        private static double GetEffectiveBoardColumnWidth(DataGridColumn column)
        {
            if (column == null)
            {
                return 0d;
            }

            var width = column.ActualWidth;
            if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
            {
                width = column.Width.DisplayValue;
            }

            if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
            {
                width = column.MinWidth;
            }

            return double.IsNaN(width) || double.IsInfinity(width) || width <= 0
                ? 0d
                : width;
        }

        private sealed record BoardColumnFitPlan(DataGridColumn Column, double MinimumWidth, double CurrentWidth);
    }
}
