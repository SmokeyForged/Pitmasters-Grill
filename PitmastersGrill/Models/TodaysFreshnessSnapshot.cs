namespace PitmastersGrill.Models
{
    public sealed class TodaysFreshnessSnapshot
    {
        public string Status { get; set; } = "Idle";
        public int VisiblePilotsTargeted { get; set; }
        public int EntitiesQueried { get; set; }
        public int ZkillResultsFound { get; set; }
        public int AlreadyKnownCount { get; set; }
        public int NewKillmailsImported { get; set; }
        public int FailedCount { get; set; }
        public string LastRunAtUtc { get; set; } = "";
        public string LastError { get; set; } = "";
        public string NextRetryAtUtc { get; set; } = "";
        public string DetailText { get; set; } = "";
    }
}
