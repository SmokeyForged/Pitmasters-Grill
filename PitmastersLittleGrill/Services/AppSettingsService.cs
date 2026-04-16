using PitmastersLittleGrill.Models;
using PitmastersLittleGrill.Persistence;
using System;
using System.IO;
using System.Text.Json;

namespace PitmastersLittleGrill.Services
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
                    return new AppSettings();
                }

                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);

                AppLogger.AppInfo($"Settings loaded successfully. path={_settingsPath}");
                return settings ?? new AppSettings();
            }
            catch (Exception ex)
            {
                AppLogger.AppWarn($"Failed to load settings. Using defaults. path={_settingsPath}");
                AppLogger.ErrorOnly("Settings load failure.", ex);
                return new AppSettings();
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

                var json = JsonSerializer.Serialize(
                    settings,
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
    }
}