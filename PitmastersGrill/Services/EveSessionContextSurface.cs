using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PitmastersGrill.Services
{
    public sealed class EveSessionContextSurface
    {
        private readonly Dispatcher _dispatcher;
        private readonly EveSessionContextCoordinator _coordinator;
        private readonly Func<CancellationToken, Task<EveSessionContext>> _captureAsync;
        private readonly Func<bool> _isShuttingDown;
        private readonly CancellationToken _shutdownToken;
        private readonly TextBlock _characterText;
        private readonly TextBlock _systemText;
        private readonly TextBlock _evidenceSourceText;
        private readonly TextBlock _observedAtText;
        private readonly TextBlock _statusText;

        private EveSessionContext? _currentContext;
        private DateTime _lastRefreshUtc = DateTime.MinValue;
        private bool _isRefreshInFlight;

        public EveSessionContextSurface(
            Dispatcher dispatcher,
            EveSessionContextCoordinator coordinator,
            Func<CancellationToken, Task<EveSessionContext>> captureAsync,
            Func<bool> isShuttingDown,
            CancellationToken shutdownToken,
            TextBlock characterText,
            TextBlock systemText,
            TextBlock evidenceSourceText,
            TextBlock observedAtText,
            TextBlock statusText)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _captureAsync = captureAsync ?? throw new ArgumentNullException(nameof(captureAsync));
            _isShuttingDown = isShuttingDown ?? throw new ArgumentNullException(nameof(isShuttingDown));
            _shutdownToken = shutdownToken;
            _characterText = characterText ?? throw new ArgumentNullException(nameof(characterText));
            _systemText = systemText ?? throw new ArgumentNullException(nameof(systemText));
            _evidenceSourceText = evidenceSourceText ?? throw new ArgumentNullException(nameof(evidenceSourceText));
            _observedAtText = observedAtText ?? throw new ArgumentNullException(nameof(observedAtText));
            _statusText = statusText ?? throw new ArgumentNullException(nameof(statusText));
        }

        public void ApplyPendingContext()
        {
            ApplyContext(_coordinator.CreatePendingContext());
        }

        public bool IsStale(DateTime nowUtc)
        {
            return _coordinator.IsStale(_currentContext, _lastRefreshUtc, nowUtc);
        }

        public void TriggerRefresh(string reason, bool force)
        {
            if (!_coordinator.ShouldTriggerRefresh(
                _isShuttingDown(),
                force,
                _currentContext,
                _lastRefreshUtc,
                _isRefreshInFlight,
                DateTime.UtcNow))
            {
                return;
            }

            _ = RefreshAsync(reason);
        }

        private async Task RefreshAsync(string reason)
        {
            if (_isRefreshInFlight || _isShuttingDown())
            {
                return;
            }

            _isRefreshInFlight = true;
            try
            {
                AppLogger.UiDebug($"EVE session context refresh started. reason='{reason}'");
                var context = await _captureAsync(_shutdownToken);
                _lastRefreshUtc = DateTime.UtcNow;
                _currentContext = context;
                await _dispatcher.InvokeAsync(() => ApplyProjection(_coordinator.BuildProjection(context)));
            }
            catch (OperationCanceledException)
            {
                // Shutdown or refresh cancellation is expected.
            }
            catch (Exception ex)
            {
                AppLogger.UiWarn($"EVE session context refresh failed. reason='{reason}' message={ex.Message}");
                var fallback = _coordinator.CreateFallbackContext();
                _lastRefreshUtc = DateTime.UtcNow;
                _currentContext = fallback;
                await _dispatcher.InvokeAsync(() => ApplyProjection(_coordinator.BuildProjection(fallback)));
            }
            finally
            {
                _isRefreshInFlight = false;
            }
        }

        private void ApplyContext(EveSessionContext context)
        {
            _currentContext = context;
            ApplyProjection(_coordinator.BuildProjection(context));
        }

        private void ApplyProjection(EveSessionContextProjection projection)
        {
            _characterText.Text = projection.CharacterText;
            _systemText.Text = projection.SystemText;
            _evidenceSourceText.Text = projection.EvidenceSourceText;
            _observedAtText.Text = projection.ObservedAtText;
            _statusText.Text = projection.StatusText;
        }
    }
}
