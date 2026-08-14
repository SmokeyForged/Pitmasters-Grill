using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using PitmastersGrill.Services;
using System;
using System.IO;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class KillmailBoardIntegrationTests
    {
        [Fact]
        public void IncrementalKillmailImport_DerivedBaitEvidence_ReachesBoardClassification()
        {
            using var tempDirectory = new TempDirectory();
            var databasePath = tempDirectory.FilePath("killmail.db");
            new KillmailDatabaseBootstrap(databasePath).Initialize();

            var importService = new KillmailIncrementalImportService(
                databasePath,
                new KillmailDbWriteGate());

            const long killmailId = 424242;
            const string victimCharacterId = "90000001";
            const string killmailJson = """
            {
              "killmail_id": 424242,
              "killmail_time": "2026-08-13T20:00:00Z",
              "solar_system_id": 30000142,
              "solar_system_name": "Jita",
              "victim": {
                "character_id": 90000001,
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
              "attackers": [
                {
                  "character_id": 90000002,
                  "ship_type_id": 587
                }
              ]
            }
            """;

            var importResult = importService.ImportKillmailJson(new IncrementalKillmailImportRequest
            {
                KillmailId = killmailId,
                KillmailHash = "phase-3-integration-proof",
                KillmailJson = killmailJson,
                Source = "integration-test",
                SequenceId = 77,
                UploadedAtUtc = "2026-08-13T20:00:01Z"
            });

            Assert.True(importResult.Success, importResult.Error);
            Assert.False(importResult.WasDuplicate);
            Assert.Equal(1, importResult.CynoObservationCount);
            Assert.Equal(1, importResult.BaitObservationCount);

            var baitRepository = new PilotBaitObservationDayRepository(databasePath);
            var baitEvidence = baitRepository.GetRecentBaitEvidenceByCharacterId(victimCharacterId);
            Assert.Single(baitEvidence);
            Assert.Equal(killmailId.ToString(), baitEvidence[0].KillmailId);
            Assert.Equal(52694, baitEvidence[0].IndustrialCynoModuleTypeId);
            Assert.Equal(447, baitEvidence[0].TackleModuleTypeId);
            Assert.Equal(TackleModuleType.WarpScrambler, baitEvidence[0].TackleType);

            var formatter = new PilotBoardRowDetailFormatter(
                new BoardPopulationRetryPolicy(),
                new PilotCynoModuleObservationDayRepository(databasePath),
                baitRepository,
                new PilotCynoTackleObservationDayRepository(databasePath));
            var row = new PilotBoardRow
            {
                CharacterId = victimCharacterId,
                CharacterName = "Harness Pilot"
            };

            formatter.UpdateConfirmedCynoModuleState(row);

            Assert.True(row.HasDerivedBaitEvidence);
            Assert.Equal(1, row.DerivedBaitEvidenceCount);
            Assert.Equal("Bait", row.BoardSignalKind);
            Assert.Equal("B", row.BoardSignalIcon);
            Assert.Equal("Bait: Confirmed", formatter.GetCompactBaitStatusText(row));
            Assert.Contains("indi", formatter.GetBaitEvidenceText(row), StringComparison.OrdinalIgnoreCase);
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
