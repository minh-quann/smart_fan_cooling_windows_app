using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SmartFanCooling.ViewModels
{
    /// <summary>
    /// Partial class for OLED Studio configurator — lightweight config-based layout system.
    /// Instead of rendering bitmaps on PC and sending heavy hex data, this sends a compact
    /// JSON config to ESP32 which renders text locally using its own Adafruit GFX library.
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

        public bool IsRowCount2 => OledRowCount == 2;
        public bool IsRowCount3 => OledRowCount == 3;
        public bool IsRowCount4 => OledRowCount == 4;

        public bool IsRow3Visible => OledRowCount >= 3;
        public bool IsRow4Visible => OledRowCount >= 4;

        partial void OnOledRowCountChanged(int value)
        {
            OnPropertyChanged(nameof(IsRowCount2));
            OnPropertyChanged(nameof(IsRowCount3));
            OnPropertyChanged(nameof(IsRowCount4));
            OnPropertyChanged(nameof(IsRow3Visible));
            OnPropertyChanged(nameof(IsRow4Visible));
            NotifyOledEvaluatedTextChanges();
        }

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

        // Row Widget Selections — maps to firmware OledWidget enum values
        // 0=HEADER_TITLE, 1=CPU_TELEMETRY, 2=GPU_TELEMETRY, 3=FAN_TELEMETRY,
        // 4=PWM_PCT, 5=RAM_TELEMETRY, 6=POWER, 7=CLOCK
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
                "TIME_TELEMETRY" => $"TIME: {DateTime.Now:HH:mm}",
                _ => fallbackText
            };
        }

        private string BuildCpuLineText()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var parts = new List<string>();
            if (OledShowCpuUsage) parts.Add($"{CpuUsage.ToString("F0", inv)}%");
            if (OledShowCpuTemp) parts.Add($"{CpuTemp.ToString("F0", inv)}C");
            if (OledShowCpuClock) parts.Add($"{CpuMaxClockGHz.ToString("0.0", inv)}G");
            if (OledShowCpuPower) parts.Add($"{CpuPowerW.ToString("F0", inv)}W");
            if (OledShowCpuFan && CpuFanRpm > 0) parts.Add($"{CpuFanRpm}RPM");
            string sep = parts.Count >= 4 ? " " : " | ";
            return parts.Count > 0 ? "CPU:" + string.Join(sep, parts) : "CPU: --";
        }

        private string BuildGpuLineText()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var parts = new List<string>();
            if (OledShowGpuUsage) parts.Add($"{GpuUsage.ToString("F0", inv)}%");
            if (OledShowGpuTemp) parts.Add($"{GpuTemp.ToString("F0", inv)}C");
            if (OledShowGpuClock) parts.Add(GpuClockMHz >= 1000 ? $"{(GpuClockMHz / 1000.0).ToString("0.1", inv)}G" : $"{GpuClockMHz.ToString("F0", inv)}M");
            if (OledShowGpuPower) parts.Add($"{GpuPowerW.ToString("F0", inv)}W");
            if (OledShowGpuVram) parts.Add($"{GpuVramUsedGB.ToString("F1", inv)}GB");
            if (OledShowGpuFan && GpuFanRpm > 0) parts.Add($"{GpuFanRpm}RPM");
            string sep = parts.Count >= 4 ? " " : " | ";
            return parts.Count > 0 ? "GPU:" + string.Join(sep, parts) : "GPU: --";
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

        // Firmware Default Display Layout Properties (Matching firmware oled_display.cpp for OLED 1 1.3")
        public string Oled1DefaultHeader => "LLANO SMART FAN";
        public string Oled1DefaultFanStatus => FanPwm > 0 ? $"{FanPwm}%" : "OFF";
        public string Oled1DefaultRpmText => $"{FanRpm}";
        public string Oled1DefaultLedModeText => $"LED: {GetLedModeName(SelectedLedMode)}";

        // New OLED 1.3" Layout Properties
        public string Oled1DefaultFanLine => FanPwm > 0 ? $"{FanRpm} RPM | PWM: {FanPwm}%" : "FAN: OFF | 0 RPM";
        public string Oled1DefaultCpuText => $"CPU:{CpuUsage:F0}% {(CpuTemp > 0 ? $"{CpuTemp:F0}C" : "--C")} {CpuMaxClockGHz:0.0}G {(CpuPowerW > 0 ? $"{CpuPowerW:F0}W" : "")}".TrimEnd();
        public string Oled1DefaultGpuText => $"GPU:{GpuUsage:F0}% {(GpuTemp > 0 ? $"{GpuTemp:F0}C" : "--C")} {(GpuClockMHz >= 1000 ? $"{GpuClockMHz / 1000.0:0.1}G" : $"{GpuClockMHz:F0}M")} {(GpuPowerW > 0 ? $"{GpuPowerW:F0}W" : "")}".TrimEnd();
        public string Oled1DefaultRamText => $"RAM: {(RamTotalGB > 0 ? (RamUsedGB / RamTotalGB * 100) : 0):F0}% | {RamUsedGB:F1}/{RamTotalGB:F1}GB";
        public string Oled1DefaultTimeText => $"TIME: {DateTime.Now:HH:mm} | LED: {GetLedModeName(SelectedLedMode)}";

        public string Oled2DefaultRpmHeader => $"{((FanRpm > 0) ? (((FanRpm + 49) / 100) * 100) : 0)} RPM";
        public string Oled2DefaultCpuText => $"CPU: {CpuFanRpm} | {(CpuTemp > 0 ? $"{CpuTemp:F0}C" : "--C")}";
        public string Oled2DefaultGpuText => $"GPU: {GpuFanRpm} | {(GpuTemp > 0 ? $"{GpuTemp:F0}C" : "--C")}";
        public string Oled2DefaultPwmText => $"PWM: {FanPwm}%";
        public string Oled2DefaultTransportText => IsConnected ? (ActiveConnectionType == "WIFI" ? "WiFi" : (ActiveConnectionType == "BLE" ? "BLE" : "USB")) : "USB";

        public bool IsOled1TabActive => ActiveOledScreenTab == 0;
        public bool IsOled2TabActive => ActiveOledScreenTab == 1;

        public bool IsCurrentScreenCustomEnabled => ActiveOledScreenTab == 0 ? IsCustomOled1Enabled : IsCustomOled2Enabled;
        public bool IsCurrentScreenDefaultEnabled => !IsCurrentScreenCustomEnabled;

        private string GetLedModeName(int mode)
        {
            return mode switch
            {
                0 => "OFF",
                1 => "STATIC",
                2 => "RAINBOW",
                3 => "BREATH",
                4 => "SYNC",
                5 => "WAVE",
                6 => "FIRE",
                7 => "COMET",
                8 => "PULSE",
                9 => "DUAL SPIN",
                _ => "STATIC"
            };
        }

        /// <summary>
        /// Convert widget string key to firmware OledWidget enum integer value
        /// </summary>
        private int WidgetKeyToEnumValue(string widgetKey)
        {
            return widgetKey switch
            {
                "HEADER_TITLE" => 0,
                "CPU_TELEMETRY" => 1,
                "GPU_TELEMETRY" => 2,
                "FAN_TELEMETRY" => 3,
                "PWM_PCT" => 4,
                "RAM_TELEMETRY" => 5,
                "POWER_TELEMETRY" => 6,
                "CLOCK_TELEMETRY" => 7,
                "TIME_TELEMETRY" => 8,
                _ => 0
            };
        }

        private void NotifyOledEvaluatedTextChanges()
        {
            OnPropertyChanged(nameof(EvaluatedRow1Text));
            OnPropertyChanged(nameof(EvaluatedRow2Text));
            OnPropertyChanged(nameof(EvaluatedRow3Text));
            OnPropertyChanged(nameof(EvaluatedRow4Text));
            OnPropertyChanged(nameof(Oled1DefaultHeader));
            OnPropertyChanged(nameof(Oled1DefaultFanStatus));
            OnPropertyChanged(nameof(Oled1DefaultRpmText));
            OnPropertyChanged(nameof(Oled1DefaultLedModeText));
            OnPropertyChanged(nameof(Oled1DefaultFanLine));
            OnPropertyChanged(nameof(Oled1DefaultCpuText));
            OnPropertyChanged(nameof(Oled1DefaultGpuText));
            OnPropertyChanged(nameof(Oled1DefaultRamText));
            OnPropertyChanged(nameof(Oled1DefaultTimeText));
            OnPropertyChanged(nameof(Oled2DefaultRpmHeader));
            OnPropertyChanged(nameof(Oled2DefaultCpuText));
            OnPropertyChanged(nameof(Oled2DefaultGpuText));
            OnPropertyChanged(nameof(Oled2DefaultPwmText));
            OnPropertyChanged(nameof(Oled2DefaultTransportText));
            OnPropertyChanged(nameof(IsOled1TabActive));
            OnPropertyChanged(nameof(IsOled2TabActive));
            OnPropertyChanged(nameof(IsCurrentScreenCustomEnabled));
            OnPropertyChanged(nameof(IsCurrentScreenDefaultEnabled));
        }

        /// <summary>
        /// Send lightweight layout config to ESP32 firmware — firmware renders text locally.
        /// This replaces the heavy bitmap-based approach (no more 2KB hex per frame).
        /// </summary>
        private void SendOledConfigToEsp32()
        {
            if (!IsConnected || ActiveConnectionType != "USB_SERIAL") return;

            int targetDisp = ActiveOledScreenTab == 0 ? 1 : 2;
            _serialService.SendOledConfig(
                targetDisp,
                OledRowCount,
                WidgetKeyToEnumValue(OledRow1Widget),
                WidgetKeyToEnumValue(OledRow2Widget),
                WidgetKeyToEnumValue(OledRow3Widget),
                WidgetKeyToEnumValue(OledRow4Widget),
                OledShowTopDivider,
                OledShowBottomDivider,
                OledShowPwmBar,
                Oled1CustomTitle
            );
        }

        partial void OnOledShowTopDividerChanged(bool value) { NotifyOledEvaluatedTextChanges(); SendOledConfigToEsp32(); }
        partial void OnOledShowBottomDividerChanged(bool value) { NotifyOledEvaluatedTextChanges(); SendOledConfigToEsp32(); }
        partial void OnOledShowPwmBarChanged(bool value) { NotifyOledEvaluatedTextChanges(); SendOledConfigToEsp32(); }

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

        partial void OnOledRow1WidgetChanged(string value) { NotifyOledEvaluatedTextChanges(); SendOledConfigToEsp32(); }
        partial void OnOledRow2WidgetChanged(string value) { NotifyOledEvaluatedTextChanges(); SendOledConfigToEsp32(); }
        partial void OnOledRow3WidgetChanged(string value) { NotifyOledEvaluatedTextChanges(); SendOledConfigToEsp32(); }
        partial void OnOledRow4WidgetChanged(string value) { NotifyOledEvaluatedTextChanges(); SendOledConfigToEsp32(); }

        partial void OnOled1CustomTitleChanged(string value) { NotifyOledEvaluatedTextChanges(); SendOledConfigToEsp32(); }
        partial void OnOled1CustomMainStatChanged(string value) => NotifyOledEvaluatedTextChanges();
        partial void OnOled1CustomSubStatChanged(string value) => NotifyOledEvaluatedTextChanges();
        partial void OnOled2CustomHeaderChanged(string value) => NotifyOledEvaluatedTextChanges();
        partial void OnOled2CustomCpuTextChanged(string value) => NotifyOledEvaluatedTextChanges();
        partial void OnOled2CustomGpuTextChanged(string value) => NotifyOledEvaluatedTextChanges();
        partial void OnOled2CustomFooterChanged(string value) => NotifyOledEvaluatedTextChanges();

        partial void OnIsCustomOled1EnabledChanged(bool value)
        {
            if (IsConnected && ActiveConnectionType == "USB_SERIAL")
            {
                if (value)
                {
                    // Send config to ESP32 — firmware renders locally
                    SendOledConfigToEsp32();
                }
                else
                {
                    // Reset to firmware default layout
                    _serialService.SendOledConfigReset(1);
                }
            }
            NotifyOledEvaluatedTextChanges();
        }

        partial void OnIsCustomOled2EnabledChanged(bool value)
        {
            if (IsConnected && ActiveConnectionType == "USB_SERIAL")
            {
                if (value)
                {
                    SendOledConfigToEsp32();
                }
                else
                {
                    _serialService.SendOledConfigReset(2);
                }
            }
            NotifyOledEvaluatedTextChanges();
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
                SendOledConfigToEsp32();
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
                }
            }
        }

        [RelayCommand]
        public void SendCurrentOledFrameToEsp32()
        {
            if (IsConnected && ActiveConnectionType == "USB_SERIAL")
            {
                int targetDisp = ActiveOledScreenTab == 0 ? 1 : 2;
                if (targetDisp == 1) IsCustomOled1Enabled = true;
                else IsCustomOled2Enabled = true;

                // Send config — firmware will render locally in real-time
                SendOledConfigToEsp32();
                StatusMessage = $"⚡ Đã gửi cấu hình layout lên Màn hình OLED {targetDisp} — firmware tự render!";
            }
        }

        [RelayCommand]
        public void SendCustomOled1Frame()
        {
            if (IsConnected && ActiveConnectionType == "USB_SERIAL")
            {
                // Send lightweight config instead of bitmap
                ActiveOledScreenTab = 0;
                IsCustomOled1Enabled = true;
                SendOledConfigToEsp32();
            }
        }

        [RelayCommand]
        public void SendCustomOled2Frame()
        {
            if (IsConnected && ActiveConnectionType == "USB_SERIAL")
            {
                ActiveOledScreenTab = 1;
                IsCustomOled2Enabled = true;
                SendOledConfigToEsp32();
            }
        }
    }
}
