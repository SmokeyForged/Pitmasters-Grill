namespace PitmastersGrill.Models
{
    public class PilotLastShipObservationAggregate
    {
        public string CharacterId { get; set; } = "";
        public int? LastSeenShipTypeId { get; set; }
        public string LastSeenShipTimeUtc { get; set; } = "";
        public string LastSeenShipName { get; set; } = "";
    }
}