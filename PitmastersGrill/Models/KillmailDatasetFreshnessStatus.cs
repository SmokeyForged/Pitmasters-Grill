namespace PitmastersGrill.Models
{
    public class KillmailDatasetFreshnessStatus
    {
        public string EarliestCompleteDayUtc { get; set; } = "";
        public string LatestCompleteDayUtc { get; set; } = "";
        public string RequiredThroughDayUtc { get; set; } = "";
        public string RequestedStartDayUtc { get; set; } = "";
        public bool IsCurrentThroughRequiredDay { get; set; }
        public bool IsRequestedCoverageComplete { get; set; }
        public bool HasRequestedCoverageWindow { get; set; }
        public int RequestedHistoryDays { get; set; }
        public int RequestedCoverageDays { get; set; }
        public int LocalCoverageDays { get; set; }
        public int MissingDayCount { get; set; }
        public string FirstMissingDayUtc { get; set; } = "";
        public string LastMissingDayUtc { get; set; } = "";
    }
}
