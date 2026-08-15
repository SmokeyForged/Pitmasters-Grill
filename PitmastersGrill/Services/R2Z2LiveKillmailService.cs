using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersGrill.Services
{
    public sealed class R2Z2LiveKillmailService
    {
        private const int DefaultStartupSequenceOverlap = 250;
        private const int CurrentDayBridgeSequenceOverlap = 10000;

        private readonly object _sync = new();
        private readonly AppSettingsService _appSettingsService;
        private readonly KillmailDatasetMetadataRepository _metadataRepository;
        private readonly KillmailIncrementalImportService _incrementalImportService;
        private readonly R2Z2SequenceClient _sequenceClient;
        private readonly R2Z2FeedStateRepository _feedStateRepository;

        private Task? _workerTask;
        private CancellationTokenSource? _runCts;
        private R2Z2LiveFeedSnapshot _snapshot = new();
        private string _nextRetryAtUtc = "";

        public R2Z2LiveKillmailService(
            AppSettingsService appSettingsService,
            KillmailIncrementalImportService incrementalImportService)
            : this(
                appSettingsService,
                incrementalImportService,
                new KillmailDatasetMetadataRepository(KillmailPaths.GetKillmailDatabasePath()),
                new R2Z2SequenceClient(),
                new R2Z2FeedStateRepository(KillmailPaths.GetKillmailDatabasePath()))
        {
        }

        internal R2Z2LiveKillmailService(
            AppSettingsService appSettingsService,
            KillmailIncrementalImportService incrementalImportService,
            KillmailDatasetMetadataRepository metadataRepository,
            R2Z2SequenceClient sequenceClient,
            R2Z2FeedStateRepository feedStateRepository)
        {
            _appSettingsService = appSettingsService ?? throw new ArgumentNullException(nameof(appSettingsService));
            _incrementalImportService = incrementalImportService ?? throw new ArgumentNullException(nameof(incrementalImportService));
            _metadataRepository = metadataRepository ?? throw new ArgumentNullException(nameof(metadataRepository));
            _sequenceClient = sequenceClient ?? throw new ArgumentNullException(nameof(sequenceClient));
            _feedStateRepository = feedStateRepository ?? throw new ArgumentNullException(nameof(feedStateRepository));
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

        public async Task StopAsync()
        {
            Task? workerTask;

            lock (_sync)
            {
                AppLogger.KillmailImportInfo("R2Z2 live feed worker stop-and-wait requested.");
                _runCts?.Cancel();
                workerTask = _workerTask;
            }

            if (workerTask == null)
            {
                return;
            }

            try
            {
                await workerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown if cancellation wins before the worker begins.
            }
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
                await EnsureInitializedSequenceAsync(cancellationToken).ConfigureAwait(false);

                while (!cancellationToken.IsCancellationRequested)
                {
                    var state = _feedStateRepository.Load();
                    if (state == null || state.Enabled == 0)
                    {
                        UpdateSnapshot(_feedStateRepository.ReadSnapshot());
                        return;
                    }

                    if (!state.NextSequenceId.HasValue || state.NextSequenceId.Value <= 0)
                    {
                        await EnsureInitializedSequenceAsync(cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    var nextSequenceId = state.NextSequenceId.Value;

                    try
                    {
                        var fetch = await _sequenceClient
                            .FetchSequenceAsync(nextSequenceId, consecutiveRateLimitCount + 1, cancellationToken)
                            .ConfigureAwait(false);

                        if (fetch.Status == R2Z2SequenceFetchStatus.NotFound)
                        {
                            consecutiveRateLimitCount = 0;
                            var retryAtUtc = DateTime.UtcNow.Add(fetch.RetryDelay).ToString("o");
                            SetNextRetryAtUtc(retryAtUtc);
                            AppLogger.KillmailImportDebug(
                                $"R2Z2 caught up wait. sequence={nextSequenceId} status=404 retryDelayMs={(long)fetch.RetryDelay.TotalMilliseconds} retryAtUtc={retryAtUtc}");
                            UpdateFeedState(stateUpdate =>
                            {
                                stateUpdate.Status = "Caught up / waiting";
                                stateUpdate.Last404AtUtc = DateTime.UtcNow.ToString("o");
                                stateUpdate.LastError = "";
                                stateUpdate.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
                            });

                            await Task.Delay(fetch.RetryDelay, cancellationToken).ConfigureAwait(false);
                            continue;
                        }

                        if (fetch.Status == R2Z2SequenceFetchStatus.RateLimited)
                        {
                            consecutiveRateLimitCount++;
                            var retryAtUtc = DateTime.UtcNow.Add(fetch.RetryDelay).ToString("o");
                            SetNextRetryAtUtc(retryAtUtc);
                            AppLogger.KillmailImportDebug(
                                $"R2Z2 rate limit backoff. sequence={nextSequenceId} status=429 retryDelayMs={(long)fetch.RetryDelay.TotalMilliseconds} source={fetch.DelaySource} consecutive429={consecutiveRateLimitCount} retryAtUtc={retryAtUtc}");
                            UpdateFeedState(stateUpdate =>
                            {
                                stateUpdate.Status = "Backing off after rate limit";
                                stateUpdate.LastErrorAtUtc = DateTime.UtcNow.ToString("o");
                                stateUpdate.LastError = $"HTTP 429 rate limit. Retry at {retryAtUtc}.";
                                stateUpdate.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
                            });

                            await Task.Delay(fetch.RetryDelay, cancellationToken).ConfigureAwait(false);
                            continue;
                        }

                        if (fetch.Status == R2Z2SequenceFetchStatus.Forbidden)
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

                        if (fetch.Status == R2Z2SequenceFetchStatus.Error)
                        {
                            SetNextRetryAtUtc("");
                            AppLogger.KillmailImportWarn(
                                $"R2Z2 sequence fetch failed. sequence={nextSequenceId} error={fetch.Error}");
                            UpdateFeedState(stateUpdate =>
                            {
                                stateUpdate.Status = "Error";
                                stateUpdate.LastErrorAtUtc = DateTime.UtcNow.ToString("o");
                                stateUpdate.LastError = fetch.Error;
                                stateUpdate.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
                            });

                            await Task.Delay(fetch.RetryDelay, cancellationToken).ConfigureAwait(false);
                            continue;
                        }

                        var processed = ProcessSequencePayload(nextSequenceId, fetch.Content, cancellationToken);

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

                            await Task.Delay(_sequenceClient.ErrorBackoffDelay, cancellationToken).ConfigureAwait(false);
                            continue;
                        }

                        consecutiveRateLimitCount = 0;
                        SetNextRetryAtUtc("");
                        AppLogger.KillmailImportDebug(
                            processed.WasDuplicate
                                ? $"R2Z2 duplicate killmail skipped. sequence={nextSequenceId} killmailId={processed.KillmailId}"
                                : $"R2Z2 live killmail processed. sequence={nextSequenceId} killmailId={processed.KillmailId} day={processed.DayUtc}");
                        await Task.Delay(_sequenceClient.SuccessPacingDelay, cancellationToken).ConfigureAwait(false);
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

                        await Task.Delay(_sequenceClient.ErrorBackoffDelay, cancellationToken).ConfigureAwait(false);
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
            var state = _feedStateRepository.LoadOrDefault();
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

            var currentSequenceId = await _sequenceClient
                .GetCurrentSequenceIdAsync(cancellationToken)
                .ConfigureAwait(false);

            var startupOverlap = DetermineStartupSequenceOverlap(out var overlapReason);
            var initializedNextSequence = Math.Max(1, currentSequenceId - startupOverlap);
            AppLogger.KillmailImportInfo(
                $"R2Z2 startup sequence initialized. currentSequence={currentSequenceId} nextSequence={initializedNextSequence} overlap={startupOverlap} reason={overlapReason}");

            UpdateFeedState(stateUpdate =>
            {
                stateUpdate.NextSequenceId = initializedNextSequence;
                stateUpdate.Status = "Starting";
                stateUpdate.LastError = "";
                stateUpdate.UpdatedAtUtc = DateTime.UtcNow.ToString("o");
            });
        }

        private LiveProcessResult ProcessSequencePayload(
            long requestedSequenceId,
            string content,
            CancellationToken cancellationToken)
        {
            if (!TryExtractSequenceEnvelope(content, requestedSequenceId, out var envelope, out var error))
            {
                return new LiveProcessResult
                {
                    Success = false,
                    Error = error
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

            var importResult = _incrementalImportService.ImportKillmailJson(new IncrementalKillmailImportRequest
            {
                KillmailId = killmailId,
                KillmailHash = envelope.KillmailHash,
                KillmailJson = envelope.KillmailJson,
                Source = "r2z2",
                SequenceId = envelope.SequenceId,
                UploadedAtUtc = envelope.UploadedAtUtc
            }, cancellationToken);

            if (!importResult.Success)
            {
                return new LiveProcessResult
                {
                    Success = false,
                    Error = importResult.Error
                };
            }

            var nowUtc = DateTime.UtcNow.ToString("o");
            UpdateFeedState(state =>
            {
                state.Enabled = 1;
                state.NextSequenceId = envelope.SequenceId + 1;
                state.LastProcessedSequenceId = envelope.SequenceId;
                state.LastSuccessAtUtc = nowUtc;
                state.LastError = "";
                state.Status = "Catching up";
                state.UpdatedAtUtc = nowUtc;
            });

            AppLogger.KillmailImportDebug(
                $"R2Z2 derived observations. sequence={requestedSequenceId} killmailId={envelope.KillmailId} registry={importResult.RegistryObservationCount} fleet={importResult.FleetObservationCount} ship={importResult.ShipObservationCount} cyno={importResult.CynoObservationCount} bait={importResult.BaitObservationCount} tackle={importResult.TackleObservationCount}");
            AppLogger.KillmailImportDebug(
                $"R2Z2 checkpoint updated. sequence={envelope.SequenceId} duplicate={importResult.WasDuplicate.ToString().ToLowerInvariant()}");

            return new LiveProcessResult
            {
                Success = true,
                WasDuplicate = importResult.WasDuplicate,
                KillmailId = envelope.KillmailId,
                DayUtc = importResult.DayUtc
            };
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

        private void UpdateFeedState(Action<R2Z2FeedState> update)
        {
            _feedStateRepository.Update(update);
            UpdateSnapshot(_feedStateRepository.ReadSnapshot());
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

        private void SetNextRetryAtUtc(string nextRetryAtUtc)
        {
            lock (_sync)
            {
                _nextRetryAtUtc = nextRetryAtUtc ?? "";
            }

            UpdateSnapshot(_feedStateRepository.ReadSnapshot());
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

        internal static bool TryExtractSequenceEnvelope(
            string payload,
            long requestedSequenceId,
            out R2Z2SequenceEnvelope envelope,
            out string error)
        {
            envelope = new R2Z2SequenceEnvelope();
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

        internal sealed class R2Z2SequenceEnvelope
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
