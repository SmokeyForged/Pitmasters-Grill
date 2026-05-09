using System;
using System.Windows.Input;

namespace PitmastersGrill.Services
{
    public sealed class MainWindowNativeInputController
    {
        private bool _globalResetWindowHotKeyRegistered;
        private bool _globalClearBoardHotKeyRegistered;
        private bool _globalToggleBoardModeHotKeyRegistered;

        public void Attach(
            IntPtr hwnd,
            Func<IntPtr, bool> addClipboardFormatListener,
            Func<IntPtr, int, uint, uint, bool> registerHotKey,
            uint modControl,
            int globalResetWindowHotKeyId,
            int globalClearBoardHotKeyId,
            int globalToggleBoardModeHotKeyId,
            Action<string> logInfo,
            Action<string> logWarn,
            Func<int> getLastWin32Error)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            addClipboardFormatListener(hwnd);

            _globalClearBoardHotKeyRegistered = registerHotKey(
                hwnd,
                globalClearBoardHotKeyId,
                0,
                (uint)KeyInterop.VirtualKeyFromKey(Key.Delete));

            if (_globalClearBoardHotKeyRegistered)
            {
                logInfo("Global Delete clear-board hotkey registered.");
            }
            else
            {
                logWarn($"Global Delete clear-board hotkey registration failed. error={getLastWin32Error()}");
            }

            _globalToggleBoardModeHotKeyRegistered = registerHotKey(
                hwnd,
                globalToggleBoardModeHotKeyId,
                0,
                (uint)KeyInterop.VirtualKeyFromKey(Key.Insert));

            if (_globalToggleBoardModeHotKeyRegistered)
            {
                logInfo("Global Insert board-mode hotkey registered.");
            }
            else
            {
                logWarn($"Global Insert board-mode hotkey registration failed. error={getLastWin32Error()}");
            }

            _globalResetWindowHotKeyRegistered = registerHotKey(
                hwnd,
                globalResetWindowHotKeyId,
                modControl,
                (uint)KeyInterop.VirtualKeyFromKey(Key.Home));

            if (_globalResetWindowHotKeyRegistered)
            {
                logInfo("Global Ctrl+Home reset-window hotkey registered.");
            }
            else
            {
                logWarn($"Global Ctrl+Home hotkey registration failed. win32Error={getLastWin32Error()}");
            }
        }

        public void Detach(
            IntPtr hwnd,
            Func<IntPtr, bool> removeClipboardFormatListener,
            Func<IntPtr, int, bool> unregisterHotKey,
            int globalResetWindowHotKeyId,
            int globalClearBoardHotKeyId,
            int globalToggleBoardModeHotKeyId,
            Action<string> logInfo,
            Action<string> logWarn,
            Func<int> getLastWin32Error)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            if (_globalClearBoardHotKeyRegistered)
            {
                if (unregisterHotKey(hwnd, globalClearBoardHotKeyId))
                {
                    logInfo("Global Delete clear-board hotkey unregistered.");
                }
                else
                {
                    logWarn($"Global Delete clear-board hotkey unregister failed. error={getLastWin32Error()}");
                }

                _globalClearBoardHotKeyRegistered = false;
            }

            if (_globalToggleBoardModeHotKeyRegistered)
            {
                if (unregisterHotKey(hwnd, globalToggleBoardModeHotKeyId))
                {
                    logInfo("Global Insert board-mode hotkey unregistered.");
                }
                else
                {
                    logWarn($"Global Insert board-mode hotkey unregister failed. error={getLastWin32Error()}");
                }

                _globalToggleBoardModeHotKeyRegistered = false;
            }

            if (_globalResetWindowHotKeyRegistered)
            {
                if (!unregisterHotKey(hwnd, globalResetWindowHotKeyId))
                {
                    logWarn($"Global Ctrl+Home hotkey unregistration failed. win32Error={getLastWin32Error()}");
                }

                _globalResetWindowHotKeyRegistered = false;
            }

            removeClipboardFormatListener(hwnd);
        }
    }
}
