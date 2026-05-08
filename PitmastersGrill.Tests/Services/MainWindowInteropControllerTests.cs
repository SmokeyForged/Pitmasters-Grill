using PitmastersGrill.Services;
using System;
using System.Windows.Input;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class MainWindowInteropControllerTests
    {
        [Fact]
        public void RouteWindowMessage_ForClipboardUpdate_SchedulesClipboardProcessing()
        {
            var controller = new MainWindowInteropController();

            var result = controller.RouteWindowMessage(
                messageId: 0x031D,
                hotKeyToken: IntPtr.Zero,
                isWindowActive: true,
                clipboardUpdateMessageId: 0x031D,
                hotKeyMessageId: 0x0312,
                globalResetWindowHotKeyId: 1,
                globalClearBoardHotKeyId: 2,
                globalToggleBoardModeHotKeyId: 3);

            Assert.False(result.Handled);
            Assert.Equal(MainWindowMessageAction.ScheduleClipboardProcessing, result.Action);
        }

        [Fact]
        public void RouteWindowMessage_ForInactiveResetHotkey_RequestsWindowReset()
        {
            var controller = new MainWindowInteropController();

            var result = controller.RouteWindowMessage(
                messageId: 0x0312,
                hotKeyToken: new IntPtr(11),
                isWindowActive: false,
                clipboardUpdateMessageId: 0x031D,
                hotKeyMessageId: 0x0312,
                globalResetWindowHotKeyId: 11,
                globalClearBoardHotKeyId: 12,
                globalToggleBoardModeHotKeyId: 13);

            Assert.True(result.Handled);
            Assert.Equal(MainWindowMessageAction.RequestWindowLayoutReset, result.Action);
        }

        [Theory]
        [InlineData(Key.Insert, false, false, MainWindowKeyboardAction.ToggleCompactMode)]
        [InlineData(Key.Delete, false, false, MainWindowKeyboardAction.ClearBoard)]
        [InlineData(Key.Home, true, false, MainWindowKeyboardAction.RequestWindowLayoutReset)]
        [InlineData(Key.Home, false, true, MainWindowKeyboardAction.None)]
        [InlineData(Key.Home, false, false, MainWindowKeyboardAction.RefreshClipboard)]
        public void RoutePreviewKey_MapsExpectedActions(
            Key key,
            bool controlModifierPressed,
            bool isTextEditing,
            MainWindowKeyboardAction expected)
        {
            var controller = new MainWindowInteropController();

            var result = controller.RoutePreviewKey(key, controlModifierPressed, isTextEditing);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void HandleEscapeTap_OnThirdTapWithinWindow_RequestsShutdown()
        {
            var controller = new MainWindowInteropController();
            var firstTap = new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc);

            var result = controller.HandleEscapeTap(
                firstTap.AddMilliseconds(500),
                firstTap,
                2,
                tripleEscapeWindowMilliseconds: 1500);

            Assert.Equal(3, result.EscapeTapCount);
            Assert.True(result.ShouldRequestShutdown);
        }
    }
}
