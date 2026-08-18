using System;

namespace PitmastersGrill.Services
{
    public sealed class ExternalNavigationCoordinator
    {
        private readonly ZkillUrlBuilder _zkillUrlBuilder;
        private readonly Func<string, BrowserLaunchResult> _tryOpenUrl;
        private readonly Action<string> _logInfo;
        private readonly Action<string> _logWarn;
        private readonly Action<string, Exception> _logError;

        public ExternalNavigationCoordinator(
            ZkillUrlBuilder zkillUrlBuilder,
            Func<string, BrowserLaunchResult> tryOpenUrl,
            Action<string> logInfo,
            Action<string> logWarn,
            Action<string, Exception> logError)
        {
            _zkillUrlBuilder = zkillUrlBuilder ?? throw new ArgumentNullException(nameof(zkillUrlBuilder));
            _tryOpenUrl = tryOpenUrl ?? throw new ArgumentNullException(nameof(tryOpenUrl));
            _logInfo = logInfo ?? throw new ArgumentNullException(nameof(logInfo));
            _logWarn = logWarn ?? throw new ArgumentNullException(nameof(logWarn));
            _logError = logError ?? throw new ArgumentNullException(nameof(logError));
        }

        public BrowserLaunchResult OpenPilotZkill(string? characterId, string? characterName)
        {
            var url = string.IsNullOrWhiteSpace(characterId)
                ? _zkillUrlBuilder.BuildSearchUrl(characterName ?? string.Empty)
                : _zkillUrlBuilder.BuildCharacterUrl(characterId);

            return OpenUrl(
                url,
                $"pilot zKill character='{characterName ?? string.Empty}' characterId='{characterId ?? string.Empty}'");
        }

        public BrowserLaunchResult OpenAffiliationZkill(string? entityType, string? entityId)
        {
            if (string.IsNullOrWhiteSpace(entityId) || !long.TryParse(entityId, out _))
            {
                _logWarn($"External navigation ignored invalid affiliation id. type='{entityType ?? string.Empty}' id='{entityId ?? string.Empty}'");
                return new BrowserLaunchResult(false, false, string.Empty);
            }

            var route = string.Equals(entityType, "alliance", StringComparison.OrdinalIgnoreCase)
                ? "alliance"
                : string.Equals(entityType, "corporation", StringComparison.OrdinalIgnoreCase)
                    ? "corporation"
                    : string.Empty;
            if (string.IsNullOrWhiteSpace(route))
            {
                _logWarn($"External navigation ignored unsupported affiliation type. type='{entityType ?? string.Empty}' id='{entityId}'");
                return new BrowserLaunchResult(false, false, string.Empty);
            }

            var trimmedId = entityId.Trim();
            var url = $"https://zkillboard.com/{route}/{Uri.EscapeDataString(trimmedId)}/";
            return OpenUrl(url, $"{route} zKill id='{trimmedId}'");
        }

        public BrowserLaunchResult OpenUrl(string? url, string context)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                _logWarn($"External navigation ignored blank URL. context='{context}'");
                return new BrowserLaunchResult(false, false, string.Empty);
            }

            BrowserLaunchResult result;
            try
            {
                result = _tryOpenUrl(url);
            }
            catch (Exception ex)
            {
                // The BrowserLauncher contract is non-throwing, but keep the coordinator safe
                // if an injected launcher violates that contract.
                _logError($"External navigation launcher threw unexpectedly. context='{context}' url='{url}'", ex);
                return new BrowserLaunchResult(true, false, url, ex);
            }

            if (result.Succeeded)
            {
                _logInfo($"External navigation opened URL. context='{context}' url='{url}'");
            }
            else if (result.Exception != null)
            {
                _logError($"External navigation failed. context='{context}' url='{url}'", result.Exception);
            }
            else
            {
                _logWarn($"External navigation launcher reported failure. context='{context}' url='{url}'");
            }

            return result with { Url = string.IsNullOrWhiteSpace(result.Url) ? url : result.Url };
        }
    }
}
