using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PitmastersGrill.Models;
using PitmastersGrill.Persistence;

namespace PitmastersGrill.Services
{
    public sealed class TodaysFreshnessService
    {
        private const string TodayFreshnessSource = "zkill_today_freshness";
        private const int FreshnessWindowPastSeconds = 36 * 60 * 60;
        private static readonly TimeSpan ZkillEntityDelay = TimeSpan.FromSeconds(1);

        private readonly object _sync = new();
        private readonly SemaphoreSlim _runGate = new(1, 1);
        private readonly KillmailIncrementalImportService _incrementalImportService;
        private readonly ZkillFreshnessClient _zkillFreshnessClient;
        private TodaysFreshnessSnapshot _snapshot = new();

        public TodaysFreshnessService(KillmailIncrementalImportService incrementalImportService)
        {
            _incrementalImportService = incrementalImportService ?? throw new ArgumentNullException(nameof(incrementalImportService));
            _zkillFreshnessClient = new ZkillFreshnessClient();
        }

        public event Action? StatusChanged;

        public TodaysFreshnessSnapshot GetSnapshot()
        {
            lock (_sync)
            {
                return CloneSnapshot(_snapshot);
            }
        }

        public async Task<TodaysFreshnessRunResult> RunAsync(IReadOnlyCollection<long> characterIds, CancellationToken cancellationToken)
        {
            await _runGate.WaitAsync(cancellationToken);
            try
            {
                var targets = characterIds?
                    .Where(id => id > 0)
                    .Distinct()
                    .OrderBy(id => id)
                    .ToList() ?? new List<long>();

                if (targets.Count == 0)
                {
                    var emptyResult = new TodaysFreshnessRunResult
                    {
                        Success = true,
                        VisiblePilotsTargeted = 0,
                        DetailText = "No visible pilot character IDs were available for Today's Freshness."
                    };

                    UpdateSnapshot(snapshot =>
                    {
                        snapshot.Status = "Completed";
                        snapshot.VisiblePilotsTargeted = 0;
                        snapshot.EntitiesQueried = 0;
                        snapshot.ZkillResultsFound = 0;
                        snapshot.AlreadyKnownCount = 0;
                        snapshot.NewKillmailsImported = 0;
                        snapshot.FailedCount = 0;
                        snapshot.LastRunAtUtc = DateTime.UtcNow.ToString("o");
                        snapshot.LastError = "";
                        snapshot.NextRetryAtUtc = "";
                        snapshot.DetailText = emptyResult.DetailText;
                    });

                    return emptyResult;
                }

                AppLogger.KillmailImportInfo(
                    $"Today's Freshness started. visiblePilots={targets.Count} queryWindowHours={FreshnessWindowPastSeconds / 3600}");

                UpdateSnapshot(snapshot =>
                {
                    snapshot.Status = "Running";
                    snapshot.VisiblePilotsTargeted = targets.Count;
                    snapshot.EntitiesQueried = 0;
                    snapshot.ZkillResultsFound = 0;
                    snapshot.AlreadyKnownCount = 0;
                    snapshot.NewKillmailsImported = 0;
                    snapshot.FailedCount = 0;
                    snapshot.LastError = "";
                    snapshot.NextRetryAtUtc = "";
                    snapshot.DetailText = "Querying zKill for visible pilots.";
                });

                var candidateKillmails = new Dictionary<long, ZkillKillmailRef>();
                var result = new TodaysFreshnessRunResult
                {
                    Success = true,
                    VisiblePilotsTargeted = targets.Count
                };

                var queryIndex = 0;
                foreach (var characterId in targets)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    queryIndex++;

                    AppLogger.KillmailImportDebug(
                        $"Today's Freshness querying entity. index={queryIndex} total={targets.Count} characterId={characterId}");

                    UpdateSnapshot(snapshot =>
                    {
                        snapshot.Status = "Running";
                        snapshot.EntitiesQueried = queryIndex;
                        snapshot.DetailText = $"Querying visible pilot {queryIndex} of {targets.Count}.";
                    });

                    foreach (var losses in new[] { false, true })
                    {
                        var queryResult = await _zkillFreshnessClient.QueryPastSecondsCharacterAsync(
                            characterId,
                            losses,
                            FreshnessWindowPastSeconds,
                            cancellationToken);
                        result.ZkillRequestsMade++;

                        if (queryResult.IsRateLimited)
                        {
                            result.FailedCount++;
                            result.Success = false;
                            result.LastError = queryResult.Error;

                            UpdateSnapshot(snapshot =>
                            {
                                snapshot.Status = "Backing off / rate limited";
                                snapshot.FailedCount = result.FailedCount;
                                snapshot.LastError = queryResult.Error;
                                snapshot.NextRetryAtUtc = queryResult.NextRetryAtUtc;
                                snapshot.DetailText = "Today's Freshness hit zKill rate limiting and paused politely.";
                            });

                            if (queryResult.RetryDelay.HasValue)
                            {
                                await Task.Delay(queryResult.RetryDelay.Value, cancellationToken);
                            }

                            FinalizeSnapshot(
                                status: "Completed with failures",
                                result,
                                $"Today's Freshness stopped after a zKill rate limit while querying visible pilot {queryIndex} of {targets.Count}.");
                            return result;
                        }

                        if (!queryResult.Success)
                        {
                            result.FailedCount++;
                            result.Success = false;
                            result.LastError = queryResult.Error;
                            AppLogger.KillmailImportWarn(
                                $"Today's Freshness entity query failed. characterId={characterId} losses={losses} error={queryResult.Error}");
                        }
                        else
                        {
                            result.ZkillResultsFound += queryResult.Killmails.Count;
                            foreach (var killmail in queryResult.Killmails)
                            {
                                if (!candidateKillmails.ContainsKey(killmail.KillmailId))
                                {
                                    candidateKillmails[killmail.KillmailId] = killmail;
                                }
                            }

                            AppLogger.KillmailImportDebug(
                                $"Today's Freshness entity results. characterId={characterId} losses={losses} resultCount={queryResult.Killmails.Count}");
                        }

                        UpdateSnapshot(snapshot =>
                        {
                            snapshot.ZkillResultsFound = result.ZkillResultsFound;
                            snapshot.FailedCount = result.FailedCount;
                            snapshot.LastError = result.LastError;
                        });

                        await Task.Delay(ZkillEntityDelay, cancellationToken);
                    }
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
                        result.AlreadyKnownCount++;
                        continue;
                    }

                    var killmailFetch = await _zkillFreshnessClient.FetchFullKillmailAsync(killmailRef, cancellationToken);
                    if (!killmailFetch.Success)
                    {
                        result.FailedCount++;
                        result.Success = false;
                        result.LastError = killmailFetch.Error;

                        await Task.Run(() => _incrementalImportService.RecordFailure(
                            killmailRef.KillmailId,
                            killmailRef.KillmailHash,
                            sequenceId: 0,
                            source: TodayFreshnessSource,
                            uploadedAtUtc: "",
                            killmailTimeUtc: "",
                            dayUtc: "",
                            error: killmailFetch.Error,
                            cancellationToken: cancellationToken), cancellationToken);

                        continue;
                    }

                    var importResult = await Task.Run(
                        () => _incrementalImportService.ImportKillmailJson(new IncrementalKillmailImportRequest
                        {
                            KillmailId = killmailRef.KillmailId,
                            KillmailHash = killmailRef.KillmailHash,
                            KillmailJson = killmailFetch.KillmailJson,
                            Source = TodayFreshnessSource,
                            SequenceId = 0,
                            UploadedAtUtc = ""
                        }, cancellationToken),
                        cancellationToken);

                    AppLogger.KillmailImportDebug(
                        $"Today's Freshness derived observations. killmailId={killmailRef.KillmailId} registry={importResult.RegistryObservationCount} fleet={importResult.FleetObservationCount} ship={importResult.ShipObservationCount} cyno={importResult.CynoObservationCount} bait={importResult.BaitObservationCount} tackle={importResult.TackleObservationCount}");

                    if (!importResult.Success)
                    {
                        result.FailedCount++;
                        result.Success = false;
                        result.LastError = importResult.Error;
                        continue;
                    }

                    if (importResult.WasDuplicate)
                    {
                        result.AlreadyKnownCount++;
                    }
                    else
                    {
                        result.NewKillmailsImported++;
                    }

                    UpdateSnapshot(snapshot =>
                    {
                        snapshot.AlreadyKnownCount = result.AlreadyKnownCount;
                        snapshot.NewKillmailsImported = result.NewKillmailsImported;
                        snapshot.FailedCount = result.FailedCount;
                        snapshot.LastError = result.LastError;
                    });
                }

                var finalStatus = result.FailedCount > 0 ? "Completed with failures" : "Completed";
                var detailText = $"Today's Freshness queried {targets.Count} visible pilots and imported {result.NewKillmailsImported} missing killmail(s).";
                FinalizeSnapshot(finalStatus, result, detailText);

                AppLogger.KillmailImportInfo(
                    $"Today's Freshness complete. visiblePilots={targets.Count} entitiesQueried={targets.Count} zkillResultsFound={result.ZkillResultsFound} alreadyKnown={result.AlreadyKnownCount} imported={result.NewKillmailsImported} failed={result.FailedCount}");

                result.DetailText = detailText;
                return result;
            }
            catch (OperationCanceledException)
            {
                var cancelled = new TodaysFreshnessRunResult
                {
                    Success = false,
                    Cancelled = true,
                    DetailText = "Today's Freshness cancelled."
                };

                UpdateSnapshot(snapshot =>
                {
                    snapshot.Status = "Cancelled";
                    snapshot.NextRetryAtUtc = "";
                    snapshot.DetailText = cancelled.DetailText;
                    snapshot.LastRunAtUtc = DateTime.UtcNow.ToString("o");
                });

                AppLogger.KillmailImportInfo("Today's Freshness cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                AppLogger.KillmailImportWarn($"Today's Freshness failed. message={ex.Message}");
                AppLogger.ErrorOnly("Today's Freshness exception.", ex);

                UpdateSnapshot(snapshot =>
                {
                    snapshot.Status = "Error";
                    snapshot.LastError = ex.Message;
                    snapshot.NextRetryAtUtc = "";
                    snapshot.LastRunAtUtc = DateTime.UtcNow.ToString("o");
                    snapshot.DetailText = "Today's Freshness failed.";
                });

                return new TodaysFreshnessRunResult
                {
                    Success = false,
                    FailedCount = 1,
                    LastError = ex.Message,
                    DetailText = "Today's Freshness failed."
                };
            }
            finally
            {
                _runGate.Release();
            }
        }

        private void FinalizeSnapshot(string status, TodaysFreshnessRunResult result, string detailText)
        {
            UpdateSnapshot(snapshot =>
            {
                snapshot.Status = status;
                snapshot.ZkillResultsFound = result.ZkillResultsFound;
                snapshot.AlreadyKnownCount = result.AlreadyKnownCount;
                snapshot.NewKillmailsImported = result.NewKillmailsImported;
                snapshot.FailedCount = result.FailedCount;
                snapshot.LastRunAtUtc = DateTime.UtcNow.ToString("o");
                snapshot.LastError = result.LastError ?? "";
                snapshot.NextRetryAtUtc = "";
                snapshot.DetailText = detailText;
            });
        }

        private void UpdateSnapshot(Action<TodaysFreshnessSnapshot> update)
        {
            lock (_sync)
            {
                var snapshot = CloneSnapshot(_snapshot);
                update(snapshot);
                _snapshot = snapshot;
            }

            StatusChanged?.Invoke();
        }

        private static TodaysFreshnessSnapshot CloneSnapshot(TodaysFreshnessSnapshot snapshot)
        {
            return new TodaysFreshnessSnapshot
            {
                Status = snapshot.Status,
                VisiblePilotsTargeted = snapshot.VisiblePilotsTargeted,
                EntitiesQueried = snapshot.EntitiesQueried,
                ZkillResultsFound = snapshot.ZkillResultsFound,
                AlreadyKnownCount = snapshot.AlreadyKnownCount,
                NewKillmailsImported = snapshot.NewKillmailsImported,
                FailedCount = snapshot.FailedCount,
                LastRunAtUtc = snapshot.LastRunAtUtc,
                LastError = snapshot.LastError,
                NextRetryAtUtc = snapshot.NextRetryAtUtc,
                DetailText = snapshot.DetailText
            };
        }

    }

    public sealed class TodaysFreshnessRunResult
    {
        public bool Success { get; set; }
        public bool Cancelled { get; set; }
        public int VisiblePilotsTargeted { get; set; }
        public int ZkillRequestsMade { get; set; }
        public int ZkillResultsFound { get; set; }
        public int AlreadyKnownCount { get; set; }
        public int NewKillmailsImported { get; set; }
        public int FailedCount { get; set; }
        public string LastError { get; set; } = "";
        public string DetailText { get; set; } = "";
    }
}
