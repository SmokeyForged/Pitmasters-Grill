namespace PitmastersLittleGrill.Models
{
    public class PilotCynoObservationAggregate
    {
        public string CharacterId { get; set; } = "";
        public int? LastSeenCynoShipTypeId { get; set; }
        public string LastSeenCynoShipName { get; set; } = "";
        public string LastSeenCynoShipTimeUtc { get; set; } = "";
    }
}