# 🚀 DỰ ÁN SMART FAN COOLING - WINUI 3 (.NET 10 NATIVE WINDOWS)

> Tài liệu tổng hợp toàn bộ mã nguồn C# (.NET 10) & WinUI 3 XAML để phát triển ứng dụng Native Windows điều khiển quạt tản nhiệt thông minh.

---

## 📂 Cấu Trúc Thư Mục Dự Án

```text
smart_fan_cooling_windows_app/
├── smart_fan_cooling_windows_app.csproj    # File cấu hình dự án WinUI 3 (.NET 10)
├── app.manifest                            # Cấu hình Quyền Administrator & DPI Awareness
├── App.xaml / App.xaml.cs                  # Entry Point ứng dụng WinUI 3
├── MainWindow.xaml / MainWindow.xaml.cs    # Giao diện chính (Fluent Design + Mica)
├── Services/
│   ├── HardwareMonitorService.cs           # Đọc nhiệt độ CPU/GPU via LibreHardwareMonitor
│   ├── SerialFanService.cs                 # Giao tiếp USB Serial (Cổng COM)
│   ├── BleFanService.cs                    # Giao tiếp Bluetooth Low Energy (BLE)
│   └── AutoFanCurveService.cs              # Tự động tính PWM theo nhiệt độ
├── ViewModels/
│   └── MainViewModel.cs                    # Quản lý State bằng CommunityToolkit.Mvvm
├── firmware/                               # Đã copy toàn bộ mã nguồn Firmware ESP32-S3
├── WIRING_GUIDE.md                         # Hướng dẫn đấu nối phần cứng 31 dây
└── wiring_diagram_*.html                   # Sơ đồ mạch tương tác
```

---

## 📄 1. File Cấu Hình Dự Án (`smart_fan_cooling_windows_app.csproj`)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
    <RootNamespace>SmartFanCooling</RootNamespace>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <Platforms>x64;ARM64</Platforms>
    <RuntimeIdentifiers>win-x64;win-arm64</RuntimeIdentifiers>
    <UseWinUI>true</UseWinUI>
    <EnableMsixTooling>true</EnableMsixTooling>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="1.5.240311000" />
    <PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="10.0.26100.1" />
    <PackageReference Include="LibreHardwareMonitorLib" Version="0.9.3" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
    <PackageReference Include="System.IO.Ports" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <Manifest Include="$(ApplicationManifest)" />
  </ItemGroup>
</Project>
```

---

## 💻 2. Service Đọc Cảm Biến Phần Cứng (`Services/HardwareMonitorService.cs`)

```csharp
using System;
using LibreHardwareMonitor.Hardware;

namespace SmartFanCooling.Services
{
    /// <summary>
    /// Reads CPU/GPU temperatures and System sensor telemetry using LibreHardwareMonitorLib.
    /// </summary>
    public class HardwareMonitorService : IDisposable
    {
        private readonly Computer _computer;

        public float CpuTemperature { get; private set; }
        public float GpuTemperature { get; private set; }

        public HardwareMonitorService()
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true
            };
            _computer.Open();
        }

        public void UpdateSensors()
        {
            foreach (var hardware in _computer.Hardware)
            {
                hardware.Update();
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Temperature)
                    {
                        // Match CPU Package or Core Average temperature
                        if (hardware.HardwareType == HardwareType.Cpu && 
                           (sensor.Name.Contains("Package") || sensor.Name.Contains("Core Average") || sensor.Name.Contains("CPU Core")))
                        {
                            CpuTemperature = sensor.Value ?? CpuTemperature;
                        }
                        // Match GPU Core temperature
                        else if ((hardware.HardwareType == HardwareType.GpuNvidia || hardware.HardwareType == HardwareType.GpuAmd || hardware.HardwareType == HardwareType.GpuIntel) && 
                                  sensor.Name.Contains("Core"))
                        {
                            GpuTemperature = sensor.Value ?? GpuTemperature;
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            _computer.Close();
        }
    }
}
```

---

## 🔌 3. Service Giao Tiếp USB Serial (`Services/SerialFanService.cs`)

```csharp
using System;
using System.IO.Ports;
using System.Text.Json;

namespace SmartFanCooling.Services
{
    /// <summary>
    /// Manages USB Serial communication with ESP32-S3 over COM ports.
    /// </summary>
    public class SerialFanService
    {
        private SerialPort? _serialPort;

        public event Action<int>? OnRpmReceived;
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
                _serialPort = new SerialPort(portName, baudRate);
                _serialPort.DataReceived += SerialPort_DataReceived;
                _serialPort.Open();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (_serialPort == null || !_serialPort.IsOpen) return;
                string line = _serialPort.ReadLine();
                
                // Parse JSON telemetry from ESP32: {"rpm": 2450, "pwm": 60, "temp": 48.5}
                using var doc = JsonDocument.Parse(line);
                if (doc.RootElement.TryGetProperty("rpm", out var rpmProp))
                {
                    OnRpmReceived?.Invoke(rpmProp.GetInt32());
                }
            }
            catch
            {
                // Ignore parse errors on incomplete lines
            }
        }

        public void SendControl(int pwmPercent, int ledMode, float cpuTemp)
        {
            if (IsConnected)
            {
                var command = new
                {
                    pwm = pwmPercent,
                    led = ledMode,
                    temp = cpuTemp
                };
                string json = JsonSerializer.Serialize(command);
                _serialPort!.WriteLine(json);
            }
        }

        public void Disconnect()
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.DataReceived -= SerialPort_DataReceived;
                _serialPort.Close();
                _serialPort.Dispose();
                _serialPort = null;
            }
        }
    }
}
```

---

## 📈 4. Service Tự Động Tính Tốc Độ Quạt Theo Đường Cong Nhiệt (`Services/AutoFanCurveService.cs`)

```csharp
namespace SmartFanCooling.Services
{
    /// <summary>
    /// Calculates fan PWM percentage based on target temperature and fan curve presets.
    /// </summary>
    public static class AutoFanCurveService
    {
        public static int CalculatePwm(float maxTemp, string mode)
        {
            return mode switch
            {
                "Quiet" => maxTemp switch
                {
                    < 40 => 20,
                    < 60 => 35,
                    < 75 => 55,
                    _ => 75
                },
                "Balanced" => maxTemp switch
                {
                    < 40 => 30,
                    < 60 => 50,
                    < 75 => 75,
                    _ => 90
                },
                "Turbo" => maxTemp switch
                {
                    < 40 => 50,
                    < 60 => 80,
                    _ => 100
                },
                _ => (int)Math.Clamp((maxTemp - 30) * 2, 20, 100) // Custom Dynamic Linear Curve
            };
        }
    }
}
```

---

## 🧠 5. Main ViewModel (`ViewModels/MainViewModel.cs`)

```csharp
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartFanCooling.Services;
using Microsoft.UI.Xaml;

namespace SmartFanCooling.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly HardwareMonitorService _hardwareService;
        private readonly SerialFanService _serialService;
        private readonly DispatcherTimer _timer;

        [ObservableProperty]
        private float _cpuTemp;

        [ObservableProperty]
        private float _gpuTemp;

        [ObservableProperty]
        private int _fanPwm = 50;

        [ObservableProperty]
        private int _fanRpm = 0;

        [ObservableProperty]
        private int _selectedLedMode = 1; // 0: Off, 1: Static, 2: Breathing, 3: Rainbow, 4: Speed Pulse

        [ObservableProperty]
        private bool _isAutoMode = true;

        [ObservableProperty]
        private string _selectedFanCurve = "Balanced";

        [ObservableProperty]
        private string _selectedComPort = "";

        [ObservableProperty]
        private bool _isConnected = false;

        public ObservableCollection<string> AvailableComPorts { get; } = new();

        public MainViewModel()
        {
            _hardwareService = new HardwareMonitorService();
            _serialService = new SerialFanService();
            _serialService.OnRpmReceived += rpm => FanRpm = rpm;

            RefreshComPorts();

            // Hardware polling timer (Every 1 second)
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, object e)
        {
            _hardwareService.UpdateSensors();
            CpuTemp = _hardwareService.CpuTemperature;
            GpuTemp = _hardwareService.GpuTemperature;

            float maxTemp = Math.Max(CpuTemp, GpuTemp);

            if (IsAutoMode)
            {
                FanPwm = AutoFanCurveService.CalculatePwm(maxTemp, SelectedFanCurve);
            }

            if (IsConnected)
            {
                _serialService.SendControl(FanPwm, SelectedLedMode, maxTemp);
            }
        }

        [RelayCommand]
        public void RefreshComPorts()
        {
            AvailableComPorts.Clear();
            foreach (var port in _serialService.GetAvailablePorts())
            {
                AvailableComPorts.Add(port);
            }
            if (AvailableComPorts.Count > 0 && string.IsNullOrEmpty(SelectedComPort))
            {
                SelectedComPort = AvailableComPorts[0];
            }
        }

        [RelayCommand]
        public void ToggleConnection()
        {
            if (IsConnected)
            {
                _serialService.Disconnect();
                IsConnected = false;
            }
            else if (!string.IsNullOrEmpty(SelectedComPort))
            {
                IsConnected = _serialService.Connect(SelectedComPort);
            }
        }
    }
}
```

---

## 🎨 6. Giao Diện WinUI 3 Fluent Design (`MainWindow.xaml`)

```xml
<Window
    x:Class="SmartFanCooling.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Title="Llano Smart Fan Cooling System - WinUI 3 Native">

    <Grid RowDefinitions="Auto, *">
        <!-- Header Title Bar -->
        <Border Grid.Row="0" Padding="16,12" Background="{ThemeResource LayerOnAccentFillColorDefaultBrush}">
            <Grid ColumnDefinitions="Auto, *, Auto">
                <StackPanel Grid.Column="0" Orientation="Horizontal" Spacing="12">
                    <FontIcon Glyph="&#xE9CA;" FontSize="24" Foreground="{ThemeResource SystemAccentColor}"/>
                    <TextBlock Text="LLANO SMART FAN COOLING" FontSize="18" FontWeight="Bold" VerticalAlignment="Center"/>
                </StackPanel>

                <!-- Connection Panel -->
                <StackPanel Grid.Column="2" Orientation="Horizontal" Spacing="8">
                    <ComboBox ItemsSource="{Binding AvailableComPorts}" SelectedItem="{Binding SelectedComPort, Mode=TwoWay}" Width="110"/>
                    <Button Command="{Binding RefreshComPortsCommand}" ToolTipService.ToolTip="Refresh Ports">
                        <FontIcon Glyph="&#xE72C;" FontSize="14"/>
                    </Button>
                    <Button Content="{Binding IsConnected, Converter={StaticResource ConnectTextConverter}}" 
                            Command="{Binding ToggleConnectionCommand}"
                            Style="{ThemeResource AccentButtonStyle}"/>
                </StackPanel>
            </Grid>
        </Border>

        <!-- Main Dashboard View -->
        <ScrollViewer Grid.Row="1" Padding="24">
            <StackPanel Spacing="20">
                
                <!-- Telemetry Cards -->
                <Grid ColumnDefinitions="*, *, *">
                    <!-- CPU Temp Card -->
                    <Border Grid.Column="0" Margin="0,0,8,0" Background="{ThemeResource CardBackgroundFillColorDefaultBrush}" CornerRadius="12" Padding="20">
                        <StackPanel Spacing="6">
                            <StackPanel Orientation="Horizontal" Spacing="8">
                                <FontIcon Glyph="&#xE9E9;" Foreground="#FF5722"/>
                                <TextBlock Text="CPU Temperature" Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
                            </StackPanel>
                            <TextBlock Text="{Binding CpuTemp, StringFormat='{}{0:F1} °C'}" FontSize="32" FontWeight="Bold" Foreground="#FF5722"/>
                        </StackPanel>
                    </Border>

                    <!-- GPU Temp Card -->
                    <Border Grid.Column="1" Margin="4,0,4,0" Background="{ThemeResource CardBackgroundFillColorDefaultBrush}" CornerRadius="12" Padding="20">
                        <StackPanel Spacing="6">
                            <StackPanel Orientation="Horizontal" Spacing="8">
                                <FontIcon Glyph="&#xE7F8;" Foreground="#FF9800"/>
                                <TextBlock Text="GPU Temperature" Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
                            </StackPanel>
                            <TextBlock Text="{Binding GpuTemp, StringFormat='{}{0:F1} °C'}" FontSize="32" FontWeight="Bold" Foreground="#FF9800"/>
                        </StackPanel>
                    </Border>

                    <!-- Fan RPM Card -->
                    <Border Grid.Column="2" Margin="8,0,0,0" Background="{ThemeResource CardBackgroundFillColorDefaultBrush}" CornerRadius="12" Padding="20">
                        <StackPanel Spacing="6">
                            <StackPanel Orientation="Horizontal" Spacing="8">
                                <FontIcon Glyph="&#xE9CA;" Foreground="#00BCD4"/>
                                <TextBlock Text="Fan Speed (RPM)" Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
                            </StackPanel>
                            <TextBlock Text="{Binding FanRpm, StringFormat='{}{0} RPM'}" FontSize="32" FontWeight="Bold" Foreground="#00BCD4"/>
                        </StackPanel>
                    </Border>
                </Grid>

                <!-- Fan Controls & Auto Curve -->
                <Border Background="{ThemeResource CardBackgroundFillColorDefaultBrush}" CornerRadius="12" Padding="20">
                    <StackPanel Spacing="16">
                        <Grid ColumnDefinitions="*, Auto">
                            <TextBlock Text="Fan Speed Control (PWM %)" FontSize="16" FontWeight="SemiBold"/>
                            <ToggleSwitch Grid.Column="1" Header="Auto Fan Curve" IsOn="{Binding IsAutoMode, Mode=TwoWay}"/>
                        </Grid>

                        <Slider Minimum="0" Maximum="100" Value="{Binding FanPwm, Mode=TwoWay}" IsEnabled="{Binding IsAutoMode, Converter={StaticResource InverseBoolConverter}}"/>
                        
                        <Grid ColumnDefinitions="Auto, *, Auto">
                            <TextBlock Text="Current Output PWM:" Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
                            <TextBlock Grid.Column="2" Text="{Binding FanPwm, StringFormat='{}{0} %'}" FontWeight="Bold" FontSize="18"/>
                        </Grid>

                        <!-- Presets -->
                        <StackPanel Orientation="Horizontal" Spacing="12" HorizontalAlignment="Center">
                            <RadioButton Content="Quiet" IsChecked="{Binding SelectedFanCurve, Converter={StaticResource StringMatchConverter}, ConverterParameter='Quiet'}"/>
                            <RadioButton Content="Balanced" IsChecked="{Binding SelectedFanCurve, Converter={StaticResource StringMatchConverter}, ConverterParameter='Balanced'}"/>
                            <RadioButton Content="Turbo" IsChecked="{Binding SelectedFanCurve, Converter={StaticResource StringMatchConverter}, ConverterParameter='Turbo'}"/>
                        </StackPanel>
                    </StackPanel>
                </Border>

                <!-- RGB Lighting Effects -->
                <Border Background="{ThemeResource CardBackgroundFillColorDefaultBrush}" CornerRadius="12" Padding="20">
                    <StackPanel Spacing="12">
                        <TextBlock Text="RGB Lighting Effect" FontSize="16" FontWeight="SemiBold"/>
                        <ComboBox SelectedIndex="{Binding SelectedLedMode, Mode=TwoWay}" HorizontalAlignment="Stretch">
                            <ComboBoxItem Content="0. Off (Tắt LED)"/>
                            <ComboBoxItem Content="1. Static (Màu Cố Định)"/>
                            <ComboBoxItem Content="2. Breathing (Nhịp Thở)"/>
                            <ComboBoxItem Content="3. Rainbow (Cầu Vồng)"/>
                            <ComboBoxItem Content="4. Speed Pulse (Theo Tốc Độ Quạt)"/>
                        </ComboBox>
                    </StackPanel>
                </Border>

            </StackPanel>
        </ScrollViewer>
    </Grid>
</Window>
```

---

## ⚡ Hướng Dẫn Mở Dự Án Với Visual Studio 2022

1. Mở **Visual Studio 2022** (Đã cài workload *.NET Desktop Development* & *Windows App SDK*).
2. Chọn **Open a project or solution** ➔ Chọn file `smart_fan_cooling_windows_app.csproj`.
3. Nhấn **F5** để Build & Run ứng dụng Native WinUI 3!
