using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class BackgroundIntelUpdateCoordinatorTests
    {
        [Fact]
        public async Task ForegroundCoordinator_RejectsConcurrentFreshnessOperation()
        {
            var coordinator = new ForegroundFreshnessCoordinator();
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var first = coordinator.RunExclusiveAsync(
                async () =>
                {
                    entered.TrySetResult(true);
                    await release.Task;
                    return "first";
                },
                () => "busy",
                "busy",
                CancellationToken.None);

            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

            var second = await coordinator.RunExclusiveAsync(
                () => Task.FromResult("second"),
                () => "busy",
                "busy",
                CancellationToken.None);

            Assert.Equal("busy", second);

            release.TrySetResult(true);
            Assert.Equal("first", await first);
        }

        [Fact]
        public async Task ForegroundCoordinator_PriorityBlocksUntilHandleIsReleased()
        {
            var coordinator = new ForegroundFreshnessCoordinator();
            var transitions = new List<bool>();
            coordinator.PriorityChanged += transitions.Add;

            var priority = coordinator.BeginPriority();
            Assert.True(coordinator.IsPriorityActive);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var wait = coordinator.WaitForPriorityToClearAsync(timeout.Token);
            Assert.False(wait.IsCompleted);

            priority.Dispose();
            await wait;

            Assert.False(coordinator.IsPriorityActive);
            Assert.Equal(new[] { true, false }, transitions);
        }

        [Fact]
        public async Task ArchiveWorker_CurrentDatasetDoesNotImport()
        {
            using var shutdown = new CancellationTokenSource();
            var freshnessObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var importCalls = 0;
            var coordinator = new ForegroundFreshnessCoordinator();
            var worker = new ArchiveSyncWorker(
                () =>
                {
                    freshnessObserved.TrySetResult(true);
                    return CurrentFreshness();
                },
                (day, token) =>
                {
                    Interlocked.Increment(ref importCalls);
                    return Task.FromResult(new KillmailDayImportResult { Success = true, DayUtc = day.DayUtc });
                },
                coordinator,
                shutdown.Token,
                TimeSpan.FromMinutes(1),
                TimeSpan.Zero);

            worker.StartIfNeeded();
            await freshnessObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));

            shutdown.Cancel();
            worker.Wake();
            await worker.WaitForCompletionAsync();

            Assert.Equal(0, importCalls);
            Assert.False(worker.GetState().IsRunning);
        }

        [Fact]
        public async Task ArchiveWorker_MissingDayImportsExactlyOnceAndStopsCleanly()
        {
            using var shutdown = new CancellationTokenSource();
            var importObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var imported = 0;
            var importCalls = 0;
            var coordinator = new ForegroundFreshnessCoordinator();
            var worker = new ArchiveSyncWorker(
                () => Volatile.Read(ref imported) == 0 ? MissingFreshness() : CurrentFreshness(),
                (day, token) =>
                {
                    Interlocked.Increment(ref importCalls);
                    Interlocked.Exchange(ref imported, 1);
                    importObserved.TrySetResult(true);
                    return Task.FromResult(new KillmailDayImportResult
                    {
                        Success = true,
                        DayUtc = day.DayUtc,
                        ImportedKillmailCount = 4
                    });
                },
                coordinator,
                shutdown.Token,
                TimeSpan.FromMinutes(1),
                TimeSpan.Zero);

            worker.StartIfNeeded();
            await importObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));

            shutdown.Cancel();
            worker.Wake();
            await worker.WaitForCompletionAsync();

            Assert.Equal(1, importCalls);
            Assert.False(worker.GetState().IsRunning);
        }

        [Fact]
        public async Task HistoricalRepairScheduler_RejectsDuplicateScheduleAndRunsOnce()
        {
            using var shutdown = new CancellationTokenSource();
            var delayEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseDelay = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var runCalls = 0;
            var coordinator = new ForegroundFreshnessCoordinator();
            var scheduler = new BackgroundHistoricalRepairScheduler(
                () => EnabledRepairConfiguration(),
                (ids, token) =>
                {
                    Interlocked.Increment(ref runCalls);
                    return Task.FromResult(new HistoricalFreshnessRunResult
                    {
                        Success = true,
                        CandidatePilotsConsidered = ids.Count,
                        PilotsChecked = ids.Count,
                        DetailText = "Completed"
                    });
                },
                coordinator,
                shutdown.Token,
                async (delay, token) =>
                {
                    delayEntered.TrySetResult(true);
                    await releaseDelay.Task.WaitAsync(token);
                });

            scheduler.ScheduleAfterUiShown(() => new long[] { 42 });
            scheduler.ScheduleAfterUiShown(() => new long[] { 42 });

            await delayEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            releaseDelay.TrySetResult(true);
            await scheduler.WaitForCompletionAsync();

            Assert.Equal(1, runCalls);
        }

        [Fact]
        public async Task HistoricalRepairScheduler_ShutdownDuringDelayCancelsWithoutRepair()
        {
            using var shutdown = new CancellationTokenSource();
            var delayEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var runCalls = 0;
            var coordinator = new ForegroundFreshnessCoordinator();
            var scheduler = new BackgroundHistoricalRepairScheduler(
                () => EnabledRepairConfiguration(),
                (ids, token) =>
                {
                    Interlocked.Increment(ref runCalls);
                    return Task.FromResult(new HistoricalFreshnessRunResult { Success = true });
                },
                coordinator,
                shutdown.Token,
                async (delay, token) =>
                {
                    delayEntered.TrySetResult(true);
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                });

            scheduler.ScheduleAfterUiShown(() => Array.Empty<long>());
            await delayEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

            shutdown.Cancel();
            await scheduler.WaitForCompletionAsync();

            Assert.Equal(0, runCalls);
        }

        private static KillmailDatasetFreshnessStatus CurrentFreshness()
        {
            return new KillmailDatasetFreshnessStatus
            {
                IsCurrentThroughRequiredDay = true,
                IsRequestedCoverageComplete = true,
                MissingDayCount = 0,
                RequiredThroughDayUtc = "2026-08-13",
                LatestCompleteDayUtc = "2026-08-13"
            };
        }

        private static KillmailDatasetFreshnessStatus MissingFreshness()
        {
            return new KillmailDatasetFreshnessStatus
            {
                IsCurrentThroughRequiredDay = false,
                IsRequestedCoverageComplete = false,
                MissingDayCount = 1,
                FirstMissingDayUtc = "2026-08-13",
                LastMissingDayUtc = "2026-08-13",
                RequestedStartDayUtc = "2026-08-13",
                RequiredThroughDayUtc = "2026-08-13",
                RequestedCoverageDays = 1,
                RequestedHistoryDays = 1
            };
        }

        private static BackgroundHistoricalRepairConfiguration EnabledRepairConfiguration()
        {
            return new BackgroundHistoricalRepairConfiguration
            {
                Enabled = true,
                DelaySeconds = 1,
                CooldownHours = 12,
                LookbackDays = 3,
                MaxPilotsPerRun = 50,
                RecentPilotWindowDays = 14
            };
        }
    }
}
