using PitmastersGrill.Diagnostics;
using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PitmastersGrill.Services
{
    public sealed class IntelActionsController
    {
        private readonly Window _owner;
        private readonly BackgroundIntelUpdateService _backgroundIntelUpdateService;
        private readonly SettingsTabController _settingsTabController;
        private readonly Func<AppSettings> _getAppSettings;
        private readonly Action _saveSettings;
        private readonly Func<bool> _isApplyingSettings;
        private readonly Func<bool> _isShuttingDown;
        private readonly CancellationToken _shutdownToken;
        private readonly Func<IReadOnlyList<PilotBoardRow>> _getCurrentRows;
        private readonly Func<string, Task> _refreshCurrentBoardRowsFromLocalIntelAsync;

        public IntelActionsController(
            Window owner,
            BackgroundIntelUpdateService backgroundIntelUpdateService,
            SettingsTabController settingsTabController,
            Func<AppSettings> getAppSettings,
            Action saveSettings,
            Func<bool> isApplyingSettings,
            Func<bool> isShuttingDown,
            CancellationToken shutdownToken,
            Func<IReadOnlyList<PilotBoardRow>> getCurrentRows,
            Func<string, Task> refreshCurrentBoardRowsFromLocalIntelAsync)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _backgroundIntelUpdateService = backgroundIntelUpdateService ?? throw new ArgumentNullException(nameof(backgroundIntelUpdateService));
            _settingsTabController = settingsTabController ?? throw new ArgumentNullException(nameof(settingsTabController));
            _getAppSettings = getAppSettings ?? throw new ArgumentNullException(nameof(getAppSettings));
            _saveSettings = saveSettings ?? throw new ArgumentNullException(nameof(saveSettings));
            _isApplyingSettings = isApplyingSettings ?? throw new ArgumentNullException(nameof(isApplyingSettings));
            _isShuttingDown = isShuttingDown ?? throw new ArgumentNullException(nameof(isShuttingDown));
            _shutdownToken = shutdownToken;
            _getCurrentRows = getCurrentRows ?? throw new ArgumentNullException(nameof(getCurrentRows));
            _refreshCurrentBoardRowsFromLocalIntelAsync = refreshCurrentBoardRowsFromLocalIntelAsync ?? throw new ArgumentNullException(nameof(refreshCurrentBoardRowsFromLocalIntelAsync));
        }

        public async Task HandleLiveFeedToggleAsync(bool enabled)
        {
            if (_isApplyingSettings())
            {
                return;
            }

            _settingsTabController.SetLiveZkillFeedEnabled(_getAppSettings(), enabled);
            _saveSettings();
            AppLogger.UiInfo($"Live zKill feed setting changed. enabled={enabled}");

            try
            {
                await _backgroundIntelUpdateService.SetLiveFeedEnabledAsync(enabled, _shutdownToken);
            }
            catch (OperationCanceledException) when (_isShuttingDown() || _shutdownToken.IsCancellationRequested)
            {
                AppLogger.UiInfo("Live zKill feed toggle cancelled during shutdown.");
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Live zKill feed toggle failed.", ex);

                MessageBox.Show(
                    _owner,
                    $"Failed to update the live zKill feed setting.\n\n{ex.Message}",
                    "PMG Live Feed Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public async Task RunTodaysFreshnessAsync()
        {
            var visibleCharacterIds = CollectVisibleCharacterIds(_getCurrentRows());
            if (visibleCharacterIds.Count == 0)
            {
                MessageBox.Show(
                    _owner,
                    "Today's Freshness needs at least one visible Grill pilot with a resolved character ID.",
                    "PMG Today's Freshness",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                using var foregroundPriority = _backgroundIntelUpdateService.BeginForegroundPriority();
                var result = await _backgroundIntelUpdateService.RunTodaysFreshnessAsync(visibleCharacterIds, _shutdownToken);

                if (!result.Success &&
                    string.Equals(result.LastError, "Another freshness operation is already running.", StringComparison.Ordinal))
                {
                    MessageBox.Show(
                        _owner,
                        "Another freshness operation is already running.",
                        "PMG Today's Freshness",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                if (!_isShuttingDown() && result.NewKillmailsImported > 0)
                {
                    await _refreshCurrentBoardRowsFromLocalIntelAsync("Today's Freshness");
                }
            }
            catch (OperationCanceledException) when (_isShuttingDown() || _shutdownToken.IsCancellationRequested)
            {
                AppLogger.UiInfo("Today's Freshness cancelled during shutdown.");
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Today's Freshness failed from the Intel UI.", ex);

                MessageBox.Show(
                    _owner,
                    $"Today's Freshness failed.\n\n{ex.Message}",
                    "PMG Today's Freshness",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public async Task RunHistoricalFreshnessAsync()
        {
            var visibleCharacterIds = CollectVisibleCharacterIds(_getCurrentRows());
            if (visibleCharacterIds.Count == 0)
            {
                MessageBox.Show(
                    _owner,
                    "Historical Freshness needs at least one visible Grill pilot with a resolved character ID.",
                    "PMG Historical Freshness",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                using var foregroundPriority = _backgroundIntelUpdateService.BeginForegroundPriority();
                var result = await _backgroundIntelUpdateService.RunHistoricalFreshnessAsync(visibleCharacterIds, _shutdownToken);

                if (!result.Success &&
                    string.Equals(result.LastError, "Another freshness operation is already running.", StringComparison.Ordinal))
                {
                    MessageBox.Show(
                        _owner,
                        "Another freshness operation is already running.",
                        "PMG Historical Freshness",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                if (!result.Success &&
                    string.Equals(result.LastError, "Historical Freshness already running.", StringComparison.Ordinal))
                {
                    MessageBox.Show(
                        _owner,
                        "Historical Freshness is already running.",
                        "PMG Historical Freshness",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                if (!_isShuttingDown() && result.MissingImportedCount > 0)
                {
                    await _refreshCurrentBoardRowsFromLocalIntelAsync("Historical Freshness");
                }
            }
            catch (OperationCanceledException) when (_isShuttingDown() || _shutdownToken.IsCancellationRequested)
            {
                AppLogger.UiInfo("Historical Freshness cancelled during shutdown.");
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Historical Freshness failed from the Intel UI.", ex);

                MessageBox.Show(
                    _owner,
                    $"Historical Freshness failed.\n\n{ex.Message}",
                    "PMG Historical Freshness",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public List<long> GetVisibleCharacterIdsForBackgroundHistoricalRepair()
        {
            return CollectVisibleCharacterIds(_getCurrentRows());
        }

        public static List<long> CollectVisibleCharacterIds(IEnumerable<PilotBoardRow> rows)
        {
            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            return rows
                .Select(row => row?.CharacterId)
                .Where(characterId => long.TryParse(characterId, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                .Select(characterId => long.Parse(characterId!, CultureInfo.InvariantCulture))
                .Distinct()
                .ToList();
        }
    }
}
