using System;
using System.IO;
using Xunit;

namespace PitmastersGrill.Tests.Ui
{
    public sealed class MainWindowNestedTabsTests
    {
        [Fact]
        public void MainWindowResources_DefinesSharedNestedTabStyles()
        {
            var resourcesXaml = ReadRepoFile("PitmastersGrill", "Resources", "MainWindowResources.xaml");

            Assert.Contains("PmgNestedSubTabControlStyle", resourcesXaml);
            Assert.Contains("PmgNestedSubTabItemStyle", resourcesXaml);
            Assert.Contains("PMG nested sub-tab shared styles", resourcesXaml);
        }

        [Fact]
        public void MainWindowXaml_AppliesNestedStyleToAtLeastTwoSubTabControls()
        {
            var xaml = ReadMainWindowXaml();

            var styledControlCount = CountOccurrences(xaml, "Style=\"{StaticResource PmgNestedSubTabControlStyle}\"");
            var styledItemCount = CountOccurrences(xaml, "Style=\"{StaticResource PmgNestedSubTabItemStyle}\"");

            Assert.True(styledControlCount >= 2, $"Expected at least two styled nested TabControls, found {styledControlCount}.");
            Assert.True(styledItemCount >= 2, $"Expected at least two styled nested TabItems, found {styledItemCount}.");
        }

        [Fact]
        public void MainWindowXaml_HelpScrollViewersUseMouseWheelHandler()
        {
            var xaml = ReadMainWindowXaml();

            Assert.Contains("Header=\"Help\"", xaml);
            Assert.Contains("General / Getting Started", xaml);
            Assert.Contains("Signal Reference", xaml);
            Assert.Contains("NestedScrollViewer_PreviewMouseWheel", xaml);
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

        private static int CountOccurrences(string text, string value)
        {
            var count = 0;
            var index = 0;

            while ((index = text.IndexOf(value, index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }
    }
}
