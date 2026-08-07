using System;
using LibreHardwareMonitor.Hardware;

namespace SmartFanCooling.Services
{
    /// <summary>
    /// Reads CPU/GPU temperatures, powers, clocks and System sensor telemetry using LibreHardwareMonitor.
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
            if (!_lhmInitialized || _computer == null) return;

            float maxClockMHz = 0f;

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
                                CpuTemperature = sensor.Value ?? CpuTemperature;
                            }
                        }
                        // Match GPU Core
                        else if (hardware.HardwareType == HardwareType.GpuNvidia || 
                                 hardware.HardwareType == HardwareType.GpuAmd || 
                                 hardware.HardwareType == HardwareType.GpuIntel)
                        {
                            if (sensor.Name.Contains("Core"))
                            {
                                GpuTemperature = sensor.Value ?? GpuTemperature;
                            }
                        }
                    }
                    else if (sensor.SensorType == SensorType.Power && hardware.HardwareType == HardwareType.Cpu)
                    {
                        if (sensor.Name.Contains("Package") || sensor.Name.Contains("CPU Total"))
                        {
                            CpuPowerW = sensor.Value ?? CpuPowerW;
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

            if (maxClockMHz > 0)
            {
                CpuMaxClockGHz = (float)Math.Round(maxClockMHz / 1000.0f, 2);
            }
        }

        public void Dispose()
        {
            if (_lhmInitialized && _computer != null)
            {
                _computer.Close();
            }
        }
    }
}
