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

            Assert.Contains("GlobalClearBoardHotKeyId", code);
            Assert.Contains("GlobalToggleBoardModeHotKeyId", code);
            Assert.Contains("Key.Delete", code);
            Assert.Contains("Key.Insert", code);
            Assert.Contains("TryRegisterGlobalBoardActionHotKeys", code);
            Assert.Contains("TryUnregisterGlobalBoardActionHotKeys", code);
        }

        [Fact]
        public void MainWindowCode_UsesPendingSessionContextAtStartup()
        {
            var code = ReadMainWindowCode();

            Assert.Contains("CreatePendingEveSessionContext", code);
            Assert.Contains("Waiting for local session evidence", code);
            Assert.Contains("Soft local read pending", code);
        }

        [Fact]
        public void MainWindowCode_DoesNotUseNullableCompactModeValueInDeferredSave()
        {
            var code = ReadMainWindowCode();

            Assert.DoesNotContain("previousCompactMode.Value ? WindowLayoutMode.Board", code);
            Assert.Contains("outgoingLayoutMode", code);
        }

        [Fact]
        public void MainWindowXaml_PlacesSessionContextBelowBoardAnalysisDetails()
        {
            var xaml = ReadMainWindowXaml();

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
            return ReadRepoFile("PitmastersGrill", "MainWindow.xaml.cs");
        }

        private static string ReadMainWindowXaml()
        {
            return ReadRepoFile("PitmastersGrill", "MainWindow.xaml");
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
