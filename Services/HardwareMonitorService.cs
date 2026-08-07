using System;
using System.Management;
using LibreHardwareMonitor.Hardware;

namespace SmartFanCooling.Services
{
    /// <summary>
    /// Reads CPU/GPU temperatures, powers, clocks and System sensor telemetry using LibreHardwareMonitor and WMI fallbacks.
    /// </summary>
    public class HardwareMonitorService : IDisposable
    {
        private readonly Computer? _computer;
        private readonly bool _lhmInitialized;

        public float CpuTemperature { get; private set; }
        public float GpuTemperature { get; private set; }
        public float CpuPowerW { get; private set; }
        public float CpuMaxClockGHz { get; private set; }
        public int HardwareCpuFanRpm { get; private set; }
        public int HardwareGpuFanRpm { get; private set; }
        public string CpuName { get; private set; } = "CPU";

        public HardwareMonitorService()
        {
            try
            {
                _computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
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
                    foreach (var hardware in _computer.Hardware)
                    {
                        hardware.Update();

                        if (hardware.HardwareType == HardwareType.Cpu)
                        {
                            CpuName = hardware.Name;
                        }

                        foreach (var sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Temperature)
                            {
                                // Match CPU Package or Core Average
                                if (hardware.HardwareType == HardwareType.Cpu)
                                {
                                    if (sensor.Name.Contains("Package") || sensor.Name.Contains("Core Average") || sensor.Name.Contains("CPU Core"))
                                    {
                                        if (sensor.Value.HasValue && sensor.Value.Value > 0)
                                            CpuTemperature = (float)Math.Round(sensor.Value.Value, 1);
                                    }
                                }
                                // Match GPU Core
                                else if (hardware.HardwareType == HardwareType.GpuNvidia || 
                                         hardware.HardwareType == HardwareType.GpuAmd || 
                                         hardware.HardwareType == HardwareType.GpuIntel)
                                {
                                    if (sensor.Name.Contains("Core"))
                                    {
                                        if (sensor.Value.HasValue && sensor.Value.Value > 0)
                                            GpuTemperature = (float)Math.Round(sensor.Value.Value, 1);
                                    }
                                }
                            }
                            else if (sensor.SensorType == SensorType.Power && hardware.HardwareType == HardwareType.Cpu)
                            {
                                if (sensor.Name.Contains("Package") || sensor.Name.Contains("CPU Total"))
                                {
                                    if (sensor.Value.HasValue && sensor.Value.Value > 0)
                                        CpuPowerW = (float)Math.Round(sensor.Value.Value, 1);
                                }
                            }
                            else if (sensor.SensorType == SensorType.Clock && hardware.HardwareType == HardwareType.Cpu)
                            {
                                if (!sensor.Name.Contains("Bus") && sensor.Value.HasValue && sensor.Value.Value > maxClockMHz)
                                {
                                    maxClockMHz = sensor.Value.Value;
                                }
                            }
                            else if (sensor.SensorType == SensorType.Fan)
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

                        foreach (var subHardware in hardware.SubHardware)
                        {
                            subHardware.Update();
                            foreach (var sensor in subHardware.Sensors)
                            {
                                if (sensor.SensorType == SensorType.Fan)
                                {
                                    string nameLower = sensor.Name.ToLower();
                                    if (nameLower.Contains("cpu") || nameLower.Contains("fan #1"))
                                    {
                                        if (sensor.Value.HasValue && sensor.Value.Value > 0)
                                            HardwareCpuFanRpm = (int)Math.Round(sensor.Value.Value);
                                    }
                                    else if (nameLower.Contains("gpu") || nameLower.Contains("fan #2"))
                                    {
                                        if (sensor.Value.HasValue && sensor.Value.Value > 0)
                                            HardwareGpuFanRpm = (int)Math.Round(sensor.Value.Value);
                                    }
                                }
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

            // Fallback 3: ASUS ATK WMI Query for hardware fans (DSTS 0x00110013 & 0x00110014)
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
