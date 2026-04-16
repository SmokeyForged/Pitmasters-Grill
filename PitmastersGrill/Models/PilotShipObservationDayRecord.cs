namespace PitmastersGrill.Models
{
    public class PilotShipObservationDayRecord
    {
        public string DayUtc { get; set; } = "";
        public string CharacterId { get; set; } = "";
        public int? LastSeenShipTypeId { get; set; }
        public string LastSeenShipTimeUtc { get; set; } = "";
        public int? LastSeenCynoShipTypeId { get; set; }
        public string LastSeenCynoShipName { get; set; } = "";
        public string LastSeenCynoShipTimeUtc { get; set; } = "";
        public string UpdatedAtUtc { get; set; } = "";
    }
}