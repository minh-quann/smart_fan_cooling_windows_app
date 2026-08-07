using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartFanCooling.Services;
using Microsoft.UI.Xaml;

namespace SmartFanCooling.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly HardwareMonitorService _hardwareService;
        private readonly SerialFanService _serialService;
        private readonly DispatcherTimer _timer;

        [ObservableProperty]
        private float _cpuTemp;

        [ObservableProperty]
        private float _gpuTemp;

        [ObservableProperty]
        private float _cpuPowerW;

        [ObservableProperty]
        private float _cpuMaxClockGHz;

        [ObservableProperty]
        private int _fanPwm = 50;

        [ObservableProperty]
        private int _fanRpm = 0;

        [ObservableProperty]
        private int _selectedLedMode = 1; // 0: Off, 1: Static, 2: Breathing, 3: Rainbow, 4: Speed Pulse

        [ObservableProperty]
        private bool _isAutoMode = true;

        [ObservableProperty]
        private string _selectedFanCurve = "Balanced";

        [ObservableProperty]
        private string _selectedComPort = "";

        [ObservableProperty]
        private bool _isConnected = false;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        public ObservableCollection<string> AvailableComPorts { get; } = new();

        public MainViewModel()
        {
            _hardwareService = new HardwareMonitorService();
            _serialService = new SerialFanService();
            _serialService.OnRpmReceived += rpm => FanRpm = rpm;
            _serialService.OnLogReceived += msg => StatusMessage = msg;

            RefreshComPorts();

            // Hardware polling timer (Every 1 second)
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, object e)
        {
            _hardwareService.UpdateSensors();
            CpuTemp = _hardwareService.CpuTemperature;
            GpuTemp = _hardwareService.GpuTemperature;
            CpuPowerW = _hardwareService.CpuPowerW;
            CpuMaxClockGHz = _hardwareService.CpuMaxClockGHz;

            float maxTemp = Math.Max(CpuTemp, GpuTemp);

            if (IsAutoMode)
            {
                FanPwm = AutoFanCurveService.CalculatePwm(maxTemp, SelectedFanCurve);
            }

            if (IsConnected)
            {
                _serialService.SendControl(FanPwm, SelectedLedMode, maxTemp);
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
            StatusMessage = AvailableComPorts.Count > 0 ? $"Found {AvailableComPorts.Count} COM ports." : "No COM ports found.";
        }

        [RelayCommand]
        public void ToggleConnection()
        {
            if (IsConnected)
            {
                _serialService.Disconnect();
                IsConnected = false;
                StatusMessage = "Disconnected.";
            }
            else if (!string.IsNullOrEmpty(SelectedComPort))
            {
                IsConnected = _serialService.Connect(SelectedComPort);
                StatusMessage = IsConnected ? $"Connected to {SelectedComPort}." : $"Failed to connect to {SelectedComPort}.";
            }
            else
            {
                StatusMessage = "Please select a COM port.";
            }
        }

        partial void OnSelectedFanCurveChanged(string value)
        {
            if (IsAutoMode)
            {
                float maxTemp = Math.Max(CpuTemp, GpuTemp);
                FanPwm = AutoFanCurveService.CalculatePwm(maxTemp, value);
            }
        }
    }
}
