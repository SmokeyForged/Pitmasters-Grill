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
                return ClipboardProcessResult.Ignored(inspection.IgnoreReason);
            }

            var parsedNames = _parser.Parse(rawClipboardText);

            if (parsedNames.Count < 2)
            {
                return ClipboardProcessResult.Ignored("Clipboard did not contain enough parsed pilot names.");
            }

            return ClipboardProcessResult.Accepted(rawClipboardText, parsedNames);
        }
    }

    public class ClipboardProcessResult
    {
        public bool ShouldProcess { get; private set; }
        public string AcceptedClipboardText { get; private set; } = string.Empty;
        public List<string> ParsedNames { get; private set; } = new();
        public string IgnoreReason { get; private set; } = string.Empty;

        public static ClipboardProcessResult Ignored(string? reason = null)
        {
            return new ClipboardProcessResult
            {
                ShouldProcess = false,
                IgnoreReason = reason ?? string.Empty
            };
        }

        public static ClipboardProcessResult Accepted(string acceptedClipboardText, List<string> parsedNames)
        {
            return new ClipboardProcessResult
            {
                ShouldProcess = true,
                AcceptedClipboardText = acceptedClipboardText ?? string.Empty,
                ParsedNames = parsedNames ?? new List<string>()
            };
        }
    }
}
