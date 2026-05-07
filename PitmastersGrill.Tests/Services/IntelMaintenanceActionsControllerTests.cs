using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class IntelMaintenanceActionsControllerTests
    {
        [Fact]
        public async Task RunRebuildKillmailDerivedIntelAsync_WhenClipboardProcessing_ShowsBlockedMessageAndSkipsRebuild()
        {
            var statusMessages = new List<string>();
            var dialogs = new List<(string Message, string Title, MessageBoxButton Buttons, MessageBoxImage Image)>();
            var rebuildInvoked = false;

            var controller = CreateController(
                isClipboardProcessing: () => true,
                setDiagnosticsStatus: statusMessages.Add,
                rebuildDerivedIntelAsync: _ =>
                {
                    rebuildInvoked = true;
                    return Task.FromResult(new KillmailDerivedIntelRebuildResult());
                },
                showDialog: (message, title, buttons, image) =>
                {
                    dialogs.Add((message, title, buttons, image));
                    return MessageBoxResult.OK;
                });

            await controller.RunRebuildKillmailDerivedIntelAsync();

            Assert.False(rebuildInvoked);
            Assert.Contains("Derived intel rebuild blocked while a lookup is active.", statusMessages);
            Assert.Single(dialogs);
            Assert.Equal("PMG Killmail Derived Intel", dialogs[0].Title);
        }

        [Fact]
        public async Task RunRebuildKillmailDerivedIntelAsync_WhenCancelledAtConfirmation_SetsCancelledStatus()
        {
            var statusMessages = new List<string>();
            var dialogs = new List<(string Message, string Title, MessageBoxButton Buttons, MessageBoxImage Image)>();
            var rebuildInvoked = false;

            var controller = CreateController(
                setDiagnosticsStatus: statusMessages.Add,
                rebuildDerivedIntelAsync: _ =>
                {
                    rebuildInvoked = true;
                    return Task.FromResult(new KillmailDerivedIntelRebuildResult());
                },
                showDialog: (message, title, buttons, image) =>
                {
                    dialogs.Add((message, title, buttons, image));
                    return MessageBoxResult.No;
                });

            await controller.RunRebuildKillmailDerivedIntelAsync();

            Assert.False(rebuildInvoked);
            Assert.Contains("Derived intel rebuild cancelled.", statusMessages);
            Assert.Single(dialogs);
            Assert.Equal(MessageBoxButton.YesNo, dialogs[0].Buttons);
        }

        [Fact]
        public async Task RunRebuildKillmailDerivedIntelAsync_WhenSuccessful_RefreshesUiAndUsesSourceMissingTitleWhenNeeded()
        {
            var statusMessages = new List<string>();
            var rebuildButtonStates = new List<bool>();
            var dialogs = new List<(string Message, string Title, MessageBoxButton Buttons, MessageBoxImage Image)>();
            var refreshCacheStatsCalls = 0;
            var refreshConfirmedStateCalls = 0;

            var controller = CreateController(
                setDiagnosticsStatus: statusMessages.Add,
                setRebuildButtonEnabled: rebuildButtonStates.Add,
                rebuildDerivedIntelAsync: _ => Task.FromResult(new KillmailDerivedIntelRebuildResult
                {
                    Message = "No imported or locally extracted killmail archive days were found.",
                    NoLocalSourceAvailable = true
                }),
                refreshCacheStatsUi: () => refreshCacheStatsCalls++,
                refreshConfirmedCynoModuleStateForCurrentRows: () => refreshConfirmedStateCalls++,
                showDialog: (message, title, buttons, image) =>
                {
                    dialogs.Add((message, title, buttons, image));
                    return dialogs.Count == 1 ? MessageBoxResult.Yes : MessageBoxResult.OK;
                });

            await controller.RunRebuildKillmailDerivedIntelAsync();

            Assert.Equal(new[] { false, true }, rebuildButtonStates);
            Assert.Equal(1, refreshCacheStatsCalls);
            Assert.Equal(1, refreshConfirmedStateCalls);
            Assert.Contains("Rebuilding killmail derived intel...", statusMessages);
            Assert.Contains("No imported or locally extracted killmail archive days were found.", statusMessages);
            Assert.Equal(2, dialogs.Count);
            Assert.Equal("PMG Killmail Derived Intel Source Missing", dialogs[1].Title);
            Assert.Equal(MessageBoxImage.Information, dialogs[1].Image);
        }

        [Fact]
        public async Task RunEnableKillmailDbPullAsync_TogglesButtonAndInvokesServiceWithSeedDays()
        {
            var buttonStates = new List<bool>();
            var invokedSeedDays = -1;
            var invokedToken = CancellationToken.None;

            var controller = CreateController(
                setEnableKillmailDbPullButtonEnabled: buttonStates.Add,
                getSeedDays: () => 45,
                enableKillmailDbPullAsync: (seedDays, token) =>
                {
                    invokedSeedDays = seedDays;
                    invokedToken = token;
                    return Task.CompletedTask;
                });

            await controller.RunEnableKillmailDbPullAsync();

            Assert.Equal(new[] { false, true }, buttonStates);
            Assert.Equal(45, invokedSeedDays);
            Assert.Equal(CancellationToken.None, invokedToken);
        }

        private static IntelMaintenanceActionsController CreateController(
            Func<bool>? isClipboardProcessing = null,
            Action<string>? setDiagnosticsStatus = null,
            Action<bool>? setRebuildButtonEnabled = null,
            Action<bool>? setEnableKillmailDbPullButtonEnabled = null,
            Func<int>? getSeedDays = null,
            Func<bool>? isShuttingDown = null,
            CancellationToken shutdownToken = default,
            Func<CancellationToken, Task<KillmailDerivedIntelRebuildResult>>? rebuildDerivedIntelAsync = null,
            Func<int, CancellationToken, Task>? enableKillmailDbPullAsync = null,
            Action? refreshCacheStatsUi = null,
            Action? refreshConfirmedCynoModuleStateForCurrentRows = null,
            Func<string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult>? showDialog = null)
        {
            return new IntelMaintenanceActionsController(
                isClipboardProcessing ?? (() => false),
                setDiagnosticsStatus ?? (_ => { }),
                setRebuildButtonEnabled ?? (_ => { }),
                setEnableKillmailDbPullButtonEnabled ?? (_ => { }),
                getSeedDays ?? (() => 30),
                isShuttingDown ?? (() => false),
                shutdownToken,
                rebuildDerivedIntelAsync ?? (_ => Task.FromResult(new KillmailDerivedIntelRebuildResult { Message = "ok" })),
                enableKillmailDbPullAsync ?? ((_, _) => Task.CompletedTask),
                refreshCacheStatsUi ?? (() => { }),
                refreshConfirmedCynoModuleStateForCurrentRows ?? (() => { }),
                showDialog ?? ((_, _, _, _) => MessageBoxResult.OK));
        }
    }
}
