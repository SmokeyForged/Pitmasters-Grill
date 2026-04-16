using System;
using System.IO;

namespace PitmastersLittleGrill.Persistence
{
    public static class AppPaths
    {
        private const string AppDirectoryName = "PitmastersLittleGrill";

        public static string GetAppDataDirectory()
        {
            var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appPath = Path.Combine(basePath, AppDirectoryName);

            Directory.CreateDirectory(appPath);

            return appPath;
        }

        public static string EnsureSubdirectory(string name)
        {
            var path = Path.Combine(GetAppDataDirectory(), name);
            Directory.CreateDirectory(path);
            return path;
        }

        public static string GetDatabasePath()
        {
            return Path.Combine(GetAppDataDirectory(), "pmg.db");
        }

        public static string GetConfigDirectory()
        {
            return EnsureSubdirectory("config");
        }

        public static string GetSettingsPath()
        {
            return Path.Combine(GetConfigDirectory(), "settings.json");
        }

        public static string GetLogsRootDirectory()
        {
            return EnsureSubdirectory("logs");
        }

        public static string GetDebugDirectory()
        {
            return EnsureSubdirectory("debug");
        }
    }
}