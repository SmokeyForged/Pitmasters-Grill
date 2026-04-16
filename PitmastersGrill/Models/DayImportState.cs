namespace PitmastersGrill.Models
{
    public class DayImportState
    {
        public string DayUtc { get; set; } = "";
        public int RemoteTotalCount { get; set; }
        public int LocalImportedCount { get; set; }
        public string State { get; set; } = "not_present";
        public string ArchiveEtag { get; set; } = "";
        public string ArchiveLastModified { get; set; } = "";
        public string CheckedAtUtc { get; set; } = "";
        public string DownloadedAtUtc { get; set; } = "";
        public string ImportedAtUtc { get; set; } = "";
        public string NormalizedAtUtc { get; set; } = "";
        public string CompletedAtUtc { get; set; } = "";
        public string LastError { get; set; } = "";
    }
}