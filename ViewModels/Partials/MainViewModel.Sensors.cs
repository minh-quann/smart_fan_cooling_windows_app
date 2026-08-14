using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartFanCooling.Services;

namespace SmartFanCooling.ViewModels
{
    /// <summary>
    /// Partial class for Hardware Sensor Management — allows users to toggle specific sub-metrics
    /// for CPU, GPU, RAM, Laptop Fans, Motherboard, and SSD drives to optimize CPU overhead.
    /// </summary>
    public partial class MainViewModel
    {
        // Category Monitoring Toggles
        [ObservableProperty] private bool _enableCpuMonitoring = true;
        [ObservableProperty] private bool _enableGpuMonitoring = true;
        [ObservableProperty] private bool _enableRamMonitoring = true;
        [ObservableProperty] private bool _enableMotherboardMonitoring = true;
        [ObservableProperty] private bool _enableStorageMonitoring = true;
        [ObservableProperty] private bool _enableLaptopFanMonitoring = true;

        // Granular Sub-Metric Toggles — CPU
        [ObservableProperty] private bool _enableCpuTemp = true;
        [ObservableProperty] private bool _enableCpuUsage = true;
        [ObservableProperty] private bool _enableCpuClock = true;
        [ObservableProperty] private bool _enableCpuPower = true;
        [ObservableProperty] private bool _enableCpuFanRpm = true;

        // Granular Sub-Metric Toggles — GPU
        [ObservableProperty] private bool _enableGpuTemp = true;
        [ObservableProperty] private bool _enableGpuHotSpotTemp = true;
        [ObservableProperty] private bool _enableGpuMemoryTemp = true;
        [ObservableProperty] private bool _enableGpuUsage = true;
        [ObservableProperty] private bool _enableGpuClock = true;
        [ObservableProperty] private bool _enableGpuPower = true;
        [ObservableProperty] private bool _enableGpuVramUsed = true;
        [ObservableProperty] private bool _enableGpuFanRpm = true;

        // Granular Sub-Metric Toggles — RAM
        [ObservableProperty] private bool _enableRamUsagePercent = true;
        [ObservableProperty] private bool _enableRamUsedGB = true;

        // Granular Sub-Metric Toggles — Motherboard & VRM
        [ObservableProperty] private bool _enableMotherboardTemp = true;
        [ObservableProperty] private bool _enableVrmTemp = true;

        // Granular Sub-Metric Toggles — Storage / SSD
        [ObservableProperty] private bool _enableSsdTemp = true;

        /// <summary>
        /// Synchronizes UI sensor toggle properties to HardwareMonitorService instance.
        /// </summary>
        private void SyncSensorTogglesToService()
        {
            if (_hardwareService != null)
            {
                _hardwareService.EnableCpuMonitoring = EnableCpuMonitoring;
                _hardwareService.EnableGpuMonitoring = EnableGpuMonitoring;
                _hardwareService.EnableRamMonitoring = EnableRamMonitoring;
                _hardwareService.EnableMotherboardMonitoring = EnableMotherboardMonitoring;
                _hardwareService.EnableStorageMonitoring = EnableStorageMonitoring;
                _hardwareService.EnableLaptopFanMonitoring = EnableLaptopFanMonitoring;

                _hardwareService.EnableCpuTemp = EnableCpuTemp;
                _hardwareService.EnableCpuUsage = EnableCpuUsage;
                _hardwareService.EnableCpuClock = EnableCpuClock;
                _hardwareService.EnableCpuPower = EnableCpuPower;
                _hardwareService.EnableCpuFanRpm = EnableCpuFanRpm;

                _hardwareService.EnableGpuTemp = EnableGpuTemp;
                _hardwareService.EnableGpuHotSpotTemp = EnableGpuHotSpotTemp;
                _hardwareService.EnableGpuMemoryTemp = EnableGpuMemoryTemp;
                _hardwareService.EnableGpuUsage = EnableGpuUsage;
                _hardwareService.EnableGpuClock = EnableGpuClock;
                _hardwareService.EnableGpuPower = EnableGpuPower;
                _hardwareService.EnableGpuVramUsed = EnableGpuVramUsed;
                _hardwareService.EnableGpuFanRpm = EnableGpuFanRpm;

                _hardwareService.EnableRamUsagePercent = EnableRamUsagePercent;
                _hardwareService.EnableRamUsedGB = EnableRamUsedGB;

                _hardwareService.EnableMotherboardTemp = EnableMotherboardTemp;
                _hardwareService.EnableVrmTemp = EnableVrmTemp;

                _hardwareService.EnableSsdTemp = EnableSsdTemp;
            }
        }

        // OnChanged callbacks to auto-save settings & auto-toggle child sub-metrics
#pragma warning disable MVVMTK0034
        partial void OnEnableCpuMonitoringChanged(bool value)
        {
            SetProperty(ref _enableCpuTemp, value, nameof(EnableCpuTemp));
            SetProperty(ref _enableCpuUsage, value, nameof(EnableCpuUsage));
            SetProperty(ref _enableCpuClock, value, nameof(EnableCpuClock));
            SetProperty(ref _enableCpuPower, value, nameof(EnableCpuPower));
            SetProperty(ref _enableCpuFanRpm, value, nameof(EnableCpuFanRpm));
            OnPropertyChanged(nameof(EnableCpuTemp));
            OnPropertyChanged(nameof(EnableCpuUsage));
            OnPropertyChanged(nameof(EnableCpuClock));
            OnPropertyChanged(nameof(EnableCpuPower));
            OnPropertyChanged(nameof(EnableCpuFanRpm));
            SaveCurrentSystemSettings();
        }

        partial void OnEnableGpuMonitoringChanged(bool value)
        {
            SetProperty(ref _enableGpuTemp, value, nameof(EnableGpuTemp));
            SetProperty(ref _enableGpuHotSpotTemp, value, nameof(EnableGpuHotSpotTemp));
            SetProperty(ref _enableGpuMemoryTemp, value, nameof(EnableGpuMemoryTemp));
            SetProperty(ref _enableGpuUsage, value, nameof(EnableGpuUsage));
            SetProperty(ref _enableGpuClock, value, nameof(EnableGpuClock));
            SetProperty(ref _enableGpuPower, value, nameof(EnableGpuPower));
            SetProperty(ref _enableGpuVramUsed, value, nameof(EnableGpuVramUsed));
            SetProperty(ref _enableGpuFanRpm, value, nameof(EnableGpuFanRpm));
            OnPropertyChanged(nameof(EnableGpuTemp));
            OnPropertyChanged(nameof(EnableGpuHotSpotTemp));
            OnPropertyChanged(nameof(EnableGpuMemoryTemp));
            OnPropertyChanged(nameof(EnableGpuUsage));
            OnPropertyChanged(nameof(EnableGpuClock));
            OnPropertyChanged(nameof(EnableGpuPower));
            OnPropertyChanged(nameof(EnableGpuVramUsed));
            OnPropertyChanged(nameof(EnableGpuFanRpm));
            SaveCurrentSystemSettings();
        }

        partial void OnEnableRamMonitoringChanged(bool value)
        {
            SetProperty(ref _enableRamUsagePercent, value, nameof(EnableRamUsagePercent));
            SetProperty(ref _enableRamUsedGB, value, nameof(EnableRamUsedGB));
            OnPropertyChanged(nameof(EnableRamUsagePercent));
            OnPropertyChanged(nameof(EnableRamUsedGB));
            SaveCurrentSystemSettings();
        }

        partial void OnEnableMotherboardMonitoringChanged(bool value)
        {
            SetProperty(ref _enableMotherboardTemp, value, nameof(EnableMotherboardTemp));
            SetProperty(ref _enableVrmTemp, value, nameof(EnableVrmTemp));
            OnPropertyChanged(nameof(EnableMotherboardTemp));
            OnPropertyChanged(nameof(EnableVrmTemp));
            SaveCurrentSystemSettings();
        }

        partial void OnEnableStorageMonitoringChanged(bool value)
        {
            SetProperty(ref _enableSsdTemp, value, nameof(EnableSsdTemp));
            OnPropertyChanged(nameof(EnableSsdTemp));
            SaveCurrentSystemSettings();
        }
#pragma warning restore MVVMTK0034

        partial void OnEnableLaptopFanMonitoringChanged(bool value) => SaveCurrentSystemSettings();

        partial void OnEnableCpuTempChanged(bool value) => SaveCurrentSystemSettings();
        partial void OnEnableCpuUsageChanged(bool value) => SaveCurrentSystemSettings();
        partial void OnEnableCpuClockChanged(bool value) => SaveCurrentSystemSettings();
        partial void OnEnableCpuPowerChanged(bool value) => SaveCurrentSystemSettings();
        partial void OnEnableCpuFanRpmChanged(bool value) => SaveCurrentSystemSettings();

        partial void OnEnableGpuTempChanged(bool value) => SaveCurrentSystemSettings();
        partial void OnEnableGpuHotSpotTempChanged(bool value) => SaveCurrentSystemSettings();
        partial void OnEnableGpuMemoryTempChanged(bool value) => SaveCurrentSystemSettings();
        partial void OnEnableGpuUsageChanged(bool value) => SaveCurrentSystemSettings();
        partial void OnEnableGpuClockChanged(bool value) => SaveCurrentSystemSettings();
        partial void OnEnableGpuPowerChanged(bool value) => SaveCurrentSystemSettings();
        partial void OnEnableGpuVramUsedChanged(bool value) => SaveCurrentSystemSettings();
        partial void OnEnableGpuFanRpmChanged(bool value) => SaveCurrentSystemSettings();

        partial void OnEnableRamUsagePercentChanged(bool value) => SaveCurrentSystemSettings();
        partial void OnEnableRamUsedGBChanged(bool value) => SaveCurrentSystemSettings();

        partial void OnEnableMotherboardTempChanged(bool value) => SaveCurrentSystemSettings();
        partial void OnEnableVrmTempChanged(bool value) => SaveCurrentSystemSettings();

        partial void OnEnableSsdTempChanged(bool value) => SaveCurrentSystemSettings();

        [RelayCommand]
        public void SetAllSensors()
        {
            EnableCpuMonitoring = true;
            EnableGpuMonitoring = true;
            EnableRamMonitoring = true;
            EnableMotherboardMonitoring = true;
            EnableStorageMonitoring = true;
            EnableLaptopFanMonitoring = true;

            EnableCpuTemp = true;
            EnableCpuUsage = true;
            EnableCpuClock = true;
            EnableCpuPower = true;
            EnableCpuFanRpm = true;

            EnableGpuTemp = true;
            EnableGpuHotSpotTemp = true;
            EnableGpuMemoryTemp = true;
            EnableGpuUsage = true;
            EnableGpuClock = true;
            EnableGpuPower = true;
            EnableGpuVramUsed = true;
            EnableGpuFanRpm = true;

            EnableRamUsagePercent = true;
            EnableRamUsedGB = true;
            EnableMotherboardTemp = true;
            EnableVrmTemp = true;
            EnableSsdTemp = true;

            StatusMessage = "🔄 Đã bật theo dõi 100% tất cả thông số cảm biến phần cứng.";
        }

        [RelayCommand]
        public void SetOledPresetSensors()
        {
            EnableCpuMonitoring = true;
            EnableGpuMonitoring = true;
            EnableRamMonitoring = true;
            EnableLaptopFanMonitoring = true;
            EnableMotherboardMonitoring = false;
            EnableStorageMonitoring = false;

            EnableCpuTemp = true;
            EnableCpuUsage = true;
            EnableCpuClock = true;
            EnableCpuPower = false;
            EnableCpuFanRpm = true;

            EnableGpuTemp = true;
            EnableGpuHotSpotTemp = false;
            EnableGpuMemoryTemp = false;
            EnableGpuUsage = true;
            EnableGpuClock = true;
            EnableGpuPower = false;
            EnableGpuVramUsed = true;
            EnableGpuFanRpm = true;

            EnableRamUsagePercent = true;
            EnableRamUsedGB = true;
            EnableMotherboardTemp = false;
            EnableVrmTemp = false;
            EnableSsdTemp = false;

            StatusMessage = "⚡ Đã cài đặt cấu hình tối ưu cho 2 màn hình OLED (Chỉ bật chỉ số hiển thị OLED, tắt WMI SSD/Mainboard giúp CPU < 0.3%).";
        }

        [RelayCommand]
        public void SetSuperEcoPresetSensors()
        {
            EnableCpuMonitoring = true;
            EnableGpuMonitoring = true;
            EnableRamMonitoring = false;
            EnableLaptopFanMonitoring = false;
            EnableMotherboardMonitoring = false;
            EnableStorageMonitoring = false;

            EnableCpuTemp = true;
            EnableCpuUsage = true;
            EnableCpuClock = false;
            EnableCpuPower = false;
            EnableCpuFanRpm = false;

            EnableGpuTemp = true;
            EnableGpuHotSpotTemp = false;
            EnableGpuMemoryTemp = false;
            EnableGpuUsage = true;
            EnableGpuClock = false;
            EnableGpuPower = false;
            EnableGpuVramUsed = false;
            EnableGpuFanRpm = false;

            EnableRamUsagePercent = false;
            EnableRamUsedGB = false;
            EnableMotherboardTemp = false;
            EnableVrmTemp = false;
            EnableSsdTemp = false;

            StatusMessage = "🍃 Đã bật chế độ Siêu Tiết Kiệm (Super Eco) — Chỉ đọc Nhiệt độ & Tải CPU/GPU để tự động chỉnh quạt.";
        }
    }
}
