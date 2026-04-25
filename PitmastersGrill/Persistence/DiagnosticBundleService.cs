using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace PitmastersGrill.Persistence
{
    public static class DiagnosticBundleService
    {
        private const string VersionLabel = "Technical Preview-v0.9.2";
        private const int MaximumBundlesToRetain = 20;

        public static string GetDiagnosticsDirectory()
        {
            return AppPaths.EnsureSubdirectory("diagnostics");
        }

        public static string TryCreateBundle(string reason, Exception? exception = null)
        {
            try
            {
                var root = GetDiagnosticsDirectory();
                var safeReason = SanitizeFilePart(reason);
                var bundlePath = Path.Combine(
                    root,
                    $"pmg-diagnostics-{DateTime.Now:yyyyMMdd-HHmmssfff}-{safeReason}.zip");

                using var archive = ZipFile.Open(bundlePath, ZipArchiveMode.Create);

                AddManifest(archive, reason, exception);
                AddBundleNotes(archive);
                AddDirectoryFiles(archive, LogPaths.GetActiveDirectory(), "logs/active");
                AddDirectoryFiles(archive, AppPaths.GetDebugDirectory(), "debug");

                PruneOldBundles(root);

                AppLogger.AppInfo($"Diagnostic bundle created. reason={Sanitize(reason)} path={RedactSensitiveDiagnosticsText(bundlePath)}");

                return bundlePath;
            }
            catch (Exception bundleException)
            {
                try
                {
                    AppLogger.AppWarn($"Diagnostic bundle creation failed. reason={Sanitize(reason)} message={Sanitize(bundleException.Message)}");
                }
                catch
                {
                    // best effort only
                }

                return string.Empty;
            }
        }

        private static void AddManifest(ZipArchive archive, string reason, Exception? exception)
        {
            var entry = archive.CreateEntry("manifest.txt", CompressionLevel.Fastest);

            using var stream = entry.Open();
            using var writer = new StreamWriter(stream, Encoding.UTF8);

            writer.WriteLine("Pitmasters Grill Diagnostic Bundle");
            writer.WriteLine($"createdLocal={DateTime.Now:O}");
            writer.WriteLine($"createdUtc={DateTime.UtcNow:O}");
            writer.WriteLine($"version={VersionLabel}");
            writer.WriteLine($"reason={Sanitize(reason)}");
            writer.WriteLine($"osVersion={Environment.OSVersion}");
            writer.WriteLine($"is64BitProcess={Environment.Is64BitProcess}");
            writer.WriteLine($"logLevel={AppLogger.CurrentLogLevel}");
            writer.WriteLine("privacyNote=Raw clipboard contents are not collected by diagnostic bundle creation.");
            writer.WriteLine("sanitizationNote=Diagnostic log copies in this bundle redact common local Windows profile and PMG app-data paths.");
            writer.WriteLine("machineName=redacted-by-default");
            writer.WriteLine("appDataDirectory=redacted-by-default");

            if (exception == null)
            {
                return;
            }

            writer.WriteLine();
            writer.WriteLine("Exception");
            writer.WriteLine($"type={exception.GetType().FullName}");
            writer.WriteLine($"message={Sanitize(RedactSensitiveDiagnosticsText(exception.Message))}");
            writer.WriteLine("stackTrace=");
            writer.WriteLine(RedactSensitiveDiagnosticsText(exception.ToString()));
        }

        private static void AddBundleNotes(ZipArchive archive)
        {
            var entry = archive.CreateEntry("bundle-notes.txt", CompressionLevel.Fastest);

            using var stream = entry.Open();
            using var writer = new StreamWriter(stream, Encoding.UTF8);

            writer.WriteLine("Pitmasters Grill diagnostic bundle notes");
            writer.WriteLine();
            writer.WriteLine("This bundle is intended for troubleshooting PMG behavior, crashes, clipboard intake issues, provider failures, and board population problems.");
            writer.WriteLine("It includes active PMG logs and debug traces when they are present.");
            writer.WriteLine("Raw clipboard contents are not collected by the diagnostic bundle service.");
            writer.WriteLine("Copies of log/debug files are sanitized for common local Windows profile and PMG app-data paths before they are added to the ZIP.");
            writer.WriteLine("Logs may still include EVE character names, public zKill URLs, public provider URLs, public IDs, and normal app activity context.");
            writer.WriteLine("Review this ZIP before attaching it to a public GitHub issue or sharing it with another person.");
        }

        private static void AddDirectoryFiles(ZipArchive archive, string sourceDirectory, string archiveDirectory)
        {
            if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
            {
                return;
            }

            var files = Directory
                .GetFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var file in files)
            {
                try
                {
                    var fileName = Path.GetFileName(file);

                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        continue;
                    }

                    AddSanitizedTextFile(
                        archive,
                        file,
                        $"{archiveDirectory}/{SanitizeFilePart(fileName)}");
                }
                catch
                {
                    // Skip individual files that are locked or unreadable.
                }
            }
        }

        private static void AddSanitizedTextFile(ZipArchive archive, string sourceFile, string archivePath)
        {
            var entry = archive.CreateEntry(archivePath, CompressionLevel.Fastest);

            using var stream = entry.Open();
            using var writer = new StreamWriter(stream, Encoding.UTF8);

            try
            {
                var text = File.ReadAllText(sourceFile, Encoding.UTF8);
                writer.Write(RedactSensitiveDiagnosticsText(text));
            }
            catch (Exception ex)
            {
                writer.WriteLine("Pitmasters Grill diagnostic file placeholder");
                writer.WriteLine("fileReadable=false");
                writer.WriteLine($"reason={Sanitize(RedactSensitiveDiagnosticsText(ex.Message))}");
            }
        }

        private static void PruneOldBundles(string diagnosticsDirectory)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(diagnosticsDirectory) || !Directory.Exists(diagnosticsDirectory))
                {
                    return;
                }

                var bundles = Directory
                    .GetFiles(diagnosticsDirectory, "pmg-diagnostics-*.zip", SearchOption.TopDirectoryOnly)
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(info => info.CreationTimeUtc)
                    .ToList();

                foreach (var staleBundle in bundles.Skip(MaximumBundlesToRetain))
                {
                    try
                    {
                        staleBundle.Delete();
                    }
                    catch
                    {
                        // Retention cleanup should never block diagnostics.
                    }
                }
            }
            catch
            {
                // Retention cleanup is best effort only.
            }
        }

        private static string RedactSensitiveDiagnosticsText(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var redacted = value;

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var userName = Environment.UserName;

            redacted = ReplacePath(redacted, Path.Combine(localAppData, "PitmastersGrill"), "%LOCALAPPDATA%\\PitmastersGrill");
            redacted = ReplacePath(redacted, localAppData, "%LOCALAPPDATA%");
            redacted = ReplacePath(redacted, userProfile, "%USERPROFILE%");

            if (!string.IsNullOrWhiteSpace(userName))
            {
                redacted = redacted.Replace(userName, "<windows-user>", StringComparison.OrdinalIgnoreCase);
            }

            return redacted;
        }

        private static string ReplacePath(string value, string? path, string replacement)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(path))
            {
                return value;
            }

            var normalizedBackslash = path.Replace('/', '\\');
            var normalizedSlash = path.Replace('\\', '/');

            return value
                .Replace(normalizedBackslash, replacement, StringComparison.OrdinalIgnoreCase)
                .Replace(normalizedSlash, replacement.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
        }

        private static string SanitizeFilePart(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            var invalidCharacters = Path.GetInvalidFileNameChars();
            var sanitized = new string(value
                .Trim()
                .Select(ch => invalidCharacters.Contains(ch) || char.IsWhiteSpace(ch) ? '-' : ch)
                .ToArray());

            while (sanitized.Contains("--", StringComparison.Ordinal))
            {
                sanitized = sanitized.Replace("--", "-", StringComparison.Ordinal);
            }

            sanitized = sanitized.Trim('-');

            return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
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
