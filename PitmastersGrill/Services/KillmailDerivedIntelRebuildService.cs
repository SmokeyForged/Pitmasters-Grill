using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using PitmastersGrill.Providers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersGrill.Services
{
    public sealed class KillmailDerivedIntelRebuildService
    {
        private const int DefaultMaxRecentDays = 7;

        private readonly PilotCynoModuleObservationDayRepository _cynoModuleObservationRepository;
        private readonly DayImportStateRepository _dayImportStateRepository;
        private readonly KillmailDayArchiveProvider _archiveProvider;

        public KillmailDerivedIntelRebuildService()
        {
            var killmailDbPath = KillmailPaths.GetKillmailDatabasePath();
            _cynoModuleObservationRepository = new PilotCynoModuleObservationDayRepository(killmailDbPath);
            _dayImportStateRepository = new DayImportStateRepository(killmailDbPath);
            _archiveProvider = new KillmailDayArchiveProvider();
        }

        public Task<KillmailDerivedIntelRebuildResult> RebuildConfirmedCynoModuleObservationsAsync(
            CancellationToken cancellationToken = default)
        {
            return RebuildConfirmedCynoModuleObservationsAsync(
                KillmailDerivedIntelRebuildOptions.RecentImportedDays(DefaultMaxRecentDays),
                progress: null,
                cancellationToken);
        }

        public async Task<KillmailDerivedIntelRebuildResult> RebuildConfirmedCynoModuleObservationsAsync(
            KillmailDerivedIntelRebuildOptions options,
            IProgress<KillmailDerivedIntelRebuildProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            options ??= KillmailDerivedIntelRebuildOptions.RecentImportedDays(DefaultMaxRecentDays);

            var allCompletedDbDays = _dayImportStateRepository.GetCompleteDays();
            var localExtractedDays = GetExtractedLocalDays();
            var candidateDays = BuildCandidateDayList(allCompletedDbDays, localExtractedDays, options);

            if (candidateDays.Count == 0)
            {
                var message = "No imported or locally extracted killmail archive days were found. Run Enable KillMail DB Pull or refresh the killmail cache before rebuilding derived intel.";
                AppLogger.DatabaseWarn(message);
                return new KillmailDerivedIntelRebuildResult
                {
                    Success = false,
                    NoLocalSourceAvailable = true,
                    WasBoundedRebuild = !options.RebuildAllImportedDays,
                    TotalCandidateDays = 0,
                    Message = message
                };
            }

            var nowUtc = DateTime.UtcNow.ToString("o");
            var successfulDays = 0;
            var daysWithLocalExtract = 0;
            var daysDownloaded = 0;
            var daysFailed = 0;
            var killmailsScanned = 0;
            var observationsFound = 0;

            if (options.RebuildAllImportedDays)
            {
                _cynoModuleObservationRepository.ClearAll();
            }

            progress?.Report(new KillmailDerivedIntelRebuildProgress
            {
                Phase = "Starting derived intel rebuild",
                CurrentDayIndex = 0,
                TotalDays = candidateDays.Count,
                KillmailsScanned = killmailsScanned,
                ConfirmedCynoModuleObservationsFound = observationsFound,
                DaysDownloaded = daysDownloaded,
                DaysFailed = daysFailed,
                Detail = options.RebuildAllImportedDays ? "all imported days" : $"latest {candidateDays.Count} day(s)"
            });

            for (var i = 0; i < candidateDays.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var day = candidateDays[i];
                var dayNumber = i + 1;

                progress?.Report(new KillmailDerivedIntelRebuildProgress
                {
                    Phase = "Preparing source archive",
                    CurrentDayUtc = day,
                    CurrentDayIndex = i,
                    TotalDays = candidateDays.Count,
                    KillmailsScanned = killmailsScanned,
                    ConfirmedCynoModuleObservationsFound = observationsFound,
                    DaysDownloaded = daysDownloaded,
                    DaysFailed = daysFailed
                });

                var availability = await EnsureExtractedDayAvailableAsync(
                    day,
                    options.DownloadMissingSourceArchives,
                    cancellationToken);

                if (!availability.Success)
                {
                    daysFailed++;
                    AppLogger.DatabaseWarn($"Killmail derived intel rebuild skipped day. day={day}, reason={availability.Error}");
                    progress?.Report(new KillmailDerivedIntelRebuildProgress
                    {
                        Phase = "Skipped day",
                        CurrentDayUtc = day,
                        CurrentDayIndex = dayNumber,
                        TotalDays = candidateDays.Count,
                        KillmailsScanned = killmailsScanned,
                        ConfirmedCynoModuleObservationsFound = observationsFound,
                        DaysDownloaded = daysDownloaded,
                        DaysFailed = daysFailed,
                        Detail = availability.Error
                    });
                    continue;
                }

                if (availability.Downloaded)
                {
                    daysDownloaded++;
                }
                else
                {
                    daysWithLocalExtract++;
                }

                var dayObservations = new List<PilotCynoModuleObservationDayRecord>();
                var relativePaths = _archiveProvider.GetExtractedJsonRelativePaths(day);
                var dayKillmailsScanned = 0;

                foreach (var relativePath in relativePaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var json = await _archiveProvider.ReadExtractedJsonAsync(day, relativePath, cancellationToken);
                    killmailsScanned++;
                    dayKillmailsScanned++;

                    dayObservations.AddRange(
                        KillmailDayImportService.ParseConfirmedCynoModuleObservations(json, day, nowUtc));

                    if (dayKillmailsScanned % 1000 == 0)
                    {
                        progress?.Report(new KillmailDerivedIntelRebuildProgress
                        {
                            Phase = "Scanning killmails",
                            CurrentDayUtc = day,
                            CurrentDayIndex = dayNumber,
                            TotalDays = candidateDays.Count,
                            KillmailsScanned = killmailsScanned,
                            ConfirmedCynoModuleObservationsFound = observationsFound + dayObservations.Count,
                            DaysDownloaded = daysDownloaded,
                            DaysFailed = daysFailed,
                            Detail = $"day killmails={dayKillmailsScanned:N0}"
                        });
                    }
                }

                observationsFound += dayObservations.Count;
                _cynoModuleObservationRepository.ReplaceDay(day, dayObservations);
                successfulDays++;

                progress?.Report(new KillmailDerivedIntelRebuildProgress
                {
                    Phase = "Completed day",
                    CurrentDayUtc = day,
                    CurrentDayIndex = dayNumber,
                    TotalDays = candidateDays.Count,
                    KillmailsScanned = killmailsScanned,
                    ConfirmedCynoModuleObservationsFound = observationsFound,
                    DaysDownloaded = daysDownloaded,
                    DaysFailed = daysFailed,
                    Detail = $"dayObservations={dayObservations.Count:N0}"
                });
            }

            var result = new KillmailDerivedIntelRebuildResult
            {
                Success = successfulDays > 0,
                WasBoundedRebuild = !options.RebuildAllImportedDays,
                TotalCandidateDays = candidateDays.Count,
                DaysScanned = successfulDays,
                DaysWithLocalExtract = daysWithLocalExtract,
                DaysDownloaded = daysDownloaded,
                DaysFailed = daysFailed,
                KillmailsScanned = killmailsScanned,
                ConfirmedCynoModuleObservationsFound = observationsFound,
                Message = BuildResultMessage(
                    options,
                    candidateDays.Count,
                    successfulDays,
                    daysWithLocalExtract,
                    daysDownloaded,
                    daysFailed,
                    killmailsScanned,
                    observationsFound)
            };

            if (result.Success)
            {
                AppLogger.DatabaseInfo(result.Message);
            }
            else
            {
                AppLogger.DatabaseWarn(result.Message);
            }

            return result;
        }

        private static string BuildResultMessage(
            KillmailDerivedIntelRebuildOptions options,
            int totalCandidateDays,
            int successfulDays,
            int daysWithLocalExtract,
            int daysDownloaded,
            int daysFailed,
            int killmailsScanned,
            int observationsFound)
        {
            var scope = options.RebuildAllImportedDays
                ? "all imported days"
                : $"latest {totalCandidateDays} imported/extracted day(s)";

            return $"Rebuilt killmail derived intel ({scope}). days={successfulDays}/{totalCandidateDays}, localDays={daysWithLocalExtract}, downloadedDays={daysDownloaded}, failedDays={daysFailed}, killmailsScanned={killmailsScanned}, confirmedCynoModuleObservations={observationsFound}.";
        }

        private static List<string> BuildCandidateDayList(
            IReadOnlyList<string> completedDbDays,
            IReadOnlyList<string> localExtractedDays,
            KillmailDerivedIntelRebuildOptions options)
        {
            var days = completedDbDays
                .Concat(localExtractedDays)
                .Where(day => !string.IsNullOrWhiteSpace(day))
                .Distinct(StringComparer.Ordinal)
                .OrderByDescending(day => day, StringComparer.Ordinal)
                .ToList();

            if (!string.IsNullOrWhiteSpace(options.PreferredDayUtc))
            {
                var preferred = options.PreferredDayUtc.Trim();
                if (!days.Contains(preferred, StringComparer.Ordinal))
                {
                    days.Insert(0, preferred);
                }
                else
                {
                    days.Remove(preferred);
                    days.Insert(0, preferred);
                }
            }

            if (!options.RebuildAllImportedDays)
            {
                var maxDays = options.MaxDaysToScan <= 0 ? DefaultMaxRecentDays : options.MaxDaysToScan;
                days = days.Take(maxDays).ToList();
            }

            return days
                .OrderBy(day => day, StringComparer.Ordinal)
                .ToList();
        }

        private async Task<ExtractedDayAvailability> EnsureExtractedDayAvailableAsync(
            string dayUtc,
            bool downloadMissingSourceArchives,
            CancellationToken cancellationToken)
        {
            if (HasExtractedJsonFiles(dayUtc))
            {
                return ExtractedDayAvailability.Local();
            }

            if (!downloadMissingSourceArchives)
            {
                return ExtractedDayAvailability.Failed("No extracted JSON source is currently available for this day.");
            }

            try
            {
                AppLogger.DatabaseInfo($"Killmail derived intel rebuild downloading missing source archive. day={dayUtc}");
                var downloadResult = await _archiveProvider.DownloadDayArchiveAsync(dayUtc, cancellationToken);
                if (!downloadResult.Success)
                {
                    return ExtractedDayAvailability.Failed(downloadResult.Error);
                }

                var extractResult = await _archiveProvider.EnsureDayExtractedAsync(
                    dayUtc,
                    downloadResult.ArchivePath,
                    cancellationToken);

                if (!extractResult.Success)
                {
                    return ExtractedDayAvailability.Failed(extractResult.Error);
                }

                return HasExtractedJsonFiles(dayUtc)
                    ? ExtractedDayAvailability.DownloadedSource()
                    : ExtractedDayAvailability.Failed("Archive extraction produced no JSON files.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return ExtractedDayAvailability.Failed(ex.Message);
            }
        }

        private static bool HasExtractedJsonFiles(string dayUtc)
        {
            try
            {
                var extractRoot = KillmailPaths.GetKillmailExtractedDayDirectory(dayUtc);
                return Directory.Exists(extractRoot) &&
                       Directory.GetFiles(extractRoot, "*.json", SearchOption.AllDirectories).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static List<string> GetExtractedLocalDays()
        {
            var archiveRoot = KillmailPaths.GetKillmailArchiveCacheDirectory();
            if (!Directory.Exists(archiveRoot))
            {
                return new List<string>();
            }

            return Directory.GetDirectories(archiveRoot, "extract_*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name) && name!.StartsWith("extract_", StringComparison.OrdinalIgnoreCase))
                .Select(name => name!.Substring("extract_".Length))
                .Where(HasExtractedJsonFiles)
                .OrderByDescending(day => day, StringComparer.Ordinal)
                .ToList();
        }

        private sealed class ExtractedDayAvailability
        {
            public bool Success { get; private set; }
            public bool Downloaded { get; private set; }
            public string Error { get; private set; } = "";

            public static ExtractedDayAvailability Local()
            {
                return new ExtractedDayAvailability { Success = true, Downloaded = false };
            }

            public static ExtractedDayAvailability DownloadedSource()
            {
                return new ExtractedDayAvailability { Success = true, Downloaded = true };
            }

            public static ExtractedDayAvailability Failed(string error)
            {
                return new ExtractedDayAvailability
                {
                    Success = false,
                    Downloaded = false,
                    Error = string.IsNullOrWhiteSpace(error) ? "Unknown source archive error." : error
                };
            }
        }
    }
}
