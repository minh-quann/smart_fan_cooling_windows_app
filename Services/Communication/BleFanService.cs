using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using SmartFanCooling.Models;

using SmartFanCooling.Services.Interfaces;

namespace SmartFanCooling.Services
{
    /// <summary>
    /// Manages Bluetooth Low Energy (BLE) communication with ESP32 Smart Fan.
    /// </summary>
    public class BleFanService : IBleFanService
    {
        private BluetoothLEDevice? _bluetoothLeDevice;
        private GattCharacteristic? _controlCharacteristic;
        private BluetoothLEAdvertisementWatcher? _watcher;

        public bool IsConnected => _bluetoothLeDevice != null && _bluetoothLeDevice.ConnectionStatus == BluetoothConnectionStatus.Connected;

        public event Action<BleDeviceItem>? OnBleDeviceDiscovered;

        public void StartContinuousScan()
        {
            StopScan();
            _watcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Active
            };

            _watcher.Received += Watcher_Received;
            _watcher.Start();
        }

        private void Watcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            string name = args.Advertisement.LocalName;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"Thiết bị BLE ({args.BluetoothAddress:X})";
            }

            byte[] bytes = BitConverter.GetBytes(args.BluetoothAddress);
            Array.Reverse(bytes);
            string mac = string.Join(":", bytes.TakeLast(6).Select(b => b.ToString("X2")));

            var item = new BleDeviceItem
            {
                Name = name,
                Address = args.BluetoothAddress,
                MacAddress = mac,
                Rssi = args.RawSignalStrengthInDBm
            };

            OnBleDeviceDiscovered?.Invoke(item);
        }

        public void StopScan()
        {
            if (_watcher != null)
            {
                try
                {
                    _watcher.Received -= Watcher_Received;
                    _watcher.Stop();
                }
                catch { }
                _watcher = null;
            }
        }

        public async Task<bool> ConnectAsync(ulong bluetoothAddress)
        {
            try
            {
                _bluetoothLeDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress);
                if (_bluetoothLeDevice == null) return false;

                var servicesResult = await _bluetoothLeDevice.GetGattServicesAsync();
                if (servicesResult.Status == GattCommunicationStatus.Success)
                {
                    foreach (var service in servicesResult.Services)
                    {
                        var charResult = await service.GetCharacteristicsAsync();
                        if (charResult.Status == GattCommunicationStatus.Success)
                        {
                            foreach (var c in charResult.Characteristics)
                            {
                                if (c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Write))
                                {
                                    _controlCharacteristic = c;
                                    return true;
                                }
                            }
                        }
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task SendControlAsync(int pwmPercent, int ledMode)
        {
            if (_controlCharacteristic != null)
            {
                try
                {
                    var writer = new DataWriter();
                    writer.WriteByte((byte)pwmPercent);
                    writer.WriteByte((byte)ledMode);
                    await _controlCharacteristic.WriteValueAsync(writer.DetachBuffer());
                }
                catch { }
            }
        }

        public void Disconnect()
        {
            _controlCharacteristic = null;
            _bluetoothLeDevice?.Dispose();
            _bluetoothLeDevice = null;
        }
    }
}
