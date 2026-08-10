using System;

namespace SmartFanCooling.Services.Interfaces
{
    /// <summary>
    /// Contract for Native Floating Win32 OSD Game Overlay.
    /// </summary>
    public interface INativeOsdOverlay : IDisposable
    {
        void SetPresetPosition(string preset);
        void SetClickThrough(bool clickThrough);
        void ShowWindow();
        void HideWindow();
        void UpdateTelemetry(
            bool showFps, bool isGameActive,
            bool showTime, string timeStr,
            bool showCpu, float cpuUsage, float cpuTemp, float cpuPower, float cpuClock, bool showCpuClock, string cpuClockUnit, int cpuFanRpm, bool showHardwareCpuFanRpm,
            bool showGpu, float gpuUsage, float gpuTemp, float gpuPower, float gpuClock, bool showGpuClock, string gpuClockUnit, float gpuVram, bool showGpuVram, int gpuFanRpm, bool showHardwareGpuFanRpm,
            bool showSmartFan, int fanPwm, int fanRpm,
            bool showRam, float ramUsagePercent,
            double transparency = 0.75, string fontSizeScale = "2K"
        );
    }
}
