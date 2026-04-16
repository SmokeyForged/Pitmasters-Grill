using System;
using System.IO;
using System.Linq;

namespace PitmastersLittleGrill.Persistence
{
    public static class LogPaths
    {
        public const int ArchiveRetentionDays = 14;

        public static string GetLogRootDirectory()
        {
            return AppPaths.GetLogsRootDirectory();
        }

        public static string GetActiveDirectory()
        {
            var path = Path.Combine(GetLogRootDirectory(), "active");
            Directory.CreateDirectory(path);
            return path;
        }

        public static string GetArchiveDirectory()
        {
            var path = Path.Combine(GetLogRootDirectory(), "archive");
            Directory.CreateDirectory(path);
            return path;
        }

        public static string GetActiveLogPath(string category)
        {
            return Path.Combine(GetActiveDirectory(), $"{NormalizeCategory(category)}.log");
        }

        public static string BuildArchiveLogPath(string category, DateTime localDay, int collisionIndex = 0)
        {
            var safeCategory = NormalizeCategory(category);
            var fileName = collisionIndex <= 0
                ? $"{safeCategory}-{localDay:yyyy-MM-dd}.log"
                : $"{safeCategory}-{localDay:yyyy-MM-dd}-{collisionIndex}.log";

            return Path.Combine(GetArchiveDirectory(), fileName);
        }

        public static string NormalizeCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return "general";
            }

            var invalidCharacters = Path.GetInvalidFileNameChars();
            var filtered = new string(category
                .Trim()
                .ToLowerInvariant()
                .Select(ch => invalidCharacters.Contains(ch) ? '_' : ch)
                .ToArray());

            return string.IsNullOrWhiteSpace(filtered) ? "general" : filtered;
        }
    }
}