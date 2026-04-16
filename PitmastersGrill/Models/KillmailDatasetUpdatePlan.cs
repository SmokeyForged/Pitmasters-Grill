namespace PitmastersLittleGrill.Models
{
    public class KillmailDatasetUpdatePlan
    {
        public bool ShouldRun { get; set; }
        public string LatestCompleteDayUtc { get; set; } = "";
        public string RequiredThroughDayUtc { get; set; } = "";
        public string StartDayUtc { get; set; } = "";
        public string EndDayUtc { get; set; } = "";
        public int DayCount { get; set; }
    }
}