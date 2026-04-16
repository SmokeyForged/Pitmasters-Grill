using System;
using System.IO;

namespace PitmastersGrill.Persistence
{
    public static class LogMaintenanceService
    {
        private static readonly object SyncRoot = new();
        private static DateTime _lastMaintenanceDay = DateTime.MinValue.Date;

        public static void RunMaintenanceIfNeeded()
        {
            RunMaintenanceIfNeeded(DateTime.Now);
        }

        public static void RunMaintenanceIfNeeded(DateTime localNow)
        {
            try
            {
                var currentDay = localNow.Date;

                lock (SyncRoot)
                {
                    if (_lastMaintenanceDay == currentDay)
                    {
                        return;
                    }

                    RotateActiveLogs(currentDay);
                    PurgeExpiredArchives(currentDay);

                    _lastMaintenanceDay = currentDay;
                }
            }
            catch
            {
                // Logging maintenance must never break the app.
            }
        }

        private static void RotateActiveLogs(DateTime currentDay)
        {
            var activeDirectory = LogPaths.GetActiveDirectory();

            foreach (var filePath in Directory.GetFiles(activeDirectory, "*.log"))
            {
                try
                {
                    var lastWriteDay = File.GetLastWriteTime(filePath).Date;
                    if (lastWriteDay >= currentDay)
                    {
                        continue;
                    }

                    var category = Path.GetFileNameWithoutExtension(filePath);
                    var archivePath = LogPaths.BuildArchiveLogPath(category, lastWriteDay);
                    var collisionIndex = 1;

                    while (File.Exists(archivePath))
                    {
                        archivePath = LogPaths.BuildArchiveLogPath(category, lastWriteDay, collisionIndex);
                        collisionIndex++;
                    }

                    File.Move(filePath, archivePath);
                }
                catch
                {
                    // Best effort only.
                }
            }
        }

        private static void PurgeExpiredArchives(DateTime currentDay)
        {
            var archiveDirectory = LogPaths.GetArchiveDirectory();
            var cutoffDay = currentDay.AddDays(-LogPaths.ArchiveRetentionDays);

            foreach (var filePath in Directory.GetFiles(archiveDirectory, "*.log"))
            {
                try
                {
                    var lastWriteDay = File.GetLastWriteTime(filePath).Date;
                    if (lastWriteDay < cutoffDay)
                    {
                        File.Delete(filePath);
                    }
                }
                catch
                {
                    // Best effort only.
                }
            }
        }
    }
}