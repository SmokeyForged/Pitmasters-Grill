using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;

namespace PitmastersGrill.Services
{
    internal sealed class IntelUpdateStatusAggregator
    {
        private readonly KillmailDatasetFreshnessService _freshnessService;
        private readonly KillmailDatasetMetadataRepository _metadataRepository;
        private readonly R2Z2LiveKillmailService _r2z2LiveKillmailService;
        private readonly TodaysFreshnessService _todaysFreshnessService;
        private readonly HistoricalFreshnessService _historicalFreshnessService;

        public IntelUpdateStatusAggregator(
            KillmailDatasetFreshnessService freshnessService,
            KillmailDatasetMetadataRepository metadataRepository,
            R2Z2LiveKillmailService r2z2LiveKillmailService,
            TodaysFreshnessService todaysFreshnessService,
            HistoricalFreshnessService historicalFreshnessService)
        {
            _freshnessService = freshnessService ?? throw new ArgumentNullException(nameof(freshnessService));
            _metadataRepository = metadataRepository ?? throw new ArgumentNullException(nameof(metadataRepository));
            _r2z2LiveKillmailService = r2z2LiveKillmailService ?? throw new ArgumentNullException(nameof(r2z2LiveKillmailService));
            _todaysFreshnessService = todaysFreshnessService ?? throw new ArgumentNullException(nameof(todaysFreshnessService));
            _historicalFreshnessService = historicalFreshnessService ?? throw new ArgumentNullException(nameof(historicalFreshnessService));
        }

        public IntelUpdateStatusSnapshot Build(ArchiveSyncState archiveState, bool foregroundActive)
        {
            archiveState ??= new ArchiveSyncState();

            var freshness = _freshnessService.GetFreshnessStatus();
            var liveFeedSnapshot = _r2z2LiveKillmailService.GetSnapshot();
            var todaysFreshnessSnapshot = _todaysFreshnessService.GetSnapshot();
            var historicalFreshnessSnapshot = _historicalFreshnessService.GetSnapshot();
            var coverageDetail = BuildCoverageDetail(freshness);
            var lastSuccessfulUpdateAtUtc = _metadataRepository.GetValue("last_successful_update_at_utc") ?? "";
            var totalProgressIsIndeterminate = archiveState.IsRunning
                ? archiveState.TotalDaysInCurrentRun <= 0
                : false;
            var totalProgressPercent = archiveState.IsRunning
                ? BuildTotalProgressPercent(archiveState.CompletedDaysInCurrentRun, archiveState.TotalDaysInCurrentRun)
                : freshness.IsCurrentThroughRequiredDay
                    ? 100
                    : 0;
            var totalProgressText = BuildTotalProgressText(
                freshness,
                archiveState.IsRunning,
                archiveState.CompletedDaysInCurrentRun,
                archiveState.TotalDaysInCurrentRun);
            var currentDayProgressText = archiveState.IsRunning
                ? "Progress details unavailable for this phase."
                : freshness.IsCurrentThroughRequiredDay
                    ? "No update currently running."
                    : "Waiting for the next local intel update pass.";

            if (!string.IsNullOrWhiteSpace(archiveState.LastError))
            {
                return new IntelUpdateStatusSnapshot
                {
                    IsRunning = false,
                    IsCurrentThroughYesterday = freshness.IsCurrentThroughRequiredDay && freshness.IsRequestedCoverageComplete,
                    HasError = true,
                    IsForegroundPriorityActive = foregroundActive,
                    EarliestCompleteDayUtc = freshness.EarliestCompleteDayUtc,
                    LatestCompleteDayUtc = freshness.LatestCompleteDayUtc,
                    RequiredThroughDayUtc = freshness.RequiredThroughDayUtc,
                    RequestedStartDayUtc = freshness.RequestedStartDayUtc,
                    CurrentImportDayUtc = archiveState.CurrentImportDayUtc,
                    LastSuccessfulUpdateAtUtc = lastSuccessfulUpdateAtUtc,
                    IsRequestedCoverageComplete = freshness.IsRequestedCoverageComplete,
                    HasRequestedCoverageWindow = freshness.HasRequestedCoverageWindow,
                    RequestedHistoryDays = freshness.RequestedHistoryDays,
                    RequestedCoverageDays = freshness.RequestedCoverageDays,
                    LocalCoverageDays = freshness.LocalCoverageDays,
                    MissingDayCount = freshness.MissingDayCount,
                    TotalDaysInCurrentRun = archiveState.TotalDaysInCurrentRun,
                    CompletedDaysInCurrentRun = archiveState.CompletedDaysInCurrentRun,
                    StatusText = "LOCAL INTEL UPDATE FAILED",
                    DetailText = archiveState.LastError,
                    ErrorText = archiveState.LastError,
                    TotalProgressIsIndeterminate = totalProgressIsIndeterminate,
                    TotalProgressPercent = totalProgressPercent,
                    TotalProgressText = totalProgressText,
                    CurrentDayProgressIsIndeterminate = true,
                    CurrentDayProgressPercent = 0,
                    CurrentDayProgressText = currentDayProgressText,
                    LiveFeed = liveFeedSnapshot,
                    TodaysFreshness = todaysFreshnessSnapshot,
                    HistoricalFreshness = historicalFreshnessSnapshot
                };
            }

            if (!string.IsNullOrWhiteSpace(archiveState.NotPublishedBoundaryDayUtc))
            {
                var isBlockedOnlyByUnpublishedBoundary = IsBlockedOnlyByUnpublishedBoundary(
                    freshness,
                    archiveState.NotPublishedBoundaryDayUtc);
                var isCurrentThroughLatestPublishedArchive = freshness.IsRequestedCoverageComplete || isBlockedOnlyByUnpublishedBoundary;
                var notPublishedDetail = BuildLatestPublishedArchiveDetail(
                    freshness,
                    archiveState.NotPublishedBoundaryDayUtc);

                return new IntelUpdateStatusSnapshot
                {
                    IsRunning = false,
                    IsCurrentThroughYesterday = isCurrentThroughLatestPublishedArchive,
                    HasError = false,
                    IsForegroundPriorityActive = foregroundActive,
                    EarliestCompleteDayUtc = freshness.EarliestCompleteDayUtc,
                    LatestCompleteDayUtc = freshness.LatestCompleteDayUtc,
                    RequiredThroughDayUtc = freshness.RequiredThroughDayUtc,
                    RequestedStartDayUtc = freshness.RequestedStartDayUtc,
                    CurrentImportDayUtc = "",
                    LastSuccessfulUpdateAtUtc = lastSuccessfulUpdateAtUtc,
                    IsRequestedCoverageComplete = isCurrentThroughLatestPublishedArchive,
                    HasRequestedCoverageWindow = freshness.HasRequestedCoverageWindow,
                    RequestedHistoryDays = freshness.RequestedHistoryDays,
                    RequestedCoverageDays = freshness.RequestedCoverageDays,
                    LocalCoverageDays = freshness.LocalCoverageDays,
                    MissingDayCount = 0,
                    TotalDaysInCurrentRun = 0,
                    CompletedDaysInCurrentRun = 0,
                    StatusText = isCurrentThroughLatestPublishedArchive
                        ? "LOCAL INTEL CURRENT — through latest published archive"
                        : "LOCAL INTEL PARTIALLY POPULATED — latest published archive reached",
                    DetailText = notPublishedDetail,
                    ErrorText = "",
                    TotalProgressIsIndeterminate = false,
                    TotalProgressPercent = 100,
                    TotalProgressText = $"Local killmail intel is current through the latest published archive. Waiting for archive day {archiveState.NotPublishedBoundaryDayUtc} to publish.",
                    CurrentDayProgressIsIndeterminate = false,
                    CurrentDayProgressPercent = 0,
                    CurrentDayProgressText = $"Archive day {archiveState.NotPublishedBoundaryDayUtc} is not published yet. PMG will retry automatically.",
                    LiveFeed = liveFeedSnapshot,
                    TodaysFreshness = todaysFreshnessSnapshot,
                    HistoricalFreshness = historicalFreshnessSnapshot
                };
            }

            if (archiveState.IsRunning)
            {
                var detail = foregroundActive
                    ? "Foreground activity detected. Killmail intel updating will resume after the current clipboard/API work finishes."
                    : $"Updating killmail intel… Current day: {archiveState.CurrentImportDayUtc} • Remaining day(s): {freshness.MissingDayCount}";

                return new IntelUpdateStatusSnapshot
                {
                    IsRunning = true,
                    IsCurrentThroughYesterday = false,
                    HasError = false,
                    IsForegroundPriorityActive = foregroundActive,
                    EarliestCompleteDayUtc = freshness.EarliestCompleteDayUtc,
                    LatestCompleteDayUtc = freshness.LatestCompleteDayUtc,
                    RequiredThroughDayUtc = freshness.RequiredThroughDayUtc,
                    RequestedStartDayUtc = freshness.RequestedStartDayUtc,
                    CurrentImportDayUtc = archiveState.CurrentImportDayUtc,
                    LastSuccessfulUpdateAtUtc = lastSuccessfulUpdateAtUtc,
                    IsRequestedCoverageComplete = freshness.IsRequestedCoverageComplete,
                    HasRequestedCoverageWindow = freshness.HasRequestedCoverageWindow,
                    RequestedHistoryDays = freshness.RequestedHistoryDays,
                    RequestedCoverageDays = freshness.RequestedCoverageDays,
                    LocalCoverageDays = freshness.LocalCoverageDays,
                    MissingDayCount = freshness.MissingDayCount,
                    TotalDaysInCurrentRun = archiveState.TotalDaysInCurrentRun,
                    CompletedDaysInCurrentRun = archiveState.CompletedDaysInCurrentRun,
                    StatusText = foregroundActive
                        ? "LOCAL INTEL UPDATE PAUSED FOR FOREGROUND ACTIVITY"
                        : "LOCAL INTEL STALE — updating in progress",
                    DetailText = detail,
                    ErrorText = "",
                    TotalProgressIsIndeterminate = totalProgressIsIndeterminate,
                    TotalProgressPercent = totalProgressPercent,
                    TotalProgressText = totalProgressText,
                    CurrentDayProgressIsIndeterminate = true,
                    CurrentDayProgressPercent = 0,
                    CurrentDayProgressText = currentDayProgressText,
                    LiveFeed = liveFeedSnapshot,
                    TodaysFreshness = todaysFreshnessSnapshot,
                    HistoricalFreshness = historicalFreshnessSnapshot
                };
            }

            if (freshness.IsCurrentThroughRequiredDay && freshness.IsRequestedCoverageComplete)
            {
                return new IntelUpdateStatusSnapshot
                {
                    IsRunning = false,
                    IsCurrentThroughYesterday = true,
                    HasError = false,
                    IsForegroundPriorityActive = foregroundActive,
                    EarliestCompleteDayUtc = freshness.EarliestCompleteDayUtc,
                    LatestCompleteDayUtc = freshness.LatestCompleteDayUtc,
                    RequiredThroughDayUtc = freshness.RequiredThroughDayUtc,
                    RequestedStartDayUtc = freshness.RequestedStartDayUtc,
                    CurrentImportDayUtc = "",
                    LastSuccessfulUpdateAtUtc = lastSuccessfulUpdateAtUtc,
                    IsRequestedCoverageComplete = true,
                    HasRequestedCoverageWindow = freshness.HasRequestedCoverageWindow,
                    RequestedHistoryDays = freshness.RequestedHistoryDays,
                    RequestedCoverageDays = freshness.RequestedCoverageDays,
                    LocalCoverageDays = freshness.LocalCoverageDays,
                    MissingDayCount = 0,
                    TotalDaysInCurrentRun = 0,
                    CompletedDaysInCurrentRun = 0,
                    StatusText = "LOCAL INTEL CURRENT — through yesterday",
                    DetailText = coverageDetail,
                    ErrorText = "",
                    TotalProgressIsIndeterminate = false,
                    TotalProgressPercent = 100,
                    TotalProgressText = "Local killmail intel is current through yesterday.",
                    CurrentDayProgressIsIndeterminate = false,
                    CurrentDayProgressPercent = 0,
                    CurrentDayProgressText = currentDayProgressText,
                    LiveFeed = liveFeedSnapshot,
                    TodaysFreshness = todaysFreshnessSnapshot,
                    HistoricalFreshness = historicalFreshnessSnapshot
                };
            }

            return new IntelUpdateStatusSnapshot
            {
                IsRunning = false,
                IsCurrentThroughYesterday = false,
                HasError = false,
                IsForegroundPriorityActive = foregroundActive,
                EarliestCompleteDayUtc = freshness.EarliestCompleteDayUtc,
                LatestCompleteDayUtc = freshness.LatestCompleteDayUtc,
                RequiredThroughDayUtc = freshness.RequiredThroughDayUtc,
                RequestedStartDayUtc = freshness.RequestedStartDayUtc,
                CurrentImportDayUtc = "",
                LastSuccessfulUpdateAtUtc = lastSuccessfulUpdateAtUtc,
                IsRequestedCoverageComplete = freshness.IsRequestedCoverageComplete,
                HasRequestedCoverageWindow = freshness.HasRequestedCoverageWindow,
                RequestedHistoryDays = freshness.RequestedHistoryDays,
                RequestedCoverageDays = freshness.RequestedCoverageDays,
                LocalCoverageDays = freshness.LocalCoverageDays,
                MissingDayCount = freshness.MissingDayCount,
                TotalDaysInCurrentRun = 0,
                CompletedDaysInCurrentRun = 0,
                StatusText = freshness.IsCurrentThroughRequiredDay && !freshness.IsRequestedCoverageComplete
                    ? "LOCAL INTEL PARTIALLY POPULATED"
                    : "LOCAL INTEL STALE — awaiting update",
                DetailText = coverageDetail,
                ErrorText = "",
                TotalProgressIsIndeterminate = false,
                TotalProgressPercent = 0,
                TotalProgressText = BuildTotalProgressText(freshness, false, 0, freshness.MissingDayCount),
                CurrentDayProgressIsIndeterminate = false,
                CurrentDayProgressPercent = 0,
                CurrentDayProgressText = currentDayProgressText,
                LiveFeed = liveFeedSnapshot,
                TodaysFreshness = todaysFreshnessSnapshot,
                HistoricalFreshness = historicalFreshnessSnapshot
            };
        }

        private static double BuildTotalProgressPercent(int completedDays, int totalDays)
        {
            if (totalDays <= 0)
            {
                return 0;
            }

            return Math.Max(0, Math.Min(100, ((double)completedDays / totalDays) * 100.0));
        }

        private static string BuildTotalProgressText(
            KillmailDatasetFreshnessStatus freshness,
            bool isRunning,
            int completedDays,
            int totalDays)
        {
            if (isRunning)
            {
                if (totalDays > 0)
                {
                    var currentDayIndex = Math.Min(totalDays, completedDays + 1);
                    return $"Day {currentDayIndex} of {totalDays} in the current catch-up run.";
                }

                return "Updating killmail intel… Progress details unavailable for this phase.";
            }

            if (freshness?.IsCurrentThroughRequiredDay == true)
            {
                return "No catch-up update is currently required.";
            }

            if (freshness != null && freshness.MissingDayCount > 0)
            {
                return $"Waiting to catch up {freshness.MissingDayCount} day(s).";
            }

            return "No update currently running.";
        }

        private static bool IsBlockedOnlyByUnpublishedBoundary(KillmailDatasetFreshnessStatus freshness, string boundaryDayUtc)
        {
            if (freshness == null || string.IsNullOrWhiteSpace(boundaryDayUtc))
            {
                return false;
            }

            if (freshness.MissingDayCount <= 0)
            {
                return true;
            }

            return string.Equals(freshness.FirstMissingDayUtc, boundaryDayUtc, StringComparison.Ordinal);
        }

        private static string BuildLatestPublishedArchiveDetail(KillmailDatasetFreshnessStatus freshness, string boundaryDayUtc)
        {
            var baseDetail = BuildCoverageDetail(freshness);
            if (string.IsNullOrWhiteSpace(boundaryDayUtc))
            {
                return baseDetail;
            }

            return $"{baseDetail} Archive day {boundaryDayUtc} is not published yet; PMG will retry automatically.";
        }

        private static string BuildCoverageDetail(KillmailDatasetFreshnessStatus freshness)
        {
            if (freshness == null)
            {
                return "Coverage unavailable.";
            }

            if (freshness.HasRequestedCoverageWindow && freshness.RequestedCoverageDays > 0)
            {
                var requestedHistoryText = $"Requested History: {freshness.RequestedHistoryDays} day{(freshness.RequestedHistoryDays == 1 ? "" : "s")}.";
                var localCoverageText = $"Local Coverage: {freshness.LocalCoverageDays} of {freshness.RequestedCoverageDays} requested day{(freshness.RequestedCoverageDays == 1 ? "" : "s")}.";
                var missingText = freshness.MissingDayCount > 0
                    ? $"Missing Days: {freshness.MissingDayCount}. Last missing day: {freshness.LastMissingDayUtc}."
                    : "Missing Days: 0.";

                return $"{requestedHistoryText} {localCoverageText} {missingText}";
            }

            var earliest = freshness.EarliestCompleteDayUtc?.Trim() ?? "";
            var latest = freshness.LatestCompleteDayUtc?.Trim() ?? "";

            if (!string.IsNullOrWhiteSpace(earliest) && !string.IsNullOrWhiteSpace(latest))
            {
                if (string.Equals(earliest, latest, StringComparison.Ordinal))
                {
                    return $"Current through {latest}.";
                }

                return $"Current through {earliest} through {latest}.";
            }

            if (!string.IsNullOrWhiteSpace(latest))
            {
                return $"Current through {latest}.";
            }

            return "Coverage unavailable.";
        }
    }
}
