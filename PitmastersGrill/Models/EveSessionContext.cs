using System;

namespace PitmastersGrill.Models
{
    public sealed class EveSessionContext
    {
        public string CharacterName { get; init; } = "Not detected";
        public string SolarSystemName { get; init; } = "Not detected";
        public string EvidenceSource { get; init; } = "Not configured";
        public DateTime? EvidenceTimestampUtc { get; init; }
        public string Confidence { get; init; } = "None";
        public string StatusMessage { get; init; } = "Unable to infer EVE context";
    }
}
