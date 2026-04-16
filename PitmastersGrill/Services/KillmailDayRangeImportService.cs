using Microsoft.Data.Sqlite;
using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersGrill.Services
{
    public class KillmailDayRangeImportService
    {
        private readonly KillmailDayImportService _killmailDayImportService;

        public KillmailDayRangeImportService(KillmailDayImportService killmailDayImportService)
        {
            _killmailDayImportService = killmailDayImportService;
        }

        public async Task<KillmailDayRangeImportResult> ImportRangeAsync(
            string startDayUtc,
            string endDayUtc,
            Func<KillmailDayRangeImportProgress, Task>? onProgress = null,
            CancellationToken cancellationToken = default,
            bool newestFirst = false,
            bool continueOnArchiveUnavailableNotPublishedYet = false)
        {
            if (!TryParseDay(startDayUtc, out var startDay))
            {
                return new KillmailDayRangeImportResult
                {
                    Success = false,
                    StartDayUtc = startDayUtc,
                    EndDayUtc = endDayUtc,
                    Error = $"Invalid start day: {startDayUtc}"
                };
            }

            if (!TryParseDay(endDayUtc, out var endDay))
            {
                return new KillmailDayRangeImportResult
                {
                    Success = false,
                    StartDayUtc = startDayUtc,
                    EndDayUtc = endDayUtc,
                    Error = $"Invalid end day: {endDayUtc}"
                };
            }

            if (endDay < startDay)
            {
                return new KillmailDayRangeImportResult
                {
                    Success = false,
                    StartDayUtc = startDayUtc,
                    EndDayUtc = endDayUtc,
                    Error = $"End day {endDayUtc} is before start day {startDayUtc}."
                };
            }

            var totalDays = (endDay - startDay).Days + 1;

            if (newestFirst && continueOnArchiveUnavailableNotPublishedYet && totalDays <= 7)
            {
                ResetLocalKillmailDerivedState();
            }

            var importedDays = 0;
            var skippedNotPublishedDays = 0;
            var importedKillmailCount = 0;
            var uniquePilotCount = 0;
            var fleetObservationPilotCount = 0;
            var shipObservationPilotCount = 0;

            for (var offset = 0; offset < totalDays; offset++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var current = newestFirst
                    ? endDay.AddDays(-offset)
                    : startDay.AddDays(offset);

                var dayText = current.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var dayIndex = offset + 1;
                var completedSteps = importedDays + skippedNotPublishedDays;
                var percentBefore = totalDays <= 0 ? 0 : ((double)completedSteps / totalDays) * 100.0;

                if (onProgress != null)
                {
                    await onProgress(new KillmailDayRangeImportProgress
                    {
                        DayUtc = dayText,
                        DayIndex = dayIndex,
                        TotalDays = totalDays,
                        CompletedDays = completedSteps,
                        PercentComplete = percentBefore
                    });
                }

                var result = await _killmailDayImportService.ImportSingleDayAsync(
                    new KillmailRemoteDayInfo
                    {
                        DayUtc = dayText,
                        RemoteTotalCount = 0
                    },
                    cancellationToken);

                if (!result.Success)
                {
                    if (continueOnArchiveUnavailableNotPublishedYet && result.ArchiveUnavailableNotPublishedYet)
                    {
                        skippedNotPublishedDays++;
                        continue;
                    }

                    return new KillmailDayRangeImportResult
                    {
                        Success = false,
                        StartDayUtc = startDayUtc,
                        EndDayUtc = endDayUtc,
                        ImportedDays = importedDays,
                        SkippedNotPublishedDays = skippedNotPublishedDays,
                        ImportedKillmailCount = importedKillmailCount,
                        UniquePilotCount = uniquePilotCount,
                        FleetObservationPilotCount = fleetObservationPilotCount,
                        ShipObservationPilotCount = shipObservationPilotCount,
                        Error = $"Day import failed for {dayText}: {result.Error}"
                    };
                }

                importedDays++;
                importedKillmailCount += result.ImportedKillmailCount;
                uniquePilotCount += result.UniquePilotCount;
                fleetObservationPilotCount += result.FleetObservationPilotCount;
                shipObservationPilotCount += result.ShipObservationPilotCount;
            }

            return new KillmailDayRangeImportResult
            {
                Success = true,
                StartDayUtc = startDayUtc,
                EndDayUtc = endDayUtc,
                ImportedDays = importedDays,
                SkippedNotPublishedDays = skippedNotPublishedDays,
                ImportedKillmailCount = importedKillmailCount,
                UniquePilotCount = uniquePilotCount,
                FleetObservationPilotCount = fleetObservationPilotCount,
                ShipObservationPilotCount = shipObservationPilotCount
            };
        }

        private static bool TryParseDay(string dayUtc, out DateTime parsedDay)
        {
            return DateTime.TryParseExact(
                dayUtc,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsedDay);
        }

        private static void ResetLocalKillmailDerivedState()
        {
            var killmailDbPath = KillmailPaths.GetKillmailDatabasePath();
            var connectionString = $"Data Source={killmailDbPath}";

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();

            ExecuteNonQuery(connection, transaction, "DELETE FROM day_import_state;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM pilot_registry_day;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM pilot_fleet_observations_day;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM pilot_ship_observations_day;");

            ExecuteNonQuery(
                connection,
                transaction,
                @"
                INSERT INTO dataset_metadata (key, value)
                VALUES ('latest_complete_day_utc', '')
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                ");

            ExecuteNonQuery(
                connection,
                transaction,
                @"
                INSERT INTO dataset_metadata (key, value)
                VALUES ('last_successful_update_at_utc', '')
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                ");

            transaction.Commit();
        }

        private static void ExecuteNonQuery(SqliteConnection connection, SqliteTransaction transaction, string sql)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }

    public class KillmailDayRangeImportProgress
    {
        public string DayUtc { get; set; } = "";
        public int DayIndex { get; set; }
        public int TotalDays { get; set; }
        public int CompletedDays { get; set; }
        public double PercentComplete { get; set; }
    }

    public class KillmailDayRangeImportResult
    {
        public bool Success { get; set; }
        public string StartDayUtc { get; set; } = "";
        public string EndDayUtc { get; set; } = "";
        public int ImportedDays { get; set; }
        public int SkippedNotPublishedDays { get; set; }
        public int ImportedKillmailCount { get; set; }
        public int UniquePilotCount { get; set; }
        public int FleetObservationPilotCount { get; set; }
        public int ShipObservationPilotCount { get; set; }
        public string Error { get; set; } = "";
    }
}