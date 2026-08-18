using System;
using System.IO;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class MainWindowCompactDragOwnershipTests
    {
        [Fact]
        public void MainWindow_DelegatesCompactDragStateAndPreservesHandledEventRouting()
        {
            var source = ReadRepoFile("PitmastersGrill", "MainWindow.xaml.cs");

            Assert.DoesNotContain("_compactDragPending", source, StringComparison.Ordinal);
            Assert.DoesNotContain("_compactDragStartPoint", source, StringComparison.Ordinal);
            Assert.Contains("_compactBoardDragController.TryBegin(", source, StringComparison.Ordinal);
            Assert.Contains("_compactBoardDragController.CancelIfLeftButtonReleased(", source, StringComparison.Ordinal);
            Assert.Contains("_compactBoardDragController.CompleteHold(", source, StringComparison.Ordinal);

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
        public void MainWindow_CompactDragHitTest_PreservesInteractiveElementBoundary()
        {
            var source = ReadRepoFile("PitmastersGrill", "MainWindow.xaml.cs");

            Assert.Contains("FindVisualParent<DataGridColumnHeader>(source)", source, StringComparison.Ordinal);
            Assert.Contains("return FindVisualParent<Thumb>(source) != null;", source, StringComparison.Ordinal);
            Assert.Contains("FindVisualParent<ButtonBase>(source) != null", source, StringComparison.Ordinal);
            Assert.Contains("FindVisualParent<ScrollBar>(source) != null", source, StringComparison.Ordinal);
            Assert.Contains("FindVisualParent<TextBox>(source) != null", source, StringComparison.Ordinal);
            Assert.Contains("FindVisualParent<ComboBox>(source) != null", source, StringComparison.Ordinal);
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
