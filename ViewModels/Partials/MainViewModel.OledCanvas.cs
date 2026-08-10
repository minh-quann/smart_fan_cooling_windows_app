using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SmartFanCooling.ViewModels
{
    /// <summary>
    /// Partial class for Independent Dual OLED Custom Canvas controls (Screen 1 & Screen 2)
    /// with interactive multi-row layout, granular metric toggles, divider lines & live ESP32 sync.
    /// </summary>
    public partial class MainViewModel
    {
        // Screen Tab Selector (0: OLED 1 - 1.3" 128x64, 1: OLED 2 - 0.96" 128x64)
        [ObservableProperty] private int _activeOledScreenTab = 0;

        public string Oled1TabButtonText => ActiveOledScreenTab == 0 ? "✓ Màn 1: OLED 1.3\" (SH1106)" : "Màn 1: OLED 1.3\" (SH1106)";
        public string Oled2TabButtonText => ActiveOledScreenTab == 1 ? "✓ Màn 2: OLED 0.96\" (SSD1306)" : "Màn 2: OLED 0.96\" (SSD1306)";
        public string ActiveOledScreenTitle => ActiveOledScreenTab == 0 ? "MÀN HÌNH 1: OLED 1.3\" (SH1106 - 128x64 PIXELS)" : "MÀN HÌNH 2: OLED 0.96\" (SSD1306 - 128x64 PIXELS)";

        partial void OnActiveOledScreenTabChanged(int value)
        {
            OnPropertyChanged(nameof(Oled1TabButtonText));
            OnPropertyChanged(nameof(Oled2TabButtonText));
            OnPropertyChanged(nameof(ActiveOledScreenTitle));
            NotifyOledEvaluatedTextChanges();
        }

        // Custom Enable Switches for Screen 1 & Screen 2
        [ObservableProperty] private bool _isCustomOled1Enabled = false;
        [ObservableProperty] private bool _isCustomOled2Enabled = false;

        // Layout Configuration
        [ObservableProperty] private int _oledRowCount = 4; // 2, 3, or 4 rows
        [ObservableProperty] private bool _oledShowTopDivider = true;
        [ObservableProperty] private bool _oledShowBottomDivider = true;
        [ObservableProperty] private bool _oledShowPwmBar = true;

        // Granular Sub-Metric Toggles for CPU Line
        [ObservableProperty] private bool _oledShowCpuTemp = true;
        [ObservableProperty] private bool _oledShowCpuUsage = true;
        [ObservableProperty] private bool _oledShowCpuClock = false;
        [ObservableProperty] private bool _oledShowCpuPower = false;
        [ObservableProperty] private bool _oledShowCpuFan = false;

        // Granular Sub-Metric Toggles for GPU Line
        [ObservableProperty] private bool _oledShowGpuTemp = true;
        [ObservableProperty] private bool _oledShowGpuUsage = true;
        [ObservableProperty] private bool _oledShowGpuClock = false;
        [ObservableProperty] private bool _oledShowGpuPower = false;
        [ObservableProperty] private bool _oledShowGpuVram = false;
        [ObservableProperty] private bool _oledShowGpuFan = false;

        // Granular Sub-Metric Toggles for Fan / RAM / Footer Line
        [ObservableProperty] private bool _oledShowSmartFanRpm = true;
        [ObservableProperty] private bool _oledShowSmartFanPwm = true;
        [ObservableProperty] private bool _oledShowRamUsage = false;

        // Row Widget Selections (HEADER_TITLE, CPU_TELEMETRY, GPU_TELEMETRY, FAN_TELEMETRY, RAM_TELEMETRY, POWER_TELEMETRY, CLOCK_TELEMETRY, PWM_PCT)
        [ObservableProperty] private string _oledRow1Widget = "HEADER_TITLE";
        [ObservableProperty] private string _oledRow2Widget = "CPU_TELEMETRY";
        [ObservableProperty] private string _oledRow3Widget = "GPU_TELEMETRY";
        [ObservableProperty] private string _oledRow4Widget = "FAN_TELEMETRY";

        // Raw Custom Inputs
        [ObservableProperty] private string _oled1CustomTitle = "LLANO SMART FAN";
        [ObservableProperty] private string _oled1CustomMainStat = "30%";
        [ObservableProperty] private string _oled1CustomSubStat = "2400 RPM";
        [ObservableProperty] private int _oled1FontSize = 2;
        [ObservableProperty] private bool _oled1ShowBar = true;

        [ObservableProperty] private string _oled2CustomHeader = "2400 RPM";
        [ObservableProperty] private string _oled2CustomCpuText = "CPU: 55C | 1200";
        [ObservableProperty] private string _oled2CustomGpuText = "GPU: 60C | 1500";
        [ObservableProperty] private string _oled2CustomFooter = "PWM: 50%  USB";

        public string EvaluatedRow1Text => EvaluateWidgetText(OledRow1Widget, Oled1CustomTitle);
        public string EvaluatedRow2Text => EvaluateWidgetText(OledRow2Widget, Oled1CustomMainStat);
        public string EvaluatedRow3Text => EvaluateWidgetText(OledRow3Widget, Oled1CustomSubStat);
        public string EvaluatedRow4Text => EvaluateWidgetText(OledRow4Widget, Oled2CustomFooter);

        private string EvaluateWidgetText(string widgetType, string fallbackText)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            return widgetType switch
            {
                "HEADER_TITLE" => string.IsNullOrEmpty(Oled1CustomTitle) ? "LLANO SMART FAN" : Oled1CustomTitle,
                "CPU_TELEMETRY" => BuildCpuLineText(),
                "GPU_TELEMETRY" => BuildGpuLineText(),
                "FAN_TELEMETRY" => BuildFanLineText(),
                "PWM_PCT" => $"PWM: {FanPwm}%",
                "RAM_TELEMETRY" => $"RAM: {RamUsedGB.ToString("F1", inv)}/{RamTotalGB.ToString("F1", inv)}GB",
                "POWER_TELEMETRY" => $"PWR: {CpuPowerW.ToString("F0", inv)}W/{GpuPowerW.ToString("F0", inv)}W",
                "CLOCK_TELEMETRY" => $"CLK: {CpuMaxClockGHz.ToString("F1", inv)}G/{GpuClockMHz.ToString("F0", inv)}M",
                _ => fallbackText
            };
        }

        private string BuildCpuLineText()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var parts = new List<string>();
            if (OledShowCpuTemp) parts.Add($"{CpuTemp.ToString("F0", inv)}C");
            if (OledShowCpuUsage) parts.Add($"{CpuUsage.ToString("F0", inv)}%");
            if (OledShowCpuClock) parts.Add($"{CpuMaxClockGHz.ToString("0.0", inv)}G");
            if (OledShowCpuPower) parts.Add($"{CpuPowerW.ToString("F0", inv)}W");
            if (OledShowCpuFan && CpuFanRpm > 0) parts.Add($"{CpuFanRpm}RPM");
            return parts.Count > 0 ? "CPU: " + string.Join(" | ", parts) : "CPU: --";
        }

        private string BuildGpuLineText()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var parts = new List<string>();
            if (OledShowGpuTemp) parts.Add($"{GpuTemp.ToString("F0", inv)}C");
            if (OledShowGpuUsage) parts.Add($"{GpuUsage.ToString("F0", inv)}%");
            if (OledShowGpuClock) parts.Add($"{GpuClockMHz.ToString("F0", inv)}M");
            if (OledShowGpuPower) parts.Add($"{GpuPowerW.ToString("F0", inv)}W");
            if (OledShowGpuVram) parts.Add($"{GpuVramUsedGB.ToString("F1", inv)}GB");
            if (OledShowGpuFan && GpuFanRpm > 0) parts.Add($"{GpuFanRpm}RPM");
            return parts.Count > 0 ? "GPU: " + string.Join(" | ", parts) : "GPU: --";
        }

        private string BuildFanLineText()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var parts = new List<string>();
            if (OledShowSmartFanRpm) parts.Add($"{FanRpm} RPM");
            if (OledShowSmartFanPwm) parts.Add($"PWM: {FanPwm}%");
            if (OledShowRamUsage) parts.Add($"RAM: {RamUsedGB.ToString("F1", inv)}/{RamTotalGB.ToString("F1", inv)}GB");
            return parts.Count > 0 ? string.Join(" | ", parts) : "LLANO SMART FAN";
        }

        private void NotifyOledEvaluatedTextChanges()
        {
            OnPropertyChanged(nameof(EvaluatedRow1Text));
            OnPropertyChanged(nameof(EvaluatedRow2Text));
            OnPropertyChanged(nameof(EvaluatedRow3Text));
            OnPropertyChanged(nameof(EvaluatedRow4Text));
        }

        partial void OnOledRowCountChanged(int value) => NotifyOledEvaluatedTextChanges();
        partial void OnOledShowTopDividerChanged(bool value) => NotifyOledEvaluatedTextChanges();
        partial void OnOledShowBottomDividerChanged(bool value) => NotifyOledEvaluatedTextChanges();
        partial void OnOledShowPwmBarChanged(bool value) => NotifyOledEvaluatedTextChanges();

        partial void OnOledShowCpuTempChanged(bool value) => NotifyOledEvaluatedTextChanges();
        partial void OnOledShowCpuUsageChanged(bool value) => NotifyOledEvaluatedTextChanges();
        partial void OnOledShowCpuClockChanged(bool value) => NotifyOledEvaluatedTextChanges();
        partial void OnOledShowCpuPowerChanged(bool value) => NotifyOledEvaluatedTextChanges();
        partial void OnOledShowCpuFanChanged(bool value) => NotifyOledEvaluatedTextChanges();

        partial void OnOledShowGpuTempChanged(bool value) => NotifyOledEvaluatedTextChanges();
        partial void OnOledShowGpuUsageChanged(bool value) => NotifyOledEvaluatedTextChanges();
        partial void OnOledShowGpuClockChanged(bool value) => NotifyOledEvaluatedTextChanges();
        partial void OnOledShowGpuPowerChanged(bool value) => NotifyOledEvaluatedTextChanges();
        partial void OnOledShowGpuVramChanged(bool value) => NotifyOledEvaluatedTextChanges();
        partial void OnOledShowGpuFanChanged(bool value) => NotifyOledEvaluatedTextChanges();

        partial void OnOledShowSmartFanRpmChanged(bool value) => NotifyOledEvaluatedTextChanges();
        partial void OnOledShowSmartFanPwmChanged(bool value) => NotifyOledEvaluatedTextChanges();
        partial void OnOledShowRamUsageChanged(bool value) => NotifyOledEvaluatedTextChanges();

        partial void OnOledRow1WidgetChanged(string value) => NotifyOledEvaluatedTextChanges();
        partial void OnOledRow2WidgetChanged(string value) => NotifyOledEvaluatedTextChanges();
        partial void OnOledRow3WidgetChanged(string value) => NotifyOledEvaluatedTextChanges();
        partial void OnOledRow4WidgetChanged(string value) => NotifyOledEvaluatedTextChanges();

        partial void OnIsCustomOled1EnabledChanged(bool value)
        {
            if (IsConnected && ActiveConnectionType == "USB_SERIAL")
            {
                _serialService.SetCustomOledMode(1, value);
                if (value) SendCustomOled1Frame();
            }
        }

        partial void OnIsCustomOled2EnabledChanged(bool value)
        {
            if (IsConnected && ActiveConnectionType == "USB_SERIAL")
            {
                _serialService.SetCustomOledMode(2, value);
                if (value) SendCustomOled2Frame();
            }
        }

        [RelayCommand]
        public void SelectOledScreenTab(object? parameter)
        {
            if (parameter != null && int.TryParse(parameter.ToString(), out int tabIndex))
            {
                ActiveOledScreenTab = tabIndex;
            }
        }

        [RelayCommand]
        public void SetOledRowCount(object? parameter)
        {
            if (parameter != null && int.TryParse(parameter.ToString(), out int rows))
            {
                OledRowCount = rows;
                StatusMessage = $"Đã thay đổi cấu trúc layout màn hình thành {rows} dòng.";
            }
        }

        [RelayCommand]
        public void AssignWidgetToRow(object? parameter)
        {
            if (parameter is string paramStr && paramStr.Contains(':'))
            {
                var parts = paramStr.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int row))
                {
                    string widgetKey = parts[1];
                    switch (row)
                    {
                        case 1: OledRow1Widget = widgetKey; break;
                        case 2: OledRow2Widget = widgetKey; break;
                        case 3: OledRow3Widget = widgetKey; break;
                        case 4: OledRow4Widget = widgetKey; break;
                    }
                    StatusMessage = $"Đã gán widget '{widgetKey}' vào Dòng {row}.";
                    SendCurrentOledFrameToEsp32();
                }
            }
        }

        [RelayCommand]
        public void SendCurrentOledFrameToEsp32()
        {
            if (IsConnected && ActiveConnectionType == "USB_SERIAL")
            {
                string hex = _oledCanvasService.GenerateDynamicOledCanvas(
                    EvaluatedRow1Text, EvaluatedRow2Text, EvaluatedRow3Text, EvaluatedRow4Text,
                    OledRowCount, OledShowTopDivider, OledShowBottomDivider, OledShowPwmBar, FanPwm
                );
                int targetDisp = ActiveOledScreenTab == 0 ? 1 : 2;
                _serialService.SendOledBitmap(targetDisp, hex);
                StatusMessage = $"⚡ Đã render & nạp khung hình Hex lên Màn hình OLED {targetDisp} thành công!";
            }
        }

        [RelayCommand]
        public void SendCustomOled1Frame()
        {
            if (IsConnected && ActiveConnectionType == "USB_SERIAL")
            {
                string hex = _oledCanvasService.GenerateDynamicOledCanvas(
                    EvaluatedRow1Text, EvaluatedRow2Text, EvaluatedRow3Text, EvaluatedRow4Text,
                    OledRowCount, OledShowTopDivider, OledShowBottomDivider, OledShowPwmBar, FanPwm
                );
                _serialService.SendOledBitmap(1, hex);
            }
        }

        [RelayCommand]
        public void SendCustomOled2Frame()
        {
            if (IsConnected && ActiveConnectionType == "USB_SERIAL")
            {
                string hex = _oledCanvasService.GenerateDynamicOledCanvas(
                    EvaluatedRow1Text, EvaluatedRow2Text, EvaluatedRow3Text, EvaluatedRow4Text,
                    OledRowCount, OledShowTopDivider, OledShowBottomDivider, OledShowPwmBar, FanPwm
                );
                _serialService.SendOledBitmap(2, hex);
            }
        }
    }
}
