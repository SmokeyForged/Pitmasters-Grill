using PitmastersGrill.Diagnostics;
using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PitmastersGrill.Services
{
    public sealed class IntelMaintenanceActionsController
    {
        private readonly Func<bool> _isClipboardProcessing;
        private readonly Action<string> _setDiagnosticsStatus;
        private readonly Action<bool> _setRebuildButtonEnabled;
        private readonly Action<bool> _setEnableKillmailDbPullButtonEnabled;
        private readonly Func<int> _getSeedDays;
        private readonly Func<bool> _isShuttingDown;
        private readonly CancellationToken _shutdownToken;
        private readonly Func<CancellationToken, Task<KillmailDerivedIntelRebuildResult>> _rebuildDerivedIntelAsync;
        private readonly Func<int, CancellationToken, Task> _enableKillmailDbPullAsync;
        private readonly Action _refreshCacheStatsUi;
        private readonly Action _refreshConfirmedCynoModuleStateForCurrentRows;
        private readonly Func<string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult> _showDialog;

        public IntelMaintenanceActionsController(
            Func<bool> isClipboardProcessing,
            Action<string> setDiagnosticsStatus,
            Action<bool> setRebuildButtonEnabled,
            Action<bool> setEnableKillmailDbPullButtonEnabled,
            Func<int> getSeedDays,
            Func<bool> isShuttingDown,
            CancellationToken shutdownToken,
            Func<CancellationToken, Task<KillmailDerivedIntelRebuildResult>> rebuildDerivedIntelAsync,
            Func<int, CancellationToken, Task> enableKillmailDbPullAsync,
            Action refreshCacheStatsUi,
            Action refreshConfirmedCynoModuleStateForCurrentRows,
            Func<string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult> showDialog)
        {
            _isClipboardProcessing = isClipboardProcessing ?? throw new ArgumentNullException(nameof(isClipboardProcessing));
            _setDiagnosticsStatus = setDiagnosticsStatus ?? throw new ArgumentNullException(nameof(setDiagnosticsStatus));
            _setRebuildButtonEnabled = setRebuildButtonEnabled ?? throw new ArgumentNullException(nameof(setRebuildButtonEnabled));
            _setEnableKillmailDbPullButtonEnabled = setEnableKillmailDbPullButtonEnabled ?? throw new ArgumentNullException(nameof(setEnableKillmailDbPullButtonEnabled));
            _getSeedDays = getSeedDays ?? throw new ArgumentNullException(nameof(getSeedDays));
            _isShuttingDown = isShuttingDown ?? throw new ArgumentNullException(nameof(isShuttingDown));
            _shutdownToken = shutdownToken;
            _rebuildDerivedIntelAsync = rebuildDerivedIntelAsync ?? throw new ArgumentNullException(nameof(rebuildDerivedIntelAsync));
            _enableKillmailDbPullAsync = enableKillmailDbPullAsync ?? throw new ArgumentNullException(nameof(enableKillmailDbPullAsync));
            _refreshCacheStatsUi = refreshCacheStatsUi ?? throw new ArgumentNullException(nameof(refreshCacheStatsUi));
            _refreshConfirmedCynoModuleStateForCurrentRows = refreshConfirmedCynoModuleStateForCurrentRows ?? throw new ArgumentNullException(nameof(refreshConfirmedCynoModuleStateForCurrentRows));
            _showDialog = showDialog ?? throw new ArgumentNullException(nameof(showDialog));
        }

        public async Task RunRebuildKillmailDerivedIntelAsync()
        {
            if (_isClipboardProcessing())
            {
                _setDiagnosticsStatus("Derived intel rebuild blocked while a lookup is active.");
                _showDialog(
                    "A board lookup is currently running. Let it finish before rebuilding derived killmail intel.",
                    "PMG Killmail Derived Intel",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var confirm = _showDialog(
                "Rebuild killmail derived intel from local extracted killmail archives?\n\nThis only rebuilds derived confirmed cyno-module and industrial-cyno bait observations. It does not clear notes, settings, themes, ignore lists, manual overrides, or unrelated cache data.",
                "PMG Killmail Derived Intel",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (confirm != MessageBoxResult.Yes)
            {
                _setDiagnosticsStatus("Derived intel rebuild cancelled.");
                return;
            }

            try
            {
                _setRebuildButtonEnabled(false);
                _setDiagnosticsStatus("Rebuilding killmail derived intel...");
                var result = await _rebuildDerivedIntelAsync(_shutdownToken);
                _refreshCacheStatsUi();
                _refreshConfirmedCynoModuleStateForCurrentRows();

                _setDiagnosticsStatus(result.Message);
                _showDialog(
                    result.Message,
                    result.NoLocalSourceAvailable ? "PMG Killmail Derived Intel Source Missing" : "PMG Killmail Derived Intel",
                    MessageBoxButton.OK,
                    result.NoLocalSourceAvailable ? MessageBoxImage.Information : MessageBoxImage.None);
            }
            catch (OperationCanceledException)
            {
                _setDiagnosticsStatus("Derived intel rebuild cancelled.");
            }
            catch (Exception ex)
            {
                AppLogger.DatabaseError("Killmail derived intel rebuild failed.", ex);
                _setDiagnosticsStatus("Derived intel rebuild failed.");
                _showDialog(
                    $"Failed to rebuild killmail derived intel.\n\n{ex.Message}",
                    "PMG Killmail Derived Intel Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _setRebuildButtonEnabled(true);
            }
        }

        public async Task RunEnableKillmailDbPullAsync()
        {
            try
            {
                _setEnableKillmailDbPullButtonEnabled(false);

                var seedDays = _getSeedDays();
                AppLogger.UiInfo(
                    $"Enable KillMail DB Pull requested. seedDays={seedDays} displayKillmailPath={KillmailPaths.GetKillmailDataDirectoryDisplayPath()} source={KillmailPaths.GetKillmailDataDirectorySourceDescription()}");

                await _enableKillmailDbPullAsync(seedDays, _shutdownToken);

                AppLogger.UiInfo($"Enable KillMail DB Pull completed successfully. seedDays={seedDays}");
            }
            catch (OperationCanceledException) when (_isShuttingDown() || _shutdownToken.IsCancellationRequested)
            {
                AppLogger.UiInfo("Enable KillMail DB Pull cancelled during shutdown.");
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Enable KillMail DB Pull failed.", ex);
                _showDialog(
                    $"Failed to enable killmail DB pull.\n\n{ex.Message}",
                    "PMG Killmail DB Pull Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                if (!_isShuttingDown())
                {
                    _setEnableKillmailDbPullButtonEnabled(true);
                }
            }
        }
    }
}
