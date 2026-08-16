using PitmastersGrill.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class DeterministicShutdownCoordinatorTests
    {
        [Fact]
        public void HandleClosing_WhenUninitialized_AllowsCloseWithoutStartingBarrier()
        {
            var stopCount = 0;
            var cancelCount = 0;
            var finalCloseCount = 0;
            var coordinator = CreateCoordinator(
                stopBackgroundWorkAsync: () =>
                {
                    stopCount++;
                    return Task.CompletedTask;
                },
                signalCancellation: () => cancelCount++,
                requestFinalCloseAsync: () =>
                {
                    finalCloseCount++;
                    return Task.CompletedTask;
                });

            var disposition = coordinator.HandleClosing(isInitialized: false);

            Assert.Equal(ShutdownCloseDisposition.AllowClose, disposition);
            Assert.Null(coordinator.BarrierTask);
            Assert.False(coordinator.IsComplete);
            Assert.Equal(0, stopCount);
            Assert.Equal(0, cancelCount);
            Assert.Equal(0, finalCloseCount);
        }

        [Fact]
        public async Task HandleClosing_FirstCloseDefersAndRepeatedCloseReusesBarrier()
        {
            var stopCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var stopCount = 0;
            var cancelCount = 0;
            var finalCloseCount = 0;
            var lifecycle = new List<string>();
            var coordinator = CreateCoordinator(
                stopBackgroundWorkAsync: () =>
                {
                    stopCount++;
                    return stopCompletion.Task;
                },
                signalCancellation: () => cancelCount++,
                requestFinalCloseAsync: () =>
                {
                    finalCloseCount++;
                    return Task.CompletedTask;
                },
                logLifecycle: (eventName, _) => lifecycle.Add(eventName));

            var first = coordinator.HandleClosing(isInitialized: true);
            var repeated = coordinator.HandleClosing(isInitialized: true);

            Assert.Equal(ShutdownCloseDisposition.DeferAndStart, first);
            Assert.Equal(ShutdownCloseDisposition.DeferExisting, repeated);
            Assert.NotNull(coordinator.BarrierTask);
            Assert.False(coordinator.IsComplete);
            Assert.Equal(1, stopCount);
            Assert.Equal(1, cancelCount);
            Assert.Equal(0, finalCloseCount);

            stopCompletion.SetResult(true);
            await coordinator.BarrierTask!;

            Assert.True(coordinator.IsComplete);
            Assert.Equal(1, finalCloseCount);
            Assert.Equal(
                new[]
                {
                    "shutdown-deferred",
                    "shutdown-barrier-begin",
                    "shutdown-background-quiescent",
                    "shutdown-barrier-complete",
                    "shutdown-final-close"
                },
                lifecycle);
            Assert.Equal(
                ShutdownCloseDisposition.AllowClose,
                coordinator.HandleClosing(isInitialized: true));
        }

        [Fact]
        public async Task HandleClosing_WhenStopFails_LogsFaultAndStillRequestsFinalClose()
        {
            var errors = new List<Exception>();
            var lifecycle = new List<string>();
            var finalCloseCount = 0;
            var expected = new InvalidOperationException("stop failed");
            var coordinator = CreateCoordinator(
                stopBackgroundWorkAsync: () => Task.FromException(expected),
                requestFinalCloseAsync: () =>
                {
                    finalCloseCount++;
                    return Task.CompletedTask;
                },
                logError: (_, ex) => errors.Add(ex),
                logLifecycle: (eventName, _) => lifecycle.Add(eventName));

            Assert.Equal(
                ShutdownCloseDisposition.DeferAndStart,
                coordinator.HandleClosing(isInitialized: true));
            await coordinator.BarrierTask!;

            Assert.True(coordinator.IsComplete);
            Assert.Equal(1, finalCloseCount);
            Assert.Contains(expected, errors);
            Assert.Contains("shutdown-barrier-fault", lifecycle);
            Assert.Contains("shutdown-barrier-complete", lifecycle);
            Assert.Contains("shutdown-final-close", lifecycle);
        }

        [Fact]
        public async Task HandleClosing_WhenDispatcherIsShuttingDown_SkipsFinalCloseRequest()
        {
            var finalCloseCount = 0;
            var lifecycle = new List<string>();
            var coordinator = CreateCoordinator(
                canRequestFinalClose: () => false,
                requestFinalCloseAsync: () =>
                {
                    finalCloseCount++;
                    return Task.CompletedTask;
                },
                logLifecycle: (eventName, _) => lifecycle.Add(eventName));

            coordinator.HandleClosing(isInitialized: true);
            await coordinator.BarrierTask!;

            Assert.True(coordinator.IsComplete);
            Assert.Equal(0, finalCloseCount);
            Assert.DoesNotContain("shutdown-final-close", lifecycle);
        }

        [Fact]
        public async Task HandleClosing_WhenCancellationSignalFails_StillRunsBarrier()
        {
            var stopCount = 0;
            var errors = new List<Exception>();
            var expected = new InvalidOperationException("cancel failed");
            var coordinator = CreateCoordinator(
                stopBackgroundWorkAsync: () =>
                {
                    stopCount++;
                    return Task.CompletedTask;
                },
                signalCancellation: () => throw expected,
                logError: (_, ex) => errors.Add(ex));

            coordinator.HandleClosing(isInitialized: true);
            await coordinator.BarrierTask!;

            Assert.Equal(1, stopCount);
            Assert.True(coordinator.IsComplete);
            Assert.Contains(expected, errors);
        }

        private static DeterministicShutdownCoordinator CreateCoordinator(
            Func<Task>? stopBackgroundWorkAsync = null,
            Action? signalCancellation = null,
            Func<bool>? canRequestFinalClose = null,
            Func<Task>? requestFinalCloseAsync = null,
            Action<string>? logInfo = null,
            Action<string, Exception>? logError = null,
            Action<string, string?>? logLifecycle = null)
        {
            return new DeterministicShutdownCoordinator(
                stopBackgroundWorkAsync ?? (() => Task.CompletedTask),
                signalCancellation ?? (() => { }),
                canRequestFinalClose ?? (() => true),
                requestFinalCloseAsync ?? (() => Task.CompletedTask),
                logInfo ?? (_ => { }),
                logError ?? ((_, _) => { }),
                logLifecycle ?? ((_, _) => { }));
        }
    }
}
