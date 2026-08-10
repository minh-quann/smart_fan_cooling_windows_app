using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SmartFanCooling.ViewModels
{
    /// <summary>
    /// Partial class for RGB Lighting modes, brightness, speed, and color customization.
    /// </summary>
    public partial class MainViewModel
    {
        // RGB Lighting
        [ObservableProperty] private int _selectedLedMode = 1;
        [ObservableProperty] private string _selectedRgbColorHex = "#00BCD4";
        [ObservableProperty] private int _rgbBrightness = 80;
        [ObservableProperty] private int _rgbSpeed = 50;
        [ObservableProperty] private bool _isLedReverse = false;

        partial void OnSelectedLedModeChanged(int value)
        {
            if (!_isSyncingFromHardware && IsConnected && ActiveConnectionType == "USB_SERIAL")
            {
                _serialService.SetLedMode(value);
            }
        }

        partial void OnRgbBrightnessChanged(int value)
        {
            if (!_isSyncingFromHardware && IsConnected && ActiveConnectionType == "USB_SERIAL")
            {
                int byteVal = Math.Clamp((int)(value * 2.55), 0, 255);
                _serialService.SetLedBrightness(byteVal);
            }
        }

        partial void OnRgbSpeedChanged(int value)
        {
            if (!_isSyncingFromHardware && IsConnected && ActiveConnectionType == "USB_SERIAL")
            {
                _serialService.SetLedSpeed(Math.Clamp(value, 1, 100));
            }
        }

        partial void OnIsLedReverseChanged(bool value)
        {
            if (!_isSyncingFromHardware && IsConnected && ActiveConnectionType == "USB_SERIAL")
            {
                _serialService.SetLedDirection(value);
            }
        }

        [RelayCommand]
        public void SelectRgbPresetColor(string hexColor)
        {
            SelectedRgbColorHex = hexColor;
            StatusMessage = $"Đã đổi màu LED RGB thành: {hexColor}";
        }
    }
}
