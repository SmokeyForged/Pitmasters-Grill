namespace PitmastersLittleGrill.Models
{
    public class AppSettings
    {
        public bool DarkModeEnabled { get; set; } = true;

        public bool AlwaysOnTopEnabled { get; set; } = false;

        public double WindowOpacityPercent { get; set; } = 100;

        // Optional override for killmail storage.
        // Leave blank to use the default LocalAppData location.
        public string KillmailDataRootPath { get; set; } = string.Empty;
    }
}