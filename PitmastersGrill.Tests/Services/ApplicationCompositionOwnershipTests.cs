using System;
using System.IO;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class ApplicationCompositionOwnershipTests
    {
        [Fact]
        public void App_UsesCompositionRootAndComposedMainWindowPath()
        {
            var appSource = ReadRepoFile("PitmastersGrill", "App.xaml.cs");

            Assert.Contains("ApplicationCompositionRoot.ComposeNormalRuntime(appSettingsService)", appSource);
            Assert.Contains("ApplicationCompositionRoot.ComposeMainWindowRuntime", appSource);
            Assert.Contains("new MainWindow(backgroundIntelUpdateService, mainWindowRuntime)", appSource);
            Assert.DoesNotContain("new MainWindow(backgroundIntelUpdateService);", appSource);
        }

        [Fact]
        public void ActiveMainWindowConstructor_DoesNotCreateLongLivedRuntimeDependencies()
        {
            var constructorSource = ReadRepoFile("PitmastersGrill", "MainWindow.ComposedConstructor.cs");

            Assert.Contains("MainWindowRuntimeDependencies runtime", constructorSource);
            Assert.DoesNotContain("new AppSettingsService", constructorSource);
            Assert.DoesNotContain("new MainWindowDiagnostics", constructorSource);
            Assert.DoesNotContain("new ResolverService", constructorSource);
            Assert.DoesNotContain("new StatsService", constructorSource);
            Assert.DoesNotContain("new BoardPopulationSurface", constructorSource);
            Assert.DoesNotContain("new EveSessionContextService", constructorSource);
        }

        [Fact]
        public void LegacyMainWindowCompositionPath_IsAbsent()
        {
            var mainWindowSource = ReadRepoFile("PitmastersGrill", "MainWindow.xaml.cs");

            Assert.DoesNotMatch(
                @"public\s+MainWindow\s*\(\s*BackgroundIntelUpdateService\s+backgroundIntelUpdateService\s*\)",
                mainWindowSource);
            Assert.False(RepoFileExists("PitmastersGrill", "Services", "MainWindowCompositionRoot.cs"));
        }

        [Fact]
        public void BackgroundServices_RemainDeferredUntilAfterFirstRender()
        {
            var appSource = ReadRepoFile("PitmastersGrill", "App.xaml.cs");
            var renderedIndex = appSource.IndexOf("mainWindow.ContentRendered", StringComparison.Ordinal);
            var archiveStartIndex = appSource.IndexOf("backgroundIntelUpdateService.StartIfNeeded()", StringComparison.Ordinal);
            var liveStartIndex = appSource.IndexOf("StartLiveFeedIfConfiguredAfterUiShownTracked", StringComparison.Ordinal);
            var repairStartIndex = appSource.IndexOf("ScheduleBackgroundHistoricalRepairAfterUiShown", StringComparison.Ordinal);

            Assert.True(renderedIndex >= 0);
            Assert.True(archiveStartIndex > renderedIndex);
            Assert.True(liveStartIndex > renderedIndex);
            Assert.True(repairStartIndex > renderedIndex);
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

        private static bool RepoFileExists(params string[] relativeSegments)
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                var candidateSegments = new string[relativeSegments.Length + 1];
                candidateSegments[0] = current.FullName;
                Array.Copy(relativeSegments, 0, candidateSegments, 1, relativeSegments.Length);
                if (File.Exists(Path.Combine(candidateSegments)))
                {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }
    }
}
