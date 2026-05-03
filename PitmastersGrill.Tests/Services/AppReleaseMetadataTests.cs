using System.Text.RegularExpressions;
using PitmastersGrill.Services;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public class AppReleaseMetadataTests
    {
        [Fact]
        public void VersionText_IsStableSemanticVersionText()
        {
            Assert.Matches(new Regex("^\\d+\\.\\d+\\.\\d+$"), AppReleaseMetadata.VersionText);
        }

        [Fact]
        public void ReleaseLabel_UsesCentralVersionText()
        {
            Assert.Equal($"{AppReleaseMetadata.ReleaseStage}-v{AppReleaseMetadata.VersionText}", AppReleaseMetadata.ReleaseLabel);
        }

        [Fact]
        public void GenericUserAgent_UsesCentralVersionText()
        {
            Assert.Equal($"{AppReleaseMetadata.ProductUserAgentName}/{AppReleaseMetadata.VersionText}", AppReleaseMetadata.GenericUserAgent);
        }

        [Fact]
        public void PanelModeText_UsesCentralVersionText()
        {
            Assert.Equal($"Panel Mode is always enabled for PMG {AppReleaseMetadata.VersionText}.", AppReleaseMetadata.PanelModeAlwaysEnabledText);
        }
    }
}
