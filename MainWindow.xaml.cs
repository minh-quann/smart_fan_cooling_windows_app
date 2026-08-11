using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartFanCooling.ViewModels;
using SmartFanCooling.Models;
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

        // Context menu Win32 API
        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, nuint uIDNewItem, string? lpNewItem);

        [DllImport("user32.dll")]
        private static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        private delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, nuint dwRefData);

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
        private const uint MF_STRING = 0x0000;
        private const uint MF_SEPARATOR = 0x0800;
        private const uint TPM_BOTTOMALIGN = 0x0020;
        private const uint TPM_LEFTALIGN = 0x0000;
        private const uint TPM_RETURNCMD = 0x0100;

        // Tray context menu command IDs
        private const nuint ID_TRAY_OPEN = 2001;
        private const nuint ID_TRAY_EXIT = 2002;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
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

        private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

        private NOTIFYICONDATA _nid;
        private IntPtr _hwnd;
        private IntPtr _msgHwnd = IntPtr.Zero;
        private SubclassProc? _wndProc; // prevent GC collection

        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "Smart Fan Cooling Hub";
            _hwnd = WindowNative.GetWindowHandle(this);

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
                
                IntPtr hIcon = SendMessage(_hwnd, 0x007F /* WM_GETICON */, (IntPtr)1 /* ICON_BIG */, IntPtr.Zero);
                if (hIcon != IntPtr.Zero)
                {
                    _nid.hIcon = hIcon;
                }

                Shell_NotifyIcon(NIM_ADD, ref _nid);

                _wndProc = new SubclassProc(TrayIconSubclassProc);
                SetWindowSubclass(targetHwnd, _wndProc, 1, 0);
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
                    ShowTrayContextMenu();
                    return IntPtr.Zero;
                }
            }
            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        private void ShowTrayContextMenu()
        {
            IntPtr hMenu = CreatePopupMenu();
            AppendMenu(hMenu, MF_STRING, ID_TRAY_OPEN, "Mở ứng dụng");
            AppendMenu(hMenu, MF_SEPARATOR, 0, null);
            AppendMenu(hMenu, MF_STRING, ID_TRAY_EXIT, "Thoát");

            IntPtr targetHwnd = (_msgHwnd != IntPtr.Zero) ? _msgHwnd : _hwnd;
            SetForegroundWindow(targetHwnd);

            GetCursorPos(out POINT pt);
            int cmd = TrackPopupMenu(hMenu, TPM_BOTTOMALIGN | TPM_LEFTALIGN | TPM_RETURNCMD, pt.X, pt.Y, 0, targetHwnd, IntPtr.Zero);
            DestroyMenu(hMenu);
            PostMessage(targetHwnd, 0x0000 /* WM_NULL */, IntPtr.Zero, IntPtr.Zero);

            if (cmd == (int)ID_TRAY_OPEN)
            {
                ShowMainWindow();
            }
            else if (cmd == (int)ID_TRAY_EXIT)
            {
                ExitApplication();
            }
        }

        private void ShowMainWindow()
        {
            this.AppWindow.Show();
            this.Activate();
        }

        private void ExitApplication()
        {
            try
            {
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
                ViewModel.SelectedTabIndex = 8; // Settings Tab
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
