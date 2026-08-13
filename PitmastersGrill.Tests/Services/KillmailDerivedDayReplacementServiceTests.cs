using Microsoft.Data.Sqlite;
using PitmastersGrill.Persistence;
using PitmastersGrill.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public class KillmailDerivedDayReplacementServiceTests
    {
        [Fact]
        public void ReplaceDay_PreservesIncrementalOnlyEvidence_AndKeepsArchiveAuthoritative()
        {
            using var tempDirectory = new TempDirectory();
            var databasePath = tempDirectory.FilePath("killmail.db");
            new KillmailDatabaseBootstrap(databasePath).Initialize();

            var importerGate = new KillmailDbWriteGate();
            var importer = new KillmailIncrementalImportService(databasePath, importerGate);
            var replacementService = new KillmailDerivedDayReplacementService(
                databasePath,
                new KillmailDbWriteGate());

            var importResult = importer.ImportKillmailJson(
                new IncrementalKillmailImportRequest
                {
                    KillmailId = 9001,
                    KillmailHash = "incremental-derived-preservation",
                    KillmailJson = BuildIndustrialCynoBaitKillmailJson(),
                    Source = "test",
                    SequenceId = 42,
                    UploadedAtUtc = "2026-08-13T12:01:00Z"
                });

            Assert.True(importResult.Success);
            Assert.False(importResult.WasDuplicate);
            Assert.Equal(1, importResult.CynoObservationCount);
            Assert.Equal(1, importResult.BaitObservationCount);
            Assert.Equal(1, importResult.TackleObservationCount);
            AssertDerivedEvidenceCounts(databasePath, expected: 1);

            replacementService.ReplaceDay(
                "2026-08-13",
                Array.Empty<string>(),
                Array.Empty<Models.PilotCynoModuleObservationDayRecord>(),
                Array.Empty<Models.PilotBaitObservationDayRecord>(),
                Array.Empty<Models.PilotCynoTackleObservationDayRecord>());

            AssertDerivedEvidenceCounts(databasePath, expected: 1);

            replacementService.ReplaceDay(
                "2026-08-13",
                new[] { "9001" },
                Array.Empty<Models.PilotCynoModuleObservationDayRecord>(),
                Array.Empty<Models.PilotBaitObservationDayRecord>(),
                Array.Empty<Models.PilotCynoTackleObservationDayRecord>());

            AssertDerivedEvidenceCounts(databasePath, expected: 0);

            var seenRecord = importer.GetSeenRecord(9001);
            Assert.NotNull(seenRecord);
            Assert.Equal("processed", seenRecord!.ProcessingStatus);
        }

        [Fact]
        public async Task ReplaceDay_WaitsForAnotherWriteGateInstance()
        {
            using var tempDirectory = new TempDirectory();
            var databasePath = tempDirectory.FilePath("killmail.db");
            new KillmailDatabaseBootstrap(databasePath).Initialize();

            var heldGate = new KillmailDbWriteGate();
            var replacementService = new KillmailDerivedDayReplacementService(
                databasePath,
                new KillmailDbWriteGate());

            var lease = heldGate.Enter("test gate hold");
            try
            {
                var replacementTask = Task.Run(() =>
                    replacementService.ReplaceDay(
                        "2026-08-13",
                        Array.Empty<string>(),
                        Array.Empty<Models.PilotCynoModuleObservationDayRecord>(),
                        Array.Empty<Models.PilotBaitObservationDayRecord>(),
                        Array.Empty<Models.PilotCynoTackleObservationDayRecord>()));

                await Task.Delay(TimeSpan.FromMilliseconds(150));
                Assert.False(replacementTask.IsCompleted);

                lease.Dispose();
                lease = null!;

                await replacementTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            finally
            {
                lease?.Dispose();
            }
        }

        private static void AssertDerivedEvidenceCounts(string databasePath, int expected)
        {
            Assert.Equal(expected, CountRows(databasePath, "pilot_cyno_module_observations_day"));
            Assert.Equal(expected, CountRows(databasePath, "pilot_bait_observations_day"));
            Assert.Equal(expected, CountRows(databasePath, "pilot_cyno_tackle_observations_day"));
        }

        private static int CountRows(string databasePath, string tableName)
        {
            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {tableName};";
            return Convert.ToInt32(command.ExecuteScalar());
        }

        private static string BuildIndustrialCynoBaitKillmailJson()
        {
            return
            """
            {
              "killmail_id": 9001,
              "killmail_time": "2026-08-13T12:00:00Z",
              "solar_system_id": 30000142,
              "solar_system_name": "Jita",
              "victim": {
                "character_id": 12345,
                "ship_type_id": 648,
                "ship_name": "Badger",
                "items": [
                  {
                    "item_type_id": 52694,
                    "quantity_destroyed": 1,
                    "quantity_dropped": 0
                  },
                  {
                    "item_type_id": 447,
                    "quantity_destroyed": 1,
                    "quantity_dropped": 0
                  }
                ]
              },
              "attackers": []
            }
            """;
        }

        private sealed class TempDirectory : IDisposable
        {
            public TempDirectory()
            {
                Root = Path.Combine(
                    Path.GetTempPath(),
                    "PitmastersGrill.Tests",
                    Guid.NewGuid().ToString("N"));
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
