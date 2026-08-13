using Microsoft.Data.Sqlite;
using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using PitmastersGrill.Services;
using System;
using System.IO;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public class KillmailArchiveDayReplacementDuplicateKeyTests
    {
        [Fact]
        public void ReplaceDay_DuplicateDerivedNaturalKeys_UpdatesInsteadOfFailing()
        {
            using var tempDirectory = new TempDirectory();
            var databasePath = tempDirectory.FilePath("killmail.db");
            new KillmailDatabaseBootstrap(databasePath).Initialize();
            var service = new KillmailArchiveDayReplacementService(databasePath);

            service.ReplaceDay(
                Day,
                Array.Empty<PilotRegistryDayRecord>(),
                Array.Empty<PilotFleetObservationDayRecord>(),
                Array.Empty<PilotShipObservationDayRecord>(),
                new[]
                {
                    Cyno("first-cyno", 1),
                    Cyno("second-cyno", 2)
                },
                new[]
                {
                    Bait("first-bait", 1),
                    Bait("second-bait", 2)
                },
                new[]
                {
                    Tackle("first-tackle", 1),
                    Tackle("second-tackle", 2)
                });

            AssertDerivedRow(databasePath, "pilot_cyno_module_observations_day", "module_name", "second-cyno", 2);
            AssertDerivedRow(databasePath, "pilot_bait_observations_day", "tackle_module_name", "second-bait", 2);
            AssertDerivedRow(databasePath, "pilot_cyno_tackle_observations_day", "tackle_module_name", "second-tackle", 2);
        }

        private const string Day = "2026-08-13";
        private const string CharacterId = "12345";
        private const string KillmailId = "9001";

        private static PilotCynoModuleObservationDayRecord Cyno(string name, int quantity) => new()
        {
            DayUtc = Day,
            CharacterId = CharacterId,
            KillmailId = KillmailId,
            KillmailTimeUtc = "2026-08-13T12:00:00Z",
            VictimShipTypeId = 648,
            ModuleTypeId = 52694,
            ModuleName = name,
            QuantityDestroyed = quantity,
            QuantityDropped = 0,
            ItemState = "destroyed",
            Source = "test",
            UpdatedAtUtc = "2026-08-13T12:01:00Z"
        };

        private static PilotBaitObservationDayRecord Bait(string name, int quantity) => new()
        {
            DayUtc = Day,
            CharacterId = CharacterId,
            KillmailId = KillmailId,
            KillmailTimeUtc = "2026-08-13T12:00:00Z",
            VictimShipTypeId = 648,
            VictimShipName = "Badger",
            SolarSystemId = 30000142,
            SolarSystemName = "Jita",
            IndustrialCynoModuleTypeId = 52694,
            IndustrialCynoModuleName = "Industrial Cynosural Field Generator",
            TackleModuleTypeId = 447,
            TackleModuleName = name,
            TackleType = TackleModuleType.WarpScrambler,
            QuantityDestroyed = quantity,
            QuantityDropped = 0,
            Source = "test",
            UpdatedAtUtc = "2026-08-13T12:01:00Z"
        };

        private static PilotCynoTackleObservationDayRecord Tackle(string name, int quantity) => new()
        {
            DayUtc = Day,
            CharacterId = CharacterId,
            KillmailId = KillmailId,
            KillmailTimeUtc = "2026-08-13T12:00:00Z",
            VictimShipTypeId = 648,
            VictimShipName = "Badger",
            TackleModuleTypeId = 447,
            TackleModuleName = name,
            TackleType = TackleModuleType.WarpScrambler,
            QuantityDestroyed = quantity,
            QuantityDropped = 0,
            Source = "test",
            UpdatedAtUtc = "2026-08-13T12:01:00Z"
        };

        private static void AssertDerivedRow(
            string databasePath,
            string tableName,
            string nameColumn,
            string expectedName,
            int expectedQuantity)
        {
            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                $"SELECT COUNT(*), MAX({nameColumn}), MAX(quantity_destroyed) FROM {tableName} WHERE day_utc = $dayUtc;";
            command.Parameters.AddWithValue("$dayUtc", Day);
            using var reader = command.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal(1, reader.GetInt32(0));
            Assert.Equal(expectedName, reader.GetString(1));
            Assert.Equal(expectedQuantity, reader.GetInt32(2));
        }

        private sealed class TempDirectory : IDisposable
        {
            public TempDirectory()
            {
                Root = Path.Combine(Path.GetTempPath(), "PitmastersGrill.Tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Root);
            }

            public string Root { get; }

            public string FilePath(params string[] segments) =>
                Path.Combine(new[] { Root }.Concat(segments).ToArray());

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
