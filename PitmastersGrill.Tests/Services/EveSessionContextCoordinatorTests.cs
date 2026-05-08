using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class EveSessionContextCoordinatorTests
    {
        [Fact]
        public void CreatePendingContext_ReturnsExpectedStartupPlaceholder()
        {
            var coordinator = new EveSessionContextCoordinator();

            var result = coordinator.CreatePendingContext();

            Assert.Equal("Waiting for local context", result.CharacterName);
            Assert.Equal("Soft local read pending", result.EvidenceSource);
            Assert.Equal("Waiting for local session evidence", result.StatusMessage);
        }

        [Fact]
        public void ShouldTriggerRefresh_WhenContextIsFreshAndNotForced_ReturnsFalse()
        {
            var coordinator = new EveSessionContextCoordinator();
            var nowUtc = new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc);

            var result = coordinator.ShouldTriggerRefresh(
                isShuttingDown: false,
                force: false,
                currentContext: new EveSessionContext { CharacterName = "Aura" },
                lastRefreshUtc: nowUtc.AddMinutes(-1),
                isRefreshInFlight: false,
                nowUtc: nowUtc);

            Assert.False(result);
        }

        [Fact]
        public void ShouldTriggerRefresh_WhenForcedAndNotShuttingDown_ReturnsTrue()
        {
            var coordinator = new EveSessionContextCoordinator();

            var result = coordinator.ShouldTriggerRefresh(
                isShuttingDown: false,
                force: true,
                currentContext: new EveSessionContext(),
                lastRefreshUtc: DateTime.UtcNow,
                isRefreshInFlight: false,
                nowUtc: DateTime.UtcNow);

            Assert.True(result);
        }

        [Fact]
        public void BuildProjection_FormatsDisplayFallbacksAndStatus()
        {
            var coordinator = new EveSessionContextCoordinator();
            var context = new EveSessionContext
            {
                CharacterName = "Aura",
                SolarSystemName = string.Empty,
                EvidenceSource = string.Empty,
                EvidenceTimestampUtc = new DateTime(2026, 5, 7, 15, 4, 5, DateTimeKind.Utc),
                Confidence = "High",
                StatusMessage = "Observed via log probe"
            };

            var result = coordinator.BuildProjection(context);

            Assert.Equal("Aura", result.CharacterText);
            Assert.Equal("Not detected", result.SystemText);
            Assert.Equal("Not configured", result.EvidenceSourceText);
            Assert.Contains("2026-05-07", result.ObservedAtText);
            Assert.Equal("High | Observed via log probe", result.StatusText);
        }
    }
}
