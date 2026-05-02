using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public class AppSettingsServiceTests
    {
        [Fact]
        public void Load_ReturnsSanitizedDefaultsWhenSettingsFileIsMissing()
        {
            using var tempDirectory = new TempDirectory();
            var service = CreateService(tempDirectory.FilePath("settings.json"));

            var result = service.Load();

            Assert.True(result.PanelModeEnabled);
            Assert.Equal(12, result.BoardTextSize);
            Assert.Equal(string.Empty, result.BoardFontFamily);
            Assert.Equal(PilotDetailPlacementPreference.AutoPreferRight.ToString(), result.PilotDetailPlacementPreference);
            Assert.Equal(AppLogLevel.Normal, result.LogLevel);
            Assert.NotNull(result.BoardColumnLayout);
        }

        [Fact]
        public void Save_AndLoad_RoundTripSanitizedSettings()
        {
            using var tempDirectory = new TempDirectory();
            var service = CreateService(tempDirectory.FilePath("settings.json"));

            service.Save(new AppSettings
            {
                PanelModeEnabled = false,
                BoardTextSize = 99,
                BoardFontFamily = "Comic Sans MS",
                MaxKillmailAgeDays = 999,
                BackgroundHistoricalRepairDelaySeconds = -1,
                BackgroundHistoricalRepairCooldownHours = 999,
                BackgroundHistoricalRepairLookbackDays = 0,
                BackgroundHistoricalRepairMaxPilotsPerRun = 999,
                BackgroundHistoricalRepairRecentPilotWindowDays = 0,
                PilotDetailPlacementPreference = "InvalidPlacement",
                LogLevel = (AppLogLevel)999,
                ShowSigColumn = null,
                BoardColumnLayout = null!
            });

            var result = service.Load();

            Assert.True(result.PanelModeEnabled);
            Assert.Equal(16, result.BoardTextSize);
            Assert.Equal(string.Empty, result.BoardFontFamily);
            Assert.Equal(365, result.MaxKillmailAgeDays);
            Assert.Equal(30, result.BackgroundHistoricalRepairDelaySeconds);
            Assert.Equal(168, result.BackgroundHistoricalRepairCooldownHours);
            Assert.Equal(3, result.BackgroundHistoricalRepairLookbackDays);
            Assert.Equal(250, result.BackgroundHistoricalRepairMaxPilotsPerRun);
            Assert.Equal(14, result.BackgroundHistoricalRepairRecentPilotWindowDays);
            Assert.Equal(PilotDetailPlacementPreference.AutoPreferRight.ToString(), result.PilotDetailPlacementPreference);
            Assert.Equal(AppLogLevel.Normal, result.LogLevel);
            Assert.True(result.ShowSigColumn);
            Assert.NotNull(result.BoardColumnLayout);
            Assert.Empty(result.BoardColumnLayout);
        }

        [Fact]
        public void Load_ReturnsDefaultsWhenSettingsJsonIsInvalid()
        {
            using var tempDirectory = new TempDirectory();
            var settingsPath = tempDirectory.FilePath("settings.json");
            File.WriteAllText(settingsPath, "{ not-valid-json");
            var service = CreateService(settingsPath);

            var result = service.Load();

            Assert.True(result.PanelModeEnabled);
            Assert.Equal(12, result.BoardTextSize);
            Assert.Equal(string.Empty, result.BoardFontFamily);
        }

        [Fact]
        public void Save_WritesIndentedJsonToConfiguredPath()
        {
            using var tempDirectory = new TempDirectory();
            var settingsPath = tempDirectory.FilePath("nested", "settings.json");
            var service = CreateService(settingsPath);

            service.Save(new AppSettings
            {
                BoardTextSize = 14,
                BoardFontFamily = "Consolas",
                BoardColumnLayout = new List<BoardColumnLayoutSetting>
                {
                    new()
                    {
                        ColumnKey = "SigColumn",
                        DisplayIndex = 0,
                        WidthValue = 42,
                        WidthUnitType = "Pixel"
                    }
                }
            });

            Assert.True(File.Exists(settingsPath));
            var json = File.ReadAllText(settingsPath);
            using var document = JsonDocument.Parse(json);
            Assert.Equal(14, document.RootElement.GetProperty("BoardTextSize").GetInt32());
            Assert.Contains(Environment.NewLine, json, StringComparison.Ordinal);
        }

        [Fact]
        public void Load_PreservesSupportedBoardFontAndValidPlacement()
        {
            using var tempDirectory = new TempDirectory();
            var settingsPath = tempDirectory.FilePath("settings.json");
            File.WriteAllText(
                settingsPath,
                """
                {
                  "BoardTextSize": 13,
                  "BoardFontFamily": "Bahnschrift",
                  "PilotDetailPlacementPreference": "AutoPreferLeft",
                  "ShowSigColumn": false
                }
                """);
            var service = CreateService(settingsPath);

            var result = service.Load();

            Assert.Equal(13, result.BoardTextSize);
            Assert.Equal("Bahnschrift", result.BoardFontFamily);
            Assert.Equal(PilotDetailPlacementPreference.AutoPreferLeft.ToString(), result.PilotDetailPlacementPreference);
            Assert.False(result.ShowSigColumn);
        }

        private static AppSettingsService CreateService(string settingsPath)
        {
            var service = new AppSettingsService();
            SetPrivateField(service, "_settingsPath", settingsPath);
            return service;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field!.SetValue(target, value);
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
