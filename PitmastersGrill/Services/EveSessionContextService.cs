using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersGrill.Services
{
    public sealed class EveSessionContextService
    {
        private static readonly Regex EveTitleRegex = new(@"^\s*EVE(?:\s+Online)?\s*-\s*(?<character>.+?)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ListenerRegex = new(@"^\s*Listener:\s*(?<listener>.+?)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SystemChangeRegex = new(@"^[\uFEFF\s]*\[(?<timestamp>[^\]]+)\]\s*EVE System\s*>\s*Channel changed to Local\s*:\s*(?<system>.+?)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public Task<EveSessionContext> CaptureAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() => CaptureCore(cancellationToken), cancellationToken);
        }

        private EveSessionContext CaptureCore(CancellationToken cancellationToken)
        {
            var foregroundEvidence = CaptureForegroundEvidence();
            var chatLogDirectory = ResolveChatLogDirectory();
            if (string.IsNullOrWhiteSpace(chatLogDirectory) || !Directory.Exists(chatLogDirectory))
            {
                AppLogger.UiInfo("EVE chat log directory not found for session context.");
                return BuildContextWithoutLogs(foregroundEvidence, "No matching Local log found");
            }

            var candidateLogs = EnumerateCandidateLocalLogs(chatLogDirectory);
            AppLogger.UiInfo($"EVE session context candidate Local logs scanned={candidateLogs.Count} directoryFound=true");

            var candidates = new List<LocalLogContextCandidate>();
            foreach (var logPath in candidateLogs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var parsed = TryParseLocalLog(logPath);
                if (parsed != null)
                {
                    candidates.Add(parsed);
                }
            }

            var selected = SelectBestCandidate(foregroundEvidence, candidates);
            if (selected == null)
            {
                return BuildContextWithoutLogs(
                    foregroundEvidence,
                    candidates.Count == 0 ? "No matching Local log found" : "No Local system change found");
            }

            var confidence = selected.IsCharacterMatched
                ? "High: Matched focused EVE client to Local log listener"
                : foregroundEvidence.HasCharacterName
                    ? "Low: Focused EVE client title detected, but no matching Local log listener"
                    : "Medium: Used most recent Local log";

            var evidenceSource = selected.IsCharacterMatched
                ? "Foreground EVE client title + Local chat log"
                : selected.ListenerFound
                    ? "Local chat log listener + system change"
                    : "Local chat log system change";

            var context = new EveSessionContext
            {
                CharacterName = selected.IsCharacterMatched
                    ? foregroundEvidence.CharacterName
                    : !string.IsNullOrWhiteSpace(selected.ListenerName)
                        ? selected.ListenerName
                        : foregroundEvidence.HasCharacterName
                            ? foregroundEvidence.CharacterName
                            : "Not detected",
                SolarSystemName = string.IsNullOrWhiteSpace(selected.SystemName) ? "Not detected" : selected.SystemName,
                EvidenceSource = evidenceSource,
                EvidenceTimestampUtc = selected.SystemTimestampUtc,
                Confidence = confidence,
                StatusMessage = "Context detected"
            };

            AppLogger.UiInfo(
                $"EVE session context selected. listenerFound={selected.ListenerFound} systemFound={selected.SystemFound} confidence='{context.Confidence}'");

            return context;
        }

        private EveSessionContext BuildContextWithoutLogs(ForegroundWindowEvidence foregroundEvidence, string sourceMessage)
        {
            if (foregroundEvidence.HasCharacterName)
            {
                return new EveSessionContext
                {
                    CharacterName = foregroundEvidence.CharacterName,
                    SolarSystemName = "Not detected",
                    EvidenceSource = "Foreground EVE client title",
                    EvidenceTimestampUtc = foregroundEvidence.CapturedAtUtc,
                    Confidence = "Low: Partial evidence only",
                    StatusMessage = sourceMessage
                };
            }

            return new EveSessionContext
            {
                CharacterName = "Not detected",
                SolarSystemName = "Not detected",
                EvidenceSource = sourceMessage,
                EvidenceTimestampUtc = null,
                Confidence = "None",
                StatusMessage = "Unable to infer EVE context"
            };
        }

        private static string ResolveChatLogDirectory()
        {
            foreach (var path in GetChatLogDirectoryCandidates())
            {
                if (Directory.Exists(path))
                {
                    AppLogger.UiInfo($"EVE chat log directory found. path='{path}'");
                    return path;
                }
            }

            return string.Empty;
        }

        private static IEnumerable<string> GetChatLogDirectoryCandidates()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            static IEnumerable<string> Build(string? documentsPath)
            {
                if (string.IsNullOrWhiteSpace(documentsPath))
                {
                    yield break;
                }

                yield return Path.Combine(documentsPath, "EVE", "logs", "Chatlogs");
            }

            foreach (var candidate in Build(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)))
            {
                if (seen.Add(candidate))
                {
                    yield return candidate;
                }
            }

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (var candidate in Build(Path.Combine(userProfile, "OneDrive", "Documents")))
            {
                if (seen.Add(candidate))
                {
                    yield return candidate;
                }
            }

            var oneDriveConsumer = Environment.GetEnvironmentVariable("OneDriveConsumer");
            foreach (var candidate in Build(string.IsNullOrWhiteSpace(oneDriveConsumer) ? null : Path.Combine(oneDriveConsumer, "Documents")))
            {
                if (seen.Add(candidate))
                {
                    yield return candidate;
                }
            }

            var oneDriveCommercial = Environment.GetEnvironmentVariable("OneDriveCommercial");
            foreach (var candidate in Build(string.IsNullOrWhiteSpace(oneDriveCommercial) ? null : Path.Combine(oneDriveCommercial, "Documents")))
            {
                if (seen.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        private static List<string> EnumerateCandidateLocalLogs(string chatLogDirectory)
        {
            try
            {
                return Directory.EnumerateFiles(chatLogDirectory, "*.txt", SearchOption.TopDirectoryOnly)
                    .Where(path => Path.GetFileName(path).Contains("Local", StringComparison.OrdinalIgnoreCase))
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .Take(8)
                    .Select(file => file.FullName)
                    .ToList();
            }
            catch (Exception ex)
            {
                AppLogger.UiWarn($"Failed to enumerate EVE Local chat logs. message={ex.Message}");
                return new List<string>();
            }
        }

        private static LocalLogContextCandidate? TryParseLocalLog(string logPath)
        {
            try
            {
                using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

                string listenerName = string.Empty;
                DateTime? latestSystemTimestampUtc = null;
                string latestSystemName = string.Empty;
                var listenerFound = false;
                var systemFound = false;

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    line = line.TrimStart('\uFEFF');

                    if (!listenerFound)
                    {
                        var listenerMatch = ListenerRegex.Match(line);
                        if (listenerMatch.Success)
                        {
                            listenerName = listenerMatch.Groups["listener"].Value.Trim();
                            listenerFound = !string.IsNullOrWhiteSpace(listenerName);
                        }
                    }

                    var systemMatch = SystemChangeRegex.Match(line);
                    if (!systemMatch.Success)
                    {
                        continue;
                    }

                    var systemName = systemMatch.Groups["system"].Value.Trim();
                    if (string.IsNullOrWhiteSpace(systemName))
                    {
                        continue;
                    }

                    var timestamp = ParseLogTimestamp(systemMatch.Groups["timestamp"].Value);
                    if (!timestamp.HasValue)
                    {
                        continue;
                    }

                    latestSystemTimestampUtc = timestamp.Value;
                    latestSystemName = systemName;
                    systemFound = true;
                }

                AppLogger.UiDebug(
                    $"EVE Local log parsed. file='{Path.GetFileName(logPath)}' listenerFound={listenerFound} systemFound={systemFound}");

                if (!listenerFound && !systemFound)
                {
                    return null;
                }

                return new LocalLogContextCandidate(
                    logPath,
                    listenerName,
                    listenerFound,
                    latestSystemName,
                    latestSystemTimestampUtc,
                    systemFound);
            }
            catch (Exception ex)
            {
                AppLogger.UiWarn($"Skipped EVE Local chat log during session-context parse. file='{Path.GetFileName(logPath)}' message={ex.Message}");
                return null;
            }
        }

        private static DateTime? ParseLogTimestamp(string rawTimestamp)
        {
            if (string.IsNullOrWhiteSpace(rawTimestamp))
            {
                return null;
            }

            if (!DateTime.TryParse(rawTimestamp.Trim(), out var parsed))
            {
                return null;
            }

            return parsed.Kind == DateTimeKind.Utc
                ? parsed
                : DateTime.SpecifyKind(parsed, DateTimeKind.Local).ToUniversalTime();
        }

        private static SelectedLocalLogCandidate? SelectBestCandidate(
            ForegroundWindowEvidence foregroundEvidence,
            IReadOnlyList<LocalLogContextCandidate> candidates)
        {
            if (foregroundEvidence.HasCharacterName)
            {
                var matched = candidates
                    .Where(candidate =>
                        candidate.ListenerFound &&
                        !string.IsNullOrWhiteSpace(candidate.ListenerName) &&
                        string.Equals(candidate.ListenerName, foregroundEvidence.CharacterName, StringComparison.OrdinalIgnoreCase) &&
                        candidate.SystemFound &&
                        candidate.SystemTimestampUtc.HasValue)
                    .OrderByDescending(candidate => candidate.SystemTimestampUtc)
                    .FirstOrDefault();

                if (matched != null)
                {
                    return new SelectedLocalLogCandidate(matched, true);
                }
            }

            var fallback = candidates
                .Where(candidate => candidate.SystemFound && candidate.SystemTimestampUtc.HasValue)
                .OrderByDescending(candidate => candidate.SystemTimestampUtc)
                .FirstOrDefault();

            return fallback == null
                ? null
                : new SelectedLocalLogCandidate(fallback, false);
        }

        private static ForegroundWindowEvidence CaptureForegroundEvidence()
        {
            try
            {
                var hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                {
                    AppLogger.UiDebug("EVE session context foreground title capture failed. hwnd=0");
                    return ForegroundWindowEvidence.None();
                }

                var titleLength = GetWindowTextLength(hwnd);
                if (titleLength <= 0)
                {
                    AppLogger.UiDebug("EVE session context foreground title capture succeeded with empty title.");
                    return ForegroundWindowEvidence.None();
                }

                var buffer = new StringBuilder(titleLength + 1);
                _ = GetWindowText(hwnd, buffer, buffer.Capacity);
                var title = buffer.ToString().Trim();
                if (string.IsNullOrWhiteSpace(title))
                {
                    AppLogger.UiDebug("EVE session context foreground title capture returned whitespace.");
                    return ForegroundWindowEvidence.None();
                }

                var match = EveTitleRegex.Match(title);
                if (!match.Success)
                {
                    AppLogger.UiDebug("EVE session context foreground title captured but did not match EVE pattern.");
                    return ForegroundWindowEvidence.None();
                }

                var characterName = match.Groups["character"].Value.Trim();
                var hasCharacterName = !string.IsNullOrWhiteSpace(characterName);
                AppLogger.UiDebug($"EVE session context foreground title matchedEve={match.Success} parsedCharacter={hasCharacterName}");

                return hasCharacterName
                    ? new ForegroundWindowEvidence(characterName, DateTime.UtcNow, true)
                    : ForegroundWindowEvidence.None();
            }
            catch (Exception ex)
            {
                AppLogger.UiWarn($"EVE session context foreground title capture failed. message={ex.Message}");
                return ForegroundWindowEvidence.None();
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        private sealed record ForegroundWindowEvidence(string CharacterName, DateTime CapturedAtUtc, bool HasCharacterName)
        {
            public static ForegroundWindowEvidence None() => new(string.Empty, DateTime.UtcNow, false);
        }

        private sealed record LocalLogContextCandidate(
            string LogPath,
            string ListenerName,
            bool ListenerFound,
            string SystemName,
            DateTime? SystemTimestampUtc,
            bool SystemFound);

        private sealed record SelectedLocalLogCandidate(LocalLogContextCandidate Candidate, bool IsCharacterMatched)
        {
            public string ListenerName => Candidate.ListenerName;
            public bool ListenerFound => Candidate.ListenerFound;
            public string SystemName => Candidate.SystemName;
            public DateTime? SystemTimestampUtc => Candidate.SystemTimestampUtc;
            public bool SystemFound => Candidate.SystemFound;
        }
    }
}
