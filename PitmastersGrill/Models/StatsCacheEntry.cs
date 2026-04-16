namespace PitmastersLittleGrill.Models
{
    public class StatsCacheEntry
    {
        public string CharacterId { get; set; } = "";
        public int KillCount { get; set; }
        public int LossCount { get; set; }
        public double AvgAttackersWhenAttacking { get; set; }
        public string LastPublicCynoCapableHull { get; set; } = "";
        public string LastShipSeenName { get; set; } = "";
        public string LastShipSeenAtUtc { get; set; } = "";
        public string RefreshedAtUtc { get; set; } = "";
        public string ExpiresAtUtc { get; set; } = "";
    }
}