using System.Collections.Generic;
using System.Linq;

namespace PitmastersGrill.Services
{
    public sealed record SignalReferenceEntry(
        string Label,
        string PlainLanguageMeaning,
        string ImportantCaveat);

    public static class SignalReferenceCatalog
    {
        private static readonly IReadOnlyList<SignalReferenceEntry> EntriesValue =
        [
            new(
                "Confirmed covert",
                "PMG found recent public killmail-derived module evidence showing a Covert Cynosural Field Generator I.",
                "This does not prove the pilot is currently in a covert cyno ship or currently fit that way."),

            new(
                "Confirmed normal",
                "PMG found recent public killmail-derived module evidence showing a regular Cynosural Field Generator I. In code this is the normal cyno type; in one UI context it may also display as hard.",
                "This does not prove the pilot is currently carrying or using a normal cyno."),

            new(
                "Possible",
                "PMG has meaningful evidence, but not enough for recent confirmed module evidence. This can come from stale or unknown-age module evidence or combined weaker signals.",
                "Treat this as a review prompt, not a live-certainty claim."),

            new(
                "Inferred",
                "Weak evidence exists, usually from cyno-capable hull observations, public activity, or related context, but no confirmed cyno module was found.",
                "Inferred signals are intentionally weaker than confirmed module evidence."),

            new(
                "Bait",
                "PMG found public loss evidence where an industrial cyno and a tackle module, such as a warp scrambler or warp disruptor, appeared together.",
                "This suggests possible industrial cyno bait, but it is still evidence-based and not proof of intent.")
        ];

        public static IReadOnlyList<SignalReferenceEntry> Entries => EntriesValue;

        public static string RequiredCaveat =>
            "PMG signals are public historical evidence summaries. They do not prove that a pilot is currently in that ship, currently fit that way, uncloaked, on-grid, or actively baiting. PMG summarizes public evidence so users can make better decisions; it does not make live-certainty claims.";

        public static string BuildPlainTextReference()
        {
            var lines = new List<string>
            {
                "PMG Signal Reference",
                string.Empty,
                RequiredCaveat,
                string.Empty
            };

            foreach (var entry in Entries)
            {
                lines.Add(entry.Label);
                lines.Add(entry.PlainLanguageMeaning);
                lines.Add(entry.ImportantCaveat);
                lines.Add(string.Empty);
            }

            return string.Join(System.Environment.NewLine, lines).TrimEnd();
        }

        public static SignalReferenceEntry? FindByLabel(string label)
        {
            return Entries.FirstOrDefault(entry =>
                string.Equals(entry.Label, label, System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
