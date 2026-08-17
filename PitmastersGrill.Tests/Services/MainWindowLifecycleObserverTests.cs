using PitmastersGrill.Services;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class MainWindowLifecycleObserverTests
    {
        [Fact]
        public void Observe_MapsSnapshotAndDetailIntoLifecycleRecord()
        {
            var messages = new List<string>();
            var observer = new MainWindowLifecycleObserver(
                () => new MainWindowLifecycleSnapshot(
                    "Maximized",
                    true,
                    true,
                    "WaitingForActivation",
                    false,
                    false,
                    false,
                    42),
                messages.Add,
                _ => { });

            observer.Observe("closing", "cancel='True'");

            var message = Assert.Single(messages);
            Assert.Contains("seq=1", message);
            Assert.Contains("event='closing'", message);
            Assert.Contains("thread=42", message);
            Assert.Contains("windowState='Maximized'", message);
            Assert.Contains("visible=True", message);
            Assert.Contains("shuttingDown=True", message);
            Assert.Contains("barrierStatus='WaitingForActivation'", message);
            Assert.Contains("barrierComplete=False", message);
            Assert.Contains("dispatcherShutdownStarted=False", message);
            Assert.Contains("dispatcherShutdownFinished=False", message);
            Assert.EndsWith("cancel='True'", message);
        }

        [Fact]
        public void Observe_IncrementsSequenceMonotonically()
        {
            var messages = new List<string>();
            var observer = CreateObserver(messages.Add);

            observer.Observe("first");
            observer.Observe("second");
            observer.Observe("third");

            Assert.Equal(3, observer.Sequence);
            Assert.Contains("seq=1 event='first'", messages[0]);
            Assert.Contains("seq=2 event='second'", messages[1]);
            Assert.Contains("seq=3 event='third'", messages[2]);
        }

        [Fact]
        public void Observe_WhenInfoLoggerFails_RemainsNonFatalAndWarns()
        {
            var warnings = new List<string>();
            var observer = new MainWindowLifecycleObserver(
                CaptureDefaultSnapshot,
                _ => throw new InvalidOperationException("logger failed"),
                warnings.Add);

            var exception = Record.Exception(() => observer.Observe("state-changed"));

            Assert.Null(exception);
            var warning = Assert.Single(warnings);
            Assert.Contains("event='state-changed'", warning);
            Assert.Contains("logger failed", warning);
        }

        [Fact]
        public void Observe_WhenWarningLoggerAlsoFails_RemainsNonFatal()
        {
            var observer = new MainWindowLifecycleObserver(
                () => throw new InvalidOperationException("snapshot failed"),
                _ => { },
                _ => throw new InvalidOperationException("warning failed"));

            Assert.Null(Record.Exception(() => observer.Observe("closed")));
        }

        [Fact]
        public void MainWindow_OwnershipBoundary_DelegatesFormattingAndSequenceToObserver()
        {
            var lifecycleAdapter = File.ReadAllText(FindRepoFile("PitmastersGrill", "MainWindow.LifecycleObserver.cs"));
            var shutdown = File.ReadAllText(FindRepoFile("PitmastersGrill", "MainWindow.Shutdown.cs"));
            var legacyPath = FindOptionalRepoFile("PitmastersGrill", "MainWindow.LifecycleDiagnostics.cs");

            Assert.Contains("MainWindowLifecycleObserver", lifecycleAdapter);
            Assert.Contains("CaptureLifecycleSnapshot", lifecycleAdapter);
            Assert.Contains("ObserveMainWindowLifecycle", lifecycleAdapter);
            Assert.DoesNotContain("Interlocked.Increment", lifecycleAdapter);
            Assert.DoesNotContain("MainWindow lifecycle. seq=", lifecycleAdapter);
            Assert.Contains("ObserveMainWindowLifecycle", shutdown);
            Assert.Null(legacyPath);
        }

        private static MainWindowLifecycleObserver CreateObserver(Action<string> logInfo) =>
            new(CaptureDefaultSnapshot, logInfo, _ => { });

        private static MainWindowLifecycleSnapshot CaptureDefaultSnapshot() =>
            new("Normal", true, false, "none", false, false, false, 7);

        private static string FindRepoFile(params string[] relativeParts) =>
            FindOptionalRepoFile(relativeParts)
            ?? throw new FileNotFoundException($"Could not find repository file '{Path.Combine(relativeParts)}'.");

        private static string? FindOptionalRepoFile(params string[] relativeParts)
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                var candidate = Path.Combine(new[] { directory.FullName }.Concat(relativeParts).ToArray());
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
