using PitmastersGrill.Services;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class CompactBoardDragControllerTests
    {
        [Fact]
        public void HoldDuration_IsThreeHundredMilliseconds()
        {
            Assert.Equal(TimeSpan.FromMilliseconds(300), CompactBoardDragController.HoldDuration);
        }

        [Fact]
        public void TryBegin_EligiblePointerDown_StartsPendingDrag()
        {
            var subject = new CompactBoardDragController();

            var started = subject.TryBegin(
                boardModeEnabled: true,
                clickCount: 1,
                source: new DependencyObject());

            Assert.True(started);
            Assert.True(subject.IsPending);
        }

        [Theory]
        [InlineData(false, 1)]
        [InlineData(true, 2)]
        public void TryBegin_IneligiblePointerDown_DoesNotStart(bool boardModeEnabled, int clickCount)
        {
            var subject = new CompactBoardDragController();

            var started = subject.TryBegin(boardModeEnabled, clickCount, source: null);

            Assert.False(started);
            Assert.False(subject.IsPending);
        }

        [Fact]
        public void TryBegin_DuplicateRoutedDelivery_LeavesExistingPendingDragIntact()
        {
            var subject = new CompactBoardDragController();
            Assert.True(subject.TryBegin(true, 1, source: null));

            var duplicateStarted = subject.TryBegin(
                boardModeEnabled: true,
                clickCount: 2,
                source: null);

            Assert.False(duplicateStarted);
            Assert.True(subject.IsPending);
        }

        [Fact]
        public void TryBegin_InteractiveControls_AreBlocked()
        {
            RunSta(() =>
            {
                DependencyObject[] blockedSources =
                {
                    new Button(),
                    new TextBox(),
                    new ComboBox(),
                    new ScrollBar(),
                    new Thumb()
                };

                foreach (var source in blockedSources)
                {
                    var subject = new CompactBoardDragController();

                    Assert.False(subject.TryBegin(true, 1, source));
                    Assert.False(subject.IsPending);
                }
            });
        }

        [Fact]
        public void TryBegin_DataGridRow_RemainsDragEligible()
        {
            RunSta(() =>
            {
                var subject = new CompactBoardDragController();
                var row = new DataGridRow();

                Assert.True(subject.TryBegin(true, 1, row));
                Assert.True(subject.IsPending);
            });
        }

        [Fact]
        public void TryBegin_ColumnHeaderAndOrdinaryHeaderContent_RemainDragEligible()
        {
            RunSta(() =>
            {
                var subject = new CompactBoardDragController();
                var header = new DataGridColumnHeader();
                var content = new Border();
                header.Content = content;

                Assert.True(subject.TryBegin(true, 1, content));
                Assert.True(subject.IsPending);
            });
        }

        [Fact]
        public void TryBegin_ResizeThumbUnderColumnHeader_IsBlocked()
        {
            RunSta(() =>
            {
                var subject = new CompactBoardDragController();
                var header = new DataGridColumnHeader();
                var thumb = new Thumb();
                header.Content = thumb;

                Assert.False(subject.TryBegin(true, 1, thumb));
                Assert.False(subject.IsPending);
            });
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
        public void CompleteHoldAction_EligiblePendingDrag_RequestsDragExactlyOnce()
        {
            var subject = PendingSubject();

            var first = subject.CompleteHoldAction(
                boardModeEnabled: true,
                leftButtonPressed: true);
            var second = subject.CompleteHoldAction(
                boardModeEnabled: true,
                leftButtonPressed: true);

            Assert.Equal(CompactBoardDragAction.RequestDrag, first);
            Assert.Equal(CompactBoardDragAction.None, second);
            Assert.False(subject.IsPending);
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        public void CompleteHoldAction_WhenEligibilityIsLost_CancelsWithoutDrag(
            bool boardModeEnabled,
            bool leftButtonPressed)
        {
            var subject = PendingSubject();

            var action = subject.CompleteHoldAction(boardModeEnabled, leftButtonPressed);

            Assert.Equal(CompactBoardDragAction.None, action);
            Assert.False(subject.IsPending);
        }

        private static CompactBoardDragController PendingSubject()
        {
            var subject = new CompactBoardDragController();
            Assert.True(subject.TryBegin(true, 1, source: null));
            return subject;
        }

        private static void RunSta(Action action)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }
    }
}
