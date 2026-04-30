namespace PitmastersGrill.Models
{
    public class IntelUpdateStatusSnapshot
    {
        public bool IsRunning { get; set; }
        public bool IsCurrentThroughYesterday { get; set; }
        public bool HasError { get; set; }
        public bool IsForegroundPriorityActive { get; set; }

        public string EarliestCompleteDayUtc { get; set; } = "";
        public string LatestCompleteDayUtc { get; set; } = "";
        public string RequiredThroughDayUtc { get; set; } = "";
        public string RequestedStartDayUtc { get; set; } = "";
        public string CurrentImportDayUtc { get; set; } = "";
        public string LastSuccessfulUpdateAtUtc { get; set; } = "";
        public bool IsRequestedCoverageComplete { get; set; }
        public bool HasRequestedCoverageWindow { get; set; }
        public int RequestedHistoryDays { get; set; }
        public int RequestedCoverageDays { get; set; }
        public int LocalCoverageDays { get; set; }
        public int MissingDayCount { get; set; }
        public int TotalDaysInCurrentRun { get; set; }
        public int CompletedDaysInCurrentRun { get; set; }

        public string StatusText { get; set; } = "";
        public string DetailText { get; set; } = "";
        public string ErrorText { get; set; } = "";
        public bool TotalProgressIsIndeterminate { get; set; }
        public double TotalProgressPercent { get; set; }
        public string TotalProgressText { get; set; } = "";
        public bool CurrentDayProgressIsIndeterminate { get; set; }
        public double CurrentDayProgressPercent { get; set; }
        public string CurrentDayProgressText { get; set; } = "";
        public R2Z2LiveFeedSnapshot LiveFeed { get; set; } = new();
        public TodaysFreshnessSnapshot TodaysFreshness { get; set; } = new();
        public HistoricalFreshnessSnapshot HistoricalFreshness { get; set; } = new();
    }
}
