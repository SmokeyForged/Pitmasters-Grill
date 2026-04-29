using PitmastersGrill.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace PitmastersGrill.Providers
{
    public class KillmailDayArchiveProvider
    {
        private readonly HttpClient _httpClient;

        public KillmailDayArchiveProvider()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PitmastersGrill/0.8.x");
        }

        public async Task<KillmailDayArchiveDownloadResult> DownloadDayArchiveAsync(
            string dayUtc,
            CancellationToken cancellationToken = default)
        {
            if (!DateTime.TryParseExact(
                dayUtc,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDay))
            {
                return new KillmailDayArchiveDownloadResult
                {
                    Success = false,
                    Error = $"Invalid day format: {dayUtc}"
                };
            }

            KillmailPaths.PurgeArchiveCacheBestEffort(dayUtc);

            var archiveUrl =
                $"https://data.everef.net/killmails/{parsedDay:yyyy}/killmails-{parsedDay:yyyy-MM-dd}.tar.bz2";

            var archivePath = KillmailPaths.GetKillmailArchivePath(dayUtc);
            var downloadStopwatch = Stopwatch.StartNew();

            DebugTraceWriter.WriteLine(
                $"killmail archive download start: day={dayUtc}, url={archiveUrl}");

            using var response = await _httpClient.GetAsync(
                archiveUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                DebugTraceWriter.WriteLine(
                    $"killmail archive download failed: day={dayUtc}, status={(int)response.StatusCode}, elapsedMs={downloadStopwatch.ElapsedMilliseconds}");

                return new KillmailDayArchiveDownloadResult
                {
                    Success = false,
                    ArchiveUrl = archiveUrl,
                    ArchivePath = archivePath,
                    Error = $"Archive download failed with status {(int)response.StatusCode}"
                };
            }

            await using (var remoteStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var fileStream = File.Create(archivePath))
            {
                await remoteStream.CopyToAsync(fileStream, cancellationToken);
            }

            var archiveLengthBytes = 0L;

            try
            {
                var fileInfo = new FileInfo(archivePath);
                archiveLengthBytes = fileInfo.Exists ? fileInfo.Length : 0;
            }
            catch
            {
                archiveLengthBytes = 0;
            }

            KillmailPaths.PurgeArchiveCacheBestEffort(dayUtc);

            DebugTraceWriter.WriteLine(
                $"killmail archive download ok: day={dayUtc}, elapsedMs={downloadStopwatch.ElapsedMilliseconds}, bytes={archiveLengthBytes}");

            return new KillmailDayArchiveDownloadResult
            {
                Success = true,
                ArchiveUrl = archiveUrl,
                ArchivePath = archivePath,
                ArchiveEtag = response.Headers.ETag?.Tag ?? "",
                ArchiveLastModified = response.Content.Headers.LastModified?.UtcDateTime.ToString("o") ?? "",
                ArchiveLengthBytes = archiveLengthBytes
            };
        }

        public async Task<KillmailArchiveExtractResult> EnsureDayExtractedAsync(
            string dayUtc,
            string archivePath,
            CancellationToken cancellationToken = default)
        {
            KillmailPaths.PurgeArchiveCacheBestEffort(dayUtc);

            var extractRoot = KillmailPaths.GetKillmailExtractedDayDirectory(dayUtc);
            var markerPath = KillmailPaths.GetKillmailExtractedDayMarkerPath(dayUtc);

            if (File.Exists(markerPath))
            {
                var existingJsonCount = Directory.GetFiles(extractRoot, "*.json", SearchOption.AllDirectories).Length;

                DebugTraceWriter.WriteLine(
                    $"killmail archive extract reuse: day={dayUtc}, jsonFiles={existingJsonCount}");

                return new KillmailArchiveExtractResult
                {
                    Success = true,
                    ExtractRoot = extractRoot,
                    JsonFileCount = existingJsonCount,
                    ReusedExistingExtract = true
                };
            }

            if (Directory.Exists(extractRoot))
            {
                try
                {
                    Directory.Delete(extractRoot, true);
                }
                catch
                {
                    // best-effort cleanup
                }
            }

            Directory.CreateDirectory(extractRoot);

            var extractStopwatch = Stopwatch.StartNew();
            try
            {
                await ExtractArchiveAsync(archivePath, extractRoot, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var error =
                    $"Archive extraction failed. archive={Path.GetFileName(archivePath)}, target={Path.GetFileName(extractRoot)}, error={ex.Message}";

                DebugTraceWriter.WriteLine(
                    $"killmail archive extract failed: day={dayUtc}, archive={Path.GetFileName(archivePath)}, target={Path.GetFileName(extractRoot)}, error={ex}");

                return new KillmailArchiveExtractResult
                {
                    Success = false,
                    ExtractRoot = extractRoot,
                    Error = error
                };
            }

            extractStopwatch.Stop();

            var jsonFileCount = Directory.GetFiles(extractRoot, "*.json", SearchOption.AllDirectories).Length;

            await File.WriteAllTextAsync(markerPath, DateTime.UtcNow.ToString("o"), cancellationToken);

            KillmailPaths.PurgeArchiveCacheBestEffort(dayUtc);

            DebugTraceWriter.WriteLine(
                $"killmail archive extract ok: day={dayUtc}, elapsedMs={extractStopwatch.ElapsedMilliseconds}, jsonFiles={jsonFileCount}");

            return new KillmailArchiveExtractResult
            {
                Success = true,
                ExtractRoot = extractRoot,
                JsonFileCount = jsonFileCount,
                ExtractElapsedMs = extractStopwatch.ElapsedMilliseconds,
                ReusedExistingExtract = false
            };
        }

        public List<string> GetExtractedJsonRelativePaths(string dayUtc)
        {
            var extractRoot = KillmailPaths.GetKillmailExtractedDayDirectory(dayUtc);

            if (!Directory.Exists(extractRoot))
            {
                return new List<string>();
            }

            return Directory.GetFiles(extractRoot, "*.json", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(extractRoot, path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public Task<string> ReadExtractedJsonAsync(
            string dayUtc,
            string relativeJsonPath,
            CancellationToken cancellationToken = default)
        {
            var extractRoot = KillmailPaths.GetKillmailExtractedDayDirectory(dayUtc);
            var absolutePath = Path.Combine(extractRoot, relativeJsonPath);

            return File.ReadAllTextAsync(absolutePath, cancellationToken);
        }

        private static async Task ExtractArchiveAsync(
            string archivePath,
            string extractRoot,
            CancellationToken cancellationToken)
        {
            using var stream = File.OpenRead(archivePath);
            await using var reader = await ReaderFactory.OpenAsyncReader(stream, cancellationToken: cancellationToken);

            while (await reader.MoveToNextEntryAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (reader.Entry.IsDirectory)
                {
                    continue;
                }

                var entryKey = reader.Entry.Key ?? "";
                if (!IsSafeArchiveEntryPath(extractRoot, entryKey))
                {
                    throw new InvalidOperationException($"Unsafe archive entry path rejected: {entryKey}");
                }

                await reader.WriteEntryToDirectoryAsync(
                    extractRoot,
                    new ExtractionOptions
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    },
                    cancellationToken);
            }
        }

        private static bool IsSafeArchiveEntryPath(string extractRoot, string entryKey)
        {
            if (string.IsNullOrWhiteSpace(entryKey) ||
                Path.IsPathRooted(entryKey))
            {
                return false;
            }

            var root = Path.GetFullPath(extractRoot);
            var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var destination = Path.GetFullPath(Path.Combine(root, entryKey));
            return destination.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }
    }

    public class KillmailDayArchiveDownloadResult
    {
        public bool Success { get; set; }
        public string ArchiveUrl { get; set; } = "";
        public string ArchivePath { get; set; } = "";
        public string ArchiveEtag { get; set; } = "";
        public string ArchiveLastModified { get; set; } = "";
        public string Error { get; set; } = "";
        public long ArchiveLengthBytes { get; set; }
    }

    public class KillmailArchiveExtractResult
    {
        public bool Success { get; set; }
        public string ExtractRoot { get; set; } = "";
        public int JsonFileCount { get; set; }
        public long ExtractElapsedMs { get; set; }
        public bool ReusedExistingExtract { get; set; }
        public string Error { get; set; } = "";
    }
}
