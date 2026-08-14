using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersGrill.Services
{
    internal sealed class BackgroundHistoricalRepairScheduler
    {
        private readonly object _sync = new();
        private readonly Func<BackgroundHistoricalRepairConfiguration> _configurationProvider;
        private readonly Func<IReadOnlyCollection<long>, CancellationToken, Task<HistoricalFreshnessRunResult>> _runRepairAsync;
        private readonly ForegroundFreshnessCoordinator _foregroundFreshnessCoordinator;
        private readonly CancellationToken _shutdownToken;
        private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
        private Task? _activeTask;

        public BackgroundHistoricalRepairScheduler(
            HistoricalFreshnessService historicalFreshnessService,
            ForegroundFreshnessCoordinator foregroundFreshnessCoordinator,
            CancellationToken shutdownToken)
            : this(
                CreateConfigurationProvider(historicalFreshnessService),
                CreateRepairRunner(historicalFreshnessService),
                foregroundFreshnessCoordinator,
                shutdownToken,
                (delay, token) => Task.Delay(delay, token))
        {
        }

        internal BackgroundHistoricalRepairScheduler(
            Func<BackgroundHistoricalRepairConfiguration> configurationProvider,
            Func<IReadOnlyCollection<long>, CancellationToken, Task<HistoricalFreshnessRunResult>> runRepairAsync,
            ForegroundFreshnessCoordinator foregroundFreshnessCoordinator,
            CancellationToken shutdownToken,
            Func<TimeSpan, CancellationToken, Task> delayAsync)
        {
            _configurationProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
            _runRepairAsync = runRepairAsync ?? throw new ArgumentNullException(nameof(runRepairAsync));
            _foregroundFreshnessCoordinator = foregroundFreshnessCoordinator ?? throw new ArgumentNullException(nameof(foregroundFreshnessCoordinator));
            _shutdownToken = shutdownToken;
            _delayAsync = delayAsync ?? throw new ArgumentNullException(nameof(delayAsync));
        }

        public void ScheduleAfterUiShown(Func<IReadOnlyCollection<long>> visibleCharacterIdsProvider)
        {
            if (visibleCharacterIdsProvider == null)
            {
                throw new ArgumentNullException(nameof(visibleCharacterIdsProvider));
            }

            lock (_sync)
            {
                if (_activeTask != null && !_activeTask.IsCompleted)
                {
                    AppLogger.KillmailImportInfo("Background historical repair startup scheduling skipped because a schedule is already active.");
                    return;
                }

                var configuration = _configurationProvider();
                AppLogger.KillmailImportInfo(
                    $"Background historical repair startup configuration evaluated. enabled={configuration.Enabled} delaySeconds={configuration.DelaySeconds} cooldownHours={configuration.CooldownHours} lookbackDays={configuration.LookbackDays} maxPilots={configuration.MaxPilotsPerRun} recentPilotWindowDays={configuration.RecentPilotWindowDays}");

                if (!configuration.Enabled)
                {
                    AppLogger.KillmailImportInfo("Background historical repair startup skipped because AppSettings disabled it.");
                    return;
                }

                AppLogger.KillmailImportInfo(
                    $"Background historical repair scheduled after UI shown. delaySeconds={configuration.DelaySeconds}");

                _activeTask = Task.Run(
                    () => RunScheduledRepairAsync(configuration, visibleCharacterIdsProvider),
                    _shutdownToken);
            }
        }

        public async Task WaitForCompletionAsync()
        {
            Task? task;
            lock (_sync)
            {
                task = _activeTask;
            }

            if (task == null)
            {
                return;
            }

            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task RunScheduledRepairAsync(
            BackgroundHistoricalRepairConfiguration configuration,
            Func<IReadOnlyCollection<long>> visibleCharacterIdsProvider)
        {
            try
            {
                if (configuration.DelaySeconds > 0)
                {
                    await _delayAsync(
                        TimeSpan.FromSeconds(configuration.DelaySeconds),
                        _shutdownToken).ConfigureAwait(false);
                }

                if (_shutdownToken.IsCancellationRequested)
                {
                    return;
                }

                await _foregroundFreshnessCoordinator
                    .WaitForPriorityToClearAsync(_shutdownToken)
                    .ConfigureAwait(false);

                IReadOnlyCollection<long> visibleCharacterIds;
                try
                {
                    visibleCharacterIds = visibleCharacterIdsProvider() ?? Array.Empty<long>();
                }
                catch (Exception ex)
                {
                    AppLogger.KillmailImportWarn($"Background historical repair could not read visible pilot IDs. message={ex.Message}");
                    AppLogger.ErrorOnly("Background historical repair visible pilot provider failed.", ex);
                    visibleCharacterIds = Array.Empty<long>();
                }

                AppLogger.KillmailImportInfo(
                    $"Background historical repair starting after UI shown. visiblePilotCount={visibleCharacterIds.Count}");

                var result = await _runRepairAsync(visibleCharacterIds, _shutdownToken).ConfigureAwait(false);

                if (result.CandidatePilotsConsidered == 0)
                {
                    AppLogger.KillmailImportInfo("Background historical repair skipped because no candidates were available.");
                }
                else if (result.PilotsChecked == 0 && result.CandidatePilotsSkippedCooldown > 0)
                {
                    AppLogger.KillmailImportInfo(
                        $"Background historical repair skipped because all candidates were in cooldown. considered={result.CandidatePilotsConsidered} cooldownSkipped={result.CandidatePilotsSkippedCooldown}");
                }
                else if (!result.Success &&
                         result.FailedCount > 0 &&
                         string.Equals(
                             result.DetailText,
                             $"Background historical repair stopped after zKill rate limiting while checking pilot {result.PilotsChecked} of {result.CandidatePilotsConsidered}.",
                             StringComparison.Ordinal))
                {
                    AppLogger.KillmailImportInfo(
                        $"Background historical repair rate-limited and exited. pilotsChecked={result.PilotsChecked} failed={result.FailedCount} detail='{result.DetailText}'");
                }

                AppLogger.KillmailImportInfo(
                    $"Background historical repair completed. candidatePilots={result.CandidatePilotsConsidered} skippedCooldown={result.CandidatePilotsSkippedCooldown} pilotsChecked={result.PilotsChecked} imported={result.MissingImportedCount} failed={result.FailedCount} detail='{result.DetailText}'");
            }
            catch (OperationCanceledException)
            {
                AppLogger.KillmailImportInfo("Background historical repair cancelled.");
            }
            catch (Exception ex)
            {
                AppLogger.KillmailImportWarn($"Background historical repair failed. message={ex.Message}");
                AppLogger.ErrorOnly("Background historical repair exception.", ex);
            }
        }

        private static Func<BackgroundHistoricalRepairConfiguration> CreateConfigurationProvider(
            HistoricalFreshnessService historicalFreshnessService)
        {
            if (historicalFreshnessService == null)
            {
                throw new ArgumentNullException(nameof(historicalFreshnessService));
            }

            return historicalFreshnessService.GetBackgroundStartupConfiguration;
        }

        private static Func<IReadOnlyCollection<long>, CancellationToken, Task<HistoricalFreshnessRunResult>> CreateRepairRunner(
            HistoricalFreshnessService historicalFreshnessService)
        {
            if (historicalFreshnessService == null)
            {
                throw new ArgumentNullException(nameof(historicalFreshnessService));
            }

            return historicalFreshnessService.RunBackgroundStartupRepairAsync;
        }
    }
}
