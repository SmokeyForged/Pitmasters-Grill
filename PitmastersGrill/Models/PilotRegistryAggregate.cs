namespace PitmastersGrill.Models
{
    public class PilotRegistryAggregate
    {
        public string CharacterId { get; set; } = "";
        public string FirstSeenKillmailTimeUtc { get; set; } = "";
        public string LastSeenKillmailTimeUtc { get; set; } = "";
        public int SeenCount { get; set; }
    }
}