using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace PitmastersGrill.Services
{
    public sealed class DiagnosticsCacheMaintenanceController
    {
        private readonly Func<bool> _isClipboardProcessing;
        private readonly Action<string> _setDiagnosticsStatus;
        private readonly Func<string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult> _showDialog;
        private readonly Func<IReadOnlyList<ProviderHealthSnapshot>> _getProviderHealthSnapshots;
        private readonly Action<IReadOnlyList<ProviderHealthSnapshot>> _applyProviderHealthSnapshots;
        private readonly Func<CacheStatsSnapshot> _getCacheStats;
        private readonly Func<int> _clearExpiredCache;
        private readonly Action _vacuumCache;
        private readonly Func<int> _clearAllCache;
        private readonly DiagnosticsCacheStatsPresenter _cacheStatsPresenter;
        private readonly Action<string> _setCacheStatsText;

        public DiagnosticsCacheMaintenanceController(
            Func<bool> isClipboardProcessing,
            Action<string> setDiagnosticsStatus,
            Func<string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult> showDialog,
            Func<IReadOnlyList<ProviderHealthSnapshot>> getProviderHealthSnapshots,
            Action<IReadOnlyList<ProviderHealthSnapshot>> applyProviderHealthSnapshots,
            Func<CacheStatsSnapshot> getCacheStats,
            Func<int> clearExpiredCache,
            Action vacuumCache,
            Func<int> clearAllCache,
            DiagnosticsCacheStatsPresenter cacheStatsPresenter,
            Action<string> setCacheStatsText)
        {
            _isClipboardProcessing = isClipboardProcessing ?? throw new ArgumentNullException(nameof(isClipboardProcessing));
            _setDiagnosticsStatus = setDiagnosticsStatus ?? throw new ArgumentNullException(nameof(setDiagnosticsStatus));
            _showDialog = showDialog ?? throw new ArgumentNullException(nameof(showDialog));
            _getProviderHealthSnapshots = getProviderHealthSnapshots ?? throw new ArgumentNullException(nameof(getProviderHealthSnapshots));
            _applyProviderHealthSnapshots = applyProviderHealthSnapshots ?? throw new ArgumentNullException(nameof(applyProviderHealthSnapshots));
            _getCacheStats = getCacheStats ?? throw new ArgumentNullException(nameof(getCacheStats));
            _clearExpiredCache = clearExpiredCache ?? throw new ArgumentNullException(nameof(clearExpiredCache));
            _vacuumCache = vacuumCache ?? throw new ArgumentNullException(nameof(vacuumCache));
            _clearAllCache = clearAllCache ?? throw new ArgumentNullException(nameof(clearAllCache));
            _cacheStatsPresenter = cacheStatsPresenter ?? throw new ArgumentNullException(nameof(cacheStatsPresenter));
            _setCacheStatsText = setCacheStatsText ?? throw new ArgumentNullException(nameof(setCacheStatsText));
        }

        public void RefreshProviderHealth()
        {
            var snapshots = _getProviderHealthSnapshots();
            _applyProviderHealthSnapshots(snapshots);
            _setDiagnosticsStatus("Provider health refreshed.");
        }

        public void RefreshProviderHealthUi()
        {
            var snapshots = _getProviderHealthSnapshots();
            _applyProviderHealthSnapshots(snapshots);
        }

        public void RefreshCacheStats()
        {
            RefreshCacheStatsUi();
            _setDiagnosticsStatus("Cache stats refreshed.");
        }

        public void RefreshCacheStatsUi()
        {
            try
            {
                var text = _cacheStatsPresenter.BuildStatsText(_getCacheStats());
                _setCacheStatsText(text);
            }
            catch (Exception ex)
            {
                AppLogger.DatabaseError("Cache stats refresh failed.", ex);
                _setCacheStatsText(_cacheStatsPresenter.BuildFailureText(ex));
            }
        }

        public void ClearExpiredCache()
        {
            RunCacheMaintenanceAction(
                "Clear expired cache",
                requiresConfirmation: true,
                action: () =>
                {
                    var removed = _clearExpiredCache();
                    _setDiagnosticsStatus($"Expired cache cleanup removed {removed:N0} rows.");
                    AppLogger.DatabaseInfo($"Cache maintenance UI cleared expired rows. removedRows={removed}");
                });
        }

        public void VacuumCache()
        {
            RunCacheMaintenanceAction(
                "Compact cache database",
                requiresConfirmation: true,
                action: () =>
                {
                    _vacuumCache();
                    _setDiagnosticsStatus("Cache database compacted.");
                    AppLogger.DatabaseInfo("Cache maintenance UI compacted SQLite database.");
                });
        }

        public void ClearAllCache()
        {
            RunCacheMaintenanceAction(
                "Clear all resolver/stat cache rows",
                requiresConfirmation: true,
                action: () =>
                {
                    var removed = _clearAllCache();
                    _setDiagnosticsStatus($"All resolver/stat cache cleanup removed {removed:N0} rows.");
                    AppLogger.DatabaseWarn($"Cache maintenance UI cleared all resolver/stat cache rows. removedRows={removed}");
                });
        }

        private void RunCacheMaintenanceAction(string title, bool requiresConfirmation, Action action)
        {
            if (_isClipboardProcessing())
            {
                _setDiagnosticsStatus("Cache maintenance blocked while a lookup is active.");
                _showDialog(
                    "A board lookup is currently running. Let it finish before changing the local cache.",
                    "PMG Cache Maintenance",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (requiresConfirmation)
            {
                var result = _showDialog(
                    $"{title}?\n\nThis only affects PMG local cache tables and does not delete unrelated files.",
                    "PMG Cache Maintenance",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    _setDiagnosticsStatus("Cache maintenance cancelled.");
                    return;
                }
            }

            try
            {
                action();
                RefreshCacheStatsUi();
            }
            catch (Exception ex)
            {
                AppLogger.DatabaseError($"Cache maintenance failed. action='{title}'", ex);
                _setDiagnosticsStatus("Cache maintenance failed.");
                _showDialog(
                    $"Cache maintenance failed.\n\n{ex.Message}",
                    "PMG Cache Maintenance Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
