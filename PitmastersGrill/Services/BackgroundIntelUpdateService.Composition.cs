using PitmastersGrill.Persistence;
using System;

namespace PitmastersGrill.Services
{
    public partial class BackgroundIntelUpdateService
    {
        public BackgroundIntelUpdateService(
            KillmailDatasetFreshnessService freshnessService,
            KillmailDbWriteGate writeGate,
            KillmailDayImportService killmailDayImportService,
            KillmailDatasetMetadataRepository metadataRepository,
            R2Z2LiveKillmailService r2z2LiveKillmailService,
            TodaysFreshnessService todaysFreshnessService,
            HistoricalFreshnessService historicalFreshnessService)
        {
            freshnessService = freshnessService ?? throw new ArgumentNullException(nameof(freshnessService));
            killmailDayImportService = killmailDayImportService ?? throw new ArgumentNullException(nameof(killmailDayImportService));
            _writeGate = writeGate ?? throw new ArgumentNullException(nameof(writeGate));
            _metadataRepository = metadataRepository ?? throw new ArgumentNullException(nameof(metadataRepository));
            _r2z2LiveKillmailService = r2z2LiveKillmailService ?? throw new ArgumentNullException(nameof(r2z2LiveKillmailService));
            _todaysFreshnessService = todaysFreshnessService ?? throw new ArgumentNullException(nameof(todaysFreshnessService));
            _historicalFreshnessService = historicalFreshnessService ?? throw new ArgumentNullException(nameof(historicalFreshnessService));

            _foregroundFreshnessCoordinator = new ForegroundFreshnessCoordinator();
            _archiveSyncWorker = new ArchiveSyncWorker(
                freshnessService,
                killmailDayImportService,
                _foregroundFreshnessCoordinator,
                _shutdownCts.Token);
            _backgroundHistoricalRepairScheduler = new BackgroundHistoricalRepairScheduler(
                _historicalFreshnessService,
                _foregroundFreshnessCoordinator,
                _shutdownCts.Token);
            _statusAggregator = new IntelUpdateStatusAggregator(
                freshnessService,
                _metadataRepository,
                _r2z2LiveKillmailService,
                _todaysFreshnessService,
                _historicalFreshnessService);

            _archiveSyncWorker.StateChanged += OnArchiveSyncStateChanged;
            _foregroundFreshnessCoordinator.PriorityChanged += OnForegroundPriorityChanged;
            _r2z2LiveKillmailService.StatusChanged += OnLiveFeedStatusChanged;
            _todaysFreshnessService.StatusChanged += OnTodaysFreshnessStatusChanged;
            _historicalFreshnessService.StatusChanged += OnHistoricalFreshnessStatusChanged;
        }
    }
}
