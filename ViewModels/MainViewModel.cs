using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using SmartFanCooling.Services;
using SmartFanCooling.Services.Interfaces;

namespace SmartFanCooling.ViewModels
{
    /// <summary>
    /// Core MainViewModel managing Tab Selection, Timer loop, and Services orchestration.
    /// Feature domains are separated cleanly into domain partial classes:
    /// - MainViewModel.Telemetry.cs
    /// - MainViewModel.FanControl.cs
    /// - MainViewModel.Rgb.cs
    /// - MainViewModel.OledCanvas.cs
    /// - MainViewModel.AppProfiles.cs
    /// - MainViewModel.GpioTest.cs
    /// - MainViewModel.OsdHud.cs
    /// - MainViewModel.Connectivity.cs
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        private readonly IHardwareMonitorService _hardwareService;
        private readonly ISerialFanService _serialService;
        private readonly IOledCanvasService _oledCanvasService;
        private readonly IBleFanService _bleService;
        private readonly DispatcherTimer _timer;
        private bool _isUpdatingHardware = false;

        // Selected Navigation Tab Index (0: Overview, 1: Fan Curve, 2: RGB, 3: App Profiles, 4: Hardware, 5: GPIO & Mouse Test, 6: HUD Overlay, 7: Settings)
        [ObservableProperty]
        private int _selectedTabIndex = 0;

        // Dashboard Telemetry Refresh Rate in milliseconds (default: 1000ms = 1s)
        [ObservableProperty]
        private int _dashboardRefreshIntervalMs = 1000;

        public string DashboardRefreshIntervalText => $"{DashboardRefreshIntervalMs} ms";

        partial void OnDashboardRefreshIntervalMsChanged(int value)
        {
            OnPropertyChanged(nameof(DashboardRefreshIntervalText));
            if (_timer != null)
            {
                _timer.Interval = TimeSpan.FromMilliseconds(Math.Clamp(value, 100, 5000));
            }
        }

        public MainViewModel() : this(new HardwareMonitorService(), new SerialFanService(), new OledCanvasService(), new BleFanService())
        {
        }

        public MainViewModel(
            IHardwareMonitorService hardwareService,
            ISerialFanService serialService,
            IOledCanvasService oledCanvasService,
            IBleFanService bleService)
        {
            _hardwareService = hardwareService;
            _serialService = serialService;
            _oledCanvasService = oledCanvasService;
            _bleService = bleService;
            _serialService.OnRpmReceived += rpm =>
            {
                App.MainWindowInstance?.DispatcherQueue.TryEnqueue(() =>
                {
                    _isSyncingFromHardware = true;
                    // Llano Laptop Fan max physical speed is ~2800 RPM.
                    if (rpm > 0 && rpm <= 3500)
                    {
                        FanRpm = rpm;
                        TargetRpm = (int)(Math.Round(rpm / 100.0) * 100);
                        FanPwm = Math.Clamp((int)Math.Round(rpm / 28.0), 0, 100);
                    }
                    else
                    {
                        FanRpm = (int)(Math.Round((FanPwm * 28.0) / 100.0) * 100);
                        TargetRpm = FanRpm;
                    }
                    _isSyncingFromHardware = false;
                });
            };
            _serialService.OnFanPctReceived += fanPct =>
            {
                App.MainWindowInstance?.DispatcherQueue.TryEnqueue(() =>
                {
                    if (FanPwm != fanPct)
                    {
                        _isSyncingFromHardware = true;
                        FanPwm = Math.Clamp(fanPct, 0, 100);
                        IsFanStateOn = FanPwm > 0;
                        int calculatedRpm = FanPwm > 0 ? (int)(Math.Round((FanPwm * 28.0) / 100.0) * 100) : 0;
                        TargetRpm = calculatedRpm;
                        FanRpm = calculatedRpm;
                        _isSyncingFromHardware = false;
                    }
                });
            };
            _serialService.OnLedModeReceived += ledMode =>
            {
                App.MainWindowInstance?.DispatcherQueue.TryEnqueue(() =>
                {
                    if (SelectedLedMode != ledMode)
                    {
                        _isSyncingFromHardware = true;
                        SelectedLedMode = ledMode;
                        _isSyncingFromHardware = false;
                    }
                });
            };
            _serialService.OnLogReceived += msg =>
            {
                App.MainWindowInstance?.DispatcherQueue.TryEnqueue(() =>
                {
                    StatusMessage = msg;
                });
            };

            InitializeDefaultProfiles();
            LoadRpmPresets();
            RefreshComPorts();
            CheckAndAutoConnectDevices();
            InitializeSystemSettings();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DashboardRefreshIntervalMs) };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private async void Timer_Tick(object? sender, object e)
        {
            if (_isUpdatingHardware) return;
            _isUpdatingHardware = true;

            try
            {
                // Offload heavy driver hardware sensor polling to background thread pool (prevents UI lag when dragging window)
                await System.Threading.Tasks.Task.Run(() => _hardwareService.UpdateSensors());

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
                UpdateEspHardwareTelemetry(IsConnected);

                // When offline, Llano Hub RPM is strictly 0 — sync all fan state
                if (!IsConnected)
                {
                    _isSyncingFromHardware = true;
                    FanRpm = 0;
                    TargetRpm = 0;
                    FanPwm = 0;
                    IsFanStateOn = false;
                    _isSyncingFromHardware = false;
                }
                else if (FanRpm < 300 || FanRpm > 3500)
                {
                    // Below 300 = fan not spinning, above 3500 = tach noise
                    // Estimate: RPM = 300 + (PWM% * 25), range 300-2800
                    FanRpm = FanPwm > 0 ? 300 + (FanPwm * 25) : 0;
                }

                if (IsConnected && ActiveConnectionType == "USB_SERIAL")
                {
                    // Send full telemetry including extended data for configurable OLED layout
                    _serialService.SendTemperature(CpuTemp, GpuTemp, CpuFanRpm, GpuFanRpm,
                        CpuUsage, GpuUsage, CpuPowerW, GpuPowerW,
                        CpuMaxClockGHz, GpuClockMHz, RamUsedGB, RamTotalGB);
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
            finally
            {
                _isUpdatingHardware = false;
            }
        }
    }
}
