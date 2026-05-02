using PitmastersGrill.Services;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public class ClipboardPayloadInspectorTests
    {
        private readonly ClipboardPayloadInspector _inspector = new();

        [Fact]
        public void Inspect_AcceptsPlausiblePilotList()
        {
            var clipboardText = "Aura\nChribba\nThe Mittani";

            var result = _inspector.Inspect(clipboardText);

            Assert.True(result.IsPlausibleLocalList);
            Assert.Equal(3, result.NonEmptyLineCount);
            Assert.Equal(3, result.PlausibleNameCount);
            Assert.Equal(string.Empty, result.IgnoreReason);
        }

        [Fact]
        public void Inspect_RejectsShellPromptContent()
        {
            var clipboardText = "PS C:\\Users\\gregm> dotnet test\nPS C:\\Users\\gregm> git status";

            var result = _inspector.Inspect(clipboardText);

            Assert.False(result.IsPlausibleLocalList);
            Assert.Equal("Clipboard looked like shell prompt content.", result.IgnoreReason);
        }
    }
}
