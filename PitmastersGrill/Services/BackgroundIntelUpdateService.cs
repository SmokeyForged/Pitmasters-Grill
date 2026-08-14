using Microsoft.Data.Sqlite;
using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersGrill.Services
{
    public partial class BackgroundIntelUpdateService
    {
        private const string ForegroundFreshnessBusyMessage = "Another freshness operation is already running.";

        private readonly object _sync = new();
        private readonly KillmailDbWriteGate _writeGate;
        private readonly KillmailDatasetMetadataRepository _metadataRepository;
        private readonly R2Z2LiveKillmailService _r2z2LiveKillmailService;
        private readonly TodaysFreshnessService _todaysFreshnessService;
        private readonly HistoricalFreshnessService _historicalFreshnessService;
        private readonly ForegroundFreshnessCoordinator _foregroundFreshnessCoordinator;
        private readonly ArchiveSyncWorker _archiveSyncWorker;
        private readonly BackgroundHistoricalRepairScheduler _backgroundHistoricalRepairScheduler;
        private readonly IntelUpdateStatusAggregator _statusAggregator;
        private readonly CancellationTokenSource _shutdownCts = new();

        public event Action<IntelUpdateStatusSnapshot>? StatusChanged;

        public BackgroundIntelUpdateService(
            KillmailDatasetFreshnessService freshnessService,
            KillmailDbWriteGate writeGate,
            KillmailDayImportService killmailDayImportService,
            R2Z2LiveKillmailService r2z2LiveKillmailService,
            TodaysFreshnessService todaysFreshnessService,
            HistoricalFreshnessService historicalFreshnessService)
            : this(
                freshnessService,
                writeGate,
                killmailDayImportService,
                new KillmailDatasetMetadataRepository(KillmailPaths.GetKillmailDatabasePath()),
                r2z2LiveKillmailService,
                todaysFreshnessService,
                historicalFreshnessService)
        {
        }

        public IntelUpdateStatusSnapshot GetSnapshot()
        {
            lock (_sync)
            {
                return BuildSnapshot();
            }
        }

        public void StartIfNeeded()
        {
            _archiveSyncWorker.StartIfNeeded();
        }

        public Task SetLiveFeedEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        {
            return _r2z2LiveKillmailService.SetEnabledAsync(enabled, cancellationToken);
        }

        public Task<TodaysFreshnessRunResult> RunTodaysFreshnessAsync(
            IReadOnlyCollection<long> characterIds,
            CancellationToken cancellationToken = default)
        {
            return _foregroundFreshnessCoordinator.RunExclusiveAsync(
                () => _todaysFreshnessService.RunAsync(characterIds, cancellationToken),
                () => CreateForegroundFreshnessBusyTodaysResult(characterIds),
                "Today's Freshness start skipped because another foreground freshness operation is already active.",
                cancellationToken);
        }

        public Task<HistoricalFreshnessRunResult> RunHistoricalFreshnessAsync(
            IReadOnlyCollection<long> characterIds,
            CancellationToken cancellationToken = default)
        {
            return _foregroundFreshnessCoordinator.RunExclusiveAsync(
                () => _historicalFreshnessService.RunAsync(characterIds, cancellationToken),
                () => CreateForegroundFreshnessBusyHistoricalResult(characterIds),
                "Historical Freshness start skipped because another foreground freshness operation is already active.",
                cancellationToken);
        }

        public void ScheduleBackgroundHistoricalRepairAfterUiShown(
            Func<IReadOnlyCollection<long>> visibleCharacterIdsProvider)
        {
            _backgroundHistoricalRepairScheduler.ScheduleAfterUiShown(visibleCharacterIdsProvider);
        }

        public async Task EnableKillmailDbPullAsync(
            int lookbackDays,
            CancellationToken cancellationToken = default)
        {
            var normalizedLookbackDays = KillmailDatasetFreshnessService.NormalizeMaxKillmailAgeDays(lookbackDays);
            var requiredThroughDay = DateTime.UtcNow.Date.AddDays(-1);
            var bootstrapStartDayUtc = KillmailDatasetFreshnessService.BuildBootstrapStartDayUtc(
                DateTime.UtcNow,
                normalizedLookbackDays);

            AppLogger.KillmailImportInfo(
                $"Killmail DB pull requested. requestedHistoryDays={lookbackDays} normalizedHistoryDays={normalizedLookbackDays} startDay={bootstrapStartDayUtc} endDay={requiredThroughDay:yyyy-MM-dd} plannedArchiveDays={normalizedLookbackDays}");

            _archiveSyncWorker.BeginBootstrap(bootstrapStartDayUtc, normalizedLookbackDays);

            try
            {
                await Task.Run(
                    () => ResetLocalKillmailDerivedState(bootstrapStartDayUtc, cancellationToken),
                    cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                _archiveSyncWorker.ResetAfterBootstrapFailure();
                throw;
            }

            _archiveSyncWorker.BeginBootstrap(bootstrapStartDayUtc, normalizedLookbackDays);
            StartIfNeeded();
        }

        public IDisposable BeginForegroundPriority()
        {
            return _foregroundFreshnessCoordinator.BeginPriority();
        }

        public void Stop()
        {
            try
            {
                AppLogger.KillmailImportInfo("Background intel update service stop requested.");
                _shutdownCts.Cancel();
                _r2z2LiveKillmailService.Stop();
                _archiveSyncWorker.Wake();
            }
            catch
            {
            }
        }

        private void ResetLocalKillmailDerivedState(
            string bootstrapStartDayUtc,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DebugTraceWriter.WriteLine("killmail reset start: clearing archive cache and derived state");
            KillmailPaths.ClearArchiveCacheBestEffort();

            cancellationToken.ThrowIfCancellationRequested();
            using var writeGate = _writeGate.Enter(
                $"killmail reset/reseed startDay={bootstrapStartDayUtc}",
                cancellationToken);

            var connectionString = $"Data Source={KillmailPaths.GetKillmailDatabasePath()}";
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();
            ExecuteNonQuery(connection, transaction, "DELETE FROM day_import_state;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM pilot_registry_day;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM pilot_fleet_observations_day;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM pilot_ship_observations_day;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM pilot_cyno_module_observations_day;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM pilot_bait_observations_day;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM pilot_cyno_tackle_observations_day;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM live_killmail_seen;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM live_killmail_feed_state;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM historical_freshness_checkpoint;");
            transaction.Commit();

            cancellationToken.ThrowIfCancellationRequested();

            AppLogger.KillmailImportInfo("Historical freshness checkpoints cleared during full killmail reset/reseed.");

            _metadataRepository.SetValue("latest_complete_day_utc", "");
            _metadataRepository.SetValue("last_successful_update_at_utc", "");
            _metadataRepository.SetValue("bootstrap_start_day_utc", bootstrapStartDayUtc);

            DebugTraceWriter.WriteLine(
                $"killmail reset complete: bootstrapStartDay={bootstrapStartDayUtc}");
        }

        private static void ExecuteNonQuery(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string sql)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        private static TodaysFreshnessRunResult CreateForegroundFreshnessBusyTodaysResult(
            IReadOnlyCollection<long> characterIds)
        {
            return new TodaysFreshnessRunResult
            {
                Success = false,
                VisiblePilotsTargeted = characterIds?.Count ?? 0,
                LastError = ForegroundFreshnessBusyMessage,
                DetailText = ForegroundFreshnessBusyMessage
            };
        }

        private static HistoricalFreshnessRunResult CreateForegroundFreshnessBusyHistoricalResult(
            IReadOnlyCollection<long> characterIds)
        {
            return new HistoricalFreshnessRunResult
            {
                Success = false,
                Mode = "Manual",
                VisiblePilotsTargeted = characterIds?.Count ?? 0,
                CandidatePilotsConsidered = characterIds?.Count ?? 0,
                LastError = ForegroundFreshnessBusyMessage,
                DetailText = ForegroundFreshnessBusyMessage
            };
        }

        private IntelUpdateStatusSnapshot BuildSnapshot()
        {
            return _statusAggregator.Build(
                _archiveSyncWorker.GetState(),
                _foregroundFreshnessCoordinator.IsPriorityActive);
        }

        private void OnArchiveSyncStateChanged()
        {
            Publish();
        }

        private void OnForegroundPriorityChanged(bool isActive)
        {
            Publish();
            if (!isActive)
            {
                _archiveSyncWorker.Wake();
            }
        }

        private void OnLiveFeedStatusChanged()
        {
            Publish();
        }

        private void OnTodaysFreshnessStatusChanged()
        {
            Publish();
        }

        private void OnHistoricalFreshnessStatusChanged()
        {
            Publish();
        }

        private void Publish()
        {
            Action<IntelUpdateStatusSnapshot>? handler;
            IntelUpdateStatusSnapshot snapshot;

            lock (_sync)
            {
                handler = StatusChanged;
                snapshot = BuildSnapshot();
            }

            handler?.Invoke(snapshot);
        }

        private void PublishLocked()
        {
            var handler = StatusChanged;
            if (handler == null)
            {
                return;
            }

            handler(BuildSnapshot());
        }
    }
}
