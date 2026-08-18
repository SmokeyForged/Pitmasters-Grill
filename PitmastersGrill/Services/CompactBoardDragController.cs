using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace PitmastersGrill.Services
{
    public enum CompactBoardDragAction
    {
        None,
        RequestDrag
    }

    public sealed class CompactBoardDragController
    {
        public static TimeSpan HoldDuration { get; } = TimeSpan.FromMilliseconds(300);

        public bool IsPending { get; private set; }

        public bool TryBegin(bool boardModeEnabled, int clickCount, DependencyObject? source)
        {
            // Preserve MainWindow's existing routed-event behavior: duplicate delivery while a
            // drag is already pending is ignored before any later eligibility checks run.
            if (IsPending || !boardModeEnabled)
            {
                return false;
            }

            if (clickCount > 1)
            {
                Cancel();
                return false;
            }

            if (IsBlockedByInteractiveElement(source))
            {
                return false;
            }

            IsPending = true;
            return true;
        }

        public void Cancel()
        {
            IsPending = false;
        }

        public bool CancelIfLeftButtonReleased(bool leftButtonPressed)
        {
            if (!IsPending || leftButtonPressed)
            {
                return false;
            }

            Cancel();
            return true;
        }

        public CompactBoardDragAction CompleteHoldAction(bool boardModeEnabled, bool leftButtonPressed)
        {
            if (!IsPending || !boardModeEnabled || !leftButtonPressed)
            {
                Cancel();
                return CompactBoardDragAction.None;
            }

            IsPending = false;
            return CompactBoardDragAction.RequestDrag;
        }

        public static bool IsBlockedByInteractiveElement(DependencyObject? source)
        {
            // Rows and column headers are valid compact-mode drag surfaces.
            // DataGridColumnHeader derives from ButtonBase, so allow it unless the source is
            // within its resize Thumb.
            if (FindParent<DataGridColumnHeader>(source) != null)
            {
                return FindParent<Thumb>(source) != null;
            }

            return FindParent<ButtonBase>(source) != null ||
                   FindParent<ScrollBar>(source) != null ||
                   FindParent<TextBox>(source) != null ||
                   FindParent<ComboBox>(source) != null ||
                   FindParent<Thumb>(source) != null;
        }

        private static T? FindParent<T>(DependencyObject? source)
            where T : DependencyObject
        {
            while (source != null)
            {
                if (source is T match)
                {
                    return match;
                }

                source = GetParentObject(source);
            }

            return null;
        }

        private static DependencyObject? GetParentObject(DependencyObject source)
        {
            if (source is FrameworkElement frameworkElement && frameworkElement.Parent != null)
            {
                return frameworkElement.Parent;
            }

            if (source is FrameworkContentElement frameworkContentElement && frameworkContentElement.Parent != null)
            {
                return frameworkContentElement.Parent;
            }

            try
            {
                return VisualTreeHelper.GetParent(source);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }
}
