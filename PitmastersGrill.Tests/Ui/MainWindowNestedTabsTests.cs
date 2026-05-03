using System;
using System.IO;
using Xunit;

namespace PitmastersGrill.Tests.Ui
{
    public sealed class MainWindowNestedTabsTests
    {
        [Fact]
        public void MainWindowXaml_DefinesSharedNestedTabStyles()
        {
            var xaml = ReadMainWindowXaml();

            Assert.Contains("PmgNestedSubTabControlStyle", xaml);
            Assert.Contains("PmgNestedSubTabItemStyle", xaml);
            Assert.Contains("PMG nested sub-tab shared styles", xaml);
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
            var current = new DirectoryInfo(AppContext.BaseDirectory);

            while (current is not null)
            {
                var candidate = Path.Combine(current.FullName, "PitmastersGrill", "MainWindow.xaml");

                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }

                current = current.Parent;
            }

            throw new FileNotFoundException("Could not locate PitmastersGrill/MainWindow.xaml from the test output directory.");
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
