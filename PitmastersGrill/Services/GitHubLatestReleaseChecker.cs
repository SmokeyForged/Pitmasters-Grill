using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersGrill.Services
{
    public sealed class GitHubLatestRelease
    {
        public GitHubLatestRelease(string version, string releasePageUrl)
        {
            Version = version;
            ReleasePageUrl = releasePageUrl;
        }

        public string Version { get; }

        public string ReleasePageUrl { get; }
    }

    public interface ILatestReleaseChecker
    {
        Task<GitHubLatestRelease?> GetLatestStableReleaseAsync(CancellationToken cancellationToken);
    }

    public sealed class GitHubLatestReleaseChecker : ILatestReleaseChecker
    {
        private static readonly Uri LatestReleaseUri = new("https://api.github.com/repos/SmokeyForged/Pitmasters-Grill/releases/latest");

        public async Task<GitHubLatestRelease?> GetLatestStableReleaseAsync(CancellationToken cancellationToken)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Pitmasters-Grill");

            using var response = await client.GetAsync(LatestReleaseUri, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;

            if (TryGetBoolean(root, "draft") || TryGetBoolean(root, "prerelease"))
            {
                return null;
            }

            var tagName = TryGetString(root, "tag_name");
            var releasePageUrl = TryGetString(root, "html_url");
            var stableVersion = ReleaseVersionComparer.NormalizeStableVersionText(tagName);

            if (string.IsNullOrWhiteSpace(stableVersion) || string.IsNullOrWhiteSpace(releasePageUrl))
            {
                return null;
            }

            return new GitHubLatestRelease(stableVersion, releasePageUrl);
        }

        private static string TryGetString(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;
        }

        private static bool TryGetBoolean(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var property)
                && property.ValueKind == JsonValueKind.True;
        }
    }
}
