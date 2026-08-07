namespace SmartFanCooling.Models
{
    public class BleDeviceItem
    {
        public string Name { get; set; } = "ESP32-S3 Smart Fan Hub";
        public ulong Address { get; set; } = 0;
        public string MacAddress { get; set; } = "AA:BB:CC:DD:EE:FF";
        public int Rssi { get; set; } = -55;
        public string SignalText => $"{Rssi} dBm";
        public bool IsConnected { get; set; } = false;
    }
}
