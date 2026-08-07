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

        private const int NIM_ADD = 0x00000000;
        private const int NIM_MODIFY = 0x00000001;
        private const int NIM_DELETE = 0x00000002;
        private const int NIF_MESSAGE = 0x00000001;
        private const int NIF_ICON = 0x00000002;
        private const int NIF_TIP = 0x00000004;
        private const int WM_USER = 0x0400;
        private const int WM_TRAYICON = WM_USER + 1;

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

        private NOTIFYICONDATA _nid;
        private IntPtr _hwnd;

        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "Llano Smart Fan Cooling System - WinUI 3 Native";
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
        }

        private void SetupSystemTrayIcon()
        {
            try
            {
                _nid = new NOTIFYICONDATA();
                _nid.cbSize = Marshal.SizeOf(_nid);
                _nid.hWnd = _hwnd;
                _nid.uID = 1001;
                _nid.uFlags = NIF_ICON | NIF_TIP | NIF_MESSAGE;
                _nid.uCallbackMessage = WM_TRAYICON;
                _nid.szTip = "Llano Smart Fan Cooling System (ONLINE)";
                
                // Get window icon handle
                IntPtr hIcon = SendMessage(_hwnd, 0x007F /* WM_GETICON */, (IntPtr)1 /* ICON_BIG */, IntPtr.Zero);
                if (hIcon == IntPtr.Zero) hIcon = SendMessage(_hwnd, 0x007F, IntPtr.Zero, IntPtr.Zero);
                _nid.hIcon = hIcon;

                Shell_NotifyIcon(NIM_ADD, ref _nid);
            }
            catch { }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            if (ViewModel.MinimizeToTray)
            {
                args.Cancel = true;
                this.AppWindow.Hide();
            }
            else
            {
                Shell_NotifyIcon(NIM_DELETE, ref _nid);
            }
        }

        private void MainNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                ViewModel.SelectedTabIndex = 7; // Settings Tab
            }
            else if (args.SelectedItem is NavigationViewItem item && item.Tag != null)
            {
                if (int.TryParse(item.Tag.ToString(), out int tabIndex))
                {
                    ViewModel.SelectedTabIndex = tabIndex;
                }
            }
        }

        private void RemoveAppBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is AppMapping mapping)
            {
                ViewModel.RemoveAppMapping(mapping);
            }
        }
    }
}
