using System.Collections.Generic;

namespace PitmastersGrill.Models
{
    public sealed class KillmailDerivedParseResult
    {
        public string KillmailId { get; set; } = "";
        public string KillmailTimeUtc { get; set; } = "";
        public string DayUtc { get; set; } = "";
        public List<KillmailRegistryPilotSeen> RegistryPilots { get; set; } = new();
        public List<KillmailFleetPilotSeen> FleetPilots { get; set; } = new();
        public List<KillmailShipPilotSeen> ShipPilots { get; set; } = new();
        public List<PilotCynoModuleObservationDayRecord> CynoModuleObservations { get; set; } = new();
        public List<PilotBaitObservationDayRecord> BaitObservations { get; set; } = new();
        public List<PilotCynoTackleObservationDayRecord> CynoTackleObservations { get; set; } = new();
    }

    public sealed class KillmailRegistryPilotSeen
    {
        public string CharacterId { get; set; } = "";
        public string FirstSeenKillmailTimeUtc { get; set; } = "";
        public string LastSeenKillmailTimeUtc { get; set; } = "";
    }

    public sealed class KillmailFleetPilotSeen
    {
        public string CharacterId { get; set; } = "";
        public int AttackerCountForThisKillmail { get; set; }
    }

    public sealed class KillmailShipPilotSeen
    {
        public string CharacterId { get; set; } = "";
        public int? LastSeenShipTypeId { get; set; }
        public string LastSeenShipTimeUtc { get; set; } = "";
    }
}
