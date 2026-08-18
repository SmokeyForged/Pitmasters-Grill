using PitmastersGrill.Diagnostics;
using PitmastersGrill.Persistence;
using PitmastersGrill.Providers;
using System;
using System.Windows.Threading;

namespace PitmastersGrill.Services
{
    public sealed class MainWindowRuntimeDependencies
    {
        public MainWindowRuntimeDependencies(
            AppSettingsService appSettingsService,
            MainWindowDiagnostics diagnostics,
            string databasePath,
            DatabaseBootstrap databaseBootstrap,
            BoardRowFactory boardRowFactory,
            NotesRepository notesRepository,
            WatchedPilotRepository watchedPilotRepository,
            PilotBoardRowDetailFormatter pilotBoardRowDetailFormatter,
            DetailPaneController detailPaneController,
            MainWindowAppearanceController mainWindowAppearanceController,
            MainWindowSettingsCoordinator mainWindowSettingsCoordinator,
            EveSessionContextCoordinator eveSessionContextCoordinator,
            EveSessionContextService eveSessionContextService,
            BoardDisplaySettingsController boardDisplaySettingsController,
            BoardColumnLayoutController boardColumnLayoutController,
            BoardColumnSettingsController boardColumnSettingsController,
            BoardColumnLayoutPersistenceController boardColumnLayoutPersistenceController,
            BoardSortController boardSortController,
            SettingsTabController settingsTabController,
            AnalysisTabController analysisTabController,
            MainWindowShellModeCoordinator mainWindowShellModeCoordinator,
            MainWindowInteropController mainWindowInteropController,
            WindowLayoutController windowLayoutController,
            IWindowWorkAreaProvider windowWorkAreaProvider,
            MainWindowNativeInputController mainWindowNativeInputController,
            INativeInputApi nativeInputApi,
            BoardPopulationStatusController boardPopulationStatusController,
            BoardPopulationRowProcessor boardPopulationRowProcessor,
            BoardPopulationPassController boardPopulationPassController,
            BoardPopulationRetryController boardPopulationRetryController,
            BoardPopulationEntryController boardPopulationEntryController,
            BoardPopulationSurface boardPopulationSurface,
            PilotDetailActionsPresenter pilotDetailActionsPresenter,
            BoardPopulationTimingMarkerTracker boardPopulationTimingMarkerTracker,
            IgnoreAllianceCoordinator ignoreAllianceCoordinator,
            IgnoreAllianceBoardController ignoreAllianceBoardController,
            ZkillUrlBuilder zkillUrlBuilder,
            BrowserLauncher browserLauncher)
        {
            AppSettingsService = appSettingsService ?? throw new ArgumentNullException(nameof(appSettingsService));
            Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            DatabasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
            DatabaseBootstrap = databaseBootstrap ?? throw new ArgumentNullException(nameof(databaseBootstrap));
            BoardRowFactory = boardRowFactory ?? throw new ArgumentNullException(nameof(boardRowFactory));
            NotesRepository = notesRepository ?? throw new ArgumentNullException(nameof(notesRepository));
            WatchedPilotRepository = watchedPilotRepository ?? throw new ArgumentNullException(nameof(watchedPilotRepository));
            PilotBoardRowDetailFormatter = pilotBoardRowDetailFormatter ?? throw new ArgumentNullException(nameof(pilotBoardRowDetailFormatter));
            DetailPaneController = detailPaneController ?? throw new ArgumentNullException(nameof(detailPaneController));
            MainWindowAppearanceController = mainWindowAppearanceController ?? throw new ArgumentNullException(nameof(mainWindowAppearanceController));
            MainWindowSettingsCoordinator = mainWindowSettingsCoordinator ?? throw new ArgumentNullException(nameof(mainWindowSettingsCoordinator));
            EveSessionContextCoordinator = eveSessionContextCoordinator ?? throw new ArgumentNullException(nameof(eveSessionContextCoordinator));
            EveSessionContextService = eveSessionContextService ?? throw new ArgumentNullException(nameof(eveSessionContextService));
            BoardDisplaySettingsController = boardDisplaySettingsController ?? throw new ArgumentNullException(nameof(boardDisplaySettingsController));
            BoardColumnLayoutController = boardColumnLayoutController ?? throw new ArgumentNullException(nameof(boardColumnLayoutController));
            BoardColumnSettingsController = boardColumnSettingsController ?? throw new ArgumentNullException(nameof(boardColumnSettingsController));
            BoardColumnLayoutPersistenceController = boardColumnLayoutPersistenceController ?? throw new ArgumentNullException(nameof(boardColumnLayoutPersistenceController));
            BoardSortController = boardSortController ?? throw new ArgumentNullException(nameof(boardSortController));
            SettingsTabController = settingsTabController ?? throw new ArgumentNullException(nameof(settingsTabController));
            AnalysisTabController = analysisTabController ?? throw new ArgumentNullException(nameof(analysisTabController));
            MainWindowShellModeCoordinator = mainWindowShellModeCoordinator ?? throw new ArgumentNullException(nameof(mainWindowShellModeCoordinator));
            MainWindowInteropController = mainWindowInteropController ?? throw new ArgumentNullException(nameof(mainWindowInteropController));
            WindowLayoutController = windowLayoutController ?? throw new ArgumentNullException(nameof(windowLayoutController));
            WindowWorkAreaProvider = windowWorkAreaProvider ?? throw new ArgumentNullException(nameof(windowWorkAreaProvider));
            MainWindowNativeInputController = mainWindowNativeInputController ?? throw new ArgumentNullException(nameof(mainWindowNativeInputController));
            NativeInputApi = nativeInputApi ?? throw new ArgumentNullException(nameof(nativeInputApi));
            BoardPopulationStatusController = boardPopulationStatusController ?? throw new ArgumentNullException(nameof(boardPopulationStatusController));
            BoardPopulationRowProcessor = boardPopulationRowProcessor ?? throw new ArgumentNullException(nameof(boardPopulationRowProcessor));
            BoardPopulationPassController = boardPopulationPassController ?? throw new ArgumentNullException(nameof(boardPopulationPassController));
            BoardPopulationRetryController = boardPopulationRetryController ?? throw new ArgumentNullException(nameof(boardPopulationRetryController));
            BoardPopulationEntryController = boardPopulationEntryController ?? throw new ArgumentNullException(nameof(boardPopulationEntryController));
            BoardPopulationSurface = boardPopulationSurface ?? throw new ArgumentNullException(nameof(boardPopulationSurface));
            PilotDetailActionsPresenter = pilotDetailActionsPresenter ?? throw new ArgumentNullException(nameof(pilotDetailActionsPresenter));
            BoardPopulationTimingMarkerTracker = boardPopulationTimingMarkerTracker ?? throw new ArgumentNullException(nameof(boardPopulationTimingMarkerTracker));
            IgnoreAllianceCoordinator = ignoreAllianceCoordinator ?? throw new ArgumentNullException(nameof(ignoreAllianceCoordinator));
            IgnoreAllianceBoardController = ignoreAllianceBoardController ?? throw new ArgumentNullException(nameof(ignoreAllianceBoardController));
            ZkillUrlBuilder = zkillUrlBuilder ?? throw new ArgumentNullException(nameof(zkillUrlBuilder));
            BrowserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
        }

        public AppSettingsService AppSettingsService { get; }
        public MainWindowDiagnostics Diagnostics { get; }
        public string DatabasePath { get; }
        public DatabaseBootstrap DatabaseBootstrap { get; }
        public BoardRowFactory BoardRowFactory { get; }
        public NotesRepository NotesRepository { get; }
        public WatchedPilotRepository WatchedPilotRepository { get; }
        public PilotBoardRowDetailFormatter PilotBoardRowDetailFormatter { get; }
        public DetailPaneController DetailPaneController { get; }
        public MainWindowAppearanceController MainWindowAppearanceController { get; }
        public MainWindowSettingsCoordinator MainWindowSettingsCoordinator { get; }
        public EveSessionContextCoordinator EveSessionContextCoordinator { get; }
        public EveSessionContextService EveSessionContextService { get; }
        public BoardDisplaySettingsController BoardDisplaySettingsController { get; }
        public BoardColumnLayoutController BoardColumnLayoutController { get; }
        public BoardColumnSettingsController BoardColumnSettingsController { get; }
        public BoardColumnLayoutPersistenceController BoardColumnLayoutPersistenceController { get; }
        public BoardSortController BoardSortController { get; }
        public SettingsTabController SettingsTabController { get; }
        public AnalysisTabController AnalysisTabController { get; }
        public MainWindowShellModeCoordinator MainWindowShellModeCoordinator { get; }
        public MainWindowInteropController MainWindowInteropController { get; }
        public WindowLayoutController WindowLayoutController { get; }
        public IWindowWorkAreaProvider WindowWorkAreaProvider { get; }
        public MainWindowNativeInputController MainWindowNativeInputController { get; }
        public INativeInputApi NativeInputApi { get; }
        public BoardPopulationStatusController BoardPopulationStatusController { get; }
        public BoardPopulationRowProcessor BoardPopulationRowProcessor { get; }
        public BoardPopulationPassController BoardPopulationPassController { get; }
        public BoardPopulationRetryController BoardPopulationRetryController { get; }
        public BoardPopulationEntryController BoardPopulationEntryController { get; }
        public BoardPopulationSurface BoardPopulationSurface { get; }
        public PilotDetailActionsPresenter PilotDetailActionsPresenter { get; }
        public BoardPopulationTimingMarkerTracker BoardPopulationTimingMarkerTracker { get; }
        public IgnoreAllianceCoordinator IgnoreAllianceCoordinator { get; }
        public IgnoreAllianceBoardController IgnoreAllianceBoardController { get; }
        public ZkillUrlBuilder ZkillUrlBuilder { get; }
        public BrowserLauncher BrowserLauncher { get; }
    }

    public static partial class ApplicationCompositionRoot
    {
        public const int DefaultBoardPopulationRetryDelaySeconds = 12;

        public static MainWindowRuntimeDependencies ComposeMainWindowRuntime(
            AppSettingsService appSettingsService,
            Dispatcher dispatcher)
        {
            ArgumentNullException.ThrowIfNull(appSettingsService);
            ArgumentNullException.ThrowIfNull(dispatcher);

            var diagnostics = new MainWindowDiagnostics(dispatcher);
            var mainWindowAppearanceController = new MainWindowAppearanceController(appSettingsService);
            var intelStorageSettingsController = new IntelStorageSettingsController(appSettingsService);
            var eveSessionContextCoordinator = new EveSessionContextCoordinator();
            var eveSessionContextService = new EveSessionContextService();
            var boardDisplaySettingsController = new BoardDisplaySettingsController();
            var boardColumnLayoutController = new BoardColumnLayoutController();
            var boardSortController = new BoardSortController();
            var boardColumnSettingsController = new BoardColumnSettingsController(
                boardColumnLayoutController,
                appSettingsService.Save);
            var boardColumnLayoutPersistenceController = new BoardColumnLayoutPersistenceController(
                boardColumnLayoutController,
                appSettingsService.Save);
            var settingsTabController = new SettingsTabController();
            var mainWindowSettingsCoordinator = new MainWindowSettingsCoordinator(
                mainWindowAppearanceController,
                intelStorageSettingsController,
                settingsTabController,
                boardDisplaySettingsController,
                appSettingsService.Save);
            var analysisTabController = new AnalysisTabController();
            var mainWindowShellModeCoordinator = new MainWindowShellModeCoordinator();
            var mainWindowInteropController = new MainWindowInteropController();
            var windowLayoutController = new WindowLayoutController();
            var windowWorkAreaProvider = new WindowsWindowWorkAreaProvider();
            var mainWindowNativeInputController = new MainWindowNativeInputController();
            var nativeInputApi = new Win32NativeInputApi();
            var boardPopulationStatusController = new BoardPopulationStatusController();
            var pilotDetailActionsPresenter = new PilotDetailActionsPresenter();
            var boardPopulationTimingMarkerTracker = new BoardPopulationTimingMarkerTracker();

            var localListParser = new LocalListParser();
            var clipboardPayloadInspector = new ClipboardPayloadInspector();
            var clipboardIngestService = new ClipboardIngestService(localListParser, clipboardPayloadInspector);
            var boardRowFactory = new BoardRowFactory();

            var boardPopulationRetryPolicy = new BoardPopulationRetryPolicy();
            var databasePath = AppPaths.GetDatabasePath();
            var databaseBootstrap = new DatabaseBootstrap(databasePath);
            var notesRepository = new NotesRepository(databasePath);
            var watchedPilotRepository = new WatchedPilotRepository(databasePath);
            var cynoModuleObservationRepository = new PilotCynoModuleObservationDayRepository(KillmailPaths.GetKillmailDatabasePath());
            var baitObservationRepository = new PilotBaitObservationDayRepository(KillmailPaths.GetKillmailDatabasePath());
            var cynoTackleObservationRepository = new PilotCynoTackleObservationDayRepository(KillmailPaths.GetKillmailDatabasePath());
            var pilotBoardRowDetailFormatter = new PilotBoardRowDetailFormatter(
                boardPopulationRetryPolicy,
                cynoModuleObservationRepository,
                baitObservationRepository,
                cynoTackleObservationRepository);
            var pilotBoardRowEnrichmentApplier = new PilotBoardRowEnrichmentApplier(DefaultBoardPopulationRetryDelaySeconds);
            var detailPaneController = new DetailPaneController(notesRepository, pilotBoardRowDetailFormatter);

            var resolverCacheRepository = new ResolverCacheRepository(databasePath);
            var statsCacheRepository = new StatsCacheRepository(databasePath);
            var zkillSearchResolverProvider = new ZkillSearchResolverProvider();
            var esiExactNameResolverProvider = new EsiExactNameResolverProvider();
            var esiPublicAffiliationProvider = new EsiPublicAffiliationProvider();
            var zkillStatsProvider = new ZkillStatsProvider();
            var resolverService = new ResolverService(resolverCacheRepository, zkillSearchResolverProvider, esiExactNameResolverProvider, esiPublicAffiliationProvider);
            var statsService = new StatsService(statsCacheRepository, zkillStatsProvider);

            var boardPopulationRowProcessor = new BoardPopulationRowProcessor(
                resolverService,
                statsService,
                pilotBoardRowEnrichmentApplier);
            var boardPopulationPassController = new BoardPopulationPassController(boardPopulationRetryPolicy);
            var boardPopulationRetryController = new BoardPopulationRetryController(
                boardPopulationRetryPolicy,
                diagnostics,
                DefaultBoardPopulationRetryDelaySeconds);
            var boardPopulationEntryController = new BoardPopulationEntryController(
                clipboardIngestService,
                resolverService,
                statsService,
                diagnostics,
                boardPopulationRetryController);
            var boardPopulationSurface = new BoardPopulationSurface(
                boardPopulationEntryController,
                boardPopulationPassController,
                boardPopulationRetryController,
                diagnostics);

            var ignoreAllianceListService = new IgnoreAllianceListService();
            var ignoreAllianceFilterService = new IgnoreAllianceFilterService();
            var ignoreAllianceCoordinator = new IgnoreAllianceCoordinator(
                ignoreAllianceListService,
                ignoreAllianceFilterService);
            var ignoreAllianceBoardController = new IgnoreAllianceBoardController(ignoreAllianceCoordinator);

            return new MainWindowRuntimeDependencies(
                appSettingsService,
                diagnostics,
                databasePath,
                databaseBootstrap,
                boardRowFactory,
                notesRepository,
                watchedPilotRepository,
                pilotBoardRowDetailFormatter,
                detailPaneController,
                mainWindowAppearanceController,
                mainWindowSettingsCoordinator,
                eveSessionContextCoordinator,
                eveSessionContextService,
                boardDisplaySettingsController,
                boardColumnLayoutController,
                boardColumnSettingsController,
                boardColumnLayoutPersistenceController,
                boardSortController,
                settingsTabController,
                analysisTabController,
                mainWindowShellModeCoordinator,
                mainWindowInteropController,
                windowLayoutController,
                windowWorkAreaProvider,
                mainWindowNativeInputController,
                nativeInputApi,
                boardPopulationStatusController,
                boardPopulationRowProcessor,
                boardPopulationPassController,
                boardPopulationRetryController,
                boardPopulationEntryController,
                boardPopulationSurface,
                pilotDetailActionsPresenter,
                boardPopulationTimingMarkerTracker,
                ignoreAllianceCoordinator,
                ignoreAllianceBoardController,
                new ZkillUrlBuilder(),
                new BrowserLauncher());
        }
    }
}
