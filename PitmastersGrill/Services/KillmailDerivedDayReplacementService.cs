using Microsoft.Data.Sqlite;
using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace PitmastersGrill.Services
{
    public sealed class KillmailDerivedDayReplacementService
    {
        private readonly string _databasePath;
        private readonly KillmailDbWriteGate _writeGate;
        private readonly PilotCynoModuleObservationDayRepository _cynoModuleObservationRepository;
        private readonly PilotBaitObservationDayRepository _baitObservationRepository;
        private readonly PilotCynoTackleObservationDayRepository _cynoTackleObservationRepository;

        public KillmailDerivedDayReplacementService(
            string databasePath,
            KillmailDbWriteGate writeGate)
        {
            _databasePath = string.IsNullOrWhiteSpace(databasePath)
                ? throw new ArgumentException("Database path is required.", nameof(databasePath))
                : databasePath;
            _writeGate = writeGate ?? throw new ArgumentNullException(nameof(writeGate));
            _cynoModuleObservationRepository = new PilotCynoModuleObservationDayRepository(databasePath);
            _baitObservationRepository = new PilotBaitObservationDayRepository(databasePath);
            _cynoTackleObservationRepository = new PilotCynoTackleObservationDayRepository(databasePath);
        }

        public void ReplaceDay(
            string dayUtc,
            IReadOnlyCollection<string> archiveKillmailIds,
            IReadOnlyList<PilotCynoModuleObservationDayRecord> cynoModuleObservations,
            IReadOnlyList<PilotBaitObservationDayRecord> baitObservations,
            IReadOnlyList<PilotCynoTackleObservationDayRecord> cynoTackleObservations,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(dayUtc))
            {
                throw new ArgumentException("Day is required.", nameof(dayUtc));
            }

            archiveKillmailIds ??= Array.Empty<string>();
            cynoModuleObservations ??= Array.Empty<PilotCynoModuleObservationDayRecord>();
            baitObservations ??= Array.Empty<PilotBaitObservationDayRecord>();
            cynoTackleObservations ??= Array.Empty<PilotCynoTackleObservationDayRecord>();

            using var writeGate = _writeGate.Enter(
                $"derived intel rebuild replacement day={dayUtc}",
                cancellationToken);

            var preserveKillmailIds = GetSuccessfulIncrementalKillmailIdsByDay(dayUtc);
            foreach (var archiveKillmailId in archiveKillmailIds)
            {
                if (!string.IsNullOrWhiteSpace(archiveKillmailId))
                {
                    preserveKillmailIds.Remove(archiveKillmailId.Trim());
                }
            }

            var preservedCyno = LoadPreservedCynoRows(dayUtc, preserveKillmailIds);
            var preservedBait = LoadPreservedBaitRows(dayUtc, preserveKillmailIds);
            var preservedTackle = LoadPreservedTackleRows(dayUtc, preserveKillmailIds);

            _cynoModuleObservationRepository.ReplaceDay(
                dayUtc,
                MergeCynoRows(cynoModuleObservations, preservedCyno));
            _baitObservationRepository.ReplaceDay(
                dayUtc,
                MergeBaitRows(baitObservations, preservedBait));
            _cynoTackleObservationRepository.ReplaceDay(
                dayUtc,
                MergeTackleRows(cynoTackleObservations, preservedTackle));
        }

        private HashSet<string> GetSuccessfulIncrementalKillmailIdsByDay(string dayUtc)
        {
            var results = new HashSet<string>(StringComparer.Ordinal);

            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            SELECT killmail_id
            FROM live_killmail_seen
            WHERE day_utc = $dayUtc
              AND processing_status IN ('processed', 'duplicate');
            ";
            command.Parameters.AddWithValue("$dayUtc", dayUtc);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!reader.IsDBNull(0))
                {
                    results.Add(reader.GetInt64(0).ToString(CultureInfo.InvariantCulture));
                }
            }

            return results;
        }

        private List<PilotCynoModuleObservationDayRecord> LoadPreservedCynoRows(
            string dayUtc,
            HashSet<string> preserveKillmailIds)
        {
            var results = new List<PilotCynoModuleObservationDayRecord>();
            if (preserveKillmailIds.Count == 0)
            {
                return results;
            }

            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            SELECT
                day_utc,
                character_id,
                killmail_id,
                killmail_time_utc,
                victim_ship_type_id,
                module_type_id,
                module_name,
                quantity_destroyed,
                quantity_dropped,
                item_state,
                source,
                updated_at_utc
            FROM pilot_cyno_module_observations_day
            WHERE day_utc = $dayUtc;
            ";
            command.Parameters.AddWithValue("$dayUtc", dayUtc);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var killmailId = reader.IsDBNull(2) ? "" : reader.GetString(2);
                if (!preserveKillmailIds.Contains(killmailId))
                {
                    continue;
                }

                results.Add(new PilotCynoModuleObservationDayRecord
                {
                    DayUtc = reader.IsDBNull(0) ? "" : reader.GetString(0),
                    CharacterId = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    KillmailId = killmailId,
                    KillmailTimeUtc = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    VictimShipTypeId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    ModuleTypeId = reader.GetInt32(5),
                    ModuleName = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    QuantityDestroyed = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                    QuantityDropped = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                    ItemState = reader.IsDBNull(9) ? "" : reader.GetString(9),
                    Source = reader.IsDBNull(10) ? "" : reader.GetString(10),
                    UpdatedAtUtc = reader.IsDBNull(11) ? "" : reader.GetString(11)
                });
            }

            return results;
        }

        private List<PilotBaitObservationDayRecord> LoadPreservedBaitRows(
            string dayUtc,
            HashSet<string> preserveKillmailIds)
        {
            var results = new List<PilotBaitObservationDayRecord>();
            if (preserveKillmailIds.Count == 0)
            {
                return results;
            }

            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            SELECT
                day_utc,
                character_id,
                killmail_id,
                killmail_time_utc,
                victim_ship_type_id,
                victim_ship_name,
                solar_system_id,
                solar_system_name,
                industrial_cyno_module_type_id,
                industrial_cyno_module_name,
                tackle_module_type_id,
                tackle_module_name,
                tackle_type,
                quantity_destroyed,
                quantity_dropped,
                source,
                updated_at_utc
            FROM pilot_bait_observations_day
            WHERE day_utc = $dayUtc;
            ";
            command.Parameters.AddWithValue("$dayUtc", dayUtc);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var killmailId = reader.IsDBNull(2) ? "" : reader.GetString(2);
                if (!preserveKillmailIds.Contains(killmailId))
                {
                    continue;
                }

                results.Add(new PilotBaitObservationDayRecord
                {
                    DayUtc = reader.IsDBNull(0) ? "" : reader.GetString(0),
                    CharacterId = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    KillmailId = killmailId,
                    KillmailTimeUtc = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    VictimShipTypeId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    VictimShipName = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    SolarSystemId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                    SolarSystemName = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    IndustrialCynoModuleTypeId = reader.GetInt32(8),
                    IndustrialCynoModuleName = reader.IsDBNull(9) ? "" : reader.GetString(9),
                    TackleModuleTypeId = reader.GetInt32(10),
                    TackleModuleName = reader.IsDBNull(11) ? "" : reader.GetString(11),
                    TackleType = ParseTackleType(reader.IsDBNull(12) ? "" : reader.GetString(12)),
                    QuantityDestroyed = reader.IsDBNull(13) ? 0 : reader.GetInt32(13),
                    QuantityDropped = reader.IsDBNull(14) ? 0 : reader.GetInt32(14),
                    Source = reader.IsDBNull(15) ? "" : reader.GetString(15),
                    UpdatedAtUtc = reader.IsDBNull(16) ? "" : reader.GetString(16)
                });
            }

            return results;
        }

        private List<PilotCynoTackleObservationDayRecord> LoadPreservedTackleRows(
            string dayUtc,
            HashSet<string> preserveKillmailIds)
        {
            var results = new List<PilotCynoTackleObservationDayRecord>();
            if (preserveKillmailIds.Count == 0)
            {
                return results;
            }

            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
            @"
            SELECT
                day_utc,
                character_id,
                killmail_id,
                killmail_time_utc,
                victim_ship_type_id,
                victim_ship_name,
                tackle_module_type_id,
                tackle_module_name,
                tackle_type,
                quantity_destroyed,
                quantity_dropped,
                source,
                updated_at_utc
            FROM pilot_cyno_tackle_observations_day
            WHERE day_utc = $dayUtc;
            ";
            command.Parameters.AddWithValue("$dayUtc", dayUtc);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var killmailId = reader.IsDBNull(2) ? "" : reader.GetString(2);
                if (!preserveKillmailIds.Contains(killmailId))
                {
                    continue;
                }

                results.Add(new PilotCynoTackleObservationDayRecord
                {
                    DayUtc = reader.IsDBNull(0) ? "" : reader.GetString(0),
                    CharacterId = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    KillmailId = killmailId,
                    KillmailTimeUtc = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    VictimShipTypeId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    VictimShipName = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    TackleModuleTypeId = reader.GetInt32(6),
                    TackleModuleName = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    TackleType = ParseTackleType(reader.IsDBNull(8) ? "" : reader.GetString(8)),
                    QuantityDestroyed = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                    QuantityDropped = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
                    Source = reader.IsDBNull(11) ? "" : reader.GetString(11),
                    UpdatedAtUtc = reader.IsDBNull(12) ? "" : reader.GetString(12)
                });
            }

            return results;
        }

        private SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();
            return connection;
        }

        private static TackleModuleType ParseTackleType(string value)
        {
            return Enum.TryParse<TackleModuleType>(value, ignoreCase: true, out var parsed)
                ? parsed
                : TackleModuleType.UnknownTackle;
        }

        private static IReadOnlyList<PilotCynoModuleObservationDayRecord> MergeCynoRows(
            IReadOnlyList<PilotCynoModuleObservationDayRecord> archiveRows,
            IReadOnlyList<PilotCynoModuleObservationDayRecord> preservedRows)
        {
            return archiveRows
                .Concat(preservedRows)
                .GroupBy(
                    row => $"{row.DayUtc}|{row.CharacterId}|{row.KillmailId}|{row.ModuleTypeId.ToString(CultureInfo.InvariantCulture)}",
                    StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
        }

        private static IReadOnlyList<PilotBaitObservationDayRecord> MergeBaitRows(
            IReadOnlyList<PilotBaitObservationDayRecord> archiveRows,
            IReadOnlyList<PilotBaitObservationDayRecord> preservedRows)
        {
            return archiveRows
                .Concat(preservedRows)
                .GroupBy(
                    row => $"{row.DayUtc}|{row.CharacterId}|{row.KillmailId}|{row.TackleModuleTypeId.ToString(CultureInfo.InvariantCulture)}",
                    StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
        }

        private static IReadOnlyList<PilotCynoTackleObservationDayRecord> MergeTackleRows(
            IReadOnlyList<PilotCynoTackleObservationDayRecord> archiveRows,
            IReadOnlyList<PilotCynoTackleObservationDayRecord> preservedRows)
        {
            return archiveRows
                .Concat(preservedRows)
                .GroupBy(
                    row => $"{row.DayUtc}|{row.CharacterId}|{row.KillmailId}|{row.TackleModuleTypeId.ToString(CultureInfo.InvariantCulture)}",
                    StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
        }
    }
}
