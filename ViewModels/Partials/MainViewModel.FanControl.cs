using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartFanCooling.Models;
using SmartFanCooling.Styles;

namespace SmartFanCooling.ViewModels
{
    /// <summary>
    /// Partial class for Fan Control, PWM, Target RPM, Fan Curve points, Profiles, and Presets.
    /// </summary>
    public partial class MainViewModel
    {
        // Llano Smart Fan Speed & PWM (Default = 0 when disconnected, synced from real hardware when online)
        [ObservableProperty] private int _fanPwm = 0;
        [ObservableProperty] private int _targetRpm = 0;
        [ObservableProperty] private int _fanRpm = 0;
        [ObservableProperty] private bool _isFanStateOn = false;
        private bool _isSyncingFromHardware = false;

        public string FanAirflowCfmText => FanRpm > 0 ? $"{ (FanRpm * 0.0243f):F1} CFM" : "0.0 CFM";

        public string FormattedFanRpmDisplay => FanRpm > 0 ? FanRpm.ToString() : "0000";

        public string FanRpmTextColor => FanRpm > 0 ? AppColors.Gray50Hex : AppColors.Gray500Hex;

        public string FanStatusTitle
        {
            get
            {
                if (FanRpm <= 0) return "OFF / DỪNG QUẠT";
                if (FanPwm <= 35) return "SILENT / ÊM ÁI";
                if (FanPwm <= 65) return "BALANCED / CÂN BẰNG";
                if (FanPwm <= 85) return "PERFORMANCE / CAO";
                return "TURBO EXTREME";
            }
        }

        public string FanStatusColor => AppColors.GetFanStatusColorHex(FanRpm, FanPwm);

        partial void OnFanRpmChanged(int value)
        {
            NotifyFanStatsChanged();
        }

        partial void OnFanPwmChanged(int value)
        {
            NotifyFanStatsChanged();
            if (!_isSyncingFromHardware)
            {
                int calculatedRpm = value > 0 ? (int)(Math.Round((value * 28.0) / 100.0) * 100) : 0;
                _isSyncingFromHardware = true;
                if (TargetRpm != calculatedRpm) TargetRpm = calculatedRpm;
                if (FanRpm != calculatedRpm) FanRpm = calculatedRpm;
                _isSyncingFromHardware = false;

                if (IsConnected && ActiveConnectionType == "USB_SERIAL")
                {
                    _serialService.SetFanSpeed(value);
                }
            }
        }

        private void NotifyFanStatsChanged()
        {
            OnPropertyChanged(nameof(FanAirflowCfmText));
            OnPropertyChanged(nameof(FanStatusTitle));
            OnPropertyChanged(nameof(FanStatusColor));
            OnPropertyChanged(nameof(FormattedFanRpmDisplay));
            OnPropertyChanged(nameof(FanRpmTextColor));
        }

        partial void OnTargetRpmChanged(int value)
        {
            int rounded = value > 0 ? (int)(Math.Round(value / 100.0) * 100) : 0;
            if (rounded != value)
            {
                TargetRpm = rounded;
                return;
            }

            if (!_isSyncingFromHardware)
            {
                int pct = value > 0 ? Math.Clamp((int)Math.Round(value / 28.0), 0, 100) : 0;
                _isSyncingFromHardware = true;
                FanPwm = pct;
                FanRpm = value;
                _isSyncingFromHardware = false;

                if (IsConnected && ActiveConnectionType == "USB_SERIAL")
                {
                    _serialService.SetTargetRpm(value);
                    _serialService.SetFanSpeed(pct);
                }
            }
        }

        // Customizable Quick RPM Presets
        public ObservableCollection<RpmPreset> QuickRpmPresets { get; } = new();
        [ObservableProperty] private int _newPresetRpm = 1400;
        [ObservableProperty] private string _newPresetLabel = "1400";

        // Profiles
        public ObservableCollection<FanProfile> Profiles { get; } = new();

        [ObservableProperty]
        private FanProfile _activeProfile = null!;

        // Fan Curve Points (°C -> PWM %)
        [ObservableProperty] private int _curveP30 = 20;
        [ObservableProperty] private int _curveP40 = 30;
        [ObservableProperty] private int _curveP50 = 45;
        [ObservableProperty] private int _curveP60 = 60;
        [ObservableProperty] private int _curveP70 = 75;
        [ObservableProperty] private int _curveP80 = 90;
        [ObservableProperty] private int _curveP90 = 100;

        public string FanCurveLinePoints => $"{30},{190 - (CurveP30 * 1.8)} {100},{190 - (CurveP40 * 1.8)} {170},{190 - (CurveP50 * 1.8)} {240},{190 - (CurveP60 * 1.8)} {310},{190 - (CurveP70 * 1.8)} {380},{190 - (CurveP80 * 1.8)} {450},{190 - (CurveP90 * 1.8)}";
        public string FanCurveFillPoints => $"30,190 {FanCurveLinePoints} 450,190";

        public double Node30_Y => 190 - (CurveP30 * 1.8) - 5;
        public double Node40_Y => 190 - (CurveP40 * 1.8) - 5;
        public double Node50_Y => 190 - (CurveP50 * 1.8) - 5;
        public double Node60_Y => 190 - (CurveP60 * 1.8) - 5;
        public double Node70_Y => 190 - (CurveP70 * 1.8) - 5;
        public double Node80_Y => 190 - (CurveP80 * 1.8) - 5;
        public double Node90_Y => 190 - (CurveP90 * 1.8) - 5;

        private void NotifyFanCurveChanged()
        {
            if (ActiveProfile != null && ActiveProfile.CurvePoints != null)
            {
                ActiveProfile.CurvePoints[30] = CurveP30;
                ActiveProfile.CurvePoints[40] = CurveP40;
                ActiveProfile.CurvePoints[50] = CurveP50;
                ActiveProfile.CurvePoints[60] = CurveP60;
                ActiveProfile.CurvePoints[70] = CurveP70;
                ActiveProfile.CurvePoints[80] = CurveP80;
                ActiveProfile.CurvePoints[90] = CurveP90;
            }

            OnPropertyChanged(nameof(FanCurveLinePoints));
            OnPropertyChanged(nameof(FanCurveFillPoints));
            OnPropertyChanged(nameof(Node30_Y));
            OnPropertyChanged(nameof(Node40_Y));
            OnPropertyChanged(nameof(Node50_Y));
            OnPropertyChanged(nameof(Node60_Y));
            OnPropertyChanged(nameof(Node70_Y));
            OnPropertyChanged(nameof(Node80_Y));
            OnPropertyChanged(nameof(Node90_Y));
        }

        partial void OnCurveP30Changed(int value) => NotifyFanCurveChanged();
        partial void OnCurveP40Changed(int value) => NotifyFanCurveChanged();
        partial void OnCurveP50Changed(int value) => NotifyFanCurveChanged();
        partial void OnCurveP60Changed(int value) => NotifyFanCurveChanged();
        partial void OnCurveP70Changed(int value) => NotifyFanCurveChanged();
        partial void OnCurveP80Changed(int value) => NotifyFanCurveChanged();
        partial void OnCurveP90Changed(int value) => NotifyFanCurveChanged();

        [ObservableProperty] private bool _isAutoMode = false;
        [ObservableProperty] private string _selectedFanCurve = "Balanced";

        /// <summary>
        /// Controls whether manual fan speed slider and preset controls are enabled.
        /// Disabled when app is disconnected from ESP32 or when Auto Fan Curve Mode is enabled.
        /// </summary>
        public bool CanControlFanSpeed => IsConnected && !IsAutoMode;

        partial void OnIsAutoModeChanged(bool value)
        {
            OnPropertyChanged(nameof(CanControlFanSpeed));
        }

        private void InitializeDefaultProfiles()
        {
            Profiles.Add(new FanProfile
            {
                Name = "Quiet",
                Description = "Chế độ yên tĩnh cho công việc văn phòng",
                ColorHex = AppColors.Emerald500Hex,
                IconGlyph = "\uE706",
                MaxFanPwm = 50,
                CurvePoints = new Dictionary<int, int> { { 30, 15 }, { 40, 25 }, { 50, 35 }, { 60, 45 }, { 70, 60 }, { 80, 75 }, { 90, 85 } }
            });

            Profiles.Add(new FanProfile
            {
                Name = "Balanced",
                Description = "Chế độ cân bằng giữa độ ồn và hiệu năng tản nhiệt",
                ColorHex = AppColors.Cyan500Hex,
                IconGlyph = "\uE9CA",
                MaxFanPwm = 75,
                CurvePoints = new Dictionary<int, int> { { 30, 20 }, { 40, 30 }, { 50, 45 }, { 60, 60 }, { 70, 75 }, { 80, 90 }, { 90, 100 } }
            });

            Profiles.Add(new FanProfile
            {
                Name = "Turbo",
                Description = "Chế độ tối đa công suất quạt khi chơi game nặng / Render",
                ColorHex = AppColors.Orange500Hex,
                IconGlyph = "\uEBA3",
                MaxFanPwm = 100,
                CurvePoints = new Dictionary<int, int> { { 30, 40 }, { 40, 60 }, { 50, 75 }, { 60, 85 }, { 70, 95 }, { 80, 100 }, { 90, 100 } }
            });

            ActiveProfile = Profiles[1]; // Balanced
            LoadCurveFromProfile(ActiveProfile);
        }

        private void LoadCurveFromProfile(FanProfile profile)
        {
            if (profile.CurvePoints.TryGetValue(30, out var p30)) CurveP30 = p30;
            if (profile.CurvePoints.TryGetValue(40, out var p40)) CurveP40 = p40;
            if (profile.CurvePoints.TryGetValue(50, out var p50)) CurveP50 = p50;
            if (profile.CurvePoints.TryGetValue(60, out var p60)) CurveP60 = p60;
            if (profile.CurvePoints.TryGetValue(70, out var p70)) CurveP70 = p70;
            if (profile.CurvePoints.TryGetValue(80, out var p80)) CurveP80 = p80;
            if (profile.CurvePoints.TryGetValue(90, out var p90)) CurveP90 = p90;
        }

        private int CalculatePwmFromCurve(float temp)
        {
            if (temp <= 30) return CurveP30;
            if (temp <= 40) return CurveP30 + (int)((temp - 30) / 10.0f * (CurveP40 - CurveP30));
            if (temp <= 50) return CurveP40 + (int)((temp - 40) / 10.0f * (CurveP50 - CurveP40));
            if (temp <= 60) return CurveP50 + (int)((temp - 50) / 10.0f * (CurveP60 - CurveP50));
            if (temp <= 70) return CurveP60 + (int)((temp - 60) / 10.0f * (CurveP70 - CurveP60));
            if (temp <= 80) return CurveP70 + (int)((temp - 70) / 10.0f * (CurveP80 - CurveP70));
            if (temp <= 90) return CurveP80 + (int)((temp - 80) / 10.0f * (CurveP90 - CurveP80));
            return CurveP90;
        }

        [RelayCommand]
        public void SelectProfile(object? parameter)
        {
            if (parameter is FanProfile profile)
            {
                ActiveProfile = profile;
            }
            else if (parameter is string name)
            {
                var found = Profiles.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (found != null) ActiveProfile = found;
            }
            if (ActiveProfile != null)
            {
                SelectedFanCurve = ActiveProfile.Name;
                LoadCurveFromProfile(ActiveProfile);
                StatusMessage = $"Đã kích hoạt Profile: {ActiveProfile.Name}";
            }
        }

        [RelayCommand]
        public void AddNewProfile()
        {
            int nextNum = Profiles.Count + 1;
            var newProfile = new FanProfile
            {
                Name = $"Custom {nextNum}",
                Description = "Đường cong tùy chỉnh cá nhân",
                ColorHex = AppColors.Violet500Hex,
                IconGlyph = "\uE9CA",
                MaxFanPwm = 100,
                CurvePoints = new Dictionary<int, int>
                {
                    { 30, CurveP30 },
                    { 40, CurveP40 },
                    { 50, CurveP50 },
                    { 60, CurveP60 },
                    { 70, CurveP70 },
                    { 80, CurveP80 },
                    { 90, CurveP90 }
                }
            };

            Profiles.Add(newProfile);
            ActiveProfile = newProfile;
            LoadCurveFromProfile(newProfile);
            StatusMessage = $"Đã tạo Profile mới: {newProfile.Name}";
        }

        [RelayCommand]
        public void DeleteActiveProfile()
        {
            if (Profiles.Count <= 1)
            {
                StatusMessage = "⚠️ Không thể xóa! Hệ thống phải duy trì ít nhất 1 Profile.";
                return;
            }

            if (ActiveProfile != null && Profiles.Contains(ActiveProfile))
            {
                string deletedName = ActiveProfile.Name;
                Profiles.Remove(ActiveProfile);
                ActiveProfile = Profiles[0];
                LoadCurveFromProfile(ActiveProfile);
                StatusMessage = $"Đã xóa Profile: {deletedName}";
            }
        }

        [RelayCommand]
        public void SetQuickFanPreset(object? parameter)
        {
            if (parameter != null && int.TryParse(parameter.ToString(), out int targetRpm))
            {
                IsAutoMode = false;
                TargetRpm = targetRpm;
                IsFanStateOn = targetRpm > 0;
                StatusMessage = targetRpm > 0 ? $"Đã đặt tốc độ quạt mục tiêu: {targetRpm} RPM" : "Đã tắt quạt thủ công";
            }
        }

        [RelayCommand]
        public void SelectRpmPreset(RpmPreset? preset)
        {
            if (preset != null)
            {
                IsAutoMode = false;
                TargetRpm = preset.Rpm;
                IsFanStateOn = preset.Rpm > 0;
                StatusMessage = preset.Rpm > 0 ? $"Đã chọn mốc tốc độ: {preset.Label} ({preset.Rpm} RPM)" : "Đã tắt quạt";
            }
        }

        [RelayCommand]
        public void AddCustomRpmPreset()
        {
            int rpm = Math.Clamp((int)(Math.Round(NewPresetRpm / 100.0) * 100), 0, 2800);
            string label = rpm == 2800 ? "2800 Max" : (rpm == 0 ? "Tắt" : $"{rpm}");

            var existing = QuickRpmPresets.FirstOrDefault(p => p.Rpm == rpm);
            if (existing != null)
            {
                existing.Label = label;
                StatusMessage = $"Đã cập nhật mốc tốc độ: {label}";
            }
            else
            {
                QuickRpmPresets.Add(new RpmPreset(label, rpm));
                var sorted = QuickRpmPresets.OrderBy(p => p.Rpm).ToList();
                QuickRpmPresets.Clear();
                foreach (var item in sorted)
                {
                    QuickRpmPresets.Add(item);
                }
                StatusMessage = $"Đã thêm mốc tốc độ mới: {label}";
            }
            SaveRpmPresets();
        }

        [RelayCommand]
        public void DeleteRpmPreset(RpmPreset? preset)
        {
            if (preset != null && QuickRpmPresets.Contains(preset))
            {
                string name = preset.Label;
                QuickRpmPresets.Remove(preset);
                StatusMessage = $"Đã xóa mốc tốc độ: {name}";
                SaveRpmPresets();
            }
        }

        [RelayCommand]
        public void ResetDefaultRpmPresets()
        {
            InitializeDefaultRpmPresets();
            SaveRpmPresets();
            StatusMessage = "Đã khôi phục các mốc tốc độ mặc định";
        }

        private void InitializeDefaultRpmPresets()
        {
            QuickRpmPresets.Clear();
            QuickRpmPresets.Add(new RpmPreset("Tắt", 0));
            QuickRpmPresets.Add(new RpmPreset("800", 800));
            QuickRpmPresets.Add(new RpmPreset("1200", 1200));
            QuickRpmPresets.Add(new RpmPreset("1600", 1600));
            QuickRpmPresets.Add(new RpmPreset("2000", 2000));
            QuickRpmPresets.Add(new RpmPreset("2800 Max", 2800));
        }

        private void SaveRpmPresets()
        {
            try
            {
                string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmartFanCooling");
                System.IO.Directory.CreateDirectory(dir);
                string file = System.IO.Path.Combine(dir, "rpm_presets.json");
                string json = System.Text.Json.JsonSerializer.Serialize(QuickRpmPresets);
                System.IO.File.WriteAllText(file, json);
            }
            catch { }
        }

        private void LoadRpmPresets()
        {
            try
            {
                string file = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmartFanCooling", "rpm_presets.json");
                if (System.IO.File.Exists(file))
                {
                    string json = System.IO.File.ReadAllText(file);
                    var items = System.Text.Json.JsonSerializer.Deserialize<List<RpmPreset>>(json);
                    if (items != null && items.Count > 0)
                    {
                        QuickRpmPresets.Clear();
                        foreach (var item in items) QuickRpmPresets.Add(item);
                        return;
                    }
                }
            }
            catch { }
            InitializeDefaultRpmPresets();
        }
    }
}
