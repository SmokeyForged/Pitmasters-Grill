using PitmastersGrill.Models;
using System;
using System.IO;
using System.Linq;

namespace PitmastersGrill.Persistence
{
    public static class AppLogger
    {
        public static class Categories
        {
            public const string App = "app";
            public const string Ui = "ui";
            public const string Clipboard = "clipboard";
            public const string Resolver = "resolver";
            public const string Providers = "providers";
            public const string KillmailImport = "killmail-import";
            public const string Database = "database";
            public const string Errors = "errors";
        }

        private enum LogSeverity
        {
            Debug,
            Info,
            Warn,
            Error
        }

        private static readonly object SyncRoot = new();

        private static string _sessionId = DateTime.Now.ToString("yyyyMMddHHmmssfff");
        private static bool _initialized;
        private static AppLogLevel _currentLogLevel = AppLogLevel.Normal;

        public static AppLogLevel CurrentLogLevel
        {
            get
            {
                lock (SyncRoot)
                {
                    return _currentLogLevel;
                }
            }
        }

        public static bool IsDebugEnabled
        {
            get
            {
                lock (SyncRoot)
                {
                    return _currentLogLevel == AppLogLevel.Debug;
                }
            }
        }

        public static void Initialize(string versionLabel, string[]? args)
        {
            try
            {
                lock (SyncRoot)
                {
                    LogMaintenanceService.RunMaintenanceIfNeeded();

                    _sessionId = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                    _initialized = true;

                    WriteCore(
                        Categories.App,
                        LogSeverity.Info,
                        $"Session start sessionId={_sessionId} version={Sanitize(versionLabel)} args={FormatArgs(args)}",
                        null);
                }
            }
            catch
            {
                // Logging must never block startup.
            }
        }

        public static void ConfigureLogLevel(AppLogLevel logLevel)
        {
            try
            {
                lock (SyncRoot)
                {
                    _currentLogLevel = logLevel;
                    EnsureInitialized_NoThrow();

                    WriteCore(
                        Categories.App,
                        LogSeverity.Info,
                        $"Log level configured. sessionId={_sessionId} level={_currentLogLevel}",
                        null);
                }
            }
            catch
            {
                // Logging configuration must never break the app.
            }
        }

        public static void Shutdown()
        {
            try
            {
                Info(Categories.App, $"Session end sessionId={_sessionId}");
            }
            catch
            {
                // best effort only
            }
        }

        public static void AppDebug(string message) => Debug(Categories.App, message);
        public static void AppInfo(string message) => Info(Categories.App, message);
        public static void AppWarn(string message) => Warn(Categories.App, message);
        public static void AppError(string message, Exception? ex = null) => Error(Categories.App, message, ex);

        public static void UiDebug(string message) => Debug(Categories.Ui, message);
        public static void UiInfo(string message) => Info(Categories.Ui, message);
        public static void UiWarn(string message) => Warn(Categories.Ui, message);
        public static void UiError(string message, Exception? ex = null) => Error(Categories.Ui, message, ex);

        public static void ClipboardDebug(string message) => Debug(Categories.Clipboard, message);
        public static void ClipboardInfo(string message) => Info(Categories.Clipboard, message);
        public static void ClipboardWarn(string message) => Warn(Categories.Clipboard, message);
        public static void ClipboardError(string message, Exception? ex = null) => Error(Categories.Clipboard, message, ex);

        public static void ResolverDebug(string message) => Debug(Categories.Resolver, message);
        public static void ResolverInfo(string message) => Info(Categories.Resolver, message);
        public static void ResolverWarn(string message) => Warn(Categories.Resolver, message);
        public static void ResolverError(string message, Exception? ex = null) => Error(Categories.Resolver, message, ex);

        public static void ProviderDebug(string message) => Debug(Categories.Providers, message);
        public static void ProviderInfo(string message) => Info(Categories.Providers, message);
        public static void ProviderWarn(string message) => Warn(Categories.Providers, message);
        public static void ProviderError(string message, Exception? ex = null) => Error(Categories.Providers, message, ex);

        public static void KillmailImportDebug(string message) => Debug(Categories.KillmailImport, message);
        public static void KillmailImportInfo(string message) => Info(Categories.KillmailImport, message);
        public static void KillmailImportWarn(string message) => Warn(Categories.KillmailImport, message);
        public static void KillmailImportError(string message, Exception? ex = null) => Error(Categories.KillmailImport, message, ex);

        public static void DatabaseDebug(string message) => Debug(Categories.Database, message);
        public static void DatabaseInfo(string message) => Info(Categories.Database, message);
        public static void DatabaseWarn(string message) => Warn(Categories.Database, message);
        public static void DatabaseError(string message, Exception? ex = null) => Error(Categories.Database, message, ex);

        public static void ErrorOnly(string message, Exception? ex = null) => Error(Categories.Errors, message, ex);

        public static void Debug(string category, string message)
        {
            if (!IsDebugEnabled)
            {
                return;
            }

            Write(category, LogSeverity.Debug, message, null);
        }

        public static void Info(string category, string message)
        {
            Write(category, LogSeverity.Info, message, null);
        }

        public static void Warn(string category, string message)
        {
            Write(category, LogSeverity.Warn, message, null);
        }

        public static void Error(string category, string message, Exception? ex = null)
        {
            Write(category, LogSeverity.Error, message, ex);
        }

        private static void Write(string category, LogSeverity severity, string message, Exception? ex)
        {
            try
            {
                lock (SyncRoot)
                {
                    EnsureInitialized_NoThrow();
                    WriteCore(category, severity, message, ex);
                }
            }
            catch
            {
                // Logging failures are intentionally swallowed.
            }
        }

        private static void EnsureInitialized_NoThrow()
        {
            if (_initialized)
            {
                return;
            }

            try
            {
                LogMaintenanceService.RunMaintenanceIfNeeded();
            }
            catch
            {
                // best effort only
            }

            _sessionId = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            _initialized = true;
        }

        private static void WriteCore(string category, LogSeverity severity, string message, Exception? ex)
        {
            var safeCategory = LogPaths.NormalizeCategory(category);
            var now = DateTime.Now;
            var primaryPath = LogPaths.GetActiveLogPath(safeCategory);
            var line = BuildLine(now, severity, safeCategory, message);

            LogMaintenanceService.RunMaintenanceIfNeeded(now);

            AppendLine(primaryPath, line);

            if (severity == LogSeverity.Error &&
                !string.Equals(safeCategory, Categories.Errors, StringComparison.OrdinalIgnoreCase))
            {
                AppendLine(LogPaths.GetActiveLogPath(Categories.Errors), line);
            }

            if (ex != null)
            {
                var exceptionLine = BuildLine(
                    now,
                    LogSeverity.Error,
                    safeCategory,
                    $"Exception: {Sanitize(ex.ToString())}");

                AppendLine(primaryPath, exceptionLine);

                if (!string.Equals(safeCategory, Categories.Errors, StringComparison.OrdinalIgnoreCase))
                {
                    AppendLine(LogPaths.GetActiveLogPath(Categories.Errors), exceptionLine);
                }
            }
        }

        private static string BuildLine(DateTime localTime, LogSeverity severity, string category, string message)
        {
            return $"{localTime:yyyy-MM-dd HH:mm:ss.fff} [{severity.ToString().ToUpperInvariant()}] [{category}] {Sanitize(message)}";
        }

        private static void AppendLine(string path, string line)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, line + Environment.NewLine);
        }

        private static string FormatArgs(string[]? args)
        {
            if (args == null || args.Length == 0)
            {
                return "<none>";
            }

            return string.Join(" ", args.Select(Sanitize));
        }

        private static string Sanitize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
        }
    }
}