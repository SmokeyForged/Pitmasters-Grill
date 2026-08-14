using System;
using System.IO;
using Xunit;

namespace PitmastersGrill.Tests.Ui
{
    public sealed class MainWindowSessionHotkeyTests
    {
        [Fact]
        public void MainWindowCode_RegistersGlobalDeleteAndInsertHotkeys()
        {
            var code = ReadMainWindowCode();
            var nativeInputControllerCode = ReadRepoFile("PitmastersGrill", "Services", "MainWindowNativeInputController.cs");

            Assert.Contains("GlobalClearBoardHotKeyId", code);
            Assert.Contains("GlobalToggleBoardModeHotKeyId", code);
            Assert.Contains("_mainWindowNativeInputController.Attach", code);
            Assert.Contains("_mainWindowNativeInputController.Detach", code);
            Assert.Contains("Key.Delete", nativeInputControllerCode);
            Assert.Contains("Key.Insert", nativeInputControllerCode);
        }

        [Fact]
        public void MainWindowCode_UsesPendingSessionContextAtStartup()
        {
            var code = ReadMainWindowCode();
            var coordinatorCode = ReadRepoFile("PitmastersGrill", "Services", "EveSessionContextCoordinator.cs");
            var surfaceCode = ReadRepoFile("PitmastersGrill", "Services", "EveSessionContextSurface.cs");

            Assert.Contains("_eveSessionContextSurface.ApplyPendingContext()", code);
            Assert.Contains("CreatePendingContext()", surfaceCode);
            Assert.Contains("Waiting for local session evidence", coordinatorCode);
            Assert.Contains("Soft local read pending", coordinatorCode);
        }

        [Fact]
        public void MainWindowCode_DoesNotUseNullableCompactModeValueInDeferredSave()
        {
            var code = ReadMainWindowCode();
            var shellSurfaceCode = ReadRepoFile("PitmastersGrill", "Services", "MainWindowShellSurface.cs");

            Assert.DoesNotContain("previousCompactMode.Value ? WindowLayoutMode.Board", code);
            Assert.Contains("transition.OutgoingLayoutMode", shellSurfaceCode);
        }

        [Fact]
        public void AnalysisViewXaml_PlacesSessionContextBelowBoardAnalysisDetails()
        {
            var xaml = ReadRepoFile("PitmastersGrill", "Views", "AnalysisView.xaml");

            var detailsIndex = xaml.IndexOf("AnalysisDetailsPanel", StringComparison.OrdinalIgnoreCase);
            var sessionIndex = xaml.IndexOf("EVE Session Context", StringComparison.OrdinalIgnoreCase);

            Assert.True(detailsIndex >= 0, "Expected AnalysisDetailsPanel to exist.");
            Assert.True(sessionIndex >= 0, "Expected EVE Session Context to exist.");
            Assert.True(
                sessionIndex > detailsIndex,
                "Expected EVE Session Context to appear after visible Board Analysis details.");
        }

        private static string ReadMainWindowCode()
        {
            return ReadRepoFile("PitmastersGrill", "MainWindow.xaml.cs")
                + ReadRepoFile("PitmastersGrill", "MainWindow.ComposedConstructor.cs");
        }

        private static string ReadRepoFile(params string[] relativeParts)
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);

            while (current is not null)
            {
                var candidateParts = new string[relativeParts.Length + 1];
                candidateParts[0] = current.FullName;
                Array.Copy(relativeParts, 0, candidateParts, 1, relativeParts.Length);

                var candidate = Path.Combine(candidateParts);

                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }

                current = current.Parent;
            }

            throw new FileNotFoundException($"Could not locate repo file: {string.Join("/", relativeParts)}");
        }
    }
}
