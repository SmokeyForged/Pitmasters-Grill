using System;
using System.IO;
using Xunit;

namespace PitmastersGrill.Tests.Ui
{
    public sealed class MainWindowClosingLifecycleTests
    {
        [Fact]
        public void OnClosing_GuardsWindowLayoutPersistenceUntilInitializationCompletes()
        {
            var source = ReadRepoFile("PitmastersGrill", "MainWindow.xaml.cs");
            var onClosing = ExtractOnClosing(source);

            var guardIndex = onClosing.IndexOf("if (_isMainWindowInitialized)", StringComparison.Ordinal);
            var saveIndex = onClosing.IndexOf(
                "_mainWindowShellSurface.SaveWindowLayoutToSettings(\"Window closing\");",
                StringComparison.Ordinal);
            var baseIndex = onClosing.IndexOf("base.OnClosing(e);", StringComparison.Ordinal);

            Assert.True(guardIndex >= 0, "OnClosing must guard layout persistence on MainWindow initialization.");
            Assert.True(saveIndex > guardIndex, "The normal layout save must remain behind the initialization guard.");
            Assert.True(baseIndex > saveIndex, "The WPF base closing path must still run after guarded layout persistence.");
        }

        private static string ExtractOnClosing(string source)
        {
            const string onClosingSignature = "protected override void OnClosing(CancelEventArgs e)";
            const string onClosedSignature = "protected override void OnClosed(EventArgs e)";

            var start = source.IndexOf(onClosingSignature, StringComparison.Ordinal);
            var end = source.IndexOf(onClosedSignature, StringComparison.Ordinal);

            Assert.True(start >= 0, "Could not locate MainWindow.OnClosing in source.");
            Assert.True(end > start, "Could not isolate MainWindow.OnClosing from MainWindow.OnClosed.");

            return source[start..end];
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
