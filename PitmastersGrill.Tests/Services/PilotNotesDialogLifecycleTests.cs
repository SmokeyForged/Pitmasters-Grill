using Microsoft.Data.Sqlite;
using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using PitmastersGrill.Services;
using PitmastersGrill.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class PilotNotesDialogLifecycleTests
    {
        [Fact]
        public void Open_ConfiguresDialogShowsModalAndReevaluatesHasNotes()
        {
            RunSta(() =>
            {
                var calls = new List<string>();
                var dialog = new FakePilotNotesDialog(calls) { Saved = true };
                var factory = new FakePilotNotesDialogFactory(dialog, calls);
                var lifecycle = new PilotNotesDialogLifecycle(
                    factory,
                    characterName =>
                    {
                        calls.Add($"has-notes:{characterName}");
                        return true;
                    });
                var row = new PilotBoardRow { CharacterName = "Test Pilot", HasNotes = false };
                var owner = new Window();
                var resources = new ResourceDictionary { ["ThemeMarker"] = "theme-value" };

                var result = lifecycle.Open(row, owner, topmost: true, resources);

                Assert.Same(row, factory.Row);
                Assert.Same(owner, dialog.Owner);
                Assert.True(dialog.Topmost);
                Assert.Same(resources, dialog.Resources);
                Assert.True(dialog.ShowDialogCalled);
                Assert.True(result.Saved);
                Assert.True(result.HasNotes);
                Assert.True(row.HasNotes);
                Assert.Equal(
                    new[]
                    {
                        "create:Test Pilot",
                        "configure-owner",
                        "copy-resources",
                        "show-dialog",
                        "has-notes:Test Pilot"
                    },
                    calls);

                owner.Close();
            });
        }

        [Fact]
        public void Open_CancelledDialogStillReevaluatesCurrentNotePresence()
        {
            RunSta(() =>
            {
                var dialog = new FakePilotNotesDialog(new List<string>()) { Saved = false };
                var lifecycle = new PilotNotesDialogLifecycle(
                    new FakePilotNotesDialogFactory(dialog, new List<string>()),
                    _ => false);
                var row = new PilotBoardRow { CharacterName = "Cancel Pilot", HasNotes = true };
                var owner = new Window();

                var result = lifecycle.Open(row, owner, topmost: false, new ResourceDictionary());

                Assert.False(result.Saved);
                Assert.False(result.HasNotes);
                Assert.False(row.HasNotes);
                Assert.False(dialog.Topmost);
                Assert.True(dialog.ShowDialogCalled);

                owner.Close();
            });
        }

        [Fact]
        public void MainWindow_ConsumesLifecycleWithoutConstructingPilotNotesWindow()
        {
            var mainWindowSource = ReadRepoFile("PitmastersGrill", "MainWindow.xaml.cs");
            var compositionSource = ReadRepoFile("PitmastersGrill", "MainWindow.PilotNotes.cs");
            var methodSource = SliceMethod(
                mainWindowSource,
                "private void OpenPilotNotesWindow(PilotBoardRow row)",
                "private void WatchPilotDetailAction_Click");

            Assert.DoesNotContain("new PilotNotesWindow", mainWindowSource, StringComparison.Ordinal);
            Assert.Contains("PilotNotesDialogLifecycle.Open(row, this, Topmost, Resources)", methodSource, StringComparison.Ordinal);
            Assert.Equal(1, CountOccurrences(methodSource, "PilotBoard.Items.Refresh();"));
            Assert.Equal(1, CountOccurrences(methodSource, "RefreshDetailWindowIfSelected(row);"));
            Assert.Equal(1, CountOccurrences(methodSource, "Pilot notes window closed."));
            Assert.Contains("new PilotNotesDialogLifecycle(_notesRepository)", compositionSource, StringComparison.Ordinal);
        }

        [Fact]
        public void PilotNotesWindow_LoadsOnStaWithExistingAutomationContract()
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"pmg-pilot-notes-{Guid.NewGuid():N}.db");

            try
            {
                RunSta(() =>
                {
                    var repository = new NotesRepository(databasePath);
                    var row = new PilotBoardRow { CharacterName = "Smoke Test Pilot" };
                    var window = new PilotNotesWindow(row, repository);

                    try
                    {
                        var pilotName = Assert.IsType<TextBlock>(window.FindName("PilotNameText"));
                        var notes = Assert.IsType<TextBox>(window.FindName("NotesTextBox"));

                        Assert.Equal("PMG Notes - Smoke Test Pilot", window.Title);
                        Assert.Equal("Smoke Test Pilot", pilotName.Text);
                        Assert.Equal("PilotNameText", AutomationProperties.GetAutomationId(pilotName));
                        Assert.Equal("NotesTextBox", AutomationProperties.GetAutomationId(notes));
                    }
                    finally
                    {
                        window.Close();
                    }
                });
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(databasePath))
                {
                    File.Delete(databasePath);
                }
            }
        }

        private static string SliceMethod(string source, string startMarker, string endMarker)
        {
            var start = source.IndexOf(startMarker, StringComparison.Ordinal);
            Assert.True(start >= 0, $"Could not find method start marker: {startMarker}");
            var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
            Assert.True(end > start, $"Could not find method end marker: {endMarker}");
            return source[start..end];
        }

        private static int CountOccurrences(string source, string value)
        {
            var count = 0;
            var offset = 0;
            while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += value.Length;
            }

            return count;
        }

        private static string ReadRepoFile(params string[] relativeSegments)
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                var candidateSegments = new string[relativeSegments.Length + 1];
                candidateSegments[0] = current.FullName;
                Array.Copy(relativeSegments, 0, candidateSegments, 1, relativeSegments.Length);
                var candidate = Path.Combine(candidateSegments);
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }

                current = current.Parent;
            }

            throw new FileNotFoundException($"Could not locate repository file: {string.Join("/", relativeSegments)}");
        }

        private static void RunSta(Action action)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }

        private sealed class FakePilotNotesDialogFactory : IPilotNotesDialogFactory
        {
            private readonly FakePilotNotesDialog _dialog;
            private readonly List<string> _calls;

            public FakePilotNotesDialogFactory(FakePilotNotesDialog dialog, List<string> calls)
            {
                _dialog = dialog;
                _calls = calls;
            }

            public PilotBoardRow? Row { get; private set; }

            public IPilotNotesDialog Create(PilotBoardRow row)
            {
                Row = row;
                _calls.Add($"create:{row.CharacterName}");
                return _dialog;
            }
        }

        private sealed class FakePilotNotesDialog : IPilotNotesDialog
        {
            private readonly List<string> _calls;

            public FakePilotNotesDialog(List<string> calls)
            {
                _calls = calls;
            }

            public bool Saved { get; set; }
            public Window? Owner { get; private set; }
            public bool Topmost { get; private set; }
            public ResourceDictionary? Resources { get; private set; }
            public bool ShowDialogCalled { get; private set; }

            public void ConfigureOwner(Window owner, bool topmost)
            {
                Owner = owner;
                Topmost = topmost;
                _calls.Add("configure-owner");
            }

            public void CopyResourcesFrom(ResourceDictionary resources)
            {
                Resources = resources;
                _calls.Add("copy-resources");
            }

            public void ShowDialog()
            {
                ShowDialogCalled = true;
                _calls.Add("show-dialog");
            }
        }
    }
}
