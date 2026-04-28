using PitmastersGrill.Diagnostics;
using PitmastersGrill.Persistence;
using PitmastersGrill.Providers;
using System;

namespace PitmastersGrill.Services
{
    public sealed class MainWindowComposedDependencies
    {
        public MainWindowComposedDependencies(
            string databasePath,
            DatabaseBootstrap databaseBootstrap,
            BoardRowFactory boardRowFactory,
            NotesRepository notesRepository,
            PilotBoardRowDetailFormatter pilotBoardRowDetailFormatter,
            DetailPaneController detailPaneController,
            BoardPopulationRowProcessor boardPopulationRowProcessor,
            BoardPopulationPassController boardPopulationPassController,
            BoardPopulationRetryController boardPopulationRetryController,
            BoardPopulationEntryController boardPopulationEntryController,
            IgnoreAllianceCoordinator ignoreAllianceCoordinator,
            IgnoreAllianceBoardController ignoreAllianceBoardController,
            ZkillUrlBuilder zkillUrlBuilder,
            BrowserLauncher browserLauncher)
        {
            DatabasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
            DatabaseBootstrap = databaseBootstrap ?? throw new ArgumentNullException(nameof(databaseBootstrap));
            BoardRowFactory = boardRowFactory ?? throw new ArgumentNullException(nameof(boardRowFactory));
            NotesRepository = notesRepository ?? throw new ArgumentNullException(nameof(notesRepository));
            PilotBoardRowDetailFormatter = pilotBoardRowDetailFormatter ?? throw new ArgumentNullException(nameof(pilotBoardRowDetailFormatter));
            DetailPaneController = detailPaneController ?? throw new ArgumentNullException(nameof(detailPaneController));
            BoardPopulationRowProcessor = boardPopulationRowProcessor ?? throw new ArgumentNullException(nameof(boardPopulationRowProcessor));
            BoardPopulationPassController = boardPopulationPassController ?? throw new ArgumentNullException(nameof(boardPopulationPassController));
            BoardPopulationRetryController = boardPopulationRetryController ?? throw new ArgumentNullException(nameof(boardPopulationRetryController));
            BoardPopulationEntryController = boardPopulationEntryController ?? throw new ArgumentNullException(nameof(boardPopulationEntryController));
            IgnoreAllianceCoordinator = ignoreAllianceCoordinator ?? throw new ArgumentNullException(nameof(ignoreAllianceCoordinator));
            IgnoreAllianceBoardController = ignoreAllianceBoardController ?? throw new ArgumentNullException(nameof(ignoreAllianceBoardController));
            ZkillUrlBuilder = zkillUrlBuilder ?? throw new ArgumentNullException(nameof(zkillUrlBuilder));
            BrowserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
        }

        public string DatabasePath { get; }
        public DatabaseBootstrap DatabaseBootstrap { get; }
        public BoardRowFactory BoardRowFactory { get; }
        public NotesRepository NotesRepository { get; }
        public PilotBoardRowDetailFormatter PilotBoardRowDetailFormatter { get; }
        public DetailPaneController DetailPaneController { get; }
        public BoardPopulationRowProcessor BoardPopulationRowProcessor { get; }
        public BoardPopulationPassController BoardPopulationPassController { get; }
        public BoardPopulationRetryController BoardPopulationRetryController { get; }
        public BoardPopulationEntryController BoardPopulationEntryController { get; }
        public IgnoreAllianceCoordinator IgnoreAllianceCoordinator { get; }
        public IgnoreAllianceBoardController IgnoreAllianceBoardController { get; }
        public ZkillUrlBuilder ZkillUrlBuilder { get; }
        public BrowserLauncher BrowserLauncher { get; }
    }

    public static class MainWindowCompositionRoot
    {
        public static MainWindowComposedDependencies Compose(
            MainWindowDiagnostics diagnostics,
            AppSettingsService appSettingsService,
            MainWindowAppearanceController mainWindowAppearanceController,
            BoardPopulationStatusController boardPopulationStatusController,
            int defaultBoardPopulationRetryDelaySeconds)
        {
            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            if (appSettingsService == null)
            {
                throw new ArgumentNullException(nameof(appSettingsService));
            }

            if (mainWindowAppearanceController == null)
            {
                throw new ArgumentNullException(nameof(mainWindowAppearanceController));
            }

            if (boardPopulationStatusController == null)
            {
                throw new ArgumentNullException(nameof(boardPopulationStatusController));
            }

            var localListParser = new LocalListParser();
            var clipboardPayloadInspector = new ClipboardPayloadInspector();
            var clipboardIngestService = new ClipboardIngestService(localListParser, clipboardPayloadInspector);
            var boardRowFactory = new BoardRowFactory();

            var boardPopulationRetryPolicy = new BoardPopulationRetryPolicy();
            var databasePath = AppPaths.GetDatabasePath();
            var databaseBootstrap = new DatabaseBootstrap(databasePath);
            var notesRepository = new NotesRepository(databasePath);
            var cynoModuleObservationRepository = new PilotCynoModuleObservationDayRepository(KillmailPaths.GetKillmailDatabasePath());
            var pilotBoardRowDetailFormatter = new PilotBoardRowDetailFormatter(
                boardPopulationRetryPolicy,
                cynoModuleObservationRepository);
            var pilotBoardRowEnrichmentApplier = new PilotBoardRowEnrichmentApplier(defaultBoardPopulationRetryDelaySeconds);
            var detailPaneController = new DetailPaneController(notesRepository, pilotBoardRowDetailFormatter);

            var resolverCacheRepository = new ResolverCacheRepository(databasePath);
            var statsCacheRepository = new StatsCacheRepository(databasePath);
            var zkillSearchResolverProvider = new ZkillSearchResolverProvider();
            var esiExactNameResolverProvider = new EsiExactNameResolverProvider();
            var esiPublicAffiliationProvider = new EsiPublicAffiliationProvider();
            var zkillStatsProvider = new ZkillStatsProvider();
            var resolverService = new ResolverService(
                resolverCacheRepository,
                zkillSearchResolverProvider,
                esiExactNameResolverProvider,
                esiPublicAffiliationProvider);
            var statsService = new StatsService(
                statsCacheRepository,
                zkillStatsProvider);

            var boardPopulationRowProcessor = new BoardPopulationRowProcessor(
                resolverService,
                statsService,
                pilotBoardRowEnrichmentApplier);
            var boardPopulationPassController = new BoardPopulationPassController(boardPopulationRetryPolicy);
            var boardPopulationRetryController = new BoardPopulationRetryController(
                boardPopulationRetryPolicy,
                diagnostics,
                defaultBoardPopulationRetryDelaySeconds);
            var boardPopulationEntryController = new BoardPopulationEntryController(
                clipboardIngestService,
                resolverService,
                statsService,
                diagnostics,
                boardPopulationRetryController);

            var ignoreAllianceListService = new IgnoreAllianceListService();
            var ignoreAllianceFilterService = new IgnoreAllianceFilterService();
            var ignoreAllianceCoordinator = new IgnoreAllianceCoordinator(
                ignoreAllianceListService,
                ignoreAllianceFilterService);
            var ignoreAllianceBoardController = new IgnoreAllianceBoardController(ignoreAllianceCoordinator);

            var zkillUrlBuilder = new ZkillUrlBuilder();
            var browserLauncher = new BrowserLauncher();

            return new MainWindowComposedDependencies(
                databasePath,
                databaseBootstrap,
                boardRowFactory,
                notesRepository,
                pilotBoardRowDetailFormatter,
                detailPaneController,
                boardPopulationRowProcessor,
                boardPopulationPassController,
                boardPopulationRetryController,
                boardPopulationEntryController,
                ignoreAllianceCoordinator,
                ignoreAllianceBoardController,
                zkillUrlBuilder,
                browserLauncher);
        }
    }
}
