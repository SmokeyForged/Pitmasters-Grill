using PitmastersGrill.Persistence;
using PitmastersGrill.Providers;
using System;

namespace PitmastersGrill.Services
{
    public sealed class ApplicationRuntimeDependencies
    {
        public ApplicationRuntimeDependencies(
            AppSettingsService appSettingsService,
            KillmailDatabaseBootstrap killmailDatabaseBootstrap,
            KillmailDatasetMetadataRepository killmailDatasetMetadataRepository,
            BackgroundIntelUpdateService backgroundIntelUpdateService)
        {
            AppSettingsService = appSettingsService ?? throw new ArgumentNullException(nameof(appSettingsService));
            KillmailDatabaseBootstrap = killmailDatabaseBootstrap ?? throw new ArgumentNullException(nameof(killmailDatabaseBootstrap));
            KillmailDatasetMetadataRepository = killmailDatasetMetadataRepository ?? throw new ArgumentNullException(nameof(killmailDatasetMetadataRepository));
            BackgroundIntelUpdateService = backgroundIntelUpdateService ?? throw new ArgumentNullException(nameof(backgroundIntelUpdateService));
        }

        public AppSettingsService AppSettingsService { get; }
        public KillmailDatabaseBootstrap KillmailDatabaseBootstrap { get; }
        public KillmailDatasetMetadataRepository KillmailDatasetMetadataRepository { get; }
        public BackgroundIntelUpdateService BackgroundIntelUpdateService { get; }
    }

    public static partial class ApplicationCompositionRoot
    {
        public static ApplicationRuntimeDependencies ComposeNormalRuntime(AppSettingsService appSettingsService)
        {
            ArgumentNullException.ThrowIfNull(appSettingsService);

            var killmailDbPath = KillmailPaths.GetKillmailDatabasePath();
            var killmailBootstrap = new KillmailDatabaseBootstrap(killmailDbPath);
            var metadataRepository = new KillmailDatasetMetadataRepository(killmailDbPath);
            var dayImportStateRepository = new DayImportStateRepository(killmailDbPath);
            var archiveProvider = new KillmailDayArchiveProvider();
            var freshnessService = new KillmailDatasetFreshnessService(metadataRepository);
            var writeGate = new KillmailDbWriteGate();
            var incrementalImportService = new KillmailIncrementalImportService(killmailDbPath, writeGate);
            var dayImportService = new KillmailDayImportService(
                writeGate,
                dayImportStateRepository,
                metadataRepository,
                archiveProvider);
            var r2z2LiveKillmailService = new R2Z2LiveKillmailService(appSettingsService, incrementalImportService);
            var todaysFreshnessService = new TodaysFreshnessService(incrementalImportService);
            var historicalFreshnessService = new HistoricalFreshnessService(incrementalImportService, appSettingsService);

            var backgroundIntelUpdateService = new BackgroundIntelUpdateService(
                freshnessService,
                writeGate,
                dayImportService,
                metadataRepository,
                r2z2LiveKillmailService,
                todaysFreshnessService,
                historicalFreshnessService);

            return new ApplicationRuntimeDependencies(
                appSettingsService,
                killmailBootstrap,
                metadataRepository,
                backgroundIntelUpdateService);
        }
    }
}
