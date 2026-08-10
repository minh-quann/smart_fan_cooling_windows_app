using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartFanCooling.ViewModels
{
    /// <summary>
    /// Partial class for hardware telemetry properties (CPU, GPU, RAM, Motherboard).
    /// </summary>
    public partial class MainViewModel
    {
        // CPU Telemetry
        [ObservableProperty] private string _cpuName = "Intel Core / AMD Ryzen";
        [ObservableProperty] private float _cpuTemp = 0f;
        [ObservableProperty] private float _cpuUsage = 0f;
        [ObservableProperty] private float _cpuPowerW = 0f;
        [ObservableProperty] private float _cpuMaxClockGHz = 0f;
        [ObservableProperty] private int _cpuFanRpm = 0;

        // GPU Telemetry
        [ObservableProperty] private string _gpuName = "NVIDIA / AMD GPU";
        [ObservableProperty] private float _gpuTemp = 0f;
        [ObservableProperty] private float _gpuUsage = 0f;
        [ObservableProperty] private float _gpuPowerW = 0f;
        [ObservableProperty] private float _gpuClockMHz = 0f;
        [ObservableProperty] private float _gpuVramUsedGB = 0f;
        [ObservableProperty] private int _gpuFanRpm = 0;

        // System RAM Telemetry
        [ObservableProperty] private float _ramUsagePercent = 0f;
        [ObservableProperty] private float _ramUsedGB = 0f;
        [ObservableProperty] private float _ramTotalGB = 16.0f;
        [ObservableProperty] private string _ramStatusText = "Bộ nhớ đã dùng: 0.0 GB / 0.0 GB";

        // Motherboard Telemetry
        [ObservableProperty] private float _motherboardTemp = 0f;
    }
}
