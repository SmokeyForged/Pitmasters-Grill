using PitmastersGrill.Services;
using System.Reflection;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class ApplicationCompositionRootTests
    {
        [Fact]
        public void ComposeNormalRuntime_PreservesSharedApplicationServiceIdentity()
        {
            var appSettingsService = new AppSettingsService();

            var runtime = ApplicationCompositionRoot.ComposeNormalRuntime(appSettingsService);

            Assert.Same(appSettingsService, runtime.AppSettingsService);
            Assert.NotNull(runtime.KillmailDatabaseBootstrap);
            Assert.NotNull(runtime.KillmailDatasetMetadataRepository);
            Assert.NotNull(runtime.BackgroundIntelUpdateService);

            var metadataField = typeof(BackgroundIntelUpdateService).GetField(
                "_metadataRepository",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(metadataField);
            Assert.Same(
                runtime.KillmailDatasetMetadataRepository,
                metadataField!.GetValue(runtime.BackgroundIntelUpdateService));
        }
    }
}
