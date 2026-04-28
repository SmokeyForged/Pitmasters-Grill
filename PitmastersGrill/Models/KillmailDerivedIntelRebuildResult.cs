namespace PitmastersGrill.Models
{
    public sealed class KillmailDerivedIntelRebuildResult
    {
        public bool Success { get; set; }
        public bool Cancelled { get; set; }
        public bool NoLocalSourceAvailable { get; set; }
        public bool WasBoundedRebuild { get; set; }
        public int TotalCandidateDays { get; set; }
        public int DaysScanned { get; set; }
        public int DaysWithLocalExtract { get; set; }
        public int DaysDownloaded { get; set; }
        public int DaysFailed { get; set; }
        public int KillmailsScanned { get; set; }
        public int ConfirmedCynoModuleObservationsFound { get; set; }
        public string Message { get; set; } = "";
    }
}
