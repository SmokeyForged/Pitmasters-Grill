using PitmastersGrill.Services;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class SignalReferenceCatalogTests
    {
        [Fact]
        public void Entries_IncludeExpectedSignalLabels()
        {
            var labels = SignalReferenceCatalog.Entries.Select(entry => entry.Label).ToList();

            Assert.Contains("Confirmed covert", labels);
            Assert.Contains("Confirmed normal", labels);
            Assert.Contains("Possible", labels);
            Assert.Contains("Inferred", labels);
            Assert.Contains("Bait", labels);
        }

        [Fact]
        public void RequiredCaveat_DoesNotClaimLiveCertainty()
        {
            Assert.Contains("public historical evidence summaries", SignalReferenceCatalog.RequiredCaveat);
            Assert.Contains("does not make live-certainty claims", SignalReferenceCatalog.RequiredCaveat);
        }

        [Fact]
        public void FindByLabel_IsCaseInsensitive()
        {
            var entry = SignalReferenceCatalog.FindByLabel("confirmed covert");

            Assert.NotNull(entry);
            Assert.Contains("Covert Cynosural Field Generator I", entry!.PlainLanguageMeaning);
        }
    }
}
