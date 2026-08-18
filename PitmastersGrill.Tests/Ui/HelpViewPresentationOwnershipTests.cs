using PitmastersGrill.Views;
using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Controls;
using Xunit;

namespace PitmastersGrill.Tests.Ui
{
    public sealed class HelpViewPresentationOwnershipTests
    {
        [Fact]
        public void HelpView_LoadsIndependentlyWithExpectedContentAndAutomationContract()
        {
            RunSta(() =>
            {
                var view = new HelpView();
                var tabs = Assert.IsType<TabControl>(view.FindName("HelpTabControl"));
                var generalTab = Assert.IsType<TabItem>(tabs.Items[0]);
                var signalTab = Assert.IsType<TabItem>(tabs.Items[1]);
                var outerScroll = Assert.IsType<ScrollViewer>(generalTab.Content);
                var outerPanel = Assert.IsType<StackPanel>(outerScroll.Content);
                var innerScroll = Assert.IsType<ScrollViewer>(outerPanel.Children[0]);
                var generalPanel = Assert.IsType<StackPanel>(innerScroll.Content);
                var helpHeading = Assert.IsType<TextBlock>(generalPanel.Children[0]);
                var signalScroll = Assert.IsType<ScrollViewer>(signalTab.Content);
                var signalPanel = Assert.IsType<StackPanel>(signalScroll.Content);
                var signalHeading = Assert.IsType<TextBlock>(signalPanel.Children[0]);

                Assert.Equal("HelpTabControl", AutomationProperties.GetAutomationId(tabs));
                Assert.Equal("General / Getting Started", generalTab.Header);
                Assert.Equal("Signal Reference", signalTab.Header);
                Assert.Equal("PMG Help", helpHeading.Text);
                Assert.Equal("PMG Signal Reference", signalHeading.Text);
                Assert.Equal("GeneralHelpOuterScrollViewer", AutomationProperties.GetAutomationId(outerScroll));
                Assert.Equal("GeneralHelpInnerScrollViewer", AutomationProperties.GetAutomationId(innerScroll));
                Assert.Equal("SignalReferenceScrollViewer", AutomationProperties.GetAutomationId(signalScroll));
                Assert.Equal(2, tabs.Items.Count);
            });
        }

        [Fact]
        public void HelpView_OwnsSharedResourcesContentAndNestedScrollBehavior()
        {
            var xaml = ReadRepoFile("PitmastersGrill", "Views", "HelpView.xaml");
            var source = ReadRepoFile("PitmastersGrill", "Views", "HelpView.xaml.cs");

            Assert.Contains("Source=\"/PitmastersGrill;component/Resources/MainWindowResources.xaml\"", xaml, StringComparison.Ordinal);
            Assert.Contains("Style=\"{StaticResource PmgNestedSubTabControlStyle}\"", xaml, StringComparison.Ordinal);
            Assert.Equal(2, CountOccurrences(xaml, "Style=\"{StaticResource PmgNestedSubTabItemStyle}\""));
            Assert.Equal(3, CountOccurrences(xaml, "PreviewMouseWheel=\"NestedScrollViewer_PreviewMouseWheel\""));
            Assert.DoesNotContain("HelpSubTabControlStyle", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("HelpSubTabItemStyle", xaml, StringComparison.Ordinal);
            Assert.Contains("Quick reference for common PMG shortcuts and interactions.", xaml, StringComparison.Ordinal);
            Assert.Contains("PMG signals are public historical evidence summaries.", xaml, StringComparison.Ordinal);

            Assert.Contains("scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);", source, StringComparison.Ordinal);
            Assert.Contains("e.Handled = true;", source, StringComparison.Ordinal);
        }

        [Fact]
        public void MainWindow_ComposesHelpViewWithoutInlineHelpPresentationOrHandler()
        {
            var xaml = ReadRepoFile("PitmastersGrill", "MainWindow.xaml");
            var source = ReadRepoFile("PitmastersGrill", "MainWindow.xaml.cs");

            Assert.Contains("<views:HelpView x:Name=\"HelpViewControl\"", xaml, StringComparison.Ordinal);
            Assert.Contains("Header=\"Help\"", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("General / Getting Started", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("Signal Reference", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("HelpSubTabControlStyle", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("HelpSubTabItemStyle", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("NestedScrollViewer_PreviewMouseWheel", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("NestedScrollViewer_PreviewMouseWheel", source, StringComparison.Ordinal);
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

        private static int CountOccurrences(string source, string value)
        {
            var count = 0;
            var offset = 0;
            while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += value.Length;
            }

            return count;
        }

        private static void RunSta(Action action)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }
    }
}
