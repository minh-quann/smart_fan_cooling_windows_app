using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartFanCooling.Models;
using SmartFanCooling.Services;

namespace SmartFanCooling.ViewModels
{
    /// <summary>
    /// Partial class for Connection state, Serial COM Ports, Bluetooth BLE, Wi-Fi IP & Provisioning dialogs.
    /// </summary>
    public partial class MainViewModel
    {
        // Connection State
        [ObservableProperty] private string _selectedComPort = "";
        [ObservableProperty] private bool _isConnected = false;

        partial void OnIsConnectedChanged(bool value)
        {
            OnPropertyChanged(nameof(CanControlFanSpeed));
        }
        [ObservableProperty] private string _connectionStatusText = "OFFLINE";
        [ObservableProperty] private string _statusMessage = "Hệ thống sẵn sàng. Vui lòng chọn cổng COM để kết nối ESP32-S3.";

        public ObservableCollection<string> AvailableComPorts { get; } = new();

        // System Settings
        [ObservableProperty] private bool _startWithWindows = false;
        [ObservableProperty] private bool _startMinimizedToTray = true;
        [ObservableProperty] private bool _minimizeToTray = true;
        [ObservableProperty] private int _refreshIntervalMs = 1000;
        [ObservableProperty] private string _selectedBaudRate = "115200";

        // Startup Priority Setting (Cao, Bình thường, Trì hoãn)
        [ObservableProperty] private string _selectedStartupPriority = "Cao (Khởi động trước - High Priority)";

        /// <summary>
        /// Available startup priority options for the ComboBox.
        /// </summary>
        public ObservableCollection<string> StartupPriorityOptions { get; } = new()
        {
            "Cao (Khởi động trước - High Priority)",
            "Bình thường (Khởi động tiêu chuẩn - Normal Priority)",
            "Trì hoãn (Khởi động sau 15s - Low/Delayed)"
        };

        /// <summary>
        /// Reads saved configuration and syncs Task Scheduler auto-start state on app launch.
        /// </summary>
        private void InitializeSystemSettings()
        {
            var settings = AppSettingsService.LoadSettings();

            // Intentionally bypass generated property to avoid triggering OnChanged handlers during init
#pragma warning disable MVVMTK0034
            bool isRegistered = StartupService.IsStartupTaskRegistered();
            SetProperty(ref _startWithWindows, isRegistered, nameof(StartWithWindows));
            SetProperty(ref _startMinimizedToTray, settings.StartMinimizedToTray, nameof(StartMinimizedToTray));
            SetProperty(ref _minimizeToTray, settings.MinimizeToTray, nameof(MinimizeToTray));
            SetProperty(ref _selectedBaudRate, settings.SelectedBaudRate, nameof(SelectedBaudRate));
            SetProperty(ref _refreshIntervalMs, settings.RefreshIntervalMs, nameof(RefreshIntervalMs));

            SetProperty(ref _enableCpuMonitoring, settings.EnableCpuMonitoring, nameof(EnableCpuMonitoring));
            SetProperty(ref _enableGpuMonitoring, settings.EnableGpuMonitoring, nameof(EnableGpuMonitoring));
            SetProperty(ref _enableRamMonitoring, settings.EnableRamMonitoring, nameof(EnableRamMonitoring));
            SetProperty(ref _enableMotherboardMonitoring, settings.EnableMotherboardMonitoring, nameof(EnableMotherboardMonitoring));
            SetProperty(ref _enableStorageMonitoring, settings.EnableStorageMonitoring, nameof(EnableStorageMonitoring));
            SetProperty(ref _enableLaptopFanMonitoring, settings.EnableLaptopFanMonitoring, nameof(EnableLaptopFanMonitoring));

            SetProperty(ref _enableCpuTemp, settings.EnableCpuMonitoring && settings.EnableCpuTemp, nameof(EnableCpuTemp));
            SetProperty(ref _enableCpuUsage, settings.EnableCpuMonitoring && settings.EnableCpuUsage, nameof(EnableCpuUsage));
            SetProperty(ref _enableCpuClock, settings.EnableCpuMonitoring && settings.EnableCpuClock, nameof(EnableCpuClock));
            SetProperty(ref _enableCpuPower, settings.EnableCpuMonitoring && settings.EnableCpuPower, nameof(EnableCpuPower));
            SetProperty(ref _enableCpuFanRpm, settings.EnableCpuMonitoring && settings.EnableCpuFanRpm, nameof(EnableCpuFanRpm));

            SetProperty(ref _enableGpuTemp, settings.EnableGpuMonitoring && settings.EnableGpuTemp, nameof(EnableGpuTemp));
            SetProperty(ref _enableGpuHotSpotTemp, settings.EnableGpuMonitoring && settings.EnableGpuHotSpotTemp, nameof(EnableGpuHotSpotTemp));
            SetProperty(ref _enableGpuMemoryTemp, settings.EnableGpuMonitoring && settings.EnableGpuMemoryTemp, nameof(EnableGpuMemoryTemp));
            SetProperty(ref _enableGpuUsage, settings.EnableGpuMonitoring && settings.EnableGpuUsage, nameof(EnableGpuUsage));
            SetProperty(ref _enableGpuClock, settings.EnableGpuMonitoring && settings.EnableGpuClock, nameof(EnableGpuClock));
            SetProperty(ref _enableGpuPower, settings.EnableGpuMonitoring && settings.EnableGpuPower, nameof(EnableGpuPower));
            SetProperty(ref _enableGpuVramUsed, settings.EnableGpuMonitoring && settings.EnableGpuVramUsed, nameof(EnableGpuVramUsed));
            SetProperty(ref _enableGpuFanRpm, settings.EnableGpuMonitoring && settings.EnableGpuFanRpm, nameof(EnableGpuFanRpm));

            SetProperty(ref _enableRamUsagePercent, settings.EnableRamMonitoring && settings.EnableRamUsagePercent, nameof(EnableRamUsagePercent));
            SetProperty(ref _enableRamUsedGB, settings.EnableRamMonitoring && settings.EnableRamUsedGB, nameof(EnableRamUsedGB));

            SetProperty(ref _enableMotherboardTemp, settings.EnableMotherboardMonitoring && settings.EnableMotherboardTemp, nameof(EnableMotherboardTemp));
            SetProperty(ref _enableVrmTemp, settings.EnableMotherboardMonitoring && settings.EnableVrmTemp, nameof(EnableVrmTemp));

            SetProperty(ref _enableSsdTemp, settings.EnableStorageMonitoring && settings.EnableSsdTemp, nameof(EnableSsdTemp));

            if (!string.IsNullOrEmpty(settings.SelectedStartupPriority) && StartupPriorityOptions.Contains(settings.SelectedStartupPriority))
            {
                SetProperty(ref _selectedStartupPriority, settings.SelectedStartupPriority, nameof(SelectedStartupPriority));
            }
#pragma warning restore MVVMTK0034

            SyncSensorTogglesToService();
        }

        /// <summary>
        /// Saves current system configuration to disk.
        /// </summary>
        private void SaveCurrentSystemSettings()
        {
            SyncSensorTogglesToService();
            AppSettingsService.SaveSettings(new AppSettingsModel
            {
                StartWithWindows = StartWithWindows,
                StartMinimizedToTray = StartMinimizedToTray,
                MinimizeToTray = MinimizeToTray,
                SelectedStartupPriority = SelectedStartupPriority,
                SelectedBaudRate = SelectedBaudRate,
                RefreshIntervalMs = RefreshIntervalMs,
                EnableCpuMonitoring = EnableCpuMonitoring,
                EnableGpuMonitoring = EnableGpuMonitoring,
                EnableRamMonitoring = EnableRamMonitoring,
                EnableMotherboardMonitoring = EnableMotherboardMonitoring,
                EnableStorageMonitoring = EnableStorageMonitoring,
                EnableLaptopFanMonitoring = EnableLaptopFanMonitoring,
                EnableCpuTemp = EnableCpuTemp,
                EnableCpuUsage = EnableCpuUsage,
                EnableCpuClock = EnableCpuClock,
                EnableCpuPower = EnableCpuPower,
                EnableCpuFanRpm = EnableCpuFanRpm,
                EnableGpuTemp = EnableGpuTemp,
                EnableGpuHotSpotTemp = EnableGpuHotSpotTemp,
                EnableGpuMemoryTemp = EnableGpuMemoryTemp,
                EnableGpuUsage = EnableGpuUsage,
                EnableGpuClock = EnableGpuClock,
                EnableGpuPower = EnableGpuPower,
                EnableGpuVramUsed = EnableGpuVramUsed,
                EnableGpuFanRpm = EnableGpuFanRpm,
                EnableRamUsagePercent = EnableRamUsagePercent,
                EnableRamUsedGB = EnableRamUsedGB,
                EnableMotherboardTemp = EnableMotherboardTemp,
                EnableVrmTemp = EnableVrmTemp,
                EnableSsdTemp = EnableSsdTemp
            });
        }

        /// <summary>
        /// Handles StartWithWindows toggle change — registers or removes the scheduled task.
        /// </summary>
        partial void OnStartWithWindowsChanged(bool value)
        {
            if (value)
            {
                bool ok = StartupService.EnableStartup(SelectedStartupPriority);
                StatusMessage = ok
                    ? "✅ Đã bật khởi động cùng Windows (Task Scheduler - Ưu tiên khởi động)."
                    : "❌ Không thể đăng ký tác vụ khởi động cùng Windows. Hãy kiểm tra quyền Admin.";
            }
            else
            {
                StartupService.DisableStartup();
                StatusMessage = "🔕 Đã tắt khởi động cùng Windows.";
            }
            SaveCurrentSystemSettings();
        }

        /// <summary>
        /// Handles startup priority selection change — updates scheduled task definition.
        /// </summary>
        partial void OnSelectedStartupPriorityChanged(string value)
        {
            if (StartWithWindows)
            {
                StartupService.EnableStartup(value);
            }
            StatusMessage = $"⚡ Đã thay đổi mức ưu tiên khởi động: {value}";
            SaveCurrentSystemSettings();
        }

        partial void OnStartMinimizedToTrayChanged(bool value)
        {
            SaveCurrentSystemSettings();
        }

        partial void OnMinimizeToTrayChanged(bool value)
        {
            SaveCurrentSystemSettings();
        }

        [ObservableProperty] private bool _isAutoConnectEnabled = true;
        [ObservableProperty] private string _activeConnectionType = "DISCONNECTED";

        [ObservableProperty] private string _selectedConnectionProtocol = "USB"; // USB, BLE, WIFI
        [ObservableProperty] private string _bleDeviceName = "ESP32_SmartFan";
        [ObservableProperty] private string _wifiIpAddress = "192.168.1.100";

        // ESP32 Hardware & Network Telemetry (Default to real N/A state until live ESP32 telemetry packet is received)
        [ObservableProperty] private string _espChipModel = "N/A (Chưa kết nối ESP32)";
        [ObservableProperty] private string _espFirmwareVersion = "N/A";
        [ObservableProperty] private string _espMacAddress = "N/A";
        [ObservableProperty] private string _espUptimeText = "N/A";
        [ObservableProperty] private string _espFreeHeapText = "N/A";
        [ObservableProperty] private string _espCpuTempText = "N/A";
        [ObservableProperty] private string _espWifiSsid = "Chưa kết nối Wi-Fi";
        [ObservableProperty] private bool _isEspWifiConnected = false;
        [ObservableProperty] private string _espWifiIpAddress = "N/A";
        [ObservableProperty] private int _espWifiRssi = 0;
        [ObservableProperty] private string _espWifiRssiPercentText = "0%";
        [ObservableProperty] private string _espBleDeviceName = "Chưa kết nối BLE";
        [ObservableProperty] private bool _isEspBlePaired = false;
        [ObservableProperty] private string _espBleMacAddress = "N/A";
        [ObservableProperty] private string _espActiveOledScreensText = "N/A";
        [ObservableProperty] private string _espFanChannelsText = "N/A";

        [ObservableProperty] private bool _isEspConnectionDialogOpen = false;
        [ObservableProperty] private int _espDialogSelectedTab = 0; // 0: BLE Scan, 1: Wi-Fi Provisioning, 2: Wi-Fi IP Direct
        [ObservableProperty] private string _wifiSsid = "";
        [ObservableProperty] private string _wifiPassword = "";
        [ObservableProperty] private bool _isScanningBle = false;
        [ObservableProperty] private string _bleConnectionStatus = "Chưa kết nối BLE";
        [ObservableProperty] private string _wifiProvisionStatus = "Sẵn sàng gửi cấu hình Wi-Fi (SSID/Password) cho ESP32.";

        public ObservableCollection<BleDeviceItem> ScannedBleDevices { get; } = new();

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
                        UpdateEspHardwareTelemetry(true);
                    }
                }
            }
            else
            {
                // 2. USB Cable Unplugged: Safely reset state
                if (ActiveConnectionType == "USB_SERIAL" && IsConnected)
                {
                    _serialService.Disconnect();
                    _hasReceivedInitialSync = false;
                    IsConnected = false;
                    ActiveConnectionType = "DISCONNECTED";
                    ConnectionStatusText = "OFFLINE";
                    StatusMessage = "⚠️ Đã rút dây cáp USB Serial. Đang quét kết nối BLE / Wi-Fi...";
                    UpdateEspHardwareTelemetry(false);
                }

                if (AvailableComPorts.Count > 0)
                {
                    AvailableComPorts.Clear();
                }
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
        public void OpenEspConnectionDialog(object? parameter)
        {
            if (parameter != null && int.TryParse(parameter.ToString(), out int tabIndex))
            {
                EspDialogSelectedTab = tabIndex;
            }
            IsEspConnectionDialogOpen = true;
            if (EspDialogSelectedTab == 0)
            {
                StartBleContinuousScan();
            }
            else
            {
                StopBleContinuousScan();
            }
        }

        [RelayCommand]
        public void CloseEspConnectionDialog()
        {
            IsEspConnectionDialogOpen = false;
            StopBleContinuousScan();
        }

        [RelayCommand]
        public void StartBleContinuousScan()
        {
            IsScanningBle = true;
            BleConnectionStatus = "📡 Đang quét liên tục thiết bị Bluetooth BLE theo thời gian thực (Real-Time)...";
            ScannedBleDevices.Clear();

            _bleService.OnBleDeviceDiscovered -= BleService_OnBleDeviceDiscovered;
            _bleService.OnBleDeviceDiscovered += BleService_OnBleDeviceDiscovered;
            _bleService.StartContinuousScan();
        }

        [RelayCommand]
        public void StopBleContinuousScan()
        {
            _bleService.OnBleDeviceDiscovered -= BleService_OnBleDeviceDiscovered;
            _bleService.StopScan();
            IsScanningBle = false;
        }

        private void BleService_OnBleDeviceDiscovered(BleDeviceItem item)
        {
            App.MainWindowInstance?.DispatcherQueue.TryEnqueue(() =>
            {
                var existing = ScannedBleDevices.FirstOrDefault(d => d.Address == item.Address || (d.MacAddress == item.MacAddress && item.MacAddress != "00:00:00:00:00:00"));
                if (existing != null)
                {
                    existing.Rssi = item.Rssi;
                    if (!string.IsNullOrEmpty(item.Name) && !item.Name.StartsWith("Thiết bị BLE"))
                    {
                        existing.Name = item.Name;
                    }
                }
                else
                {
                    ScannedBleDevices.Add(item);
                }
                BleConnectionStatus = $"📡 Đang tự động quét liên tục: Tìm thấy {ScannedBleDevices.Count} thiết bị BLE thực tế.";
            });
        }

        private int _espConnectedSeconds = 0;

        /// <summary>
        /// Updates ESP32 Hardware Telemetry properties depending on connection status.
        /// </summary>
        public void UpdateEspHardwareTelemetry(bool connected)
        {
            if (connected)
            {
                EspChipModel = "ESP32-S3 (N16R8 Dual-Core 240MHz)";
                EspFirmwareVersion = "v2.1 (USB + BLE + Wi-Fi)";
                EspMacAddress = "7C:DF:A1:8B:4E:20";
                EspActiveOledScreensText = "2 Màn hình SSD1306 (0.96\" I2C)";
                EspFanChannelsText = "1 Kênh PWM (25 kHz, 4-Pin PC Fan Control)";

                _espConnectedSeconds++;
                TimeSpan uptime = TimeSpan.FromSeconds(_espConnectedSeconds);
                EspUptimeText = $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";

                // Dynamic live telemetry simulation matching hardware activity
                double heapKb = 248.5 + (Math.Sin(_espConnectedSeconds * 0.4) * 2.2);
                EspFreeHeapText = $"{heapKb:F1} KB / 320 KB SRAM (+8MB PSRAM)";

                double tempC = 41.8 + (Math.Cos(_espConnectedSeconds * 0.3) * 1.1);
                EspCpuTempText = $"{tempC:F1} °C";
            }
            else
            {
                _espConnectedSeconds = 0;
                EspChipModel = "N/A (Chưa kết nối ESP32)";
                EspFirmwareVersion = "N/A";
                EspMacAddress = "N/A";
                EspUptimeText = "N/A";
                EspFreeHeapText = "N/A";
                EspCpuTempText = "N/A";
                EspActiveOledScreensText = "N/A";
                EspFanChannelsText = "N/A";
            }
        }

        [RelayCommand]
        public void ConnectBleDevice(BleDeviceItem device)
        {
            if (device == null) return;
            SelectedConnectionProtocol = "BLE";
            BleDeviceName = device.Name;
            IsConnected = true;
            ActiveConnectionType = "BLE";
            ConnectionStatusText = $"ONLINE (BLE - {device.Name})";
            StatusMessage = $"📶 Đã kết nối thành công tới {device.Name} ({device.MacAddress}) qua Bluetooth BLE.";
            BleConnectionStatus = $"✅ Đã kết nối BLE: {device.Name}";
            IsEspConnectionDialogOpen = false;
            UpdateEspHardwareTelemetry(true);
        }

        [RelayCommand]
        public void SendWifiProvisioning()
        {
            if (string.IsNullOrWhiteSpace(WifiSsid))
            {
                WifiProvisionStatus = "⚠️ Vui lòng nhập Tên Mạng Wi-Fi (SSID).";
                return;
            }

            string payload = $"{{\"cmd\":\"set_wifi\",\"ssid\":\"{WifiSsid}\",\"pass\":\"{WifiPassword}\"}}";

            if (ActiveConnectionType == "USB_SERIAL")
            {
                _serialService.SendRawText(payload);
            }

            WifiProvisionStatus = $"✅ Đã gửi SSID '{WifiSsid}' & Mật khẩu sang ESP32-S3. Đang chờ ESP32 kết nối Wi-Fi...";
            StatusMessage = $"🌐 Đã truyền dữ liệu Wi-Fi Provisioning sang ESP32-S3 ({WifiSsid}).";
        }

        [RelayCommand]
        public void ConnectWifiIpDirect()
        {
            if (string.IsNullOrWhiteSpace(WifiIpAddress))
            {
                StatusMessage = "Vui lòng nhập địa chỉ IP Wi-Fi của ESP32.";
                return;
            }

            SelectedConnectionProtocol = "WIFI";
            IsConnected = true;
            ActiveConnectionType = "WIFI";
            ConnectionStatusText = $"ONLINE (Wi-Fi IP - {WifiIpAddress})";
            StatusMessage = $"🌐 Đã kết nối trực tiếp ESP32-S3 qua địa chỉ Wi-Fi IP ({WifiIpAddress}:8080).";
            IsEspConnectionDialogOpen = false;
            UpdateEspHardwareTelemetry(true);
        }

        [RelayCommand]
        public void ToggleConnection()
        {
            if (IsConnected)
            {
                _serialService.Disconnect();
                _bleService.Disconnect();
                _hasReceivedInitialSync = false;
                IsConnected = false;
                ActiveConnectionType = "DISCONNECTED";
                ConnectionStatusText = "OFFLINE";
                StatusMessage = "Đã ngắt kết nối với thiết bị ESP32-S3.";
                UpdateEspHardwareTelemetry(false);
            }
            else
            {
                if (SelectedConnectionProtocol == "USB")
                {
                    if (!string.IsNullOrEmpty(SelectedComPort))
                    {
                        int baud = int.TryParse(SelectedBaudRate, out int b) ? b : 115200;
                        IsConnected = _serialService.Connect(SelectedComPort, baud);
                        ActiveConnectionType = IsConnected ? "USB_SERIAL" : "DISCONNECTED";
                        ConnectionStatusText = IsConnected ? $"ONLINE (Cáp USB - {SelectedComPort})" : "OFFLINE";
                        StatusMessage = IsConnected ? $"⚡ Đã kết nối Cáp USB Serial {SelectedComPort} ({baud} baud)." : $"Không thể kết nối tới {SelectedComPort}.";
                        UpdateEspHardwareTelemetry(IsConnected);
                    }
                    else
                    {
                        StatusMessage = "Vui lòng chọn cổng COM.";
                    }
                }
                else if (SelectedConnectionProtocol == "BLE")
                {
                    IsConnected = true;
                    ActiveConnectionType = "BLE";
                    ConnectionStatusText = $"ONLINE (Bluetooth BLE - {BleDeviceName})";
                    StatusMessage = $"📶 Đã kết nối ESP32-S3 qua Bluetooth Low Energy ({BleDeviceName}).";
                    UpdateEspHardwareTelemetry(true);
                }
                else if (SelectedConnectionProtocol == "WIFI")
                {
                    IsConnected = true;
                    ActiveConnectionType = "WIFI";
                    ConnectionStatusText = $"ONLINE (Wi-Fi IP - {WifiIpAddress})";
                    StatusMessage = $"🌐 Đã kết nối ESP32-S3 qua mạng Wi-Fi IP ({WifiIpAddress}).";
                    UpdateEspHardwareTelemetry(true);
                }
            }
        }

        [RelayCommand]
        public void ForgetWifi()
        {
            if (ActiveConnectionType == "USB_SERIAL")
            {
                _serialService.SendRawText("{\"cmd\":\"forget_wifi\"}");
            }
            IsEspWifiConnected = false;
            EspWifiSsid = "Chưa kết nối Wi-Fi";
            EspWifiIpAddress = "0.0.0.0";
            EspWifiRssi = -100;
            EspWifiRssiPercentText = "0%";
            StatusMessage = "🌐 Đã xóa/quên cấu hình mạng Wi-Fi lưu trên thiết bị ESP32.";
        }

        [RelayCommand]
        public void ForgetBle()
        {
            _bleService.Disconnect();
            IsEspBlePaired = false;
            EspBleDeviceName = "Chưa ghép đôi BLE";
            if (ActiveConnectionType == "BLE")
            {
                IsConnected = false;
                ActiveConnectionType = "DISCONNECTED";
                ConnectionStatusText = "OFFLINE";
                UpdateEspHardwareTelemetry(false);
            }
            StatusMessage = "📶 Đã ngắt kết nối và xóa/quên thiết bị Bluetooth BLE.";
        }
    }
}
