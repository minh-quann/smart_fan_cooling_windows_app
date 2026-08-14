using System;
using System.IO;
using System.Text.Json;

namespace SmartFanCooling.Services
{
    /// <summary>
    /// Model for persisting application system settings.
    /// </summary>
    public class AppSettingsModel
    {
        public bool StartWithWindows { get; set; } = false;
        public bool StartMinimizedToTray { get; set; } = true;
        public bool MinimizeToTray { get; set; } = true;
        public string SelectedStartupPriority { get; set; } = "Cao (Khởi động trước - High Priority)";
        public string SelectedBaudRate { get; set; } = "115200";
        public int RefreshIntervalMs { get; set; } = 1000;
    }

    /// <summary>
    /// Service for loading and saving system configuration to app_settings.json.
    /// </summary>
    public static class AppSettingsService
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartFanCooling",
            "app_settings.json");

        /// <summary>
        /// Loads settings from disk or returns defaults if file does not exist.
        /// </summary>
        public static AppSettingsModel LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettingsModel>(json);
                    if (settings != null) return settings;
                }
            }
            catch { }
            return new AppSettingsModel();
        }

        /// <summary>
        /// Saves current settings model to disk as formatted JSON.
        /// </summary>
        public static void SaveSettings(AppSettingsModel settings)
        {
            try
            {
                string? dir = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch { }
        }
    }
}
