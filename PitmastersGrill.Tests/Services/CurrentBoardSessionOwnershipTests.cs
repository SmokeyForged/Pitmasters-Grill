using System;
using System.IO;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class CurrentBoardSessionOwnershipTests
    {
        [Fact]
        public void MainWindow_DelegatesCurrentBoardStateToSession()
        {
            var mainWindowSource = ReadRepoFile("PitmastersGrill", "MainWindow.xaml.cs")
                + ReadRepoFile("PitmastersGrill", "MainWindow.ComposedConstructor.cs");

            Assert.Contains("CurrentBoardSession _currentBoardSession", mainWindowSource);
            Assert.Contains("PilotBoard.ItemsSource = _currentBoardSession.Rows", mainWindowSource);
            Assert.Contains("_currentBoardSession.Changed += CurrentBoardSession_Changed", mainWindowSource);
            Assert.Contains("_currentBoardSession.BeginProcessingGeneration", mainWindowSource);
            Assert.Contains("_currentBoardSession.ClearAndInvalidate", mainWindowSource);
            Assert.Contains("_currentBoardSession.ReplaceRows", mainWindowSource);
            Assert.Contains("_currentBoardSession.RemoveRows", mainWindowSource);
            Assert.Contains("_currentBoardSession.ReorderRows", mainWindowSource);
            Assert.Contains("ApplyToCurrentRows(_currentBoardSession.Rows", mainWindowSource);
            Assert.Contains("_currentBoardSession.RemoveRows(applyResult.RemovedRows)", mainWindowSource);

            Assert.DoesNotContain("_currentRows", mainWindowSource);
            Assert.DoesNotContain("_processingGeneration", mainWindowSource);
            Assert.DoesNotContain("SubscribeToBoardRow", mainWindowSource);
            Assert.DoesNotContain("UnsubscribeFromBoardRow", mainWindowSource);
            Assert.DoesNotContain("UnsubscribeFromAllBoardRows", mainWindowSource);
            Assert.DoesNotContain("BoardRow_PropertyChanged", mainWindowSource);
            Assert.DoesNotContain("CurrentRows_CollectionChanged", mainWindowSource);
        }

        [Fact]
        public void SortAndClearSurfaces_DoNotExposeWritableBoardCollections()
        {
            var sortControllerSource = ReadRepoFile("PitmastersGrill", "Services", "BoardSortController.cs");
            var populationSurfaceSource = ReadRepoFile("PitmastersGrill", "Services", "BoardPopulationSurface.cs");

            Assert.DoesNotContain("ObservableCollection<PilotBoardRow>", sortControllerSource);
            Assert.DoesNotContain("ObservableCollection<PilotBoardRow>", populationSurfaceSource);
            Assert.Contains("Action<IReadOnlyList<PilotBoardRow>> applyOrderedRows", sortControllerSource);
            Assert.Contains("Action clearAndInvalidateBoardSession", populationSurfaceSource);
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
