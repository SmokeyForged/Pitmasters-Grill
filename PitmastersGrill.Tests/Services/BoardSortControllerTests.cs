using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class BoardSortControllerTests
    {
        [Fact]
        public void ResetManualBoardSort_SetsCharacterColumnAscending()
        {
            RunOnStaThread(() =>
            {
                var controller = new BoardSortController();
                var grid = new DataGrid();
                var characterColumn = new DataGridTextColumn
                {
                    SortMemberPath = nameof(PilotBoardRow.CharacterName),
                    Binding = new Binding(nameof(PilotBoardRow.CharacterName))
                };
                var killsColumn = new DataGridTextColumn
                {
                    SortMemberPath = nameof(PilotBoardRow.KillCount),
                    Binding = new Binding(nameof(PilotBoardRow.KillCount))
                };
                grid.Columns.Add(characterColumn);
                grid.Columns.Add(killsColumn);

                controller.ResetManualBoardSort(grid, characterColumn);

                Assert.Equal(ListSortDirection.Ascending, characterColumn.SortDirection);
                Assert.Null(killsColumn.SortDirection);
            });
        }

        [Fact]
        public void TryHandleSorting_TogglesDirectionAndReordersRows()
        {
            RunOnStaThread(() =>
            {
                var controller = new BoardSortController();
                var grid = new DataGrid();
                var killsColumn = new DataGridTextColumn
                {
                    SortMemberPath = nameof(PilotBoardRow.KillCount),
                    Binding = new Binding(nameof(PilotBoardRow.KillCount))
                };
                grid.Columns.Add(killsColumn);
                var rows = new ObservableCollection<PilotBoardRow>
                {
                    new() { CharacterName = "Alpha", KillCount = 2 },
                    new() { CharacterName = "Bravo", KillCount = 9 }
                };
                var firstHandled = controller.TryHandleSorting(
                    grid,
                    killsColumn,
                    rows,
                    rows[0],
                    out var firstMember,
                    out var firstDirection);

                Assert.True(firstHandled);
                Assert.Equal(nameof(PilotBoardRow.KillCount), firstMember);
                Assert.Equal(ListSortDirection.Ascending, firstDirection);
                Assert.Equal("Alpha", rows[0].CharacterName);

                var secondHandled = controller.TryHandleSorting(
                    grid,
                    killsColumn,
                    rows,
                    rows[0],
                    out var secondMember,
                    out var secondDirection);

                Assert.True(secondHandled);
                Assert.Equal(nameof(PilotBoardRow.KillCount), secondMember);
                Assert.Equal(ListSortDirection.Descending, secondDirection);
                Assert.Equal("Bravo", rows[0].CharacterName);
                Assert.Equal(ListSortDirection.Descending, killsColumn.SortDirection);
            });
        }

        [Fact]
        public void ApplyCurrentBoardOrdering_PrioritizesWatchedRows()
        {
            var controller = new BoardSortController();
            var rows = new ObservableCollection<PilotBoardRow>
            {
                new() { CharacterName = "Alpha", IsWatched = false },
                new() { CharacterName = "Bravo", IsWatched = true }
            };

            controller.ApplyCurrentBoardOrdering(rows, null, _ => { });

            Assert.Equal("Bravo", rows[0].CharacterName);
            Assert.Equal("Alpha", rows[1].CharacterName);
        }

        private static void RunOnStaThread(Action action)
        {
            Exception? capturedException = null;

            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    capturedException = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (capturedException != null)
            {
                ExceptionDispatchInfo.Capture(capturedException).Throw();
            }
        }
    }
}
