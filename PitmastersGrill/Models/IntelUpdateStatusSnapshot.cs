namespace PitmastersGrill.Models
{
    public class IntelUpdateStatusSnapshot
    {
        public bool IsRunning { get; set; }
        public bool IsCurrentThroughYesterday { get; set; }
        public bool HasError { get; set; }
        public bool IsForegroundPriorityActive { get; set; }

        public string LatestCompleteDayUtc { get; set; } = "";
        public string RequiredThroughDayUtc { get; set; } = "";
        public string CurrentImportDayUtc { get; set; } = "";
        public int MissingDayCount { get; set; }

        public string StatusText { get; set; } = "";
        public string DetailText { get; set; } = "";
        public string ErrorText { get; set; } = "";
    }
}