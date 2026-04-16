using PitmastersGrill.Persistence;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersGrill.Services
{
    public class SeedBuildService
    {
        private readonly KillmailDayRangeImportService _rangeImportService;
        private readonly KillmailDatasetMetadataRepository _metadataRepository;

        public SeedBuildService(
            KillmailDayRangeImportService rangeImportService,
            KillmailDatasetMetadataRepository metadataRepository)
        {
            _rangeImportService = rangeImportService;
            _metadataRepository = metadataRepository;
        }

        public async Task<SeedBuildResult> BuildSeedAsync(
            string seedVersion,
            string startDayUtc,
            string endDayUtc,
            Func<SeedBuildProgress, Task>? onProgress = null,
            CancellationToken cancellationToken = default)
        {
            var startedAtUtc = DateTime.UtcNow.ToString("o");

            if (onProgress != null)
            {
                await onProgress(new SeedBuildProgress
                {
                    StatusText = "Starting seed build",
                    DetailText = $"Building seed from {startDayUtc} through {endDayUtc}.",
                    ProgressValue = 0
                });
            }

            var rangeResult = await _rangeImportService.ImportRangeAsync(
                startDayUtc,
                endDayUtc,
                async progress =>
                {
                    if (onProgress != null)
                    {
                        await onProgress(new SeedBuildProgress
                        {
                            StatusText = "Building seed database",
                            DetailText = $"Importing day {progress.DayIndex} of {progress.TotalDays}: {progress.DayUtc}",
                            ProgressValue = progress.PercentComplete
                        });
                    }
                },
                cancellationToken);

            if (!rangeResult.Success)
            {
                return new SeedBuildResult
                {
                    Success = false,
                    SeedVersion = seedVersion,
                    StartDayUtc = startDayUtc,
                    EndDayUtc = endDayUtc,
                    Error = rangeResult.Error
                };
            }

            _metadataRepository.SetValue("seed_version", seedVersion);
            _metadataRepository.SetValue("seed_built_at_utc", DateTime.UtcNow.ToString("o"));

            if (onProgress != null)
            {
                await onProgress(new SeedBuildProgress
                {
                    StatusText = "Seed build complete",
                    DetailText = $"Seed built through {endDayUtc}. Imported {rangeResult.ImportedDays} day(s).",
                    ProgressValue = 100
                });
            }

            return new SeedBuildResult
            {
                Success = true,
                SeedVersion = seedVersion,
                StartDayUtc = startDayUtc,
                EndDayUtc = endDayUtc,
                ImportedDays = rangeResult.ImportedDays,
                ImportedKillmailCount = rangeResult.ImportedKillmailCount,
                UniquePilotCount = rangeResult.UniquePilotCount,
                FleetObservationPilotCount = rangeResult.FleetObservationPilotCount,
                ShipObservationPilotCount = rangeResult.ShipObservationPilotCount,
                SeedBuiltAtUtc = DateTime.UtcNow.ToString("o"),
                StartedAtUtc = startedAtUtc
            };
        }
    }

    public class SeedBuildProgress
    {
        public string StatusText { get; set; } = "";
        public string DetailText { get; set; } = "";
        public double ProgressValue { get; set; }
    }

    public class SeedBuildResult
    {
        public bool Success { get; set; }
        public string SeedVersion { get; set; } = "";
        public string StartDayUtc { get; set; } = "";
        public string EndDayUtc { get; set; } = "";
        public int ImportedDays { get; set; }
        public int ImportedKillmailCount { get; set; }
        public int UniquePilotCount { get; set; }
        public int FleetObservationPilotCount { get; set; }
        public int ShipObservationPilotCount { get; set; }
        public string SeedBuiltAtUtc { get; set; } = "";
        public string StartedAtUtc { get; set; } = "";
        public string Error { get; set; } = "";
    }
}