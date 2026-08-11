using System;
using System.IO.Ports;
using System.Text.Json;
using System.Globalization;

using SmartFanCooling.Services.Interfaces;

namespace SmartFanCooling.Services
{
    /// <summary>
    /// Manages USB Serial communication with ESP32-S3 over COM ports.
    /// Implements protocol matching ESP32 firmware (ping heartbeat, JSON commands & telemetry).
    /// </summary>
    public class SerialFanService : ISerialFanService
    {
        private SerialPort? _serialPort;
        private System.Timers.Timer? _pingTimer;
        private string _rxBuffer = "";

        public event Action<int>? OnRpmReceived;
        public event Action<int>? OnFanPctReceived;
        public event Action<int>? OnLedModeReceived;
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
                _serialPort = new SerialPort(portName, baudRate)
                {
                    BaudRate = baudRate,
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None,
                    Encoding = System.Text.Encoding.UTF8,
                    DtrEnable = true,
                    RtsEnable = true,
                    NewLine = "\n"
                };

                _serialPort.DataReceived += SerialPort_DataReceived;
                _serialPort.Open();

                // Start ping timer every 2 seconds to keep ESP32 USB session active and receiving telemetry
                _pingTimer = new System.Timers.Timer(2000);
                _pingTimer.Elapsed += (s, e) => SendPing();
                _pingTimer.Start();

                // Send initial ping to establish connection
                SendPing();

                return true;
            }
            catch (Exception ex)
            {
                OnLogReceived?.Invoke($"Connection failed: {ex.Message}");
                return false;
            }
        }

        public void SendPing()
        {
            SendRawText("{\"cmd\":\"ping\"}");
        }

        public void SetFanSpeed(int percent)
        {
            SendRawText($"{{\"cmd\":\"fan_speed\",\"value\":{percent}}}");
        }

        public void SetTargetRpm(int targetRpm)
        {
            SendRawText($"{{\"cmd\":\"fan_target_rpm\",\"value\":{targetRpm}}}");
        }

        public void SetFanState(bool on)
        {
            SendRawText($"{{\"cmd\":\"fan_state\",\"value\":{(on ? 1 : 0)}}}");
        }

        public void SetLedState(bool on)
        {
            SendRawText($"{{\"cmd\":\"led_on\",\"value\":{(on ? "true" : "false")}}}");
        }

        public void SetLedMode(int mode)
        {
            SendRawText($"{{\"cmd\":\"led_mode\",\"value\":{mode}}}");
        }

        public void SetLedColor(int r, int g, int b)
        {
            SendRawText($"{{\"cmd\":\"led_color\",\"r\":{r},\"g\":{g},\"b\":{b}}}");
        }

        public void SetLedBrightness(int brightness)
        {
            SendRawText($"{{\"cmd\":\"led_brightness\",\"value\":{brightness}}}");
        }

        public void SetLedSpeed(int speed)
        {
            SendRawText($"{{\"cmd\":\"led_speed\",\"value\":{speed}}}");
        }

        public void SetLedDirection(bool reverse)
        {
            SendRawText($"{{\"cmd\":\"led_direction\",\"reverse\":{(reverse ? "true" : "false")}}}");
        }

        public void SetRainbowColorCount(int count)
        {
            SendRawText($"{{\"cmd\":\"rainbow_count\",\"value\":{count}}}");
        }

        public void SendTemperature(float cpuTemp, float gpuTemp, int cpuFanRpm = 0, int gpuFanRpm = 0)
        {
            string cpuStr = cpuTemp.ToString("F1", CultureInfo.InvariantCulture);
            string gpuStr = gpuTemp.ToString("F1", CultureInfo.InvariantCulture);
            SendRawText($"{{\"cmd\":\"temp\",\"cpu\":{cpuStr},\"gpu\":{gpuStr},\"cpu_fan\":{cpuFanRpm},\"gpu_fan\":{gpuFanRpm}}}");
        }

        public void SendOledBitmap(int dispIndex, string hexData)
        {
            SendRawText($"{{\"cmd\":\"draw_bitmap\",\"disp\":{dispIndex},\"data\":\"{hexData}\"}}");
        }

        public void SetCustomOledMode(int dispIndex, bool enable)
        {
            SendRawText($"{{\"cmd\":\"custom_oled\",\"disp\":{dispIndex},\"enable\":{(enable ? "true" : "false")}}}");
        }

        public void SendControl(int pwmPercent, int ledMode, float cpuTemp, float gpuTemp = 0f, int cpuFanRpm = 0, int gpuFanRpm = 0)
        {
            if (IsConnected)
            {
                SendTemperature(cpuTemp, gpuTemp, cpuFanRpm, gpuFanRpm);
            }
        }

        public void SendRawText(string text)
        {
            if (IsConnected && _serialPort != null)
            {
                try
                {
                    // Firmware expects strict raw JSON line ending with \n and NO \r
                    string cleanText = text.Trim('\r', '\n') + "\n";
                    _serialPort.Write(cleanText);
                }
                catch (Exception ex)
                {
                    OnLogReceived?.Invoke($"Failed to send text: {ex.Message}");
                }
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (_serialPort == null || !_serialPort.IsOpen) return;
                string existingData = _serialPort.ReadExisting();
                _rxBuffer += existingData;

                while (_rxBuffer.Contains("\n"))
                {
                    int newlineIndex = _rxBuffer.IndexOf("\n");
                    string line = _rxBuffer.Substring(0, newlineIndex).Trim();
                    _rxBuffer = _rxBuffer.Substring(newlineIndex + 1);

                    if (string.IsNullOrEmpty(line)) continue;

                    ProcessIncomingJson(line);
                }

                if (_rxBuffer.Length > 4096)
                {
                    _rxBuffer = "";
                }
            }
            catch { }
        }

        private void ProcessIncomingJson(string line)
        {
            try
            {
                int firstBrace = line.IndexOf('{');
                int lastBrace = line.LastIndexOf('}');
                if (firstBrace >= 0 && lastBrace > firstBrace)
                {
                    string json = line.Substring(firstBrace, lastBrace - firstBrace + 1);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    // Firmware telemetry format: {"fan_pct":40,"fan_on":true,"led_mode":1,"led_on":true,"rpm":2450,"cpu":65.0,"gpu":55.0}
                    if (root.TryGetProperty("rpm", out var rpmProp))
                    {
                        if (rpmProp.ValueKind == JsonValueKind.Number)
                        {
                            int rpm = rpmProp.GetInt32();
                            OnRpmReceived?.Invoke(rpm);
                        }
                    }

                    if (root.TryGetProperty("fan_pct", out var fanPctProp))
                    {
                        if (fanPctProp.ValueKind == JsonValueKind.Number)
                        {
                            int fanPct = fanPctProp.GetInt32();
                            OnFanPctReceived?.Invoke(fanPct);
                        }
                    }

                    if (root.TryGetProperty("led_mode", out var ledModeProp))
                    {
                        if (ledModeProp.ValueKind == JsonValueKind.Number)
                        {
                            int ledMode = ledModeProp.GetInt32();
                            OnLedModeReceived?.Invoke(ledMode);
                        }
                    }
                }
            }
            catch { }
        }

        public void SendShutdown()
        {
            SendRawText("{\"cmd\":\"shutdown\"}");
        }

        public void Disconnect()
        {
            if (_pingTimer != null)
            {
                _pingTimer.Stop();
                _pingTimer.Dispose();
                _pingTimer = null;
            }

            if (_serialPort != null)
            {
                try
                {
                    if (_serialPort.IsOpen)
                    {
                        SendShutdown();
                        System.Threading.Thread.Sleep(50);
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
