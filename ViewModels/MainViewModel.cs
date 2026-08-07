using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartFanCooling.Models;
using SmartFanCooling.Services;
using Microsoft.UI.Xaml;

namespace SmartFanCooling.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly HardwareMonitorService _hardwareService;
        private readonly SerialFanService _serialService;
        private readonly DispatcherTimer _timer;

        // Selected Navigation Tab Index (0: Overview, 1: Fan Curve, 2: RGB, 3: App Profiles, 4: Hardware, 5: GPIO & Mouse Test, 6: HUD Overlay, 7: Settings)
        [ObservableProperty]
        private int _selectedTabIndex = 0;

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

        // Llano Smart Fan Speed & PWM
        [ObservableProperty] private int _fanPwm = 50;
        [ObservableProperty] private int _fanRpm = 0;
        [ObservableProperty] private bool _isFanStateOn = true;

        // Connection State
        [ObservableProperty] private string _selectedComPort = "";
        [ObservableProperty] private bool _isConnected = false;
        [ObservableProperty] private string _connectionStatusText = "OFFLINE";
        [ObservableProperty] private string _statusMessage = "Hệ thống sẵn sàng. Vui lòng chọn cổng COM để kết nối ESP32-S3.";

        public ObservableCollection<string> AvailableComPorts { get; } = new();

        // Profiles
        public ObservableCollection<FanProfile> Profiles { get; } = new();

        [ObservableProperty]
        private FanProfile _activeProfile = null!;

        // Fan Curve Points (°C -> PWM %)
        [ObservableProperty] private int _curveP30 = 20;
        [ObservableProperty] private int _curveP40 = 30;
        [ObservableProperty] private int _curveP50 = 45;
        [ObservableProperty] private int _curveP60 = 60;
        [ObservableProperty] private int _curveP70 = 75;
        [ObservableProperty] private int _curveP80 = 90;
        [ObservableProperty] private int _curveP90 = 100;

        public string FanCurveLinePoints => $"{30},{190 - (CurveP30 * 1.8)} {100},{190 - (CurveP40 * 1.8)} {170},{190 - (CurveP50 * 1.8)} {240},{190 - (CurveP60 * 1.8)} {310},{190 - (CurveP70 * 1.8)} {380},{190 - (CurveP80 * 1.8)} {450},{190 - (CurveP90 * 1.8)}";
        public string FanCurveFillPoints => $"30,190 {FanCurveLinePoints} 450,190";

        public double Node30_Y => 190 - (CurveP30 * 1.8) - 5;
        public double Node40_Y => 190 - (CurveP40 * 1.8) - 5;
        public double Node50_Y => 190 - (CurveP50 * 1.8) - 5;
        public double Node60_Y => 190 - (CurveP60 * 1.8) - 5;
        public double Node70_Y => 190 - (CurveP70 * 1.8) - 5;
        public double Node80_Y => 190 - (CurveP80 * 1.8) - 5;
        public double Node90_Y => 190 - (CurveP90 * 1.8) - 5;

        private void NotifyFanCurveChanged()
        {
            if (ActiveProfile != null && ActiveProfile.CurvePoints != null)
            {
                ActiveProfile.CurvePoints[30] = CurveP30;
                ActiveProfile.CurvePoints[40] = CurveP40;
                ActiveProfile.CurvePoints[50] = CurveP50;
                ActiveProfile.CurvePoints[60] = CurveP60;
                ActiveProfile.CurvePoints[70] = CurveP70;
                ActiveProfile.CurvePoints[80] = CurveP80;
                ActiveProfile.CurvePoints[90] = CurveP90;
            }

            OnPropertyChanged(nameof(FanCurveLinePoints));
            OnPropertyChanged(nameof(FanCurveFillPoints));
            OnPropertyChanged(nameof(Node30_Y));
            OnPropertyChanged(nameof(Node40_Y));
            OnPropertyChanged(nameof(Node50_Y));
            OnPropertyChanged(nameof(Node60_Y));
            OnPropertyChanged(nameof(Node70_Y));
            OnPropertyChanged(nameof(Node80_Y));
            OnPropertyChanged(nameof(Node90_Y));
        }

        partial void OnCurveP30Changed(int value) => NotifyFanCurveChanged();
        partial void OnCurveP40Changed(int value) => NotifyFanCurveChanged();
        partial void OnCurveP50Changed(int value) => NotifyFanCurveChanged();
        partial void OnCurveP60Changed(int value) => NotifyFanCurveChanged();
        partial void OnCurveP70Changed(int value) => NotifyFanCurveChanged();
        partial void OnCurveP80Changed(int value) => NotifyFanCurveChanged();
        partial void OnCurveP90Changed(int value) => NotifyFanCurveChanged();

        [ObservableProperty] private bool _isAutoMode = false;
        [ObservableProperty] private string _selectedFanCurve = "Balanced";

        // RGB Lighting
        [ObservableProperty] private int _selectedLedMode = 1; // 0: Off, 1: Static, 2: Breathing, 3: Rainbow, 4: Speed Pulse
        [ObservableProperty] private string _selectedRgbColorHex = "#00BCD4";
        [ObservableProperty] private int _rgbBrightness = 100;
        [ObservableProperty] private int _rgbSpeed = 50;

        // App Mappings
        public ObservableCollection<AppMapping> AppMappings { get; } = new();
        [ObservableProperty] private bool _isAutoAppSwitchEnabled = true;

        // Mouse & GPIO Test States
        [ObservableProperty] private int _mouseClickCountLeft = 0;
        [ObservableProperty] private int _mouseClickCountRight = 0;
        [ObservableProperty] private int _mouseClickCountMiddle = 0;
        [ObservableProperty] private int _mouseX = 0;
        [ObservableProperty] private int _mouseY = 0;
        [ObservableProperty] private bool _encoderAState = true;
        [ObservableProperty] private bool _encoderBState = false;
        [ObservableProperty] private int _encoder2AdcMv = 1650;
        [ObservableProperty] private string _gpioStatusLog = "GPIO & Encoder Test initialized.";

        // HUD Overlay Settings
        [ObservableProperty] private bool _isOverlayEnabled = false;
        [ObservableProperty] private bool _isOverlayLocked = true;
        [ObservableProperty] private double _overlayBackgroundOpacity = 0.75;
        [ObservableProperty] private string _overlayStyle = "horizontal";
        [ObservableProperty] private string _overlayFontSizeScale = "2K";
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

        // System Settings
        [ObservableProperty] private bool _startWithWindows = false;
        [ObservableProperty] private bool _minimizeToTray = true;
        [ObservableProperty] private int _refreshIntervalMs = 1000;
        [ObservableProperty] private string _selectedBaudRate = "115200";

        [ObservableProperty] private bool _isAutoConnectEnabled = true;
        [ObservableProperty] private string _activeConnectionType = "DISCONNECTED";

        public MainViewModel()
        {
            _hardwareService = new HardwareMonitorService();
            _serialService = new SerialFanService();
            _serialService.OnRpmReceived += rpm => FanRpm = rpm;
            _serialService.OnLogReceived += msg => StatusMessage = msg;

            InitializeDefaultProfiles();
            RefreshComPorts();
            CheckAndAutoConnectDevices();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void CheckAndAutoConnectDevices()
        {
            if (!IsAutoConnectEnabled) return;

            var ports = _serialService.GetAvailablePorts();
            bool isUsbCablePluggedIn = ports.Length > 0;

            // 1. USB CABLE HIGHEST PRIORITY (Ưu tiên cắm dây USB Serial)
            if (isUsbCablePluggedIn)
            {
                string targetPort = ports[0];
                if (AvailableComPorts.Count == 0 || !AvailableComPorts.Contains(targetPort))
                {
                    AvailableComPorts.Clear();
                    foreach (var p in ports) AvailableComPorts.Add(p);
                }

                if (!IsConnected || ActiveConnectionType != "USB_SERIAL" || SelectedComPort != targetPort)
                {
                    if (IsConnected && ActiveConnectionType != "USB_SERIAL")
                    {
                        _serialService.Disconnect();
                    }

                    SelectedComPort = targetPort;
                    int baud = int.TryParse(SelectedBaudRate, out int b) ? b : 115200;
                    bool connected = _serialService.Connect(targetPort, baud);
                    if (connected)
                    {
                        IsConnected = true;
                        ActiveConnectionType = "USB_SERIAL";
                        ConnectionStatusText = $"ONLINE (Cáp USB - {targetPort})";
                        StatusMessage = $"⚡ [Ưu Tiên Cáp USB] Đã tự động kết nối ESP32-S3 qua dây cáp USB ({targetPort}) [Ưu tiên hàng đầu].";
                    }
                }
            }
            else
            {
                // 2. USB Cable Unplugged: Safely reset state
                if (ActiveConnectionType == "USB_SERIAL" && IsConnected)
                {
                    _serialService.Disconnect();
                    IsConnected = false;
                    ActiveConnectionType = "DISCONNECTED";
                    ConnectionStatusText = "OFFLINE";
                    StatusMessage = "⚠️ Đã rút dây cáp USB Serial. Đang quét kết nối BLE / Wi-Fi...";
                }

                if (AvailableComPorts.Count > 0)
                {
                    AvailableComPorts.Clear();
                }
            }
        }

        private void InitializeDefaultProfiles()
        {
            Profiles.Add(new FanProfile
            {
                Name = "Quiet",
                Description = "Chế độ yên tĩnh cho công việc văn phòng",
                ColorHex = "#4CAF50",
                IconGlyph = "\uE706",
                MaxFanPwm = 50,
                CurvePoints = new Dictionary<int, int> { { 30, 15 }, { 40, 25 }, { 50, 35 }, { 60, 45 }, { 70, 60 }, { 80, 75 }, { 90, 85 } }
            });

            Profiles.Add(new FanProfile
            {
                Name = "Balanced",
                Description = "Chế độ cân bằng giữa độ ồn và hiệu năng tản nhiệt",
                ColorHex = "#00BCD4",
                IconGlyph = "\uE9CA",
                MaxFanPwm = 75,
                CurvePoints = new Dictionary<int, int> { { 30, 20 }, { 40, 30 }, { 50, 45 }, { 60, 60 }, { 70, 75 }, { 80, 90 }, { 90, 100 } }
            });

            Profiles.Add(new FanProfile
            {
                Name = "Turbo",
                Description = "Chế độ tối đa công suất quạt khi chơi game nặng / Render",
                ColorHex = "#FF5722",
                IconGlyph = "\uEBA3",
                MaxFanPwm = 100,
                CurvePoints = new Dictionary<int, int> { { 30, 40 }, { 40, 60 }, { 50, 75 }, { 60, 85 }, { 70, 95 }, { 80, 100 }, { 90, 100 } }
            });

            ActiveProfile = Profiles[1]; // Balanced
            LoadCurveFromProfile(ActiveProfile);
        }

        private void LoadCurveFromProfile(FanProfile profile)
        {
            if (profile.CurvePoints.TryGetValue(30, out var p30)) CurveP30 = p30;
            if (profile.CurvePoints.TryGetValue(40, out var p40)) CurveP40 = p40;
            if (profile.CurvePoints.TryGetValue(50, out var p50)) CurveP50 = p50;
            if (profile.CurvePoints.TryGetValue(60, out var p60)) CurveP60 = p60;
            if (profile.CurvePoints.TryGetValue(70, out var p70)) CurveP70 = p70;
            if (profile.CurvePoints.TryGetValue(80, out var p80)) CurveP80 = p80;
            if (profile.CurvePoints.TryGetValue(90, out var p90)) CurveP90 = p90;
        }

        private void Timer_Tick(object? sender, object e)
        {
            _hardwareService.UpdateSensors();

            // CPU Telemetry
            CpuTemp = _hardwareService.CpuTemperature;
            CpuUsage = _hardwareService.CpuUsage;
            CpuPowerW = _hardwareService.CpuPowerW;
            CpuMaxClockGHz = _hardwareService.CpuMaxClockGHz;
            if (!string.IsNullOrEmpty(_hardwareService.CpuName) && _hardwareService.CpuName != "CPU")
            {
                CpuName = _hardwareService.CpuName;
            }

            // GPU Telemetry
            GpuTemp = _hardwareService.GpuTemperature;
            GpuUsage = _hardwareService.GpuUsage;
            GpuPowerW = _hardwareService.GpuPowerW;
            GpuClockMHz = _hardwareService.GpuClockMHz;
            GpuVramUsedGB = _hardwareService.GpuVramUsedGB;
            if (!string.IsNullOrEmpty(_hardwareService.GpuName) && _hardwareService.GpuName != "GPU")
            {
                GpuName = _hardwareService.GpuName;
            }

            // System RAM Telemetry
            RamUsagePercent = _hardwareService.RamUsagePercent;
            RamUsedGB = _hardwareService.RamUsedGB;
            if (_hardwareService.RamTotalGB > 0) RamTotalGB = _hardwareService.RamTotalGB;
            RamStatusText = $"Bộ nhớ đã dùng: {RamUsedGB:F1} GB / {RamTotalGB:F1} GB";

            // Laptop Fans
            CpuFanRpm = _hardwareService.HardwareCpuFanRpm;
            GpuFanRpm = _hardwareService.HardwareGpuFanRpm;

            float maxTemp = Math.Max(CpuTemp, GpuTemp);

            if (IsAutoMode)
            {
                FanPwm = CalculatePwmFromCurve(maxTemp);
            }

            CheckAndAutoConnectDevices();

            // When offline, Llano Hub RPM is strictly 0!
            if (!IsConnected)
            {
                FanRpm = 0;
            }

            if (IsConnected)
            {
                _serialService.SendControl(FanPwm, SelectedLedMode, maxTemp);
            }

            // Update Native Floating OSD Overlay Window
            if (IsOverlayEnabled)
            {
                if (_osdWindow == null)
                {
                    _osdWindow = new NativeOsdOverlay();
                    _osdWindow.SetPresetPosition(OverlayPositionPreset);
                    _osdWindow.SetClickThrough(IsOverlayLocked);
                }
                UpdateOsdOverlayNow();
            }
            else if (_osdWindow != null)
            {
                _osdWindow.Dispose();
                _osdWindow = null;
            }
        }

        public string OverlayBackgroundOpacityPercentText => $"{Math.Round(OverlayBackgroundOpacity * 100):F0}%";
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
        public string SubTabFanText => ActiveOverlayCategoryTab == 4 ? "✓ Llano Fan" : "Llano Fan";

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
                    OverlayBackgroundOpacity, OverlayFontSizeScale
                );
            }
            else if (_osdWindow != null)
            {
                _osdWindow.Dispose();
                _osdWindow = null;
            }
        }

        private int CalculatePwmFromCurve(float temp)
        {
            if (temp <= 30) return CurveP30;
            if (temp <= 40) return CurveP30 + (int)((temp - 30) / 10.0f * (CurveP40 - CurveP30));
            if (temp <= 50) return CurveP40 + (int)((temp - 40) / 10.0f * (CurveP50 - CurveP40));
            if (temp <= 60) return CurveP50 + (int)((temp - 50) / 10.0f * (CurveP60 - CurveP50));
            if (temp <= 70) return CurveP60 + (int)((temp - 60) / 10.0f * (CurveP70 - CurveP60));
            if (temp <= 80) return CurveP70 + (int)((temp - 70) / 10.0f * (CurveP80 - CurveP70));
            if (temp <= 90) return CurveP80 + (int)((temp - 80) / 10.0f * (CurveP90 - CurveP80));
            return CurveP90;
        }

        [RelayCommand]
        public void SelectProfile(object? parameter)
        {
            if (parameter is FanProfile profile)
            {
                ActiveProfile = profile;
            }
            else if (parameter is string name)
            {
                var found = Profiles.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (found != null) ActiveProfile = found;
            }
            if (ActiveProfile != null)
            {
                SelectedFanCurve = ActiveProfile.Name;
                LoadCurveFromProfile(ActiveProfile);
                StatusMessage = $"Đã kích hoạt Profile: {ActiveProfile.Name}";
            }
        }

        [RelayCommand]
        public void AddNewProfile()
        {
            int nextNum = Profiles.Count + 1;
            var newProfile = new FanProfile
            {
                Name = $"Custom {nextNum}",
                Description = "Đường cong tùy chỉnh cá nhân",
                ColorHex = "#9C27B0",
                IconGlyph = "\uE9CA",
                MaxFanPwm = 100,
                CurvePoints = new Dictionary<int, int>
                {
                    { 30, CurveP30 },
                    { 40, CurveP40 },
                    { 50, CurveP50 },
                    { 60, CurveP60 },
                    { 70, CurveP70 },
                    { 80, CurveP80 },
                    { 90, CurveP90 }
                }
            };

            Profiles.Add(newProfile);
            ActiveProfile = newProfile;
            LoadCurveFromProfile(newProfile);
            StatusMessage = $"Đã tạo Profile mới: {newProfile.Name}";
        }

        [RelayCommand]
        public void DeleteActiveProfile()
        {
            if (Profiles.Count <= 1)
            {
                StatusMessage = "⚠️ Không thể xóa! Hệ thống phải duy trì ít nhất 1 Profile.";
                return;
            }

            if (ActiveProfile != null && Profiles.Contains(ActiveProfile))
            {
                string deletedName = ActiveProfile.Name;
                Profiles.Remove(ActiveProfile);
                ActiveProfile = Profiles[0];
                LoadCurveFromProfile(ActiveProfile);
                StatusMessage = $"Đã xóa Profile: {deletedName}";
            }
        }

        [RelayCommand]
        public void SetQuickFanPreset(object? parameter)
        {
            if (parameter != null && int.TryParse(parameter.ToString(), out int pwmPercent))
            {
                IsAutoMode = false;
                FanPwm = pwmPercent;
                IsFanStateOn = pwmPercent > 0;
                StatusMessage = $"Đã đặt tốc độ quạt thủ công: {pwmPercent}%";
            }
        }

        [RelayCommand]
        public void RefreshComPorts()
        {
            AvailableComPorts.Clear();
            var ports = _serialService.GetAvailablePorts();
            foreach (var port in ports)
            {
                AvailableComPorts.Add(port);
            }
            if (AvailableComPorts.Count > 0 && (string.IsNullOrEmpty(SelectedComPort) || !AvailableComPorts.Contains(SelectedComPort)))
            {
                SelectedComPort = AvailableComPorts[0];
            }
            StatusMessage = AvailableComPorts.Count > 0 ? $"Tìm thấy {AvailableComPorts.Count} cổng COM phần cứng." : "Không tìm thấy cổng COM kết nối.";
        }

        [RelayCommand]
        public void ToggleConnection()
        {
            if (IsConnected)
            {
                _serialService.Disconnect();
                IsConnected = false;
                ConnectionStatusText = "OFFLINE";
                StatusMessage = "Đã ngắt kết nối với ESP32-S3.";
            }
            else if (!string.IsNullOrEmpty(SelectedComPort))
            {
                int baud = int.TryParse(SelectedBaudRate, out int b) ? b : 115200;
                IsConnected = _serialService.Connect(SelectedComPort, baud);
                ConnectionStatusText = IsConnected ? "ONLINE (ESP32-S3)" : "OFFLINE";
                StatusMessage = IsConnected ? $"Đã kết nối thành công tới {SelectedComPort} ({baud} baud)." : $"Không thể kết nối tới {SelectedComPort}.";
            }
            else
            {
                StatusMessage = "Vui lòng chọn cổng COM.";
            }
        }

        [RelayCommand]
        public void SelectRgbPresetColor(string hexColor)
        {
            SelectedRgbColorHex = hexColor;
            StatusMessage = $"Đã đổi màu LED RGB thành: {hexColor}";
        }

        [ObservableProperty] private string _newAppName = "";
        [ObservableProperty] private string _newExePath = "";
        [ObservableProperty] private string _selectedMappingProfileName = "Turbo";

        [ObservableProperty] private bool _isAppPickerOpen = false;
        [ObservableProperty] private string _appPickerSearchText = "";
        public ObservableCollection<RunningAppInfo> RunningApps { get; } = new();
        public ObservableCollection<RunningAppInfo> FilteredRunningApps { get; } = new();

        [RelayCommand]
        public void OpenAppPicker()
        {
            RefreshRunningApps();
            IsAppPickerOpen = true;
        }

        [RelayCommand]
        public void CloseAppPicker()
        {
            IsAppPickerOpen = false;
        }

        [RelayCommand]
        public void RefreshRunningApps()
        {
            RunningApps.Clear();
            var apps = GetSystemRunningApps();
            foreach (var app in apps)
            {
                RunningApps.Add(app);
            }
            FilterRunningApps();
        }

        partial void OnAppPickerSearchTextChanged(string value)
        {
            FilterRunningApps();
        }

        private void FilterRunningApps()
        {
            FilteredRunningApps.Clear();
            string q = (AppPickerSearchText ?? "").Trim().ToLower();
            var matches = string.IsNullOrEmpty(q)
                ? RunningApps
                : RunningApps.Where(a => a.Name.ToLower().Contains(q) || a.ProcessName.ToLower().Contains(q) || a.ExecutablePath.ToLower().Contains(q));

            foreach (var app in matches)
            {
                FilteredRunningApps.Add(app);
            }
        }

        public void SelectRunningApp(RunningAppInfo app)
        {
            if (app == null) return;
            NewAppName = app.Name;
            NewExePath = string.IsNullOrEmpty(app.ExecutablePath) ? app.ProcessName : app.ExecutablePath;
            IsAppPickerOpen = false;
            StatusMessage = $"Đã chọn ứng dụng: {app.Name} ({app.ProcessName})";
        }

        private List<RunningAppInfo> GetSystemRunningApps()
        {
            var list = new List<RunningAppInfo>();
            try
            {
                var processes = Process.GetProcesses();
                int currentPid = Process.GetCurrentProcess().Id;

                foreach (var proc in processes)
                {
                    try
                    {
                        if (proc.Id == currentPid) continue;
                        if (string.IsNullOrWhiteSpace(proc.MainWindowTitle)) continue;

                        string procName = proc.ProcessName;
                        if (procName.Equals("explorer", StringComparison.OrdinalIgnoreCase) ||
                            procName.Equals("SearchHost", StringComparison.OrdinalIgnoreCase) ||
                            procName.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase) ||
                            procName.Equals("cmd", StringComparison.OrdinalIgnoreCase) ||
                            procName.Equals("powershell", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string exePath = "";
                        try { exePath = proc.MainModule?.FileName ?? ""; } catch { }

                        list.Add(new RunningAppInfo
                        {
                            Name = proc.MainWindowTitle,
                            ProcessName = procName + ".exe",
                            ExecutablePath = string.IsNullOrEmpty(exePath) ? procName + ".exe" : exePath,
                            MainWindowTitle = proc.MainWindowTitle
                        });
                    }
                    catch { }
                }
            }
            catch { }

            return list.OrderBy(a => a.Name).ToList();
        }

        [RelayCommand]
        public void AddAppMapping()
        {
            if (string.IsNullOrWhiteSpace(NewAppName)) return;
            AppMappings.Add(new AppMapping
            {
                AppName = NewAppName,
                ExecutablePath = NewExePath,
                ProcessName = System.IO.Path.GetFileNameWithoutExtension(NewExePath),
                ProfileName = SelectedMappingProfileName,
                IsEnabled = true
            });
            NewAppName = "";
            NewExePath = "";
            StatusMessage = "Đã thêm gán ứng dụng mới.";
        }

        [RelayCommand]
        public void RemoveAppMapping(AppMapping mapping)
        {
            if (mapping != null && AppMappings.Contains(mapping))
            {
                AppMappings.Remove(mapping);
                StatusMessage = $"Đã xóa gán ứng dụng: {mapping.AppName}";
            }
        }

        [RelayCommand]
        public void RegisterLeftMouseClick()
        {
            MouseClickCountLeft++;
        }

        [RelayCommand]
        public void RegisterRightMouseClick()
        {
            MouseClickCountRight++;
        }

        [RelayCommand]
        public void RegisterMiddleMouseClick()
        {
            MouseClickCountMiddle++;
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

        [RelayCommand]
        public void ResetMouseTestCounters()
        {
            MouseClickCountLeft = 0;
            MouseClickCountRight = 0;
            MouseClickCountMiddle = 0;
            StatusMessage = "Đã reset bộ đếm Test chuột.";
        }
    }
}
