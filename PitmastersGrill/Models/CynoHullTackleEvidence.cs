using System;

namespace PitmastersGrill.Models
{
    public sealed class CynoHullTackleEvidence
    {
        public string CharacterId { get; set; } = "";
        public string KillmailId { get; set; } = "";
        public DateTime? KillmailTimeUtc { get; set; }
        public int? VictimShipTypeId { get; set; }
        public string VictimShipName { get; set; } = "";
        public int TackleModuleTypeId { get; set; }
        public string TackleModuleName { get; set; } = "";
        public TackleModuleType TackleType { get; set; } = TackleModuleType.UnknownTackle;
        public int QuantityDestroyed { get; set; }
        public int QuantityDropped { get; set; }
        public string Source { get; set; } = "public loss victim item list";
    }
}
