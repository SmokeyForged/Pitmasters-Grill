using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System;
using System.Linq;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class CurrentBoardSessionTests
    {
        [Fact]
        public void ProcessingGeneration_IsMonotonicAndRejectsStaleGeneration()
        {
            using var session = new CurrentBoardSession();

            var first = session.BeginProcessingGeneration();
            var second = session.BeginProcessingGeneration();

            Assert.Equal(1, first);
            Assert.Equal(2, second);
            Assert.False(session.IsCurrentGeneration(first));
            Assert.True(session.IsCurrentGeneration(second));
            Assert.Equal(second, session.CurrentGeneration);
        }

        [Fact]
        public void ReplaceRows_KeepsStableViewAndOwnsSingleRelevantPropertySubscription()
        {
            using var session = new CurrentBoardSession();
            var rowsView = session.Rows;
            var row = NewRow("One");
            var relevantChanges = 0;
            session.Changed += (_, args) =>
            {
                if (args.Kind == CurrentBoardSessionChangeKind.BoardRelevantRowStateChanged)
                {
                    relevantChanges++;
                }
            };

            session.ReplaceRows(new[] { row });
            session.ReplaceRows(new[] { row });
            row.IsWatched = true;

            Assert.Same(rowsView, session.Rows);
            Assert.Single(session.Rows);
            Assert.Same(row, session.Rows[0]);
            Assert.Equal(1, relevantChanges);
        }

        [Fact]
        public void ReplaceRows_UnsubscribesRowsThatLeaveTheSession()
        {
            using var session = new CurrentBoardSession();
            var first = NewRow("First");
            var second = NewRow("Second");
            var relevantChanges = 0;
            session.Changed += (_, args) =>
            {
                if (args.Kind == CurrentBoardSessionChangeKind.BoardRelevantRowStateChanged)
                {
                    relevantChanges++;
                }
            };

            session.ReplaceRows(new[] { first });
            session.ReplaceRows(new[] { second });

            first.BaitOverride = true;
            second.BaitOverride = true;

            Assert.Equal(1, relevantChanges);
            Assert.Single(session.Rows);
            Assert.Same(second, session.Rows[0]);
        }

        [Fact]
        public void RemoveRow_UnsubscribesRemovedRow()
        {
            using var session = new CurrentBoardSession();
            var row = NewRow("Remove Me");
            var relevantChanges = 0;
            session.Changed += (_, args) =>
            {
                if (args.Kind == CurrentBoardSessionChangeKind.BoardRelevantRowStateChanged)
                {
                    relevantChanges++;
                }
            };
            session.ReplaceRows(new[] { row });

            Assert.True(session.RemoveRow(row));
            row.IsWatched = true;

            Assert.Empty(session.Rows);
            Assert.Equal(0, relevantChanges);
        }

        [Fact]
        public void ClearAndInvalidate_EmptiesRowsUnsubscribesAndInvalidatesGeneration()
        {
            using var session = new CurrentBoardSession();
            var row = NewRow("Clear Me");
            session.ReplaceRows(new[] { row });
            var activeGeneration = session.BeginProcessingGeneration();
            var relevantChanges = 0;
            session.Changed += (_, args) =>
            {
                if (args.Kind == CurrentBoardSessionChangeKind.BoardRelevantRowStateChanged)
                {
                    relevantChanges++;
                }
            };

            var invalidatedGeneration = session.ClearAndInvalidate();
            row.AllianceName = "Former Alliance";

            Assert.Equal(activeGeneration + 1, invalidatedGeneration);
            Assert.False(session.IsCurrentGeneration(activeGeneration));
            Assert.True(session.IsCurrentGeneration(invalidatedGeneration));
            Assert.Empty(session.Rows);
            Assert.Equal(0, relevantChanges);
        }

        [Fact]
        public void ReorderRows_RequiresExactlyTheActiveRows()
        {
            using var session = new CurrentBoardSession();
            var alpha = NewRow("Alpha");
            var bravo = NewRow("Bravo");
            session.ReplaceRows(new[] { alpha, bravo });

            session.ReorderRows(new[] { bravo, alpha });

            Assert.Equal(new[] { "Bravo", "Alpha" }, session.Rows.Select(row => row.CharacterName));
            Assert.Throws<InvalidOperationException>(() => session.ReorderRows(new[] { alpha }));
        }

        private static PilotBoardRow NewRow(string characterName)
        {
            return new PilotBoardRow
            {
                CharacterName = characterName,
                CharacterId = Guid.NewGuid().GetHashCode().ToString()
            };
        }
    }
}
