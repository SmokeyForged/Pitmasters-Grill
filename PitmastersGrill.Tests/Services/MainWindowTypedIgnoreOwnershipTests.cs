using System;
using System.IO;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class MainWindowTypedIgnoreOwnershipTests
    {
        [Fact]
        public void MainWindow_DoesNotOwnTypedIgnoreMappingOrPersistence()
        {
            var source = ReadRepoFile("PitmastersGrill", "MainWindow.xaml.cs");
            var coordinatorSource = ReadRepoFile("PitmastersGrill", "Services", "TypedIgnoreActionCoordinator.cs");

            Assert.DoesNotContain("GetIgnoreId", source, StringComparison.Ordinal);
            Assert.DoesNotContain("GetIgnoreDisplayName", source, StringComparison.Ordinal);
            Assert.DoesNotContain("AddEntryAndPersist", source, StringComparison.Ordinal);
            Assert.Contains("_typedIgnoreActionCoordinator.TryAdd", source, StringComparison.Ordinal);

            Assert.Contains("row.CharacterId", coordinatorSource, StringComparison.Ordinal);
            Assert.Contains("row.CorpId", coordinatorSource, StringComparison.Ordinal);
            Assert.Contains("row.AllianceId", coordinatorSource, StringComparison.Ordinal);
            Assert.Contains("AddEntryAndPersist", coordinatorSource, StringComparison.Ordinal);
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
    }
}
