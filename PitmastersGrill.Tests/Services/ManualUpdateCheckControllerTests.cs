using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class ManualUpdateCheckControllerTests
    {
        [Fact]
        public async Task RunAsync_WhenCurrent_DisablesThenEnablesAndShowsCurrentStatus()
        {
            var harness = new Harness
            {
                CheckAsync = (skipped, _) =>
                {
                    harnessObservedSkip = skipped;
                    return Task.FromResult(PmgUpdateAwarenessResult.Current("1.2.3"));
                }
            };
            harness.LoadedSettings.SkippedUpdateVersion = "1.1.0";
            string? harnessObservedSkip = null;

            await harness.CreateSubject().RunAsync();

            Assert.Equal(new[] { false, true }, harness.EnabledStates);
            Assert.Contains(harness.StatusTexts, text => text.StartsWith("Checking GitHub", StringComparison.Ordinal));
            Assert.Contains(harness.StatusTexts, text => text.Contains("PMG is current. Current version: 1.2.3.", StringComparison.Ordinal));
            Assert.Equal("1.1.0", harnessObservedSkip);
            Assert.Empty(harness.OpenedUrls);
            Assert.Empty(harness.SavedSettings);
            var message = Assert.Single(harness.Messages);
            Assert.Equal(MessageBoxButton.OK, message.Buttons);
            Assert.Equal(MessageBoxImage.Information, message.Image);
        }

        [Fact]
        public async Task RunAsync_WhenUpdateAvailableAndYes_OpensReleasePageWithoutSkipping()
        {
            var harness = new Harness
            {
                Response = MessageBoxResult.Yes,
                CheckAsync = (_, _) => Task.FromResult(
                    PmgUpdateAwarenessResult.Available("1.2.3", "1.3.0", "https://example.test/release"))
            };

            await harness.CreateSubject().RunAsync();

            Assert.Equal(new[] { false, true }, harness.EnabledStates);
            Assert.Equal("https://example.test/release", Assert.Single(harness.OpenedUrls));
            Assert.Empty(harness.SavedSettings);
            Assert.Null(harness.RuntimeSettings.SkippedUpdateVersion);
            Assert.Contains(harness.StatusTexts, text => text.Contains("PMG 1.3.0 is available", StringComparison.Ordinal));
            Assert.Equal(MessageBoxButton.YesNoCancel, Assert.Single(harness.Messages).Buttons);
        }

        [Fact]
        public async Task RunAsync_WhenUpdateAvailableAndNo_DoesNotOpenOrPersistSkip()
        {
            var harness = new Harness
            {
                Response = MessageBoxResult.No,
                CheckAsync = (_, _) => Task.FromResult(
                    PmgUpdateAwarenessResult.Available("1.2.3", "1.3.0", "https://example.test/release"))
            };

            await harness.CreateSubject().RunAsync();

            Assert.Equal(new[] { false, true }, harness.EnabledStates);
            Assert.Empty(harness.OpenedUrls);
            Assert.Empty(harness.SavedSettings);
            Assert.Null(harness.RuntimeSettings.SkippedUpdateVersion);
            Assert.Contains(harness.StatusTexts, text => text.Contains("PMG 1.3.0 is available", StringComparison.Ordinal));
        }

        [Fact]
        public async Task RunAsync_WhenUpdateAvailableAndCancel_PersistsSkipWithoutOpening()
        {
            var harness = new Harness
            {
                Response = MessageBoxResult.Cancel,
                CheckAsync = (_, _) => Task.FromResult(
                    PmgUpdateAwarenessResult.Available("1.2.3", "1.3.0", "https://example.test/release"))
            };

            await harness.CreateSubject().RunAsync();

            Assert.Equal(new[] { false, true }, harness.EnabledStates);
            Assert.Empty(harness.OpenedUrls);
            Assert.Same(harness.LoadedSettings, Assert.Single(harness.SavedSettings));
            Assert.Equal("1.3.0", harness.LoadedSettings.SkippedUpdateVersion);
            Assert.Equal("1.3.0", harness.RuntimeSettings.SkippedUpdateVersion);
            Assert.Contains(harness.StatusTexts, text => text.StartsWith("Skipped PMG 1.3.0.", StringComparison.Ordinal));
        }

        [Fact]
        public async Task RunAsync_WhenShutdownTokenIsCancelled_ReportsCancellationAndReenables()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var harness = new Harness
            {
                CheckAsync = (_, token) => Task.FromCanceled<PmgUpdateAwarenessResult>(token)
            };

            await harness.CreateSubject(cts.Token).RunAsync();

            Assert.Equal(new[] { false, true }, harness.EnabledStates);
            Assert.Equal("Update check cancelled.", harness.StatusTexts[^1]);
            Assert.Empty(harness.OpenedUrls);
            Assert.Empty(harness.SavedSettings);
            Assert.Empty(harness.Messages);
        }

        [Fact]
        public async Task RunAsync_WhenCheckFails_RemainsFailOpenAndReenables()
        {
            var harness = new Harness
            {
                CheckAsync = (_, _) => Task.FromException<PmgUpdateAwarenessResult>(new InvalidOperationException("network unavailable"))
            };

            await harness.CreateSubject().RunAsync();

            Assert.Equal(new[] { false, true }, harness.EnabledStates);
            Assert.Equal("Update check failed: network unavailable", harness.StatusTexts[^1]);
            Assert.Empty(harness.OpenedUrls);
            Assert.Empty(harness.SavedSettings);
            var message = Assert.Single(harness.Messages);
            Assert.Equal(MessageBoxButton.OK, message.Buttons);
            Assert.Equal(MessageBoxImage.Warning, message.Image);
        }

        private sealed class Harness
        {
            public AppSettings LoadedSettings { get; } = new();
            public AppSettings RuntimeSettings { get; } = new();
            public List<bool> EnabledStates { get; } = new();
            public List<string> StatusTexts { get; } = new();
            public List<string> OpenedUrls { get; } = new();
            public List<AppSettings> SavedSettings { get; } = new();
            public List<MessageRecord> Messages { get; } = new();
            public MessageBoxResult Response { get; set; } = MessageBoxResult.OK;
            public Func<string?, CancellationToken, Task<PmgUpdateAwarenessResult>> CheckAsync { get; set; } =
                (_, _) => Task.FromResult(PmgUpdateAwarenessResult.Current("1.0.0"));

            public ManualUpdateCheckController CreateSubject(CancellationToken cancellationToken = default)
            {
                return new ManualUpdateCheckController(
                    EnabledStates.Add,
                    StatusTexts.Add,
                    OpenedUrls.Add,
                    RuntimeSettings,
                    cancellationToken,
                    () => cancellationToken.IsCancellationRequested,
                    () => LoadedSettings,
                    SavedSettings.Add,
                    CheckAsync,
                    (message, title, buttons, image) =>
                    {
                        Messages.Add(new MessageRecord(message, title, buttons, image));
                        return Response;
                    });
            }
        }

        private sealed record MessageRecord(
            string Message,
            string Title,
            MessageBoxButton Buttons,
            MessageBoxImage Image);
    }
}
