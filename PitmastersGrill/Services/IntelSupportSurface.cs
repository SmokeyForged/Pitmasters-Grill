using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using PitmastersGrill.Views;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PitmastersGrill.Services
{
    public sealed class IntelSupportSurface
    {
        private readonly Dispatcher _dispatcher;
        private readonly Border _intelUpdateBanner;
        private readonly TextBlock _intelUpdateStatusText;
        private readonly TextBlock _intelUpdateDetailText;
        private readonly IntelUpdateBannerController _intelUpdateBannerController;
        private readonly IntelStatusDetailsPresenter _intelStatusDetailsPresenter;
        private readonly IntelActionsController _intelActionsController;
        private readonly IntelMaintenanceActionsController _intelMaintenanceActionsController;

        private IntelSupportSurface(
            Dispatcher dispatcher,
            Border intelUpdateBanner,
            TextBlock intelUpdateStatusText,
            TextBlock intelUpdateDetailText,
            IntelUpdateBannerController intelUpdateBannerController,
            IntelStatusDetailsPresenter intelStatusDetailsPresenter,
            IntelActionsController intelActionsController,
            IntelMaintenanceActionsController intelMaintenanceActionsController)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _intelUpdateBanner = intelUpdateBanner ?? throw new ArgumentNullException(nameof(intelUpdateBanner));
            _intelUpdateStatusText = intelUpdateStatusText ?? throw new ArgumentNullException(nameof(intelUpdateStatusText));
            _intelUpdateDetailText = intelUpdateDetailText ?? throw new ArgumentNullException(nameof(intelUpdateDetailText));
            _intelUpdateBannerController = intelUpdateBannerController ?? throw new ArgumentNullException(nameof(intelUpdateBannerController));
            _intelStatusDetailsPresenter = intelStatusDetailsPresenter ?? throw new ArgumentNullException(nameof(intelStatusDetailsPresenter));
            _intelActionsController = intelActionsController ?? throw new ArgumentNullException(nameof(intelActionsController));
            _intelMaintenanceActionsController = intelMaintenanceActionsController ?? throw new ArgumentNullException(nameof(intelMaintenanceActionsController));
        }

        public static IntelSupportSurface Create(
            Window owner,
            Dispatcher dispatcher,
            IntelSupportView view,
            BackgroundIntelUpdateService backgroundIntelUpdateService,
            SettingsTabController settingsTabController,
            Func<AppSettings> getAppSettings,
            Action saveSettings,
            Func<bool> isApplyingSettings,
            Func<bool> isShuttingDown,
            CancellationToken shutdownToken,
            Func<IReadOnlyList<PilotBoardRow>> getCurrentRows,
            Func<string, Task> refreshCurrentBoardRowsFromLocalIntelAsync,
            Func<bool> isClipboardProcessing,
            Action<string> setDiagnosticsStatus,
            Action<bool> setRebuildKillmailDerivedIntelEnabled,
            Func<int> getSeedDays,
            Func<CancellationToken, Task<KillmailDerivedIntelRebuildResult>> rebuildDerivedIntelAsync,
            Func<int, CancellationToken, Task> enableKillmailDbPullAsync,
            Action refreshCacheStatsUi,
            Action refreshConfirmedCynoModuleStateForCurrentRows,
            Func<string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult> showDialog)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            var intelUpdateBannerController = new IntelUpdateBannerController(dispatcher);
            var intelStatusDetailsPresenter = new IntelStatusDetailsPresenter(
                view.IntelLastUpdatedTextBlock,
                view.IntelOldestKillmailDayTextBlock,
                view.IntelNewestKillmailDayTextBlock,
                view.IntelCurrentUpdateStatusTextBlock,
                view.IntelTotalProgressBarControl,
                view.IntelTotalProgressTextBlock,
                view.IntelCurrentDayProgressBarControl,
                view.IntelCurrentDayProgressTextBlock,
                view.IntelLiveFeedSourceTextBlock,
                view.IntelLiveFeedStatusTextBlock,
                view.IntelLiveFeedEnabledTextBlock,
                view.IntelLiveFeedRecentImportsTextBlock,
                view.IntelLiveFeedNextSequenceTextBlock,
                view.IntelLiveFeedLastProcessedSequenceTextBlock,
                view.IntelLiveFeedLastSuccessTextBlock,
                view.IntelLiveFeedLastCaughtUpTextBlock,
                view.IntelLiveFeedLastErrorTextBlock,
                view.TodaysFreshnessStatusTextBlock,
                view.TodaysFreshnessVisiblePilotsTextBlock,
                view.TodaysFreshnessEntitiesQueriedTextBlock,
                view.TodaysFreshnessResultsFoundTextBlock,
                view.TodaysFreshnessKnownSkippedTextBlock,
                view.TodaysFreshnessImportedTextBlock,
                view.TodaysFreshnessFailedTextBlock,
                view.TodaysFreshnessLastRunTextBlock,
                view.TodaysFreshnessDetailTextBlock,
                view.TodaysFreshnessLastErrorTextBlock,
                view.RunTodaysFreshnessButtonControl,
                view.HistoricalFreshnessStatusTextBlock,
                view.HistoricalFreshnessModeTextBlock,
                view.HistoricalFreshnessVisiblePilotsTextBlock,
                view.HistoricalFreshnessCandidatesConsideredTextBlock,
                view.HistoricalFreshnessCandidatesSkippedCooldownTextBlock,
                view.HistoricalFreshnessPilotsCheckedTextBlock,
                view.HistoricalFreshnessDaysCheckedTextBlock,
                view.HistoricalFreshnessEntitiesQueriedTextBlock,
                view.HistoricalFreshnessResultsFoundTextBlock,
                view.HistoricalFreshnessKnownSkippedTextBlock,
                view.HistoricalFreshnessImportedTextBlock,
                view.HistoricalFreshnessFailedTextBlock,
                view.HistoricalFreshnessLastRunTextBlock,
                view.HistoricalFreshnessDetailTextBlock,
                view.HistoricalFreshnessLastErrorTextBlock,
                view.RunHistoricalFreshnessButtonControl);
            var intelActionsController = new IntelActionsController(
                owner,
                backgroundIntelUpdateService,
                settingsTabController,
                getAppSettings,
                saveSettings,
                isApplyingSettings,
                isShuttingDown,
                shutdownToken,
                getCurrentRows,
                refreshCurrentBoardRowsFromLocalIntelAsync);
            var intelMaintenanceActionsController = new IntelMaintenanceActionsController(
                isClipboardProcessing,
                setDiagnosticsStatus,
                setRebuildKillmailDerivedIntelEnabled,
                enabled => view.EnableKillmailDbPullButtonControl.IsEnabled = enabled,
                getSeedDays,
                isShuttingDown,
                shutdownToken,
                rebuildDerivedIntelAsync,
                enableKillmailDbPullAsync,
                refreshCacheStatsUi,
                refreshConfirmedCynoModuleStateForCurrentRows,
                showDialog);

            return new IntelSupportSurface(
                dispatcher,
                view.IntelUpdateBannerControl,
                view.IntelUpdateStatusTextBlock,
                view.IntelUpdateDetailTextBlock,
                intelUpdateBannerController,
                intelStatusDetailsPresenter,
                intelActionsController,
                intelMaintenanceActionsController);
        }

        public void HandleStatusChanged(IntelUpdateStatusSnapshot snapshot, bool isShuttingDown)
        {
            if (_dispatcher.CheckAccess())
            {
                ApplySnapshot(snapshot, isShuttingDown);
                return;
            }

            _dispatcher.BeginInvoke(
                new Action(() => ApplySnapshot(snapshot, isShuttingDown)),
                DispatcherPriority.Background);
        }

        public void ApplySnapshot(IntelUpdateStatusSnapshot snapshot, bool isShuttingDown)
        {
            _intelUpdateBannerController.ApplySnapshot(
                snapshot,
                _intelUpdateBanner,
                _intelUpdateStatusText,
                _intelUpdateDetailText);
            ApplyStatusDetails(snapshot, isShuttingDown);
        }

        public Task RunEnableKillmailDbPullAsync() => _intelMaintenanceActionsController.RunEnableKillmailDbPullAsync();

        public Task HandleLiveFeedToggleAsync(bool enabled) => _intelActionsController.HandleLiveFeedToggleAsync(enabled);

        public Task RunRebuildKillmailDerivedIntelAsync() => _intelMaintenanceActionsController.RunRebuildKillmailDerivedIntelAsync();

        public Task RunTodaysFreshnessAsync() => _intelActionsController.RunTodaysFreshnessAsync();

        public Task RunHistoricalFreshnessAsync() => _intelActionsController.RunHistoricalFreshnessAsync();

        public List<long> GetVisibleCharacterIdsForBackgroundHistoricalRepair()
            => _intelActionsController.GetVisibleCharacterIdsForBackgroundHistoricalRepair();

        private void ApplyStatusDetails(IntelUpdateStatusSnapshot snapshot, bool isShuttingDown)
        {
            if (snapshot == null)
            {
                return;
            }

            var projection = IntelStatusDetailsProjection.Create(snapshot, isShuttingDown);
            _intelStatusDetailsPresenter.Apply(projection);
        }
    }
}
