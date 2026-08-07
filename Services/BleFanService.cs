using System;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace SmartFanCooling.Services
{
    /// <summary>
    /// Manages Bluetooth Low Energy (BLE) communication with ESP32 Smart Fan.
    /// </summary>
    public class BleFanService
    {
        private BluetoothLEDevice? _bluetoothLeDevice;
        private GattCharacteristic? _controlCharacteristic;

        public bool IsConnected => _bluetoothLeDevice != null && _bluetoothLeDevice.ConnectionStatus == BluetoothConnectionStatus.Connected;

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
