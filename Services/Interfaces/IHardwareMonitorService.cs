using System;

namespace SmartFanCooling.Services.Interfaces
{
    /// <summary>
    /// Contract for hardware telemetry sensor monitoring service.
    /// </summary>
    public interface IHardwareMonitorService : IDisposable
    {
        float CpuTemperature { get; }
        float CpuUsage { get; }
        float CpuPowerW { get; }
        float CpuMaxClockGHz { get; }
        int HardwareCpuFanRpm { get; }

        float GpuTemperature { get; }
        float GpuUsage { get; }
        float GpuPowerW { get; }
        float GpuClockMHz { get; }
        float GpuVramUsedGB { get; }
        int HardwareGpuFanRpm { get; }

        string CpuName { get; }
        string GpuName { get; }

        float RamUsagePercent { get; }
        float RamUsedGB { get; }
        float RamTotalGB { get; }

        void UpdateSensors();
    }
}
