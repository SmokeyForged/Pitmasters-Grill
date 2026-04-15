namespace PitmastersLittleGrill.Models
{
    public class KillmailDatasetFreshnessStatus
    {
        public string EarliestCompleteDayUtc { get; set; } = "";
        public string LatestCompleteDayUtc { get; set; } = "";
        public string RequiredThroughDayUtc { get; set; } = "";
        public bool IsCurrentThroughRequiredDay { get; set; }
        public int MissingDayCount { get; set; }
        public string FirstMissingDayUtc { get; set; } = "";
        public string LastMissingDayUtc { get; set; } = "";
    }
}
