using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SmartFanCooling.ViewModels
{
    /// <summary>
    /// Partial class for Mouse click tests and ESP32 GPIO/Encoder hardware test properties.
    /// </summary>
    public partial class MainViewModel
    {
        // Mouse & GPIO Test States
        [ObservableProperty] private int _mouseClickCountLeft = 0;
        [ObservableProperty] private int _mouseClickCountRight = 0;
        [ObservableProperty] private int _mouseClickCountMiddle = 0;
        [ObservableProperty] private int _mouseX = 0;
        [ObservableProperty] private int _mouseY = 0;
        [ObservableProperty] private bool _encoderAState = true;
        [ObservableProperty] private bool _encoderBState = false;
        [ObservableProperty] private int _encoder2AdcMv = 1650;
        [ObservableProperty] private string _gpioStatusLog = "GPIO & Encoder Test initialized.";

        [RelayCommand]
        public void RegisterLeftMouseClick()
        {
            MouseClickCountLeft++;
        }

        [RelayCommand]
        public void RegisterRightMouseClick()
        {
            MouseClickCountRight++;
        }

        [RelayCommand]
        public void RegisterMiddleMouseClick()
        {
            MouseClickCountMiddle++;
        }

        [RelayCommand]
        public void ResetMouseTestCounters()
        {
            MouseClickCountLeft = 0;
            MouseClickCountRight = 0;
            MouseClickCountMiddle = 0;
            StatusMessage = "Đã reset bộ đếm Test chuột.";
        }
    }
}
