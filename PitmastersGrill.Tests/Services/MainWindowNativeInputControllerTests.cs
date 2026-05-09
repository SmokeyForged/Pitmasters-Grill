using PitmastersGrill.Services;
using System;
using System.Collections.Generic;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class MainWindowNativeInputControllerTests
    {
        [Fact]
        public void Attach_RegistersExpectedHotkeys_AndLogsFailures()
        {
            var controller = new MainWindowNativeInputController();
            var registered = new List<int>();
            var infoLogs = new List<string>();
            var warnLogs = new List<string>();

            controller.Attach(
                new IntPtr(42),
                _ => true,
                (_, id, _, _) =>
                {
                    registered.Add(id);
                    return id != 12;
                },
                modControl: 2,
                globalResetWindowHotKeyId: 11,
                globalClearBoardHotKeyId: 12,
                globalToggleBoardModeHotKeyId: 13,
                infoLogs.Add,
                warnLogs.Add,
                () => 99);

            Assert.Equal(new[] { 12, 13, 11 }, registered);
            Assert.Contains(infoLogs, message => message.Contains("Global Insert board-mode hotkey registered.", StringComparison.Ordinal));
            Assert.Contains(infoLogs, message => message.Contains("Global Ctrl+Home reset-window hotkey registered.", StringComparison.Ordinal));
            Assert.Contains(warnLogs, message => message.Contains("Global Delete clear-board hotkey registration failed. error=99", StringComparison.Ordinal));
        }

        [Fact]
        public void Detach_UnregistersOnlyRegisteredHotkeys()
        {
            var controller = new MainWindowNativeInputController();
            var unregistered = new List<int>();

            controller.Attach(
                new IntPtr(42),
                _ => true,
                (_, _, _, _) => true,
                modControl: 2,
                globalResetWindowHotKeyId: 11,
                globalClearBoardHotKeyId: 12,
                globalToggleBoardModeHotKeyId: 13,
                _ => { },
                _ => { },
                () => 0);

            controller.Detach(
                new IntPtr(42),
                _ => true,
                (_, id) =>
                {
                    unregistered.Add(id);
                    return true;
                },
                globalResetWindowHotKeyId: 11,
                globalClearBoardHotKeyId: 12,
                globalToggleBoardModeHotKeyId: 13,
                _ => { },
                _ => { },
                () => 0);

            Assert.Equal(new[] { 12, 13, 11 }, unregistered);
        }
    }
}
