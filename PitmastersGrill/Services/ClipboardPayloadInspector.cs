using System;
using System.Collections.Generic;
using System.Linq;

namespace PitmastersGrill.Services
{
    public sealed class ClipboardPayloadInspector
    {
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

            var nonEmptyLines = rawClipboardText
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (nonEmptyLines.Count < 2)
            {
                return ClipboardPayloadInspectionResult.Reject("Clipboard did not contain enough non-empty lines.");
            }

            var promptLikeCount = 0;
            var commandLikeCount = 0;
            var pathLikeCount = 0;
            var shellOperatorCount = 0;

            foreach (var line in nonEmptyLines)
            {
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
            }

            if (promptLikeCount > 0)
            {
                return ClipboardPayloadInspectionResult.Reject("Clipboard looked like shell prompt content.");
            }

            if (commandLikeCount > 0 && nonEmptyLines.Count <= 4)
            {
                return ClipboardPayloadInspectionResult.Reject("Clipboard looked like command content.");
            }

            var suspiciousLineCount = commandLikeCount + pathLikeCount + shellOperatorCount;

            if (suspiciousLineCount >= Math.Max(1, nonEmptyLines.Count / 2))
            {
                return ClipboardPayloadInspectionResult.Reject("Clipboard contained too many non-local command or path signals.");
            }

            return ClipboardPayloadInspectionResult.Accept();
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

        public static ClipboardPayloadInspectionResult Accept()
        {
            return new ClipboardPayloadInspectionResult
            {
                IsPlausibleLocalList = true
            };
        }

        public static ClipboardPayloadInspectionResult Reject(string reason)
        {
            return new ClipboardPayloadInspectionResult
            {
                IsPlausibleLocalList = false,
                IgnoreReason = reason ?? string.Empty
            };
        }
    }
}