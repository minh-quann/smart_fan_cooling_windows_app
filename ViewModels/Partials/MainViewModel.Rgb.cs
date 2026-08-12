using System;
using System.Drawing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartFanCooling.Styles;

namespace SmartFanCooling.ViewModels
{
    /// <summary>
    /// Partial class for RGB Lighting modes, brightness, speed, and color customization.
    /// </summary>
    public partial class MainViewModel
    {
        // RGB Lighting
        [ObservableProperty] private int _selectedLedMode = 1;
        [ObservableProperty] private string _selectedRgbColorHex = AppColors.Cyan500Hex;
        [ObservableProperty] private int _rgbBrightness = 80;
        [ObservableProperty] private int _rgbSpeed = 50;
        [ObservableProperty] private bool _isLedReverse = false;
        [ObservableProperty] private int _rainbowColorCountIndex = 0; // 0: Full (0), 1: 7 Colors, 2: 5 Colors, 3: 3 Colors, 4: 2 Colors

        public bool IsRainbowColorCountVisible => SelectedLedMode == 2 || SelectedLedMode == 5;
        public bool IsStaticColorPickerVisible => SelectedLedMode == 1 || SelectedLedMode == 3;
        public bool IsDirectionToggleVisible => SelectedLedMode != 0 && SelectedLedMode != 1;

        partial void OnSelectedLedModeChanged(int value)
        {
            OnPropertyChanged(nameof(IsRainbowColorCountVisible));
            OnPropertyChanged(nameof(IsStaticColorPickerVisible));
            OnPropertyChanged(nameof(IsDirectionToggleVisible));

            if (!_isSyncingFromHardware && IsConnected && ActiveConnectionType == "USB_SERIAL")
            {
                _serialService.SetLedMode(value);

                // Auto-send static color when switching to Static (1) or Breathing (3)
                if (value == 1 || value == 3)
                {
                    SendCurrentColorToHardware(SelectedRgbColorHex);
                }
                else if (value == 2 || value == 5)
                {
                    int count = RainbowColorCountIndex switch
                    {
                        1 => 7,
                        2 => 5,
                        3 => 3,
                        4 => 2,
                        _ => 0
                    };
                    _serialService.SetRainbowColorCount(count);
                }
            }
        }

        partial void OnSelectedRgbColorHexChanged(string value)
        {
            SendCurrentColorToHardware(value);
        }

        partial void OnRainbowColorCountIndexChanged(int value)
        {
            int count = value switch
            {
                1 => 7,
                2 => 5,
                3 => 3,
                4 => 2,
                _ => 0
            };

            if (!_isSyncingFromHardware && IsConnected && ActiveConnectionType == "USB_SERIAL")
            {
                _serialService.SetRainbowColorCount(count);
            }
        }

        private void SendCurrentColorToHardware(string hexColor)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hexColor)) return;
                var color = ColorTranslator.FromHtml(hexColor);

                if (!_isSyncingFromHardware && IsConnected && ActiveConnectionType == "USB_SERIAL")
                {
                    _serialService.SetLedColor(color.R, color.G, color.B);
                }
            }
            catch { }
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
            SendCurrentColorToHardware(hexColor);
            StatusMessage = $"Đã đổi màu LED RGB thành: {hexColor}";
        }
    }
}
