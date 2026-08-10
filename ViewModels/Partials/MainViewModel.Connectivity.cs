using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartFanCooling.Models;

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
        [ObservableProperty] private string _connectionStatusText = "OFFLINE";
        [ObservableProperty] private string _statusMessage = "Hệ thống sẵn sàng. Vui lòng chọn cổng COM để kết nối ESP32-S3.";

        public ObservableCollection<string> AvailableComPorts { get; } = new();

        // System Settings
        [ObservableProperty] private bool _startWithWindows = false;
        [ObservableProperty] private bool _minimizeToTray = true;
        [ObservableProperty] private int _refreshIntervalMs = 1000;
        [ObservableProperty] private string _selectedBaudRate = "115200";

        [ObservableProperty] private bool _isAutoConnectEnabled = true;
        [ObservableProperty] private string _activeConnectionType = "DISCONNECTED";

        [ObservableProperty] private string _selectedConnectionProtocol = "USB"; // USB, BLE, WIFI
        [ObservableProperty] private string _bleDeviceName = "ESP32_SmartFan";
        [ObservableProperty] private string _wifiIpAddress = "192.168.1.100";

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
        }

        [RelayCommand]
        public void ToggleConnection()
        {
            if (IsConnected)
            {
                _serialService.Disconnect();
                _bleService.Disconnect();
                IsConnected = false;
                ActiveConnectionType = "DISCONNECTED";
                ConnectionStatusText = "OFFLINE";
                StatusMessage = "Đã ngắt kết nối với thiết bị ESP32-S3.";
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
                }
                else if (SelectedConnectionProtocol == "WIFI")
                {
                    IsConnected = true;
                    ActiveConnectionType = "WIFI";
                    ConnectionStatusText = $"ONLINE (Wi-Fi IP - {WifiIpAddress})";
                    StatusMessage = $"🌐 Đã kết nối ESP32-S3 qua mạng Wi-Fi IP ({WifiIpAddress}).";
                }
            }
        }
    }
}
