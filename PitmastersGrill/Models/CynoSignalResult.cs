using System.Collections.Generic;

namespace PitmastersGrill.Models
{
    public sealed class CynoSignalResult
    {
        public CynoSignalStatus Status { get; set; } = CynoSignalStatus.Unknown;
        public CynoSignalType SignalType { get; set; } = CynoSignalType.Unknown;
        public int Score { get; set; }
        public string SourceFreshness { get; set; } = "Unknown";
        public List<CynoEvidenceItem> Evidence { get; set; } = new();
        public List<string> Limitations { get; set; } = new();
    }
}
