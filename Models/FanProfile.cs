using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartFanCooling.Models
{
    public class FanProfile : ObservableObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "New Profile";
        public string Description { get; set; } = "Custom fan curve profile";
        public string IconGlyph { get; set; } = "\uE9CA"; // Fan icon
        public string ColorHex { get; set; } = "#00BCD4";
        public int MaxFanPwm { get; set; } = 100;

        // Fan control settings remembered per profile
        private bool _isAutoMode = false;
        public bool IsAutoMode
        {
            get => _isAutoMode;
            set => SetProperty(ref _isAutoMode, value);
        }

        private int _targetRpm = 1400;
        public int TargetRpm
        {
            get => _targetRpm;
            set => SetProperty(ref _targetRpm, value);
        }

        private int _fanPwm = 50;
        public int FanPwm
        {
            get => _fanPwm;
            set => SetProperty(ref _fanPwm, value);
        }

        // LED RGB settings remembered per profile
        private int _ledMode = 1; // 0: Off, 1: Static, 2: Rainbow, etc.
        public int LedMode
        {
            get => _ledMode;
            set => SetProperty(ref _ledMode, value);
        }

        private string _ledColorHex = "#00BCD4";
        public string LedColorHex
        {
            get => _ledColorHex;
            set => SetProperty(ref _ledColorHex, value);
        }

        private int _ledBrightness = 80;
        public int LedBrightness
        {
            get => _ledBrightness;
            set => SetProperty(ref _ledBrightness, value);
        }

        private int _ledSpeed = 50;
        public int LedSpeed
        {
            get => _ledSpeed;
            set => SetProperty(ref _ledSpeed, value);
        }

        private bool _isLedReverse = false;
        public bool IsLedReverse
        {
            get => _isLedReverse;
            set => SetProperty(ref _isLedReverse, value);
        }

        private int _rainbowColorCountIndex = 0;
        public int RainbowColorCountIndex
        {
            get => _rainbowColorCountIndex;
            set => SetProperty(ref _rainbowColorCountIndex, value);
        }

        private bool _isActive;
        [JsonIgnore]
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

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
