using PitmastersGrill.Services;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class CompactBoardDragControllerTests
    {
        [Fact]
        public void TryBegin_EligiblePointerDown_StartsPendingDrag()
        {
            var subject = new CompactBoardDragController();

            var started = subject.TryBegin(
                boardModeEnabled: true,
                clickCount: 1,
                blockedByInteractiveElement: false);

            Assert.True(started);
            Assert.True(subject.IsPending);
        }

        [Theory]
        [InlineData(false, 1, false)]
        [InlineData(true, 2, false)]
        [InlineData(true, 1, true)]
        public void TryBegin_IneligiblePointerDown_DoesNotStart(
            bool boardModeEnabled,
            int clickCount,
            bool blockedByInteractiveElement)
        {
            var subject = new CompactBoardDragController();

            var started = subject.TryBegin(
                boardModeEnabled,
                clickCount,
                blockedByInteractiveElement);

            Assert.False(started);
            Assert.False(subject.IsPending);
        }

        [Fact]
        public void TryBegin_DuplicateRoutedDelivery_LeavesExistingPendingDragIntact()
        {
            var subject = new CompactBoardDragController();
            Assert.True(subject.TryBegin(true, 1, false));

            var duplicateStarted = subject.TryBegin(
                boardModeEnabled: true,
                clickCount: 2,
                blockedByInteractiveElement: false);

            Assert.False(duplicateStarted);
            Assert.True(subject.IsPending);
        }

        [Fact]
        public void Cancel_ReleaseBeforeHold_ClearsPendingDrag()
        {
            var subject = PendingSubject();

            subject.Cancel();

            Assert.False(subject.IsPending);
        }

        [Fact]
        public void CancelIfLeftButtonReleased_LostButtonState_CancelsPendingDrag()
        {
            var subject = PendingSubject();

            var cancelled = subject.CancelIfLeftButtonReleased(leftButtonPressed: false);

            Assert.True(cancelled);
            Assert.False(subject.IsPending);
        }

        [Fact]
        public void CancelIfLeftButtonReleased_PressedButton_KeepsPendingDrag()
        {
            var subject = PendingSubject();

            var cancelled = subject.CancelIfLeftButtonReleased(leftButtonPressed: true);

            Assert.False(cancelled);
            Assert.True(subject.IsPending);
        }

        [Fact]
        public void CompleteHold_EligiblePendingDrag_RequestsDragExactlyOnce()
        {
            var subject = PendingSubject();

            var first = subject.CompleteHold(
                boardModeEnabled: true,
                leftButtonPressed: true);
            var second = subject.CompleteHold(
                boardModeEnabled: true,
                leftButtonPressed: true);

            Assert.True(first);
            Assert.False(second);
            Assert.False(subject.IsPending);
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        public void CompleteHold_WhenEligibilityIsLost_CancelsWithoutDrag(
            bool boardModeEnabled,
            bool leftButtonPressed)
        {
            var subject = PendingSubject();

            var shouldDrag = subject.CompleteHold(boardModeEnabled, leftButtonPressed);

            Assert.False(shouldDrag);
            Assert.False(subject.IsPending);
        }

        private static CompactBoardDragController PendingSubject()
        {
            var subject = new CompactBoardDragController();
            Assert.True(subject.TryBegin(true, 1, false));
            return subject;
        }
    }
}
