using Microsoft.Data.Sqlite;
using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using PitmastersGrill.Providers;
using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersGrill.Services
{
    public sealed class R2Z2LiveKillmailService
    {
        private const string FeedName = "r2z2";
        private const string CurrentSequenceUrl = "https://r2z2.zkillboard.com/ephemeral/sequence.json";
        private const string SequenceFileUrlFormat = "https://r2z2.zkillboard.com/ephemeral/{0}.json";
        private const int DefaultStartupSequenceOverlap = 250;
        private const int CurrentDayBridgeSequenceOverlap = 10000;
        private static readonly TimeSpan SuccessPacingDelay = TimeSpan.FromMilliseconds(200);
        private static readonly TimeSpan CaughtUpBaseDelay = TimeSpan.FromSeconds(8);
        private static readonly TimeSpan CaughtUpJitterMaxDelay = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan RateLimitBaseDelay = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan RateLimitJitterMaxDelay = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan RateLimitMaxDelay = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan ErrorBackoffDelay = TimeSpan.FromSeconds(15);

        private readonly object _sync = new();
        private readonly string _databasePath;
        private readonly AppSettingsService _appSettingsService;
        private readonly KillmailDatasetMetadataRepository _metadataRepository;
        private readonly KillmailDerivedObservationParser _parser = new();
        private readonly CynoShipCatalog _cynoShipCatalog = new();
        private readonly HttpClient _httpClient;

        private Task? _workerTask;
        private CancellationTokenSource? _runCts;
        private R2Z2LiveFeedSnapshot _snapshot = new();
        private string _nextRetryAtUtc = "";

        public R2Z2LiveKillmailService(AppSettingsService appSettingsService)
        {
            _appSettingsService = appSettingsService ?? throw new ArgumentNullException(nameof(appSettingsService));
            _databasePath = KillmailPaths.GetKillmailDatabasePath();
            _metadataRepository = new KillmailDatasetMetadataRepository(_databasePath);
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(AppHttpDefaults.GenericUserAgent);
            _snapshot = CreateDisabledSnapshot();
        }

        public event Action? StatusChanged;

        public R2Z2LiveFeedSnapshot GetSnapshot()
        {
            lock (_sync)
            {
                return CloneSnapshot(_snapshot);
            }
        }

        public void StartIfConfiguredAfterUiShown()
        {
            var enabled = _appSettingsService.Load().LiveZkillFeedEnabled;
            AppLogger.KillmailImportInfo($"R2Z2 startup configuration evaluated. enabled={enabled}");
            PersistEnabledFlag(enabled);

            if (!enabled)
            {
                AppLogger.KillmailImportInfo("R2Z2 live feed startup skipped because AppSettings disabled it.");
                return;
            }

            AppLogger.KillmailImportInfo("R2Z2 live feed startup approved after UI shown.");
            EnsureWorkerStarted("main window shown");
        }

        public Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PersistEnabledFlag(enabled);

            if (enabled)
            {
                AppLogger.KillmailImportInfo("R2Z2 live feed enabled.");
                EnsureWorkerStarted("settings toggle");
            }
            else
            {
                AppLogger.KillmailImportInfo("R2Z2 live feed disabled.");
                CancelWorker();
            }

            return Task.CompletedTask;
        }

        public void Stop()
        {
            CancelWorker();
        }

        private void EnsureWorkerStarted(string reason)
        {
            lock (_sync)
            {
                if (_workerTask != null && !_workerTask.IsCompleted)
                {
                    return;
                }

                _runCts?.Cancel();
                _runCts?.Dispose();
                _runCts = new CancellationTokenSource();
                _workerTask = Task.Run(() => RunLoopAsync(_runCts.Token));
                AppLogger.KillmailImportInfo($"R2Z2 live feed worker started. reason='{reason}'");
            }
        }

        private void CancelWorker()
        {
            lock (_sync)
            {
                AppLogger.KillmailImportInfo("R2Z2 live feed worker stop requested.");
                _runCts?.Cancel();
            }
        }

        private async Task RunLoopAsync(CancellationToken cancellationToken)
        {
            var consecutiveRateLimitCount = 0;

            try
            {
                await EnsureInitializedSequenceAsync(cancellationToken);

                while (!cancellationToken.IsCancellationRequested)
                {
                    var state = LoadFeedState();
                    if (state == null || state.Enabled == 0)
                    {
                        UpdateSnapshot(ReadSnapshotFromDatabase());
                        return;
                    }

                    if (!state.NextSequenceId.HasValue || state.NextSequenceId.Value <= 0)
                    {
                        await EnsureInitializedSequenceAsync(cancellationToken);
                        continue;
                    }

                    var nextSequenceId = state.NextSequenceId.Value;
                    var sequenceUrl = string.Format(CultureInfo.InvariantCulture, SequenceFileUrlFormat, nextSequenceId);
                    AppLogger.KillmailImportDebug($"R2Z2 sequence fetch start. sequence={nextSequenceId} url={sequenceUrl}");

                    try
                    {
                        using var response = await _httpClient.GetAsync(sequenceUrl, cancellationToken);

                        if (response.StatusCode == HttpStatusCode.NotFound)
                        {
                            consecutiveRateLimitCount = 0;
                            var retryDelay = BuildCaughtUpDelay();
                            var retryAtUtc = DateTime.UtcNow.Add(retryDelay).ToString("o");
                            SetNextRetryAtUtc(retryAtUtc);
                            AppLogger.KillmailImportDebug($"R2Z2 caught up wait. sequence={nextSequenceId} status=404 retryDelayMs={(long)retryDelay.TotalMilliseconds} retryAtUtc={retryAtUtc}");
                            UpdateFeedState(stateUpdate =>
                            {
                                stateUpdate.Status = "Caught up / waiting";
                                stateUpdate.Last404AtUtc = DateTime.UtcNow.ToString("o");
                                stateUpdate.LastError = "";
                                stateUpdate.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
                            });

                            await Task.Delay(retryDelay, cancellationToken);
                            continue;
                        }

                        if (response.StatusCode == (HttpStatusCode)429)
                        {
                            consecutiveRateLimitCount++;
                            var retryDelay = GetRateLimitDelay(response, consecutiveRateLimitCount, out var delaySource);
                            var retryAtUtc = DateTime.UtcNow.Add(retryDelay).ToString("o");
                            SetNextRetryAtUtc(retryAtUtc);
                            AppLogger.KillmailImportDebug($"R2Z2 rate limit backoff. sequence={nextSequenceId} status=429 retryDelayMs={(long)retryDelay.TotalMilliseconds} source={delaySource} consecutive429={consecutiveRateLimitCount} retryAtUtc={retryAtUtc}");
                            UpdateFeedState(stateUpdate =>
                            {
                                stateUpdate.Status = "Backing off after rate limit";
                                stateUpdate.LastErrorAtUtc = DateTime.UtcNow.ToString("o");
                                stateUpdate.LastError = $"HTTP 429 rate limit. Retry at {retryAtUtc}.";
                                stateUpdate.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
                            });

                            await Task.Delay(retryDelay, cancellationToken);
                            continue;
                        }

                        if (response.StatusCode == HttpStatusCode.Forbidden)
                        {
                            SetNextRetryAtUtc("");
                            AppLogger.KillmailImportWarn($"R2Z2 live feed paused. sequence={nextSequenceId} status=403");
                            UpdateFeedState(stateUpdate =>
                            {
                                stateUpdate.Status = "Blocked / paused";
                                stateUpdate.LastErrorAtUtc = DateTime.UtcNow.ToString("o");
                                stateUpdate.LastError = "HTTP 403 forbidden.";
                                stateUpdate.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
                            });

                            return;
                        }

                        if (!response.IsSuccessStatusCode)
                        {
                            SetNextRetryAtUtc("");
                            var error = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
                            AppLogger.KillmailImportWarn($"R2Z2 sequence fetch failed. sequence={nextSequenceId} error={error}");
                            UpdateFeedState(stateUpdate =>
                            {
                                stateUpdate.Status = "Error";
                                stateUpdate.LastErrorAtUtc = DateTime.UtcNow.ToString("o");
                                stateUpdate.LastError = error;
                                stateUpdate.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
                            });

                            await Task.Delay(ErrorBackoffDelay, cancellationToken);
                            continue;
                        }

                        var content = await response.Content.ReadAsStringAsync(cancellationToken);
                        var processed = ProcessSequencePayload(nextSequenceId, content);

                        if (!processed.Success)
                        {
                            SetNextRetryAtUtc("");
                            AppLogger.KillmailImportWarn(
                                $"R2Z2 live killmail parse/import failed. sequence={nextSequenceId} error={processed.Error}");
                            UpdateFeedState(stateUpdate =>
                            {
                                stateUpdate.Status = "Error";
                                stateUpdate.LastErrorAtUtc = DateTime.UtcNow.ToString("o");
                                stateUpdate.LastError = processed.Error;
                                stateUpdate.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
                            });

                            await Task.Delay(ErrorBackoffDelay, cancellationToken);
                            continue;
                        }

                        consecutiveRateLimitCount = 0;
                        SetNextRetryAtUtc("");
                        AppLogger.KillmailImportDebug(
                            processed.WasDuplicate
                                ? $"R2Z2 duplicate killmail skipped. sequence={nextSequenceId} killmailId={processed.KillmailId}"
                                : $"R2Z2 live killmail processed. sequence={nextSequenceId} killmailId={processed.KillmailId} day={processed.DayUtc}");
                        await Task.Delay(SuccessPacingDelay, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        SetNextRetryAtUtc("");
                        AppLogger.KillmailImportWarn($"R2Z2 live feed error. sequence={nextSequenceId} message={ex.Message}");
                        AppLogger.ErrorOnly("R2Z2 live feed exception.", ex);

                        UpdateFeedState(stateUpdate =>
                        {
                            stateUpdate.Status = "Error";
                            stateUpdate.LastErrorAtUtc = DateTime.UtcNow.ToString("o");
                            stateUpdate.LastError = ex.Message;
                            stateUpdate.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
                        });

                        await Task.Delay(ErrorBackoffDelay, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                AppLogger.KillmailImportInfo("R2Z2 live feed worker stopped.");
            }
        }

        private async Task EnsureInitializedSequenceAsync(CancellationToken cancellationToken)
        {
            var state = LoadFeedState() ?? CreateDefaultState();
            if (state.NextSequenceId.HasValue && state.NextSequenceId.Value > 0)
            {
                return;
            }

            UpdateFeedState(stateUpdate =>
            {
                stateUpdate.Status = "Starting";
                stateUpdate.LastError = "";
                stateUpdate.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
            });

            AppLogger.KillmailImportDebug("R2Z2 current sequence fetch begin.");
            using var response = await _httpClient.GetAsync(CurrentSequenceUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!TryParseCurrentSequenceId(payload, out var currentSequenceId))
            {
                throw new InvalidOperationException("Unable to parse the current R2Z2 sequence.");
            }

            var startupOverlap = DetermineStartupSequenceOverlap(out var overlapReason);
            var initializedNextSequence = Math.Max(1, currentSequenceId - startupOverlap);
            AppLogger.KillmailImportInfo(
                $"R2Z2 startup sequence initialized. currentSequence={currentSequenceId} nextSequence={initializedNextSequence} overlap={startupOverlap} reason={overlapReason}");
            AppLogger.KillmailImportDebug($"R2Z2 current sequence fetch end. currentSequence={currentSequenceId}");

            UpdateFeedState(stateUpdate =>
            {
                stateUpdate.NextSequenceId = initializedNextSequence;
                stateUpdate.Status = "Starting";
                stateUpdate.LastError = "";
                stateUpdate.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
            });
        }

        private LiveProcessResult ProcessSequencePayload(long requestedSequenceId, string content)
        {
            if (!TryExtractSequenceEnvelope(content, requestedSequenceId, out var envelope, out var error))
            {
                return new LiveProcessResult
                {
                    Success = false,
                    Error = error
                };
            }

            var parsed = _parser.ParseKillmailEntry(envelope.KillmailJson);
            if (parsed == null)
            {
                return new LiveProcessResult
                {
                    Success = false,
                    Error = "Unable to parse embedded killmail JSON."
                };
            }

            if (!long.TryParse(envelope.KillmailId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var killmailId))
            {
                return new LiveProcessResult
                {
                    Success = false,
                    Error = "Live payload killmail_id was missing or invalid."
                };
            }

            var nowUtc = DateTime.UtcNow.ToString("o");
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var transaction = connection.BeginTransaction();

            AppLogger.KillmailImportDebug(
                $"R2Z2 derived observations. sequence={requestedSequenceId} killmailId={envelope.KillmailId} registry={parsed.RegistryPilots.Count} fleet={parsed.FleetPilots.Count} ship={parsed.ShipPilots.Count} cyno={parsed.CynoModuleObservations.Count} bait={parsed.BaitObservations.Count} tackle={parsed.CynoTackleObservations.Count}");

            if (TryGetSeenRecord(connection, transaction, killmailId, out _))
            {
                UpsertSeenRecord(
                    connection,
                    transaction,
                    killmailId,
                    envelope.KillmailHash,
                    envelope.SequenceId,
                    parsed.KillmailTimeUtc,
                    parsed.DayUtc,
                    envelope.UploadedAtUtc,
                    nowUtc,
                    "duplicate",
                    "");

                UpdateFeedStateInTransaction(
                    connection,
                    transaction,
                    state =>
                    {
                        state.Enabled = 1;
                        state.NextSequenceId = envelope.SequenceId + 1;
                        state.LastProcessedSequenceId = envelope.SequenceId;
                        state.LastSuccessAtUtc = nowUtc;
                        state.LastError = "";
                        state.Status = "Catching up";
                        state.UpdatedAtUtc = nowUtc;
                    });

                transaction.Commit();

                UpdateSnapshot(ReadSnapshotFromDatabase());
                AppLogger.KillmailImportDebug($"R2Z2 checkpoint updated. sequence={envelope.SequenceId} duplicate=true");

                return new LiveProcessResult
                {
                    Success = true,
                    WasDuplicate = true,
                    KillmailId = envelope.KillmailId,
                    DayUtc = parsed.DayUtc
                };
            }

            foreach (var registryPilot in parsed.RegistryPilots)
            {
                UpsertRegistryRecord(connection, transaction, parsed.DayUtc, registryPilot, nowUtc);
            }

            foreach (var fleetPilot in parsed.FleetPilots)
            {
                UpsertFleetRecord(connection, transaction, parsed.DayUtc, fleetPilot, nowUtc);
            }

            foreach (var shipPilot in parsed.ShipPilots)
            {
                var shipUpsertAction = UpsertShipRecord(connection, transaction, parsed.DayUtc, shipPilot, nowUtc);
                AppLogger.KillmailImportDebug(
                    $"R2Z2 ship observation upsert. sequence={requestedSequenceId} killmailId={envelope.KillmailId} characterId={shipPilot.CharacterId} shipTypeId={(shipPilot.LastSeenShipTypeId.HasValue ? shipPilot.LastSeenShipTypeId.Value.ToString(CultureInfo.InvariantCulture) : "<null>")} killmailTimeUtc={shipPilot.LastSeenShipTimeUtc} day={parsed.DayUtc} action={shipUpsertAction}");
            }

            foreach (var observation in parsed.CynoModuleObservations)
            {
                observation.DayUtc = parsed.DayUtc;
                observation.UpdatedAtUtc = nowUtc;
                UpsertCynoModuleRecord(connection, transaction, observation);
            }

            foreach (var observation in parsed.BaitObservations)
            {
                observation.DayUtc = parsed.DayUtc;
                observation.UpdatedAtUtc = nowUtc;
                UpsertBaitRecord(connection, transaction, observation);
            }

            foreach (var observation in parsed.CynoTackleObservations)
            {
                observation.DayUtc = parsed.DayUtc;
                observation.UpdatedAtUtc = nowUtc;
                UpsertCynoTackleRecord(connection, transaction, observation);
            }

            UpsertSeenRecord(
                connection,
                transaction,
                killmailId,
                envelope.KillmailHash,
                envelope.SequenceId,
                parsed.KillmailTimeUtc,
                parsed.DayUtc,
                envelope.UploadedAtUtc,
                nowUtc,
                "processed",
                "");

            UpdateFeedStateInTransaction(
                connection,
                transaction,
                state =>
                {
                    state.Enabled = 1;
                    state.NextSequenceId = envelope.SequenceId + 1;
                    state.LastProcessedSequenceId = envelope.SequenceId;
                    state.LastSuccessAtUtc = nowUtc;
                    state.LastError = "";
                    state.Status = "Catching up";
                    state.UpdatedAtUtc = nowUtc;
                });

            transaction.Commit();
            _metadataRepository.SetUtcNow("last_successful_update_at_utc");

            UpdateSnapshot(ReadSnapshotFromDatabase());
            AppLogger.KillmailImportDebug($"R2Z2 checkpoint updated. sequence={envelope.SequenceId} duplicate=false");

            return new LiveProcessResult
            {
                Success = true,
                WasDuplicate = false,
                KillmailId = envelope.KillmailId,
                DayUtc = parsed.DayUtc
            };
        }

        private void UpsertRegistryRecord(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string dayUtc,
            KillmailRegistryPilotSeen record,
            string updatedAtUtc)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            INSERT INTO pilot_registry_day (
                day_utc,
                character_id,
                first_seen_killmail_time_utc,
                last_seen_killmail_time_utc,
                seen_count,
                updated_at_utc
            )
            VALUES (
                $dayUtc,
                $characterId,
                $firstSeen,
                $lastSeen,
                1,
                $updatedAtUtc
            )
            ON CONFLICT(day_utc, character_id) DO UPDATE SET
                first_seen_killmail_time_utc = CASE
                    WHEN pilot_registry_day.first_seen_killmail_time_utc = '' OR excluded.first_seen_killmail_time_utc < pilot_registry_day.first_seen_killmail_time_utc
                        THEN excluded.first_seen_killmail_time_utc
                    ELSE pilot_registry_day.first_seen_killmail_time_utc
                END,
                last_seen_killmail_time_utc = CASE
                    WHEN excluded.last_seen_killmail_time_utc > pilot_registry_day.last_seen_killmail_time_utc
                        THEN excluded.last_seen_killmail_time_utc
                    ELSE pilot_registry_day.last_seen_killmail_time_utc
                END,
                seen_count = pilot_registry_day.seen_count + 1,
                updated_at_utc = excluded.updated_at_utc;
            ";
            command.Parameters.AddWithValue("$dayUtc", dayUtc);
            command.Parameters.AddWithValue("$characterId", record.CharacterId);
            command.Parameters.AddWithValue("$firstSeen", record.FirstSeenKillmailTimeUtc);
            command.Parameters.AddWithValue("$lastSeen", record.LastSeenKillmailTimeUtc);
            command.Parameters.AddWithValue("$updatedAtUtc", updatedAtUtc);
            command.ExecuteNonQuery();
        }

        private void UpsertFleetRecord(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string dayUtc,
            KillmailFleetPilotSeen record,
            string derivedAtUtc)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            INSERT INTO pilot_fleet_observations_day (
                day_utc,
                character_id,
                attacker_sample_count,
                attacker_count_sum,
                derived_at_utc
            )
            VALUES (
                $dayUtc,
                $characterId,
                1,
                $attackerCountSum,
                $derivedAtUtc
            )
            ON CONFLICT(day_utc, character_id) DO UPDATE SET
                attacker_sample_count = pilot_fleet_observations_day.attacker_sample_count + 1,
                attacker_count_sum = pilot_fleet_observations_day.attacker_count_sum + excluded.attacker_count_sum,
                derived_at_utc = excluded.derived_at_utc;
            ";
            command.Parameters.AddWithValue("$dayUtc", dayUtc);
            command.Parameters.AddWithValue("$characterId", record.CharacterId);
            command.Parameters.AddWithValue("$attackerCountSum", record.AttackerCountForThisKillmail);
            command.Parameters.AddWithValue("$derivedAtUtc", derivedAtUtc);
            command.ExecuteNonQuery();
        }

        private string UpsertShipRecord(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string dayUtc,
            KillmailShipPilotSeen record,
            string updatedAtUtc)
        {
            var isCynoCapable = _cynoShipCatalog.TryGetCynoShipName(record.LastSeenShipTypeId, out var cynoShipName);
            var action = DetermineShipUpsertAction(connection, transaction, dayUtc, record);

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            INSERT INTO pilot_ship_observations_day (
                day_utc,
                character_id,
                last_seen_ship_type_id,
                last_seen_ship_time_utc,
                last_seen_cyno_ship_type_id,
                last_seen_cyno_ship_name,
                last_seen_cyno_ship_time_utc,
                updated_at_utc
            )
            VALUES (
                $dayUtc,
                $characterId,
                $lastSeenShipTypeId,
                $lastSeenShipTimeUtc,
                $lastSeenCynoShipTypeId,
                $lastSeenCynoShipName,
                $lastSeenCynoShipTimeUtc,
                $updatedAtUtc
            )
            ON CONFLICT(day_utc, character_id) DO UPDATE SET
                last_seen_ship_type_id = CASE
                    WHEN excluded.last_seen_ship_time_utc > pilot_ship_observations_day.last_seen_ship_time_utc
                        THEN excluded.last_seen_ship_type_id
                    ELSE pilot_ship_observations_day.last_seen_ship_type_id
                END,
                last_seen_ship_time_utc = CASE
                    WHEN excluded.last_seen_ship_time_utc > pilot_ship_observations_day.last_seen_ship_time_utc
                        THEN excluded.last_seen_ship_time_utc
                    ELSE pilot_ship_observations_day.last_seen_ship_time_utc
                END,
                last_seen_cyno_ship_type_id = CASE
                    WHEN excluded.last_seen_cyno_ship_time_utc <> ''
                         AND (pilot_ship_observations_day.last_seen_cyno_ship_time_utc = ''
                              OR excluded.last_seen_cyno_ship_time_utc > pilot_ship_observations_day.last_seen_cyno_ship_time_utc)
                        THEN excluded.last_seen_cyno_ship_type_id
                    ELSE pilot_ship_observations_day.last_seen_cyno_ship_type_id
                END,
                last_seen_cyno_ship_name = CASE
                    WHEN excluded.last_seen_cyno_ship_time_utc <> ''
                         AND (pilot_ship_observations_day.last_seen_cyno_ship_time_utc = ''
                              OR excluded.last_seen_cyno_ship_time_utc > pilot_ship_observations_day.last_seen_cyno_ship_time_utc)
                        THEN excluded.last_seen_cyno_ship_name
                    ELSE pilot_ship_observations_day.last_seen_cyno_ship_name
                END,
                last_seen_cyno_ship_time_utc = CASE
                    WHEN excluded.last_seen_cyno_ship_time_utc <> ''
                         AND (pilot_ship_observations_day.last_seen_cyno_ship_time_utc = ''
                              OR excluded.last_seen_cyno_ship_time_utc > pilot_ship_observations_day.last_seen_cyno_ship_time_utc)
                        THEN excluded.last_seen_cyno_ship_time_utc
                    ELSE pilot_ship_observations_day.last_seen_cyno_ship_time_utc
                END,
                updated_at_utc = excluded.updated_at_utc;
            ";
            command.Parameters.AddWithValue("$dayUtc", dayUtc);
            command.Parameters.AddWithValue("$characterId", record.CharacterId);
            command.Parameters.AddWithValue("$lastSeenShipTypeId", (object?)record.LastSeenShipTypeId ?? DBNull.Value);
            command.Parameters.AddWithValue("$lastSeenShipTimeUtc", record.LastSeenShipTimeUtc);
            command.Parameters.AddWithValue("$lastSeenCynoShipTypeId", isCynoCapable ? (object?)record.LastSeenShipTypeId ?? DBNull.Value : DBNull.Value);
            command.Parameters.AddWithValue("$lastSeenCynoShipName", isCynoCapable ? cynoShipName : "");
            command.Parameters.AddWithValue("$lastSeenCynoShipTimeUtc", isCynoCapable ? record.LastSeenShipTimeUtc : "");
            command.Parameters.AddWithValue("$updatedAtUtc", updatedAtUtc);
            command.ExecuteNonQuery();
            return action;
        }

        private static string DetermineShipUpsertAction(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string dayUtc,
            KillmailShipPilotSeen record)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            SELECT last_seen_ship_time_utc
            FROM pilot_ship_observations_day
            WHERE day_utc = $dayUtc
              AND character_id = $characterId
            LIMIT 1;
            ";
            command.Parameters.AddWithValue("$dayUtc", dayUtc);
            command.Parameters.AddWithValue("$characterId", record.CharacterId);

            var existingLastSeen = command.ExecuteScalar()?.ToString() ?? "";

            if (string.IsNullOrWhiteSpace(existingLastSeen))
            {
                return "inserted";
            }

            return string.CompareOrdinal(record.LastSeenShipTimeUtc ?? "", existingLastSeen) > 0
                ? "updated"
                : "skipped";
        }

        private static void UpsertCynoModuleRecord(
            SqliteConnection connection,
            SqliteTransaction transaction,
            PilotCynoModuleObservationDayRecord record)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            INSERT INTO pilot_cyno_module_observations_day (
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
            )
            VALUES (
                $dayUtc,
                $characterId,
                $killmailId,
                $killmailTimeUtc,
                $victimShipTypeId,
                $moduleTypeId,
                $moduleName,
                $quantityDestroyed,
                $quantityDropped,
                $itemState,
                $source,
                $updatedAtUtc
            )
            ON CONFLICT(day_utc, character_id, killmail_id, module_type_id) DO UPDATE SET
                killmail_time_utc = excluded.killmail_time_utc,
                victim_ship_type_id = excluded.victim_ship_type_id,
                module_name = excluded.module_name,
                quantity_destroyed = excluded.quantity_destroyed,
                quantity_dropped = excluded.quantity_dropped,
                item_state = excluded.item_state,
                source = excluded.source,
                updated_at_utc = excluded.updated_at_utc;
            ";
            command.Parameters.AddWithValue("$dayUtc", record.DayUtc);
            command.Parameters.AddWithValue("$characterId", record.CharacterId);
            command.Parameters.AddWithValue("$killmailId", record.KillmailId);
            command.Parameters.AddWithValue("$killmailTimeUtc", record.KillmailTimeUtc);
            command.Parameters.AddWithValue("$victimShipTypeId", (object?)record.VictimShipTypeId ?? DBNull.Value);
            command.Parameters.AddWithValue("$moduleTypeId", record.ModuleTypeId);
            command.Parameters.AddWithValue("$moduleName", record.ModuleName);
            command.Parameters.AddWithValue("$quantityDestroyed", record.QuantityDestroyed);
            command.Parameters.AddWithValue("$quantityDropped", record.QuantityDropped);
            command.Parameters.AddWithValue("$itemState", record.ItemState);
            command.Parameters.AddWithValue("$source", record.Source);
            command.Parameters.AddWithValue("$updatedAtUtc", record.UpdatedAtUtc);
            command.ExecuteNonQuery();
        }

        private static void UpsertBaitRecord(
            SqliteConnection connection,
            SqliteTransaction transaction,
            PilotBaitObservationDayRecord record)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            INSERT INTO pilot_bait_observations_day (
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
            )
            VALUES (
                $dayUtc,
                $characterId,
                $killmailId,
                $killmailTimeUtc,
                $victimShipTypeId,
                $victimShipName,
                $solarSystemId,
                $solarSystemName,
                $industrialCynoModuleTypeId,
                $industrialCynoModuleName,
                $tackleModuleTypeId,
                $tackleModuleName,
                $tackleType,
                $quantityDestroyed,
                $quantityDropped,
                $source,
                $updatedAtUtc
            )
            ON CONFLICT(day_utc, character_id, killmail_id, tackle_module_type_id) DO UPDATE SET
                killmail_time_utc = excluded.killmail_time_utc,
                victim_ship_type_id = excluded.victim_ship_type_id,
                victim_ship_name = excluded.victim_ship_name,
                solar_system_id = excluded.solar_system_id,
                solar_system_name = excluded.solar_system_name,
                industrial_cyno_module_type_id = excluded.industrial_cyno_module_type_id,
                industrial_cyno_module_name = excluded.industrial_cyno_module_name,
                tackle_module_name = excluded.tackle_module_name,
                tackle_type = excluded.tackle_type,
                quantity_destroyed = excluded.quantity_destroyed,
                quantity_dropped = excluded.quantity_dropped,
                source = excluded.source,
                updated_at_utc = excluded.updated_at_utc;
            ";
            command.Parameters.AddWithValue("$dayUtc", record.DayUtc);
            command.Parameters.AddWithValue("$characterId", record.CharacterId);
            command.Parameters.AddWithValue("$killmailId", record.KillmailId);
            command.Parameters.AddWithValue("$killmailTimeUtc", record.KillmailTimeUtc);
            command.Parameters.AddWithValue("$victimShipTypeId", (object?)record.VictimShipTypeId ?? DBNull.Value);
            command.Parameters.AddWithValue("$victimShipName", record.VictimShipName);
            command.Parameters.AddWithValue("$solarSystemId", (object?)record.SolarSystemId ?? DBNull.Value);
            command.Parameters.AddWithValue("$solarSystemName", record.SolarSystemName);
            command.Parameters.AddWithValue("$industrialCynoModuleTypeId", record.IndustrialCynoModuleTypeId);
            command.Parameters.AddWithValue("$industrialCynoModuleName", record.IndustrialCynoModuleName);
            command.Parameters.AddWithValue("$tackleModuleTypeId", record.TackleModuleTypeId);
            command.Parameters.AddWithValue("$tackleModuleName", record.TackleModuleName);
            command.Parameters.AddWithValue("$tackleType", record.TackleType.ToString());
            command.Parameters.AddWithValue("$quantityDestroyed", record.QuantityDestroyed);
            command.Parameters.AddWithValue("$quantityDropped", record.QuantityDropped);
            command.Parameters.AddWithValue("$source", record.Source);
            command.Parameters.AddWithValue("$updatedAtUtc", record.UpdatedAtUtc);
            command.ExecuteNonQuery();
        }

        private static void UpsertCynoTackleRecord(
            SqliteConnection connection,
            SqliteTransaction transaction,
            PilotCynoTackleObservationDayRecord record)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            INSERT INTO pilot_cyno_tackle_observations_day (
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
            )
            VALUES (
                $dayUtc,
                $characterId,
                $killmailId,
                $killmailTimeUtc,
                $victimShipTypeId,
                $victimShipName,
                $tackleModuleTypeId,
                $tackleModuleName,
                $tackleType,
                $quantityDestroyed,
                $quantityDropped,
                $source,
                $updatedAtUtc
            )
            ON CONFLICT(day_utc, character_id, killmail_id, tackle_module_type_id) DO UPDATE SET
                killmail_time_utc = excluded.killmail_time_utc,
                victim_ship_type_id = excluded.victim_ship_type_id,
                victim_ship_name = excluded.victim_ship_name,
                tackle_module_name = excluded.tackle_module_name,
                tackle_type = excluded.tackle_type,
                quantity_destroyed = excluded.quantity_destroyed,
                quantity_dropped = excluded.quantity_dropped,
                source = excluded.source,
                updated_at_utc = excluded.updated_at_utc;
            ";
            command.Parameters.AddWithValue("$dayUtc", record.DayUtc);
            command.Parameters.AddWithValue("$characterId", record.CharacterId);
            command.Parameters.AddWithValue("$killmailId", record.KillmailId);
            command.Parameters.AddWithValue("$killmailTimeUtc", record.KillmailTimeUtc);
            command.Parameters.AddWithValue("$victimShipTypeId", (object?)record.VictimShipTypeId ?? DBNull.Value);
            command.Parameters.AddWithValue("$victimShipName", record.VictimShipName);
            command.Parameters.AddWithValue("$tackleModuleTypeId", record.TackleModuleTypeId);
            command.Parameters.AddWithValue("$tackleModuleName", record.TackleModuleName);
            command.Parameters.AddWithValue("$tackleType", record.TackleType.ToString());
            command.Parameters.AddWithValue("$quantityDestroyed", record.QuantityDestroyed);
            command.Parameters.AddWithValue("$quantityDropped", record.QuantityDropped);
            command.Parameters.AddWithValue("$source", record.Source);
            command.Parameters.AddWithValue("$updatedAtUtc", record.UpdatedAtUtc);
            command.ExecuteNonQuery();
        }

        private static void UpsertSeenRecord(
            SqliteConnection connection,
            SqliteTransaction transaction,
            long killmailId,
            string killmailHash,
            long sequenceId,
            string killmailTimeUtc,
            string dayUtc,
            string uploadedAtUtc,
            string processedAtUtc,
            string processingStatus,
            string lastError)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            INSERT INTO live_killmail_seen (
                killmail_id,
                killmail_hash,
                first_sequence_id,
                last_sequence_id,
                killmail_time_utc,
                day_utc,
                uploaded_at_utc,
                processed_at_utc,
                source,
                processing_status,
                last_error
            )
            VALUES (
                $killmailId,
                $killmailHash,
                $firstSequenceId,
                $lastSequenceId,
                $killmailTimeUtc,
                $dayUtc,
                $uploadedAtUtc,
                $processedAtUtc,
                'r2z2',
                $processingStatus,
                $lastError
            )
            ON CONFLICT(killmail_id) DO UPDATE SET
                killmail_hash = excluded.killmail_hash,
                last_sequence_id = excluded.last_sequence_id,
                killmail_time_utc = excluded.killmail_time_utc,
                day_utc = excluded.day_utc,
                uploaded_at_utc = excluded.uploaded_at_utc,
                processed_at_utc = excluded.processed_at_utc,
                source = excluded.source,
                processing_status = excluded.processing_status,
                last_error = excluded.last_error;
            ";
            command.Parameters.AddWithValue("$killmailId", killmailId);
            command.Parameters.AddWithValue("$killmailHash", killmailHash ?? "");
            command.Parameters.AddWithValue("$firstSequenceId", sequenceId);
            command.Parameters.AddWithValue("$lastSequenceId", sequenceId);
            command.Parameters.AddWithValue("$killmailTimeUtc", killmailTimeUtc ?? "");
            command.Parameters.AddWithValue("$dayUtc", dayUtc ?? "");
            command.Parameters.AddWithValue("$uploadedAtUtc", uploadedAtUtc ?? "");
            command.Parameters.AddWithValue("$processedAtUtc", processedAtUtc ?? "");
            command.Parameters.AddWithValue("$processingStatus", processingStatus ?? "processed");
            command.Parameters.AddWithValue("$lastError", lastError ?? "");
            command.ExecuteNonQuery();
        }

        private static bool TryGetSeenRecord(
            SqliteConnection connection,
            SqliteTransaction transaction,
            long killmailId,
            out long lastSequenceId)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            SELECT last_sequence_id
            FROM live_killmail_seen
            WHERE killmail_id = $killmailId
            LIMIT 1;
            ";
            command.Parameters.AddWithValue("$killmailId", killmailId);

            var scalar = command.ExecuteScalar();
            if (scalar == null || scalar == DBNull.Value)
            {
                lastSequenceId = 0;
                return false;
            }

            lastSequenceId = Convert.ToInt64(scalar, CultureInfo.InvariantCulture);
            return true;
        }

        private void PersistEnabledFlag(bool enabled)
        {
            if (!enabled)
            {
                SetNextRetryAtUtc("");
            }

            UpdateFeedState(state =>
            {
                state.Enabled = enabled ? 1 : 0;
                if (!enabled)
                {
                    state.Status = "Disabled";
                    state.LastError = "";
                }

                state.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
            });
        }

        private void UpdateFeedState(Action<LiveFeedStateRow> update)
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var transaction = connection.BeginTransaction();
            UpdateFeedStateInTransaction(connection, transaction, update);
            transaction.Commit();

            UpdateSnapshot(ReadSnapshotFromDatabase());
        }

        private void UpdateFeedStateInTransaction(
            SqliteConnection connection,
            SqliteTransaction transaction,
            Action<LiveFeedStateRow> update)
        {
            var state = LoadFeedState(connection, transaction) ?? CreateDefaultState();
            update(state);
            UpsertFeedState(connection, transaction, state);
        }

        private LiveFeedStateRow? LoadFeedState()
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();
            return LoadFeedState(connection, transaction: null);
        }

        private static LiveFeedStateRow? LoadFeedState(SqliteConnection connection, SqliteTransaction? transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            SELECT
                feed_name,
                enabled,
                next_sequence_id,
                last_processed_sequence_id,
                last_success_at_utc,
                last_404_at_utc,
                last_error_at_utc,
                last_error,
                status,
                updated_at_utc
            FROM live_killmail_feed_state
            WHERE feed_name = $feedName
            LIMIT 1;
            ";
            command.Parameters.AddWithValue("$feedName", FeedName);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            return new LiveFeedStateRow
            {
                FeedName = reader.IsDBNull(0) ? FeedName : reader.GetString(0),
                Enabled = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                NextSequenceId = reader.IsDBNull(2) ? null : reader.GetInt64(2),
                LastProcessedSequenceId = reader.IsDBNull(3) ? null : reader.GetInt64(3),
                LastSuccessAtUtc = reader.IsDBNull(4) ? "" : reader.GetString(4),
                Last404AtUtc = reader.IsDBNull(5) ? "" : reader.GetString(5),
                LastErrorAtUtc = reader.IsDBNull(6) ? "" : reader.GetString(6),
                LastError = reader.IsDBNull(7) ? "" : reader.GetString(7),
                Status = reader.IsDBNull(8) ? "Disabled" : reader.GetString(8),
                UpdatedAtUtc = reader.IsDBNull(9) ? "" : reader.GetString(9)
            };
        }

        private static void UpsertFeedState(SqliteConnection connection, SqliteTransaction transaction, LiveFeedStateRow state)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
            @"
            INSERT INTO live_killmail_feed_state (
                feed_name,
                enabled,
                next_sequence_id,
                last_processed_sequence_id,
                last_success_at_utc,
                last_404_at_utc,
                last_error_at_utc,
                last_error,
                status,
                updated_at_utc
            )
            VALUES (
                $feedName,
                $enabled,
                $nextSequenceId,
                $lastProcessedSequenceId,
                $lastSuccessAtUtc,
                $last404AtUtc,
                $lastErrorAtUtc,
                $lastError,
                $status,
                $updatedAtUtc
            )
            ON CONFLICT(feed_name) DO UPDATE SET
                enabled = excluded.enabled,
                next_sequence_id = excluded.next_sequence_id,
                last_processed_sequence_id = excluded.last_processed_sequence_id,
                last_success_at_utc = excluded.last_success_at_utc,
                last_404_at_utc = excluded.last_404_at_utc,
                last_error_at_utc = excluded.last_error_at_utc,
                last_error = excluded.last_error,
                status = excluded.status,
                updated_at_utc = excluded.updated_at_utc;
            ";
            command.Parameters.AddWithValue("$feedName", state.FeedName);
            command.Parameters.AddWithValue("$enabled", state.Enabled);
            command.Parameters.AddWithValue("$nextSequenceId", (object?)state.NextSequenceId ?? DBNull.Value);
            command.Parameters.AddWithValue("$lastProcessedSequenceId", (object?)state.LastProcessedSequenceId ?? DBNull.Value);
            command.Parameters.AddWithValue("$lastSuccessAtUtc", state.LastSuccessAtUtc ?? "");
            command.Parameters.AddWithValue("$last404AtUtc", state.Last404AtUtc ?? "");
            command.Parameters.AddWithValue("$lastErrorAtUtc", state.LastErrorAtUtc ?? "");
            command.Parameters.AddWithValue("$lastError", state.LastError ?? "");
            command.Parameters.AddWithValue("$status", state.Status ?? "Disabled");
            command.Parameters.AddWithValue("$updatedAtUtc", state.UpdatedAtUtc ?? "");
            command.ExecuteNonQuery();
        }

        private R2Z2LiveFeedSnapshot ReadSnapshotFromDatabase()
        {
            var state = LoadFeedState() ?? CreateDefaultState();
            return new R2Z2LiveFeedSnapshot
            {
                Source = "R2Z2",
                Enabled = state.Enabled != 0,
                Status = string.IsNullOrWhiteSpace(state.Status) ? "Disabled" : state.Status,
                NextSequenceId = state.NextSequenceId,
                LastProcessedSequenceId = state.LastProcessedSequenceId,
                LastSuccessAtUtc = state.LastSuccessAtUtc,
                LastCaughtUpAtUtc = state.Last404AtUtc,
                LastErrorAtUtc = state.LastErrorAtUtc,
                LastError = state.LastError,
                RecentLiveImportsCount = CountSeenRows()
            };
        }

        private int CountSeenRows()
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM live_killmail_seen;";
            var scalar = command.ExecuteScalar();
            return scalar == null || scalar == DBNull.Value
                ? 0
                : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
        }

        private void UpdateSnapshot(R2Z2LiveFeedSnapshot snapshot)
        {
            lock (_sync)
            {
                snapshot.NextRetryAtUtc = _nextRetryAtUtc;
                _snapshot = CloneSnapshot(snapshot);
            }

            StatusChanged?.Invoke();
        }

        private static R2Z2LiveFeedSnapshot CloneSnapshot(R2Z2LiveFeedSnapshot snapshot)
        {
            return new R2Z2LiveFeedSnapshot
            {
                Source = snapshot.Source,
                Enabled = snapshot.Enabled,
                Status = snapshot.Status,
                NextSequenceId = snapshot.NextSequenceId,
                LastProcessedSequenceId = snapshot.LastProcessedSequenceId,
                LastSuccessAtUtc = snapshot.LastSuccessAtUtc,
                LastCaughtUpAtUtc = snapshot.LastCaughtUpAtUtc,
                LastErrorAtUtc = snapshot.LastErrorAtUtc,
                LastError = snapshot.LastError,
                NextRetryAtUtc = snapshot.NextRetryAtUtc,
                RecentLiveImportsCount = snapshot.RecentLiveImportsCount
            };
        }

        private static R2Z2LiveFeedSnapshot CreateDisabledSnapshot()
        {
            return new R2Z2LiveFeedSnapshot
            {
                Source = "R2Z2",
                Enabled = false,
                Status = "Disabled"
            };
        }

        private static LiveFeedStateRow CreateDefaultState()
        {
            return new LiveFeedStateRow
            {
                FeedName = FeedName,
                Enabled = 0,
                Status = "Disabled",
                UpdatedAtUtc = DateTime.UtcNow.ToString("o")
            };
        }

        private static bool TryParseCurrentSequenceId(string payload, out long sequenceId)
        {
            sequenceId = 0;
            if (string.IsNullOrWhiteSpace(payload))
            {
                return false;
            }

            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Number && root.TryGetInt64(out sequenceId))
            {
                return true;
            }

            if (TryReadLong(root, "sequence", out sequenceId) || TryReadLong(root, "sequence_id", out sequenceId))
            {
                return true;
            }

            return false;
        }

        private static TimeSpan BuildCaughtUpDelay()
        {
            return CaughtUpBaseDelay + BuildJitter(CaughtUpJitterMaxDelay);
        }

        private static TimeSpan GetRateLimitDelay(HttpResponseMessage response, int consecutiveRateLimitCount, out string delaySource)
        {
            if (TryGetRetryAfterDelay(response, out var retryAfterDelay))
            {
                delaySource = "retry-after";
                return retryAfterDelay + BuildJitter(RateLimitJitterMaxDelay);
            }

            var multiplier = Math.Max(0, consecutiveRateLimitCount - 1);
            var exponentialSeconds = RateLimitBaseDelay.TotalSeconds * Math.Pow(2, multiplier);
            var cappedDelay = TimeSpan.FromSeconds(Math.Min(exponentialSeconds, RateLimitMaxDelay.TotalSeconds));
            delaySource = "exponential";
            return cappedDelay + BuildJitter(RateLimitJitterMaxDelay);
        }

        private static bool TryGetRetryAfterDelay(HttpResponseMessage response, out TimeSpan retryDelay)
        {
            retryDelay = TimeSpan.Zero;

            var retryAfter = response?.Headers?.RetryAfter;
            if (retryAfter == null)
            {
                return false;
            }

            if (retryAfter.Delta.HasValue && retryAfter.Delta.Value > TimeSpan.Zero)
            {
                retryDelay = retryAfter.Delta.Value;
                return true;
            }

            if (retryAfter.Date.HasValue)
            {
                var delay = retryAfter.Date.Value.UtcDateTime - DateTime.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    retryDelay = delay;
                    return true;
                }
            }

            return false;
        }

        private static TimeSpan BuildJitter(TimeSpan maxJitter)
        {
            if (maxJitter <= TimeSpan.Zero)
            {
                return TimeSpan.Zero;
            }

            return TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * maxJitter.TotalMilliseconds);
        }

        private void SetNextRetryAtUtc(string nextRetryAtUtc)
        {
            lock (_sync)
            {
                _nextRetryAtUtc = nextRetryAtUtc ?? "";
            }

            UpdateSnapshot(ReadSnapshotFromDatabase());
        }

        private int DetermineStartupSequenceOverlap(out string reason)
        {
            var latestCompleteDayUtc = _metadataRepository.GetValue("latest_complete_day_utc")?.Trim() ?? "";
            var currentUtcDay = DateTime.UtcNow.Date;

            if (TryParseDayUtc(latestCompleteDayUtc, out var latestCompleteDay) &&
                latestCompleteDay < currentUtcDay)
            {
                reason = $"bridge_since_archive_day_{latestCompleteDayUtc}";
                return CurrentDayBridgeSequenceOverlap;
            }

            if (string.IsNullOrWhiteSpace(latestCompleteDayUtc))
            {
                reason = "no_archive_checkpoint";
                return CurrentDayBridgeSequenceOverlap;
            }

            reason = $"default_since_archive_day_{latestCompleteDayUtc}";
            return DefaultStartupSequenceOverlap;
        }

        private static bool TryParseDayUtc(string dayUtc, out DateTime day)
        {
            return DateTime.TryParseExact(
                dayUtc,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out day);
        }

        private static bool TryExtractSequenceEnvelope(
            string payload,
            long requestedSequenceId,
            out SequenceEnvelope envelope,
            out string error)
        {
            envelope = new SequenceEnvelope();
            error = "";

            if (string.IsNullOrWhiteSpace(payload))
            {
                error = "Sequence payload was empty.";
                return false;
            }

            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "Sequence payload was not a JSON object.";
                return false;
            }

            envelope.SequenceId = TryReadLong(root, "sequence_id", out var parsedSequenceId)
                ? parsedSequenceId
                : TryReadLong(root, "sequence", out parsedSequenceId)
                    ? parsedSequenceId
                    : requestedSequenceId;

            envelope.KillmailHash = TryReadString(root, "hash") ??
                                    TryReadNestedString(root, "zkb", "hash") ??
                                    "";
            envelope.UploadedAtUtc = TryReadString(root, "uploaded_at") ??
                                     TryReadString(root, "sequence_updated") ??
                                     "";

            JsonElement killmailElement;
            if (root.TryGetProperty("killmail", out killmailElement) && killmailElement.ValueKind == JsonValueKind.Object)
            {
                envelope.KillmailJson = killmailElement.GetRawText();
                envelope.KillmailId = TryReadLongAsString(killmailElement, "killmail_id");
            }
            else if (root.TryGetProperty("esi", out killmailElement) && killmailElement.ValueKind == JsonValueKind.Object)
            {
                envelope.KillmailJson = killmailElement.GetRawText();
                envelope.KillmailId = TryReadLongAsString(killmailElement, "killmail_id");
            }
            else if (root.TryGetProperty("killmail_esi", out killmailElement) && killmailElement.ValueKind == JsonValueKind.Object)
            {
                envelope.KillmailJson = killmailElement.GetRawText();
                envelope.KillmailId = TryReadLongAsString(killmailElement, "killmail_id");
            }
            else if (root.TryGetProperty("victim", out _) && root.TryGetProperty("killmail_time", out _))
            {
                envelope.KillmailJson = root.GetRawText();
                envelope.KillmailId = TryReadLongAsString(root, "killmail_id");
            }

            if (string.IsNullOrWhiteSpace(envelope.KillmailId))
            {
                envelope.KillmailId = TryReadLongAsString(root, "killmail_id");
            }

            if (string.IsNullOrWhiteSpace(envelope.KillmailJson) || string.IsNullOrWhiteSpace(envelope.KillmailId))
            {
                error = "Sequence payload did not contain a usable killmail envelope.";
                return false;
            }

            return true;
        }

        private static bool TryReadLong(JsonElement element, string propertyName, out long value)
        {
            value = 0;

            if (!element.TryGetProperty(propertyName, out var property))
            {
                return false;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out value))
            {
                return true;
            }

            if (property.ValueKind == JsonValueKind.String &&
                long.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            return false;
        }

        private static string TryReadLongAsString(JsonElement element, string propertyName)
        {
            return TryReadLong(element, propertyName, out var value)
                ? value.ToString(CultureInfo.InvariantCulture)
                : "";
        }

        private static string? TryReadString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return property.GetString();
        }

        private static string? TryReadNestedString(JsonElement element, string outerPropertyName, string innerPropertyName)
        {
            if (!element.TryGetProperty(outerPropertyName, out var outer) || outer.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return TryReadString(outer, innerPropertyName);
        }

        private sealed class LiveFeedStateRow
        {
            public string FeedName { get; set; } = "r2z2";
            public int Enabled { get; set; }
            public long? NextSequenceId { get; set; }
            public long? LastProcessedSequenceId { get; set; }
            public string LastSuccessAtUtc { get; set; } = "";
            public string Last404AtUtc { get; set; } = "";
            public string LastErrorAtUtc { get; set; } = "";
            public string LastError { get; set; } = "";
            public string Status { get; set; } = "Disabled";
            public string UpdatedAtUtc { get; set; } = "";
        }

        private sealed class SequenceEnvelope
        {
            public long SequenceId { get; set; }
            public string KillmailId { get; set; } = "";
            public string KillmailHash { get; set; } = "";
            public string UploadedAtUtc { get; set; } = "";
            public string KillmailJson { get; set; } = "";
        }

        private sealed class LiveProcessResult
        {
            public bool Success { get; set; }
            public bool WasDuplicate { get; set; }
            public string KillmailId { get; set; } = "";
            public string DayUtc { get; set; } = "";
            public string Error { get; set; } = "";
        }
    }
}
