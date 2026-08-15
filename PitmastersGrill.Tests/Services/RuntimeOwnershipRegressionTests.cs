using System;
using System.IO;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class RuntimeOwnershipRegressionTests
    {
        [Fact]
        public void TrayLifecycle_IsOwnedOnlyByApplicationTrayService()
        {
            var appSource = ReadRepoFile("PitmastersGrill", "App.xaml.cs");
            var mainWindowSource = ReadRepoFile("PitmastersGrill", "MainWindow.xaml.cs");
            var duplicateTrayPath = GetRepoFilePath("PitmastersGrill", "Services", "SystemTrayIconService.cs");

            Assert.Contains("new PmgTrayIconService(this, mainWindow)", appSource);
            Assert.DoesNotContain("SystemTrayIconService", mainWindowSource);
            Assert.False(
                File.Exists(duplicateTrayPath),
                $"Duplicate tray implementation should not exist: {duplicateTrayPath}");
        }

        [Fact]
        public void R2Z2EvidencePersistence_DelegatesToIncrementalImport()
        {
            var source = ReadRepoFile("PitmastersGrill", "Services", "R2Z2LiveKillmailService.cs");
            var stateRepository = ReadRepoFile(
                "PitmastersGrill",
                "Persistence",
                "R2Z2FeedStateRepository.cs");

            Assert.Contains("_incrementalImportService.ImportKillmailJson", source);

            Assert.DoesNotContain("INSERT INTO pilot_registry_day", source);
            Assert.DoesNotContain("INSERT INTO pilot_fleet_observations_day", source);
            Assert.DoesNotContain("INSERT INTO pilot_ship_observations_day", source);
            Assert.DoesNotContain("INSERT INTO pilot_cyno_module_observations_day", source);
            Assert.DoesNotContain("INSERT INTO pilot_bait_observations_day", source);
            Assert.DoesNotContain("INSERT INTO pilot_cyno_tackle_observations_day", source);
            Assert.DoesNotContain("UpsertSeenRecord(", source);
            Assert.DoesNotContain("TryGetSeenRecord(", source);

            Assert.DoesNotContain("INSERT INTO live_killmail_feed_state", source);
            Assert.DoesNotContain("SELECT COUNT(*) FROM live_killmail_seen", source);
            Assert.Contains("_feedStateRepository.Update(", source);
            Assert.Contains("_feedStateRepository.ReadSnapshot()", source);

            Assert.Contains("INSERT INTO live_killmail_feed_state", stateRepository);
            Assert.Contains("SELECT COUNT(*) FROM live_killmail_seen", stateRepository);
            Assert.DoesNotContain("ImportKillmailJson", stateRepository);
        }

        [Fact]
        public void LiveFeedStartup_UsesTrackedPostUiPath()
        {
            var appSource = ReadRepoFile("PitmastersGrill", "App.xaml.cs");
            var backgroundSource = ReadRepoFile(
                "PitmastersGrill",
                "Services",
                "BackgroundIntelUpdateService.cs");
            var shutdownSource = ReadRepoFile(
                "PitmastersGrill",
                "Services",
                "BackgroundIntelUpdateService.Shutdown.cs");

            Assert.Contains(
                "backgroundIntelUpdateService.StartLiveFeedIfConfiguredAfterUiShownTracked();",
                appSource);
            Assert.DoesNotContain("StartLiveFeedIfConfiguredAfterUiShown()", backgroundSource);
            Assert.Contains("StartLiveFeedIfConfiguredAfterUiShownTracked()", shutdownSource);
        }

        [Fact]
        public void IntelStatusUiDispatch_DoesNotSynchronouslyBlockBackgroundPublishers()
        {
            var supportSource = ReadRepoFile(
                "PitmastersGrill",
                "Services",
                "IntelSupportSurface.cs");
            var bannerSource = ReadRepoFile(
                "PitmastersGrill",
                "Services",
                "IntelUpdateBannerController.cs");

            Assert.Contains("_dispatcher.BeginInvoke(", supportSource);
            Assert.DoesNotContain("_dispatcher.Invoke(", supportSource);
            Assert.Contains("_intelUpdateBannerController.ApplySnapshot(", supportSource);

            Assert.Contains("_dispatcher.BeginInvoke(", bannerSource);
            Assert.DoesNotContain("_dispatcher.Invoke(", bannerSource);
        }

        private static string ReadRepoFile(params string[] relativeSegments)
        {
            var path = GetRepoFilePath(relativeSegments);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Could not locate repository file '{path}'.", path);
            }

            return File.ReadAllText(path);
        }

        private static string GetRepoFilePath(params string[] relativeSegments)
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);

            while (current is not null)
            {
                var projectPath = Path.Combine(current.FullName, "PitmastersGrill", "PitmastersGrill.csproj");
                if (File.Exists(projectPath))
                {
                    var pathSegments = new string[relativeSegments.Length + 1];
                    pathSegments[0] = current.FullName;
                    Array.Copy(relativeSegments, 0, pathSegments, 1, relativeSegments.Length);
                    return Path.Combine(pathSegments);
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the Pitmasters-Grill repository root from the test output directory.");
        }
    }
}
