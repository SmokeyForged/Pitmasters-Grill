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
            INativeInputApi nativeInputApi,
            uint modControl,
            int globalResetWindowHotKeyId,
            int globalClearBoardHotKeyId,
            int globalToggleBoardModeHotKeyId,
            Action<string> logInfo,
            Action<string> logWarn)
        {
            ArgumentNullException.ThrowIfNull(nativeInputApi);

            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            nativeInputApi.AddClipboardFormatListener(hwnd);

            _globalClearBoardHotKeyRegistered = nativeInputApi.RegisterHotKey(
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
                logWarn($"Global Delete clear-board hotkey registration failed. error={nativeInputApi.GetLastError()}");
            }

            _globalToggleBoardModeHotKeyRegistered = nativeInputApi.RegisterHotKey(
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
                logWarn($"Global Insert board-mode hotkey registration failed. error={nativeInputApi.GetLastError()}");
            }

            _globalResetWindowHotKeyRegistered = nativeInputApi.RegisterHotKey(
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
                logWarn($"Global Ctrl+Home hotkey registration failed. win32Error={nativeInputApi.GetLastError()}");
            }
        }

        public void Detach(
            IntPtr hwnd,
            INativeInputApi nativeInputApi,
            int globalResetWindowHotKeyId,
            int globalClearBoardHotKeyId,
            int globalToggleBoardModeHotKeyId,
            Action<string> logInfo,
            Action<string> logWarn)
        {
            ArgumentNullException.ThrowIfNull(nativeInputApi);

            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            if (_globalClearBoardHotKeyRegistered)
            {
                if (nativeInputApi.UnregisterHotKey(hwnd, globalClearBoardHotKeyId))
                {
                    logInfo("Global Delete clear-board hotkey unregistered.");
                }
                else
                {
                    logWarn($"Global Delete clear-board hotkey unregister failed. error={nativeInputApi.GetLastError()}");
                }

                _globalClearBoardHotKeyRegistered = false;
            }

            if (_globalToggleBoardModeHotKeyRegistered)
            {
                if (nativeInputApi.UnregisterHotKey(hwnd, globalToggleBoardModeHotKeyId))
                {
                    logInfo("Global Insert board-mode hotkey unregistered.");
                }
                else
                {
                    logWarn($"Global Insert board-mode hotkey unregister failed. error={nativeInputApi.GetLastError()}");
                }

                _globalToggleBoardModeHotKeyRegistered = false;
            }

            if (_globalResetWindowHotKeyRegistered)
            {
                if (!nativeInputApi.UnregisterHotKey(hwnd, globalResetWindowHotKeyId))
                {
                    logWarn($"Global Ctrl+Home hotkey unregistration failed. win32Error={nativeInputApi.GetLastError()}");
                }

                _globalResetWindowHotKeyRegistered = false;
            }

            nativeInputApi.RemoveClipboardFormatListener(hwnd);
        }
    }
}
