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

        // Sensor Monitoring Category Toggles (allows users to disable unused sensors to minimize CPU usage)
        public bool EnableCpuMonitoring { get; set; } = true;
        public bool EnableGpuMonitoring { get; set; } = true;
        public bool EnableRamMonitoring { get; set; } = true;
        public bool EnableMotherboardMonitoring { get; set; } = true;
        public bool EnableStorageMonitoring { get; set; } = true;
        public bool EnableLaptopFanMonitoring { get; set; } = true;

        // Granular Sub-Metric Toggles
        public bool EnableCpuTemp { get; set; } = true;
        public bool EnableCpuUsage { get; set; } = true;
        public bool EnableCpuClock { get; set; } = true;
        public bool EnableCpuPower { get; set; } = true;
        public bool EnableCpuFanRpm { get; set; } = true;

        public bool EnableGpuTemp { get; set; } = true;
        public bool EnableGpuHotSpotTemp { get; set; } = true;
        public bool EnableGpuMemoryTemp { get; set; } = true;
        public bool EnableGpuUsage { get; set; } = true;
        public bool EnableGpuClock { get; set; } = true;
        public bool EnableGpuPower { get; set; } = true;
        public bool EnableGpuVramUsed { get; set; } = true;
        public bool EnableGpuFanRpm { get; set; } = true;

        public bool EnableRamUsagePercent { get; set; } = true;
        public bool EnableRamUsedGB { get; set; } = true;

        public bool EnableMotherboardTemp { get; set; } = true;
        public bool EnableVrmTemp { get; set; } = true;

        public bool EnableSsdTemp { get; set; } = true;
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
