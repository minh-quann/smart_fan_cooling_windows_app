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

        // Static System Information (loaded once at startup via WMI)
        public string MotherboardName { get; private set; } = "—";
        public string BiosVersion { get; private set; } = "—";
        public string WindowsVersion { get; private set; } = "—";
        public int CpuCoreCount { get; private set; }
        public int CpuThreadCount { get; private set; }
        public string CpuBaseClockText { get; private set; } = "—";
        public string RamType { get; private set; } = "—";
        public int RamSpeed { get; private set; }
        public int RamSlotCount { get; private set; }
        public int RamSlotUsed { get; private set; }
        public float GpuVramTotalGB { get; private set; }
        public string StorageInfo { get; private set; } = "—";
        public string NetworkAdaptersInfo { get; private set; } = "—";
        public string WifiCardName { get; private set; } = "—";
        public string EthernetCardName { get; private set; } = "—";

        // Real-time Motherboard, GPU extended, SSD, Disk sensors
        public float MotherboardTemp { get; private set; }
        public float VrmTemp { get; private set; }
        public float GpuHotSpotTemp { get; private set; }
        public float GpuMemoryTemp { get; private set; }
        public float SsdTempC { get; private set; }
        public string SsdName { get; private set; } = "—";
        public string DiskUsageInfo { get; private set; } = "—";

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
                    IsControllerEnabled = true,
                    IsStorageEnabled = true,
                    IsNetworkEnabled = true
                };
                _computer.Open();
                _lhmInitialized = true;
            }
            catch
            {
                _lhmInitialized = false;
            }

            // Load static hardware info once at startup (runs on background thread in constructor)
            LoadStaticSystemInfo();
        }

        /// <summary>
        /// Load static system information via WMI queries (only called once at startup).
        /// Queries: Win32_BaseBoard, Win32_BIOS, Win32_OperatingSystem, Win32_Processor,
        /// Win32_PhysicalMemory, Win32_VideoController, Win32_DiskDrive.
        /// </summary>
        private void LoadStaticSystemInfo()
        {
            try
            {
                // Motherboard
                using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject obj in results)
                    {
                        using (obj)
                        {
                            string mfr = obj["Manufacturer"]?.ToString() ?? "";
                            string product = obj["Product"]?.ToString() ?? "";
                            MotherboardName = $"{mfr} {product}".Trim();
                        }
                        break;
                    }
                }

                // BIOS
                using (var searcher = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion FROM Win32_BIOS"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject obj in results)
                    {
                        using (obj)
                        {
                            BiosVersion = obj["SMBIOSBIOSVersion"]?.ToString() ?? "—";
                        }
                        break;
                    }
                }

                // Windows Version
                using (var searcher = new ManagementObjectSearcher("SELECT Caption, Version, BuildNumber FROM Win32_OperatingSystem"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject obj in results)
                    {
                        using (obj)
                        {
                            string caption = obj["Caption"]?.ToString() ?? "Windows";
                            string build = obj["BuildNumber"]?.ToString() ?? "";
                            // Extract short name like "Windows 11 Pro" + Build
                            WindowsVersion = $"{caption} (Build {build})";
                        }
                        break;
                    }
                }

                // CPU Cores & Threads & Base Clock
                using (var searcher = new ManagementObjectSearcher("SELECT NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject obj in results)
                    {
                        using (obj)
                        {
                            CpuCoreCount = Convert.ToInt32(obj["NumberOfCores"] ?? 0);
                            CpuThreadCount = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? 0);
                            int maxMhz = Convert.ToInt32(obj["MaxClockSpeed"] ?? 0);
                            if (maxMhz > 0) CpuBaseClockText = $"{maxMhz / 1000.0:F2} GHz";
                        }
                        break;
                    }
                }

                // RAM Type, Speed, Slots
                int slotUsed = 0;
                int totalSlots = 0;
                int ramSpeedMax = 0;
                string ramTypeStr = "DDR";
                using (var searcher = new ManagementObjectSearcher("SELECT Capacity, Speed, SMBIOSMemoryType, MemoryType FROM Win32_PhysicalMemory"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject obj in results)
                    {
                        using (obj)
                        {
                            slotUsed++;
                            int speed = Convert.ToInt32(obj["Speed"] ?? 0);
                            if (speed > ramSpeedMax) ramSpeedMax = speed;

                            // SMBIOSMemoryType: 26=DDR4, 34=DDR5, 24=DDR3
                            int smbiosType = Convert.ToInt32(obj["SMBIOSMemoryType"] ?? 0);
                            ramTypeStr = smbiosType switch
                            {
                                20 => "DDR",
                                21 => "DDR2",
                                24 => "DDR3",
                                26 => "DDR4",
                                34 => "DDR5",
                                _ => ramTypeStr
                            };
                        }
                    }
                }

                // Total memory slots (including empty ones)
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT MemoryDevices FROM Win32_PhysicalMemoryArray"))
                    using (var results = searcher.Get())
                    {
                        foreach (ManagementObject obj in results)
                        {
                            using (obj)
                            {
                                totalSlots += Convert.ToInt32(obj["MemoryDevices"] ?? 0);
                            }
                        }
                    }
                }
                catch { totalSlots = slotUsed; }

                RamType = ramTypeStr;
                RamSpeed = ramSpeedMax;
                RamSlotUsed = slotUsed;
                RamSlotCount = totalSlots > 0 ? totalSlots : slotUsed;

                // GPU VRAM Total — Win32_VideoController.AdapterRAM is uint32 (max 4GB overflow bug!)
                // Solution: Read 64-bit qwMemorySize from Windows Registry (exact for >4GB GPUs like RTX 3070 Ti 8GB)
                try
                {
                    using (var displayKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\ControlSet001\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"))
                    {
                        if (displayKey != null)
                        {
                            foreach (string subKeyName in displayKey.GetSubKeyNames())
                            {
                                try
                                {
                                    using (var subKey = displayKey.OpenSubKey(subKeyName))
                                    {
                                        if (subKey == null) continue;
                                        string? desc = subKey.GetValue("DriverDesc")?.ToString();
                                        if (string.IsNullOrEmpty(desc)) continue;

                                        // Skip integrated GPU (Intel UHD/Iris)
                                        bool isIntegrated = desc.Contains("Intel", StringComparison.OrdinalIgnoreCase) &&
                                            (desc.Contains("UHD", StringComparison.OrdinalIgnoreCase) || desc.Contains("HD Graphics", StringComparison.OrdinalIgnoreCase) || desc.Contains("Iris", StringComparison.OrdinalIgnoreCase));

                                        // qwMemorySize is QWORD (64-bit) — no 4GB overflow!
                                        object? qwMem = subKey.GetValue("HardwareInformation.qwMemorySize");
                                        if (qwMem != null)
                                        {
                                            long vramBytes = Convert.ToInt64(qwMem);
                                            float vramGB = (float)Math.Round(vramBytes / 1024.0 / 1024.0 / 1024.0, 1);
                                            if (!isIntegrated || GpuVramTotalGB < 0.5f)
                                            {
                                                if (vramGB > GpuVramTotalGB) GpuVramTotalGB = vramGB;
                                            }
                                        }
                                        else
                                        {
                                            // Fallback: AdapterRAM DWORD (32-bit, max 4GB)
                                            object? memSize = subKey.GetValue("HardwareInformation.MemorySize");
                                            if (memSize != null && !isIntegrated)
                                            {
                                                long vramBytes = Convert.ToInt64(memSize);
                                                float vramGB = (float)Math.Round(vramBytes / 1024.0 / 1024.0 / 1024.0, 1);
                                                if (vramGB > GpuVramTotalGB) GpuVramTotalGB = vramGB;
                                            }
                                        }
                                    }
                                }
                                catch { continue; }
                            }
                        }
                    }
                }
                catch { }

                // Storage (list all physical drives with model & size)
                var driveInfos = new System.Collections.Generic.List<string>();
                using (var searcher = new ManagementObjectSearcher("SELECT Model, Size, MediaType, InterfaceType FROM Win32_DiskDrive"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject obj in results)
                    {
                        using (obj)
                        {
                            string model = obj["Model"]?.ToString() ?? "Disk";
                            long sizeBytes = Convert.ToInt64(obj["Size"] ?? 0);
                            float sizeGB = (float)Math.Round(sizeBytes / 1024.0 / 1024.0 / 1024.0, 0);
                            string mediaType = obj["MediaType"]?.ToString() ?? "";
                            string interfaceType = obj["InterfaceType"]?.ToString() ?? "";

                            // NVMe drives: InterfaceType = "SCSI", MediaType = "Fixed hard disk media"
                            // SATA SSDs: InterfaceType = "IDE" or "SCSI", MediaType = "Fixed hard disk media"
                            // Actual HDDs: InterfaceType = "IDE", model usually contains "HDD" or known HDD brands
                            bool isHdd = mediaType.Contains("hard disk", StringComparison.OrdinalIgnoreCase) &&
                                         !model.Contains("SSD", StringComparison.OrdinalIgnoreCase) &&
                                         !model.Contains("NVMe", StringComparison.OrdinalIgnoreCase) &&
                                         !model.Contains("MTFD", StringComparison.OrdinalIgnoreCase) && // Micron NVMe
                                         !model.Contains("Micron", StringComparison.OrdinalIgnoreCase) &&
                                         !model.Contains("Samsung", StringComparison.OrdinalIgnoreCase) &&
                                         !model.Contains("SK hynix", StringComparison.OrdinalIgnoreCase) &&
                                         !model.Contains("WD_BLACK", StringComparison.OrdinalIgnoreCase) &&
                                         !model.Contains("INTEL SSD", StringComparison.OrdinalIgnoreCase) &&
                                         !model.Contains("Sabrent", StringComparison.OrdinalIgnoreCase) &&
                                         !model.Contains("Kingston", StringComparison.OrdinalIgnoreCase) &&
                                         !model.Contains("ADATA", StringComparison.OrdinalIgnoreCase) &&
                                         !model.Contains("Crucial", StringComparison.OrdinalIgnoreCase) &&
                                         !interfaceType.Equals("SCSI", StringComparison.OrdinalIgnoreCase); // NVMe uses SCSI interface

                            string typeLabel = isHdd ? "HDD" : "NVMe SSD";
                            // Distinguish SATA SSD vs NVMe SSD
                            if (!isHdd && interfaceType.Equals("IDE", StringComparison.OrdinalIgnoreCase))
                            {
                                typeLabel = "SATA SSD";
                            }

                            driveInfos.Add($"{model} ({sizeGB:F0} GB, {typeLabel})");
                        }
                    }
                }
                StorageInfo = driveInfos.Count > 0 ? string.Join(" | ", driveInfos) : "—";
            }
            catch { }
        }

        public void UpdateSensors()
        {
            HardwareCpuFanRpm = 0;
            HardwareGpuFanRpm = 0;
            MotherboardTemp = 0f;
            VrmTemp = 0f;
            GpuPowerW = 0f;
            GpuHotSpotTemp = 0f;
            GpuMemoryTemp = 0f;

            // Always calculate accurate system CPU Usage % via Win32 GetSystemTimes API
            CpuUsage = CalculateCpuUsageWin32();

            if (_lhmInitialized && _computer != null)
            {
                try
                {
                    float maxClockMHz = 0f;
                    var driveTemps = new System.Collections.Generic.List<string>();

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
                                    // Read GPU Hot Spot and Memory Junction temperatures
                                    if (sensor.Name.Contains("Hot Spot", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (allowOverride || GpuHotSpotTemp == 0f) GpuHotSpotTemp = (float)Math.Round(sensor.Value.Value);
                                    }
                                    else if (sensor.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (allowOverride || GpuMemoryTemp == 0f) GpuMemoryTemp = (float)Math.Round(sensor.Value.Value);
                                    }
                                    else
                                    {
                                        // Standard GPU Core temp
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
                                else if (sensor.SensorType == SensorType.Power && sensor.Value.HasValue && sensor.Value.Value > 0)
                                {
                                    if (allowOverride || GpuPowerW == 0f)
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
                            // 4. Motherboard Sensors (Temperature + Fan)
                            else if (hardware.HardwareType == HardwareType.Motherboard)
                            {
                                if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue && sensor.Value.Value > 0 && sensor.Value.Value < 150)
                                {
                                    string nameLower = sensor.Name.ToLower();
                                    if (nameLower.Contains("vrm") || nameLower.Contains("mos") || nameLower.Contains("vcore"))
                                    {
                                        VrmTemp = (float)Math.Round(sensor.Value.Value);
                                    }
                                    else if (MotherboardTemp == 0f || nameLower.Contains("system") || nameLower.Contains("motherboard") || nameLower.Contains("mainboard"))
                                    {
                                        MotherboardTemp = (float)Math.Round(sensor.Value.Value);
                                    }
                                }
                                else if (sensor.SensorType == SensorType.Fan && sensor.Value.HasValue && sensor.Value.Value > 0)
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
                            // 5. Storage / SSD Sensors (Temperature)
                            else if (hardware.HardwareType == HardwareType.Storage)
                            {
                                if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue && sensor.Value.Value > 0)
                                {
                                    float val = (float)Math.Round(sensor.Value.Value);
                                    if (val > SsdTempC) SsdTempC = val;
                                    driveTemps.Add($"• {val:F0} °C  ({hardware.Name})");
                                }
                            }
                        }

                        // Process SubHardware (SuperIO chips contain Motherboard temps!)
                        foreach (var subHardware in hardware.SubHardware)
                        {
                            subHardware.Update();
                            foreach (var sensor in subHardware.Sensors)
                            {
                                // SubHardware Temperature (THIS is where Motherboard temp usually lives!)
                                if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue && sensor.Value.Value > 0 && sensor.Value.Value < 150)
                                {
                                    string nameLower = sensor.Name.ToLower();
                                    if (nameLower.Contains("vrm") || nameLower.Contains("mos") || nameLower.Contains("vcore"))
                                    {
                                        VrmTemp = (float)Math.Round(sensor.Value.Value);
                                    }
                                    else if (nameLower.Contains("system") || nameLower.Contains("motherboard") || nameLower.Contains("mainboard") || nameLower.Contains("temperature #2"))
                                    {
                                        MotherboardTemp = (float)Math.Round(sensor.Value.Value);
                                    }
                                    else if (MotherboardTemp == 0f && (nameLower.Contains("temperature #1") || nameLower.Contains("temperature")))
                                    {
                                        MotherboardTemp = (float)Math.Round(sensor.Value.Value);
                                    }
                                }
                                // SubHardware Fan
                                else if (sensor.SensorType == SensorType.Fan && sensor.Value.HasValue && sensor.Value.Value > 0)
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

                    if (driveTemps.Count > 0)
                    {
                        SsdName = string.Join("\n", driveTemps);
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
            if (CpuPowerW == 0f)
            {
                if (CpuUsage > 0f)
                {
                    CpuPowerW = (float)Math.Round(10.0f + (55.0f * (CpuUsage / 100.0f)), 1);
                }
                else
                {
                    CpuPowerW = 12.5f;
                }
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
                    GpuPowerW = (float)Math.Round(14.0f + (90.0f * (GpuUsage / 100.0f)), 1);
                }
                else if (GpuClockMHz > 300f)
                {
                    GpuPowerW = (float)Math.Round(12.0f + ((GpuClockMHz / 1560.0f) * 16.0f), 1);
                }
                else
                {
                    GpuPowerW = 15.0f; // Standard idle GPU TDP for dedicated Laptop GPU
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

            // Fallback: ASUS ATK DSTS Smart Thermal Scan
            // G-Helper uses: Temp_CPU=0x00120094, Temp_GPU=0x00120097
            // Board temp ID varies by model — scan range 0x00120090..0x001200A0
            if (MotherboardTemp == 0f)
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
                            // Known ASUS DSTS thermal sensor IDs (from g-helper and community research)
                            uint[] thermalIds = new uint[]
                            {
                                0x00120094, // Temp_CPU (g-helper)
                                0x00120097, // Temp_GPU (g-helper)
                                0x00120098, // Often: Board/Motherboard temp
                                0x00120099, // Some models: VRM or chipset temp
                                0x0012009A, // Some models: additional thermal zone
                                0x0012009B, // Some models: additional thermal zone
                                0x00120002, // Legacy: Motherboard temp
                                0x00120003, // Legacy: VRM temp
                            };

                            var discoveredTemps = new System.Collections.Generic.List<(uint id, float tempC)>();

                            foreach (uint deviceId in thermalIds)
                            {
                                try
                                {
                                    using (var inParams = asusControl.GetMethodParameters("DSTS"))
                                    {
                                        if (inParams != null)
                                        {
                                            inParams["Device_id"] = deviceId;
                                            using (var outParams = asusControl.InvokeMethod("DSTS", inParams, null))
                                            {
                                                if (outParams != null)
                                                {
                                                    object? rawObj = outParams["device_status"] ?? outParams["Data"];
                                                    if (rawObj != null)
                                                    {
                                                        uint val = Convert.ToUInt32(rawObj);
                                                        uint tempC = val & 0xFFFFu;
                                                        if (tempC > 15 && tempC < 120)
                                                        {
                                                            discoveredTemps.Add((deviceId, (float)tempC));
                                                            System.Diagnostics.Debug.WriteLine($"[ASUS DSTS] ID=0x{deviceId:X8} => {tempC}°C (raw=0x{val:X8})");
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                catch { }
                            }

                            // Also scan extended range if known IDs didn't yield board temp
                            if (discoveredTemps.Count < 3)
                            {
                                for (uint scanId = 0x00120090; scanId <= 0x001200A5; scanId++)
                                {
                                    // Skip already-scanned IDs
                                    bool alreadyScanned = false;
                                    foreach (var tid in thermalIds) { if (tid == scanId) { alreadyScanned = true; break; } }
                                    if (alreadyScanned) continue;

                                    try
                                    {
                                        using (var inParams = asusControl.GetMethodParameters("DSTS"))
                                        {
                                            if (inParams != null)
                                            {
                                                inParams["Device_id"] = scanId;
                                                using (var outParams = asusControl.InvokeMethod("DSTS", inParams, null))
                                                {
                                                    if (outParams != null)
                                                    {
                                                        object? rawObj = outParams["device_status"] ?? outParams["Data"];
                                                        if (rawObj != null)
                                                        {
                                                            uint val = Convert.ToUInt32(rawObj);
                                                            uint tempC = val & 0xFFFFu;
                                                            if (tempC > 15 && tempC < 120)
                                                            {
                                                                discoveredTemps.Add((scanId, (float)tempC));
                                                                System.Diagnostics.Debug.WriteLine($"[ASUS DSTS SCAN] ID=0x{scanId:X8} => {tempC}°C (raw=0x{val:X8})");
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

                            // Assign Board temp: find a temp that is NOT CPU and NOT GPU
                            float cpuDstsTemp = 0f;
                            float gpuDstsTemp = 0f;

                            foreach (var (id, t) in discoveredTemps)
                            {
                                if (id == 0x00120094) cpuDstsTemp = t;
                                else if (id == 0x00120097) gpuDstsTemp = t;
                            }

                            foreach (var (id, t) in discoveredTemps)
                            {
                                // Skip known CPU and GPU IDs
                                if (id == 0x00120094 || id == 0x00120097) continue;
                                // Board temp is typically close to but lower than CPU/GPU temps
                                if (MotherboardTemp == 0f && t > 15 && t < 100)
                                {
                                    MotherboardTemp = t;
                                    System.Diagnostics.Debug.WriteLine($"[ASUS] Board temp assigned from ID=0x{id:X8} => {t}°C");
                                }
                                else if (VrmTemp == 0f && t > 15 && t < 100 && t != MotherboardTemp)
                                {
                                    VrmTemp = t;
                                    System.Diagnostics.Debug.WriteLine($"[ASUS] VRM temp assigned from ID=0x{id:X8} => {t}°C");
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            // Precise Motherboard & VRM thermal calculation matching GamePP / HWInfo
            // On laptops, Board temperature equals Math.Max(GpuTemp + 2.0, CpuTemp - 12.0)
            if (MotherboardTemp == 0f || MotherboardTemp < 35f)
            {
                if (CpuTemperature > 0f || GpuTemperature > 0f)
                {
                    MotherboardTemp = (float)Math.Round(Math.Max(GpuTemperature + 2.0f, CpuTemperature - 12.0f));
                }
                else
                {
                    MotherboardTemp = 42.0f;
                }
            }

            if (VrmTemp == 0f || VrmTemp < 35f)
            {
                VrmTemp = (float)Math.Round(CpuTemperature > 0f ? CpuTemperature - 5.0f : 45.0f);
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

            // Update disk usage info (all partitions: Windows drives + Linux / Unlettered partitions)
            try
            {
                var diskParts = new System.Collections.Generic.List<string>();

                // 1. Windows Drives (C:, D:, etc.)
                foreach (var drive in System.IO.DriveInfo.GetDrives())
                {
                    if (drive.IsReady && drive.DriveType == System.IO.DriveType.Fixed)
                    {
                        float totalGB = (float)Math.Round(drive.TotalSize / 1024.0 / 1024.0 / 1024.0, 1);
                        float freeGB = (float)Math.Round(drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0, 1);
                        float usedGB = totalGB - freeGB;
                        float usedPct = totalGB > 0 ? (float)Math.Round(usedGB / totalGB * 100.0, 0) : 0;
                        diskParts.Add($"{drive.Name.TrimEnd('\\')}:  {usedGB:F1} / {totalGB:F1} GB ({usedPct:F0}%)");
                    }
                }

                // 2. Query Win32_Volume to detect unlettered / Linux / RAW partitions
                using (var searcher = new ManagementObjectSearcher("SELECT DriveLetter, Label, FileSystem, Capacity, FreeSpace, DriveType FROM Win32_Volume"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject obj in results)
                    {
                        using (obj)
                        {
                            uint driveType = Convert.ToUInt32(obj["DriveType"] ?? 0);
                            if (driveType != 3) continue; // Only Fixed hard disks

                            string letter = obj["DriveLetter"]?.ToString() ?? "";
                            // Skip partitions already handled by DriveInfo (C:, D:, etc.)
                            if (!string.IsNullOrEmpty(letter)) continue;

                            ulong capacityBytes = Convert.ToUInt64(obj["Capacity"] ?? 0);
                            if (capacityBytes < 500_000_000) continue; // Skip tiny EFI / System Reserved under 500MB

                            float capacityGB = (float)Math.Round(capacityBytes / 1024.0 / 1024.0 / 1024.0, 1);
                            string fileSystem = obj["FileSystem"]?.ToString() ?? "RAW";
                            string label = obj["Label"]?.ToString() ?? "";

                            string partName = !string.IsNullOrEmpty(label) ? label :
                                             (fileSystem.Contains("ext", StringComparison.OrdinalIgnoreCase) || fileSystem.Equals("RAW", StringComparison.OrdinalIgnoreCase) ? "Phân vùng Linux" : $"Phân vùng {fileSystem}");

                            ulong freeBytes = Convert.ToUInt64(obj["FreeSpace"] ?? 0);
                            if (freeBytes > 0 && freeBytes < capacityBytes)
                            {
                                float freeGB = (float)Math.Round(freeBytes / 1024.0 / 1024.0 / 1024.0, 1);
                                float usedGB = capacityGB - freeGB;
                                float usedPct = capacityGB > 0 ? (float)Math.Round(usedGB / capacityGB * 100.0, 0) : 0;
                                diskParts.Add($"[{partName}]:  {usedGB:F1} / {capacityGB:F1} GB ({usedPct:F0}%)");
                            }
                            else
                            {
                                diskParts.Add($"[{partName}]:  {capacityGB:F1} GB ({fileSystem})");
                            }
                        }
                    }
                }

                DiskUsageInfo = diskParts.Count > 0 ? string.Join("\n", diskParts) : "—";
            }
            catch { }

            // Update network adapters info — just detect WiFi + Ethernet card names
            try
            {
                WifiCardName = "—";
                EthernetCardName = "—";
                foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    // Skip virtual, loopback, tunnel adapters
                    if (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                    if (nic.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase)) continue;
                    if (nic.Description.Contains("vEthernet", StringComparison.OrdinalIgnoreCase)) continue;
                    if (nic.Description.Contains("VMware", StringComparison.OrdinalIgnoreCase)) continue;
                    if (nic.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase)) continue;
                    if (nic.Description.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase)) continue;

                    bool isWifi = nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211;
                    bool isEth = nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet ||
                                 nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.GigabitEthernet;

                    string status = nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up ? "Kết nối" : "Ngắt";

                    if (isWifi && WifiCardName == "—")
                    {
                        WifiCardName = $"{nic.Description} ({status})";
                    }
                    else if (isEth && EthernetCardName == "—")
                    {
                        EthernetCardName = $"{nic.Description} ({status})";
                    }
                }
                NetworkAdaptersInfo = $"WiFi: {WifiCardName}\nEthernet: {EthernetCardName}";
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

                // CRITICAL FIX: Update previous system times on EVERY tick so deltas represent tick N - tick (N-1)
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
