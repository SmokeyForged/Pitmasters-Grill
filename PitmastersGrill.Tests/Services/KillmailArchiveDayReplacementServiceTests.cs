using Microsoft.Data.Sqlite;
using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using PitmastersGrill.Services;
using System;
using System.IO;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public class KillmailArchiveDayReplacementServiceTests
    {
        [Fact]
        public void ReplaceDay_LaterTableFailure_RollsBackEntireSnapshot()
        {
            using var tempDirectory = new TempDirectory();
            var databasePath = tempDirectory.FilePath("killmail.db");
            new KillmailDatabaseBootstrap(databasePath).Initialize();
            var service = new KillmailArchiveDayReplacementService(databasePath);

            service.ReplaceDay(
                Day,
                Registry("old"), Fleet("old"), Ship("old"),
                Cyno("old"), Bait("old"), Tackle("old"));

            using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText =
                @"
                CREATE TRIGGER fail_archive_day_tackle_insert
                BEFORE INSERT ON pilot_cyno_tackle_observations_day
                BEGIN
                    SELECT RAISE(ABORT, 'fault injection: tackle insert');
                END;
                ";
                command.ExecuteNonQuery();
            }

            var exception = Assert.Throws<SqliteException>(() =>
                service.ReplaceDay(
                    Day,
                    Registry("new"), Fleet("new"), Ship("new"),
                    Cyno("new"), Bait("new"), Tackle("new")));

            Assert.Contains("fault injection", exception.Message, StringComparison.OrdinalIgnoreCase);
            AssertSnapshot(databasePath, "old");
        }

        [Fact]
        public void ReplaceDay_Success_ReplacesAllSixTablesTogether()
        {
            using var tempDirectory = new TempDirectory();
            var databasePath = tempDirectory.FilePath("killmail.db");
            new KillmailDatabaseBootstrap(databasePath).Initialize();
            var service = new KillmailArchiveDayReplacementService(databasePath);

            service.ReplaceDay(
                Day,
                Registry("old"), Fleet("old"), Ship("old"),
                Cyno("old"), Bait("old"), Tackle("old"));

            service.ReplaceDay(
                Day,
                Registry("new"), Fleet("new"), Ship("new"),
                Cyno("new"), Bait("new"), Tackle("new"));

            AssertSnapshot(databasePath, "new");
        }

        private const string Day = "2026-08-13";

        private static PilotRegistryDayRecord[] Registry(string generation) =>
        [
            new()
            {
                DayUtc = Day,
                CharacterId = $"{generation}-registry",
                FirstSeenKillmailTimeUtc = "2026-08-13T12:00:00Z",
                LastSeenKillmailTimeUtc = "2026-08-13T12:00:00Z",
                SeenCount = 1,
                UpdatedAtUtc = "2026-08-13T12:01:00Z"
            }
        ];

        private static PilotFleetObservationDayRecord[] Fleet(string generation) =>
        [
            new()
            {
                DayUtc = Day,
                CharacterId = $"{generation}-fleet",
                AttackerSampleCount = 1,
                AttackerCountSum = 2,
                DerivedAtUtc = "2026-08-13T12:01:00Z"
            }
        ];

        private static PilotShipObservationDayRecord[] Ship(string generation) =>
        [
            new()
            {
                DayUtc = Day,
                CharacterId = $"{generation}-ship",
                LastSeenShipTypeId = 648,
                LastSeenShipTimeUtc = "2026-08-13T12:00:00Z",
                LastSeenCynoShipTypeId = null,
                LastSeenCynoShipName = "",
                LastSeenCynoShipTimeUtc = "",
                UpdatedAtUtc = "2026-08-13T12:01:00Z"
            }
        ];

        private static PilotCynoModuleObservationDayRecord[] Cyno(string generation) =>
        [
            new()
            {
                DayUtc = Day,
                CharacterId = $"{generation}-cyno",
                KillmailId = generation == "old" ? "1001" : "2001",
                KillmailTimeUtc = "2026-08-13T12:00:00Z",
                VictimShipTypeId = 648,
                ModuleTypeId = 52694,
                ModuleName = "Industrial Cynosural Field Generator",
                QuantityDestroyed = 1,
                QuantityDropped = 0,
                ItemState = "destroyed",
                Source = "test",
                UpdatedAtUtc = "2026-08-13T12:01:00Z"
            }
        ];

        private static PilotBaitObservationDayRecord[] Bait(string generation) =>
        [
            new()
            {
                DayUtc = Day,
                CharacterId = $"{generation}-bait",
                KillmailId = generation == "old" ? "1002" : "2002",
                KillmailTimeUtc = "2026-08-13T12:00:00Z",
                VictimShipTypeId = 648,
                VictimShipName = "Badger",
                SolarSystemId = 30000142,
                SolarSystemName = "Jita",
                IndustrialCynoModuleTypeId = 52694,
                IndustrialCynoModuleName = "Industrial Cynosural Field Generator",
                TackleModuleTypeId = 447,
                TackleModuleName = "Warp Scrambler I",
                TackleType = TackleModuleType.WarpScrambler,
                QuantityDestroyed = 1,
                QuantityDropped = 0,
                Source = "test",
                UpdatedAtUtc = "2026-08-13T12:01:00Z"
            }
        ];

        private static PilotCynoTackleObservationDayRecord[] Tackle(string generation) =>
        [
            new()
            {
                DayUtc = Day,
                CharacterId = $"{generation}-tackle",
                KillmailId = generation == "old" ? "1003" : "2003",
                KillmailTimeUtc = "2026-08-13T12:00:00Z",
                VictimShipTypeId = 648,
                VictimShipName = "Badger",
                TackleModuleTypeId = 447,
                TackleModuleName = "Warp Scrambler I",
                TackleType = TackleModuleType.WarpScrambler,
                QuantityDestroyed = 1,
                QuantityDropped = 0,
                Source = "test",
                UpdatedAtUtc = "2026-08-13T12:01:00Z"
            }
        ];

        private static void AssertSnapshot(string databasePath, string generation)
        {
            Assert.Equal($"{generation}-registry", ReadOnlyCharacter(databasePath, "pilot_registry_day"));
            Assert.Equal($"{generation}-fleet", ReadOnlyCharacter(databasePath, "pilot_fleet_observations_day"));
            Assert.Equal($"{generation}-ship", ReadOnlyCharacter(databasePath, "pilot_ship_observations_day"));
            Assert.Equal($"{generation}-cyno", ReadOnlyCharacter(databasePath, "pilot_cyno_module_observations_day"));
            Assert.Equal($"{generation}-bait", ReadOnlyCharacter(databasePath, "pilot_bait_observations_day"));
            Assert.Equal($"{generation}-tackle", ReadOnlyCharacter(databasePath, "pilot_cyno_tackle_observations_day"));
        }

        private static string ReadOnlyCharacter(string databasePath, string tableName)
        {
            using var connection = new SqliteConnection($"Data Source={databasePath}"));
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT character_id FROM {tableName} WHERE day_utc = $dayUtc;";
            command.Parameters.AddWithValue("$dayUtc", Day);
            return Convert.ToString(command.ExecuteScalar()) ?? "";
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
