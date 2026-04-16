using PitmastersLittleGrill.Models;
using System;
using System.IO;
using System.Text.Json;

namespace PitmastersLittleGrill.Persistence
{
    public static class KillmailPaths
    {
        private const string DefaultDisplayPath = @"%LOCALAPPDATA%\PitmastersLittleGrill\KillmailDb";

        public static string GetDefaultKillmailDataDirectory()
        {
            var path = ExpandPathTokens(DefaultDisplayPath);
            Directory.CreateDirectory(path);
            return path;
        }

        public static string GetDefaultKillmailDataDirectoryDisplayPath()
        {
            return DefaultDisplayPath;
        }

        public static string GetKillmailDataDirectory()
        {
            var configuredPath = TryGetConfiguredKillmailDataDirectory();

            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                Directory.CreateDirectory(configuredPath);
                return configuredPath;
            }

            return GetDefaultKillmailDataDirectory();
        }

        public static string GetKillmailDataDirectoryDisplayPath()
        {
            var configuredRaw = TryGetConfiguredKillmailDataDirectoryRaw();

            if (!string.IsNullOrWhiteSpace(configuredRaw))
            {
                return CollapsePathTokens(configuredRaw.Trim());
            }

            return GetDefaultKillmailDataDirectoryDisplayPath();
        }

        public static string GetKillmailDatabasePath()
        {
            return Path.Combine(GetKillmailDataDirectory(), "pmg-killmail.db");
        }

        public static string GetKillmailArchiveCacheDirectory()
        {
            var archiveDirectory = Path.Combine(GetKillmailDataDirectory(), "Archives");
            Directory.CreateDirectory(archiveDirectory);
            return archiveDirectory;
        }

        public static string GetKillmailArchivePath(string dayUtc)
        {
            return Path.Combine(GetKillmailArchiveCacheDirectory(), $"killmails-{dayUtc}.tar.bz2");
        }

        public static string GetKillmailExtractedDayDirectory(string dayUtc)
        {
            var extractDirectory = Path.Combine(GetKillmailArchiveCacheDirectory(), $"extract_{dayUtc}");
            Directory.CreateDirectory(extractDirectory);
            return extractDirectory;
        }

        public static string GetKillmailExtractedDayMarkerPath(string dayUtc)
        {
            return Path.Combine(GetKillmailExtractedDayDirectory(dayUtc), ".pmg_extract_complete");
        }

        public static bool IsUsingConfiguredOverride()
        {
            return !string.IsNullOrWhiteSpace(TryGetConfiguredKillmailDataDirectoryRaw());
        }

        public static string GetKillmailDataDirectorySourceDescription()
        {
            return IsUsingConfiguredOverride()
                ? "settings override"
                : "default %LOCALAPPDATA%";
        }

        public static string NormalizeForComparison(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                var expanded = ExpandPathTokens(path.Trim());
                var fullPath = Path.GetFullPath(expanded);

                return fullPath
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .ToUpperInvariant();
            }
            catch
            {
                return path
                    .Trim()
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .ToUpperInvariant();
            }
        }

        public static string ExpandPathTokens(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return Environment.ExpandEnvironmentVariables(path.Trim());
        }

        public static string CollapsePathTokens(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var trimmed = path.Trim();

            try
            {
                var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(trimmed));

                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                if (!string.IsNullOrWhiteSpace(localAppData) &&
                    fullPath.StartsWith(localAppData, StringComparison.OrdinalIgnoreCase))
                {
                    return @"%LOCALAPPDATA%" + fullPath.Substring(localAppData.Length);
                }

                if (!string.IsNullOrWhiteSpace(userProfile) &&
                    fullPath.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase))
                {
                    return @"%USERPROFILE%" + fullPath.Substring(userProfile.Length);
                }

                return trimmed;
            }
            catch
            {
                return trimmed;
            }
        }

        private static string TryGetConfiguredKillmailDataDirectory()
        {
            var raw = TryGetConfiguredKillmailDataDirectoryRaw();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            try
            {
                var expanded = ExpandPathTokens(raw);
                if (string.IsNullOrWhiteSpace(expanded))
                {
                    return string.Empty;
                }

                return Path.GetFullPath(expanded);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string TryGetConfiguredKillmailDataDirectoryRaw()
        {
            try
            {
                var settingsPath = AppPaths.GetSettingsPath();
                if (!File.Exists(settingsPath))
                {
                    return string.Empty;
                }

                var json = File.ReadAllText(settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);

                if (settings == null || string.IsNullOrWhiteSpace(settings.KillmailDataRootPath))
                {
                    return string.Empty;
                }

                return settings.KillmailDataRootPath.Trim();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}