using PitmastersGrill.Views;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class VersionUpdatePresentationOwnershipTests
    {
        [Fact]
        public void VersionUpdateView_LoadsIndependentlyAndOwnsManualUpdatePresentation()
        {
            RunSta(() =>
            {
                var view = new VersionUpdateView();
                var button = Assert.IsType<Button>(view.FindName("ManualUpdateCheckButton"));
                var status = Assert.IsType<TextBlock>(view.FindName("ManualUpdateStatusText"));
                var requestCount = 0;
                view.ManualUpdateCheckRequested += (_, _) => requestCount++;

                Assert.Equal("ManualUpdateCheckButton", AutomationProperties.GetAutomationId(button));
                Assert.Equal("ManualUpdateStatusText", AutomationProperties.GetAutomationId(status));
                Assert.Equal("Check for Updates", button.Content);
                Assert.Equal("Manual update check has not run this session.", status.Text);

                view.SetManualUpdateCheckEnabled(false);
                view.SetManualUpdateStatusText("Checking...");
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                Assert.False(button.IsEnabled);
                Assert.Equal("Checking...", status.Text);
                Assert.Equal(1, requestCount);
            });
        }

        [Fact]
        public void VersionUpdateView_PreservesReleaseNavigationAutomationAndSharedResourceContract()
        {
            var xaml = ReadRepoFile("PitmastersGrill", "Views", "VersionUpdateView.xaml");

            Assert.Contains("{x:Static services:AppReleaseMetadata.ReleaseLabel}", xaml, StringComparison.Ordinal);
            Assert.Contains("https://github.com/SmokeyForged/Pitmasters-Grill", xaml, StringComparison.Ordinal);
            Assert.Contains("AutomationProperties.AutomationId=\"GitHubRepositoryLink\"", xaml, StringComparison.Ordinal);
            Assert.Contains("AutomationProperties.AutomationId=\"ManualUpdateCheckButton\"", xaml, StringComparison.Ordinal);
            Assert.Contains("AutomationProperties.AutomationId=\"ManualUpdateStatusText\"", xaml, StringComparison.Ordinal);
            Assert.Contains("Source=\"/PitmastersGrill;component/Resources/MainWindowResources.xaml\"", xaml, StringComparison.Ordinal);
            Assert.Contains("Style=\"{StaticResource SettingsLabelStyle}\"", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("<Style x:Key=\"SettingsLabelStyle\"", xaml, StringComparison.Ordinal);
        }

        [Fact]
        public void MainWindow_ComposesFocusedVersionViewWithoutRawVersionControls()
        {
            var xaml = ReadRepoFile("PitmastersGrill", "MainWindow.xaml");
            var constructor = ReadRepoFile("PitmastersGrill", "MainWindow.ComposedConstructor.cs");
            var navigation = ReadRepoFile("PitmastersGrill", "MainWindow.ExternalNavigation.cs");
            var controller = ReadRepoFile("PitmastersGrill", "Services", "ManualUpdateCheckController.cs");

            Assert.Contains("<views:VersionUpdateView x:Name=\"VersionUpdateViewControl\"", xaml, StringComparison.Ordinal);
            Assert.Contains("RepositoryNavigateRequested=\"VersionUpdateView_RepositoryNavigateRequested\"", xaml, StringComparison.Ordinal);
            Assert.Contains("ManualUpdateCheckRequested=\"VersionUpdateView_ManualUpdateCheckRequested\"", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("x:Name=\"ManualUpdateCheckButton\"", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("x:Name=\"ManualUpdateStatusText\"", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("GitHubRepoLink_RequestNavigate", xaml, StringComparison.Ordinal);

            Assert.Contains("VersionUpdateViewControl.SetManualUpdateCheckEnabled", constructor, StringComparison.Ordinal);
            Assert.Contains("VersionUpdateViewControl.SetManualUpdateStatusText", constructor, StringComparison.Ordinal);
            Assert.Contains("OpenManualUpdateReleasePage", constructor, StringComparison.Ordinal);
            Assert.Contains("ExternalNavigation.OpenUrl(url, \"GitHub repository\")", navigation, StringComparison.Ordinal);
            Assert.Contains("ExternalNavigation.OpenUrl(url, \"manual update release page\")", navigation, StringComparison.Ordinal);

            Assert.DoesNotContain("System.Windows.Controls", controller, StringComparison.Ordinal);
            Assert.DoesNotContain("BrowserLauncher", controller, StringComparison.Ordinal);
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
