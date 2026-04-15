using System;
using System.Collections.Generic;

namespace PitmastersLittleGrill.Services
{
    public class LocalListParser
    {
        public List<string> Parse(string rawClipboardText)
        {
            var seen = new HashSet<string>();
            var results = new List<string>();

            if (string.IsNullOrWhiteSpace(rawClipboardText))
            {
                return results;
            }

            var lines = rawClipboardText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                if (seen.Add(trimmed))
                {
                    results.Add(trimmed);
                }
            }

            return results;
        }
    }
}