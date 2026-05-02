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

        [Fact]
        public void Inspect_RejectsLongLinesWhenNotStrongLocalList()
        {
            var longLine = new string('A', ClipboardPayloadInspector.MaximumSingleLineCharacters + 1);
            var clipboardText = $"Aura\n{longLine}";

            var result = _inspector.Inspect(clipboardText);

            Assert.False(result.IsPlausibleLocalList);
            Assert.Equal("Clipboard contained lines too long to treat as pilot names.", result.IgnoreReason);
        }

        [Fact]
        public void Inspect_RejectsCodeLikeContent()
        {
            var clipboardText = "Aura\n<div>markup</div>\nChribba";

            var result = _inspector.Inspect(clipboardText);

            Assert.False(result.IsPlausibleLocalList);
            Assert.Equal("Clipboard looked like code, markup, or stack-trace content.", result.IgnoreReason);
        }

        [Fact]
        public void Inspect_RejectsSmallPayloadWithMixedPilotAndCommandLines()
        {
            var clipboardText = "Aura\ndotnet build";

            var result = _inspector.Inspect(clipboardText);

            Assert.False(result.IsPlausibleLocalList);
            Assert.Equal("Clipboard looked like command content.", result.IgnoreReason);
        }

        [Fact]
        public void Inspect_RejectsPayloadWithTooManyNonEmptyLines()
        {
            var clipboardText = string.Join("\n", Enumerable.Repeat("Aura", ClipboardPayloadInspector.MaximumNonEmptyLines + 1));

            var result = _inspector.Inspect(clipboardText);

            Assert.False(result.IsPlausibleLocalList);
            Assert.Equal("Clipboard contained too many lines to treat as a local list.", result.IgnoreReason);
        }

        [Fact]
        public void Inspect_AcceptsStrongLocalListSignalEvenWithOneMarkupLine()
        {
            var names = Enumerable.Range(1, 20).Select(index => $"Pilot {index}").ToList();
            names[19] = "<Grid";
            var clipboardText = string.Join("\n", names);

            var result = _inspector.Inspect(clipboardText);

            Assert.True(result.IsPlausibleLocalList);
            Assert.Equal(20, result.NonEmptyLineCount);
            Assert.Equal(19, result.PlausibleNameCount);
        }
    }
}
