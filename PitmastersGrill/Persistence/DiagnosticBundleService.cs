using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using PitmastersGrill.Services;

namespace PitmastersGrill.Persistence
{
    public static class DiagnosticBundleService
    {
        private const string VersionLabel = "Technical Preview-v0.9.4";
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
                    $"PMG-diagnostics-{DateTime.Now:yyyyMMdd-HHmmssfff}-{safeReason}.zip");

                using var archive = ZipFile.Open(bundlePath, ZipArchiveMode.Create);

                AddManifest(archive, reason, exception);
                AddBundleNotes(archive);
                AddSettingsSummary(archive);
                AddProviderHealthSummary(archive);
                AddPerformanceSummary(archive);
                AddCacheSummary(archive);
                AddClipboardSummary(archive);
                AddResolverFailureSummary(archive);
                AddCynoSignalSummary(archive);
                AddIgnoreSummary(archive);
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
            writer.WriteLine($"assemblyVersion={Assembly.GetExecutingAssembly().GetName().Version}");
            writer.WriteLine($"framework={System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
            writer.WriteLine($"osDescription={System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
            writer.WriteLine($"processArchitecture={System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
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

        private static void AddSettingsSummary(ZipArchive archive)
        {
            AddTextEntry(archive, "settings-summary.txt", writer =>
            {
                writer.WriteLine("PMG settings summary");
                var settings = new AppSettingsService().Load();
                writer.WriteLine($"settingsPath={RedactSensitiveDiagnosticsText(AppPaths.GetSettingsPath())}");
                writer.WriteLine($"visualTheme={settings.VisualTheme}");
                writer.WriteLine($"colorBlindMode={settings.ColorBlindMode}");
                writer.WriteLine($"darkMode={settings.DarkModeEnabled}");
                writer.WriteLine($"logLevel={AppLogger.CurrentLogLevel}");
                writer.WriteLine($"killmailDataPath={RedactSensitiveDiagnosticsText(KillmailPaths.GetKillmailDataDirectoryDisplayPath())}");
                writer.WriteLine($"killmailDataPathSource={KillmailPaths.GetKillmailDataDirectorySourceDescription()}");
                writer.WriteLine("Secrets/tokens/credentials are not collected.");
            });
        }

        private static void AddProviderHealthSummary(ZipArchive archive)
        {
            AddTextEntry(archive, "provider-health.txt", writer =>
            {
                writer.WriteLine("Provider health summary");
                foreach (var provider in DiagnosticTelemetry.GetProviderHealthSnapshots())
                {
                    writer.WriteLine($"{provider.ProviderName}: status={provider.Status}; lastSuccessUtc={provider.LastSuccessUtc:O}; lastFailureUtc={provider.LastFailureUtc:O}; failures={provider.RecentFailureCount}; avgLatencyMs={provider.AverageLatencyMs:F0}; backoffActive={provider.IsBackoffActive}; backoffUntilUtc={provider.BackoffUntilUtc:O}; cacheHits={provider.CacheHitCount}; cacheMisses={provider.CacheMissCount}; lastError={provider.LastErrorSummary}");
                }
            });
        }

        private static void AddPerformanceSummary(ZipArchive archive)
        {
            AddTextEntry(archive, "performance-timings.txt", writer =>
            {
                writer.WriteLine("Recent performance timings");
                foreach (var timing in DiagnosticTelemetry.GetRecentTimings())
                {
                    writer.WriteLine($"{timing.TimestampUtc:O}; stage={timing.Stage}; elapsedMs={timing.ElapsedMs}; detail={timing.Detail}");
                }
            });
        }

        private static void AddCacheSummary(ZipArchive archive)
        {
            AddTextEntry(archive, "cache-summary.txt", writer =>
            {
                try
                {
                    writer.Write(CacheMaintenanceService.FormatStats(new CacheMaintenanceService().GetStats()));
                }
                catch (Exception ex)
                {
                    writer.WriteLine($"cacheSummaryError={Sanitize(ex.Message)}");
                }
            });
        }

        private static void AddClipboardSummary(ZipArchive archive)
        {
            AddTextEntry(archive, "clipboard-parse-summary.txt", writer =>
            {
                writer.WriteLine(DiagnosticTelemetry.GetClipboardSummary());
                writer.WriteLine("Raw clipboard contents are not included.");
            });
        }

        private static void AddResolverFailureSummary(ZipArchive archive)
        {
            AddTextEntry(archive, "recent-resolver-failures.txt", writer =>
            {
                var failures = DiagnosticTelemetry.GetProviderHealthSnapshots()
                    .Where(x => x.LastFailureUtc.HasValue || x.RecentFailureCount > 0)
                    .ToList();
                if (failures.Count == 0)
                {
                    writer.WriteLine("No provider failures recorded in this app session.");
                    return;
                }

                foreach (var failure in failures)
                {
                    writer.WriteLine($"{failure.ProviderName}: failures={failure.RecentFailureCount}; lastFailureUtc={failure.LastFailureUtc:O}; lastError={failure.LastErrorSummary}");
                }
            });
        }

        private static void AddCynoSignalSummary(ZipArchive archive)
        {
            AddTextEntry(archive, "cyno-signal-summary.txt", writer =>
            {
                var summaries = DiagnosticTelemetry.GetRecentCynoSignalSummaries();
                if (summaries.Count == 0)
                {
                    writer.WriteLine("No Cyno Signal detail-panel analyses recorded in this app session.");
                    return;
                }

                foreach (var summary in summaries)
                {
                    writer.WriteLine(summary);
                }
            });
        }

        private static void AddIgnoreSummary(ZipArchive archive)
        {
            AddTextEntry(archive, "typed-ignore-list.txt", writer =>
            {
                var state = new IgnoreAllianceListService().LoadState();
                writer.WriteLine("Typed ignore list entries");
                foreach (var entry in state.Entries.OrderBy(x => x.Type).ThenBy(x => x.Id))
                {
                    writer.WriteLine($"type={entry.Type}; id={entry.Id}; name={Sanitize(entry.DisplayName)}; source={Sanitize(entry.Source)}; updatedAtUtc={entry.UpdatedAtUtc}");
                }

                writer.WriteLine();
                writer.WriteLine($"ignoreSuppressionCount={DiagnosticTelemetry.GetIgnoreSuppressionCount()}");
                writer.WriteLine("recent suppression samples");
                foreach (var sample in DiagnosticTelemetry.GetRecentIgnoreSuppressionSamples())
                {
                    writer.WriteLine(sample);
                }
            });
        }

        private static void AddTextEntry(ZipArchive archive, string path, Action<StreamWriter> write)
        {
            var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
            using var stream = entry.Open();
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            write(writer);
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
                    .GetFiles(diagnosticsDirectory, "*diagnostics-*.zip", SearchOption.TopDirectoryOnly)
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
