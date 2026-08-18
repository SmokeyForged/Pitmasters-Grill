using PitmastersGrill.Services;
using PitmastersGrill.Views;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class BoardStatusPresentationOwnershipTests
    {
        [Fact]
        public void BoardPopulationStatusController_EmitsSemanticTextAndVisualState()
        {
            var subject = new BoardPopulationStatusController();
            var resources = new ResourceDictionary
            {
                ["MutedTextBrush"] = Brushes.Gray
            };
            var text = string.Empty;
            Brush? foreground = null;

            subject.UpdateStatus(
                "Retrying board population",
                BoardPopulationStatusKind.Warning,
                value => text = value,
                value => foreground = value,
                resources);

            Assert.Equal("Retrying board population", text);
            Assert.Equal(BoardPopulationStatusKind.Warning, subject.CurrentKind);
            Assert.NotNull(foreground);
            Assert.NotEqual(Brushes.Gray, foreground);

            foreground = null;
            subject.ApplyStatusVisual(value => foreground = value, resources);
            Assert.NotNull(foreground);
        }

        [Fact]
        public void BoardStatusView_LoadsAndOwnsStatusControlsWithStableAutomationIds()
        {
            RunSta(() =>
            {
                var view = new BoardStatusView();
                var population = Assert.IsType<TextBlock>(view.FindName("BoardPopulationStatusText"));
                var refreshed = Assert.IsType<TextBlock>(view.FindName("LastRefreshedText"));
                var summary = Assert.IsType<TextBlock>(view.FindName("BoardSummaryText"));
                var footer = Assert.IsType<Border>(view.FindName("BoardStatusFooter"));

                view.SetPopulationStatusText("Population complete");
                view.SetPopulationStatusForeground(Brushes.Green);
                view.SetLastRefreshedText("Last Refreshed: deterministic");
                view.SetSummaryText("Visible 2 | Watched 1");

                Assert.Same(footer, view.FooterBorder);
                Assert.Equal("Population complete", population.Text);
                Assert.Same(Brushes.Green, population.Foreground);
                Assert.Equal("Last Refreshed: deterministic", refreshed.Text);
                Assert.Equal("Visible 2 | Watched 1", summary.Text);
                Assert.Equal("BoardStatusFooter", AutomationProperties.GetAutomationId(footer));
                Assert.Equal("BoardPopulationStatusText", AutomationProperties.GetAutomationId(population));
                Assert.Equal("LastRefreshedText", AutomationProperties.GetAutomationId(refreshed));
                Assert.Equal("BoardSummaryText", AutomationProperties.GetAutomationId(summary));
            });
        }

        [Fact]
        public void MainWindow_ComposesFocusedStatusViewWithoutRawStatusControlOwnership()
        {
            var xaml = ReadRepoFile("PitmastersGrill", "MainWindow.xaml");
            var source = ReadRepoFile("PitmastersGrill", "MainWindow.xaml.cs");
            var constructorSource = ReadRepoFile("PitmastersGrill", "MainWindow.ComposedConstructor.cs");
            var partialSource = ReadRepoFile("PitmastersGrill", "MainWindow.BoardStatusView.cs");

            Assert.Contains("<views:BoardStatusView x:Name=\"BoardStatusViewControl\"", xaml, StringComparison.Ordinal);
            Assert.Contains("Grid.Row=\"2\"", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("x:Name=\"BoardPopulationStatusText\"", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("x:Name=\"LastRefreshedText\"", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("x:Name=\"BoardSummaryText\"", xaml, StringComparison.Ordinal);

            Assert.Contains("BoardStatusViewControl.SetPopulationStatusText", source, StringComparison.Ordinal);
            Assert.Contains("BoardStatusViewControl.SetPopulationStatusForeground", source, StringComparison.Ordinal);
            Assert.Contains("BoardStatusViewControl.SetLastRefreshedText", source, StringComparison.Ordinal);
            Assert.Contains("BoardStatusViewControl.FooterBorder", constructorSource, StringComparison.Ordinal);
            Assert.Contains("BoardStatusViewControl.SetSummaryText", constructorSource, StringComparison.Ordinal);

            Assert.DoesNotContain("BoardPopulationStatusText =>", partialSource, StringComparison.Ordinal);
            Assert.DoesNotContain("LastRefreshedText =>", partialSource, StringComparison.Ordinal);
            Assert.DoesNotContain("BoardSummaryText =>", partialSource, StringComparison.Ordinal);
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
