using System;
using System.Threading.Tasks;
using SmartFanCooling.Models;

namespace SmartFanCooling.Services.Interfaces
{
    /// <summary>
    /// Contract for Bluetooth Low Energy (BLE) communication with ESP32 Smart Fan.
    /// </summary>
    public interface IBleFanService
    {
        event Action<BleDeviceItem>? OnBleDeviceDiscovered;

        bool IsConnected { get; }

        void StartContinuousScan();
        void StopScan();
        Task<bool> ConnectAsync(ulong bluetoothAddress);
        Task SendControlAsync(int pwmPercent, int ledMode);
        void Disconnect();
    }
}
