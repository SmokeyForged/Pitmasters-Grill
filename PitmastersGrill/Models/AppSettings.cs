using System.Collections.Generic;

namespace PitmastersGrill.Models
{
    public class AppSettings
    {
        public bool DarkModeEnabled { get; set; } = true;

        public string VisualTheme { get; set; } = "CharcoalOps";

        public string ColorBlindMode { get; set; } = "Standard";

        public string PilotDetailPlacementPreference { get; set; } = "AutoPreferRight";

        public bool AlwaysOnTopEnabled { get; set; } = false;

        public double WindowOpacityPercent { get; set; } = 100;

        public bool PanelModeEnabled { get; set; } = false;

        public bool CompactModeEnabled { get; set; } = false;

        public double? SavedWindowLeft { get; set; }

        public double? SavedWindowTop { get; set; }

        public double? SavedWindowWidth { get; set; }

        public double? SavedWindowHeight { get; set; }

        public bool SavedWindowIsMaximized { get; set; } = false;

        public bool ShowCorpAllianceCounts { get; set; } = false;

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

        public bool LiveZkillFeedEnabled { get; set; } = false;

        public string KillmailDataRootPath { get; set; } = string.Empty;

        public List<BoardColumnLayoutSetting> BoardColumnLayout { get; set; } = new();

        public AppLogLevel LogLevel { get; set; } = AppLogLevel.Normal;
    }
}
