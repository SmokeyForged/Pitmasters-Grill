using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public class TypedIgnoreActionCoordinatorTests
    {
        [Fact]
        public void TryAdd_Pilot_MapsCharacterIdentityAndPersists()
        {
            using var fixture = new Fixture();
            var row = new PilotBoardRow
            {
                CharacterId = " 101 ",
                CharacterName = "  Alice Example  "
            };

            var result = fixture.Subject.TryAdd(row, IgnoreEntryType.Pilot);

            Assert.Equal(TypedIgnoreActionOutcome.Added, result.Outcome);
            Assert.Equal(IgnoreEntryType.Pilot, result.Type);
            Assert.Equal(101, result.Id);
            Assert.Equal("Alice Example", result.DisplayName);
            var entry = Assert.Single(fixture.IgnoreCoordinator.GetIgnoredEntries());
            Assert.Equal(101, entry.Id);
            Assert.Equal(IgnoreEntryType.Pilot, entry.Type);
            Assert.Equal("Alice Example", entry.DisplayName);
            Assert.Equal("detail window ignore Pilot", entry.Source);
        }

        [Fact]
        public void TryAdd_Corporation_MapsCorporationIdentityAndPersists()
        {
            using var fixture = new Fixture();
            var row = new PilotBoardRow
            {
                CorpId = "202",
                CorpName = "Example Corp"
            };

            var result = fixture.Subject.TryAdd(row, IgnoreEntryType.Corporation);

            Assert.Equal(TypedIgnoreActionOutcome.Added, result.Outcome);
            Assert.Equal(202, result.Id);
            var entry = Assert.Single(fixture.IgnoreCoordinator.GetIgnoredEntries());
            Assert.Equal(IgnoreEntryType.Corporation, entry.Type);
            Assert.Equal("Example Corp", entry.DisplayName);
            Assert.Equal("detail window ignore Corporation", entry.Source);
        }

        [Fact]
        public void TryAdd_Alliance_MapsAllianceIdentityAndPersists()
        {
            using var fixture = new Fixture();
            var row = new PilotBoardRow
            {
                AllianceId = "303",
                AllianceName = "Example Alliance"
            };

            var result = fixture.Subject.TryAdd(row, IgnoreEntryType.Alliance);

            Assert.Equal(TypedIgnoreActionOutcome.Added, result.Outcome);
            Assert.Equal(303, result.Id);
            var entry = Assert.Single(fixture.IgnoreCoordinator.GetIgnoredEntries());
            Assert.Equal(IgnoreEntryType.Alliance, entry.Type);
            Assert.Equal("Example Alliance", entry.DisplayName);
            Assert.Equal("detail window ignore Alliance", entry.Source);
        }

        [Theory]
        [InlineData("")]
        [InlineData("0")]
        [InlineData("-7")]
        [InlineData("not-an-id")]
        public void TryAdd_InvalidPilotId_DoesNotPersist(string rawId)
        {
            using var fixture = new Fixture();
            var row = new PilotBoardRow
            {
                CharacterId = rawId,
                CharacterName = "Invalid Pilot"
            };

            var result = fixture.Subject.TryAdd(row, IgnoreEntryType.Pilot);

            Assert.Equal(TypedIgnoreActionOutcome.InvalidId, result.Outcome);
            Assert.Null(result.Id);
            Assert.Empty(fixture.IgnoreCoordinator.GetIgnoredEntries());
        }

        [Fact]
        public void TryAdd_UnsupportedType_DoesNotPersist()
        {
            using var fixture = new Fixture();
            var row = new PilotBoardRow
            {
                CharacterId = "101",
                CharacterName = "Alice Example"
            };

            var result = fixture.Subject.TryAdd(row, (IgnoreEntryType)999);

            Assert.Equal(TypedIgnoreActionOutcome.InvalidId, result.Outcome);
            Assert.Null(result.Id);
            Assert.Empty(fixture.IgnoreCoordinator.GetIgnoredEntries());
        }

        [Fact]
        public void TryAdd_BlankDisplayName_UsesUnresolvedFallback()
        {
            using var fixture = new Fixture();
            var row = new PilotBoardRow
            {
                AllianceId = "303",
                AllianceName = "   "
            };

            var result = fixture.Subject.TryAdd(row, IgnoreEntryType.Alliance);

            Assert.Equal(TypedIgnoreActionOutcome.Added, result.Outcome);
            Assert.Equal("Unresolved", result.DisplayName);
            Assert.Equal("Unresolved", Assert.Single(fixture.IgnoreCoordinator.GetIgnoredEntries()).DisplayName);
        }

        [Fact]
        public void TryAdd_ExistingEntry_ReturnsAlreadyPresentWithoutDuplication()
        {
            using var fixture = new Fixture();
            var row = new PilotBoardRow
            {
                CorpId = "202",
                CorpName = "Example Corp"
            };

            var first = fixture.Subject.TryAdd(row, IgnoreEntryType.Corporation);
            var second = fixture.Subject.TryAdd(row, IgnoreEntryType.Corporation);

            Assert.Equal(TypedIgnoreActionOutcome.Added, first.Outcome);
            Assert.Equal(TypedIgnoreActionOutcome.AlreadyPresent, second.Outcome);
            Assert.Single(fixture.IgnoreCoordinator.GetIgnoredEntries());
            Assert.Single(fixture.ListService.LoadTypedEntries());
        }

        private sealed class Fixture : IDisposable
        {
            private readonly string _root;

            public Fixture()
            {
                _root = Path.Combine(Path.GetTempPath(), "PitmastersGrill.Tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_root);

                ListService = new IgnoreAllianceListService();
                var field = typeof(IgnoreAllianceListService).GetField(
                    "_ignoreAllianceListPath",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(field);
                field!.SetValue(ListService, Path.Combine(_root, "ignore-alliances.json"));

                IgnoreCoordinator = new IgnoreAllianceCoordinator(
                    ListService,
                    new IgnoreAllianceFilterService());
                Subject = new TypedIgnoreActionCoordinator(IgnoreCoordinator);
            }

            public IgnoreAllianceListService ListService { get; }
            public IgnoreAllianceCoordinator IgnoreCoordinator { get; }
            public TypedIgnoreActionCoordinator Subject { get; }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(_root))
                    {
                        Directory.Delete(_root, recursive: true);
                    }
                }
                catch
                {
                }
            }
        }
    }
}
