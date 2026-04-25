using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PitmastersGrill.Services
{
    public enum BoardRowProcessMarkerKind
    {
        ResolverValueAvailable,
        IdentityUiUpdated,
        AffiliationUiUpdated,
        StatsUiUpdated,
        IgnoredAllianceStatsSkipped
    }

    public sealed class BoardPopulationRowProcessor
    {
        private readonly ResolverService _resolverService;
        private readonly StatsService _statsService;
        private readonly PilotBoardRowEnrichmentApplier _pilotBoardRowEnrichmentApplier;

        public BoardPopulationRowProcessor(
            ResolverService resolverService,
            StatsService statsService,
            PilotBoardRowEnrichmentApplier pilotBoardRowEnrichmentApplier)
        {
            _resolverService = resolverService;
            _statsService = statsService;
            _pilotBoardRowEnrichmentApplier = pilotBoardRowEnrichmentApplier;
        }

        public async Task ProcessAsync(
            PilotBoardRow row,
            int generation,
            Func<int> getCurrentGeneration,
            Func<Action, Task> runOnUiAsync,
            Action<PilotBoardRow> refreshDetailPaneIfSelected,
            Action updateLastRefreshed,
            Action<BoardRowProcessMarkerKind, string> writeMarker,
            Func<PilotBoardRow, bool>? shouldStopAfterAffiliation = null)
        {
            if (!IsCurrentGeneration(generation, getCurrentGeneration))
            {
                return;
            }

            var rowStopwatch = Stopwatch.StartNew();
            DebugTraceWriter.WriteLine(
                $"row process start: generation={generation}, name='{row.CharacterName}'");

            var existingIdentity = BuildExistingIdentity(row);
            var identityOutcome = await _resolverService.ResolveCharacterAsync(row.CharacterName, existingIdentity);

            if (!IsCurrentGeneration(generation, getCurrentGeneration))
            {
                return;
            }

            await runOnUiAsync(() =>
            {
                _pilotBoardRowEnrichmentApplier.ApplyIdentityOutcomeToRow(row, identityOutcome);
                refreshDetailPaneIfSelected(row);
                updateLastRefreshed();
            });

            if (identityOutcome.Value != null)
            {
                writeMarker(
                    BoardRowProcessMarkerKind.ResolverValueAvailable,
                    $"first resolver value: generation={generation}, name='{row.CharacterName}', outcome={identityOutcome.Kind}, elapsedMs={rowStopwatch.ElapsedMilliseconds}");

                writeMarker(
                    BoardRowProcessMarkerKind.IdentityUiUpdated,
                    $"first identity UI update: generation={generation}, name='{row.CharacterName}', outcome={identityOutcome.Kind}, elapsedMs={rowStopwatch.ElapsedMilliseconds}");
            }

            var effectiveIdentity = identityOutcome.Value;

            if (identityOutcome.Kind == ProviderOutcomeKind.NotFound ||
                effectiveIdentity == null ||
                string.IsNullOrWhiteSpace(effectiveIdentity.CharacterId))
            {
                await runOnUiAsync(() =>
                {
                    _pilotBoardRowEnrichmentApplier.RecalculateRetryMetadata(row);
                    refreshDetailPaneIfSelected(row);
                    updateLastRefreshed();
                });

                return;
            }

            var affiliationOutcome = await _resolverService.EnrichAffiliationIfNeededAsync(effectiveIdentity);

            if (!IsCurrentGeneration(generation, getCurrentGeneration))
            {
                return;
            }

            await runOnUiAsync(() =>
            {
                _pilotBoardRowEnrichmentApplier.ApplyAffiliationOutcomeToRow(row, affiliationOutcome);
                refreshDetailPaneIfSelected(row);
                updateLastRefreshed();
            });

            if (affiliationOutcome.Value != null &&
                _pilotBoardRowEnrichmentApplier.HasIdentityStageChange(effectiveIdentity, affiliationOutcome.Value))
            {
                writeMarker(
                    BoardRowProcessMarkerKind.AffiliationUiUpdated,
                    $"first affiliation UI update: generation={generation}, name='{row.CharacterName}', outcome={affiliationOutcome.Kind}, elapsedMs={rowStopwatch.ElapsedMilliseconds}");
            }

            var statsIdentity = affiliationOutcome.Value ?? effectiveIdentity;

            if (shouldStopAfterAffiliation?.Invoke(row) == true)
            {
                await runOnUiAsync(() =>
                {
                    row.StatsStage = EnrichmentStageState.Skipped;
                    row.StatsStatusDetail = "Stats skipped because alliance is ignored";
                    row.StatsRetryAtUtc = null;
                    _pilotBoardRowEnrichmentApplier.RecalculateRetryMetadata(row);
                    refreshDetailPaneIfSelected(row);
                    updateLastRefreshed();
                });

                writeMarker(
                    BoardRowProcessMarkerKind.IgnoredAllianceStatsSkipped,
                    $"ignored alliance skipped stats: generation={generation}, name='{row.CharacterName}', elapsedMs={rowStopwatch.ElapsedMilliseconds}");

                return;
            }

            if (statsIdentity == null || string.IsNullOrWhiteSpace(statsIdentity.CharacterId))
            {
                await runOnUiAsync(() =>
                {
                    _pilotBoardRowEnrichmentApplier.RecalculateRetryMetadata(row);
                    refreshDetailPaneIfSelected(row);
                    updateLastRefreshed();
                });

                return;
            }

            var existingStats = BuildExistingStats(row, statsIdentity.CharacterId);
            var statsOutcome = await _statsService.ResolveSingleAsync(statsIdentity.CharacterId, existingStats);

            if (!IsCurrentGeneration(generation, getCurrentGeneration))
            {
                return;
            }

            await runOnUiAsync(() =>
            {
                _pilotBoardRowEnrichmentApplier.ApplyStatsOutcomeToRow(row, statsOutcome);
                refreshDetailPaneIfSelected(row);
                updateLastRefreshed();
            });

            if (statsOutcome.Value != null)
            {
                writeMarker(
                    BoardRowProcessMarkerKind.StatsUiUpdated,
                    $"first stats UI update: generation={generation}, name='{row.CharacterName}', outcome={statsOutcome.Kind}, elapsedMs={rowStopwatch.ElapsedMilliseconds}");
            }
        }

        private static bool IsCurrentGeneration(int generation, Func<int> getCurrentGeneration)
        {
            return generation == getCurrentGeneration();
        }

        private static ResolverCacheEntry? BuildExistingIdentity(PilotBoardRow row)
        {
            if (string.IsNullOrWhiteSpace(row.CharacterId) &&
                !string.Equals(row.ResolverConfidence, "not_found", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(row.ResolverConfidence, "esi_exact_fallback", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return new ResolverCacheEntry
            {
                CharacterName = row.CharacterName,
                CharacterId = row.CharacterId,
                AllianceName = row.AllianceName,
                AllianceTicker = row.AllianceTicker,
                CorpName = row.CorpName,
                CorpTicker = row.CorpTicker,
                ResolverConfidence = row.ResolverConfidence,
                ResolvedAtUtc = row.ResolvedAtUtc,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(30).ToString("o"),
                AffiliationCheckedAtUtc = row.AffiliationCheckedAtUtc
            };
        }

        private static StatsCacheEntry? BuildExistingStats(PilotBoardRow row, string characterId)
        {
            if (!row.KillCount.HasValue && !row.LossCount.HasValue)
            {
                return null;
            }

            return new StatsCacheEntry
            {
                CharacterId = characterId,
                KillCount = row.KillCount ?? 0,
                LossCount = row.LossCount ?? 0,
                AvgAttackersWhenAttacking = row.AvgAttackersWhenAttacking ?? 0,
                LastPublicCynoCapableHull = row.LastPublicCynoCapableHull ?? string.Empty,
                LastShipSeenName = row.LastShipSeenName ?? string.Empty,
                LastShipSeenAtUtc = row.LastShipSeenAtUtc ?? string.Empty,
                RefreshedAtUtc = DateTime.UtcNow.ToString("o"),
                ExpiresAtUtc = DateTime.UtcNow.AddHours(12).ToString("o")
            };
        }
    }
}
