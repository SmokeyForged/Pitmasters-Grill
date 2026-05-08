using PitmastersGrill.Models;
using System;

namespace PitmastersGrill.Services
{
    public sealed record EveSessionContextProjection(
        string CharacterText,
        string SystemText,
        string EvidenceSourceText,
        string ObservedAtText,
        string StatusText);

    public sealed class EveSessionContextCoordinator
    {
        private static readonly TimeSpan DefaultStaleThreshold = TimeSpan.FromMinutes(3);

        public bool IsStale(EveSessionContext? currentContext, DateTime lastRefreshUtc, DateTime nowUtc)
        {
            return currentContext == null || (nowUtc - lastRefreshUtc) > DefaultStaleThreshold;
        }

        public bool ShouldTriggerRefresh(
            bool isShuttingDown,
            bool force,
            EveSessionContext? currentContext,
            DateTime lastRefreshUtc,
            bool isRefreshInFlight,
            DateTime nowUtc)
        {
            if (isShuttingDown || isRefreshInFlight)
            {
                return false;
            }

            return force || IsStale(currentContext, lastRefreshUtc, nowUtc);
        }

        public EveSessionContext CreatePendingContext()
        {
            return new EveSessionContext
            {
                CharacterName = "Waiting for local context",
                SolarSystemName = "Waiting for local context",
                EvidenceSource = "Soft local read pending",
                EvidenceTimestampUtc = null,
                Confidence = "Pending",
                StatusMessage = "Waiting for local session evidence"
            };
        }

        public EveSessionContext CreateFallbackContext()
        {
            return new EveSessionContext
            {
                CharacterName = "Not detected",
                SolarSystemName = "Not detected",
                EvidenceSource = "Unable to read local evidence",
                EvidenceTimestampUtc = null,
                Confidence = "None",
                StatusMessage = "Unable to infer EVE context"
            };
        }

        public EveSessionContextProjection BuildProjection(EveSessionContext context)
        {
            var characterText = string.IsNullOrWhiteSpace(context.CharacterName)
                ? "Not detected"
                : context.CharacterName;
            var systemText = string.IsNullOrWhiteSpace(context.SolarSystemName)
                ? "Not detected"
                : context.SolarSystemName;
            var evidenceSourceText = string.IsNullOrWhiteSpace(context.EvidenceSource)
                ? "Not configured"
                : context.EvidenceSource;
            var observedAtText = context.EvidenceTimestampUtc.HasValue
                ? context.EvidenceTimestampUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                : "Not detected";

            var statusParts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrWhiteSpace(context.Confidence))
            {
                statusParts.Add(context.Confidence);
            }

            if (!string.IsNullOrWhiteSpace(context.StatusMessage))
            {
                statusParts.Add(context.StatusMessage);
            }

            var statusText = statusParts.Count > 0
                ? string.Join(" | ", statusParts)
                : "Unable to infer EVE context";

            return new EveSessionContextProjection(
                characterText,
                systemText,
                evidenceSourceText,
                observedAtText,
                statusText);
        }
    }
}
