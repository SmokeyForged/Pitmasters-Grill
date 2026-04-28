using System;

namespace PitmastersGrill.Models
{
    public sealed class TypedIgnoreEntry
    {
        public long Id { get; set; }
        public IgnoreEntryType Type { get; set; } = IgnoreEntryType.Unknown;
        public string ResolvedName { get; set; } = "Unresolved";
        public string Source { get; set; } = "";
        public string CreatedAtUtc { get; set; } = "";
        public string UpdatedAtUtc { get; set; } = "";

        public string DisplayName => string.IsNullOrWhiteSpace(ResolvedName) ? "Unresolved" : ResolvedName;

        public void Touch(string source)
        {
            var now = DateTime.UtcNow.ToString("O");
            if (string.IsNullOrWhiteSpace(CreatedAtUtc))
            {
                CreatedAtUtc = now;
            }

            UpdatedAtUtc = now;
            Source = source ?? string.Empty;
        }
    }
}
