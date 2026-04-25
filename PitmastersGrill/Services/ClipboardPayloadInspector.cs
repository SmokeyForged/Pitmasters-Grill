using System;
using System.Linq;

namespace PitmastersGrill.Services
{
    public sealed class ClipboardPayloadInspector
    {
        public const int MaximumClipboardCharacters = 80000;
        public const int MaximumNonEmptyLines = 1500;
        public const int MaximumSingleLineCharacters = 160;
        private const double MinimumPlausibleNameRatio = 0.70;

        private static readonly string[] CommandPrefixes =
        {
            "git ",
            "dotnet ",
            "pwsh ",
            "powershell ",
            "cmd ",
            "msbuild ",
            "npm ",
            "yarn ",
            "pnpm ",
            "winget ",
            "choco ",
            "ssh ",
            "scp ",
            "curl ",
            "wget ",
            "docker ",
            "kubectl ",
            "systemctl ",
            "sudo ",
            "cd ",
            "mkdir ",
            "rmdir ",
            "del ",
            "copy ",
            "move ",
            "type ",
            "cat ",
            "ls ",
            "dir ",
            "start ",
            "explorer ",
            "code "
        };

        public ClipboardPayloadInspectionResult Inspect(string? rawClipboardText)
        {
            if (string.IsNullOrWhiteSpace(rawClipboardText))
            {
                return ClipboardPayloadInspectionResult.Reject("Clipboard was empty.");
            }

            if (rawClipboardText.Length > MaximumClipboardCharacters)
            {
                return ClipboardPayloadInspectionResult.Reject(
                    "Clipboard was too large to treat as a local list.",
                    rawClipboardText.Length,
                    0,
                    0,
                    0);
            }

            var nonEmptyLines = rawClipboardText
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (nonEmptyLines.Count < 2)
            {
                return ClipboardPayloadInspectionResult.Reject(
                    "Clipboard did not contain enough non-empty lines.",
                    rawClipboardText.Length,
                    nonEmptyLines.Count,
                    0,
                    0);
            }

            if (nonEmptyLines.Count > MaximumNonEmptyLines)
            {
                return ClipboardPayloadInspectionResult.Reject(
                    "Clipboard contained too many lines to treat as a local list.",
                    rawClipboardText.Length,
                    nonEmptyLines.Count,
                    0,
                    0);
            }

            var promptLikeCount = 0;
            var commandLikeCount = 0;
            var pathLikeCount = 0;
            var shellOperatorCount = 0;
            var codeOrMarkupCount = 0;
            var longLineCount = 0;
            var plausibleNameCount = 0;

            foreach (var line in nonEmptyLines)
            {
                if (line.Length > MaximumSingleLineCharacters)
                {
                    longLineCount++;
                }

                if (ClipboardPilotNameHeuristics.IsPlausiblePilotName(line))
                {
                    plausibleNameCount++;
                }

                if (LooksLikePowerShellPrompt(line) || LooksLikeShellPrompt(line))
                {
                    promptLikeCount++;
                    continue;
                }

                if (LooksLikeCommand(line))
                {
                    commandLikeCount++;
                }

                if (LooksLikeFilesystemPath(line))
                {
                    pathLikeCount++;
                }

                if (LooksLikeShellExpression(line))
                {
                    shellOperatorCount++;
                }

                if (ClipboardPilotNameHeuristics.ContainsCodeOrMarkupSignal(line))
                {
                    codeOrMarkupCount++;
                }
            }

            var suspiciousLineCount = promptLikeCount +
                                      commandLikeCount +
                                      pathLikeCount +
                                      shellOperatorCount +
                                      codeOrMarkupCount +
                                      longLineCount;

            if (promptLikeCount > 0)
            {
                return ClipboardPayloadInspectionResult.Reject(
                    "Clipboard looked like shell prompt content.",
                    rawClipboardText.Length,
                    nonEmptyLines.Count,
                    plausibleNameCount,
                    suspiciousLineCount);
            }

            if (codeOrMarkupCount > 0)
            {
                return ClipboardPayloadInspectionResult.Reject(
                    "Clipboard looked like code, markup, or stack-trace content.",
                    rawClipboardText.Length,
                    nonEmptyLines.Count,
                    plausibleNameCount,
                    suspiciousLineCount);
            }

            if (commandLikeCount > 0 && nonEmptyLines.Count <= 4)
            {
                return ClipboardPayloadInspectionResult.Reject(
                    "Clipboard looked like command content.",
                    rawClipboardText.Length,
                    nonEmptyLines.Count,
                    plausibleNameCount,
                    suspiciousLineCount);
            }

            if (longLineCount > 0)
            {
                return ClipboardPayloadInspectionResult.Reject(
                    "Clipboard contained lines too long to treat as pilot names.",
                    rawClipboardText.Length,
                    nonEmptyLines.Count,
                    plausibleNameCount,
                    suspiciousLineCount);
            }

            if (suspiciousLineCount >= Math.Max(1, nonEmptyLines.Count / 3))
            {
                return ClipboardPayloadInspectionResult.Reject(
                    "Clipboard contained too many non-local command, code, or path signals.",
                    rawClipboardText.Length,
                    nonEmptyLines.Count,
                    plausibleNameCount,
                    suspiciousLineCount);
            }

            if (plausibleNameCount < 2)
            {
                return ClipboardPayloadInspectionResult.Reject(
                    "Clipboard did not contain enough plausible pilot names.",
                    rawClipboardText.Length,
                    nonEmptyLines.Count,
                    plausibleNameCount,
                    suspiciousLineCount);
            }

            if (nonEmptyLines.Count <= 5 && plausibleNameCount != nonEmptyLines.Count)
            {
                return ClipboardPayloadInspectionResult.Reject(
                    "Small clipboard payload contained non-pilot-name lines.",
                    rawClipboardText.Length,
                    nonEmptyLines.Count,
                    plausibleNameCount,
                    suspiciousLineCount);
            }

            var plausibleRatio = plausibleNameCount / (double)nonEmptyLines.Count;

            if (plausibleRatio < MinimumPlausibleNameRatio)
            {
                return ClipboardPayloadInspectionResult.Reject(
                    "Clipboard did not look enough like an EVE local pilot list.",
                    rawClipboardText.Length,
                    nonEmptyLines.Count,
                    plausibleNameCount,
                    suspiciousLineCount);
            }

            return ClipboardPayloadInspectionResult.Accept(
                rawClipboardText.Length,
                nonEmptyLines.Count,
                plausibleNameCount,
                suspiciousLineCount);
        }

        private static bool LooksLikePowerShellPrompt(string line)
        {
            return line.StartsWith("PS ", StringComparison.OrdinalIgnoreCase)
                   && line.Contains(@":\");
        }

        private static bool LooksLikeShellPrompt(string line)
        {
            return line.StartsWith("$ ", StringComparison.Ordinal)
                   || line.StartsWith("# ", StringComparison.Ordinal)
                   || line.StartsWith("> ", StringComparison.Ordinal);
        }

        private static bool LooksLikeCommand(string line)
        {
            foreach (var prefix in CommandPrefixes)
            {
                if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool LooksLikeFilesystemPath(string line)
        {
            if (line.Contains(@":\"))
            {
                return true;
            }

            if (line.StartsWith(@"\", StringComparison.Ordinal))
            {
                return true;
            }

            if (line.StartsWith("/", StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        private static bool LooksLikeShellExpression(string line)
        {
            return line.Contains("&&", StringComparison.Ordinal)
                   || line.Contains("||", StringComparison.Ordinal)
                   || line.Contains(" > ", StringComparison.Ordinal)
                   || line.Contains(" < ", StringComparison.Ordinal)
                   || line.Contains(" --", StringComparison.Ordinal)
                   || line.Contains(" /", StringComparison.Ordinal);
        }
    }

    public sealed class ClipboardPayloadInspectionResult
    {
        public bool IsPlausibleLocalList { get; private set; }
        public string IgnoreReason { get; private set; } = string.Empty;
        public int CharacterCount { get; private set; }
        public int NonEmptyLineCount { get; private set; }
        public int PlausibleNameCount { get; private set; }
        public int SuspiciousLineCount { get; private set; }

        public static ClipboardPayloadInspectionResult Accept(
            int characterCount,
            int nonEmptyLineCount,
            int plausibleNameCount,
            int suspiciousLineCount)
        {
            return new ClipboardPayloadInspectionResult
            {
                IsPlausibleLocalList = true,
                CharacterCount = characterCount,
                NonEmptyLineCount = nonEmptyLineCount,
                PlausibleNameCount = plausibleNameCount,
                SuspiciousLineCount = suspiciousLineCount
            };
        }

        public static ClipboardPayloadInspectionResult Reject(
            string reason,
            int characterCount = 0,
            int nonEmptyLineCount = 0,
            int plausibleNameCount = 0,
            int suspiciousLineCount = 0)
        {
            return new ClipboardPayloadInspectionResult
            {
                IsPlausibleLocalList = false,
                IgnoreReason = reason ?? string.Empty,
                CharacterCount = characterCount,
                NonEmptyLineCount = nonEmptyLineCount,
                PlausibleNameCount = plausibleNameCount,
                SuspiciousLineCount = suspiciousLineCount
            };
        }
    }
}
