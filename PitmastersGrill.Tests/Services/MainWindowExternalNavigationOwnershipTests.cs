using System;
using System.IO;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class MainWindowExternalNavigationOwnershipTests
    {
        [Fact]
        public void MainWindow_RoutesAllExternalNavigationThroughCoordinator()
        {
            var source = ReadRepoFile("PitmastersGrill", "MainWindow.xaml.cs");

            Assert.DoesNotContain("_browserLauncher.OpenUrl(", source, StringComparison.Ordinal);
            Assert.Contains("ExternalNavigation.OpenPilotZkill(", source, StringComparison.Ordinal);
            Assert.Contains("ExternalNavigation.OpenUrl(url, \"GitHub repository\")", source, StringComparison.Ordinal);
            Assert.Contains("ExternalNavigation.OpenUrl(url, \"analysis hyperlink\")", source, StringComparison.Ordinal);
            Assert.Contains("ExternalNavigation.OpenAffiliationZkill(item.EntityType, item.Id)", source, StringComparison.Ordinal);
        }

        [Fact]
        public void MainWindow_NavigationEntryPoints_DoNotOwnProcessLaunchTryCatchPolicy()
        {
            var source = ReadRepoFile("PitmastersGrill", "MainWindow.xaml.cs");

            AssertMethodHasNoTryCatch(source, "private void OpenZkillForRow", "private void Window_PreviewKeyDown");
            AssertMethodHasNoTryCatch(source, "private void GitHubRepoLink_RequestNavigate", "private void ApplyWatchedState");
            AssertMethodHasNoTryCatch(source, "private void AnalysisHyperlink_RequestNavigate", "private void AnalysisAllianceListBox_MouseDoubleClick");
            AssertMethodHasNoTryCatch(source, "private void OpenAnalysisAffiliationItem", "private void NestedScrollViewer_PreviewMouseWheel");
        }

        [Fact]
        public void MainWindow_PreservesExistingUserFacingFailureBoundaryOnlyWhereItPreviouslyExisted()
        {
            var source = ReadRepoFile("PitmastersGrill", "MainWindow.xaml.cs");

            var pilotMethod = SliceMethod(source, "private void OpenZkillForRow", "private void Window_PreviewKeyDown");
            var githubMethod = SliceMethod(source, "private void GitHubRepoLink_RequestNavigate", "private void ApplyWatchedState");
            var analysisMethod = SliceMethod(source, "private void AnalysisHyperlink_RequestNavigate", "private void AnalysisAllianceListBox_MouseDoubleClick");
            var affiliationMethod = SliceMethod(source, "private void OpenAnalysisAffiliationItem", "private void NestedScrollViewer_PreviewMouseWheel");

            Assert.Contains("ShowExternalNavigationErrorIfNeeded(result);", pilotMethod, StringComparison.Ordinal);
            Assert.Contains("ShowExternalNavigationErrorIfNeeded(result);", githubMethod, StringComparison.Ordinal);
            Assert.DoesNotContain("ShowExternalNavigationErrorIfNeeded", analysisMethod, StringComparison.Ordinal);
            Assert.DoesNotContain("ShowExternalNavigationErrorIfNeeded", affiliationMethod, StringComparison.Ordinal);
            Assert.Contains("e.Handled = true;", githubMethod, StringComparison.Ordinal);
            Assert.Contains("e.Handled = true;", analysisMethod, StringComparison.Ordinal);
        }

        [Fact]
        public void MainWindow_AffiliationRouting_NoLongerDefaultsUnsupportedTypesToCorporation()
        {
            var source = ReadRepoFile("PitmastersGrill", "MainWindow.xaml.cs");
            var method = SliceMethod(source, "private void OpenAnalysisAffiliationItem", "private void NestedScrollViewer_PreviewMouseWheel");

            Assert.DoesNotContain("BuildAllianceZkillUrl", method, StringComparison.Ordinal);
            Assert.DoesNotContain("BuildCorporationZkillUrl", method, StringComparison.Ordinal);
            Assert.DoesNotContain("StringComparison.OrdinalIgnoreCase", method, StringComparison.Ordinal);
            Assert.Contains("ExternalNavigation.OpenAffiliationZkill(item.EntityType, item.Id)", method, StringComparison.Ordinal);
        }

        [Fact]
        public void MainWindowAdapter_UsesStructuredNonLoggingLauncherSeam()
        {
            var source = ReadRepoFile("PitmastersGrill", "MainWindow.ExternalNavigation.cs");

            Assert.Contains("_browserLauncher.TryOpenUrl", source, StringComparison.Ordinal);
            Assert.DoesNotContain("_browserLauncher.OpenUrl", source, StringComparison.Ordinal);
            Assert.Contains("AppLogger.UiInfo", source, StringComparison.Ordinal);
            Assert.Contains("AppLogger.UiWarn", source, StringComparison.Ordinal);
            Assert.Contains("AppLogger.UiError", source, StringComparison.Ordinal);
            Assert.Contains("if (result.Exception == null)", source, StringComparison.Ordinal);
        }

        private static void AssertMethodHasNoTryCatch(string source, string start, string next)
        {
            var method = SliceMethod(source, start, next);
            Assert.DoesNotContain("try", method, StringComparison.Ordinal);
            Assert.DoesNotContain("catch", method, StringComparison.Ordinal);
        }

        private static string SliceMethod(string source, string start, string next)
        {
            var startIndex = source.IndexOf(start, StringComparison.Ordinal);
            Assert.True(startIndex >= 0, $"Could not find method marker: {start}");

            var endIndex = source.IndexOf(next, startIndex, StringComparison.Ordinal);
            Assert.True(endIndex > startIndex, $"Could not isolate method beginning with: {start}");

            return source[startIndex..endIndex];
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
