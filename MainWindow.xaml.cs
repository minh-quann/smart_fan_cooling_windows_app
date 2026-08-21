using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartFanCooling.ViewModels;
using SmartFanCooling.Models;
using SmartFanCooling.Views;
using WinRT.Interop;

namespace SmartFanCooling
{
    public partial class MainWindow : Window
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        // WndProc subclass for tray icon messages
        [DllImport("comctl32.dll")]
        private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, nuint uIdSubclass, nuint dwRefData);

        [DllImport("comctl32.dll")]
        private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, nuint uIdSubclass);

        [DllImport("comctl32.dll")]
        private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);



        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        // Low-level mouse hook for scroll-to-adjust fan speed over tray icon
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        // Get tray icon bounding rectangle (Windows 7+)
        [DllImport("shell32.dll")]
        private static extern int Shell_NotifyIconGetRect(ref NOTIFYICONIDENTIFIER identifier, out RECT iconRect);

        private delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, nuint dwRefData);
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private const int NIM_ADD = 0x00000000;
        private const int NIM_MODIFY = 0x00000001;
        private const int NIM_DELETE = 0x00000002;
        private const int NIF_MESSAGE = 0x00000001;
        private const int NIF_ICON = 0x00000002;
        private const int NIF_TIP = 0x00000004;
        private const int WM_USER = 0x0400;
        private const int WM_TRAYICON = WM_USER + 1;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_COMMAND = 0x0111;
        // Mouse hook constants
        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEWHEEL = 0x020A;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NOTIFYICONIDENTIFIER
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public Guid guidItem;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public int mouseData;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public int uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public int dwInfoFlags;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            int dwExStyle,
            string lpClassName,
            string lpWindowName,
            int dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private const uint IMAGE_ICON = 1;
        private const uint LR_LOADFROMFILE = 0x00000010;
        private const int SM_CXSMICON = 49;
        private const int SM_CYSMICON = 50;

        private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

        private NOTIFYICONDATA _nid;
        private IntPtr _hwnd;
        private IntPtr _msgHwnd = IntPtr.Zero;
        private SubclassProc? _wndProc; // prevent GC collection
        private LowLevelMouseProc? _mouseHookProc; // prevent GC collection of hook delegate
        private IntPtr _mouseHook = IntPtr.Zero;
        private TrayFanSpeedPopup? _trayFanPopup;

        public MainViewModel ViewModel { get; }

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_HIDE = 0;

        public MainWindow(bool startHidden = false)
        {
            this.InitializeComponent();
            this.Title = "Smart Fan Cooling Hub";
            _hwnd = WindowNative.GetWindowHandle(this);

            // Force hide the window immediately via Win32 if starting minimized to tray.
            // In WinUI 3, the window may become visible during construction;
            // AppWindow.Hide() alone is not reliable in the constructor,
            // so we use the native ShowWindow(SW_HIDE) API to guarantee invisibility.
            if (startHidden)
            {
                ShowWindow(_hwnd, SW_HIDE);
            }

            try
            {
                string iconPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "app_icon.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    this.AppWindow.SetIcon(iconPath);
                }
            }
            catch { }

            ViewModel = new MainViewModel();
            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.DataContext = ViewModel;
            }

            SetupSystemTrayIcon();

            if (this.AppWindow != null)
            {
                this.AppWindow.Closing += AppWindow_Closing;
            }

            Microsoft.Win32.SystemEvents.SessionEnding += SystemEvents_SessionEnding;

            // Also use AppWindow.Hide() for belt-and-suspenders approach
            if (startHidden && this.AppWindow != null)
            {
                this.AppWindow.Hide();
            }
        }

        private void SystemEvents_SessionEnding(object sender, Microsoft.Win32.SessionEndingEventArgs e)
        {
            ExitApplication();
        }

        private void SetupSystemTrayIcon()
        {
            try
            {
                // Create a Message-Only Window so system tray icon actions never focus or activate MainWindow automatically
                _msgHwnd = CreateWindowEx(0, "STATIC", "SmartFanTrayMsgHost", 0, 0, 0, 0, 0, HWND_MESSAGE, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                IntPtr targetHwnd = (_msgHwnd != IntPtr.Zero) ? _msgHwnd : _hwnd;

                _nid = new NOTIFYICONDATA();
                _nid.cbSize = Marshal.SizeOf(_nid);
                _nid.hWnd = targetHwnd;
                _nid.uID = 1001;
                _nid.uFlags = NIF_ICON | NIF_TIP | NIF_MESSAGE;
                _nid.uCallbackMessage = WM_TRAYICON;
                _nid.szTip = "Smart Fan Cooling Hub (ONLINE)";
                
                IntPtr hIcon = IntPtr.Zero;
                string iconPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "app_icon.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    int smCx = GetSystemMetrics(SM_CXSMICON);
                    int smCy = GetSystemMetrics(SM_CYSMICON);
                    hIcon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, smCx, smCy, LR_LOADFROMFILE);
                }
                if (hIcon == IntPtr.Zero)
                {
                    hIcon = SendMessage(_hwnd, 0x007F /* WM_GETICON */, (IntPtr)0 /* ICON_SMALL */, IntPtr.Zero);
                }
                if (hIcon != IntPtr.Zero)
                {
                    _nid.hIcon = hIcon;
                }

                Shell_NotifyIcon(NIM_ADD, ref _nid);

                _wndProc = new SubclassProc(TrayIconSubclassProc);
                SetWindowSubclass(targetHwnd, _wndProc, 1, 0);

                // Install low-level mouse hook for scroll-to-adjust fan speed over tray icon
                InstallTrayMouseWheelHook();
            }
            catch { }
        }

        private IntPtr TrayIconSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, nuint dwRefData)
        {
            if (uMsg == WM_TRAYICON)
            {
                int lEvt = (int)lParam;
                if (lEvt == WM_LBUTTONDBLCLK)
                {
                    ShowMainWindow();
                    return IntPtr.Zero;
                }
                else if (lEvt == WM_RBUTTONUP)
                {
                    ShowTrayFanPopup();
                    return IntPtr.Zero;
                }
            }
            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }



        /// <summary>
        /// Creates and shows a borderless popup with a fan speed slider near the tray icon.
        /// Replaces the traditional context menu for a more intuitive UX.
        /// </summary>
        private void ShowTrayFanPopup()
        {
            // Create popup on first use, reuse on subsequent calls
            if (_trayFanPopup == null)
            {
                _trayFanPopup = new TrayFanSpeedPopup();
                _trayFanPopup.FanSpeedChanged += SetTrayFanSpeed;
                _trayFanPopup.OpenAppRequested += ShowMainWindow;
                _trayFanPopup.ExitRequested += ExitApplication;
            }

            // Use cursor position for compatibility with third-party docks (MyDock Finder, etc.)
            // Shell_NotifyIconGetRect returns native Windows tray position, which may differ from
            // the actual icon location when a dock app moves icons to another screen edge.
            GetCursorPos(out POINT pt);
            _trayFanPopup.ShowNear(pt.X, pt.Y, ViewModel);
        }

        private void ShowMainWindow()
        {
            this.AppWindow.Show();
            this.Activate();
        }

        /// <summary>
        /// Installs a global low-level mouse hook to capture scroll wheel events over the tray icon.
        /// </summary>
        private void InstallTrayMouseWheelHook()
        {
            _mouseHookProc = TrayMouseHookCallback;
            _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseHookProc, GetModuleHandle(null), 0);
        }

        /// <summary>
        /// Low-level mouse hook callback. Intercepts WM_MOUSEWHEEL when cursor is over our tray icon
        /// and adjusts fan speed ±4% per scroll notch (matches firmware encoder step).
        /// </summary>
        private IntPtr TrayMouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (int)wParam == WM_MOUSEWHEEL)
            {
                var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                // Check if cursor is precisely over our tray icon using Shell_NotifyIconGetRect
                var nii = new NOTIFYICONIDENTIFIER
                {
                    cbSize = (uint)Marshal.SizeOf<NOTIFYICONIDENTIFIER>(),
                    hWnd = _nid.hWnd,
                    uID = (uint)_nid.uID,
                    guidItem = Guid.Empty
                };

                if (Shell_NotifyIconGetRect(ref nii, out RECT iconRect) == 0) // S_OK
                {
                    if (hookStruct.pt.X >= iconRect.Left && hookStruct.pt.X <= iconRect.Right &&
                        hookStruct.pt.Y >= iconRect.Top && hookStruct.pt.Y <= iconRect.Bottom)
                    {
                        // Only adjust if connected and not in auto fan curve mode
                        if (ViewModel.IsConnected && !ViewModel.IsAutoMode)
                        {
                            // Extract wheel delta: HIWORD of mouseData (positive = scroll up, negative = scroll down)
                            int delta = (short)((hookStruct.mouseData >> 16) & 0xFFFF);
                            int step = delta > 0 ? 4 : -4; // 4% per notch (matches firmware ENCODER_STEP)
                            SetTrayFanSpeed(Math.Clamp(ViewModel.FanPwm + step, 0, 100));
                        }
                        return (IntPtr)1; // Consume scroll event over our tray icon
                    }
                }
            }

            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        /// <summary>
        /// Sets fan speed from tray controls (context menu presets or scroll wheel).
        /// Disables auto mode and updates tray tooltip.
        /// </summary>
        private void SetTrayFanSpeed(int percent)
        {
            if (!ViewModel.IsConnected) return;
            ViewModel.IsAutoMode = false;
            ViewModel.FanPwm = Math.Clamp(percent, 0, 100);
            ViewModel.IsFanStateOn = percent > 0;
            UpdateTrayTooltip();
        }

        /// <summary>
        /// Updates the tray icon tooltip to show current fan speed and connection status.
        /// </summary>
        private void UpdateTrayTooltip()
        {
            try
            {
                string status = ViewModel.IsConnected
                    ? $"Smart Fan Cooling Hub — Quạt: {ViewModel.FanPwm}% ({ViewModel.FanRpm} RPM)"
                    : "Smart Fan Cooling Hub (OFFLINE)";

                // szTip max length = 128 chars
                if (status.Length > 127) status = status.Substring(0, 127);
                _nid.szTip = status;
                Shell_NotifyIcon(NIM_MODIFY, ref _nid);
            }
            catch { }
        }

        private void ExitApplication()
        {
            try
            {
                // Close tray fan speed popup
                if (_trayFanPopup != null)
                {
                    _trayFanPopup.Close();
                    _trayFanPopup = null;
                }

                // Unhook low-level mouse hook
                if (_mouseHook != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_mouseHook);
                    _mouseHook = IntPtr.Zero;
                }

                Microsoft.Win32.SystemEvents.SessionEnding -= SystemEvents_SessionEnding;
                Shell_NotifyIcon(NIM_DELETE, ref _nid);

                IntPtr targetHwnd = (_msgHwnd != IntPtr.Zero) ? _msgHwnd : _hwnd;
                if (_wndProc != null)
                {
                    RemoveWindowSubclass(targetHwnd, _wndProc, 1);
                }

                if (_msgHwnd != IntPtr.Zero)
                {
                    DestroyWindow(_msgHwnd);
                    _msgHwnd = IntPtr.Zero;
                }

                if (ViewModel.IsConnected)
                {
                    ViewModel.ToggleConnection();
                }
            }
            catch { }

            System.Environment.Exit(0);
        }

        private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            if (ViewModel.MinimizeToTray)
            {
                args.Cancel = true;
                this.AppWindow.Hide();
            }
            else
            {
                ExitApplication();
            }
        }

        private void MainNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                ViewModel.SelectedTabIndex = 9; // Settings Tab
            }
            else if (args.SelectedItem is NavigationViewItem item && item.Tag != null)
            {
                if (int.TryParse(item.Tag.ToString(), out int tabIndex))
                {
                    ViewModel.SelectedTabIndex = tabIndex;
                }
            }
        }

        private void RunningAppsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is RunningAppInfo app)
            {
                ViewModel.SelectRunningApp(app);
            }
        }

        private async void BrowseExeFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
                picker.FileTypeFilter.Add(".exe");

                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    ViewModel.NewAppName = System.IO.Path.GetFileNameWithoutExtension(file.Path);
                    ViewModel.NewExePath = file.Path;
                    ViewModel.IsAppPickerOpen = false;
                    ViewModel.StatusMessage = $"Đã chọn file: {file.Name}";
                }
            }
            catch { }
        }
    }
}
