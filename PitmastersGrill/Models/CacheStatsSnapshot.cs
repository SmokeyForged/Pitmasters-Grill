using System;
using System.Collections.Generic;

namespace PitmastersGrill.Models
{
    public sealed class CacheStatsSnapshot
    {
        public string DatabasePathDisplay { get; set; } = "";
        public long DatabaseSizeBytes { get; set; }
        public Dictionary<string, long> TableCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> ExpiredCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public DateTime? OldestCachedRecordUtc { get; set; }
        public DateTime? NewestCachedRecordUtc { get; set; }
        public DateTime? LastMaintenanceUtc { get; set; }
        public string Status { get; set; } = "";
    }
}
