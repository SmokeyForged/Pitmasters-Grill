namespace PitmastersGrill.Models
{
    public sealed class HistoricalFreshnessSnapshot
    {
        public string Status { get; set; } = "Idle";
        public string Mode { get; set; } = "";
        public int CandidatePilotsConsidered { get; set; }
        public int CandidatePilotsSkippedCooldown { get; set; }
        public int PilotsChecked { get; set; }
        public int VisiblePilotsTargeted { get; set; }
        public int HistoricalDaysChecked { get; set; }
        public int EntitiesQueried { get; set; }
        public int ZkillResultsFound { get; set; }
        public int AlreadyKnownCount { get; set; }
        public int MissingImportedCount { get; set; }
        public int FailedCount { get; set; }
        public string LastRunAtUtc { get; set; } = "";
        public string LastError { get; set; } = "";
        public string NextRetryAtUtc { get; set; } = "";
        public string DetailText { get; set; } = "";
    }
}
