namespace PitmastersGrill.Models
{
    public class ResolverCacheEntry
    {
        public string CharacterName { get; set; } = "";
        public string CharacterId { get; set; } = "";

        public string AllianceId { get; set; } = "";
        public string AllianceName { get; set; } = "";
        public string AllianceTicker { get; set; } = "";

        public string CorpId { get; set; } = "";
        public string CorpName { get; set; } = "";
        public string CorpTicker { get; set; } = "";

        public string ResolverConfidence { get; set; } = "";
        public string ResolvedAtUtc { get; set; } = "";
        public string ExpiresAtUtc { get; set; } = "";
        public string AffiliationCheckedAtUtc { get; set; } = "";
    }
}
