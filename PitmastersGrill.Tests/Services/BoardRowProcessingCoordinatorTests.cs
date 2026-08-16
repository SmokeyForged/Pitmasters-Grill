using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class BoardRowProcessingCoordinatorTests
    {
        [Fact]
        public async Task ProcessSingleRowAsync_RejectsStaleGenerationBeforeProcessing()
        {
            var row = new PilotBoardRow { CharacterName = "Pilot" };
            using var session = new CurrentBoardSession();
            session.ReplaceRows(new[] { row });
            var staleGeneration = session.BeginProcessingGeneration();
            session.BeginProcessingGeneration();
            var processCalls = 0;
            var reconcileCalls = 0;
            var coordinator = CreateCoordinator(
                session,
                (_, _, _) =>
                {
                    processCalls++;
                    return Task.CompletedTask;
                },
                applyWatchedState: _ => reconcileCalls++);
            using var semaphore = new SemaphoreSlim(1, 1);

            await coordinator.ProcessSingleRowAsync(row, semaphore, staleGeneration);

            Assert.Equal(0, processCalls);
            Assert.Equal(0, reconcileCalls);
            Assert.Equal(1, semaphore.CurrentCount);
        }

        [Fact]
        public async Task ProcessSingleRowAsync_ReleasesSemaphoreWhenProcessingFails()
        {
            var row = new PilotBoardRow { CharacterName = "Pilot" };
            using var session = new CurrentBoardSession();
            session.ReplaceRows(new[] { row });
            var generation = session.BeginProcessingGeneration();
            var coordinator = CreateCoordinator(
                session,
                (_, _, _) => Task.FromException(new InvalidOperationException("boom")));
            using var semaphore = new SemaphoreSlim(1, 1);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => coordinator.ProcessSingleRowAsync(row, semaphore, generation));

            Assert.True(await semaphore.WaitAsync(0));
            semaphore.Release();
        }

        [Fact]
        public async Task ProcessSingleRowAsync_ReconcilesCurrentGenerationOnce()
        {
            var row = new PilotBoardRow { CharacterName = "Pilot" };
            using var session = new CurrentBoardSession();
            session.ReplaceRows(new[] { row });
            var generation = session.BeginProcessingGeneration();
            var processorUiUpdates = 0;
            var watchedCalls = 0;
            var orderingCalls = 0;
            var watchActionCalls = 0;
            var detailCalls = 0;
            var coordinator = CreateCoordinator(
                session,
                async (_, _, runOnUiAsync) =>
                {
                    await runOnUiAsync(() => processorUiUpdates++);
                    await runOnUiAsync(() => processorUiUpdates++);
                },
                applyWatchedState: _ => watchedCalls++,
                applyCurrentBoardOrdering: () => orderingCalls++,
                updateWatchActionState: () => watchActionCalls++,
                refreshDetail: _ => detailCalls++);
            using var semaphore = new SemaphoreSlim(1, 1);

            await coordinator.ProcessSingleRowAsync(row, semaphore, generation);

            Assert.Equal(2, processorUiUpdates);
            Assert.Equal(1, watchedCalls);
            Assert.Equal(1, orderingCalls);
            Assert.Equal(1, watchActionCalls);
            Assert.Equal(1, detailCalls);
        }

        [Fact]
        public async Task ProcessSingleRowAsync_IgnoredRemovalRemainsGenerationSafe()
        {
            var row = new PilotBoardRow { CharacterName = "Pilot" };
            using var session = new CurrentBoardSession();
            session.ReplaceRows(new[] { row });
            var generation = session.BeginProcessingGeneration();
            var removeCalls = 0;
            var coordinator = CreateCoordinator(
                session,
                (_, _, _) => Task.CompletedTask,
                shouldRemoveIgnoredRow: _ =>
                {
                    session.BeginProcessingGeneration();
                    return true;
                },
                removeIgnoredRow: _ => removeCalls++);
            using var semaphore = new SemaphoreSlim(1, 1);

            await coordinator.ProcessSingleRowAsync(row, semaphore, generation);

            Assert.Equal(0, removeCalls);
            Assert.Same(row, Assert.Single(session.Rows));
        }

        [Fact]
        public async Task RefreshCurrentRowsFromLocalIntelAsync_StartsFreshGenerationAndFinalizes()
        {
            var row = new PilotBoardRow { CharacterName = "Pilot" };
            using var session = new CurrentBoardSession();
            session.ReplaceRows(new[] { row });
            var events = new List<string>();
            var observedGeneration = 0;
            var coordinator = CreateCoordinator(session, (_, _, _) => Task.CompletedTask);

            await coordinator.RefreshCurrentRowsFromLocalIntelAsync(
                () => events.Add("cancel"),
                () => events.Add("status"),
                (rows, generation) =>
                {
                    events.Add("process");
                    observedGeneration = generation;
                    Assert.Same(row, Assert.Single(rows));
                    return Task.CompletedTask;
                },
                generation =>
                {
                    events.Add("finalize");
                    Assert.Equal(observedGeneration, generation);
                },
                () => events.Add("refreshed"));

            Assert.Equal(1, observedGeneration);
            Assert.Equal(observedGeneration, session.CurrentGeneration);
            Assert.Equal(new[] { "cancel", "status", "process", "finalize", "refreshed" }, events);
        }

        private static BoardRowProcessingCoordinator CreateCoordinator(
            CurrentBoardSession session,
            BoardRowProcessingWork processRowAsync,
            Action<PilotBoardRow>? applyWatchedState = null,
            Action? applyCurrentBoardOrdering = null,
            Action? updateWatchActionState = null,
            Action<PilotBoardRow>? refreshDetail = null,
            Func<PilotBoardRow, bool>? shouldRemoveIgnoredRow = null,
            Action<PilotBoardRow>? removeIgnoredRow = null)
        {
            return new BoardRowProcessingCoordinator(
                session,
                processRowAsync,
                action =>
                {
                    action();
                    return Task.CompletedTask;
                },
                applyWatchedState ?? (_ => { }),
                applyCurrentBoardOrdering ?? (() => { }),
                updateWatchActionState ?? (() => { }),
                refreshDetail ?? (_ => { }),
                shouldRemoveIgnoredRow ?? (_ => false),
                removeIgnoredRow ?? (_ => { }));
        }
    }
}
