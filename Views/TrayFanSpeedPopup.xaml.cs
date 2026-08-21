using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using SmartFanCooling.ViewModels;

namespace SmartFanCooling.Views
{
    /// <summary>
    /// Borderless popup window with fan speed slider, shown near the system tray icon on right-click.
    /// Positioned above the tray icon, auto-hides when losing focus.
    /// RPM calculation uses the same rounding formula as ESP32 firmware (fan_controller.cpp).
    /// </summary>
    public sealed partial class TrayFanSpeedPopup : Window
    {
        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x0080;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWCP_ROUND = 2; // Windows 11 rounded corners

        // Dark card color #1E1E22 in Win32 COLORREF format (0x00BBGGRR)
        private const int DARK_CARD_COLORREF = 0x00221E1E;

        private MainViewModel? _viewModel;
        private bool _isSyncing = false;

        /// <summary>Raised when user adjusts slider speed (int = new PWM percent 0-100).</summary>
        public event Action<int>? FanSpeedChanged;
        /// <summary>Raised when user clicks "Mở app" button.</summary>
        public event Action? OpenAppRequested;
        /// <summary>Raised when user clicks "Thoát" button.</summary>
        public event Action? ExitRequested;

        public TrayFanSpeedPopup()
        {
            this.InitializeComponent();
            this.Title = "";

            // Keep border enabled so DWM lets us color it; hide title bar only
            if (this.AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(true, false);
                presenter.IsResizable = false;
                presenter.IsAlwaysOnTop = true;
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
            }

            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

            // Hide from taskbar and Alt+Tab
            IntPtr exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)((long)exStyle | WS_EX_TOOLWINDOW));

            // Force dark mode on DWM frame
            int darkMode = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

            // Color the DWM border to exactly match card background (#1E1E22)
            int borderColor = DARK_CARD_COLORREF;
            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref borderColor, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref borderColor, sizeof(int));

            // Rounded corners
            int cornerPref = DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));

            this.ExtendsContentIntoTitleBar = true;

            // Auto-hide when popup loses focus
            this.Activated += OnWindowActivated;
        }

        private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                this.AppWindow.Hide();
            }
        }

        /// <summary>
        /// Shows the popup positioned above the tray icon center.
        /// </summary>
        /// <param name="screenX">Tray icon center X in screen coordinates.</param>
        /// <param name="screenY">Tray icon top Y in screen coordinates.</param>
        /// <param name="vm">ViewModel to read current fan state from.</param>
        public void ShowNear(int screenX, int screenY, MainViewModel vm)
        {
            _viewModel = vm;

            // Sync profiles list & active profile name
            ProfilesItemsControl.ItemsSource = vm.Profiles;
            if (ActiveProfileText != null && vm.ActiveProfile != null)
            {
                ActiveProfileText.Text = vm.ActiveProfile.Name;
            }

            // Sync slider to current fan state without triggering FanSpeedChanged
            _isSyncing = true;
            FanSlider.Value = vm.FanPwm;
            FanSlider.IsEnabled = vm.IsConnected && !vm.IsAutoMode;
            UpdateSpeedDisplay((int)FanSlider.Value);
            _isSyncing = false;

            // Calculate DPI-scaled popup size
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            uint dpi = GetDpiForWindow(hwnd);
            if (dpi == 0) dpi = 96;
            float scale = dpi / 96f;
            int width = (int)(360 * scale);
            int height = (int)(295 * scale);

            // Auto-detect tray position: try above cursor first, flip below if off-screen
            // This handles both standard Windows taskbar (bottom) and MyDock Finder (top)
            int posX = screenX - (width / 2);
            int gap = (int)(8 * scale);
            int posY = screenY - height - gap; // Default: popup above cursor

            // If popup would go off-screen top (tray is at top edge, e.g. MyDock Finder),
            // position it below the cursor instead
            if (posY < 0)
            {
                posY = screenY + gap;
            }

            // Ensure popup doesn't go off-screen left
            if (posX < 0) posX = 0;

            this.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(posX, posY, width, height));

            // Re-assert DWM dark border color matching card
            int darkBorderColor = DARK_CARD_COLORREF;
            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref darkBorderColor, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref darkBorderColor, sizeof(int));

            this.AppWindow.Show();
            this.Activate();
        }

        private void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Models.FanProfile profile && _viewModel != null)
            {
                _viewModel.ApplyProfile(profile);
                _isSyncing = true;
                FanSlider.Value = _viewModel.FanPwm;
                FanSlider.IsEnabled = _viewModel.IsConnected && !_viewModel.IsAutoMode;
                _isSyncing = false;
                UpdateSpeedDisplay((int)FanSlider.Value);
                if (ActiveProfileText != null)
                {
                    ActiveProfileText.Text = profile.Name;
                }
            }
        }

        private void FanSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isSyncing) return;
            int percent = (int)e.NewValue;
            UpdateSpeedDisplay(percent);
            FanSpeedChanged?.Invoke(percent);
        }

        /// <summary>
        /// Updates RPM display using the same rounding formula as ESP32 firmware:
        /// uint16_t rounded = ((percent * 28 + 49) / 100) * 100;
        /// </summary>
        private void UpdateSpeedDisplay(int percent)
        {
            // Firmware RPM formula: round to nearest 100
            int rpm = 0;
            if (percent > 0)
            {
                int raw = percent * 28;
                rpm = ((raw + 49) / 100) * 100;
                if (rpm > 2800) rpm = 2800;
            }
            FanSpeedText.Text = $"{rpm} RPM ({percent}%)";

            // Update connection status indicator
            if (_viewModel != null)
            {
                if (_viewModel.IsAutoMode)
                {
                    StatusText.Text = "AUTO MODE";
                    StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Orange);
                }
                else if (_viewModel.IsConnected)
                {
                    StatusText.Text = "ONLINE";
                    StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.LimeGreen);
                }
                else
                {
                    StatusText.Text = "OFFLINE";
                    StatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gray);
                }
            }
        }

        private void PresetRpm_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tagStr && int.TryParse(tagStr, out int pwm))
            {
                _isSyncing = true;
                FanSlider.Value = pwm;
                _isSyncing = false;
                UpdateSpeedDisplay(pwm);
                FanSpeedChanged?.Invoke(pwm);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.AppWindow.Hide();
        }

        private void OpenApp_Click(object sender, RoutedEventArgs e)
        {
            this.AppWindow.Hide();
            OpenAppRequested?.Invoke();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            this.AppWindow.Hide();
            ExitRequested?.Invoke();
        }
    }
}
