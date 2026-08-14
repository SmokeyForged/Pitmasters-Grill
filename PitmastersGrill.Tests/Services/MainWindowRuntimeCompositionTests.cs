using PitmastersGrill.Services;
using System.Windows.Threading;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class MainWindowRuntimeCompositionTests
    {
        [Fact]
        public void ComposeMainWindowRuntime_UsesSharedSettingsAndBuildsNonControlGraph()
        {
            var appSettingsService = new AppSettingsService();
            var runtime = ApplicationCompositionRoot.ComposeMainWindowRuntime(
                appSettingsService,
                Dispatcher.CurrentDispatcher);

            Assert.Same(appSettingsService, runtime.AppSettingsService);
            Assert.NotNull(runtime.Diagnostics);
            Assert.NotNull(runtime.DatabaseBootstrap);
            Assert.NotNull(runtime.BoardPopulationSurface);
            Assert.NotNull(runtime.BoardPopulationRowProcessor);
            Assert.NotNull(runtime.MainWindowAppearanceController);
            Assert.NotNull(runtime.MainWindowSettingsCoordinator);
            Assert.NotNull(runtime.EveSessionContextService);
            Assert.NotNull(runtime.CacheMaintenanceService);
            Assert.NotNull(runtime.KillmailDerivedIntelRebuildService);
            Assert.NotNull(runtime.BrowserLauncher);

            // MainWindow owns its window-lifetime cleanup and disposes Diagnostics in OnClosed.
            runtime.Diagnostics.Dispose();
        }
    }
}
