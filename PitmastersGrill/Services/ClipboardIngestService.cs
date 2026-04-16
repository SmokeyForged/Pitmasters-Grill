using System;
using System.Collections.Generic;

namespace PitmastersGrill.Services
{
    public class ClipboardIngestService
    {
        private readonly LocalListParser _parser;

        public ClipboardIngestService(LocalListParser parser)
        {
            _parser = parser;
        }

        public ClipboardProcessResult Process(string rawClipboardText, string lastProcessedClipboardText)
        {
            if (string.IsNullOrWhiteSpace(rawClipboardText))
            {
                return ClipboardProcessResult.Ignored();
            }

            if (rawClipboardText == lastProcessedClipboardText)
            {
                return ClipboardProcessResult.Ignored();
            }

            var parsedNames = _parser.Parse(rawClipboardText);

            if (parsedNames.Count < 2)
            {
                return ClipboardProcessResult.Ignored();
            }

            return ClipboardProcessResult.Accepted(rawClipboardText, parsedNames);
        }
    }

    public class ClipboardProcessResult
    {
        public bool ShouldProcess { get; private set; }
        public string AcceptedClipboardText { get; private set; } = string.Empty;
        public List<string> ParsedNames { get; private set; } = new();

        public static ClipboardProcessResult Ignored()
        {
            return new ClipboardProcessResult
            {
                ShouldProcess = false
            };
        }

        public static ClipboardProcessResult Accepted(string acceptedClipboardText, List<string> parsedNames)
        {
            return new ClipboardProcessResult
            {
                ShouldProcess = true,
                AcceptedClipboardText = acceptedClipboardText,
                ParsedNames = parsedNames
            };
        }
    }
}