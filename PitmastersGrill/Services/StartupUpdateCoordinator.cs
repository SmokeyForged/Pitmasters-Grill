using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersGrill.Services
{
    public class StartupUpdateCoordinator
    {
        private readonly KillmailDatasetFreshnessService _freshnessService;
        private readonly KillmailDayRangeImportService _rangeImportService;
        private readonly KillmailDatasetMetadataRepository _metadataRepository;

        public StartupUpdateCoordinator(
            KillmailDatasetFreshnessService freshnessService,
            KillmailDayRangeImportService rangeImportService,
            KillmailDatasetMetadataRepository metadataRepository)
        {
            _freshnessService = freshnessService;
            _rangeImportService = rangeImportService;
            _metadataRepository = metadataRepository;
        }

        public async Task RunAsync(
            Func<StartupUpdateState, Task> publishState,
            CancellationToken cancellationToken = default)
        {
            await publishState(new StartupUpdateState
            {
                StatusText = "Checking local intel database",
                DetailText = "Reviewing local killmail coverage and determining whether an update is required.",
                IsIndeterminate = true,
                ProgressValue = 0,
                IsExceptionMessage = false
            });

            _metadataRepository.SetUtcNow("last_startup_check_at_utc");

            var freshnessStatus = _freshnessService.GetFreshnessStatus();
            var updatePlan = _freshnessService.BuildUpdatePlan(freshnessStatus);

            if (!updatePlan.ShouldRun)
            {
                await publishState(new StartupUpdateState
                {
                    StatusText = "Local intel is current",
                    DetailText = string.IsNullOrWhiteSpace(freshnessStatus.RequiredThroughDayUtc)
                        ? "The local killmail database is current."
                        : $"The local killmail database is current through {freshnessStatus.RequiredThroughDayUtc}.",
                    IsIndeterminate = false,
                    ProgressValue = 100,
                    IsExceptionMessage = false
                });

                return;
            }

            var showStandbyMessage = updatePlan.DayCount > 10;

            await publishState(new StartupUpdateState
            {
                StatusText = "Refreshing local intel database",
                DetailText = showStandbyMessage
                    ? "The database is out of date, please standby while we refresh local intel."
                    : $"Importing {updatePlan.DayCount} missing day(s): {updatePlan.StartDayUtc} → {updatePlan.EndDayUtc}",
                IsIndeterminate = false,
                ProgressValue = 0,
                IsExceptionMessage = showStandbyMessage
            });

            var rangeResult = await _rangeImportService.ImportRangeAsync(
                updatePlan.StartDayUtc,
                updatePlan.EndDayUtc,
                async progress =>
                {
                    await publishState(new StartupUpdateState
                    {
                        StatusText = "Refreshing local intel database",
                        DetailText = $"Importing day {progress.DayIndex} of {progress.TotalDays}: {progress.DayUtc}",
                        IsIndeterminate = false,
                        ProgressValue = progress.PercentComplete,
                        IsExceptionMessage = showStandbyMessage
                    });
                },
                cancellationToken);

            if (!rangeResult.Success)
            {
                await publishState(new StartupUpdateState
                {
                    StatusText = "Local intel refresh failed",
                    DetailText = rangeResult.Error,
                    IsIndeterminate = false,
                    ProgressValue = 0,
                    IsExceptionMessage = true
                });

                throw new InvalidOperationException(rangeResult.Error);
            }

            await publishState(new StartupUpdateState
            {
                StatusText = "Local intel is current",
                DetailText = $"Imported {rangeResult.ImportedDays} day(s). Local intel is now current through {updatePlan.RequiredThroughDayUtc}.",
                IsIndeterminate = false,
                ProgressValue = 100,
                IsExceptionMessage = false
            });
        }
    }
}