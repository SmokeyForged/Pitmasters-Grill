using PitmastersGrill.Services;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class PilotDetailWindowPlacementControllerTests
    {
        [Fact]
        public void BuildPlacement_PrefersRightWhenAvailable()
        {
            var controller = new PilotDetailWindowPlacementController();

            var result = controller.BuildPlacement(
                detailWidth: 430,
                detailHeight: 360,
                ownerLeft: 100,
                ownerTop: 50,
                ownerWidth: 760,
                workLeft: 0,
                workTop: 0,
                workRight: 1600,
                workBottom: 900,
                preferLeft: false,
                detailWindowGap: 8);

            Assert.Equal("right", result.FinalSide);
            Assert.False(result.WasAdjusted);
        }

        [Fact]
        public void BuildPlacement_FallsBackToLeftWhenRightDoesNotFit()
        {
            var controller = new PilotDetailWindowPlacementController();

            var result = controller.BuildPlacement(
                detailWidth: 430,
                detailHeight: 360,
                ownerLeft: 900,
                ownerTop: 50,
                ownerWidth: 500,
                workLeft: 0,
                workTop: 0,
                workRight: 1400,
                workBottom: 900,
                preferLeft: false,
                detailWindowGap: 8);

            Assert.Equal("right", result.PreferredSide);
            Assert.Equal("left", result.FinalSide);
            Assert.True(result.WasAdjusted);
        }

        [Fact]
        public void BuildPlacement_ClampsIntoWorkArea()
        {
            var controller = new PilotDetailWindowPlacementController();

            var result = controller.BuildPlacement(
                detailWidth: 430,
                detailHeight: 360,
                ownerLeft: 100,
                ownerTop: 800,
                ownerWidth: 300,
                workLeft: 0,
                workTop: 0,
                workRight: 900,
                workBottom: 900,
                preferLeft: true,
                detailWindowGap: 8);

            Assert.True(result.Top <= 540);
            Assert.True(result.WasAdjusted);
        }
    }
}
