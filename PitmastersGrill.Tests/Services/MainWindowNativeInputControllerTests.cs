using PitmastersGrill.Services;
using System;
using System.Collections.Generic;
using System.Windows.Input;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class MainWindowNativeInputControllerTests
    {
        [Fact]
        public void Attach_UsesAdapterInExpectedOrder_AndLogsFailures()
        {
            var controller = new MainWindowNativeInputController();
            var nativeInputApi = new FakeNativeInputApi
            {
                LastError = 99,
                RegisterResultById = { [12] = false, [13] = true, [11] = true }
            };
            var infoLogs = new List<string>();
            var warnLogs = new List<string>();

            controller.Attach(
                new IntPtr(42),
                nativeInputApi,
                modControl: 2,
                globalResetWindowHotKeyId: 11,
                globalClearBoardHotKeyId: 12,
                globalToggleBoardModeHotKeyId: 13,
                infoLogs.Add,
                warnLogs.Add);

            Assert.Equal(new IntPtr(42), nativeInputApi.ClipboardListenerAddedFor);
            Assert.Equal(
                new[]
                {
                    new HotKeyRegistration(12, 0, (uint)KeyInterop.VirtualKeyFromKey(Key.Delete)),
                    new HotKeyRegistration(13, 0, (uint)KeyInterop.VirtualKeyFromKey(Key.Insert)),
                    new HotKeyRegistration(11, 2, (uint)KeyInterop.VirtualKeyFromKey(Key.Home))
                },
                nativeInputApi.Registrations);
            Assert.Contains(infoLogs, message => message.Contains("Global Insert board-mode hotkey registered.", StringComparison.Ordinal));
            Assert.Contains(infoLogs, message => message.Contains("Global Ctrl+Home reset-window hotkey registered.", StringComparison.Ordinal));
            Assert.Contains(warnLogs, message => message.Contains("Global Delete clear-board hotkey registration failed. error=99", StringComparison.Ordinal));
        }

        [Fact]
        public void Detach_UnregistersOnlySuccessfullyRegisteredHotkeys_AndRemovesClipboardListener()
        {
            var controller = new MainWindowNativeInputController();
            var nativeInputApi = new FakeNativeInputApi
            {
                RegisterResultById = { [12] = false, [13] = true, [11] = true }
            };

            controller.Attach(
                new IntPtr(42),
                nativeInputApi,
                modControl: 2,
                globalResetWindowHotKeyId: 11,
                globalClearBoardHotKeyId: 12,
                globalToggleBoardModeHotKeyId: 13,
                _ => { },
                _ => { });

            controller.Detach(
                new IntPtr(42),
                nativeInputApi,
                globalResetWindowHotKeyId: 11,
                globalClearBoardHotKeyId: 12,
                globalToggleBoardModeHotKeyId: 13,
                _ => { },
                _ => { });

            Assert.Equal(new[] { 13, 11 }, nativeInputApi.Unregistrations);
            Assert.Equal(new IntPtr(42), nativeInputApi.ClipboardListenerRemovedFor);

            controller.Detach(
                new IntPtr(42),
                nativeInputApi,
                globalResetWindowHotKeyId: 11,
                globalClearBoardHotKeyId: 12,
                globalToggleBoardModeHotKeyId: 13,
                _ => { },
                _ => { });

            Assert.Equal(new[] { 13, 11 }, nativeInputApi.Unregistrations);
        }

        [Fact]
        public void AttachAndDetach_WithZeroHwnd_AreSafeNoOps()
        {
            var controller = new MainWindowNativeInputController();
            var nativeInputApi = new FakeNativeInputApi();

            controller.Attach(
                IntPtr.Zero,
                nativeInputApi,
                modControl: 2,
                globalResetWindowHotKeyId: 11,
                globalClearBoardHotKeyId: 12,
                globalToggleBoardModeHotKeyId: 13,
                _ => { },
                _ => { });

            controller.Detach(
                IntPtr.Zero,
                nativeInputApi,
                globalResetWindowHotKeyId: 11,
                globalClearBoardHotKeyId: 12,
                globalToggleBoardModeHotKeyId: 13,
                _ => { },
                _ => { });

            Assert.Null(nativeInputApi.ClipboardListenerAddedFor);
            Assert.Null(nativeInputApi.ClipboardListenerRemovedFor);
            Assert.Empty(nativeInputApi.Registrations);
            Assert.Empty(nativeInputApi.Unregistrations);
        }

        [Fact]
        public void Detach_LogsAdapterError_WhenRegisteredHotkeyCannotUnregister()
        {
            var controller = new MainWindowNativeInputController();
            var nativeInputApi = new FakeNativeInputApi
            {
                LastError = 55,
                RegisterResultById = { [12] = true, [13] = false, [11] = false },
                UnregisterResultById = { [12] = false }
            };
            var warnLogs = new List<string>();

            controller.Attach(
                new IntPtr(42),
                nativeInputApi,
                modControl: 2,
                globalResetWindowHotKeyId: 11,
                globalClearBoardHotKeyId: 12,
                globalToggleBoardModeHotKeyId: 13,
                _ => { },
                _ => { });

            controller.Detach(
                new IntPtr(42),
                nativeInputApi,
                globalResetWindowHotKeyId: 11,
                globalClearBoardHotKeyId: 12,
                globalToggleBoardModeHotKeyId: 13,
                _ => { },
                warnLogs.Add);

            Assert.Contains(warnLogs, message => message.Contains("Global Delete clear-board hotkey unregister failed. error=55", StringComparison.Ordinal));
        }

        private readonly record struct HotKeyRegistration(int Id, uint Modifiers, uint VirtualKey);

        private sealed class FakeNativeInputApi : INativeInputApi
        {
            public Dictionary<int, bool> RegisterResultById { get; } = new();
            public Dictionary<int, bool> UnregisterResultById { get; } = new();
            public List<HotKeyRegistration> Registrations { get; } = new();
            public List<int> Unregistrations { get; } = new();
            public IntPtr? ClipboardListenerAddedFor { get; private set; }
            public IntPtr? ClipboardListenerRemovedFor { get; private set; }
            public int LastError { get; init; }

            public bool AddClipboardFormatListener(IntPtr hwnd)
            {
                ClipboardListenerAddedFor = hwnd;
                return true;
            }

            public bool RemoveClipboardFormatListener(IntPtr hwnd)
            {
                ClipboardListenerRemovedFor = hwnd;
                return true;
            }

            public bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey)
            {
                Registrations.Add(new HotKeyRegistration(id, modifiers, virtualKey));
                return !RegisterResultById.TryGetValue(id, out var result) || result;
            }

            public bool UnregisterHotKey(IntPtr hwnd, int id)
            {
                Unregistrations.Add(id);
                return !UnregisterResultById.TryGetValue(id, out var result) || result;
            }

            public int GetLastError() => LastError;
        }
    }
}
