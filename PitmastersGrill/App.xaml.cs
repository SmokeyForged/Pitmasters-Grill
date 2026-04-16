using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using PitmastersGrill.Providers;
using PitmastersGrill.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PitmastersGrill
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                if (IsSeedBuildMode(e.Args))
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown;
                    await RunSeedBuildModeAsync(e.Args);
                    Shutdown();
                    return;
                }

                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                await RunNormalStartupAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"PMG failed during startup.\n\n{ex.Message}",
                    "PMG Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown();
            }
        }

        private static bool IsSeedBuildMode(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return false;
            }

            return args.Any(arg =>
                string.Equals(arg, "--build-seed-6m", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--build-seed-range", StringComparison.OrdinalIgnoreCase));
        }

        private async Task RunSeedBuildModeAsync(string[] args)
        {
            var killmailDbPath = KillmailPaths.GetKillmailDatabasePath();

            var killmailBootstrap = new KillmailDatabaseBootstrap(killmailDbPath);
            killmailBootstrap.Initialize();

            var metadataRepository = new KillmailDatasetMetadataRepository(killmailDbPath);
            var dayImportStateRepository = new DayImportStateRepository(killmailDbPath);
            var archiveProvider = new KillmailDayArchiveProvider();

            var dayImportService = new KillmailDayImportService(
                dayImportStateRepository,
                metadataRepository,
                archiveProvider);

            var rangeImportService = new KillmailDayRangeImportService(dayImportService);
            var seedWindowPolicyService = new SeedWindowPolicyService();
            var seedBuildService = new SeedBuildService(rangeImportService, metadataRepository);

            var seedVersion = $"seed-{DateTime.UtcNow:yyyyMMddHHmmss}";
            SeedWindowResult window;

            if (args.Any(arg => string.Equals(arg, "--build-seed-range", StringComparison.OrdinalIgnoreCase)))
            {
                if (args.Length < 3)
                {
                    throw new InvalidOperationException("Seed range mode requires start and end days: --build-seed-range YYYY-MM-DD YYYY-MM-DD");
                }

                var startDayUtc = args[1];
                var endDayUtc = args[2];
                window = seedWindowPolicyService.GetExplicitWindowUtc(startDayUtc, endDayUtc);
            }
            else
            {
                window = seedWindowPolicyService.GetSixMonthSeedWindowUtc();
            }

            var progressWindow = new Views.StartupSplashWindow();
            progressWindow.Show();

            try
            {
                var result = await seedBuildService.BuildSeedAsync(
                    seedVersion,
                    window.StartDayUtc,
                    window.EndDayUtc,
                    progress => progressWindow.Dispatcher.InvokeAsync(() =>
                    {
                        progressWindow.ApplyState(new StartupUpdateState
                        {
                            StatusText = progress.StatusText,
                            DetailText = progress.DetailText,
                            IsIndeterminate = false,
                            ProgressValue = progress.ProgressValue,
                            IsExceptionMessage = false
                        });
                    }).Task,
                    CancellationToken.None);

                if (!result.Success)
                {
                    throw new InvalidOperationException(result.Error);
                }

                MessageBox.Show(
                    $"Seed build complete.\n\n" +
                    $"Seed Version: {result.SeedVersion}\n" +
                    $"Window: {result.StartDayUtc} → {result.EndDayUtc}\n" +
                    $"Imported Days: {result.ImportedDays}\n" +
                    $"Killmails: {result.ImportedKillmailCount}\n" +
                    $"Unique Pilots: {result.UniquePilotCount}\n" +
                    $"Fleet Observation Pilots: {result.FleetObservationPilotCount}\n" +
                    $"Ship Observation Pilots: {result.ShipObservationPilotCount}",
                    "PMG Seed Build Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            finally
            {
                progressWindow.Close();
            }
        }

        private async Task RunNormalStartupAsync()
        {
            var splash = new Views.StartupSplashWindow();
            splash.ApplyState(new StartupUpdateState
            {
                StatusText = "Checking local intel database",
                DetailText = "Initializing PMG and reviewing local intel freshness.",
                IsIndeterminate = true,
                ProgressValue = 0,
                IsExceptionMessage = false
            });
            splash.Show();

            try
            {
                var killmailDbPath = KillmailPaths.GetKillmailDatabasePath();

                var killmailBootstrap = new KillmailDatabaseBootstrap(killmailDbPath);
                killmailBootstrap.Initialize();

                var metadataRepository = new KillmailDatasetMetadataRepository(killmailDbPath);
                var dayImportStateRepository = new DayImportStateRepository(killmailDbPath);
                var archiveProvider = new KillmailDayArchiveProvider();
                var freshnessService = new KillmailDatasetFreshnessService(metadataRepository);
                var dayImportService = new KillmailDayImportService(
                    dayImportStateRepository,
                    metadataRepository,
                    archiveProvider);

                var backgroundIntelUpdateService = new BackgroundIntelUpdateService(
                    freshnessService,
                    dayImportService);

                metadataRepository.SetUtcNow("last_startup_check_at_utc");

                var mainWindow = new MainWindow(backgroundIntelUpdateService);
                MainWindow = mainWindow;

                ShutdownMode = ShutdownMode.OnMainWindowClose;
                mainWindow.Show();
                splash.Close();

                backgroundIntelUpdateService.StartIfNeeded();

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                try
                {
                    await splash.Dispatcher.InvokeAsync(() =>
                    {
                        splash.ApplyState(new StartupUpdateState
                        {
                            StatusText = "Startup failed",
                            DetailText = ex.Message,
                            IsIndeterminate = false,
                            ProgressValue = 0,
                            IsExceptionMessage = true
                        });
                    });
                }
                catch
                {
                }

                MessageBox.Show(
                    $"PMG failed during startup.\n\n{ex.Message}",
                    "PMG Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                try
                {
                    splash.Close();
                }
                catch
                {
                }

                Shutdown();
            }
        }
    }
}