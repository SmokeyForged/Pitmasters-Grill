using System;
using System.Text.RegularExpressions;

namespace PitmastersGrill.Services
{
    public static class ReleaseVersionComparer
    {
        public static bool IsNewerStableVersion(string? currentVersionText, string? latestVersionText)
        {
            if (!TryParseStableVersion(latestVersionText, out var latestVersion))
            {
                return false;
            }

            if (!TryParseStableVersion(currentVersionText, out var currentVersion))
            {
                currentVersion = new Version(0, 0, 0);
            }

            return latestVersion.CompareTo(currentVersion) > 0;
        }

        public static bool IsSameStableVersion(string? leftVersionText, string? rightVersionText)
        {
            return TryParseStableVersion(leftVersionText, out var leftVersion)
                && TryParseStableVersion(rightVersionText, out var rightVersion)
                && leftVersion.CompareTo(rightVersion) == 0;
        }

        public static string NormalizeStableVersionText(string? versionText)
        {
            return TryParseStableVersion(versionText, out var version)
                ? version.ToString(3)
                : string.Empty;
        }

        private static bool TryParseStableVersion(string? versionText, out Version version)
        {
            version = new Version(0, 0, 0);

            if (string.IsNullOrWhiteSpace(versionText))
            {
                return false;
            }

            var normalized = versionText.Trim();

            if (normalized.StartsWith("General Release-", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized["General Release-".Length..];
            }

            normalized = normalized.TrimStart('v', 'V').Trim();

            if (normalized.Contains('-', StringComparison.Ordinal))
            {
                return false;
            }

            var match = Regex.Match(normalized, @"^(\d+)\.(\d+)\.(\d+)$");
            if (!match.Success)
            {
                return false;
            }

            version = new Version(
                int.Parse(match.Groups[1].Value),
                int.Parse(match.Groups[2].Value),
                int.Parse(match.Groups[3].Value));

            return true;
        }
    }
}
