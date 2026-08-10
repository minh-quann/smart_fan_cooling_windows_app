using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using LibreHardwareMonitor.Hardware;

using SmartFanCooling.Services.Interfaces;

namespace SmartFanCooling.Services
{
    /// <summary>
    /// Reads CPU/GPU temperatures, powers, clocks and System sensor telemetry using LibreHardwareMonitor with fail-safe WMI & Registry fallbacks.
    /// </summary>
    public class HardwareMonitorService : IHardwareMonitorService, IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out long lpIdleTime, out long lpKernelTime, out long lpUserTime);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX()
            {
                this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        private long _prevIdleTime = 0;
        private long _prevKernelTime = 0;
        private long _prevUserTime = 0;

        private readonly Computer? _computer;
        private readonly bool _lhmInitialized;

        public float CpuTemperature { get; private set; }
        public float CpuUsage { get; private set; }
        public float CpuPowerW { get; private set; }
        public float CpuMaxClockGHz { get; private set; }
        public int HardwareCpuFanRpm { get; private set; }

        public float GpuTemperature { get; private set; }
        public float GpuUsage { get; private set; }
        public float GpuPowerW { get; private set; }
        public float GpuClockMHz { get; private set; }
        public float GpuVramUsedGB { get; private set; }
        public int HardwareGpuFanRpm { get; private set; }

        public string CpuName { get; private set; } = "CPU";
        public string GpuName { get; private set; } = "GPU";

        public float RamUsagePercent { get; private set; }
        public float RamUsedGB { get; private set; }
        public float RamTotalGB { get; private set; } = 16.0f;

        public HardwareMonitorService()
        {
            try
            {
                _computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMemoryEnabled = true,
                    IsMotherboardEnabled = true,
                    IsControllerEnabled = true
                };
                _computer.Open();
                _lhmInitialized = true;
            }
            catch
            {
                _lhmInitialized = false;
            }
        }

        public void UpdateSensors()
        {
            HardwareCpuFanRpm = 0;
            HardwareGpuFanRpm = 0;

            // Always calculate accurate system CPU Usage % via Win32 GetSystemTimes API
            CpuUsage = CalculateCpuUsageWin32();

            if (_lhmInitialized && _computer != null)
            {
                try
                {
                    float maxClockMHz = 0f;

                    // Prioritize dedicated GPU (Nvidia first, discrete AMD second, iGPU last)
                    var sortedHardware = _computer.Hardware
                        .OrderBy(h => h.HardwareType == HardwareType.GpuNvidia ? 0 :
                                     (h.HardwareType == HardwareType.GpuAmd && !h.Name.Contains("Radeon(TM) Graphics", StringComparison.OrdinalIgnoreCase) ? 1 :
                                     (h.HardwareType == HardwareType.Cpu ? 3 :
                                     (h.HardwareType == HardwareType.Memory ? 4 : 5))));

                    foreach (var hardware in sortedHardware)
                    {
                        hardware.Update();

                        bool isDedicatedGpu = hardware.HardwareType == HardwareType.GpuNvidia ||
                                              (hardware.HardwareType == HardwareType.GpuAmd && !hardware.Name.Contains("Radeon(TM) Graphics", StringComparison.OrdinalIgnoreCase));

                        if (hardware.HardwareType == HardwareType.Cpu)
                        {
                            if (!string.IsNullOrEmpty(hardware.Name)) CpuName = hardware.Name;
                        }
                        else if (hardware.HardwareType == HardwareType.GpuNvidia ||
                                 hardware.HardwareType == HardwareType.GpuAmd ||
                                 hardware.HardwareType == HardwareType.GpuIntel)
                        {
                            if (!string.IsNullOrEmpty(hardware.Name) && (GpuName == "GPU" || isDedicatedGpu))
                            {
                                GpuName = hardware.Name;
                            }
                        }

                        foreach (var sensor in hardware.Sensors)
                        {
                            // 1. CPU Sensors
                            if (hardware.HardwareType == HardwareType.Cpu)
                            {
                                if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue && sensor.Value.Value > 0)
                                {
                                    if (sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) || CpuTemperature == 0f)
                                    {
                                        CpuTemperature = (float)Math.Round(sensor.Value.Value);
                                    }
                                }
                                else if (sensor.SensorType == SensorType.Power && sensor.Value.HasValue && sensor.Value.Value > 0)
                                {
                                    if (sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) || CpuPowerW == 0f)
                                    {
                                        CpuPowerW = (float)Math.Round(sensor.Value.Value, 1);
                                    }
                                }
                                else if (sensor.SensorType == SensorType.Clock && sensor.Value.HasValue && sensor.Value.Value > maxClockMHz)
                                {
                                    if (!sensor.Name.Contains("Bus", StringComparison.OrdinalIgnoreCase))
                                    {
                                        maxClockMHz = sensor.Value.Value;
                                    }
                                }
                                else if (sensor.SensorType == SensorType.Fan && sensor.Value.HasValue && sensor.Value.Value > 0)
                                {
                                    HardwareCpuFanRpm = (int)Math.Round(sensor.Value.Value);
                                }
                            }
                            // 2. GPU Sensors
                            else if (hardware.HardwareType == HardwareType.GpuNvidia ||
                                     hardware.HardwareType == HardwareType.GpuAmd ||
                                     hardware.HardwareType == HardwareType.GpuIntel)
                            {
                                bool allowOverride = isDedicatedGpu || GpuName == hardware.Name;

                                if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue && sensor.Value.Value > 0)
                                {
                                    if (!sensor.Name.Contains("Hot Spot", StringComparison.OrdinalIgnoreCase) &&
                                        !sensor.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (allowOverride || GpuTemperature == 0f) GpuTemperature = (float)Math.Round(sensor.Value.Value);
                                    }
                                }
                                else if (sensor.SensorType == SensorType.Load && sensor.Value.HasValue && sensor.Value.Value >= 0)
                                {
                                    string sName = sensor.Name;
                                    float val = sensor.Value.Value;

                                    if (sName.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                                        sName.Equals("GPU", StringComparison.OrdinalIgnoreCase) ||
                                        sName.Contains("GPU Core", StringComparison.OrdinalIgnoreCase) ||
                                        sName.Contains("3D", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (!sName.Contains("Memory", StringComparison.OrdinalIgnoreCase) &&
                                            !sName.Contains("Video", StringComparison.OrdinalIgnoreCase) &&
                                            !sName.Contains("Bus", StringComparison.OrdinalIgnoreCase))
                                        {
                                            if (allowOverride || GpuUsage == 0f || val > GpuUsage)
                                            {
                                                GpuUsage = (float)Math.Round(val);
                                            }
                                        }
                                    }
                                    else if (sName.Contains("Memory", StringComparison.OrdinalIgnoreCase) && !sName.Contains("Controller", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if ((allowOverride || GpuVramUsedGB == 0f) && val > 0 && GpuVramUsedGB == 0f)
                                        {
                                            GpuVramUsedGB = (float)Math.Round(8.0f * (val / 100.0f), 1);
                                        }
                                    }
                                }
                                else if (sensor.SensorType == SensorType.Power && sensor.Value.HasValue && sensor.Value.Value >= 0)
                                {
                                    if (allowOverride || GpuPowerW == 0f || sensor.Value.Value > 0)
                                    {
                                        GpuPowerW = (float)Math.Round(sensor.Value.Value, 1);
                                    }
                                }
                                else if (sensor.SensorType == SensorType.Clock && sensor.Value.HasValue && sensor.Value.Value > 0)
                                {
                                    if (sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) || GpuClockMHz == 0f)
                                    {
                                        if (allowOverride || GpuClockMHz == 0f) GpuClockMHz = (float)Math.Round(sensor.Value.Value);
                                    }
                                }
                                else if ((sensor.SensorType == SensorType.SmallData || sensor.SensorType == SensorType.Data) && sensor.Value.HasValue && sensor.Value.Value > 0)
                                {
                                    if (sensor.Name.Contains("Memory Used", StringComparison.OrdinalIgnoreCase) ||
                                        sensor.Name.Contains("VRAM Used", StringComparison.OrdinalIgnoreCase) ||
                                        sensor.Name.Contains("Dedicated", StringComparison.OrdinalIgnoreCase))
                                    {
                                        float rawVal = sensor.Value.Value;
                                        float gbVal = rawVal > 100.0f ? (rawVal / 1024.0f) : rawVal;
                                        if (allowOverride || GpuVramUsedGB == 0f) GpuVramUsedGB = (float)Math.Round(gbVal, 1);
                                    }
                                }
                                else if (sensor.SensorType == SensorType.Fan && sensor.Value.HasValue && sensor.Value.Value > 0)
                                {
                                    if (allowOverride || HardwareGpuFanRpm == 0) HardwareGpuFanRpm = (int)Math.Round(sensor.Value.Value);
                                }
                            }
                            // 3. RAM Sensors
                            else if (hardware.HardwareType == HardwareType.Memory)
                            {
                                if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase) && sensor.Value.HasValue)
                                {
                                    RamUsagePercent = (float)Math.Round(sensor.Value.Value);
                                }
                                else if (sensor.SensorType == SensorType.Data && sensor.Name.Contains("Used", StringComparison.OrdinalIgnoreCase) && sensor.Value.HasValue)
                                {
                                    RamUsedGB = (float)Math.Round(sensor.Value.Value, 1);
                                }
                            }
                            // 4. Motherboard Fans
                            else if (hardware.HardwareType == HardwareType.Motherboard)
                            {
                                if (sensor.SensorType == SensorType.Fan && sensor.Value.HasValue && sensor.Value.Value > 0)
                                {
                                    string nameLower = sensor.Name.ToLower();
                                    if (nameLower.Contains("cpu") || nameLower.Contains("fan #1") || nameLower.Contains("fan 1"))
                                    {
                                        HardwareCpuFanRpm = (int)Math.Round(sensor.Value.Value);
                                    }
                                    else if (nameLower.Contains("gpu") || nameLower.Contains("fan #2") || nameLower.Contains("fan 2"))
                                    {
                                        HardwareGpuFanRpm = (int)Math.Round(sensor.Value.Value);
                                    }
                                }
                            }
                        }

                        foreach (var subHardware in hardware.SubHardware)
                        {
                            subHardware.Update();
                            foreach (var sensor in subHardware.Sensors)
                            {
                                if (sensor.SensorType == SensorType.Fan && sensor.Value.HasValue && sensor.Value.Value > 0)
                                {
                                    string nameLower = sensor.Name.ToLower();
                                    if (nameLower.Contains("cpu") || nameLower.Contains("fan #1"))
                                    {
                                        HardwareCpuFanRpm = (int)Math.Round(sensor.Value.Value);
                                    }
                                    else if (nameLower.Contains("gpu") || nameLower.Contains("fan #2"))
                                    {
                                        HardwareGpuFanRpm = (int)Math.Round(sensor.Value.Value);
                                    }
                                }
                            }
                        }
                    }

                    if (maxClockMHz > 0)
                    {
                        CpuMaxClockGHz = (float)Math.Round(maxClockMHz / 1000.0f, 2);
                    }
                }
                catch { }
            }

            // 1. Fallback for CPU Max Clock (via Windows Registry & Win32_Processor)
            if (CpuMaxClockGHz == 0f)
            {
                try
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                    {
                        if (key != null)
                        {
                            object? mhzObj = key.GetValue("~MHz");
                            if (mhzObj != null)
                            {
                                double mhz = Convert.ToDouble(mhzObj);
                                if (mhz > 0) CpuMaxClockGHz = (float)Math.Round(mhz / 1000.0, 2);
                            }
                        }
                    }
                }
                catch { }

                if (CpuMaxClockGHz == 0f)
                {
                    try
                    {
                        using (var searcher = new ManagementObjectSearcher(@"root\cimv2", "SELECT CurrentClockSpeed FROM Win32_Processor"))
                        {
                            using (var collection = searcher.Get())
                            {
                                foreach (ManagementObject obj in collection)
                                {
                                    using (obj)
                                    {
                                        if (obj["CurrentClockSpeed"] != null)
                                        {
                                            double mhz = Convert.ToDouble(obj["CurrentClockSpeed"]);
                                            if (mhz > 0)
                                            {
                                                CpuMaxClockGHz = (float)Math.Round(mhz / 1000.0, 2);
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            // 2. Fallback for CPU Temperature (via ACPI Thermal Zone)
            if (CpuTemperature == 0f)
            {
                try
                {
                    using (var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature"))
                    {
                        using (var collection = searcher.Get())
                        {
                            foreach (ManagementObject obj in collection)
                            {
                                using (obj)
                                {
                                    if (obj["CurrentTemperature"] != null)
                                    {
                                        double raw = Convert.ToDouble(obj["CurrentTemperature"]);
                                        double celsius = Math.Round((raw - 2732.0) / 10.0, 1);
                                        if (celsius > 20.0 && celsius < 115.0)
                                        {
                                            CpuTemperature = (float)celsius;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            // 3. Fallback for CPU Power (Estimated TDP calculation based on CPU Usage %)
            if (CpuPowerW == 0f && CpuUsage > 0f)
            {
                CpuPowerW = (float)Math.Round(5.0f + (40.0f * (CpuUsage / 100.0f)), 1);
            }

            // 4. Fallback for GPU Usage % via Native Windows WMI (GPU Engine -> 3D)
            if (GpuUsage == 0f)
            {
                float wmiGpuUsage = ReadGpuUsageWmi();
                if (wmiGpuUsage > 0f)
                {
                    GpuUsage = wmiGpuUsage;
                }
            }

            // 5. Fallback for GPU VRAM Used Estimation
            if (GpuVramUsedGB <= 0.1f)
            {
                if (GpuUsage > 0f)
                {
                    GpuVramUsedGB = (float)Math.Round(1.2f + (5.5f * (GpuUsage / 100.0f)), 1);
                }
                else if (GpuClockMHz > 500f)
                {
                    GpuVramUsedGB = 1.2f; // Standard idle VRAM reservation for 8GB Dedicated Laptop GPU
                }
            }

            // 6. Fallback for GPU Power (W) via TDP Estimation
            if (GpuPowerW == 0f)
            {
                if (GpuUsage > 0f)
                {
                    GpuPowerW = (float)Math.Round(15.0f + (100.0f * (GpuUsage / 100.0f)), 1);
                }
                else if (GpuClockMHz > 500f)
                {
                    GpuPowerW = (float)Math.Round(14.5f + ((GpuClockMHz / 1560.0f) * 10.5f), 1);
                }
            }

            // Fallback: ASUS ATK WMI Query for ROG/TUF Laptop Fans (DSTS 0x00110013 & 0x00110014)
            if (HardwareCpuFanRpm == 0 || HardwareGpuFanRpm == 0)
            {
                try
                {
                    ManagementObject? asusControl = null;
                    var scope = new ManagementScope(@"\root\wmi");
                    var query = new SelectQuery("SELECT * FROM AsusAtkWmi_WMNB");
                    using (var searcher = new ManagementObjectSearcher(scope, query))
                    {
                        using (var collection = searcher.Get())
                        {
                            foreach (ManagementObject obj in collection)
                            {
                                asusControl = obj;
                                break;
                            }
                        }
                    }

                    if (asusControl != null)
                    {
                        using (asusControl)
                        {
                            if (HardwareCpuFanRpm == 0)
                            {
                                using (var inParams = asusControl.GetMethodParameters("DSTS"))
                                {
                                    if (inParams != null)
                                    {
                                        inParams["Device_id"] = 0x00110013u;
                                        using (var outParams = asusControl.InvokeMethod("DSTS", inParams, null))
                                        {
                                            if (outParams != null)
                                            {
                                                object? rawObj = outParams["device_status"] ?? outParams["Data"];
                                                if (rawObj != null)
                                                {
                                                    uint val = Convert.ToUInt32(rawObj);
                                                    uint rpm = val & 0xFFFFu;
                                                    if (rpm > 0 && rpm <= 120) HardwareCpuFanRpm = (int)(rpm * 100);
                                                    else if (rpm > 120) HardwareCpuFanRpm = (int)rpm;
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            if (HardwareGpuFanRpm == 0)
                            {
                                using (var inParams = asusControl.GetMethodParameters("DSTS"))
                                {
                                    if (inParams != null)
                                    {
                                        inParams["Device_id"] = 0x00110014u;
                                        using (var outParams = asusControl.InvokeMethod("DSTS", inParams, null))
                                        {
                                            if (outParams != null)
                                            {
                                                object? rawObj = outParams["device_status"] ?? outParams["Data"];
                                                if (rawObj != null)
                                                {
                                                    uint val = Convert.ToUInt32(rawObj);
                                                    uint rpm = val & 0xFFFFu;
                                                    if (rpm > 0 && rpm <= 120) HardwareGpuFanRpm = (int)(rpm * 100);
                                                    else if (rpm > 120) HardwareGpuFanRpm = (int)rpm;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            // Always read 100% exact System RAM metrics matching Task Manager via Win32 GlobalMemoryStatusEx API
            try
            {
                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    RamUsagePercent = (float)memStatus.dwMemoryLoad;
                    double totalGb = memStatus.ullTotalPhys / 1024.0 / 1024.0 / 1024.0;
                    double availGb = memStatus.ullAvailPhys / 1024.0 / 1024.0 / 1024.0;
                    double usedGb = totalGb - availGb;

                    RamTotalGB = (float)Math.Round(totalGb, 1);
                    RamUsedGB = (float)Math.Round(usedGb, 1);
                }
            }
            catch { }
        }

        private float CalculateCpuUsageWin32()
        {
            if (GetSystemTimes(out long idleTime, out long kernelTime, out long userTime))
            {
                if (_prevIdleTime == 0)
                {
                    _prevIdleTime = idleTime;
                    _prevKernelTime = kernelTime;
                    _prevUserTime = userTime;
                    return CpuUsage;
                }

                long usr = userTime - _prevUserTime;
                long ker = kernelTime - _prevKernelTime;
                long idl = idleTime - _prevIdleTime;
                long sys = usr + ker;

                if (sys > 0)
                {
                    float usage = (float)(sys - idl) * 100.0f / sys;
                    return (float)Math.Round(Math.Clamp(usage, 0.0f, 100.0f));
                }
            }
            return CpuUsage;
        }

        private float ReadGpuUsageWmi()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(@"root\cimv2", "SELECT UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine WHERE Name LIKE '%engtype_3D%'"))
                {
                    using (var collection = searcher.Get())
                    {
                        ulong maxVal = 0;
                        foreach (ManagementObject obj in collection)
                        {
                            using (obj)
                            {
                                if (obj["UtilizationPercentage"] != null)
                                {
                                    ulong val = Convert.ToUInt64(obj["UtilizationPercentage"]);
                                    if (val > maxVal) maxVal = val;
                                }
                            }
                        }
                        if (maxVal > 0) return (float)Math.Round((double)maxVal);
                    }
                }
            }
            catch { }
            return 0f;
        }

        public void Dispose()
        {
            if (_lhmInitialized && _computer != null)
            {
                try
                {
                    _computer.Close();
                }
                catch { }
            }
        }
    }
}
