using System;
using System.Collections.Generic;

namespace PitmastersGrill.Services
{
    public class ClipboardIngestService
    {
        private readonly LocalListParser _parser;
        private readonly ClipboardPayloadInspector _payloadInspector;

        public ClipboardIngestService(
            LocalListParser parser,
            ClipboardPayloadInspector payloadInspector)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _payloadInspector = payloadInspector ?? throw new ArgumentNullException(nameof(payloadInspector));
        }

        public ClipboardProcessResult Process(string? rawClipboardText, string? lastProcessedClipboardText)
        {
            if (string.IsNullOrWhiteSpace(rawClipboardText))
            {
                return ClipboardProcessResult.Ignored("Clipboard was empty.");
            }

            if (rawClipboardText == lastProcessedClipboardText)
            {
                return ClipboardProcessResult.Ignored("Clipboard matched the last processed payload.");
            }

            var inspection = _payloadInspector.Inspect(rawClipboardText);

            if (!inspection.IsPlausibleLocalList)
            {
                return ClipboardProcessResult.Ignored(
                    inspection.IgnoreReason,
                    inspection.CharacterCount,
                    inspection.NonEmptyLineCount,
                    inspection.PlausibleNameCount,
                    inspection.SuspiciousLineCount);
            }

            var parsedNames = _parser.Parse(rawClipboardText);

            if (parsedNames.Count < 2)
            {
                return ClipboardProcessResult.Ignored(
                    "Clipboard did not contain enough parsed pilot names after filtering.",
                    inspection.CharacterCount,
                    inspection.NonEmptyLineCount,
                    inspection.PlausibleNameCount,
                    inspection.SuspiciousLineCount);
            }

            return ClipboardProcessResult.Accepted(
                rawClipboardText,
                parsedNames,
                inspection.CharacterCount,
                inspection.NonEmptyLineCount,
                inspection.PlausibleNameCount,
                inspection.SuspiciousLineCount);
        }
    }

    public class ClipboardProcessResult
    {
        public bool ShouldProcess { get; private set; }
        public string AcceptedClipboardText { get; private set; } = string.Empty;
        public List<string> ParsedNames { get; private set; } = new();
        public string IgnoreReason { get; private set; } = string.Empty;
        public int CharacterCount { get; private set; }
        public int NonEmptyLineCount { get; private set; }
        public int PlausibleNameCount { get; private set; }
        public int SuspiciousLineCount { get; private set; }

        public static ClipboardProcessResult Ignored(
            string? reason = null,
            int characterCount = 0,
            int nonEmptyLineCount = 0,
            int plausibleNameCount = 0,
            int suspiciousLineCount = 0)
        {
            return new ClipboardProcessResult
            {
                ShouldProcess = false,
                IgnoreReason = reason ?? string.Empty,
                CharacterCount = characterCount,
                NonEmptyLineCount = nonEmptyLineCount,
                PlausibleNameCount = plausibleNameCount,
                SuspiciousLineCount = suspiciousLineCount
            };
        }

        public static ClipboardProcessResult Accepted(
            string acceptedClipboardText,
            List<string> parsedNames,
            int characterCount,
            int nonEmptyLineCount,
            int plausibleNameCount,
            int suspiciousLineCount)
        {
            return new ClipboardProcessResult
            {
                ShouldProcess = true,
                AcceptedClipboardText = acceptedClipboardText ?? string.Empty,
                ParsedNames = parsedNames ?? new List<string>(),
                CharacterCount = characterCount,
                NonEmptyLineCount = nonEmptyLineCount,
                PlausibleNameCount = plausibleNameCount,
                SuspiciousLineCount = suspiciousLineCount
            };
        }
    }
}
