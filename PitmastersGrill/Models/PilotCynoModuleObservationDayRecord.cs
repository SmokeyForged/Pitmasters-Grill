namespace PitmastersGrill.Models
{
    public sealed class PilotCynoModuleObservationDayRecord
    {
        public string DayUtc { get; set; } = "";
        public string CharacterId { get; set; } = "";
        public string KillmailId { get; set; } = "";
        public string KillmailTimeUtc { get; set; } = "";
        public int? VictimShipTypeId { get; set; }
        public int ModuleTypeId { get; set; }
        public string ModuleName { get; set; } = "";
        public int QuantityDestroyed { get; set; }
        public int QuantityDropped { get; set; }
        public string ItemState { get; set; } = "";
        public string Source { get; set; } = "";
        public string UpdatedAtUtc { get; set; } = "";
    }
}
