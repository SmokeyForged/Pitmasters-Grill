using Microsoft.Data.Sqlite;
using PitmastersGrill.Persistence;
using PitmastersGrill.Services;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class R2Z2ResponsibilityTests
    {
        [Fact]
        public async Task SequenceClient_NotFoundPreservesCaughtUpDelay()
        {
            using var httpClient = new HttpClient(new StubHandler(
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)));
            var client = new R2Z2SequenceClient(httpClient, () => 0);

            var result = await client.FetchSequenceAsync(123, 1, CancellationToken.None);

            Assert.Equal(R2Z2SequenceFetchStatus.NotFound, result.Status);
            Assert.Equal(TimeSpan.FromSeconds(8), result.RetryDelay);
        }

        [Fact]
        public async Task SequenceClient_RateLimitHonorsRetryAfter()
        {
            using var httpClient = new HttpClient(new StubHandler(_ =>
            {
                var response = new HttpResponseMessage((HttpStatusCode)429);
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(45));
                return response;
            }));
            var client = new R2Z2SequenceClient(httpClient, () => 0);

            var result = await client.FetchSequenceAsync(456, 1, CancellationToken.None);

            Assert.Equal(R2Z2SequenceFetchStatus.RateLimited, result.Status);
            Assert.Equal("retry-after", result.DelaySource);
            Assert.Equal(TimeSpan.FromSeconds(45), result.RetryDelay);
        }

        [Fact]
        public async Task SequenceClient_RateLimitPreservesExponentialFallback()
        {
            using var httpClient = new HttpClient(new StubHandler(
                _ => new HttpResponseMessage((HttpStatusCode)429)));
            var client = new R2Z2SequenceClient(httpClient, () => 0);

            var result = await client.FetchSequenceAsync(789, 2, CancellationToken.None);

            Assert.Equal(R2Z2SequenceFetchStatus.RateLimited, result.Status);
            Assert.Equal("exponential", result.DelaySource);
            Assert.Equal(TimeSpan.FromSeconds(60), result.RetryDelay);
        }

        [Fact]
        public async Task SequenceClient_ForbiddenPausesWithoutRetryDelay()
        {
            using var httpClient = new HttpClient(new StubHandler(
                _ => new HttpResponseMessage(HttpStatusCode.Forbidden)));
            var client = new R2Z2SequenceClient(httpClient, () => 0);

            var result = await client.FetchSequenceAsync(101, 1, CancellationToken.None);

            Assert.Equal(R2Z2SequenceFetchStatus.Forbidden, result.Status);
            Assert.Equal(TimeSpan.Zero, result.RetryDelay);
        }

        [Theory]
        [InlineData("12345", 12345)]
        [InlineData("{\"sequence\":23456}", 23456)]
        [InlineData("{\"sequence_id\":\"34567\"}", 34567)]
        public void SequenceClient_CurrentSequenceParsingPreservesSupportedShapes(
            string payload,
            long expected)
        {
            Assert.True(R2Z2SequenceClient.TryParseCurrentSequenceId(payload, out var actual));
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void FeedStateRepository_PersistsCheckpointAndBuildsSnapshot()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                $"pmg-r2z2-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            var databasePath = Path.Combine(directory, "r2z2-test.db");

            try
            {
                CreateFeedStateSchema(databasePath);
                var repository = new R2Z2FeedStateRepository(databasePath);

                repository.Update(state =>
                {
                    state.Enabled = 1;
                    state.NextSequenceId = 9002;
                    state.LastProcessedSequenceId = 9001;
                    state.LastSuccessAtUtc = "2026-08-14T20:00:00.0000000Z";
                    state.Last404AtUtc = "2026-08-14T19:59:00.0000000Z";
                    state.LastErrorAtUtc = "";
                    state.LastError = "";
                    state.Status = "Catching up";
                    state.UpdatedAtUtc = "2026-08-14T20:00:00.0000000Z";
                });

                var loaded = repository.Load();
                var snapshot = repository.ReadSnapshot();

                Assert.NotNull(loaded);
                Assert.Equal(9002L, loaded!.NextSequenceId);
                Assert.Equal(9001L, loaded.LastProcessedSequenceId);
                Assert.True(snapshot.Enabled);
                Assert.Equal("Catching up", snapshot.Status);
                Assert.Equal(9002L, snapshot.NextSequenceId);
                Assert.Equal(9001L, snapshot.LastProcessedSequenceId);
                Assert.Equal(1, snapshot.RecentLiveImportsCount);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }

        [Fact]
        public void SequenceEnvelopeParsing_PreservesSequenceAndKillmailPayload()
        {
            const string payload =
                """
                {
                  "sequence_id": 7001,
                  "uploaded_at": "2026-08-14T20:00:00Z",
                  "zkb": { "hash": "abc123" },
                  "killmail": {
                    "killmail_id": 998877,
                    "killmail_time": "2026-08-14T19:58:00Z",
                    "victim": { "character_id": 42 }
                  }
                }
                """;

            var parsed = R2Z2LiveKillmailService.TryExtractSequenceEnvelope(
                payload,
                requestedSequenceId: 7000,
                out var envelope,
                out var error);

            Assert.True(parsed, error);
            Assert.Equal(7001, envelope.SequenceId);
            Assert.Equal("998877", envelope.KillmailId);
            Assert.Equal("abc123", envelope.KillmailHash);
            Assert.Equal("2026-08-14T20:00:00Z", envelope.UploadedAtUtc);
            Assert.Contains("\"killmail_id\"", envelope.KillmailJson);
        }

        [Fact]
        public void R2Z2Facade_RetainsSingleImportAndLifecycleOwnership()
        {
            var facade = ReadRepoFile(
                "PitmastersGrill",
                "Services",
                "R2Z2LiveKillmailService.cs");
            var sequenceClient = ReadRepoFile(
                "PitmastersGrill",
                "Services",
                "R2Z2SequenceClient.cs");
            var stateRepository = ReadRepoFile(
                "PitmastersGrill",
                "Persistence",
                "R2Z2FeedStateRepository.cs");

            Assert.Equal(
                1,
                CountOccurrences(
                    facade,
                    "_incrementalImportService.ImportKillmailJson("));
            Assert.Contains("private CancellationTokenSource? _runCts;", facade);
            Assert.DoesNotContain("CancellationTokenSource", sequenceClient);
            Assert.DoesNotContain("CancellationTokenSource", stateRepository);

            Assert.DoesNotContain("pilot_registry_day", facade);
            Assert.DoesNotContain("pilot_fleet_observations_day", facade);
            Assert.DoesNotContain("pilot_ship_observations_day", facade);
        }

        private static void CreateFeedStateSchema(string databasePath)
        {
            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
            """
            CREATE TABLE live_killmail_feed_state (
                feed_name TEXT PRIMARY KEY,
                enabled INTEGER NOT NULL,
                next_sequence_id INTEGER NULL,
                last_processed_sequence_id INTEGER NULL,
                last_success_at_utc TEXT NOT NULL,
                last_404_at_utc TEXT NOT NULL,
                last_error_at_utc TEXT NOT NULL,
                last_error TEXT NOT NULL,
                status TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
            );

            CREATE TABLE live_killmail_seen (
                killmail_id INTEGER PRIMARY KEY
            );

            INSERT INTO live_killmail_seen (killmail_id) VALUES (998877);
            """;
            command.ExecuteNonQuery();
        }

        private static int CountOccurrences(string text, string value)
        {
            var count = 0;
            var start = 0;

            while ((start = text.IndexOf(value, start, StringComparison.Ordinal)) >= 0)
            {
                count++;
                start += value.Length;
            }

            return count;
        }

        private static string ReadRepoFile(params string[] relativeSegments)
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);

            while (current is not null)
            {
                var projectPath = Path.Combine(
                    current.FullName,
                    "PitmastersGrill",
                    "PitmastersGrill.csproj");

                if (File.Exists(projectPath))
                {
                    var pathSegments = new string[relativeSegments.Length + 1];
                    pathSegments[0] = current.FullName;
                    Array.Copy(relativeSegments, 0, pathSegments, 1, relativeSegments.Length);
                    return File.ReadAllText(Path.Combine(pathSegments));
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the Pitmasters-Grill repository root from the test output directory.");
        }

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

            public StubHandler(
                Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
            {
                _responseFactory = responseFactory;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(_responseFactory(request));
            }
        }
    }
}
