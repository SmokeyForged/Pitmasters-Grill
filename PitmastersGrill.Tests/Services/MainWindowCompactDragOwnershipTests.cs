using System;
using System.IO;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class MainWindowCompactDragOwnershipTests
    {
        [Fact]
        public void MainWindow_DelegatesCompactDragStatePolicyAndPreservesHandledEventRouting()
        {
            var source = ReadRepoFile("PitmastersGrill", "MainWindow.xaml.cs");
            var constructorSource = ReadRepoFile("PitmastersGrill", "MainWindow.ComposedConstructor.cs");

            Assert.DoesNotContain("_compactDragPending", source, StringComparison.Ordinal);
            Assert.DoesNotContain("_compactDragStartPoint", source, StringComparison.Ordinal);
            Assert.DoesNotContain("CompactDragHoldMilliseconds", source, StringComparison.Ordinal);
            Assert.DoesNotContain("IsFromCompactDragBlockedElement", source, StringComparison.Ordinal);
            Assert.Contains("_compactBoardDragController.TryBegin(", source, StringComparison.Ordinal);
            Assert.Contains("e.OriginalSource as DependencyObject", source, StringComparison.Ordinal);
            Assert.Contains("_compactBoardDragController.CancelIfLeftButtonReleased(", source, StringComparison.Ordinal);
            Assert.Contains("_compactBoardDragController.CompleteHoldAction(", source, StringComparison.Ordinal);
            Assert.Contains("CompactBoardDragAction.RequestDrag", source, StringComparison.Ordinal);
            Assert.Contains("Interval = CompactBoardDragController.HoldDuration", constructorSource, StringComparison.Ordinal);

            Assert.Contains(
                "PilotBoard.AddHandler(UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler(PilotBoard_PreviewMouseDownHandledToo), true);",
                source,
                StringComparison.Ordinal);
            Assert.Contains(
                "PilotBoard.AddHandler(UIElement.PreviewMouseUpEvent, new MouseButtonEventHandler(PilotBoard_PreviewMouseUpHandledToo), true);",
                source,
                StringComparison.Ordinal);
            Assert.Contains(
                "PilotBoard.AddHandler(UIElement.PreviewMouseMoveEvent, new MouseEventHandler(PilotBoard_PreviewMouseMoveHandledToo), true);",
                source,
                StringComparison.Ordinal);
        }

        [Fact]
        public void MainWindow_NoLongerOwnsCompactDragBlockedElementPolicy()
        {
            var source = ReadRepoFile("PitmastersGrill", "MainWindow.xaml.cs");
            var controllerSource = ReadRepoFile("PitmastersGrill", "Services", "CompactBoardDragController.cs");

            Assert.DoesNotContain("FindVisualParent<DataGridColumnHeader>(source)", source, StringComparison.Ordinal);
            Assert.DoesNotContain("FindVisualParent<ScrollBar>(source) != null", source, StringComparison.Ordinal);
            Assert.Contains("FindParent<DataGridColumnHeader>(source)", controllerSource, StringComparison.Ordinal);
            Assert.Contains("FindParent<Thumb>(source) != null", controllerSource, StringComparison.Ordinal);
            Assert.Contains("FindParent<ButtonBase>(source) != null", controllerSource, StringComparison.Ordinal);
            Assert.Contains("FindParent<ScrollBar>(source) != null", controllerSource, StringComparison.Ordinal);
            Assert.Contains("FindParent<TextBox>(source) != null", controllerSource, StringComparison.Ordinal);
            Assert.Contains("FindParent<ComboBox>(source) != null", controllerSource, StringComparison.Ordinal);
        }

        [Fact]
        public void MainWindow_RowDoubleClick_CancelsPendingCompactDragBeforeOpeningZkill()
        {
            var source = ReadRepoFile("PitmastersGrill", "MainWindow.xaml.cs");
            const string methodSignature = "private void PilotBoard_MouseDoubleClick";
            const string nextMethodSignature = "private void PilotBoard_PreviewMouseRightButtonUp";

            var methodStart = source.IndexOf(methodSignature, StringComparison.Ordinal);
            var methodEnd = source.IndexOf(nextMethodSignature, methodStart, StringComparison.Ordinal);

            Assert.True(methodStart >= 0, "PilotBoard_MouseDoubleClick was not found.");
            Assert.True(methodEnd > methodStart, "Could not isolate PilotBoard_MouseDoubleClick.");

            var methodSource = source[methodStart..methodEnd];
            var cancelIndex = methodSource.IndexOf("CancelCompactBoardDrag();", StringComparison.Ordinal);
            var openIndex = methodSource.IndexOf("OpenZkillForRow(selectedRow);", StringComparison.Ordinal);

            Assert.True(cancelIndex >= 0, "Row double-click must cancel pending compact drag.");
            Assert.True(openIndex > cancelIndex, "Compact drag cancellation must occur before the row double-click action opens zKill.");
        }

        private static string ReadRepoFile(params string[] relativeSegments)
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                var candidateSegments = new string[relativeSegments.Length + 1];
                candidateSegments[0] = current.FullName;
                Array.Copy(relativeSegments, 0, candidateSegments, 1, relativeSegments.Length);
                var candidate = Path.Combine(candidateSegments);
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }

                current = current.Parent;
            }

            throw new FileNotFoundException($"Could not locate repository file: {string.Join("/", relativeSegments)}");
        }
    }
}
