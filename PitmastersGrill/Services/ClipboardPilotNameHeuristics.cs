using System;

namespace PitmastersGrill.Services
{
    internal static class ClipboardPilotNameHeuristics
    {
        public const int MinimumPilotNameLength = 3;
        public const int MaximumPilotNameLength = 37;

        private static readonly string[] CodeOrMarkupStartSignals =
        {
            "using ",
            "namespace ",
            "public class ",
            "private class ",
            "protected class ",
            "internal class ",
            "public sealed class ",
            "public static ",
            "private readonly ",
            "private static ",
            "return ",
            "var ",
            "new ",
            "async ",
            "await ",
            "try ",
            "try{",
            "catch ",
            "catch(",
            "finally ",
            "finally{",
            "throw ",
            "if (",
            "else ",
            "else{",
            "switch ",
            "foreach ",
            "for (",
            "while ",
            "<Window",
            "<Grid",
            "<TextBlock",
            "<DataGrid",
            "<Project",
            "</",
            "<?xml",
            "function ",
            "const ",
            "let ",
            "import ",
            "export ",
            "class ",
            "def ",
            "Traceback",
            "Exception:",
            "git diff",
            "@@",
            "```"
        };

        public static bool IsPlausiblePilotName(string? rawLine)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                return false;
            }

            var line = rawLine.Trim();

            if (line.Length < MinimumPilotNameLength || line.Length > MaximumPilotNameLength)
            {
                return false;
            }

            if (StartsOrEndsWithSeparator(line))
            {
                return false;
            }

            if (ContainsRepeatedSeparators(line))
            {
                return false;
            }

            var hasLetter = false;

            foreach (var ch in line)
            {
                if (char.IsLetter(ch))
                {
                    hasLetter = true;
                    continue;
                }

                if (char.IsDigit(ch))
                {
                    continue;
                }

                if (ch == ' ' || ch == '-' || ch == '\'' || ch == '.')
                {
                    continue;
                }

                return false;
            }

            return hasLetter;
        }

        public static bool ContainsCodeOrMarkupSignal(string? rawLine)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                return false;
            }

            var line = rawLine.Trim();

            if (string.Equals(line, "try", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(line, "catch", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(line, "finally", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(line, "else", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (line.Contains("{", StringComparison.Ordinal) ||
                line.Contains("}", StringComparison.Ordinal) ||
                line.Contains(";", StringComparison.Ordinal) ||
                line.Contains("=>", StringComparison.Ordinal) ||
                line.Contains("==", StringComparison.Ordinal) ||
                line.Contains("!=", StringComparison.Ordinal) ||
                line.Contains("::", StringComparison.Ordinal) ||
                line.Contains("//", StringComparison.Ordinal) ||
                line.Contains("/*", StringComparison.Ordinal) ||
                line.Contains("*/", StringComparison.Ordinal))
            {
                return true;
            }

            foreach (var signal in CodeOrMarkupStartSignals)
            {
                if (line.StartsWith(signal, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return line.Contains("</", StringComparison.OrdinalIgnoreCase) ||
                   line.Contains("<?xml", StringComparison.OrdinalIgnoreCase) ||
                   line.Contains("Traceback", StringComparison.OrdinalIgnoreCase) ||
                   line.Contains("Exception:", StringComparison.OrdinalIgnoreCase) ||
                   line.Contains("@@", StringComparison.OrdinalIgnoreCase) ||
                   line.Contains("```", StringComparison.OrdinalIgnoreCase);
        }

        private static bool StartsOrEndsWithSeparator(string line)
        {
            return line.StartsWith(" ", StringComparison.Ordinal) ||
                   line.EndsWith(" ", StringComparison.Ordinal) ||
                   line.StartsWith("-", StringComparison.Ordinal) ||
                   line.EndsWith("-", StringComparison.Ordinal) ||
                   line.StartsWith(".", StringComparison.Ordinal) ||
                   line.EndsWith(".", StringComparison.Ordinal) ||
                   line.StartsWith("'", StringComparison.Ordinal) ||
                   line.EndsWith("'", StringComparison.Ordinal);
        }

        private static bool ContainsRepeatedSeparators(string line)
        {
            return line.Contains("  ", StringComparison.Ordinal) ||
                   line.Contains("--", StringComparison.Ordinal) ||
                   line.Contains("..", StringComparison.Ordinal) ||
                   line.Contains("''", StringComparison.Ordinal);
        }
    }
}
