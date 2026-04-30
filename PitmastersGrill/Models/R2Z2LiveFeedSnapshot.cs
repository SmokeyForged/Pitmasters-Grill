namespace PitmastersGrill.Models
{
    public sealed class R2Z2LiveFeedSnapshot
    {
        public string Source { get; set; } = "R2Z2";
        public bool Enabled { get; set; }
        public string Status { get; set; } = "Disabled";
        public long? NextSequenceId { get; set; }
        public long? LastProcessedSequenceId { get; set; }
        public string LastSuccessAtUtc { get; set; } = "";
        public string LastCaughtUpAtUtc { get; set; } = "";
        public string LastErrorAtUtc { get; set; } = "";
        public string LastError { get; set; } = "";
        public string NextRetryAtUtc { get; set; } = "";
        public int RecentLiveImportsCount { get; set; }
    }
}
