using System;
using System.IO.Ports;
using System.Text.Json;

namespace SmartFanCooling.Services
{
    /// <summary>
    /// Manages USB Serial communication with ESP32-S3 over COM ports.
    /// </summary>
    public class SerialFanService
    {
        private SerialPort? _serialPort;

        public event Action<int>? OnRpmReceived;
        public event Action<string>? OnLogReceived;

        public bool IsConnected => _serialPort != null && _serialPort.IsOpen;

        public string[] GetAvailablePorts()
        {
            return SerialPort.GetPortNames();
        }

        public bool Connect(string portName, int baudRate = 115200)
        {
            try
            {
                Disconnect();
                _serialPort = new SerialPort(portName, baudRate);
                _serialPort.DataReceived += SerialPort_DataReceived;
                _serialPort.Open();
                return true;
            }
            catch (Exception ex)
            {
                OnLogReceived?.Invoke($"Connection failed: {ex.Message}");
                return false;
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (_serialPort == null || !_serialPort.IsOpen) return;
                string line = _serialPort.ReadLine();
                
                // Parse JSON telemetry from ESP32: {"rpm": 2450, "pwm": 60, "temp": 48.5}
                using var doc = JsonDocument.Parse(line);
                if (doc.RootElement.TryGetProperty("rpm", out var rpmProp))
                {
                    OnRpmReceived?.Invoke(rpmProp.GetInt32());
                }
            }
            catch
            {
                // Ignore parsing errors on fragmented lines
            }
        }

        public void SendControl(int pwmPercent, int ledMode, float cpuTemp)
        {
            if (IsConnected && _serialPort != null)
            {
                try
                {
                    var command = new
                    {
                        pwm = pwmPercent,
                        led = ledMode,
                        temp = cpuTemp
                    };
                    string json = JsonSerializer.Serialize(command);
                    _serialPort.WriteLine(json);
                }
                catch (Exception ex)
                {
                    OnLogReceived?.Invoke($"Failed to send control: {ex.Message}");
                }
            }
        }

        public void Disconnect()
        {
            if (_serialPort != null)
            {
                try
                {
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.DataReceived -= SerialPort_DataReceived;
                        _serialPort.Close();
                    }
                    _serialPort.Dispose();
                }
                catch { }
                finally
                {
                    _serialPort = null;
                }
            }
        }
    }
}
