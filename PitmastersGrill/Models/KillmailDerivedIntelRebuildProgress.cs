using System;

namespace PitmastersGrill.Models
{
    public sealed class KillmailDerivedIntelRebuildProgress
    {
        public string CurrentDayUtc { get; set; } = "";
        public int CurrentDayIndex { get; set; }
        public int TotalDays { get; set; }
        public int KillmailsScanned { get; set; }
        public int ConfirmedCynoModuleObservationsFound { get; set; }
        public int DaysDownloaded { get; set; }
        public int DaysFailed { get; set; }
        public string Phase { get; set; } = "Idle";
        public string Detail { get; set; } = "";

        public double PercentComplete
        {
            get
            {
                if (TotalDays <= 0 || CurrentDayIndex <= 0)
                {
                    return 0;
                }

                return Math.Max(0, Math.Min(100, (double)CurrentDayIndex / TotalDays * 100.0));
            }
        }

        public string ToDisplayText()
        {
            var dayPart = string.IsNullOrWhiteSpace(CurrentDayUtc)
                ? ""
                : $" day={CurrentDayUtc}";

            var progressPart = TotalDays > 0
                ? $" ({Math.Min(CurrentDayIndex, TotalDays)}/{TotalDays})"
                : "";

            var detailPart = string.IsNullOrWhiteSpace(Detail)
                ? ""
                : $" - {Detail}";

            return $"{Phase}{progressPart}{dayPart}: killmails={KillmailsScanned:N0}, cynoModules={ConfirmedCynoModuleObservationsFound:N0}, downloadedDays={DaysDownloaded:N0}, failedDays={DaysFailed:N0}{detailPart}";
        }
    }
}
