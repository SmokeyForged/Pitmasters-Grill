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
        public void MainWindowAndHelpView_ApplySharedNestedTabStyles()
        {
            var mainWindowXaml = ReadRepoFile("PitmastersGrill", "MainWindow.xaml");
            var helpViewXaml = ReadRepoFile("PitmastersGrill", "Views", "HelpView.xaml");

            Assert.True(
                CountOccurrences(mainWindowXaml, "Style=\"{StaticResource PmgNestedSubTabControlStyle}\"") >= 1,
                "Expected MainWindow Settings to retain the shared nested TabControl style.");
            Assert.True(
                CountOccurrences(mainWindowXaml, "Style=\"{StaticResource PmgNestedSubTabItemStyle}\"") >= 1,
                "Expected MainWindow Settings to retain the shared nested TabItem style.");
            Assert.Equal(1, CountOccurrences(helpViewXaml, "Style=\"{StaticResource PmgNestedSubTabControlStyle}\""));
            Assert.Equal(2, CountOccurrences(helpViewXaml, "Style=\"{StaticResource PmgNestedSubTabItemStyle}\""));
        }

        [Fact]
        public void HelpView_OwnsHelpTabsAndMouseWheelHandlers()
        {
            var mainWindowXaml = ReadRepoFile("PitmastersGrill", "MainWindow.xaml");
            var helpViewXaml = ReadRepoFile("PitmastersGrill", "Views", "HelpView.xaml");

            Assert.Contains("Header=\"Help\"", mainWindowXaml);
            Assert.Contains("<views:HelpView x:Name=\"HelpViewControl\"", mainWindowXaml);
            Assert.DoesNotContain("General / Getting Started", mainWindowXaml);
            Assert.DoesNotContain("Signal Reference", mainWindowXaml);
            Assert.DoesNotContain("NestedScrollViewer_PreviewMouseWheel", mainWindowXaml);

            Assert.Contains("General / Getting Started", helpViewXaml);
            Assert.Contains("Signal Reference", helpViewXaml);
            Assert.Equal(3, CountOccurrences(helpViewXaml, "NestedScrollViewer_PreviewMouseWheel"));
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
