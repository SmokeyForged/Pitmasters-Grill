using System;

namespace PitmastersGrill.Models
{
    public sealed class PerformanceTimingSnapshot
    {
        public DateTime TimestampUtc { get; set; }
        public string Stage { get; set; } = "";
        public long ElapsedMs { get; set; }
        public string Detail { get; set; } = "";
    }
}
