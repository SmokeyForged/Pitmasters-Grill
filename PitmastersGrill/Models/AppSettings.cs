namespace PitmastersGrill.Models
{
    public class AppSettings
    {
        public bool DarkModeEnabled { get; set; } = true;

        public bool AlwaysOnTopEnabled { get; set; } = false;

        public double WindowOpacityPercent { get; set; } = 100;

        public bool PanelModeEnabled { get; set; } = false;

        public int MaxKillmailAgeDays { get; set; } = 30;

        public string KillmailDataRootPath { get; set; } = string.Empty;

        public AppLogLevel LogLevel { get; set; } = AppLogLevel.Normal;
    }
}