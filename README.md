# 🚀 Llano Smart Fan Cooling System - WinUI 3 Native (.NET 10)

Ứng dụng Native Windows điều khiển tản nhiệt quạt thông minh kết nối ESP32-S3 qua USB Serial (COM) hoặc Bluetooth LE. Được xây dựng dựa trên kiến trúc MVVM, giao diện WinUI 3 (Fluent Design & Mica) và bộ đọc cảm biến hệ thống `LibreHardwareMonitor`.

---

## 🤖 Hướng Dẫn Cho Antigravity AI Agent (Windows Context)

> **Dành cho AI Agent khi mở dự án này trên môi trường Windows:**
> Dự án này là ứng dụng WinUI 3 C# Native (.NET 10). Toàn bộ kiến trúc và mã nguồn chính đã được hoàn thiện. Hãy đọc các thông tin dưới đây để nắm được cấu trúc dự án.

### 📂 Cấu Trúc Mã Nguồn Dữ Liệu
- **Project Config**: [`smart_fan_cooling_windows_app.csproj`](file:///home/quan/Documents/smart_fan_cooling_windows_app/smart_fan_cooling_windows_app.csproj) - Chứa thông tin Target Framework (`net10.0-windows10.0.19041.0`) và NuGet packages (`Microsoft.WindowsAppSDK`, `LibreHardwareMonitorLib`, `CommunityToolkit.Mvvm`, `System.IO.Ports`).
- **Application Manifest**: [`app.manifest`](file:///home/quan/Documents/smart_fan_cooling_windows_app/app.manifest) - Cấu hình DPI Awareness (PerMonitorV2) và quyền Administrator.
- **Entry Point**: [`App.xaml`](file:///home/quan/Documents/smart_fan_cooling_windows_app/App.xaml) & [`App.xaml.cs`](file:///home/quan/Documents/smart_fan_cooling_windows_app/App.xaml.cs) - Điểm khởi chạy ứng dụng WinUI 3.
- **Main UI Window**: [`MainWindow.xaml`](file:///home/quan/Documents/smart_fan_cooling_windows_app/MainWindow.xaml) & [`MainWindow.xaml.cs`](file:///home/quan/Documents/smart_fan_cooling_windows_app/MainWindow.xaml.cs) - Giao diện chính (Fluent Design, Telemetry Cards, Fan Speed Slider, RGB Mode selector).
- **Converters**: [`Converters/ValueConverters.cs`](file:///home/quan/Documents/smart_fan_cooling_windows_app/Converters/ValueConverters.cs) - Các ValueConverter chuyển đổi dữ liệu XAML Binding (`ConnectTextConverter`, `InverseBoolConverter`, `StringMatchConverter`).
- **Main ViewModel**: [`ViewModels/MainViewModel.cs`](file:///home/quan/Documents/smart_fan_cooling_windows_app/ViewModels/MainViewModel.cs) - Quản lý State bằng `CommunityToolkit.Mvvm`, Timer 1 giây đọc cảm biến và gửi lệnh điều khiển.
- **Services**:
  - [`Services/HardwareMonitorService.cs`](file:///home/quan/Documents/smart_fan_cooling_windows_app/Services/HardwareMonitorService.cs) - Đọc cảm biến CPU/GPU (nhiệt độ, công suất Watt, xung nhịp GHz) qua LibreHardwareMonitorLib.
  - [`Services/SerialFanService.cs`](file:///home/quan/Documents/smart_fan_cooling_windows_app/Services/SerialFanService.cs) - Giao tiếp USB Serial với ESP32-S3 qua cổng COM (JSON Telemetry & Command).
  - [`Services/BleFanService.cs`](file:///home/quan/Documents/smart_fan_cooling_windows_app/Services/BleFanService.cs) - Giao tiếp Bluetooth Low Energy (BLE) với ESP32-S3.
  - [`Services/AutoFanCurveService.cs`](file:///home/quan/Documents/smart_fan_cooling_windows_app/Services/AutoFanCurveService.cs) - Tính toán PWM % theo nhiệt độ và các profile (`Quiet`, `Balanced`, `Turbo`, `Custom`).
- **Tài liệu bổ trợ**:
  - [`WINUI3_PROJECT_CODE.md`](file:///home/quan/Documents/smart_fan_cooling_windows_app/WINUI3_PROJECT_CODE.md) - Tài liệu tổng hợp mã nguồn WinUI 3.
  - [`WIRING_GUIDE.md`](file:///home/quan/Documents/smart_fan_cooling_windows_app/WIRING_GUIDE.md) - Hướng dẫn đấu nối 31 dây phần cứng.
  - `firmware/` - Mã nguồn C++ PlatformIO / Arduino cho phần cứng ESP32-S3.

---

## 🛠 Yêu Cầu Môi Trường (Windows)

1. **Hệ điều hành**: Windows 10 (Phiên bản 1809 trở lên) hoặc Windows 11.
2. **Công cụ phát triển**:
   - Visual Studio 2022 (Phiên bản 17.8 trở lên).
   - Workloads bắt buộc trong Visual Studio Installer:
     - **.NET Desktop Development**
     - **Windows App SDK / WinUI 3 Build Tools**
3. **Quyền Administrator**: 
   - Ứng dụng đọc cảm biến nhiệt độ hệ thống trực tiếp từ phần cứng thông qua `LibreHardwareMonitorLib`, do đó cần chạy bằng quyền **Run as Administrator** trên Windows.

---

## 💻 Hướng Dẫn Biên Dịch & Khởi Chạy Trên Windows

### Cách 1: Sử dụng Visual Studio 2022 (Khuyên dùng)
1. Mở Visual Studio 2022.
2. Chọn **Open a project or solution** ➔ Chọn file [`smart_fan_cooling_windows_app.csproj`](file:///home/quan/Documents/smart_fan_cooling_windows_app/smart_fan_cooling_windows_app.csproj).
3. Đảm bảo cấu hình chọn là `Debug` hoặc `Release`, nền tảng `x64`.
4. Nhấn **F5** để biên dịch và khởi chạy ứng dụng.

### Cách 2: Sử dụng .NET CLI (Command Line)
Mở PowerShell hoặc Command Prompt dưới quyền Administrator:
```powershell
# Chuyển vào thư mục dự án
cd path\to\smart_fan_cooling_windows_app

# Restore NuGet packages
dotnet restore

# Biên dịch dự án
dotnet build -c Release

# Chạy ứng dụng
dotnet run -c Release
```

---

## ⚡ Các Tính Năng Chính Của Ứng Dụng

- **Telemetry Dashboard**: Hiển thị realtime nhiệt độ CPU, GPU, công suất tiêu thụ (Watt), xung nhịp (GHz) và RPM quạt.
- **Tự động điều tốc (Auto Fan Curve)**: Tự động điều chỉnh phần trăm PWM quạt theo đường cong nhiệt độ (các chế độ `Quiet`, `Balanced`, `Turbo`).
- **Điều khiển thủ công (Manual PWM)**: Slider điều chỉnh trực tiếp từ 0% - 100%.
- **Chế độ LED RGB**: Thay đổi 5 hiệu ứng ánh sáng (Off, Static, Breathing, Rainbow, Speed Pulse).
- **Tự động nhận diện cổng COM**: Nút Refresh quét nhanh các thiết bị USB Serial kết nối với máy tính.
