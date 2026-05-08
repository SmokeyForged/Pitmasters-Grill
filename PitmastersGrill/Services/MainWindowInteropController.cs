using System;
using System.Windows.Input;

namespace PitmastersGrill.Services
{
    public enum MainWindowMessageAction
    {
        None = 0,
        ScheduleClipboardProcessing,
        RequestWindowLayoutReset,
        ClearBoard,
        ToggleCompactMode
    }

    public enum MainWindowKeyboardAction
    {
        None = 0,
        RequestWindowLayoutReset,
        ToggleCompactMode,
        ClearBoard,
        RefreshClipboard,
        HandleEscape
    }

    public sealed record MainWindowMessageRouteResult(bool Handled, MainWindowMessageAction Action);

    public sealed record MainWindowEscapeTapResult(DateTime LastEscapeTapUtc, int EscapeTapCount, bool ShouldRequestShutdown);

    public sealed class MainWindowInteropController
    {
        public MainWindowMessageRouteResult RouteWindowMessage(
            int messageId,
            IntPtr hotKeyToken,
            bool isWindowActive,
            int clipboardUpdateMessageId,
            int hotKeyMessageId,
            int globalResetWindowHotKeyId,
            int globalClearBoardHotKeyId,
            int globalToggleBoardModeHotKeyId)
        {
            if (messageId == clipboardUpdateMessageId)
            {
                return new MainWindowMessageRouteResult(false, MainWindowMessageAction.ScheduleClipboardProcessing);
            }

            if (messageId != hotKeyMessageId)
            {
                return new MainWindowMessageRouteResult(false, MainWindowMessageAction.None);
            }

            var hotKeyId = hotKeyToken.ToInt64();

            return hotKeyId switch
            {
                _ when hotKeyId == globalResetWindowHotKeyId =>
                    new MainWindowMessageRouteResult(true, isWindowActive ? MainWindowMessageAction.None : MainWindowMessageAction.RequestWindowLayoutReset),
                _ when hotKeyId == globalClearBoardHotKeyId =>
                    new MainWindowMessageRouteResult(true, MainWindowMessageAction.ClearBoard),
                _ when hotKeyId == globalToggleBoardModeHotKeyId =>
                    new MainWindowMessageRouteResult(true, MainWindowMessageAction.ToggleCompactMode),
                _ => new MainWindowMessageRouteResult(false, MainWindowMessageAction.None)
            };
        }

        public MainWindowKeyboardAction RoutePreviewKey(Key key, bool controlModifierPressed, bool isTextEditing)
        {
            if (controlModifierPressed && key == Key.Home)
            {
                return MainWindowKeyboardAction.RequestWindowLayoutReset;
            }

            if (isTextEditing)
            {
                return MainWindowKeyboardAction.None;
            }

            return key switch
            {
                Key.Insert => MainWindowKeyboardAction.ToggleCompactMode,
                Key.Delete => MainWindowKeyboardAction.ClearBoard,
                Key.Home => MainWindowKeyboardAction.RefreshClipboard,
                Key.Escape => MainWindowKeyboardAction.HandleEscape,
                _ => MainWindowKeyboardAction.None
            };
        }

        public MainWindowEscapeTapResult HandleEscapeTap(
            DateTime nowUtc,
            DateTime lastEscapeTapUtc,
            int escapeTapCount,
            int tripleEscapeWindowMilliseconds)
        {
            var nextEscapeTapCount = (nowUtc - lastEscapeTapUtc).TotalMilliseconds <= tripleEscapeWindowMilliseconds
                ? escapeTapCount + 1
                : 1;

            return new MainWindowEscapeTapResult(
                nowUtc,
                nextEscapeTapCount,
                nextEscapeTapCount >= 3);
        }
    }
}
