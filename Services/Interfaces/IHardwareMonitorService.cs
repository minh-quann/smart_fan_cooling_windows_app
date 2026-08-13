using System;

namespace SmartFanCooling.Services.Interfaces
{
    /// <summary>
    /// Contract for hardware telemetry sensor monitoring service.
    /// </summary>
    public interface IHardwareMonitorService : IDisposable
    {
        // Real-time CPU Telemetry
        float CpuTemperature { get; }
        float CpuUsage { get; }
        float CpuPowerW { get; }
        float CpuMaxClockGHz { get; }
        int HardwareCpuFanRpm { get; }

        // Real-time GPU Telemetry
        float GpuTemperature { get; }
        float GpuUsage { get; }
        float GpuPowerW { get; }
        float GpuClockMHz { get; }
        float GpuVramUsedGB { get; }
        int HardwareGpuFanRpm { get; }
        float GpuHotSpotTemp { get; }
        float GpuMemoryTemp { get; }

        // Hardware Names
        string CpuName { get; }
        string GpuName { get; }

        // Real-time RAM Telemetry
        float RamUsagePercent { get; }
        float RamUsedGB { get; }
        float RamTotalGB { get; }

        // Real-time Motherboard Telemetry
        float MotherboardTemp { get; }
        float VrmTemp { get; }

        // Real-time Storage Telemetry
        float SsdTempC { get; }
        string SsdName { get; }
        string DiskUsageInfo { get; }

        // Static System Information (loaded once at startup)
        string MotherboardName { get; }
        string BiosVersion { get; }
        string WindowsVersion { get; }
        int CpuCoreCount { get; }
        int CpuThreadCount { get; }
        string CpuBaseClockText { get; }
        string RamType { get; }
        int RamSpeed { get; }
        int RamSlotCount { get; }
        int RamSlotUsed { get; }
        float GpuVramTotalGB { get; }
        string StorageInfo { get; }
        string NetworkAdaptersInfo { get; }
        string WifiCardName { get; }
        string EthernetCardName { get; }

        void UpdateSensors();
    }
}
