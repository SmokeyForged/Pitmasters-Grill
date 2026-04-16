namespace PitmastersGrill.Models
{
    public class PilotRegistryDayRecord
    {
        public string DayUtc { get; set; } = "";
        public string CharacterId { get; set; } = "";
        public string FirstSeenKillmailTimeUtc { get; set; } = "";
        public string LastSeenKillmailTimeUtc { get; set; } = "";
        public int SeenCount { get; set; }
        public string UpdatedAtUtc { get; set; } = "";
    }
}