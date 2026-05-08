using PitmastersGrill.Services;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class PilotDetailWindowLifecycleControllerTests
    {
        [Fact]
        public void DecideOpenAction_WhenNoActiveWindow_ReturnsCreateNew()
        {
            var controller = new PilotDetailWindowLifecycleController();

            var result = controller.DecideOpenAction("Aura");

            Assert.Equal(PilotDetailWindowOpenAction.CreateNew, result);
        }

        [Fact]
        public void DecideOpenAction_WhenSameCharacterAlreadyOpen_ReturnsActivateExisting()
        {
            var controller = new PilotDetailWindowLifecycleController();
            controller.MarkWindowOpened("Aura");

            var result = controller.DecideOpenAction("aura");

            Assert.Equal(PilotDetailWindowOpenAction.ActivateExisting, result);
        }

        [Fact]
        public void DecideOpenAction_WhenDifferentCharacterAlreadyOpen_ReturnsReplaceExisting()
        {
            var controller = new PilotDetailWindowLifecycleController();
            controller.MarkWindowOpened("Aura");

            var result = controller.DecideOpenAction("Chribba");

            Assert.Equal(PilotDetailWindowOpenAction.ReplaceExisting, result);
        }

        [Fact]
        public void ShouldRefreshActiveWindow_MatchesActiveCharacter()
        {
            var controller = new PilotDetailWindowLifecycleController();
            controller.MarkWindowOpened("Aura");

            Assert.True(controller.ShouldRefreshActiveWindow("Aura"));
            Assert.False(controller.ShouldRefreshActiveWindow("Chribba"));
        }

        [Fact]
        public void ClearActiveWindow_RemovesTrackedState()
        {
            var controller = new PilotDetailWindowLifecycleController();
            controller.MarkWindowOpened("Aura");

            controller.ClearActiveWindow();

            Assert.False(controller.HasActiveWindow);
            Assert.Equal(string.Empty, controller.ActiveCharacterName);
        }
    }
}
