using PitmastersGrill.Persistence;
using PitmastersGrill.Services;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public class KillmailIncrementalImportServiceTests
    {
        [Fact]
        public async Task ImportKillmailJson_MalformedJson_ReturnsFailurePersistsErrorAndReleasesGate()
        {
            using var tempDirectory = new TempDirectory();
            var databasePath = tempDirectory.FilePath("killmail.db");
            new KillmailDatabaseBootstrap(databasePath).Initialize();

            var writeGate = new KillmailDbWriteGate();
            var service = new KillmailIncrementalImportService(databasePath, writeGate);
            var request = new IncrementalKillmailImportRequest
            {
                KillmailId = 9001,
                KillmailHash = "malformed-json-regression",
                KillmailJson = "{ not-valid-json",
                Source = "test",
                SequenceId = 42,
                UploadedAtUtc = "2026-08-13T15:45:00Z"
            };

            var importTask = Task.Run(() => service.ImportKillmailJson(request));
            var completedTask = await Task.WhenAny(importTask, Task.Delay(TimeSpan.FromSeconds(2)));

            Assert.Same(importTask, completedTask);

            var result = await importTask;
            Assert.False(result.Success);
            Assert.Equal("Unable to parse killmail JSON.", result.Error);

            var seenRecord = service.GetSeenRecord(request.KillmailId);
            Assert.NotNull(seenRecord);
            Assert.Equal("error", seenRecord!.ProcessingStatus);
            Assert.Equal("Unable to parse killmail JSON.", seenRecord.LastError);
            Assert.Equal(request.KillmailHash, seenRecord.KillmailHash);
            Assert.Equal(request.SequenceId, seenRecord.FirstSequenceId);
            Assert.Equal(request.SequenceId, seenRecord.LastSequenceId);
            Assert.Equal(request.Source, seenRecord.Source);

            using var gateProbeCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            using var gateProbe = writeGate.Enter("malformed-json regression gate probe", gateProbeCancellation.Token);
        }

        private sealed class TempDirectory : IDisposable
        {
            public TempDirectory()
            {
                Root = Path.Combine(Path.GetTempPath(), "PitmastersGrill.Tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Root);
            }

            public string Root { get; }

            public string FilePath(params string[] segments)
            {
                return Path.Combine(new[] { Root }.Concat(segments).ToArray());
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Root))
                    {
                        Directory.Delete(Root, recursive: true);
                    }
                }
                catch
                {
                }
            }
        }
    }
}
