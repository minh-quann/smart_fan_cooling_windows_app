using System;

namespace SmartFanCooling.Services.Interfaces
{
    /// <summary>
    /// Contract for USB Serial communication with ESP32-S3 over COM ports.
    /// </summary>
    public interface ISerialFanService
    {
        event Action<int>? OnRpmReceived;
        event Action<int>? OnFanPctReceived;
        event Action<int>? OnLedModeReceived;
        event Action<string>? OnLogReceived;

        bool IsConnected { get; }

        string[] GetAvailablePorts();
        bool Connect(string portName, int baudRate = 115200);
        void Disconnect();
        void SendPing();
        void SetFanSpeed(int percent);
        void SetTargetRpm(int targetRpm);
        void SetFanState(bool on);
        void SetLedState(bool on);
        void SetLedMode(int mode);
        void SetLedColor(int r, int g, int b);
        void SetLedBrightness(int brightness);
        void SetLedSpeed(int speed);
        void SetLedDirection(bool reverse);
        void SetRainbowColorCount(int count);
        void SendShutdown();
        void SendTemperature(float cpuTemp, float gpuTemp, int cpuFanRpm = 0, int gpuFanRpm = 0,
            float cpuUsage = 0, float gpuUsage = 0, float cpuPower = 0, float gpuPower = 0,
            float cpuClock = 0, float gpuClock = 0, float ramUsed = 0, float ramTotal = 0,
            float boardTemp = 0);
        void SendOledBitmap(int dispIndex, string hexData);
        void SetCustomOledMode(int dispIndex, bool enable);
        void SendOledConfig(int dispIndex, int rowCount, int row1, int row2, int row3, int row4,
            bool topDiv, bool botDiv, bool pwmBar, string customTitle);
        void SendOledConfigReset(int dispIndex);
        void SendControl(int pwmPercent, int ledMode, float cpuTemp, float gpuTemp = 0f, int cpuFanRpm = 0, int gpuFanRpm = 0);
        void SendRawText(string text);
    }
}
