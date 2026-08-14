using PitmastersGrill.Diagnostics;
using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Windows.Threading;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class BoardPopulationSurfaceTests
    {
        [Fact]
        public void FinalizeBoardPopulationPass_WhenRetryableRowsRemain_RequestsRetryAndUpdatesStatus()
        {
            using var diagnostics = new MainWindowDiagnostics(Dispatcher.CurrentDispatcher);
            var surface = CreateSurface(diagnostics);
            var rows = new List<PilotBoardRow>
            {
                new()
                {
                    CharacterName = "Retry Me",
                    IdentityStage = EnrichmentStageState.Throttled,
                    AffiliationStage = EnrichmentStageState.Success,
                    StatsStage = EnrichmentStageState.Success
                },
                new()
                {
                    CharacterName = "Done",
                    IdentityStage = EnrichmentStageState.Success,
                    AffiliationStage = EnrichmentStageState.Success,
                    StatsStage = EnrichmentStageState.Success
                }
            };
            var scheduled = false;
            string? statusText = null;
            BoardPopulationStatusKind? statusKind = null;

            surface.FinalizeBoardPopulationPass(
                generation: 5,
                currentGeneration: 5,
                currentRows: rows,
                maxBoardPopulationRetryAttempts: 5,
                updateBoardPopulationStatus: (text, kind) =>
                {
                    statusText = text;
                    statusKind = kind;
                },
                scheduleBoardPopulationRetry: () => scheduled = true);

            Assert.True(scheduled);
            Assert.Equal(BoardPopulationStatusKind.Warning, statusKind);
            Assert.Contains("retryable", statusText, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ClearBoard_DelegatesMutationAndInvokesCallbacks()
        {
            using var diagnostics = new MainWindowDiagnostics(Dispatcher.CurrentDispatcher);
            var surface = CreateSurface(diagnostics);
            using var session = new CurrentBoardSession();
            session.ReplaceRows(new[]
            {
                new PilotBoardRow { CharacterName = "Aura" },
                new PilotBoardRow { CharacterName = "Chribba" }
            });
            var activeGeneration = session.BeginProcessingGeneration();
            var saveCalls = 0;
            var retryCancelled = 0;
            var resetCalls = 0;
            var sortResetCalls = 0;
            var recountCalls = 0;
            var closeCalls = 0;
            var detailButtonUpdates = 0;
            var refreshedCalls = 0;
            string? statusText = null;

            surface.ClearBoard(
                "test clear",
                () => session.Count,
                () => saveCalls++,
                () => retryCancelled++,
                () => resetCalls++,
                () => sortResetCalls++,
                () => session.ClearAndInvalidate(),
                () => recountCalls++,
                () => closeCalls++,
                () => detailButtonUpdates++,
                () => refreshedCalls++,
                (text, _) => statusText = text);

            Assert.Empty(session.Rows);
            Assert.False(session.IsCurrentGeneration(activeGeneration));
            Assert.Equal(1, saveCalls);
            Assert.Equal(1, retryCancelled);
            Assert.Equal(1, resetCalls);
            Assert.Equal(1, sortResetCalls);
            Assert.Equal(1, recountCalls);
            Assert.Equal(1, closeCalls);
            Assert.Equal(1, detailButtonUpdates);
            Assert.Equal(1, refreshedCalls);
            Assert.Equal("Board cleared", statusText);
        }

#pragma warning disable SYSLIB0050
        private static BoardPopulationSurface CreateSurface(MainWindowDiagnostics diagnostics)
        {
            var retryPolicy = new BoardPopulationRetryPolicy();
            var passController = new BoardPopulationPassController(retryPolicy);
            var retryController = new BoardPopulationRetryController(retryPolicy, diagnostics, defaultBoardPopulationRetryDelaySeconds: 12);
            var entryController = (BoardPopulationEntryController)FormatterServices.GetUninitializedObject(typeof(BoardPopulationEntryController));
            return new BoardPopulationSurface(entryController, passController, retryController, diagnostics);
        }
#pragma warning restore SYSLIB0050
    }
}
