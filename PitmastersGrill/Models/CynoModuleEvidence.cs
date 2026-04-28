using System;

namespace PitmastersGrill.Models
{
    public sealed class CynoModuleEvidence
    {
        public string CharacterId { get; set; } = "";
        public int TypeId { get; set; }
        public string TypeName { get; set; } = "";
        public string KillmailId { get; set; } = "";
        public DateTime? KillmailTimeUtc { get; set; }
        public int? ShipTypeId { get; set; }
        public string ShipName { get; set; } = "";
        public int QuantityDestroyed { get; set; }
        public int QuantityDropped { get; set; }
        public string ItemState { get; set; } = "";
        public string Source { get; set; } = "public loss victim item list";
    }
}
