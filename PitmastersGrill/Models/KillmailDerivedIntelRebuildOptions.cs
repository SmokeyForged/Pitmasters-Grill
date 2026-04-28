namespace PitmastersGrill.Models
{
    public sealed class KillmailDerivedIntelRebuildOptions
    {
        public int MaxDaysToScan { get; set; } = 7;
        public bool RebuildAllImportedDays { get; set; }
        public bool DownloadMissingSourceArchives { get; set; } = true;
        public string PreferredDayUtc { get; set; } = "";

        public static KillmailDerivedIntelRebuildOptions RecentImportedDays(int maxDays = 7)
        {
            return new KillmailDerivedIntelRebuildOptions
            {
                MaxDaysToScan = maxDays <= 0 ? 7 : maxDays,
                RebuildAllImportedDays = false,
                DownloadMissingSourceArchives = true
            };
        }
    }
}
