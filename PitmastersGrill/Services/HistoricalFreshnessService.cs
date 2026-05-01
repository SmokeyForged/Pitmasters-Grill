using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersGrill.Services
{
    public sealed class HistoricalFreshnessService
    {
        private const string HistoricalFreshnessSource = "zkill_historical_freshness";
        private const string ManualRunMode = "Manual";
        private const string BackgroundRunMode = "Background";
        private const int DefaultCompletedDayWindow = 3;
        private const int DefaultBackgroundDelaySeconds = 30;
        private const int DefaultBackgroundCooldownHours = 12;
        private const int DefaultBackgroundMaxPilotsPerRun = 50;
        private const int DefaultBackgroundRecentPilotWindowDays = 14;
        private static readonly TimeSpan ZkillEntityDelay = TimeSpan.FromSeconds(1);

        private readonly object _sync = new();
        private readonly SemaphoreSlim _runGate = new(1, 1);
        private readonly KillmailIncrementalImportService _incrementalImportService;
        private readonly ZkillFreshnessClient _zkillFreshnessClient;
        private readonly AppSettingsService _appSettingsService;
        private readonly WatchedPilotRepository _watchedPilotRepository;
        private readonly PilotRegistryDayRepository _pilotRegistryDayRepository;
        private readonly HistoricalFreshnessCheckpointRepository _checkpointRepository;
        private HistoricalFreshnessSnapshot _snapshot = new();

        public HistoricalFreshnessService(
            KillmailIncrementalImportService incrementalImportService,
            AppSettingsService appSettingsService)
        {
            _incrementalImportService = incrementalImportService ?? throw new ArgumentNullException(nameof(incrementalImportService));
            _appSettingsService = appSettingsService ?? throw new ArgumentNullException(nameof(appSettingsService));
            _zkillFreshnessClient = new ZkillFreshnessClient();
            _watchedPilotRepository = new WatchedPilotRepository(AppPaths.GetDatabasePath());
            _pilotRegistryDayRepository = new PilotRegistryDayRepository(KillmailPaths.GetKillmailDatabasePath());
            _checkpointRepository = new HistoricalFreshnessCheckpointRepository(KillmailPaths.GetKillmailDatabasePath());
        }

        public event Action? StatusChanged;

        public HistoricalFreshnessSnapshot GetSnapshot()
        {
            lock (_sync)
            {
                return CloneSnapshot(_snapshot);
            }
        }

        public BackgroundHistoricalRepairConfiguration GetBackgroundStartupConfiguration()
        {
            var settings = _appSettingsService.Load();
            return new BackgroundHistoricalRepairConfiguration
            {
                Enabled = settings.BackgroundHistoricalRepairEnabled,
                DelaySeconds = settings.BackgroundHistoricalRepairDelaySeconds <= 0
                    ? DefaultBackgroundDelaySeconds
                    : settings.BackgroundHistoricalRepairDelaySeconds,
                CooldownHours = settings.BackgroundHistoricalRepairCooldownHours <= 0
                    ? DefaultBackgroundCooldownHours
                    : settings.BackgroundHistoricalRepairCooldownHours,
                LookbackDays = settings.BackgroundHistoricalRepairLookbackDays <= 0
                    ? DefaultCompletedDayWindow
                    : settings.BackgroundHistoricalRepairLookbackDays,
                MaxPilotsPerRun = settings.BackgroundHistoricalRepairMaxPilotsPerRun <= 0
                    ? DefaultBackgroundMaxPilotsPerRun
                    : settings.BackgroundHistoricalRepairMaxPilotsPerRun,
                RecentPilotWindowDays = settings.BackgroundHistoricalRepairRecentPilotWindowDays <= 0
                    ? DefaultBackgroundRecentPilotWindowDays
                    : settings.BackgroundHistoricalRepairRecentPilotWindowDays
            };
        }

        public Task<HistoricalFreshnessRunResult> RunAsync(IReadOnlyCollection<long> characterIds, CancellationToken cancellationToken)
        {
            var targets = NormalizeCharacterIds(characterIds);
            var options = new HistoricalFreshnessExecutionOptions
            {
                Mode = ManualRunMode,
                CharacterIds = targets,
                VisiblePilotsTargeted = targets.Count,
                CandidatePilotsConsidered = targets.Count,
                HistoricalDaysChecked = DefaultCompletedDayWindow,
                QueryPastSeconds = BuildPastSeconds(DefaultCompletedDayWindow),
                BypassCooldown = true,
                WaitOnRateLimit = true
            };

            return RunInternalAsync(options, cancellationToken);
        }

        public Task<HistoricalFreshnessRunResult> RunBackgroundStartupRepairAsync(
            IReadOnlyCollection<long> visibleCharacterIds,
            CancellationToken cancellationToken)
        {
            var configuration = GetBackgroundStartupConfiguration();
            var visibleTargets = NormalizeCharacterIds(visibleCharacterIds);
            var candidates = BuildBackgroundCandidatePool(visibleTargets, configuration);

            var options = new HistoricalFreshnessExecutionOptions
            {
                Mode = BackgroundRunMode,
                CharacterIds = candidates,
                VisiblePilotsTargeted = visibleTargets.Count,
                CandidatePilotsConsidered = candidates.Count,
                HistoricalDaysChecked = configuration.LookbackDays,
                QueryPastSeconds = BuildPastSeconds(configuration.LookbackDays),
                BypassCooldown = false,
                CooldownHours = configuration.CooldownHours,
                WaitOnRateLimit = false
            };

            return RunInternalAsync(options, cancellationToken);
        }

        public Task<HistoricalFreshnessRunResult> RunBackgroundStartupRepairForCandidatesAsync(
            IReadOnlyCollection<long> characterIds,
            CancellationToken cancellationToken)
        {
            var configuration = GetBackgroundStartupConfiguration();
            var candidates = NormalizeCharacterIds(characterIds);
            var options = new HistoricalFreshnessExecutionOptions
            {
                Mode = BackgroundRunMode,
                CharacterIds = candidates,
                VisiblePilotsTargeted = candidates.Count,
                CandidatePilotsConsidered = candidates.Count,
                HistoricalDaysChecked = configuration.LookbackDays,
                QueryPastSeconds = BuildPastSeconds(configuration.LookbackDays),
                BypassCooldown = false,
                CooldownHours = configuration.CooldownHours,
                WaitOnRateLimit = false
            };

            return RunInternalAsync(options, cancellationToken);
        }

        private async Task<HistoricalFreshnessRunResult> RunInternalAsync(
            HistoricalFreshnessExecutionOptions options,
            CancellationToken cancellationToken)
        {
            if (!await _runGate.WaitAsync(0, cancellationToken))
            {
                AppLogger.KillmailImportInfo(
                    $"Historical Freshness start skipped because another run is already active. mode={options.Mode}");

                return new HistoricalFreshnessRunResult
                {
                    Success = false,
                    Mode = options.Mode,
                    VisiblePilotsTargeted = options.VisiblePilotsTargeted,
                    CandidatePilotsConsidered = options.CandidatePilotsConsidered,
                    HistoricalDaysChecked = options.HistoricalDaysChecked,
                    DetailText = "A Historical Freshness run is already active.",
                    LastError = "Historical Freshness already running."
                };
            }

            try
            {
                var targets = options.CharacterIds ?? new List<long>();
                var targetDays = BuildTargetDays(options.HistoricalDaysChecked);
                var targetDaySet = new HashSet<string>(targetDays, StringComparer.OrdinalIgnoreCase);
                var windowStartDayUtc = targetDays.Count > 0 ? targetDays[targetDays.Count - 1] : "";
                var windowEndDayUtc = targetDays.Count > 0 ? targetDays[0] : "";

                if (targets.Count == 0)
                {
                    var emptyDetailText = string.Equals(options.Mode, BackgroundRunMode, StringComparison.OrdinalIgnoreCase)
                        ? "No local background historical repair candidates were available."
                        : "No visible pilot character IDs were available for Historical Freshness.";

                    var emptyResult = new HistoricalFreshnessRunResult
                    {
                        Success = true,
                        Mode = options.Mode,
                        VisiblePilotsTargeted = options.VisiblePilotsTargeted,
                        CandidatePilotsConsidered = options.CandidatePilotsConsidered,
                        HistoricalDaysChecked = targetDays.Count,
                        DetailText = emptyDetailText,
                        TargetDays = targetDays
                    };

                    UpdateSnapshot(snapshot =>
                    {
                        snapshot.Status = "Completed";
                        snapshot.Mode = options.Mode;
                        snapshot.VisiblePilotsTargeted = options.VisiblePilotsTargeted;
                        snapshot.CandidatePilotsConsidered = options.CandidatePilotsConsidered;
                        snapshot.CandidatePilotsSkippedCooldown = 0;
                        snapshot.PilotsChecked = 0;
                        snapshot.HistoricalDaysChecked = targetDays.Count;
                        snapshot.EntitiesQueried = 0;
                        snapshot.ZkillResultsFound = 0;
                        snapshot.AlreadyKnownCount = 0;
                        snapshot.MissingImportedCount = 0;
                        snapshot.FailedCount = 0;
                        snapshot.LastRunAtUtc = DateTime.UtcNow.ToString("o");
                        snapshot.LastError = "";
                        snapshot.NextRetryAtUtc = "";
                        snapshot.DetailText = emptyDetailText;
                    });

                    return emptyResult;
                }

                AppLogger.KillmailImportInfo(
                    $"Historical Freshness started. mode={options.Mode} visiblePilots={options.VisiblePilotsTargeted} candidatePilots={options.CandidatePilotsConsidered} targetDays={string.Join(",", targetDays)} queryWindowPastSeconds={options.QueryPastSeconds}");

                UpdateSnapshot(snapshot =>
                {
                    snapshot.Status = "Running";
                    snapshot.Mode = options.Mode;
                    snapshot.VisiblePilotsTargeted = options.VisiblePilotsTargeted;
                    snapshot.CandidatePilotsConsidered = options.CandidatePilotsConsidered;
                    snapshot.CandidatePilotsSkippedCooldown = 0;
                    snapshot.PilotsChecked = 0;
                    snapshot.HistoricalDaysChecked = targetDays.Count;
                    snapshot.EntitiesQueried = 0;
                    snapshot.ZkillResultsFound = 0;
                    snapshot.AlreadyKnownCount = 0;
                    snapshot.MissingImportedCount = 0;
                    snapshot.FailedCount = 0;
                    snapshot.LastError = "";
                    snapshot.NextRetryAtUtc = "";
                    snapshot.DetailText = $"Checking completed days {string.Join(", ", targetDays)} for {targets.Count} pilot candidates.";
                });

                var result = new HistoricalFreshnessRunResult
                {
                    Success = true,
                    Mode = options.Mode,
                    VisiblePilotsTargeted = options.VisiblePilotsTargeted,
                    CandidatePilotsConsidered = options.CandidatePilotsConsidered,
                    HistoricalDaysChecked = targetDays.Count,
                    TargetDays = targetDays
                };

                var utcNow = DateTime.UtcNow;
                var queryIndex = 0;
                foreach (var characterId in targets)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    queryIndex++;

                    if (!options.BypassCooldown)
                    {
                        var checkpoint = _checkpointRepository.Get(characterId, windowStartDayUtc, windowEndDayUtc);
                        if (HistoricalFreshnessCheckpointRepository.WasCheckedWithinCooldown(checkpoint, utcNow, options.CooldownHours))
                        {
                            result.CandidatePilotsSkippedCooldown++;
                            AppLogger.KillmailImportDebug(
                                $"Historical Freshness checkpoint skip. mode={options.Mode} characterId={characterId} windowStart={windowStartDayUtc} windowEnd={windowEndDayUtc} cooldownHours={options.CooldownHours}");
                            UpdateSnapshot(snapshot =>
                            {
                                snapshot.CandidatePilotsSkippedCooldown = result.CandidatePilotsSkippedCooldown;
                            });
                            continue;
                        }
                    }

                    result.PilotsChecked++;
                    var characterResult = await ProcessCharacterAsync(
                        characterId,
                        queryIndex,
                        targets.Count,
                        options,
                        targetDaySet,
                        cancellationToken);

                    result.EntitiesQueried += characterResult.EntitiesQueried;
                    result.ZkillResultsFound += characterResult.ZkillResultsFound;
                    result.AlreadyKnownCount += characterResult.AlreadyKnownCount;
                    result.MissingImportedCount += characterResult.MissingImportedCount;
                    result.FailedCount += characterResult.FailedCount;

                    if (!string.IsNullOrWhiteSpace(characterResult.LastError))
                    {
                        result.LastError = characterResult.LastError;
                        result.Success = false;
                    }

                    _checkpointRepository.Upsert(new HistoricalFreshnessCheckpointRecord
                    {
                        CharacterId = characterId,
                        WindowStartDayUtc = windowStartDayUtc,
                        WindowEndDayUtc = windowEndDayUtc,
                        LastCheckedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                        LastStatus = characterResult.Status,
                        LastImportedCount = characterResult.MissingImportedCount,
                        LastKnownCount = characterResult.AlreadyKnownCount,
                        LastFailedCount = characterResult.FailedCount,
                        LastError = characterResult.LastError ?? ""
                    });

                    UpdateSnapshot(snapshot =>
                    {
                        snapshot.PilotsChecked = result.PilotsChecked;
                        snapshot.EntitiesQueried = result.EntitiesQueried;
                        snapshot.ZkillResultsFound = result.ZkillResultsFound;
                        snapshot.AlreadyKnownCount = result.AlreadyKnownCount;
                        snapshot.MissingImportedCount = result.MissingImportedCount;
                        snapshot.FailedCount = result.FailedCount;
                        snapshot.LastError = result.LastError ?? "";
                    });

                    if (characterResult.RateLimited)
                    {
                        result.Success = false;
                        UpdateSnapshot(snapshot =>
                        {
                            snapshot.Status = "Backing off / rate limited";
                            snapshot.NextRetryAtUtc = characterResult.NextRetryAtUtc;
                            snapshot.DetailText = string.Equals(options.Mode, BackgroundRunMode, StringComparison.OrdinalIgnoreCase)
                                ? "Background historical repair hit zKill rate limiting and stopped politely."
                                : "Historical Freshness hit zKill rate limiting and paused politely.";
                        });

                        if (options.WaitOnRateLimit && characterResult.RetryDelay.HasValue)
                        {
                            await Task.Delay(characterResult.RetryDelay.Value, cancellationToken);
                        }

                        var rateLimitedDetail = string.Equals(options.Mode, BackgroundRunMode, StringComparison.OrdinalIgnoreCase)
                            ? $"Background historical repair stopped after zKill rate limiting while checking pilot {result.PilotsChecked} of {targets.Count}."
                            : $"Historical Freshness stopped after a zKill rate limit while checking visible pilot {result.PilotsChecked} of {targets.Count}.";
                        FinalizeSnapshot("Completed with failures", result, rateLimitedDetail);
                        result.DetailText = rateLimitedDetail;
                        return result;
                    }
                }

                var finalStatus = result.FailedCount > 0 ? "Completed with failures" : "Completed";
                var detailText = BuildCompletionDetail(options.Mode, targetDays.Count, result);
                if (!options.BypassCooldown &&
                    result.PilotsChecked == 0 &&
                    result.CandidatePilotsSkippedCooldown > 0)
                {
                    detailText = string.Equals(options.Mode, BackgroundRunMode, StringComparison.OrdinalIgnoreCase)
                        ? "Background historical repair skipped because all candidate pilots were still inside cooldown."
                        : "Historical Freshness skipped because all candidate pilots were still inside cooldown.";
                }

                FinalizeSnapshot(finalStatus, result, detailText);

                AppLogger.KillmailImportInfo(
                    $"Historical Freshness complete. mode={options.Mode} candidatePilots={result.CandidatePilotsConsidered} skippedCooldown={result.CandidatePilotsSkippedCooldown} pilotsChecked={result.PilotsChecked} historicalDaysChecked={targetDays.Count} entitiesQueried={result.EntitiesQueried} zkillResultsFound={result.ZkillResultsFound} alreadyKnown={result.AlreadyKnownCount} imported={result.MissingImportedCount} failed={result.FailedCount}");

                result.DetailText = detailText;
                return result;
            }
            catch (OperationCanceledException)
            {
                var cancelled = new HistoricalFreshnessRunResult
                {
                    Success = false,
                    Cancelled = true,
                    DetailText = "Historical Freshness cancelled."
                };

                UpdateSnapshot(snapshot =>
                {
                    snapshot.Status = "Cancelled";
                    snapshot.NextRetryAtUtc = "";
                    snapshot.DetailText = cancelled.DetailText;
                    snapshot.LastRunAtUtc = DateTime.UtcNow.ToString("o");
                });

                AppLogger.KillmailImportInfo("Historical Freshness cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                AppLogger.KillmailImportWarn($"Historical Freshness failed. message={ex.Message}");
                AppLogger.ErrorOnly("Historical Freshness exception.", ex);

                UpdateSnapshot(snapshot =>
                {
                    snapshot.Status = "Error";
                    snapshot.LastError = ex.Message;
                    snapshot.NextRetryAtUtc = "";
                    snapshot.LastRunAtUtc = DateTime.UtcNow.ToString("o");
                    snapshot.DetailText = "Historical Freshness failed.";
                });

                return new HistoricalFreshnessRunResult
                {
                    Success = false,
                    FailedCount = 1,
                    LastError = ex.Message,
                    DetailText = "Historical Freshness failed."
                };
            }
            finally
            {
                _runGate.Release();
            }
        }

        private async Task<HistoricalFreshnessCharacterRunResult> ProcessCharacterAsync(
            long characterId,
            int queryIndex,
            int totalTargets,
            HistoricalFreshnessExecutionOptions options,
            HashSet<string> targetDaySet,
            CancellationToken cancellationToken)
        {
            var result = new HistoricalFreshnessCharacterRunResult();
            var candidateKillmails = new Dictionary<long, ZkillKillmailRef>();

            foreach (var losses in new[] { false, true })
            {
                AppLogger.KillmailImportDebug(
                    $"Historical Freshness querying entity. mode={options.Mode} index={queryIndex} total={totalTargets} characterId={characterId} pastSeconds={options.QueryPastSeconds} losses={losses}");

                UpdateSnapshot(snapshot =>
                {
                    snapshot.Status = "Running";
                    snapshot.DetailText = $"Checking pilot {queryIndex} of {totalTargets} across the last {options.HistoricalDaysChecked} completed day(s).";
                });

                var queryResult = await _zkillFreshnessClient.QueryPastSecondsCharacterAsync(
                    characterId,
                    losses,
                    options.QueryPastSeconds,
                    cancellationToken);
                result.EntitiesQueried++;

                if (queryResult.IsRateLimited)
                {
                    result.FailedCount++;
                    result.LastError = queryResult.Error;
                    result.RateLimited = true;
                    result.RetryDelay = queryResult.RetryDelay;
                    result.NextRetryAtUtc = queryResult.NextRetryAtUtc;
                    result.Status = "RateLimited";
                    return result;
                }

                if (!queryResult.Success)
                {
                    result.FailedCount++;
                    result.LastError = queryResult.Error;
                    result.Status = "CompletedWithFailures";
                    AppLogger.KillmailImportWarn(
                        $"Historical Freshness entity query failed. mode={options.Mode} characterId={characterId} pastSeconds={options.QueryPastSeconds} losses={losses} error={queryResult.Error}");
                }
                else
                {
                    foreach (var killmail in queryResult.Killmails)
                    {
                        if (!candidateKillmails.ContainsKey(killmail.KillmailId))
                        {
                            candidateKillmails[killmail.KillmailId] = killmail;
                        }
                    }

                    AppLogger.KillmailImportDebug(
                        $"Historical Freshness entity results. mode={options.Mode} characterId={characterId} pastSeconds={options.QueryPastSeconds} losses={losses} rawResultCount={queryResult.Killmails.Count}");
                }

                await Task.Delay(ZkillEntityDelay, cancellationToken);
            }

            foreach (var killmailRef in candidateKillmails.Values.OrderByDescending(item => item.KillmailId))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var seenRecord = await Task.Run(
                    () => _incrementalImportService.GetSeenRecord(killmailRef.KillmailId),
                    cancellationToken);

                if (seenRecord != null &&
                    !string.Equals(seenRecord.ProcessingStatus, "error", StringComparison.OrdinalIgnoreCase))
                {
                    if (targetDaySet.Contains(seenRecord.DayUtc))
                    {
                        result.ZkillResultsFound++;
                        result.AlreadyKnownCount++;
                    }

                    continue;
                }

                var killmailFetch = await _zkillFreshnessClient.FetchFullKillmailAsync(killmailRef, cancellationToken);
                if (!killmailFetch.Success)
                {
                    result.FailedCount++;
                    result.LastError = killmailFetch.Error;
                    result.Status = "CompletedWithFailures";

                    await Task.Run(() => _incrementalImportService.RecordFailure(
                        killmailRef.KillmailId,
                        killmailRef.KillmailHash,
                        sequenceId: 0,
                        source: HistoricalFreshnessSource,
                        uploadedAtUtc: "",
                        killmailTimeUtc: killmailRef.KillmailTimeUtc,
                        dayUtc: killmailRef.DayUtc,
                        error: killmailFetch.Error,
                        cancellationToken: cancellationToken), cancellationToken);
                    continue;
                }

                var killmailDayUtc = TryReadKillmailDayUtc(killmailFetch.KillmailJson);
                if (!targetDaySet.Contains(killmailDayUtc))
                {
                    AppLogger.KillmailImportDebug(
                        $"Historical Freshness skipped non-target day killmail. mode={options.Mode} killmailId={killmailRef.KillmailId} dayUtc={killmailDayUtc}");
                    continue;
                }

                result.ZkillResultsFound++;

                var importResult = await Task.Run(
                    () => _incrementalImportService.ImportKillmailJson(new IncrementalKillmailImportRequest
                    {
                        KillmailId = killmailRef.KillmailId,
                        KillmailHash = killmailRef.KillmailHash,
                        KillmailJson = killmailFetch.KillmailJson,
                        Source = HistoricalFreshnessSource,
                        SequenceId = 0,
                        UploadedAtUtc = ""
                    }, cancellationToken),
                    cancellationToken);

                AppLogger.KillmailImportDebug(
                    $"Historical Freshness derived observations. mode={options.Mode} killmailId={killmailRef.KillmailId} registry={importResult.RegistryObservationCount} fleet={importResult.FleetObservationCount} ship={importResult.ShipObservationCount} cyno={importResult.CynoObservationCount} bait={importResult.BaitObservationCount} tackle={importResult.TackleObservationCount}");

                if (!importResult.Success)
                {
                    result.FailedCount++;
                    result.LastError = importResult.Error;
                    result.Status = "CompletedWithFailures";
                    continue;
                }

                if (importResult.WasDuplicate)
                {
                    result.AlreadyKnownCount++;
                }
                else
                {
                    result.MissingImportedCount++;
                }
            }

            if (string.IsNullOrWhiteSpace(result.Status))
            {
                result.Status = result.FailedCount > 0 ? "CompletedWithFailures" : "Completed";
            }

            return result;
        }

        private List<long> BuildBackgroundCandidatePool(
            IReadOnlyCollection<long> visibleCharacterIds,
            BackgroundHistoricalRepairConfiguration configuration)
        {
            var candidateIds = new List<long>();
            var seen = new HashSet<long>();
            var limit = configuration.MaxPilotsPerRun <= 0 ? DefaultBackgroundMaxPilotsPerRun : configuration.MaxPilotsPerRun;

            void AddRange(IEnumerable<long> ids)
            {
                foreach (var id in ids)
                {
                    if (id <= 0 || !seen.Add(id))
                    {
                        continue;
                    }

                    candidateIds.Add(id);
                    if (candidateIds.Count >= limit)
                    {
                        return;
                    }
                }
            }

            var watchedIds = _watchedPilotRepository.GetWatchedCharacterIds(limit);
            AddRange(watchedIds);

            if (candidateIds.Count < limit)
            {
                AddRange(visibleCharacterIds ?? Array.Empty<long>());
            }

            if (candidateIds.Count < limit)
            {
                var recentSinceUtc = DateTime.UtcNow.AddDays(-Math.Max(1, configuration.RecentPilotWindowDays));
                var recentLocalIds = _pilotRegistryDayRepository.GetRecentlySeenCharacterIds(recentSinceUtc, limit * 3);
                AddRange(recentLocalIds);
            }

            AppLogger.KillmailImportDebug(
                $"Historical Freshness candidate pool built. watched={watchedIds.Count} visible={visibleCharacterIds?.Count ?? 0} finalCandidates={candidateIds.Count} maxPilots={limit} recentPilotWindowDays={configuration.RecentPilotWindowDays}");

            return candidateIds;
        }

        private static string TryReadKillmailDayUtc(string killmailJson)
        {
            if (string.IsNullOrWhiteSpace(killmailJson))
            {
                return string.Empty;
            }

            try
            {
                using var document = JsonDocument.Parse(killmailJson);
                if (!document.RootElement.TryGetProperty("killmail_time", out var property) ||
                    property.ValueKind != JsonValueKind.String)
                {
                    return string.Empty;
                }

                var text = property.GetString();
                if (string.IsNullOrWhiteSpace(text) ||
                    !DateTime.TryParse(
                        text,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                        out var parsed))
                {
                    return string.Empty;
                }

                return DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
                    .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static List<long> NormalizeCharacterIds(IReadOnlyCollection<long> characterIds)
        {
            return characterIds?
                .Where(id => id > 0)
                .Distinct()
                .OrderBy(id => id)
                .ToList() ?? new List<long>();
        }

        private static List<string> BuildTargetDays(int completedDayWindow)
        {
            var window = completedDayWindow <= 0 ? DefaultCompletedDayWindow : completedDayWindow;
            var todayUtc = DateTime.UtcNow.Date;
            return Enumerable.Range(1, window)
                .Select(offset => todayUtc.AddDays(-offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .ToList();
        }

        private static int BuildPastSeconds(int completedDayWindow)
        {
            var window = completedDayWindow <= 0 ? DefaultCompletedDayWindow : completedDayWindow;
            return (window + 1) * 24 * 60 * 60;
        }

        private static string BuildCompletionDetail(string mode, int dayCount, HistoricalFreshnessRunResult result)
        {
            if (string.Equals(mode, BackgroundRunMode, StringComparison.OrdinalIgnoreCase))
            {
                return $"Background historical repair checked {result.PilotsChecked} pilot candidates across {dayCount} completed day(s) and imported {result.MissingImportedCount} missing killmail(s).";
            }

            return $"Historical Freshness checked {dayCount} completed day(s) for {result.VisiblePilotsTargeted} visible pilots and imported {result.MissingImportedCount} missing killmail(s).";
        }

        private void FinalizeSnapshot(string status, HistoricalFreshnessRunResult result, string detailText)
        {
            UpdateSnapshot(snapshot =>
            {
                snapshot.Status = status;
                snapshot.Mode = result.Mode;
                snapshot.VisiblePilotsTargeted = result.VisiblePilotsTargeted;
                snapshot.CandidatePilotsConsidered = result.CandidatePilotsConsidered;
                snapshot.CandidatePilotsSkippedCooldown = result.CandidatePilotsSkippedCooldown;
                snapshot.PilotsChecked = result.PilotsChecked;
                snapshot.HistoricalDaysChecked = result.HistoricalDaysChecked;
                snapshot.EntitiesQueried = result.EntitiesQueried;
                snapshot.ZkillResultsFound = result.ZkillResultsFound;
                snapshot.AlreadyKnownCount = result.AlreadyKnownCount;
                snapshot.MissingImportedCount = result.MissingImportedCount;
                snapshot.FailedCount = result.FailedCount;
                snapshot.LastRunAtUtc = DateTime.UtcNow.ToString("o");
                snapshot.LastError = result.LastError ?? "";
                snapshot.NextRetryAtUtc = "";
                snapshot.DetailText = detailText;
            });
        }

        private void UpdateSnapshot(Action<HistoricalFreshnessSnapshot> update)
        {
            lock (_sync)
            {
                var snapshot = CloneSnapshot(_snapshot);
                update(snapshot);
                _snapshot = snapshot;
            }

            StatusChanged?.Invoke();
        }

        private static HistoricalFreshnessSnapshot CloneSnapshot(HistoricalFreshnessSnapshot snapshot)
        {
            return new HistoricalFreshnessSnapshot
            {
                Status = snapshot.Status,
                Mode = snapshot.Mode,
                CandidatePilotsConsidered = snapshot.CandidatePilotsConsidered,
                CandidatePilotsSkippedCooldown = snapshot.CandidatePilotsSkippedCooldown,
                PilotsChecked = snapshot.PilotsChecked,
                VisiblePilotsTargeted = snapshot.VisiblePilotsTargeted,
                HistoricalDaysChecked = snapshot.HistoricalDaysChecked,
                EntitiesQueried = snapshot.EntitiesQueried,
                ZkillResultsFound = snapshot.ZkillResultsFound,
                AlreadyKnownCount = snapshot.AlreadyKnownCount,
                MissingImportedCount = snapshot.MissingImportedCount,
                FailedCount = snapshot.FailedCount,
                LastRunAtUtc = snapshot.LastRunAtUtc,
                LastError = snapshot.LastError,
                NextRetryAtUtc = snapshot.NextRetryAtUtc,
                DetailText = snapshot.DetailText
            };
        }
    }

    public sealed class BackgroundHistoricalRepairConfiguration
    {
        public bool Enabled { get; set; } = true;
        public int DelaySeconds { get; set; } = DefaultDelaySeconds;
        public int CooldownHours { get; set; } = DefaultCooldownHours;
        public int LookbackDays { get; set; } = DefaultLookbackDays;
        public int MaxPilotsPerRun { get; set; } = DefaultMaxPilotsPerRun;
        public int RecentPilotWindowDays { get; set; } = DefaultRecentPilotWindowDays;

        public const int DefaultDelaySeconds = 30;
        public const int DefaultCooldownHours = 12;
        public const int DefaultLookbackDays = 3;
        public const int DefaultMaxPilotsPerRun = 50;
        public const int DefaultRecentPilotWindowDays = 14;
    }

    internal sealed class HistoricalFreshnessExecutionOptions
    {
        public string Mode { get; set; } = "";
        public List<long> CharacterIds { get; set; } = new();
        public int VisiblePilotsTargeted { get; set; }
        public int CandidatePilotsConsidered { get; set; }
        public int HistoricalDaysChecked { get; set; }
        public int QueryPastSeconds { get; set; }
        public bool BypassCooldown { get; set; }
        public int CooldownHours { get; set; }
        public bool WaitOnRateLimit { get; set; } = true;
    }

    internal sealed class HistoricalFreshnessCharacterRunResult
    {
        public int EntitiesQueried { get; set; }
        public int ZkillResultsFound { get; set; }
        public int AlreadyKnownCount { get; set; }
        public int MissingImportedCount { get; set; }
        public int FailedCount { get; set; }
        public string LastError { get; set; } = "";
        public string Status { get; set; } = "";
        public bool RateLimited { get; set; }
        public TimeSpan? RetryDelay { get; set; }
        public string NextRetryAtUtc { get; set; } = "";
    }

    public sealed class HistoricalFreshnessRunResult
    {
        public bool Success { get; set; }
        public bool Cancelled { get; set; }
        public string Mode { get; set; } = "";
        public int CandidatePilotsConsidered { get; set; }
        public int CandidatePilotsSkippedCooldown { get; set; }
        public int PilotsChecked { get; set; }
        public int VisiblePilotsTargeted { get; set; }
        public int HistoricalDaysChecked { get; set; }
        public int EntitiesQueried { get; set; }
        public int ZkillResultsFound { get; set; }
        public int AlreadyKnownCount { get; set; }
        public int MissingImportedCount { get; set; }
        public int FailedCount { get; set; }
        public string LastError { get; set; } = "";
        public string DetailText { get; set; } = "";
        public IReadOnlyList<string> TargetDays { get; set; } = Array.Empty<string>();
    }
}
