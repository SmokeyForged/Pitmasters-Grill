using System;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersGrill.Services
{
    public sealed class PmgUpdateAwarenessResult
    {
        private PmgUpdateAwarenessResult(
            bool isUpdateAvailable,
            string currentVersion,
            string latestVersion,
            string releasePageUrl,
            string statusText)
        {
            IsUpdateAvailable = isUpdateAvailable;
            CurrentVersion = currentVersion;
            LatestVersion = latestVersion;
            ReleasePageUrl = releasePageUrl;
            StatusText = statusText;
        }

        public bool IsUpdateAvailable { get; }

        public string CurrentVersion { get; }

        public string LatestVersion { get; }

        public string ReleasePageUrl { get; }

        public string StatusText { get; }

        public static PmgUpdateAwarenessResult Current(string currentVersion)
        {
            return new PmgUpdateAwarenessResult(false, currentVersion, string.Empty, string.Empty, "PMG is current.");
        }

        public static PmgUpdateAwarenessResult Skipped(string currentVersion, string latestVersion)
        {
            return new PmgUpdateAwarenessResult(false, currentVersion, latestVersion, string.Empty, $"PMG {latestVersion} was skipped.");
        }

        public static PmgUpdateAwarenessResult Available(string currentVersion, string latestVersion, string releasePageUrl)
        {
            return new PmgUpdateAwarenessResult(true, currentVersion, latestVersion, releasePageUrl, $"PMG {latestVersion} is available.");
        }
    }

    public sealed class PmgUpdateAwarenessService
    {
        private readonly ILatestReleaseChecker _latestReleaseChecker;
        private readonly string _currentVersion;

        public PmgUpdateAwarenessService(ILatestReleaseChecker latestReleaseChecker, string currentVersion)
        {
            _latestReleaseChecker = latestReleaseChecker ?? throw new ArgumentNullException(nameof(latestReleaseChecker));
            _currentVersion = ReleaseVersionComparer.NormalizeStableVersionText(currentVersion);

            if (string.IsNullOrWhiteSpace(_currentVersion))
            {
                _currentVersion = "0.0.0";
            }
        }

        public Task<PmgUpdateAwarenessResult> CheckAsync(string? skippedUpdateVersion, CancellationToken cancellationToken)
        {
            return CheckAsync(skippedUpdateVersion, respectSkippedVersion: true, cancellationToken);
        }

        public async Task<PmgUpdateAwarenessResult> CheckAsync(
            string? skippedUpdateVersion,
            bool respectSkippedVersion,
            CancellationToken cancellationToken)
        {
            var latestRelease = await _latestReleaseChecker.GetLatestStableReleaseAsync(cancellationToken).ConfigureAwait(false);
            if (latestRelease == null)
            {
                return PmgUpdateAwarenessResult.Current(_currentVersion);
            }

            var latestVersion = ReleaseVersionComparer.NormalizeStableVersionText(latestRelease.Version);
            if (string.IsNullOrWhiteSpace(latestVersion))
            {
                return PmgUpdateAwarenessResult.Current(_currentVersion);
            }

            if (!ReleaseVersionComparer.IsNewerStableVersion(_currentVersion, latestVersion))
            {
                return PmgUpdateAwarenessResult.Current(_currentVersion);
            }

            if (respectSkippedVersion
                && ReleaseVersionComparer.IsSameStableVersion(skippedUpdateVersion, latestVersion))
            {
                return PmgUpdateAwarenessResult.Skipped(_currentVersion, latestVersion);
            }

            return PmgUpdateAwarenessResult.Available(
                _currentVersion,
                latestVersion,
                latestRelease.ReleasePageUrl);
        }
    }
}
