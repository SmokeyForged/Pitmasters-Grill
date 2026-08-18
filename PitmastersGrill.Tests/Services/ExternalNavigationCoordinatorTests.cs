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
                return Success(url);
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
                return Success(url);
            });

            var result = coordinator.OpenPilotZkill(" ", "Alice Example");

            Assert.True(result.Succeeded);
            Assert.Equal("https://zkillboard.com/search/Alice%20Example/", opened);
        }

        [Theory]
        [InlineData("alliance", "3001", "https://zkillboard.com/alliance/3001/")]
        [InlineData("corporation", "2001", "https://zkillboard.com/corporation/2001/")]
        public void OpenAffiliationZkill_WithSupportedTypeAndValidId_UsesExpectedRoute(
            string entityType,
            string entityId,
            string expectedUrl)
        {
            string? opened = null;
            var coordinator = CreateCoordinator(url =>
            {
                opened = url;
                return Success(url);
            });

            var result = coordinator.OpenAffiliationZkill(entityType, entityId);

            Assert.True(result.Attempted);
            Assert.True(result.Succeeded);
            Assert.Equal(expectedUrl, opened);
        }

        [Theory]
        [InlineData("alliance", "not-a-number")]
        [InlineData("corporation", "")]
        [InlineData("alliance", "0")]
        [InlineData("corporation", "0")]
        [InlineData("alliance", "-1")]
        [InlineData("corporation", "-1")]
        [InlineData("other", "123")]
        public void OpenAffiliationZkill_WithInvalidOrUnsupportedInput_DoesNotLaunch(
            string entityType,
            string entityId)
        {
            var launchCount = 0;
            var coordinator = CreateCoordinator(url =>
            {
                launchCount++;
                return Success(url);
            });

            var result = coordinator.OpenAffiliationZkill(entityType, entityId);

            Assert.False(result.Attempted);
            Assert.False(result.Succeeded);
            Assert.Equal(0, launchCount);
        }

        [Fact]
        public void OpenUrl_WhenLauncherReportsFailure_ReturnsSafeFailureAndWarns()
        {
            var warnings = new List<string>();
            var coordinator = CreateCoordinator(
                url => new BrowserLaunchResult(true, false, url),
                logWarn: warnings.Add);

            var result = coordinator.OpenUrl("https://example.test/", "generic link");

            Assert.True(result.Attempted);
            Assert.False(result.Succeeded);
            Assert.Null(result.Exception);
            Assert.Contains(warnings, warning => warning.Contains("launcher reported failure", StringComparison.Ordinal));
        }

        [Fact]
        public void OpenUrl_WhenLauncherReturnsException_LogsContextAndPreservesException()
        {
            var failure = new InvalidOperationException("boom");
            string? loggedMessage = null;
            Exception? loggedException = null;
            var coordinator = CreateCoordinator(
                url => new BrowserLaunchResult(true, false, url, failure),
                logError: (message, ex) =>
                {
                    loggedMessage = message;
                    loggedException = ex;
                });

            var result = coordinator.OpenUrl("https://example.test/", "generic link");

            Assert.True(result.Attempted);
            Assert.False(result.Succeeded);
            Assert.Same(failure, result.Exception);
            Assert.Same(failure, loggedException);
            Assert.Contains("context='generic link'", loggedMessage, StringComparison.Ordinal);
        }

        [Fact]
        public void OpenUrl_WhenInjectedLauncherThrows_CapturesUnexpectedContractViolation()
        {
            Exception? loggedException = null;
            var coordinator = CreateCoordinator(
                _ => throw new InvalidOperationException("contract violation"),
                logError: (_, ex) => loggedException = ex);

            var result = coordinator.OpenUrl("https://example.test/", "generic link");

            Assert.True(result.Attempted);
            Assert.False(result.Succeeded);
            Assert.IsType<InvalidOperationException>(result.Exception);
            Assert.Same(result.Exception, loggedException);
        }

        private static ExternalNavigationCoordinator CreateCoordinator(
            Func<string, BrowserLaunchResult> tryOpenUrl,
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

        private static BrowserLaunchResult Success(string url) =>
            new(true, true, url);
    }
}
