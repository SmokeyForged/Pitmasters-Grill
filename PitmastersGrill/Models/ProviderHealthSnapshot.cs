using System;

namespace PitmastersGrill.Models
{
    public sealed class ProviderHealthSnapshot
    {
        public string ProviderName { get; set; } = "";
        public string Status { get; set; } = "Unknown";
        public DateTime? LastSuccessUtc { get; set; }
        public DateTime? LastFailureUtc { get; set; }
        public int RecentFailureCount { get; set; }
        public string LastErrorSummary { get; set; } = "";
        public double AverageLatencyMs { get; set; }
        public bool IsBackoffActive { get; set; }
        public DateTime? BackoffUntilUtc { get; set; }
        public long CacheHitCount { get; set; }
        public long CacheMissCount { get; set; }
        public string CacheHitMissDisplay => $"{CacheHitCount}/{CacheMissCount}";
    }
}
