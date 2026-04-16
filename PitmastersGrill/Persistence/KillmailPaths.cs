using PitmastersGrill.Models;
using System;
using System.IO;
using System.Text.Json;

namespace PitmastersGrill.Persistence
{
    public static class KillmailPaths
    {
        private const string DefaultDisplayPath = @"%LOCALAPPDATA%\PitmastersGrill\KillmailDb";
        private static readonly TimeSpan ArchiveCacheTtl = TimeSpan.FromHours(24);

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

        public static TimeSpan GetArchiveCacheTtl()
        {
            return ArchiveCacheTtl;
        }

        public static void PurgeArchiveCacheBestEffort(string excludeDayUtc = "")
        {
            try
            {
                var archiveRoot = GetKillmailArchiveCacheDirectory();
                if (!Directory.Exists(archiveRoot))
                {
                    return;
                }

                var nowUtc = DateTime.UtcNow;

                foreach (var archivePath in Directory.GetFiles(archiveRoot, "killmails-*.tar.bz2", SearchOption.TopDirectoryOnly))
                {
                    TryDeleteArchiveFileIfExpired(archivePath, excludeDayUtc, nowUtc);
                }

                foreach (var extractDirectory in Directory.GetDirectories(archiveRoot, "extract_*", SearchOption.TopDirectoryOnly))
                {
                    TryDeleteExtractDirectoryIfExpired(extractDirectory, excludeDayUtc, nowUtc);
                }
            }
            catch (Exception ex)
            {
                DebugTraceWriter.WriteLine($"killmail cache purge failed: scope=best_effort, message={ex.Message}");
            }
        }

        public static void ClearArchiveCacheBestEffort()
        {
            try
            {
                var archiveRoot = GetKillmailArchiveCacheDirectory();
                if (!Directory.Exists(archiveRoot))
                {
                    return;
                }

                foreach (var archivePath in Directory.GetFiles(archiveRoot, "killmails-*.tar.bz2", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        File.Delete(archivePath);
                        DebugTraceWriter.WriteLine($"killmail cache clear removed archive: path={archivePath}");
                    }
                    catch (Exception ex)
                    {
                        DebugTraceWriter.WriteLine($"killmail cache clear archive failed: path={archivePath}, message={ex.Message}");
                    }
                }

                foreach (var extractDirectory in Directory.GetDirectories(archiveRoot, "extract_*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        Directory.Delete(extractDirectory, true);
                        DebugTraceWriter.WriteLine($"killmail cache clear removed extract: path={extractDirectory}");
                    }
                    catch (Exception ex)
                    {
                        DebugTraceWriter.WriteLine($"killmail cache clear extract failed: path={extractDirectory}, message={ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugTraceWriter.WriteLine($"killmail cache clear failed: scope=best_effort, message={ex.Message}");
            }
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

        private static void TryDeleteArchiveFileIfExpired(string archivePath, string excludeDayUtc, DateTime nowUtc)
        {
            try
            {
                var fileName = Path.GetFileName(archivePath);
                var dayUtc = TryParseDayUtcFromArchiveFileName(fileName);

                if (IsExcludedDay(dayUtc, excludeDayUtc))
                {
                    return;
                }

                var lastWriteUtc = File.GetLastWriteTimeUtc(archivePath);
                var age = nowUtc - lastWriteUtc;

                if (age <= ArchiveCacheTtl)
                {
                    return;
                }

                File.Delete(archivePath);

                DebugTraceWriter.WriteLine(
                    $"killmail cache purge removed archive: day={dayUtc}, ageHours={age.TotalHours:F2}, path={archivePath}");
            }
            catch (Exception ex)
            {
                DebugTraceWriter.WriteLine(
                    $"killmail cache purge archive failed: path={archivePath}, message={ex.Message}");
            }
        }

        private static void TryDeleteExtractDirectoryIfExpired(string extractDirectory, string excludeDayUtc, DateTime nowUtc)
        {
            try
            {
                var directoryName = Path.GetFileName(extractDirectory);
                var dayUtc = TryParseDayUtcFromExtractDirectoryName(directoryName);

                if (IsExcludedDay(dayUtc, excludeDayUtc))
                {
                    return;
                }

                var referenceUtc = GetExtractReferenceTimeUtc(extractDirectory, dayUtc);
                var age = nowUtc - referenceUtc;

                if (age <= ArchiveCacheTtl)
                {
                    return;
                }

                Directory.Delete(extractDirectory, true);

                DebugTraceWriter.WriteLine(
                    $"killmail cache purge removed extract: day={dayUtc}, ageHours={age.TotalHours:F2}, path={extractDirectory}");
            }
            catch (Exception ex)
            {
                DebugTraceWriter.WriteLine(
                    $"killmail cache purge extract failed: path={extractDirectory}, message={ex.Message}");
            }
        }

        private static DateTime GetExtractReferenceTimeUtc(string extractDirectory, string dayUtc)
        {
            try
            {
                var markerPath = Path.Combine(extractDirectory, ".pmg_extract_complete");
                if (File.Exists(markerPath))
                {
                    return File.GetLastWriteTimeUtc(markerPath);
                }
            }
            catch
            {
            }

            try
            {
                return Directory.GetLastWriteTimeUtc(extractDirectory);
            }
            catch
            {
                return DateTime.UtcNow;
            }
        }

        private static bool IsExcludedDay(string candidateDayUtc, string excludeDayUtc)
        {
            if (string.IsNullOrWhiteSpace(candidateDayUtc) || string.IsNullOrWhiteSpace(excludeDayUtc))
            {
                return false;
            }

            return string.Equals(candidateDayUtc, excludeDayUtc, StringComparison.Ordinal);
        }

        private static string TryParseDayUtcFromArchiveFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return string.Empty;
            }

            if (!fileName.StartsWith("killmails-", StringComparison.OrdinalIgnoreCase) ||
                !fileName.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var dayUtc = fileName.Substring("killmails-".Length);
            dayUtc = dayUtc.Substring(0, dayUtc.Length - ".tar.bz2".Length);
            return dayUtc;
        }

        private static string TryParseDayUtcFromExtractDirectoryName(string directoryName)
        {
            if (string.IsNullOrWhiteSpace(directoryName))
            {
                return string.Empty;
            }

            if (!directoryName.StartsWith("extract_", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return directoryName.Substring("extract_".Length);
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