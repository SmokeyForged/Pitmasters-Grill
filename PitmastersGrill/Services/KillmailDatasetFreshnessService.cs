using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Globalization;

namespace PitmastersGrill.Services
{
    public class KillmailDatasetFreshnessService
    {
        public const int DefaultMaxKillmailAgeDays = 30;
        public const int MinimumMaxKillmailAgeDays = 1;
        public const int MaximumMaxKillmailAgeDays = 365;

        private readonly KillmailDatasetMetadataRepository _metadataRepository;
        private readonly DayImportStateRepository _dayImportStateRepository;

        public KillmailDatasetFreshnessService(KillmailDatasetMetadataRepository metadataRepository)
        {
            _metadataRepository = metadataRepository;
            _dayImportStateRepository = new DayImportStateRepository(KillmailPaths.GetKillmailDatabasePath());
        }

        public static int NormalizeMaxKillmailAgeDays(int value)
        {
            if (value < MinimumMaxKillmailAgeDays)
            {
                return DefaultMaxKillmailAgeDays;
            }

            if (value > MaximumMaxKillmailAgeDays)
            {
                return MaximumMaxKillmailAgeDays;
            }

            return value;
        }

        public static string BuildBootstrapStartDayUtc(DateTime utcNow, int maxKillmailAgeDays)
        {
            var normalizedDays = NormalizeMaxKillmailAgeDays(maxKillmailAgeDays);
            var requiredThroughDay = utcNow.Date.AddDays(-1);
            var bootstrapStartDay = requiredThroughDay.AddDays(-(normalizedDays - 1));

            return bootstrapStartDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public KillmailDatasetFreshnessStatus GetFreshnessStatus()
        {
            var earliestCompleteDayUtc = _dayImportStateRepository.GetEarliestCompleteDayUtc();

            var latestCompleteDayUtc = _metadataRepository.GetValue("latest_complete_day_utc") ?? "";
            if (string.IsNullOrWhiteSpace(latestCompleteDayUtc))
            {
                latestCompleteDayUtc = _dayImportStateRepository.GetLatestCompleteDayUtc();
            }

            var bootstrapStartDayUtc = _metadataRepository.GetValue("bootstrap_start_day_utc") ?? "";
            var requiredThroughDayUtc = DateTime.UtcNow.Date.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            if (!TryParseDay(requiredThroughDayUtc, out var requiredThroughDay))
            {
                return new KillmailDatasetFreshnessStatus
                {
                    EarliestCompleteDayUtc = earliestCompleteDayUtc,
                    LatestCompleteDayUtc = latestCompleteDayUtc,
                    RequiredThroughDayUtc = requiredThroughDayUtc,
                    IsCurrentThroughRequiredDay = true,
                    MissingDayCount = 0
                };
            }

            if (!TryParseDay(latestCompleteDayUtc, out var latestCompleteDay))
            {
                if (TryParseDay(bootstrapStartDayUtc, out var bootstrapStartDay))
                {
                    if (bootstrapStartDay > requiredThroughDay)
                    {
                        return new KillmailDatasetFreshnessStatus
                        {
                            EarliestCompleteDayUtc = earliestCompleteDayUtc,
                            LatestCompleteDayUtc = latestCompleteDayUtc,
                            RequiredThroughDayUtc = requiredThroughDayUtc,
                            IsCurrentThroughRequiredDay = true,
                            MissingDayCount = 0,
                            FirstMissingDayUtc = "",
                            LastMissingDayUtc = ""
                        };
                    }

                    var bootstrapMissingDayCount = (requiredThroughDay - bootstrapStartDay).Days + 1;

                    return new KillmailDatasetFreshnessStatus
                    {
                        EarliestCompleteDayUtc = earliestCompleteDayUtc,
                        LatestCompleteDayUtc = latestCompleteDayUtc,
                        RequiredThroughDayUtc = requiredThroughDayUtc,
                        IsCurrentThroughRequiredDay = false,
                        MissingDayCount = bootstrapMissingDayCount,
                        FirstMissingDayUtc = bootstrapStartDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        LastMissingDayUtc = requiredThroughDayUtc
                    };
                }

                return new KillmailDatasetFreshnessStatus
                {
                    EarliestCompleteDayUtc = earliestCompleteDayUtc,
                    LatestCompleteDayUtc = latestCompleteDayUtc,
                    RequiredThroughDayUtc = requiredThroughDayUtc,
                    IsCurrentThroughRequiredDay = false,
                    MissingDayCount = 1,
                    FirstMissingDayUtc = requiredThroughDayUtc,
                    LastMissingDayUtc = requiredThroughDayUtc
                };
            }

            if (latestCompleteDay >= requiredThroughDay)
            {
                return new KillmailDatasetFreshnessStatus
                {
                    EarliestCompleteDayUtc = earliestCompleteDayUtc,
                    LatestCompleteDayUtc = latestCompleteDayUtc,
                    RequiredThroughDayUtc = requiredThroughDayUtc,
                    IsCurrentThroughRequiredDay = true,
                    MissingDayCount = 0
                };
            }

            var firstMissingDay = latestCompleteDay.AddDays(1);
            var missingDayCount = (requiredThroughDay - firstMissingDay).Days + 1;

            return new KillmailDatasetFreshnessStatus
            {
                EarliestCompleteDayUtc = earliestCompleteDayUtc,
                LatestCompleteDayUtc = latestCompleteDayUtc,
                RequiredThroughDayUtc = requiredThroughDayUtc,
                IsCurrentThroughRequiredDay = false,
                MissingDayCount = missingDayCount,
                FirstMissingDayUtc = firstMissingDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                LastMissingDayUtc = requiredThroughDayUtc
            };
        }

        public KillmailDatasetUpdatePlan BuildUpdatePlan(KillmailDatasetFreshnessStatus freshnessStatus)
        {
            if (freshnessStatus == null || freshnessStatus.IsCurrentThroughRequiredDay || freshnessStatus.MissingDayCount <= 0)
            {
                return new KillmailDatasetUpdatePlan
                {
                    ShouldRun = false,
                    LatestCompleteDayUtc = freshnessStatus?.LatestCompleteDayUtc ?? "",
                    RequiredThroughDayUtc = freshnessStatus?.RequiredThroughDayUtc ?? "",
                    StartDayUtc = "",
                    EndDayUtc = "",
                    DayCount = 0
                };
            }

            return new KillmailDatasetUpdatePlan
            {
                ShouldRun = true,
                LatestCompleteDayUtc = freshnessStatus.LatestCompleteDayUtc,
                RequiredThroughDayUtc = freshnessStatus.RequiredThroughDayUtc,
                StartDayUtc = freshnessStatus.FirstMissingDayUtc,
                EndDayUtc = freshnessStatus.LastMissingDayUtc,
                DayCount = freshnessStatus.MissingDayCount
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
    }
}
