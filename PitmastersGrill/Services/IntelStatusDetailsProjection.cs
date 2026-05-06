using PitmastersGrill.Models;
using System;
using System.Globalization;

namespace PitmastersGrill.Services
{
    public sealed class IntelStatusDetailsProjection
    {
        public string LastUpdatedText { get; private init; } = string.Empty;
        public string OldestKillmailDayText { get; private init; } = string.Empty;
        public string NewestKillmailDayText { get; private init; } = string.Empty;
        public string CurrentUpdateStatusText { get; private init; } = string.Empty;
        public bool TotalProgressIsIndeterminate { get; private init; }
        public double TotalProgressValue { get; private init; }
        public string TotalProgressText { get; private init; } = string.Empty;
        public bool CurrentDayProgressIsIndeterminate { get; private init; }
        public double CurrentDayProgressValue { get; private init; }
        public string CurrentDayProgressText { get; private init; } = string.Empty;
        public string LiveFeedSourceText { get; private init; } = string.Empty;
        public string LiveFeedStatusText { get; private init; } = string.Empty;
        public string LiveFeedEnabledText { get; private init; } = string.Empty;
        public string LiveFeedRecentImportsText { get; private init; } = string.Empty;
        public string LiveFeedNextSequenceText { get; private init; } = string.Empty;
        public string LiveFeedLastProcessedSequenceText { get; private init; } = string.Empty;
        public string LiveFeedLastSuccessText { get; private init; } = string.Empty;
        public string LiveFeedLastCaughtUpText { get; private init; } = string.Empty;
        public string LiveFeedLastErrorText { get; private init; } = string.Empty;
        public string TodaysFreshnessStatusText { get; private init; } = string.Empty;
        public string TodaysFreshnessVisiblePilotsText { get; private init; } = string.Empty;
        public string TodaysFreshnessEntitiesQueriedText { get; private init; } = string.Empty;
        public string TodaysFreshnessResultsFoundText { get; private init; } = string.Empty;
        public string TodaysFreshnessKnownSkippedText { get; private init; } = string.Empty;
        public string TodaysFreshnessImportedText { get; private init; } = string.Empty;
        public string TodaysFreshnessFailedText { get; private init; } = string.Empty;
        public string TodaysFreshnessLastRunText { get; private init; } = string.Empty;
        public string TodaysFreshnessDetailText { get; private init; } = string.Empty;
        public string TodaysFreshnessLastErrorText { get; private init; } = string.Empty;
        public bool RunTodaysFreshnessButtonIsEnabled { get; private init; }
        public string RunTodaysFreshnessButtonLabel { get; private init; } = string.Empty;
        public string HistoricalFreshnessStatusText { get; private init; } = string.Empty;
        public string HistoricalFreshnessModeText { get; private init; } = string.Empty;
        public string HistoricalFreshnessVisiblePilotsText { get; private init; } = string.Empty;
        public string HistoricalFreshnessCandidatesConsideredText { get; private init; } = string.Empty;
        public string HistoricalFreshnessCandidatesSkippedCooldownText { get; private init; } = string.Empty;
        public string HistoricalFreshnessPilotsCheckedText { get; private init; } = string.Empty;
        public string HistoricalFreshnessDaysCheckedText { get; private init; } = string.Empty;
        public string HistoricalFreshnessEntitiesQueriedText { get; private init; } = string.Empty;
        public string HistoricalFreshnessResultsFoundText { get; private init; } = string.Empty;
        public string HistoricalFreshnessKnownSkippedText { get; private init; } = string.Empty;
        public string HistoricalFreshnessImportedText { get; private init; } = string.Empty;
        public string HistoricalFreshnessFailedText { get; private init; } = string.Empty;
        public string HistoricalFreshnessLastRunText { get; private init; } = string.Empty;
        public string HistoricalFreshnessDetailText { get; private init; } = string.Empty;
        public string HistoricalFreshnessLastErrorText { get; private init; } = string.Empty;
        public bool RunHistoricalFreshnessButtonIsEnabled { get; private init; }
        public string RunHistoricalFreshnessButtonLabel { get; private init; } = string.Empty;

        public static IntelStatusDetailsProjection Create(IntelUpdateStatusSnapshot snapshot, bool isShuttingDown)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var liveFeed = snapshot.LiveFeed ?? new R2Z2LiveFeedSnapshot();
            var todaysFreshness = snapshot.TodaysFreshness ?? new TodaysFreshnessSnapshot();
            var historicalFreshness = snapshot.HistoricalFreshness ?? new HistoricalFreshnessSnapshot();

            var todaysIsRunning = IsFreshnessRunning(todaysFreshness.Status);
            var historicalIsRunning = IsFreshnessRunning(historicalFreshness.Status);

            return new IntelStatusDetailsProjection
            {
                LastUpdatedText = FormatTimestamp(snapshot.LastSuccessfulUpdateAtUtc, "No successful local intel update recorded yet."),
                OldestKillmailDayText = FormatDay(snapshot.EarliestCompleteDayUtc, "No local killmail days recorded yet."),
                NewestKillmailDayText = FormatDay(snapshot.LatestCompleteDayUtc, "No local killmail days recorded yet."),
                CurrentUpdateStatusText = PublicDataStatusTextBuilder.BuildIntelCurrentUpdateStatusText(snapshot),
                TotalProgressIsIndeterminate = snapshot.TotalProgressIsIndeterminate,
                TotalProgressValue = snapshot.TotalProgressIsIndeterminate ? 0 : ClampProgress(snapshot.TotalProgressPercent),
                TotalProgressText = string.IsNullOrWhiteSpace(snapshot.TotalProgressText) ? "No update currently running." : snapshot.TotalProgressText,
                CurrentDayProgressIsIndeterminate = snapshot.CurrentDayProgressIsIndeterminate,
                CurrentDayProgressValue = snapshot.CurrentDayProgressIsIndeterminate ? 0 : ClampProgress(snapshot.CurrentDayProgressPercent),
                CurrentDayProgressText = string.IsNullOrWhiteSpace(snapshot.CurrentDayProgressText) ? "No update currently running." : snapshot.CurrentDayProgressText,
                LiveFeedSourceText = string.IsNullOrWhiteSpace(liveFeed.Source) ? "R2Z2" : liveFeed.Source,
                LiveFeedStatusText = BuildLiveFeedStatusText(liveFeed),
                LiveFeedEnabledText = liveFeed.Enabled ? "Yes" : "No",
                LiveFeedRecentImportsText = liveFeed.RecentLiveImportsCount.ToString(CultureInfo.InvariantCulture),
                LiveFeedNextSequenceText = liveFeed.NextSequenceId.HasValue ? liveFeed.NextSequenceId.Value.ToString(CultureInfo.InvariantCulture) : "Not initialized",
                LiveFeedLastProcessedSequenceText = liveFeed.LastProcessedSequenceId.HasValue ? liveFeed.LastProcessedSequenceId.Value.ToString(CultureInfo.InvariantCulture) : "None",
                LiveFeedLastSuccessText = FormatTimestamp(liveFeed.LastSuccessAtUtc, "No live imports recorded yet."),
                LiveFeedLastCaughtUpText = FormatTimestamp(liveFeed.LastCaughtUpAtUtc, "No caught-up wait recorded yet."),
                LiveFeedLastErrorText = BuildLiveFeedLastErrorText(liveFeed),
                TodaysFreshnessStatusText = BuildFreshnessStatusText(todaysFreshness.Status, todaysFreshness.NextRetryAtUtc),
                TodaysFreshnessVisiblePilotsText = todaysFreshness.VisiblePilotsTargeted.ToString(CultureInfo.InvariantCulture),
                TodaysFreshnessEntitiesQueriedText = todaysFreshness.EntitiesQueried.ToString(CultureInfo.InvariantCulture),
                TodaysFreshnessResultsFoundText = todaysFreshness.ZkillResultsFound.ToString(CultureInfo.InvariantCulture),
                TodaysFreshnessKnownSkippedText = todaysFreshness.AlreadyKnownCount.ToString(CultureInfo.InvariantCulture),
                TodaysFreshnessImportedText = todaysFreshness.NewKillmailsImported.ToString(CultureInfo.InvariantCulture),
                TodaysFreshnessFailedText = todaysFreshness.FailedCount.ToString(CultureInfo.InvariantCulture),
                TodaysFreshnessLastRunText = FormatTimestamp(todaysFreshness.LastRunAtUtc, "No Today's Freshness run recorded yet."),
                TodaysFreshnessDetailText = string.IsNullOrWhiteSpace(todaysFreshness.DetailText) ? "Today's Freshness is idle." : todaysFreshness.DetailText,
                TodaysFreshnessLastErrorText = string.IsNullOrWhiteSpace(todaysFreshness.LastError) ? "No Today's Freshness errors recorded." : todaysFreshness.LastError,
                RunTodaysFreshnessButtonIsEnabled = !todaysIsRunning && !historicalIsRunning && !isShuttingDown,
                RunTodaysFreshnessButtonLabel = todaysIsRunning ? "Today's Freshness Running..." : historicalIsRunning ? "Historical Freshness Running..." : "Refresh Today's zKill Intel",
                HistoricalFreshnessStatusText = BuildFreshnessStatusText(historicalFreshness.Status, historicalFreshness.NextRetryAtUtc),
                HistoricalFreshnessModeText = string.IsNullOrWhiteSpace(historicalFreshness.Mode) ? "Not run yet" : historicalFreshness.Mode,
                HistoricalFreshnessVisiblePilotsText = historicalFreshness.VisiblePilotsTargeted.ToString(CultureInfo.InvariantCulture),
                HistoricalFreshnessCandidatesConsideredText = historicalFreshness.CandidatePilotsConsidered.ToString(CultureInfo.InvariantCulture),
                HistoricalFreshnessCandidatesSkippedCooldownText = historicalFreshness.CandidatePilotsSkippedCooldown.ToString(CultureInfo.InvariantCulture),
                HistoricalFreshnessPilotsCheckedText = historicalFreshness.PilotsChecked.ToString(CultureInfo.InvariantCulture),
                HistoricalFreshnessDaysCheckedText = historicalFreshness.HistoricalDaysChecked.ToString(CultureInfo.InvariantCulture),
                HistoricalFreshnessEntitiesQueriedText = historicalFreshness.EntitiesQueried.ToString(CultureInfo.InvariantCulture),
                HistoricalFreshnessResultsFoundText = historicalFreshness.ZkillResultsFound.ToString(CultureInfo.InvariantCulture),
                HistoricalFreshnessKnownSkippedText = historicalFreshness.AlreadyKnownCount.ToString(CultureInfo.InvariantCulture),
                HistoricalFreshnessImportedText = historicalFreshness.MissingImportedCount.ToString(CultureInfo.InvariantCulture),
                HistoricalFreshnessFailedText = historicalFreshness.FailedCount.ToString(CultureInfo.InvariantCulture),
                HistoricalFreshnessLastRunText = FormatTimestamp(historicalFreshness.LastRunAtUtc, "No Historical Freshness run recorded yet."),
                HistoricalFreshnessDetailText = string.IsNullOrWhiteSpace(historicalFreshness.DetailText) ? "Historical Freshness is idle." : historicalFreshness.DetailText,
                HistoricalFreshnessLastErrorText = string.IsNullOrWhiteSpace(historicalFreshness.LastError) ? "No Historical Freshness errors recorded." : historicalFreshness.LastError,
                RunHistoricalFreshnessButtonIsEnabled = !historicalIsRunning && !todaysIsRunning && !isShuttingDown,
                RunHistoricalFreshnessButtonLabel = historicalIsRunning ? "Historical Freshness Running..." : todaysIsRunning ? "Today's Freshness Running..." : "Repair Recent Historical Intel"
            };
        }

        private static string BuildLiveFeedStatusText(R2Z2LiveFeedSnapshot liveFeed)
        {
            var statusText = string.IsNullOrWhiteSpace(liveFeed.Status) ? "Disabled" : liveFeed.Status;
            var nextRetryText = FormatTimestamp(liveFeed.NextRetryAtUtc, string.Empty);
            if (!string.IsNullOrWhiteSpace(nextRetryText) &&
                (statusText.Contains("wait", StringComparison.OrdinalIgnoreCase) ||
                 statusText.Contains("backing off", StringComparison.OrdinalIgnoreCase)))
            {
                statusText = $"{statusText} (retry {nextRetryText})";
            }

            return statusText;
        }

        private static string BuildLiveFeedLastErrorText(R2Z2LiveFeedSnapshot liveFeed)
        {
            var lastErrorTime = FormatTimestamp(liveFeed.LastErrorAtUtc, string.Empty);
            if (string.IsNullOrWhiteSpace(liveFeed.LastError))
            {
                return "No live-feed errors recorded.";
            }

            return string.IsNullOrWhiteSpace(lastErrorTime)
                ? liveFeed.LastError
                : $"{lastErrorTime} - {liveFeed.LastError}";
        }

        private static string BuildFreshnessStatusText(string status, string nextRetryAtUtc)
        {
            var statusText = string.IsNullOrWhiteSpace(status) ? "Idle" : status;
            var nextRetryText = FormatTimestamp(nextRetryAtUtc, string.Empty);
            if (!string.IsNullOrWhiteSpace(nextRetryText) &&
                statusText.Contains("rate limited", StringComparison.OrdinalIgnoreCase))
            {
                statusText = $"{statusText} (retry {nextRetryText})";
            }

            return statusText;
        }

        private static bool IsFreshnessRunning(string status)
        {
            return string.Equals(status, "Running", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(status, "Backing off / rate limited", StringComparison.OrdinalIgnoreCase);
        }

        private static double ClampProgress(double progress)
        {
            return Math.Max(0, Math.Min(100, progress));
        }

        private static string FormatTimestamp(string value, string emptyText)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return emptyText;
            }

            return DateTime.TryParse(value, out var parsed)
                ? parsed.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                : value;
        }

        private static string FormatDay(string value, string emptyText)
        {
            return string.IsNullOrWhiteSpace(value) ? emptyText : value;
        }
    }
}
