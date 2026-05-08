using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using PitmastersGrill.Services;
using System;
using System.IO;
using System.Windows;
using System.Reflection;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class DetailPaneControllerTests : IDisposable
    {
        private readonly string _tempDirectory;

        public DetailPaneControllerTests()
        {
            _tempDirectory = Path.Combine(
                Path.GetTempPath(),
                "PitmastersGrill.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        [Fact]
        public void GetSelectedOrDisplayedDetailRow_ReturnsSelectedRowFirst()
        {
            var controller = CreateController();
            var selectedRow = new PilotBoardRow { CharacterName = "Aura" };
            var displayedRow = new PilotBoardRow { CharacterName = "Chribba" };

            var result = controller.GetSelectedOrDisplayedDetailRow(
                selectedRow,
                Visibility.Visible,
                "Chribba",
                new[] { selectedRow, displayedRow });

            Assert.Same(selectedRow, result);
        }

        [Fact]
        public void GetSelectedOrDisplayedDetailRow_UsesDisplayedCharacterWhenVisible()
        {
            var controller = CreateController();
            var row = new PilotBoardRow { CharacterName = "Chribba" };

            var result = controller.GetSelectedOrDisplayedDetailRow(
                null,
                Visibility.Visible,
                "  chribba  ",
                new[] { row });

            Assert.Same(row, result);
        }

        [Fact]
        public void IsRowDisplayedInDetailPane_ReturnsTrueWhenDisplayedCharacterMatches()
        {
            var controller = CreateController();
            var row = new PilotBoardRow { CharacterName = "Aura" };

            var result = controller.IsRowDisplayedInDetailPane(
                row,
                null,
                Visibility.Visible,
                "Aura");

            Assert.True(result);
        }

        [Fact]
        public void SaveCurrentNotesAndTags_UpdatesSelectedRowWhenCharacterMatches()
        {
            var controller = CreateController();
            var row = new PilotBoardRow { CharacterName = "Aura" };
            SetActiveDetailCharacter(controller, row.CharacterName);

            controller.SaveCurrentNotesAndTags("notes", knownCynoOverride: true, baitOverride: true, row);

            Assert.True(row.KnownCynoOverride);
            Assert.True(row.BaitOverride);
        }

        private DetailPaneController CreateController()
        {
            return new DetailPaneController(
                new NotesRepository(Path.Combine(_tempDirectory, "notes.json")),
                new PilotBoardRowDetailFormatter(new BoardPopulationRetryPolicy()));
        }

        private static void SetActiveDetailCharacter(DetailPaneController controller, string characterName)
        {
            var field = typeof(DetailPaneController).GetField(
                "_activeDetailCharacterName",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(field);
            field!.SetValue(controller, characterName);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDirectory))
                {
                    Directory.Delete(_tempDirectory, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
