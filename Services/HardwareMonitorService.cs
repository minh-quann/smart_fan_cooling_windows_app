using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;

namespace SmartFanCooling.Services
{
    /// <summary>
    /// Reads CPU/GPU temperatures, powers, clocks and System sensor telemetry using LibreHardwareMonitor and WMI fallbacks.
    /// </summary>
    public class HardwareMonitorService : IDisposable
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
            float maxClockMHz = 0f;

            if (_lhmInitialized && _computer != null)
            {
                try
                {
                    // 1. CPU Hardware Scanning
                    var cpuHardware = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
                    if (cpuHardware != null)
                    {
                        cpuHardware.Update();
                        CpuName = cpuHardware.Name;

                        // CPU Package Temperature
                        ISensor? packageSensor = Array.Find(cpuHardware.Sensors, s =>
                            s.SensorType == SensorType.Temperature &&
                            s.Name.IndexOf("Package", StringComparison.OrdinalIgnoreCase) >= 0);

                        if (packageSensor != null && packageSensor.Value.HasValue && packageSensor.Value.Value > 0)
                        {
                            CpuTemperature = (float)Math.Round(packageSensor.Value.Value);
                        }
                        else
                        {
                            ISensor? coreSensor = Array.Find(cpuHardware.Sensors, s =>
                                s.SensorType == SensorType.Temperature &&
                                (s.Name.IndexOf("Core Max", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 s.Name.IndexOf("Core Average", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 s.Name.IndexOf("CPU Core", StringComparison.OrdinalIgnoreCase) >= 0));

                            if (coreSensor != null && coreSensor.Value.HasValue && coreSensor.Value.Value > 0)
                            {
                                CpuTemperature = (float)Math.Round(coreSensor.Value.Value);
                            }
                        }

                        foreach (var sensor in cpuHardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Power)
                            {
                                if (sensor.Name.IndexOf("Package", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    sensor.Name.IndexOf("CPU Total", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    if (sensor.Value.HasValue && sensor.Value.Value > 0)
                                        CpuPowerW = (float)Math.Round(sensor.Value.Value, 1);
                                }
                            }
                            else if (sensor.SensorType == SensorType.Clock)
                            {
                                if (sensor.Name.IndexOf("Bus", StringComparison.OrdinalIgnoreCase) < 0 && sensor.Value.HasValue && sensor.Value.Value > maxClockMHz)
                                {
                                    maxClockMHz = sensor.Value.Value;
                                }
                            }
                            else if (sensor.SensorType == SensorType.Fan)
                            {
                                if (sensor.Value.HasValue && sensor.Value.Value > 0)
                                    HardwareCpuFanRpm = (int)Math.Round(sensor.Value.Value);
                            }
                        }
                    }

                    // 2. GPU Hardware Scanning (Prioritize Discrete Nvidia/AMD GPU over Intel iGPU)
                    var gpuHardwares = _computer.Hardware.Where(h =>
                        h.HardwareType == HardwareType.GpuNvidia ||
                        h.HardwareType == HardwareType.GpuAmd ||
                        h.HardwareType == HardwareType.GpuIntel).ToList();

                    var targetGpu = gpuHardwares.FirstOrDefault(h => h.HardwareType == HardwareType.GpuNvidia || h.HardwareType == HardwareType.GpuAmd)
                                    ?? gpuHardwares.FirstOrDefault();

                    if (targetGpu != null)
                    {
                        targetGpu.Update();
                        if (!string.IsNullOrEmpty(targetGpu.Name)) GpuName = targetGpu.Name;

                        // 1. GPU Core Temperature (Excluding GPU Hot Spot and Memory)
                        ISensor? gpuTempSensor = Array.Find(targetGpu.Sensors, s =>
                            s.SensorType == SensorType.Temperature &&
                            s.Name.IndexOf("Hot Spot", StringComparison.OrdinalIgnoreCase) < 0 &&
                            s.Name.IndexOf("Memory", StringComparison.OrdinalIgnoreCase) < 0);

                        if (gpuTempSensor != null && gpuTempSensor.Value.HasValue && gpuTempSensor.Value.Value > 0)
                        {
                            GpuTemperature = (float)Math.Round(gpuTempSensor.Value.Value);
                        }

                        // 2. GPU Core Usage % (Excluding Memory Controller, Video Engine, Bus)
                        ISensor? gpuUsageSensor = Array.Find(targetGpu.Sensors, s =>
                            s.SensorType == SensorType.Load &&
                            (s.Name.Equals("GPU Core", StringComparison.OrdinalIgnoreCase) ||
                             s.Name.Equals("Core", StringComparison.OrdinalIgnoreCase) ||
                             s.Name.Equals("GPU 3D", StringComparison.OrdinalIgnoreCase)));

                        if (gpuUsageSensor == null)
                        {
                            gpuUsageSensor = Array.Find(targetGpu.Sensors, s =>
                                s.SensorType == SensorType.Load &&
                                (s.Name.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 s.Name.IndexOf("GPU", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 s.Name.IndexOf("D3D", StringComparison.OrdinalIgnoreCase) >= 0) &&
                                s.Name.IndexOf("Memory", StringComparison.OrdinalIgnoreCase) < 0 &&
                                s.Name.IndexOf("Video", StringComparison.OrdinalIgnoreCase) < 0 &&
                                s.Name.IndexOf("Bus", StringComparison.OrdinalIgnoreCase) < 0 &&
                                s.Name.IndexOf("FB", StringComparison.OrdinalIgnoreCase) < 0);
                        }

                        if (gpuUsageSensor != null && gpuUsageSensor.Value.HasValue && gpuUsageSensor.Value.Value >= 0)
                        {
                            GpuUsage = (float)Math.Round(gpuUsageSensor.Value.Value);
                        }

                        foreach (var sensor in targetGpu.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Power)
                            {
                                if (sensor.Value.HasValue && sensor.Value.Value >= 0)
                                    GpuPowerW = (float)Math.Round(sensor.Value.Value, 1);
                            }
                            else if (sensor.SensorType == SensorType.Clock)
                            {
                                if (sensor.Name.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    if (sensor.Value.HasValue && sensor.Value.Value > 0)
                                        GpuClockMHz = (float)Math.Round(sensor.Value.Value);
                                }
                            }
                            else if (sensor.SensorType == SensorType.SmallData || sensor.SensorType == SensorType.Data)
                            {
                                if (sensor.Name.IndexOf("Memory Used", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    if (sensor.Value.HasValue && sensor.Value.Value > 0)
                                        GpuVramUsedGB = (float)Math.Round(sensor.Value.Value / 1024.0f, 1);
                                }
                            }
                            else if (sensor.SensorType == SensorType.Fan)
                            {
                                if (sensor.Value.HasValue && sensor.Value.Value > 0)
                                    HardwareGpuFanRpm = (int)Math.Round(sensor.Value.Value);
                            }
                        }
                    }

                    // 3. System RAM Telemetry
                    var memHardware = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Memory);
                    if (memHardware != null)
                    {
                        memHardware.Update();
                        foreach (var sensor in memHardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Load && sensor.Name.IndexOf("Memory", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                if (sensor.Value.HasValue) RamUsagePercent = (float)Math.Round(sensor.Value.Value);
                            }
                            else if (sensor.SensorType == SensorType.Data && sensor.Name.IndexOf("Used", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                if (sensor.Value.HasValue) RamUsedGB = (float)Math.Round(sensor.Value.Value, 1);
                            }
                        }
                    }

                    // 4. Motherboard Fan Controllers
                    foreach (var mobo in _computer.Hardware.Where(h => h.HardwareType == HardwareType.Motherboard))
                    {
                        mobo.Update();
                        foreach (var sensor in mobo.Sensors.Where(s => s.SensorType == SensorType.Fan))
                        {
                            string nameLower = sensor.Name.ToLower();
                            if (nameLower.Contains("cpu") || nameLower.Contains("fan #1") || nameLower.Contains("fan 1"))
                            {
                                if (sensor.Value.HasValue && sensor.Value.Value > 0)
                                    HardwareCpuFanRpm = (int)Math.Round(sensor.Value.Value);
                            }
                            else if (nameLower.Contains("gpu") || nameLower.Contains("fan #2") || nameLower.Contains("fan 2"))
                            {
                                if (sensor.Value.HasValue && sensor.Value.Value > 0)
                                    HardwareGpuFanRpm = (int)Math.Round(sensor.Value.Value);
                            }
                        }
                    }
                }
                catch { }
            }

            if (maxClockMHz > 0)
            {
                CpuMaxClockGHz = (float)Math.Round(maxClockMHz / 1000.0f, 2);
            }

            // Fallback 1: CPU Max Clock via Win32_Processor
            if (CpuMaxClockGHz == 0f)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\cimv2", "SELECT CurrentClockSpeed FROM Win32_Processor");
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        if (obj["CurrentClockSpeed"] != null)
                        {
                            double mhz = Convert.ToDouble(obj["CurrentClockSpeed"]);
                            if (mhz > 0) CpuMaxClockGHz = (float)Math.Round(mhz / 1000.0, 2);
                        }
                    }
                }
                catch { }
            }

            // Fallback 2: CPU Temp via ACPI Thermal Zone
            if (CpuTemperature == 0f)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
                    foreach (ManagementObject obj in searcher.Get())
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
                catch { }
            }

            // Fallback 3: ASUS ATK WMI Query for ROG/TUF Laptop Fans (DSTS 0x00110013 & 0x00110014)
            if (HardwareCpuFanRpm == 0 || HardwareGpuFanRpm == 0)
            {
                try
                {
                    ManagementObject? asusControl = null;
                    var scope = new ManagementScope(@"\root\wmi");
                    var query = new SelectQuery("SELECT * FROM AsusAtkWmi_WMNB");
                    using (var searcher = new ManagementObjectSearcher(scope, query))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            asusControl = obj;
                            break;
                        }
                    }

                    if (asusControl != null)
                    {
                        if (HardwareCpuFanRpm == 0)
                        {
                            var inParams = asusControl.GetMethodParameters("DSTS");
                            inParams["Device_id"] = 0x00110013u;
                            var outParams = asusControl.InvokeMethod("DSTS", inParams, null);
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

                        if (HardwareGpuFanRpm == 0)
                        {
                            var inParams = asusControl.GetMethodParameters("DSTS");
                            inParams["Device_id"] = 0x00110014u;
                            var outParams = asusControl.InvokeMethod("DSTS", inParams, null);
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

            // Always calculate accurate system CPU Usage % via Win32 GetSystemTimes API
            CpuUsage = CalculateCpuUsageWin32();
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

                _prevIdleTime = idleTime;
                _prevKernelTime = kernelTime;
                _prevUserTime = userTime;

                if (sys > 0)
                {
                    float usage = (float)(sys - idl) * 100.0f / sys;
                    return (float)Math.Round(Math.Clamp(usage, 0.0f, 100.0f));
                }
            }
            return CpuUsage;
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
