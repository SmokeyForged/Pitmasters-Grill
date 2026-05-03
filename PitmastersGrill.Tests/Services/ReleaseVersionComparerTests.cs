using PitmastersGrill.Services;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class ReleaseVersionComparerTests
    {
        [Theory]
        [InlineData("1.2.0", "1.3.0")]
        [InlineData("v1.2.0", "v1.3.0")]
        [InlineData("General Release-v1.2.0", "v1.3.0")]
        public void IsNewerStableVersion_ReturnsTrue_WhenLatestIsNewer(string currentVersion, string latestVersion)
        {
            Assert.True(ReleaseVersionComparer.IsNewerStableVersion(currentVersion, latestVersion));
        }

        [Theory]
        [InlineData("1.3.0", "1.3.0")]
        [InlineData("1.3.1", "1.3.0")]
        [InlineData("1.2.0", "v1.3.0-beta.1")]
        public void IsNewerStableVersion_ReturnsFalse_WhenLatestIsNotNewerStable(string currentVersion, string latestVersion)
        {
            Assert.False(ReleaseVersionComparer.IsNewerStableVersion(currentVersion, latestVersion));
        }

        [Theory]
        [InlineData("v1.3.0", "1.3.0")]
        [InlineData("General Release-v1.3.0", "v1.3.0")]
        public void IsSameStableVersion_NormalizesKnownPrefixes(string leftVersion, string rightVersion)
        {
            Assert.True(ReleaseVersionComparer.IsSameStableVersion(leftVersion, rightVersion));
        }

        [Fact]
        public async Task CheckAsync_ReturnsAvailable_WhenLatestStableIsNewer()
        {
            var service = new PmgUpdateAwarenessService(
                new StubLatestReleaseChecker(new GitHubLatestRelease("v1.3.0", "https://example.test/release")),
                "1.2.0");

            var result = await service.CheckAsync(string.Empty, CancellationToken.None);

            Assert.True(result.IsUpdateAvailable);
            Assert.Equal("1.2.0", result.CurrentVersion);
            Assert.Equal("1.3.0", result.LatestVersion);
            Assert.Equal("https://example.test/release", result.ReleasePageUrl);
        }

        [Fact]
        public async Task CheckAsync_RespectsSkippedVersion_ForStartupChecks()
        {
            var service = new PmgUpdateAwarenessService(
                new StubLatestReleaseChecker(new GitHubLatestRelease("v1.3.0", "https://example.test/release")),
                "1.2.0");

            var result = await service.CheckAsync("v1.3.0", CancellationToken.None);

            Assert.False(result.IsUpdateAvailable);
            Assert.Equal("1.3.0", result.LatestVersion);
        }

        [Fact]
        public async Task CheckAsync_IgnoresSkippedVersion_ForManualChecks()
        {
            var service = new PmgUpdateAwarenessService(
                new StubLatestReleaseChecker(new GitHubLatestRelease("v1.3.0", "https://example.test/release")),
                "1.2.0");

            var result = await service.CheckAsync("v1.3.0", respectSkippedVersion: false, CancellationToken.None);

            Assert.True(result.IsUpdateAvailable);
            Assert.Equal("1.3.0", result.LatestVersion);
        }

        private sealed class StubLatestReleaseChecker : ILatestReleaseChecker
        {
            private readonly GitHubLatestRelease? _release;

            public StubLatestReleaseChecker(GitHubLatestRelease? release)
            {
                _release = release;
            }

            public Task<GitHubLatestRelease?> GetLatestStableReleaseAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(_release);
            }
        }
    }
}
