using PitmastersGrill.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PitmastersGrill.Services
{
    public static class DiagnosticTelemetry
    {
        private const int MaxTimings = 250;
        private const int MaxCynoSignals = 100;
        private const int MaxIgnoreSuppressionSamples = 100;
        private static readonly object SyncRoot = new();
        private static readonly Dictionary<string, MutableProviderHealth> Providers = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Queue<PerformanceTimingSnapshot> Timings = new();
        private static readonly Queue<string> CynoSignals = new();
        private static readonly Queue<string> IgnoreSuppressionSamples = new();
        private static string _lastClipboardSummary = "No clipboard classification recorded yet.";
        private static long _ignoreSuppressionCount;

        public static IDisposable BeginTiming(string stage, string detail = "")
        {
            return new TimingScope(stage, detail);
        }

        public static void RecordTiming(string stage, long elapsedMs, string detail = "")
        {
            lock (SyncRoot)
            {
                Timings.Enqueue(new PerformanceTimingSnapshot
                {
                    TimestampUtc = DateTime.UtcNow,
                    Stage = stage,
                    ElapsedMs = elapsedMs,
                    Detail = Sanitize(detail)
                });

                while (Timings.Count > MaxTimings)
                {
                    Timings.Dequeue();
                }
            }
        }

        public static void RecordClipboardSummary(string summary)
        {
            lock (SyncRoot)
            {
                _lastClipboardSummary = Sanitize(summary);
            }
        }

        public static string GetClipboardSummary()
        {
            lock (SyncRoot)
            {
                return _lastClipboardSummary;
            }
        }

        public static void RecordProviderOutcome<T>(ProviderOutcome<T> outcome, long elapsedMs)
        {
            if (outcome == null)
            {
                return;
            }

            RecordProviderOutcome(
                string.IsNullOrWhiteSpace(outcome.ProviderName) ? "unknown_provider" : outcome.ProviderName,
                outcome.Kind,
                outcome.Detail,
                elapsedMs,
                outcome.RetryAfterUtc);
        }

        public static void RecordProviderOutcome(string providerName, ProviderOutcomeKind kind, string detail, long elapsedMs, DateTime? retryAfterUtc = null)
        {
            lock (SyncRoot)
            {
                var health = GetOrCreateProvider(providerName);
                health.Latencies.Add(Math.Max(0, elapsedMs));
                while (health.Latencies.Count > 50)
                {
                    health.Latencies.RemoveAt(0);
                }

                if (kind == ProviderOutcomeKind.Success || kind == ProviderOutcomeKind.NotFound || kind == ProviderOutcomeKind.Skipped)
                {
                    health.LastSuccessUtc = DateTime.UtcNow;
                }
                else
                {
                    health.LastFailureUtc = DateTime.UtcNow;
                    health.RecentFailureCount++;
                    health.LastErrorSummary = Sanitize(detail);
                }

                if (kind == ProviderOutcomeKind.Throttled)
                {
                    health.BackoffUntilUtc = retryAfterUtc;
                }
            }
        }

        public static void RecordCacheHit(string providerName)
        {
            lock (SyncRoot)
            {
                GetOrCreateProvider(providerName).CacheHitCount++;
            }
        }

        public static void RecordCacheMiss(string providerName)
        {
            lock (SyncRoot)
            {
                GetOrCreateProvider(providerName).CacheMissCount++;
            }
        }

        public static List<ProviderHealthSnapshot> GetProviderHealthSnapshots()
        {
            lock (SyncRoot)
            {
                EnsureKnownProvider("resolver_cache");
                EnsureKnownProvider("stats_cache");
                EnsureKnownProvider("esi_exact_name");
                EnsureKnownProvider("esi_public_affiliation");
                EnsureKnownProvider("zkill_search");
                EnsureKnownProvider("zkill_stats");

                return Providers.Values
                    .Select(x => x.ToSnapshot())
                    .OrderBy(x => x.ProviderName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        public static List<PerformanceTimingSnapshot> GetRecentTimings()
        {
            lock (SyncRoot)
            {
                return Timings.Reverse().ToList();
            }
        }

        public static void RecordCynoSignalSummary(string summary)
        {
            lock (SyncRoot)
            {
                CynoSignals.Enqueue(Sanitize(summary));
                while (CynoSignals.Count > MaxCynoSignals)
                {
                    CynoSignals.Dequeue();
                }
            }
        }

        public static List<string> GetRecentCynoSignalSummaries()
        {
            lock (SyncRoot)
            {
                return CynoSignals.Reverse().ToList();
            }
        }

        public static void RecordIgnoreSuppression(string summary)
        {
            lock (SyncRoot)
            {
                _ignoreSuppressionCount++;
                IgnoreSuppressionSamples.Enqueue(Sanitize(summary));
                while (IgnoreSuppressionSamples.Count > MaxIgnoreSuppressionSamples)
                {
                    IgnoreSuppressionSamples.Dequeue();
                }
            }
        }

        public static long GetIgnoreSuppressionCount()
        {
            lock (SyncRoot)
            {
                return _ignoreSuppressionCount;
            }
        }

        public static List<string> GetRecentIgnoreSuppressionSamples()
        {
            lock (SyncRoot)
            {
                return IgnoreSuppressionSamples.Reverse().ToList();
            }
        }

        private static MutableProviderHealth GetOrCreateProvider(string providerName)
        {
            providerName = string.IsNullOrWhiteSpace(providerName) ? "unknown_provider" : providerName.Trim();
            if (!Providers.TryGetValue(providerName, out var health))
            {
                health = new MutableProviderHealth(providerName);
                Providers[providerName] = health;
            }

            return health;
        }

        private static void EnsureKnownProvider(string providerName)
        {
            _ = GetOrCreateProvider(providerName);
        }

        private static string Sanitize(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? ""
                : value.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private sealed class TimingScope : IDisposable
        {
            private readonly string _stage;
            private readonly string _detail;
            private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

            public TimingScope(string stage, string detail)
            {
                _stage = stage;
                _detail = detail;
            }

            public void Dispose()
            {
                _stopwatch.Stop();
                RecordTiming(_stage, _stopwatch.ElapsedMilliseconds, _detail);
            }
        }

        private sealed class MutableProviderHealth
        {
            public MutableProviderHealth(string providerName)
            {
                ProviderName = providerName;
            }

            public string ProviderName { get; }
            public DateTime? LastSuccessUtc { get; set; }
            public DateTime? LastFailureUtc { get; set; }
            public int RecentFailureCount { get; set; }
            public string LastErrorSummary { get; set; } = "";
            public DateTime? BackoffUntilUtc { get; set; }
            public long CacheHitCount { get; set; }
            public long CacheMissCount { get; set; }
            public List<long> Latencies { get; } = new();

            public ProviderHealthSnapshot ToSnapshot()
            {
                var backoffActive = BackoffUntilUtc.HasValue && BackoffUntilUtc.Value > DateTime.UtcNow;
                var status = "Unknown";

                if (backoffActive)
                {
                    status = "Degraded";
                }
                else if (LastFailureUtc.HasValue && (!LastSuccessUtc.HasValue || LastFailureUtc > LastSuccessUtc))
                {
                    status = RecentFailureCount >= 3 ? "Offline" : "Degraded";
                }
                else if (LastSuccessUtc.HasValue)
                {
                    status = "Healthy";
                }

                return new ProviderHealthSnapshot
                {
                    ProviderName = ProviderName,
                    Status = status,
                    LastSuccessUtc = LastSuccessUtc,
                    LastFailureUtc = LastFailureUtc,
                    RecentFailureCount = RecentFailureCount,
                    LastErrorSummary = LastErrorSummary,
                    AverageLatencyMs = Latencies.Count == 0 ? 0 : Latencies.Average(),
                    IsBackoffActive = backoffActive,
                    BackoffUntilUtc = BackoffUntilUtc,
                    CacheHitCount = CacheHitCount,
                    CacheMissCount = CacheMissCount
                };
            }
        }
    }
}
