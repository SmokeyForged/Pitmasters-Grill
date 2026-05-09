using PitmastersGrill.Models;
using PitmastersGrill.Views;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Windows;

namespace PitmastersGrill.Services
{
    public sealed class DiagnosticsSupportSurface
    {
        private readonly DiagnosticsActionController _diagnosticsActionController;
        private readonly DiagnosticsCacheMaintenanceController _diagnosticsCacheMaintenanceController;

        private DiagnosticsSupportSurface(
            DiagnosticsActionController diagnosticsActionController,
            DiagnosticsCacheMaintenanceController diagnosticsCacheMaintenanceController)
        {
            _diagnosticsActionController = diagnosticsActionController ?? throw new ArgumentNullException(nameof(diagnosticsActionController));
            _diagnosticsCacheMaintenanceController = diagnosticsCacheMaintenanceController ?? throw new ArgumentNullException(nameof(diagnosticsCacheMaintenanceController));
        }

        public static DiagnosticsSupportSurface Create(
            Window owner,
            DiagnosticsSupportView view,
            BrowserLauncher browserLauncher,
            ObservableCollection<ProviderHealthSnapshot> providerHealthRows,
            Func<bool> isClipboardProcessing,
            Func<string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult> showDialog,
            Func<IReadOnlyList<ProviderHealthSnapshot>> getProviderHealthSnapshots,
            Func<CacheStatsSnapshot> getCacheStats,
            Func<int> clearExpiredCache,
            Action vacuumCache,
            Func<int> clearAllCache)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (providerHealthRows == null)
            {
                throw new ArgumentNullException(nameof(providerHealthRows));
            }

            var diagnosticsActionController = new DiagnosticsActionController(
                owner,
                view.DiagnosticsStatusTextBlock,
                browserLauncher);
            var providerHealthPresenter = new ProviderHealthPresenter();
            var diagnosticsCacheStatsPresenter = new DiagnosticsCacheStatsPresenter();
            var diagnosticsCacheMaintenanceController = new DiagnosticsCacheMaintenanceController(
                isClipboardProcessing,
                diagnosticsActionController.SetStatus,
                showDialog,
                getProviderHealthSnapshots,
                snapshots => providerHealthPresenter.ApplySnapshots(providerHealthRows, snapshots),
                getCacheStats,
                clearExpiredCache,
                vacuumCache,
                clearAllCache,
                diagnosticsCacheStatsPresenter,
                view.SetCacheStatsText);

            return new DiagnosticsSupportSurface(
                diagnosticsActionController,
                diagnosticsCacheMaintenanceController);
        }

        public void OpenLogs() => _diagnosticsActionController.OpenLogs();

        public void PackageDiagnostics() => _diagnosticsActionController.PackageDiagnostics();

        public void OpenDiagnosticsFolder() => _diagnosticsActionController.OpenDiagnosticsFolder();

        public void SetStatus(string message) => _diagnosticsActionController.SetStatus(message);

        public void RefreshProviderHealth() => _diagnosticsCacheMaintenanceController.RefreshProviderHealth();

        public void RefreshProviderHealthUi() => _diagnosticsCacheMaintenanceController.RefreshProviderHealthUi();

        public void RefreshCacheStats() => _diagnosticsCacheMaintenanceController.RefreshCacheStats();

        public void RefreshCacheStatsUi() => _diagnosticsCacheMaintenanceController.RefreshCacheStatsUi();

        public void ClearExpiredCache() => _diagnosticsCacheMaintenanceController.ClearExpiredCache();

        public void VacuumCache() => _diagnosticsCacheMaintenanceController.VacuumCache();

        public void ClearAllCache() => _diagnosticsCacheMaintenanceController.ClearAllCache();
    }
}
