using System;
using System.IO;

namespace PitmastersLittleGrill.Persistence
{
    public static class KillmailPaths
    {
        public static string GetKillmailDataDirectory()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var dataDirectory = Path.Combine(baseDirectory, "Data", "KillmailDb");

            Directory.CreateDirectory(dataDirectory);
            return dataDirectory;
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
    }
}