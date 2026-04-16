using System;
using System.IO;

namespace PitmastersGrill.Persistence
{
    public static class DebugTraceWriter
    {
        private static readonly object SyncRoot = new();

        public static void Clear()
        {
            try
            {
                var path = DebugPaths.GetResolverTracePath();

                lock (SyncRoot)
                {
                    File.WriteAllText(path, string.Empty);
                }
            }
            catch
            {
                // Ignore debug trace failures.
            }
        }

        public static void WriteLine(string message)
        {
            try
            {
                var path = DebugPaths.GetResolverTracePath();
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";

                lock (SyncRoot)
                {
                    File.AppendAllText(path, line);
                }
            }
            catch
            {
                // Ignore debug trace failures.
            }
        }
    }
}