using PitmastersGrill.Models;
using System;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace PitmastersGrill.Services
{
    internal static class MainWindowAppearanceControllerStorageCompatibility
    {
        private sealed class CompatibilityState
        {
            public required IntelStorageSettingsController StorageSettingsController { get; init; }
            public required Action<AppSettings> SaveSettings { get; init; }
        }

        private static readonly ConditionalWeakTable<MainWindowAppearanceController, CompatibilityState> States = new();

        public static void Register(
            MainWindowAppearanceController appearanceController,
            IntelStorageSettingsController storageSettingsController,
            Action<AppSettings> saveSettings)
        {
            ArgumentNullException.ThrowIfNull(appearanceController);
            ArgumentNullException.ThrowIfNull(storageSettingsController);
            ArgumentNullException.ThrowIfNull(saveSettings);

            States.Remove(appearanceController);
            States.Add(appearanceController, new CompatibilityState
            {
                StorageSettingsController = storageSettingsController,
                SaveSettings = saveSettings
            });
        }

        public static void SaveSettings(
            this MainWindowAppearanceController appearanceController,
            AppSettings settings)
        {
            GetState(appearanceController).SaveSettings(settings);
        }

        public static int GetMaxKillmailAgeDaysSettingValue(
            this MainWindowAppearanceController appearanceController,
            AppSettings settings)
        {
            return GetState(appearanceController)
                .StorageSettingsController
                .GetMaxKillmailAgeDaysSettingValue(settings);
        }

        public static void SaveMaxKillmailAge(
            this MainWindowAppearanceController appearanceController,
            AppSettings settings,
            TextBox maxKillmailAgeDaysTextBox,
            TextBlock effectiveMaxKillmailAgeText)
        {
            GetState(appearanceController).StorageSettingsController.SaveMaxKillmailAge(
                settings,
                maxKillmailAgeDaysTextBox,
                effectiveMaxKillmailAgeText);
        }

        public static void ResetMaxKillmailAgeToDefault(
            this MainWindowAppearanceController appearanceController,
            AppSettings settings,
            TextBox maxKillmailAgeDaysTextBox,
            TextBlock effectiveMaxKillmailAgeText)
        {
            GetState(appearanceController).StorageSettingsController.ResetMaxKillmailAgeToDefault(
                settings,
                maxKillmailAgeDaysTextBox,
                effectiveMaxKillmailAgeText);
        }

        public static void SaveKillmailPath(
            this MainWindowAppearanceController appearanceController,
            AppSettings settings,
            TextBox killmailDataRootPathTextBox,
            TextBlock killmailDataPathModeText,
            TextBlock effectiveKillmailDataPathText)
        {
            GetState(appearanceController).StorageSettingsController.SaveKillmailPath(
                settings,
                killmailDataRootPathTextBox,
                killmailDataPathModeText,
                effectiveKillmailDataPathText);
        }

        public static void ResetKillmailPathToDefault(
            this MainWindowAppearanceController appearanceController,
            AppSettings settings,
            TextBox killmailDataRootPathTextBox,
            TextBlock killmailDataPathModeText,
            TextBlock effectiveKillmailDataPathText)
        {
            GetState(appearanceController).StorageSettingsController.ResetKillmailPathToDefault(
                settings,
                killmailDataRootPathTextBox,
                killmailDataPathModeText,
                effectiveKillmailDataPathText);
        }

        private static CompatibilityState GetState(MainWindowAppearanceController appearanceController)
        {
            ArgumentNullException.ThrowIfNull(appearanceController);

            if (States.TryGetValue(appearanceController, out var state))
            {
                return state;
            }

            throw new InvalidOperationException(
                "MainWindow appearance/storage compatibility routing was not registered by composition.");
        }
    }
}
