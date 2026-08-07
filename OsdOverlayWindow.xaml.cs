using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;
using Microsoft.UI.Windowing;

namespace SmartFanCooling
{
    public partial class OsdOverlayWindow : Window
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetCompositionTimingInfo(IntPtr hwnd, ref DWM_TIMING_INFO pTimingInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct DWM_TIMING_INFO
        {
            public uint cbSize;
            public uint rateRefresh_num;
            public uint rateRefresh_den;
            public ulong qpcRefreshPeriod;
            public uint rateCompose_num;
            public uint rateCompose_den;
        }

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const int GWL_EXSTYLE = -20;
        private const int GWL_STYLE = -16;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const uint LWA_COLORKEY = 0x00000001;

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWCP_DONOTROUND = 1;
        private const uint DWMWA_COLOR_NONE = 0xFFFFFFFE;

        private IntPtr _hwnd;
        private bool _isClickThrough = true;

        public OsdOverlayWindow()
        {
            this.InitializeComponent();
            this.Title = "SmartFanCooling OSD Overlay";

            _hwnd = WindowNative.GetWindowHandle(this);

            // Configure AppWindow presenter for borderless overlay without titlebar caption buttons
            if (this.AppWindow != null)
            {
                this.AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
                if (this.AppWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.IsAlwaysOnTop = true;
                    presenter.IsResizable = false;
                    presenter.IsMinimizable = false;
                    presenter.IsMaximizable = false;
                    presenter.SetBorderAndTitleBar(false, false);
                }
                this.AppWindow.Resize(new Windows.Graphics.SizeInt32(600, 32));
            }

            DisableDwmWindowBorder();
            MakeTopMost();
            SetClickThrough(true);
            SetPresetPosition("top_left");
        }

        private void DisableDwmWindowBorder()
        {
            if (_hwnd == IntPtr.Zero) return;
            try
            {
                int cornerPref = DWMWCP_DONOTROUND;
                DwmSetWindowAttribute(_hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));

                int colorNone = unchecked((int)DWMWA_COLOR_NONE);
                DwmSetWindowAttribute(_hwnd, DWMWA_BORDER_COLOR, ref colorNone, sizeof(int));
            }
            catch { }
        }

        public void ShowWindow()
        {
            if (this.AppWindow != null)
            {
                this.AppWindow.Show();
                MakeTopMost();
            }
        }

        public void HideWindow()
        {
            if (this.AppWindow != null)
            {
                this.AppWindow.Hide();
            }
        }

        public void MakeTopMost()
        {
            if (_hwnd != IntPtr.Zero)
            {
                SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, 0x0002 | 0x0001 | 0x0010 | 0x0040);
            }
        }

        public void SetClickThrough(bool clickThrough)
        {
            _isClickThrough = clickThrough;
            if (_hwnd == IntPtr.Zero) return;

            int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW;
            if (clickThrough)
            {
                exStyle |= WS_EX_TRANSPARENT;
            }
            else
            {
                exStyle &= ~WS_EX_TRANSPARENT;
            }
            SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle);

            // Strip caption / sysmenu styles
            int style = GetWindowLong(_hwnd, GWL_STYLE);
            style &= ~(0x00C00000 | 0x00080000 | 0x00040000 | 0x00020000 | 0x00010000);
            SetWindowLong(_hwnd, GWL_STYLE, style);

            // Remove DWM Windows 11 outline border
            DisableDwmWindowBorder();

            // Set pure black RGB(0,0,0) as 100% transparent COLORKEY so canvas is completely invisible
            SetLayeredWindowAttributes(_hwnd, 0x00000000, 0, LWA_COLORKEY);

            MakeTopMost();
        }

        public void SetPresetPosition(string preset)
        {
            if (_hwnd == IntPtr.Zero) return;

            int screenWidth = GetSystemMetrics(0); // SM_CXSCREEN
            int screenHeight = GetSystemMetrics(1); // SM_CYSCREEN
            int width = this.AppWindow != null ? this.AppWindow.Size.Width : 600;
            int height = this.AppWindow != null ? this.AppWindow.Size.Height : 32;

            int x = 30;
            int y = 30;

            switch (preset)
            {
                case "top_left":
                    x = 30;
                    y = 30;
                    break;
                case "top_center":
                    x = (screenWidth - width) / 2;
                    y = 30;
                    break;
                case "top_right":
                    x = screenWidth - width - 30;
                    y = 30;
                    break;
                case "bottom_left":
                    x = 30;
                    y = screenHeight - height - 60;
                    break;
                case "bottom_center":
                    x = (screenWidth - width) / 2;
                    y = screenHeight - height - 60;
                    break;
                case "bottom_right":
                    x = screenWidth - width - 30;
                    y = screenHeight - height - 60;
                    break;
            }

            SetWindowPos(_hwnd, HWND_TOPMOST, x, y, width, height, 0x0010 | 0x0040);
        }

        private SolidColorBrush GetDynamicColor(float val, float warnVal, float hotVal)
        {
            if (val >= hotVal)
            {
                return new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xFF, 0x17, 0x44)); // Bright Red (#FF1744)
            }
            else if (val >= warnVal)
            {
                return new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xFF, 0xC1, 0x07)); // Bright Yellow (#FFC107)
            }
            else
            {
                return new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x00, 0xE6, 0x76)); // Bright Green (#00E676)
            }
        }

        private int CalculateDisplayFps()
        {
            try
            {
                DWM_TIMING_INFO timing = new DWM_TIMING_INFO();
                timing.cbSize = (uint)Marshal.SizeOf(typeof(DWM_TIMING_INFO));
                if (DwmGetCompositionTimingInfo(IntPtr.Zero, ref timing) == 0)
                {
                    if (timing.rateRefresh_den > 0 && timing.rateRefresh_num > 0)
                    {
                        return (int)Math.Round((double)timing.rateRefresh_num / timing.rateRefresh_den);
                    }
                }
            }
            catch { }
            return 60;
        }

        private void ApplyFontSizeScale(string fontScale)
        {
            double valueSize = 12;
            Thickness padding = new Thickness(3, 1, 3, 1);
            double spacing = 28;
            double itemSpacing = 12;

            switch (fontScale)
            {
                case "1080":
                    valueSize = 12;
                    padding = new Thickness(3, 1, 3, 1);
                    spacing = 28;
                    itemSpacing = 12;
                    break;
                case "2K":
                    valueSize = 15;
                    padding = new Thickness(4, 2, 4, 2);
                    spacing = 34;
                    itemSpacing = 15;
                    break;
                case "4K":
                    valueSize = 18;
                    padding = new Thickness(6, 3, 6, 3);
                    spacing = 40;
                    itemSpacing = 18;
                    break;
            }

            OverlayContainer.Padding = padding;
            OverlayContainer.CornerRadius = new CornerRadius(0);
            OverlayContainer.BorderThickness = new Thickness(0);
            MetricsPanel.Spacing = spacing;

            SetStackScale(FpsBadge, valueSize, 8);
            SetStackScale(TimeBadge, valueSize, 8);
            SetStackScale(CpuBadge, valueSize, itemSpacing);
            SetStackScale(GpuBadge, valueSize, itemSpacing);
            SetStackScale(FanBadge, valueSize, itemSpacing);
            SetStackScale(RamBadge, valueSize, itemSpacing);
        }

        private void SetStackScale(StackPanel sp, double valueSize, double itemSpacing)
        {
            sp.Spacing = itemSpacing;
            foreach (var child in sp.Children)
            {
                if (child is TextBlock tb)
                {
                    tb.FontSize = valueSize;
                }
            }
        }

        public void UpdateTelemetry(
            bool showFps,
            bool showTime, string timeStr,
            bool showCpu, float cpuUsage, float cpuTemp, float cpuPower, float cpuClockGHz, bool showCpuClock, int cpuFanRpm, bool showCpuFan,
            bool showGpu, float gpuUsage, float gpuTemp, float gpuPower, float gpuClockMHz, bool showGpuClock, float gpuVramGB, bool showGpuVram, int gpuFanRpm, bool showGpuFan,
            bool showFan, int fanPwm, int fanRpm,
            bool showRam, float ramUsage,
            double opacity, string style)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ApplyFontSizeScale(style);

                // BACKGROUND CONTAINER OPACITY: 0% = Invisible, 100% = Dark navy (#121622)
                byte bgAlpha = (byte)Math.Clamp((int)(opacity * 255.0), 0, 255);
                OverlayContainer.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(bgAlpha, 0x12, 0x16, 0x22));

                // ColorKey cut-through pure black background canvas
                SetLayeredWindowAttributes(_hwnd, 0x00000000, 0, LWA_COLORKEY);

                // Remove DWM Windows 11 border line
                DisableDwmWindowBorder();

                // FPS Badge
                FpsBadge.Visibility = showFps ? Visibility.Visible : Visibility.Collapsed;
                int fps = CalculateDisplayFps();
                FpsText.Text = $"{fps}";
                FpsText.Foreground = GetDynamicColor(fps >= 50 ? 40 : 80, 50f, 80f);

                // Time Badge
                TimeBadge.Visibility = showTime ? Visibility.Visible : Visibility.Collapsed;
                TimeText.Text = timeStr;

                // CPU Badge
                CpuBadge.Visibility = showCpu ? Visibility.Visible : Visibility.Collapsed;
                CpuUsageText.Text = $"{cpuUsage:F0}%";
                CpuUsageText.Foreground = GetDynamicColor(cpuUsage, 50f, 80f);

                CpuTempText.Text = $"{cpuTemp:F0}°C";
                CpuTempText.Foreground = GetDynamicColor(cpuTemp, 65f, 80f);

                CpuClockText.Visibility = showCpuClock ? Visibility.Visible : Visibility.Collapsed;
                CpuClockText.Text = $"{cpuClockGHz:F2}GHz";

                CpuPowerText.Text = $"{cpuPower:F0}W";

                CpuFanText.Visibility = showCpuFan ? Visibility.Visible : Visibility.Collapsed;
                CpuFanText.Text = $"{cpuFanRpm} RPM";

                // GPU Badge
                GpuBadge.Visibility = showGpu ? Visibility.Visible : Visibility.Collapsed;
                GpuUsageText.Text = $"{gpuUsage:F0}%";
                GpuUsageText.Foreground = GetDynamicColor(gpuUsage, 50f, 80f);

                GpuTempText.Text = $"{gpuTemp:F0}°C";
                GpuTempText.Foreground = GetDynamicColor(gpuTemp, 65f, 80f);

                GpuClockText.Visibility = showGpuClock ? Visibility.Visible : Visibility.Collapsed;
                GpuClockText.Text = $"{gpuClockMHz:F0}MHz";

                GpuPowerText.Text = $"{gpuPower:F0}W";

                GpuVramText.Visibility = showGpuVram ? Visibility.Visible : Visibility.Collapsed;
                GpuVramText.Text = $"{gpuVramGB:F1}GB";

                GpuFanText.Visibility = showGpuFan ? Visibility.Visible : Visibility.Collapsed;
                GpuFanText.Text = $"{gpuFanRpm} RPM";

                // Fan Badge
                FanBadge.Visibility = showFan ? Visibility.Visible : Visibility.Collapsed;
                FanPwmText.Text = $"{fanPwm}%";
                FanRpmText.Text = $"{fanRpm} RPM";

                // RAM Badge
                RamBadge.Visibility = showRam ? Visibility.Visible : Visibility.Collapsed;
                RamText.Text = $"{ramUsage:F0}%";
                RamText.Foreground = GetDynamicColor(ramUsage, 60f, 85f);

                // Orientation style
                if (style == "upright")
                {
                    MetricsPanel.Orientation = Microsoft.UI.Xaml.Controls.Orientation.Vertical;
                }
                else
                {
                    MetricsPanel.Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal;
                }

                // DYNAMIC AUTO-FIT WINDOW WIDTH: Measure exact content size and shrink window width!
                OverlayContainer.Measure(new Windows.Foundation.Size(3000, 300));
                int desiredWidth = Math.Max(80, (int)Math.Ceiling(OverlayContainer.DesiredSize.Width + 4));
                int desiredHeight = Math.Max(24, (int)Math.Ceiling(OverlayContainer.DesiredSize.Height + 2));

                if (this.AppWindow != null && Math.Abs(this.AppWindow.Size.Width - desiredWidth) > 2)
                {
                    this.AppWindow.Resize(new Windows.Graphics.SizeInt32(desiredWidth, desiredHeight));
                }
            });
        }
    }
}
