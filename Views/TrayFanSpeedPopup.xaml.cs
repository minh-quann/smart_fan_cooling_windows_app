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

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x0080;

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

            // Configure borderless, always-on-top popup style
            if (this.AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(false, false);
                presenter.IsResizable = false;
                presenter.IsAlwaysOnTop = true;
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
            }

            // Set WS_EX_TOOLWINDOW so popup doesn't appear in taskbar or Alt+Tab
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            IntPtr exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)((long)exStyle | WS_EX_TOOLWINDOW));

            // Auto-hide when popup loses focus (user clicks outside)
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
            int width = (int)(350 * scale);
            int height = (int)(240 * scale);

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
            this.AppWindow.Show();
            this.Activate();
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
