using System;

namespace PitmastersGrill.Models
{
    public sealed class CynoEvidenceItem
    {
        public string Summary { get; set; } = "";
        public CynoSignalType SignalType { get; set; } = CynoSignalType.Unknown;
        public int ScoreContribution { get; set; }
        public bool IsConfirmedModuleEvidence { get; set; }
        public bool IsHullInference { get; set; }
        public string Source { get; set; } = "";
        public DateTime? ObservedAtUtc { get; set; }
        public string ShipName { get; set; } = "";
        public string KillmailId { get; set; } = "";
    }
}
