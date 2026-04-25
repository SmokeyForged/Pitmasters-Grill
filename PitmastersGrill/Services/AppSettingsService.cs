using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.IO;
using System.Text.Json;

namespace PitmastersGrill.Services
{
    public class AppSettingsService
    {
        private readonly string _settingsPath;

        public AppSettingsService()
        {
            _settingsPath = AppPaths.GetSettingsPath();
        }

        public AppSettings Load()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    AppLogger.AppInfo($"Settings file not found. Using defaults. path={_settingsPath}");
                    return Sanitize(new AppSettings());
                }

                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);

                var sanitized = Sanitize(settings ?? new AppSettings());

                AppLogger.AppInfo($"Settings loaded successfully. path={_settingsPath}");
                return sanitized;
            }
            catch (Exception ex)
            {
                AppLogger.AppWarn($"Failed to load settings. Using defaults. path={_settingsPath}");
                AppLogger.ErrorOnly("Settings load failure.", ex);
                return Sanitize(new AppSettings());
            }
        }

        public void Save(AppSettings settings)
        {
            try
            {
                var directory = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var sanitized = Sanitize(settings);

                var json = JsonSerializer.Serialize(
                    sanitized,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                File.WriteAllText(_settingsPath, json);
                AppLogger.AppInfo($"Settings saved successfully. path={_settingsPath}");
            }
            catch (Exception ex)
            {
                AppLogger.AppWarn($"Failed to save settings. path={_settingsPath}");
                AppLogger.ErrorOnly("Settings save failure.", ex);
            }
        }

        private static AppSettings Sanitize(AppSettings settings)
        {
            settings.MaxKillmailAgeDays =
                KillmailDatasetFreshnessService.NormalizeMaxKillmailAgeDays(settings.MaxKillmailAgeDays);

            if (!Enum.IsDefined(typeof(AppLogLevel), settings.LogLevel))
            {
                settings.LogLevel = AppLogLevel.Normal;
            }

            settings.PanelModeEnabled = settings.PanelModeEnabled;

            settings.ShowSigColumn ??= true;
            settings.ShowAllianceColumn ??= true;
            settings.ShowCorpColumn ??= true;
            settings.ShowKillsColumn ??= true;
            settings.ShowLossesColumn ??= true;
            settings.ShowAvgFleetSizeColumn ??= true;
            settings.ShowLastShipSeenColumn ??= true;
            settings.ShowLastSeenColumn ??= true;
            settings.ShowCynoHullSeenColumn ??= true;

            return settings;
        }
    }
}
