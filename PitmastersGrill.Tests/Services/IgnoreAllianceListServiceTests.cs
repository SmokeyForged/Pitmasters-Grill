using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public class IgnoreAllianceListServiceTests
    {
        [Fact]
        public void NormalizeRawAllianceIds_RemovesInvalidDuplicateAndNonPositiveEntries()
        {
            var service = new IgnoreAllianceListService();

            var result = service.NormalizeRawAllianceIds(new[] { " 44 ", "44", "0", "-5", "abc", "77" });

            Assert.Equal(new long[] { 44, 77 }, result.NormalizedAllianceIds);
            Assert.Equal(new[] { "-5", "0", "abc" }, result.InvalidEntries);
        }

        [Fact]
        public void NormalizeRawTypedEntries_NormalizesTypeAndDeduplicatesByIdAndType()
        {
            var service = new IgnoreAllianceListService();

            var result = service.NormalizeRawTypedEntries(new[] { "15", "15", "abc", "0", "21" }, (IgnoreEntryType)999, "unit-test");

            Assert.Equal(2, result.Entries.Count);
            Assert.All(result.Entries, entry => Assert.Equal(IgnoreEntryType.Unknown, entry.Type));
            Assert.Equal(new long[] { 15, 21 }, result.Entries.Select(x => x.Id).ToArray());
            Assert.Equal(new[] { "0", "abc" }, result.InvalidEntries);
        }

        [Fact]
        public void SaveTypedEntries_AndLoadState_PersistsSanitizedEntriesAndAllianceIds()
        {
            using var tempDirectory = new TempDirectory();
            var service = CreateService(tempDirectory.FilePath("ignore-alliances.json"));

            service.SaveTypedEntries(new[]
            {
                new TypedIgnoreEntry
                {
                    Id = 44,
                    Type = IgnoreEntryType.Alliance,
                    ResolvedName = "",
                    UpdatedAtUtc = "2026-05-01T00:00:00.0000000Z"
                },
                new TypedIgnoreEntry
                {
                    Id = 44,
                    Type = IgnoreEntryType.Alliance,
                    ResolvedName = "Goonswarm Federation",
                    UpdatedAtUtc = "2026-05-02T00:00:00.0000000Z"
                },
                new TypedIgnoreEntry
                {
                    Id = 88,
                    Type = IgnoreEntryType.Corporation,
                    ResolvedName = "Brave Corp",
                    UpdatedAtUtc = "2026-05-02T00:00:00.0000000Z"
                }
            });

            var state = service.LoadState();

            Assert.Equal(new long[] { 44 }, state.AllianceIds);
            Assert.Equal(2, state.Entries.Count);
            Assert.Equal(IgnoreEntryType.Alliance, state.Entries.Single(x => x.Id == 44).Type);
            Assert.Equal("Unresolved", state.Entries.Single(x => x.Id == 44).DisplayName);
            Assert.Equal(IgnoreEntryType.Corporation, state.Entries.Single(x => x.Id == 88).Type);
            Assert.Equal("Brave Corp", state.Entries.Single(x => x.Id == 88).DisplayName);
        }

        [Fact]
        public void SaveAllianceIds_WritesLegacyAllianceEntriesAsTypedAlliances()
        {
            using var tempDirectory = new TempDirectory();
            var service = CreateService(tempDirectory.FilePath("ignore-alliances.json"));

            service.SaveAllianceIds(new long[] { 300, 0, 200, 300, -5 });

            var state = service.LoadState();

            Assert.Equal(new long[] { 200, 300 }, state.AllianceIds);
            Assert.Equal(2, state.Entries.Count);
            Assert.All(state.Entries, entry => Assert.Equal(IgnoreEntryType.Alliance, entry.Type));
        }

        [Fact]
        public void LoadState_MigratesLegacyAllianceIdsIntoTypedEntries()
        {
            using var tempDirectory = new TempDirectory();
            var path = tempDirectory.FilePath("ignore-alliances.json");
            File.WriteAllText(
                path,
                """
                {
                  "AllianceIds": [ 901, 900 ],
                  "Entries": []
                }
                """);
            var service = CreateService(path);

            var state = service.LoadState();

            Assert.Equal(new long[] { 900, 901 }, state.AllianceIds);
            Assert.Equal(2, state.Entries.Count);
            Assert.All(state.Entries, entry => Assert.Equal(IgnoreEntryType.Alliance, entry.Type));
        }

        [Fact]
        public void LoadAllianceIds_ReturnsOnlyAllianceTypedEntries()
        {
            using var tempDirectory = new TempDirectory();
            var service = CreateService(tempDirectory.FilePath("ignore-alliances.json"));
            service.SaveTypedEntries(new[]
            {
                new TypedIgnoreEntry { Id = 300, Type = IgnoreEntryType.Alliance, ResolvedName = "Alliance A" },
                new TypedIgnoreEntry { Id = 400, Type = IgnoreEntryType.Corporation, ResolvedName = "Corp B" },
                new TypedIgnoreEntry { Id = 500, Type = IgnoreEntryType.Alliance, ResolvedName = "Alliance C" }
            });

            var result = service.LoadAllianceIds();

            Assert.Equal(new long[] { 300, 500 }, result.OrderBy(x => x).ToArray());
        }

        private static IgnoreAllianceListService CreateService(string path)
        {
            var service = new IgnoreAllianceListService();
            var field = typeof(IgnoreAllianceListService).GetField("_ignoreAllianceListPath", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field!.SetValue(service, path);
            return service;
        }

        private sealed class TempDirectory : IDisposable
        {
            public TempDirectory()
            {
                Root = Path.Combine(Path.GetTempPath(), "PitmastersGrill.Tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Root);
            }

            public string Root { get; }

            public string FilePath(params string[] segments)
            {
                return Path.Combine(new[] { Root }.Concat(segments).ToArray());
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Root))
                    {
                        Directory.Delete(Root, recursive: true);
                    }
                }
                catch
                {
                }
            }
        }
    }
}
