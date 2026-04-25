namespace PitmastersGrill.Models
{
    public class AppSettings
    {
        public bool DarkModeEnabled { get; set; } = true;

        public bool AlwaysOnTopEnabled { get; set; } = false;

        public double WindowOpacityPercent { get; set; } = 100;

        public bool PanelModeEnabled { get; set; } = false;

        public bool? ShowSigColumn { get; set; } = true;

        public bool? ShowAllianceColumn { get; set; } = true;

        public bool? ShowCorpColumn { get; set; } = true;

        public bool? ShowKillsColumn { get; set; } = true;

        public bool? ShowLossesColumn { get; set; } = true;

        public bool? ShowAvgFleetSizeColumn { get; set; } = true;

        public bool? ShowLastShipSeenColumn { get; set; } = true;

        public bool? ShowLastSeenColumn { get; set; } = true;

        public bool? ShowCynoHullSeenColumn { get; set; } = true;

        public int MaxKillmailAgeDays { get; set; } = 30;

        public string KillmailDataRootPath { get; set; } = string.Empty;

        public AppLogLevel LogLevel { get; set; } = AppLogLevel.Normal;
    }
}
