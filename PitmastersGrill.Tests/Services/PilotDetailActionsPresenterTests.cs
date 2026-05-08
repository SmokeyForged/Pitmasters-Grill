using PitmastersGrill.Models;
using PitmastersGrill.Services;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class PilotDetailActionsPresenterTests
    {
        [Fact]
        public void BuildWatchPilotActionState_WithNoRow_DisablesAction()
        {
            var presenter = new PilotDetailActionsPresenter();

            var result = presenter.BuildWatchPilotActionState(null);

            Assert.False(result.IsEnabled);
            Assert.Equal("Watch", result.Content);
            Assert.Equal("SuccessGreenBrush", result.ForegroundResourceKey);
        }

        [Fact]
        public void BuildWatchPilotActionState_WithWatchedResolvedRow_ShowsUnwatch()
        {
            var presenter = new PilotDetailActionsPresenter();
            var row = new PilotBoardRow
            {
                CharacterId = "123",
                IsWatched = true
            };

            var result = presenter.BuildWatchPilotActionState(row);

            Assert.True(result.IsEnabled);
            Assert.Equal("Unwatch", result.Content);
            Assert.Equal("WatchedPilotMarkerBrush", result.ForegroundResourceKey);
        }

        [Fact]
        public void BuildIgnoreAllianceActionState_WithIgnoredAlliance_DisablesAction()
        {
            var presenter = new PilotDetailActionsPresenter();
            var row = new PilotBoardRow
            {
                AllianceId = "456",
                AllianceName = "TEST"
            };

            var result = presenter.BuildIgnoreAllianceActionState(row, allianceAlreadyIgnored: true);

            Assert.False(result.IsEnabled);
            Assert.Equal("This alliance is already on the ignore list.", result.ToolTip);
        }

        [Fact]
        public void BuildIgnoreAllianceActionState_WithKnownAlliance_BuildsTooltip()
        {
            var presenter = new PilotDetailActionsPresenter();
            var row = new PilotBoardRow
            {
                AllianceId = "456",
                AllianceName = "TEST"
            };

            var result = presenter.BuildIgnoreAllianceActionState(row, allianceAlreadyIgnored: false);

            Assert.True(result.IsEnabled);
            Assert.Equal("Ignore alliance 'TEST' (456).", result.ToolTip);
        }
    }
}
