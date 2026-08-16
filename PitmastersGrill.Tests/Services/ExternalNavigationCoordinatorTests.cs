using PitmastersGrill.Services;
using System;
using System.Collections.Generic;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class ExternalNavigationCoordinatorTests
    {
        [Fact]
        public void OpenPilotZkill_WithCharacterId_UsesCharacterRoute()
        {
            var opened = new List<string>();
            var coordinator = CreateCoordinator(url =>
            {
                opened.Add(url);
                return true;
            });

            var result = coordinator.OpenPilotZkill("9001", "Alice");

            Assert.True(result.Attempted);
            Assert.True(result.Succeeded);
            Assert.Equal("https://zkillboard.com/character/9001/", result.Url);
            Assert.Single(opened);
            Assert.Equal(result.Url, opened[0]);
        }

        [Fact]
        public void OpenPilotZkill_WithoutCharacterId_UsesEncodedNameSearch()
        {
            string? opened = null;
            var coordinator = CreateCoordinator(url =>
            {
                opened = url;
                return true;
            });

            var result = coordinator.OpenPilotZkill(" ", "Alice Example");

            Assert.True(result.Succeeded);
            Assert.Equal("https://zkillboard.com/search/Alice%20Example/", opened);
        }

        [Theory]
        [InlineData("alliance", "3001", "https://zkillboard.com/alliance/3001/")]
        [InlineData("corporation", "2001", "https://zkillboard.com/corporation/2001/")]
        public void OpenAffiliationZkill_WithValidId_UsesExpectedRoute(string entityType, string entityId, string expectedUrl)
        {
            string? opened = null;
            var coordinator = CreateCoordinator(url =>
            {
                opened = url;
                return true;
            });

            var result = coordinator.OpenAffiliationZkill(entityType, entityId);

            Assert.True(result.Attempted);
            Assert.True(result.Succeeded);
            Assert.Equal(expectedUrl, opened);
        }

        [Theory]
        [InlineData("alliance", "not-a-number")]
        [InlineData("corporation", "")]
        [InlineData("other", "123")]
        public void OpenAffiliationZkill_WithInvalidInput_DoesNotLaunch(string entityType, string entityId)
        {
            var launchCount = 0;
            var coordinator = CreateCoordinator(_ =>
            {
                launchCount++;
                return true;
            });

            var result = coordinator.OpenAffiliationZkill(entityType, entityId);

            Assert.False(result.Attempted);
            Assert.False(result.Succeeded);
            Assert.Equal(0, launchCount);
        }

        [Fact]
        public void OpenUrl_WhenLauncherReportsFailure_ReturnsSafeFailure()
        {
            var warnings = new List<string>();
            var coordinator = CreateCoordinator(
                _ => false,
                logWarn: warnings.Add);

            var result = coordinator.OpenUrl("https://example.test/", "generic link");

            Assert.True(result.Attempted);
            Assert.False(result.Succeeded);
            Assert.Null(result.Exception);
            Assert.Contains(warnings, warning => warning.Contains("launcher reported failure", StringComparison.Ordinal));
        }

        [Fact]
        public void OpenUrl_WhenLauncherThrows_CapturesAndLogsException()
        {
            Exception? loggedException = null;
            var coordinator = CreateCoordinator(
                _ => throw new InvalidOperationException("boom"),
                logError: (_, ex) => loggedException = ex);

            var result = coordinator.OpenUrl("https://example.test/", "generic link");

            Assert.True(result.Attempted);
            Assert.False(result.Succeeded);
            Assert.IsType<InvalidOperationException>(result.Exception);
            Assert.Same(result.Exception, loggedException);
        }

        private static ExternalNavigationCoordinator CreateCoordinator(
            Func<string, bool> tryOpenUrl,
            Action<string>? logInfo = null,
            Action<string>? logWarn = null,
            Action<string, Exception>? logError = null)
        {
            return new ExternalNavigationCoordinator(
                new ZkillUrlBuilder(),
                tryOpenUrl,
                logInfo ?? (_ => { }),
                logWarn ?? (_ => { }),
                logError ?? ((_, _) => { }));
        }
    }
}
