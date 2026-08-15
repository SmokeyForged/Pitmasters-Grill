using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using PitmastersGrill.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class IntelStorageSettingsControllerTests
    {
        [Fact]
        public void InitializeSettingsUi_ProjectsRetentionAndConfiguredPath()
        {
            RunOnStaThread(() =>
            {
                const string configuredPath = @"C:\PMG\KillmailData";
                var controller = CreateController(
                    getEffectiveKillmailDataPath: () => configuredPath,
                    getKillmailDataPathSourceDescription: () => "settings override");
                var settings = new AppSettings
                {
                    MaxKillmailAgeDays = 45,
                    KillmailDataRootPath = configuredPath
                };
                var maxAgeTextBox = new TextBox();
                var effectiveAgeText = new TextBlock();
                var pathTextBox = new TextBox();
                var pathModeText = new TextBlock();
                var effectivePathText = new TextBlock();

                controller.InitializeSettingsUi(
                    settings,
                    maxAgeTextBox,
                    effectiveAgeText,
                    pathTextBox,
                    pathModeText,
                    effectivePathText);

                Assert.Equal("45", maxAgeTextBox.Text);
                Assert.Equal("Effective max killmail age: 45 days", effectiveAgeText.Text);
                Assert.Equal(configuredPath, pathTextBox.Text);
                Assert.Equal("Source: settings override", pathModeText.Text);
                Assert.Equal($"Effective path: {configuredPath}", effectivePathText.Text);
            });
        }

        [Fact]
        public void SaveMaxKillmailAge_InvalidInputPreservesSettingAndDoesNotSave()
        {
            RunOnStaThread(() =>
            {
                var saves = new List<AppSettings>();
                var messages = new List<string>();
                var controller = CreateController(
                    saveSettings: settings => saves.Add(settings),
                    showMessage: (message, _, _) => messages.Add(message));
                var settings = new AppSettings { MaxKillmailAgeDays = 45 };
                var textBox = new TextBox { Text = "not-a-number" };
                var effective = new TextBlock();

                controller.SaveMaxKillmailAge(settings, textBox, effective);

                Assert.Equal(45, settings.MaxKillmailAgeDays);
                Assert.Equal("45", textBox.Text);
                Assert.Empty(saves);
                Assert.Single(messages);
                Assert.Contains("whole number", messages[0], StringComparison.OrdinalIgnoreCase);
            });
        }

        [Fact]
        public void SaveMaxKillmailAge_NormalizesAndPersists()
        {
            RunOnStaThread(() =>
            {
                var saveCalls = 0;
                var controller = CreateController(saveSettings: _ => saveCalls++);
                var settings = new AppSettings { MaxKillmailAgeDays = 30 };
                var textBox = new TextBox { Text = "999" };
                var effective = new TextBlock();

                controller.SaveMaxKillmailAge(settings, textBox, effective);

                Assert.Equal(KillmailDatasetFreshnessService.MaximumMaxKillmailAgeDays, settings.MaxKillmailAgeDays);
                Assert.Equal("365", textBox.Text);
                Assert.Equal("Effective max killmail age: 365 days", effective.Text);
                Assert.Equal(1, saveCalls);
            });
        }

        [Fact]
        public void ResetMaxKillmailAgeToDefault_PersistsDefault()
        {
            RunOnStaThread(() =>
            {
                var saveCalls = 0;
                var controller = CreateController(saveSettings: _ => saveCalls++);
                var settings = new AppSettings { MaxKillmailAgeDays = 120 };
                var textBox = new TextBox();
                var effective = new TextBlock();

                controller.ResetMaxKillmailAgeToDefault(settings, textBox, effective);

                Assert.Equal(KillmailDatasetFreshnessService.DefaultMaxKillmailAgeDays, settings.MaxKillmailAgeDays);
                Assert.Equal("30", textBox.Text);
                Assert.Equal("Effective max killmail age: 30 days", effective.Text);
                Assert.Equal(1, saveCalls);
            });
        }

        [Fact]
        public void SaveKillmailPath_CustomPathCreatesDirectoryPersistsAndShowsRestartGuidance()
        {
            RunOnStaThread(() =>
            {
                var saves = 0;
                string? createdPath = null;
                var messages = new List<string>();
                var customPath = Path.Combine(Path.GetTempPath(), $"pmg-phase7-{Guid.NewGuid():N}");
                var controller = CreateController(
                    saveSettings: _ => saves++,
                    createDirectory: path => createdPath = path,
                    showMessage: (message, _, _) => messages.Add(message),
                    getEffectiveKillmailDataPath: () => customPath,
                    getKillmailDataPathSourceDescription: () => "settings override");
                var settings = new AppSettings();
                var pathTextBox = new TextBox { Text = customPath };
                var pathModeText = new TextBlock();
                var effectivePathText = new TextBlock();

                controller.SaveKillmailPath(settings, pathTextBox, pathModeText, effectivePathText);

                Assert.Equal(customPath, settings.KillmailDataRootPath);
                Assert.Equal(KillmailPaths.ExpandPathTokens(customPath), createdPath);
                Assert.Equal(1, saves);
                Assert.Equal("Source: settings override", pathModeText.Text);
                Assert.Equal($"Effective path: {customPath}", effectivePathText.Text);
                Assert.Single(messages);
                Assert.Contains("Restart PMG", messages[0], StringComparison.Ordinal);
                Assert.Contains("not migrated automatically", messages[0], StringComparison.OrdinalIgnoreCase);
            });
        }

        [Fact]
        public void SaveKillmailPath_BlankValueRestoresDefaultWithoutCreatingDirectory()
        {
            RunOnStaThread(() =>
            {
                var saves = 0;
                var createCalls = 0;
                var controller = CreateController(
                    saveSettings: _ => saves++,
                    createDirectory: _ => createCalls++);
                var settings = new AppSettings { KillmailDataRootPath = @"C:\OldPmgData" };
                var pathTextBox = new TextBox { Text = "  " };
                var pathModeText = new TextBlock();
                var effectivePathText = new TextBlock();

                controller.SaveKillmailPath(settings, pathTextBox, pathModeText, effectivePathText);

                Assert.Equal(string.Empty, settings.KillmailDataRootPath);
                Assert.Equal(KillmailPaths.GetDefaultKillmailDataDirectoryDisplayPath(), pathTextBox.Text);
                Assert.Equal("Source: default %LOCALAPPDATA%", pathModeText.Text);
                Assert.Equal(
                    $"Effective path: {KillmailPaths.GetDefaultKillmailDataDirectoryDisplayPath()}",
                    effectivePathText.Text);
                Assert.Equal(1, saves);
                Assert.Equal(0, createCalls);
            });
        }

        [Fact]
        public void ResetKillmailPathToDefault_PreservesDefaultSemanticsAndRestartGuidance()
        {
            RunOnStaThread(() =>
            {
                var saves = 0;
                var messages = new List<string>();
                var controller = CreateController(
                    saveSettings: _ => saves++,
                    showMessage: (message, _, _) => messages.Add(message));
                var settings = new AppSettings { KillmailDataRootPath = @"D:\Intel" };
                var pathTextBox = new TextBox();
                var pathModeText = new TextBlock();
                var effectivePathText = new TextBlock();

                controller.ResetKillmailPathToDefault(settings, pathTextBox, pathModeText, effectivePathText);

                Assert.Equal(string.Empty, settings.KillmailDataRootPath);
                Assert.Equal(KillmailPaths.GetDefaultKillmailDataDirectoryDisplayPath(), pathTextBox.Text);
                Assert.Equal("Source: default %LOCALAPPDATA%", pathModeText.Text);
                Assert.Equal(1, saves);
                Assert.Single(messages);
                Assert.Contains("Restart PMG", messages[0], StringComparison.Ordinal);
            });
        }

        [Fact]
        public void AppearanceAndStorageSources_EnforceSeparatedOwnership()
        {
            var appearance = ReadRepoFile("PitmastersGrill", "Services", "MainWindowAppearanceController.cs");
            var storage = ReadRepoFile("PitmastersGrill", "Services", "IntelStorageSettingsController.cs");
            var compatibility = ReadRepoFile("PitmastersGrill", "Services", "MainWindowAppearanceControllerStorageCompatibility.cs");

            Assert.DoesNotContain("MaxKillmailAge", appearance);
            Assert.DoesNotContain("KillmailDataRootPath", appearance);
            Assert.DoesNotContain("KillmailPaths", appearance);
            Assert.DoesNotContain("Directory.CreateDirectory", appearance);

            Assert.Contains("SaveMaxKillmailAge", storage);
            Assert.Contains("ResetMaxKillmailAgeToDefault", storage);
            Assert.Contains("SaveKillmailPath", storage);
            Assert.Contains("ResetKillmailPathToDefault", storage);
            Assert.Contains("KillmailDatasetFreshnessService.NormalizeMaxKillmailAgeDays", storage);
            Assert.Contains("KillmailPaths.NormalizeForComparison", storage);
            Assert.Contains("_createDirectory", storage);
            Assert.Contains("Restart PMG", storage);

            Assert.Contains("StorageSettingsController.SaveMaxKillmailAge", compatibility);
            Assert.Contains("StorageSettingsController.SaveKillmailPath", compatibility);
            Assert.DoesNotContain("KillmailPaths.", compatibility);
            Assert.DoesNotContain("Directory.CreateDirectory", compatibility);
        }

        [Fact]
        public void AppearanceController_OpacityCoercionRemainsStable()
        {
            var controller = new MainWindowAppearanceController(new AppSettingsService());

            Assert.Equal(35, controller.CoerceOpacityPercent(1));
            Assert.Equal(100, controller.CoerceOpacityPercent(150));
            Assert.Equal(73, controller.CoerceOpacityPercent(72.6));
        }

        private static IntelStorageSettingsController CreateController(
            Action<AppSettings>? saveSettings = null,
            Action<string>? createDirectory = null,
            Action<string, string, MessageBoxImage>? showMessage = null,
            Func<string>? getEffectiveKillmailDataPath = null,
            Func<string>? getKillmailDataPathSourceDescription = null)
        {
            return new IntelStorageSettingsController(
                saveSettings ?? (_ => { }),
                createDirectory ?? (_ => { }),
                showMessage ?? ((_, _, _) => { }),
                getEffectiveKillmailDataPath ?? KillmailPaths.GetDefaultKillmailDataDirectoryDisplayPath,
                getKillmailDataPathSourceDescription ?? (() => "default %LOCALAPPDATA%"));
        }

        private static string ReadRepoFile(params string[] relativeSegments)
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);

            while (current is not null)
            {
                var projectPath = Path.Combine(current.FullName, "PitmastersGrill", "PitmastersGrill.csproj");
                if (File.Exists(projectPath))
                {
                    var pathSegments = new string[relativeSegments.Length + 1];
                    pathSegments[0] = current.FullName;
                    Array.Copy(relativeSegments, 0, pathSegments, 1, relativeSegments.Length);
                    return File.ReadAllText(Path.Combine(pathSegments));
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the Pitmasters-Grill repository root from the test output directory.");
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
