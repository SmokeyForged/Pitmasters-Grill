using System;
using System.IO;

namespace PitmastersLittleGrill.Persistence
{
    public static class AppPaths
    {
        public static string GetAppDataDirectory()
        {
            var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appPath = Path.Combine(basePath, "PitmastersLittleGrill");

            Directory.CreateDirectory(appPath);

            return appPath;
        }

        public static string GetDatabasePath()
        {
            return Path.Combine(GetAppDataDirectory(), "pmg.db");
        }
    }
}