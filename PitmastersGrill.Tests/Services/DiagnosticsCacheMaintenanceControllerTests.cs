using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System.Collections.Generic;
using System.Windows;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class DiagnosticsCacheMaintenanceControllerTests
    {
        [Fact]
        public void RefreshProviderHealth_AppliesSnapshotsAndSetsStatus()
        {
            var applied = new List<IReadOnlyList<ProviderHealthSnapshot>>();
            var statuses = new List<string>();
            var controller = CreateController(
                setDiagnosticsStatus: statuses.Add,
                getProviderHealthSnapshots: () => new[]
                {
                    new ProviderHealthSnapshot { ProviderName = "esi" }
                },
                applyProviderHealthSnapshots: snapshots => applied.Add(snapshots));

            controller.RefreshProviderHealth();

            Assert.Single(applied);
            Assert.Single(applied[0]);
            Assert.Equal("Provider health refreshed.", statuses[0]);
        }

        [Fact]
        public void RefreshCacheStats_WhenStatsLoadFails_UsesPresenterFailureText()
        {
            var cacheStatsTexts = new List<string>();
            var controller = CreateController(
                getCacheStats: () => throw new System.InvalidOperationException("stats unavailable"),
                setCacheStatsText: cacheStatsTexts.Add);

            controller.RefreshCacheStatsUi();

            Assert.Single(cacheStatsTexts);
            Assert.Equal("Cache stats failed: stats unavailable", cacheStatsTexts[0]);
        }

        [Fact]
        public void ClearExpiredCache_WhenClipboardProcessing_ShowsBlockedMessageAndSkipsAction()
        {
            var statuses = new List<string>();
            var dialogs = new List<(string Message, string Title, MessageBoxButton Buttons, MessageBoxImage Image)>();
            var controller = CreateController(
                isClipboardProcessing: () => true,
                setDiagnosticsStatus: statuses.Add,
                showDialog: (message, title, buttons, image) =>
                {
                    dialogs.Add((message, title, buttons, image));
                    return MessageBoxResult.OK;
                });

            controller.ClearExpiredCache();

            Assert.Contains("Cache maintenance blocked while a lookup is active.", statuses);
            Assert.Single(dialogs);
            Assert.Equal("PMG Cache Maintenance", dialogs[0].Title);
        }

        [Fact]
        public void ClearAllCache_WhenConfirmationDeclined_SetsCancelledStatus()
        {
            var statuses = new List<string>();
            var controller = CreateController(
                setDiagnosticsStatus: statuses.Add,
                showDialog: (_, _, _, _) => MessageBoxResult.No);

            controller.ClearAllCache();

            Assert.Contains("Cache maintenance cancelled.", statuses);
        }

        private static DiagnosticsCacheMaintenanceController CreateController(
            System.Func<bool>? isClipboardProcessing = null,
            System.Action<string>? setDiagnosticsStatus = null,
            System.Func<string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult>? showDialog = null,
            System.Func<IReadOnlyList<ProviderHealthSnapshot>>? getProviderHealthSnapshots = null,
            System.Action<IReadOnlyList<ProviderHealthSnapshot>>? applyProviderHealthSnapshots = null,
            System.Func<CacheStatsSnapshot>? getCacheStats = null,
            System.Func<int>? clearExpiredCache = null,
            System.Action? vacuumCache = null,
            System.Func<int>? clearAllCache = null,
            System.Action<string>? setCacheStatsText = null)
        {
            return new DiagnosticsCacheMaintenanceController(
                isClipboardProcessing ?? (() => false),
                setDiagnosticsStatus ?? (_ => { }),
                showDialog ?? ((_, _, _, _) => MessageBoxResult.Yes),
                getProviderHealthSnapshots ?? (() => System.Array.Empty<ProviderHealthSnapshot>()),
                applyProviderHealthSnapshots ?? (_ => { }),
                getCacheStats ?? (() => new CacheStatsSnapshot()),
                clearExpiredCache ?? (() => 0),
                vacuumCache ?? (() => { }),
                clearAllCache ?? (() => 0),
                new DiagnosticsCacheStatsPresenter(),
                setCacheStatsText ?? (_ => { }));
        }
    }
}
