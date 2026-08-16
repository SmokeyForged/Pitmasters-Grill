using System;

namespace PitmastersGrill.Services
{
    public sealed record ExternalNavigationResult(
        bool Attempted,
        bool Succeeded,
        string Url,
        Exception? Exception = null);

    public sealed class ExternalNavigationCoordinator
    {
        private readonly ZkillUrlBuilder _zkillUrlBuilder;
        private readonly Func<string, bool> _tryOpenUrl;
        private readonly Action<string> _logInfo;
        private readonly Action<string> _logWarn;
        private readonly Action<string, Exception> _logError;

        public ExternalNavigationCoordinator(
            ZkillUrlBuilder zkillUrlBuilder,
            Func<string, bool> tryOpenUrl,
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

        public ExternalNavigationResult OpenPilotZkill(string? characterId, string? characterName)
        {
            var url = string.IsNullOrWhiteSpace(characterId)
                ? _zkillUrlBuilder.BuildSearchUrl(characterName ?? string.Empty)
                : _zkillUrlBuilder.BuildCharacterUrl(characterId);

            return OpenUrl(
                url,
                $"pilot zKill character='{characterName ?? string.Empty}' characterId='{characterId ?? string.Empty}'");
        }

        public ExternalNavigationResult OpenAffiliationZkill(string? entityType, string? entityId)
        {
            if (string.IsNullOrWhiteSpace(entityId) || !long.TryParse(entityId, out _))
            {
                _logWarn($"External navigation ignored invalid affiliation id. type='{entityType ?? string.Empty}' id='{entityId ?? string.Empty}'");
                return new ExternalNavigationResult(false, false, string.Empty);
            }

            var route = string.Equals(entityType, "alliance", StringComparison.OrdinalIgnoreCase)
                ? "alliance"
                : string.Equals(entityType, "corporation", StringComparison.OrdinalIgnoreCase)
                    ? "corporation"
                    : string.Empty;
            if (string.IsNullOrWhiteSpace(route))
            {
                _logWarn($"External navigation ignored unsupported affiliation type. type='{entityType ?? string.Empty}' id='{entityId}'");
                return new ExternalNavigationResult(false, false, string.Empty);
            }

            var trimmedId = entityId.Trim();
            var url = $"https://zkillboard.com/{route}/{Uri.EscapeDataString(trimmedId)}/";
            return OpenUrl(url, $"{route} zKill id='{trimmedId}'");
        }

        public ExternalNavigationResult OpenUrl(string? url, string context)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                _logWarn($"External navigation ignored blank URL. context='{context}'");
                return new ExternalNavigationResult(false, false, string.Empty);
            }

            try
            {
                var succeeded = _tryOpenUrl(url);
                if (succeeded)
                {
                    _logInfo($"External navigation opened URL. context='{context}' url='{url}'");
                }
                else
                {
                    _logWarn($"External navigation launcher reported failure. context='{context}' url='{url}'");
                }

                return new ExternalNavigationResult(true, succeeded, url);
            }
            catch (Exception ex)
            {
                _logError($"External navigation failed. context='{context}' url='{url}'", ex);
                return new ExternalNavigationResult(true, false, url, ex);
            }
        }
    }
}
