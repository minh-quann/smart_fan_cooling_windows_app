using System.Collections.Generic;

namespace SmartFanCooling.Models
{
    public class FanProfile
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString();
        public string Name { get; set; } = "New Profile";
        public string Description { get; set; } = "Custom fan curve profile";
        public string IconGlyph { get; set; } = "\uE9CA"; // Fan icon
        public string ColorHex { get; set; } = "#00BCD4";
        public int MaxFanPwm { get; set; } = 80;
        public int LedMode { get; set; } = 1;

        // Fan curve mapping: Temperature (°C) => PWM (%)
        public Dictionary<int, int> CurvePoints { get; set; } = new()
        {
            { 30, 20 },
            { 40, 30 },
            { 50, 45 },
            { 60, 60 },
            { 70, 75 },
            { 80, 90 },
            { 90, 100 }
        };
    }
}
