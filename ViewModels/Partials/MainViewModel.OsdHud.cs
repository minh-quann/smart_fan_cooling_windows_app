using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartFanCooling.Services;

namespace SmartFanCooling.ViewModels
{
    /// <summary>
    /// Partial class for Native Floating OSD HUD Game Overlay settings and window management.
    /// </summary>
    public partial class MainViewModel
    {
        // HUD Overlay Settings
        [ObservableProperty] private bool _isOverlayEnabled = false;
        [ObservableProperty] private bool _isOverlayLocked = true;
        [ObservableProperty] private double _overlayBackgroundOpacity = 0.75;
        [ObservableProperty] private string _overlayStyle = "horizontal";
        [ObservableProperty] private string _overlayFontSizeScale = "2K";
        [ObservableProperty] private int _overlayFontSize = 16;
        [ObservableProperty] private int _osdRefreshIntervalMs = 500;
        [ObservableProperty] private string _overlayPositionPreset = "top_center";
        [ObservableProperty] private int _activeOverlayCategoryTab = 0;
        [ObservableProperty] private string _selectedDisplayMode = "always"; 

        // Individual HUD Metric Toggles
        [ObservableProperty] private bool _showFps = true;
        [ObservableProperty] private bool _showTime = false;
        [ObservableProperty] private bool _showCpuTemp = true;
        [ObservableProperty] private bool _showCpuUsage = true;
        [ObservableProperty] private bool _showCpuClock = true;
        [ObservableProperty] private bool _showCpuPower = true;
        [ObservableProperty] private bool _showHardwareCpuFanRpm = false;

        [ObservableProperty] private bool _showGpuTemp = true;
        [ObservableProperty] private bool _showGpuUsage = true;
        [ObservableProperty] private bool _showGpuClock = true;
        [ObservableProperty] private bool _showGpuPower = true;
        [ObservableProperty] private bool _showGpuVram = true;
        [ObservableProperty] private bool _showHardwareGpuFanRpm = false;

        [ObservableProperty] private bool _showSmartFanRpm = true;
        [ObservableProperty] private bool _showSmartFanPwm = true;
        [ObservableProperty] private bool _showRamUsage = true;

        [ObservableProperty] private string _cpuClockUnit = "GHz";
        [ObservableProperty] private string _gpuClockUnit = "MHz";

        private NativeOsdOverlay? _osdWindow;

        public string OverlayBackgroundOpacityPercentText => $"{Math.Round(OverlayBackgroundOpacity * 100):F0}%";
        public string OverlayFontSizePxText => $"{OverlayFontSize} px";
        public string OsdRefreshIntervalPxText => $"{OsdRefreshIntervalMs} ms";
        public string OverlayLockStatusText => IsOverlayLocked
            ? "🔒 HUD ĐANG KHÓA (XUYÊN CHUỘT) - BẤM VÀO ĐÂY ĐỂ MỞ KHÓA KÉO DI CHUYỂN HUD (Phím Ctrl + Shift + O)"
            : "🔓 HUD ĐANG MỞ KHÓA - NHẤP GIỮ CHUỘT TRÁI ĐỂ KÉO RÊ ĐẾN VỊ TRÍ BẤT KỲ (Bấm vào đây để khóa lại)";

        partial void OnIsOverlayLockedChanged(bool value)
        {
            OnPropertyChanged(nameof(OverlayLockStatusText));
            if (_osdWindow != null)
            {
                _osdWindow.SetClickThrough(value);
            }
        }

        public string PresetTopLeftText => OverlayPositionPreset == "top_left" ? "✓ Góc Trên Trái" : "Góc Trên Trái";
        public string PresetTopCenterText => OverlayPositionPreset == "top_center" ? "✓ Giữa Trên" : "Giữa Trên";
        public string PresetTopRightText => OverlayPositionPreset == "top_right" ? "✓ Góc Trên Phải" : "Góc Trên Phải";
        public string PresetBottomLeftText => OverlayPositionPreset == "bottom_left" ? "✓ Góc Dưới Trái" : "Góc Dưới Trái";
        public string PresetBottomCenterText => OverlayPositionPreset == "bottom_center" ? "✓ Giữa Dưới" : "Giữa Dưới";
        public string PresetBottomRightText => OverlayPositionPreset == "bottom_right" ? "✓ Góc Dưới Phải" : "Góc Dưới Phải";

        public string SubTabBasicText => ActiveOverlayCategoryTab == 0 ? "✓ Basic" : "Basic";
        public string SubTabCpuText => ActiveOverlayCategoryTab == 1 ? "✓ CPU" : "CPU";
        public string SubTabGpuText => ActiveOverlayCategoryTab == 2 ? "✓ GPU" : "GPU";
        public string SubTabMemoryText => ActiveOverlayCategoryTab == 3 ? "✓ Memory" : "Memory";
        public string SubTabFanText => ActiveOverlayCategoryTab == 4 ? "✓ Smart Fan" : "Smart Fan";

        partial void OnActiveOverlayCategoryTabChanged(int value)
        {
            OnPropertyChanged(nameof(SubTabBasicText));
            OnPropertyChanged(nameof(SubTabCpuText));
            OnPropertyChanged(nameof(SubTabGpuText));
            OnPropertyChanged(nameof(SubTabMemoryText));
            OnPropertyChanged(nameof(SubTabFanText));
        }

        partial void OnOverlayPositionPresetChanged(string value)
        {
            OnPropertyChanged(nameof(PresetTopLeftText));
            OnPropertyChanged(nameof(PresetTopCenterText));
            OnPropertyChanged(nameof(PresetTopRightText));
            OnPropertyChanged(nameof(PresetBottomLeftText));
            OnPropertyChanged(nameof(PresetBottomCenterText));
            OnPropertyChanged(nameof(PresetBottomRightText));
            UpdateOsdOverlayNow();
        }

        partial void OnOverlayBackgroundOpacityChanged(double value)
        {
            OnPropertyChanged(nameof(OverlayBackgroundOpacityPercentText));
            UpdateOsdOverlayNow();
        }

        partial void OnOverlayFontSizeChanged(int value)
        {
            OnPropertyChanged(nameof(OverlayFontSizePxText));
            UpdateOsdOverlayNow();
        }

        partial void OnOsdRefreshIntervalMsChanged(int value)
        {
            OnPropertyChanged(nameof(OsdRefreshIntervalPxText));
            UpdateOsdOverlayNow();
        }

        partial void OnOverlayFontSizeScaleChanged(string value) => UpdateOsdOverlayNow();
        partial void OnSelectedDisplayModeChanged(string value) => UpdateOsdOverlayNow();
        partial void OnShowFpsChanged(bool value) => UpdateOsdOverlayNow();
        partial void OnShowTimeChanged(bool value) => UpdateOsdOverlayNow();
        partial void OnShowCpuTempChanged(bool value) => UpdateOsdOverlayNow();
        partial void OnShowCpuUsageChanged(bool value) => UpdateOsdOverlayNow();
        partial void OnShowCpuClockChanged(bool value) => UpdateOsdOverlayNow();
        partial void OnShowCpuPowerChanged(bool value) => UpdateOsdOverlayNow();
        partial void OnShowHardwareCpuFanRpmChanged(bool value) => UpdateOsdOverlayNow();
        partial void OnShowGpuTempChanged(bool value) => UpdateOsdOverlayNow();
        partial void OnShowGpuUsageChanged(bool value) => UpdateOsdOverlayNow();
        partial void OnShowGpuClockChanged(bool value) => UpdateOsdOverlayNow();
        partial void OnShowGpuPowerChanged(bool value) => UpdateOsdOverlayNow();
        partial void OnShowGpuVramChanged(bool value) => UpdateOsdOverlayNow();
        partial void OnShowHardwareGpuFanRpmChanged(bool value) => UpdateOsdOverlayNow();
        partial void OnShowSmartFanRpmChanged(bool value) => UpdateOsdOverlayNow();
        partial void OnShowSmartFanPwmChanged(bool value) => UpdateOsdOverlayNow();
        partial void OnShowRamUsageChanged(bool value) => UpdateOsdOverlayNow();
        partial void OnCpuClockUnitChanged(string value) => UpdateOsdOverlayNow();
        partial void OnGpuClockUnitChanged(string value) => UpdateOsdOverlayNow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private bool IsGameOr3DAppActive()
        {
            if (GpuUsage >= 6.0f) return true;

            IntPtr fgHwnd = GetForegroundWindow();
            if (fgHwnd == IntPtr.Zero) return false;

            GetWindowThreadProcessId(fgHwnd, out uint pid);
            if (pid == 0) return false;

            try
            {
                using var proc = Process.GetProcessById((int)pid);
                string name = proc.ProcessName.ToLower();
                if (name == "explorer" || name == "searchhost" || name == "shellexperiencehost" ||
                    name == "smart_fan_cooling_windows_app" || name == "cmd" || name == "powershell")
                {
                    return false;
                }

                foreach (ProcessModule mod in proc.Modules)
                {
                    string mName = mod.ModuleName.ToLower();
                    if (mName == "d3d11.dll" || mName == "d3d12.dll" || mName == "vulkan-1.dll" || mName == "opengl32.dll" || mName == "dxgi.dll")
                    {
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        private void UpdateOsdOverlayNow()
        {
            if (IsOverlayEnabled)
            {
                bool shouldShow = true;
                if (SelectedDisplayMode == "game_only")
                {
                    shouldShow = IsGameOr3DAppActive();
                }

                if (!shouldShow)
                {
                    if (_osdWindow != null)
                    {
                        _osdWindow.HideWindow();
                    }
                    return;
                }

                if (_osdWindow == null)
                {
                    _osdWindow = new NativeOsdOverlay();
                    _osdWindow.SetPresetPosition(OverlayPositionPreset);
                    _osdWindow.SetClickThrough(IsOverlayLocked);
                }

                _osdWindow.ShowWindow();
                _osdWindow.UpdateTelemetry(
                    ShowFps, IsGameOr3DAppActive(),
                    ShowTime, DateTime.Now.ToString("HH:mm:ss"),
                    ShowCpuTemp || ShowCpuUsage || ShowCpuClock || ShowCpuPower || ShowHardwareCpuFanRpm, CpuUsage, CpuTemp, CpuPowerW, CpuMaxClockGHz, ShowCpuClock, CpuClockUnit, CpuFanRpm, ShowHardwareCpuFanRpm,
                    ShowGpuTemp || ShowGpuUsage || ShowGpuClock || ShowGpuPower || ShowGpuVram || ShowHardwareGpuFanRpm, GpuUsage, GpuTemp, GpuPowerW, GpuClockMHz, ShowGpuClock, GpuClockUnit, GpuVramUsedGB, ShowGpuVram, GpuFanRpm, ShowHardwareGpuFanRpm,
                    ShowSmartFanRpm || ShowSmartFanPwm, FanPwm, FanRpm,
                    ShowRamUsage, RamUsagePercent,
                    OverlayBackgroundOpacity, OverlayFontSize
                );
            }
            else if (_osdWindow != null)
            {
                _osdWindow.Dispose();
                _osdWindow = null;
            }
        }

        [RelayCommand]
        public void SelectOverlayCategoryTab(object? parameter)
        {
            if (parameter != null && int.TryParse(parameter.ToString(), out int tabIndex))
            {
                ActiveOverlayCategoryTab = tabIndex;
            }
        }

        [RelayCommand]
        public void ToggleOverlayLock()
        {
            IsOverlayLocked = !IsOverlayLocked;
            _osdWindow?.SetClickThrough(IsOverlayLocked);
            StatusMessage = IsOverlayLocked ? "Đã khóa vị trí HUD Overlay (Click-through)." : "Đã mở khóa di chuyển vị trí HUD Overlay.";
        }

        [RelayCommand]
        public void SetOverlayPositionPreset(string presetKey)
        {
            OverlayPositionPreset = presetKey;
            _osdWindow?.SetPresetPosition(presetKey);
            StatusMessage = $"Đã chuyển vị trí HUD Overlay: {presetKey}";
        }

        [RelayCommand]
        public void ResetOverlayConfig()
        {
            IsOverlayEnabled = true;
            IsOverlayLocked = true;
            OverlayBackgroundOpacity = 0.75;
            OverlayStyle = "horizontal";
            OverlayFontSizeScale = "2K";
            OverlayFontSize = 16;
            OsdRefreshIntervalMs = 500;
            OverlayPositionPreset = "top_center";
            ShowTime = true;
            ShowCpuTemp = true;
            ShowCpuUsage = true;
            ShowCpuPower = true;
            ShowGpuTemp = true;
            ShowGpuUsage = true;
            ShowGpuPower = true;
            ShowSmartFanRpm = true;
            ShowSmartFanPwm = true;
            ShowRamUsage = true;
            _osdWindow?.SetPresetPosition("top_center");
            _osdWindow?.SetClickThrough(true);
            StatusMessage = "Đã khôi phục mặc định cấu hình HUD Overlay.";
        }
    }
}
