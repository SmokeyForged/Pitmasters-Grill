using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class PublicDataStatusTextBuilderTests
    {
        [Fact]
        public void BuildIntelCurrentUpdateStatusText_WhenProviderFails_UsesUncertaintyLanguage()
        {
            var snapshot = LoadFixture("provider-failure.json");

            var result = PublicDataStatusTextBuilder.BuildIntelCurrentUpdateStatusText(snapshot);

            Assert.Equal(
                "Public-data provider check failed. PMG cannot confirm current freshness right now: zKill archive request returned HTTP 503 Service Unavailable",
                result);
        }

        [Fact]
        public void BuildIntelCurrentUpdateStatusText_WhenCoverageIsPartial_ShowsLocalAndRequestedDays()
        {
            var snapshot = LoadFixture("partial-coverage.json");

            var result = PublicDataStatusTextBuilder.BuildIntelCurrentUpdateStatusText(snapshot);

            Assert.Equal(
                "Public data is partially populated. Local coverage is 28 of 30 requested archive day(s); missing 2 day(s).",
                result);
        }

        [Fact]
        public void BuildIntelCurrentUpdateStatusText_WhenCurrent_NamesCompletedArchiveDay()
        {
            var snapshot = LoadFixture("current-through-yesterday.json");

            var result = PublicDataStatusTextBuilder.BuildIntelCurrentUpdateStatusText(snapshot);

            Assert.Equal(
                "Public data archive is current through completed archive day 2026-05-02.",
                result);
        }

        [Fact]
        public void BuildIntelCurrentUpdateStatusText_WhenMissingDays_WarnsAboutStaleOrPartialEvidence()
        {
            var snapshot = LoadFixture("missing-days.json");

            var result = PublicDataStatusTextBuilder.BuildIntelCurrentUpdateStatusText(snapshot);

            Assert.Equal(
                "Public data archive is incomplete. Missing 3 archive day(s); PMG may show stale or partial public evidence until catch-up completes.",
                result);
        }

        private static IntelUpdateStatusSnapshot LoadFixture(string fileName)
        {
            var fixtureRoot = FindFixtureRoot();
            var path = Path.Combine(fixtureRoot, "public-data-workflows", fileName);
            var json = File.ReadAllText(path);
            var snapshot = JsonSerializer.Deserialize<IntelUpdateStatusSnapshot>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(snapshot);
            return snapshot!;
        }

        private static string FindFixtureRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "test-fixtures");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate test-fixtures directory from test output path.");
        }
    }
}
