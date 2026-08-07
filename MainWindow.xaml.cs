using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
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

            SubscribeViewModelEvents();
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

        private void ProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is FanProfile profile)
            {
                ViewModel.SelectProfile(profile);
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

        private int _draggingNodeIndex = -1;
        private Canvas? _activeDraggingCanvas = null;

        private void SubscribeViewModelEvents()
        {
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName != null && (
                        e.PropertyName.StartsWith("CurveP") ||
                        e.PropertyName == nameof(MainViewModel.ActiveProfile)))
                    {
                        RedrawFanCurveGraph();
                    }
                };
            }
        }

        private void RedrawFanCurveGraph()
        {
            RedrawSingleCanvas(FanCurveCanvas, false);
            RedrawSingleCanvas(OverviewFanCurveCanvas, true);
        }

        private void RedrawSingleCanvas(Canvas? canvas, bool isOverview)
        {
            if (canvas == null || ViewModel == null) return;

            double width = canvas.ActualWidth;
            double height = canvas.ActualHeight;
            if (width < 50 || height < 30) return;

            double topY = isOverview ? 10.0 : 15.0;
            double bottomY = isOverview ? 150.0 : 185.0;
            double rangeY = bottomY - topY;

            Line? h100 = isOverview ? O_GridH_100 : GridH_100;
            Line? h75  = isOverview ? O_GridH_75  : GridH_75;
            Line? h50  = isOverview ? O_GridH_50  : GridH_50;
            Line? h25  = isOverview ? O_GridH_25  : GridH_25;
            Line? h0   = isOverview ? O_GridH_0   : GridH_0;

            if (h100 != null) h100.X2 = width;
            if (h75 != null)  h75.X2  = width;
            if (h50 != null)  h50.X2  = width;
            if (h25 != null)  h25.X2  = width;
            if (h0 != null)   h0.X2   = width;

            double marginX = 15;
            double usableW = Math.Max(10, width - (2 * marginX));
            double stepX = usableW / 6.0;

            double[] nodeXs = new double[7];
            for (int i = 0; i < 7; i++)
            {
                nodeXs[i] = marginX + (i * stepX);
            }

            Line? v30 = isOverview ? O_GridV_30 : GridV_30;
            Line? v40 = isOverview ? O_GridV_40 : GridV_40;
            Line? v50 = isOverview ? O_GridV_50 : GridV_50;
            Line? v60 = isOverview ? O_GridV_60 : GridV_60;
            Line? v70 = isOverview ? O_GridV_70 : GridV_70;
            Line? v80 = isOverview ? O_GridV_80 : GridV_80;
            Line? v90 = isOverview ? O_GridV_90 : GridV_90;

            if (v30 != null) v30.X1 = v30.X2 = nodeXs[0];
            if (v40 != null) v40.X1 = v40.X2 = nodeXs[1];
            if (v50 != null) v50.X1 = v50.X2 = nodeXs[2];
            if (v60 != null) v60.X1 = v60.X2 = nodeXs[3];
            if (v70 != null) v70.X1 = v70.X2 = nodeXs[4];
            if (v80 != null) v80.X1 = v80.X2 = nodeXs[5];
            if (v90 != null) v90.X1 = v90.X2 = nodeXs[6];

            int[] pwms = new int[]
            {
                ViewModel.CurveP30,
                ViewModel.CurveP40,
                ViewModel.CurveP50,
                ViewModel.CurveP60,
                ViewModel.CurveP70,
                ViewModel.CurveP80,
                ViewModel.CurveP90
            };

            double[] nodeYs = new double[7];
            for (int i = 0; i < 7; i++)
            {
                nodeYs[i] = bottomY - (pwms[i] / 100.0 * rangeY);
            }

            var pts = new Microsoft.UI.Xaml.Media.PointCollection();
            var fillPts = new Microsoft.UI.Xaml.Media.PointCollection();

            fillPts.Add(new Windows.Foundation.Point(nodeXs[0], bottomY));

            for (int i = 0; i < 7; i++)
            {
                var pt = new Windows.Foundation.Point(nodeXs[i], nodeYs[i]);
                pts.Add(pt);
                fillPts.Add(pt);
            }

            fillPts.Add(new Windows.Foundation.Point(nodeXs[6], bottomY));

            Polyline? poly = isOverview ? O_CurvePolyline : CurvePolyline;
            Polygon? fill = isOverview ? O_CurvePolygonFill : CurvePolygonFill;

            if (poly != null) poly.Points = pts;
            if (fill != null) fill.Points = fillPts;

            UIElement? n30 = isOverview ? O_NodeRect_30 : NodeRect_30;
            UIElement? n40 = isOverview ? O_NodeRect_40 : NodeRect_40;
            UIElement? n50 = isOverview ? O_NodeRect_50 : NodeRect_50;
            UIElement? n60 = isOverview ? O_NodeRect_60 : NodeRect_60;
            UIElement? n70 = isOverview ? O_NodeRect_70 : NodeRect_70;
            UIElement? n80 = isOverview ? O_NodeRect_80 : NodeRect_80;
            UIElement? n90 = isOverview ? O_NodeRect_90 : NodeRect_90;

            double halfNode = isOverview ? 4.0 : 5.0;

            if (n30 != null) { Canvas.SetLeft(n30, nodeXs[0] - halfNode); Canvas.SetTop(n30, nodeYs[0] - halfNode); }
            if (n40 != null) { Canvas.SetLeft(n40, nodeXs[1] - halfNode); Canvas.SetTop(n40, nodeYs[1] - halfNode); }
            if (n50 != null) { Canvas.SetLeft(n50, nodeXs[2] - halfNode); Canvas.SetTop(n50, nodeYs[2] - halfNode); }
            if (n60 != null) { Canvas.SetLeft(n60, nodeXs[3] - halfNode); Canvas.SetTop(n60, nodeYs[3] - halfNode); }
            if (n70 != null) { Canvas.SetLeft(n70, nodeXs[4] - halfNode); Canvas.SetTop(n70, nodeYs[4] - halfNode); }
            if (n80 != null) { Canvas.SetLeft(n80, nodeXs[5] - halfNode); Canvas.SetTop(n80, nodeYs[5] - halfNode); }
            if (n90 != null) { Canvas.SetLeft(n90, nodeXs[6] - halfNode); Canvas.SetTop(n90, nodeYs[6] - halfNode); }

            UIElement? t30 = isOverview ? O_TxtLabel_30 : TxtLabel_30;
            UIElement? t40 = isOverview ? O_TxtLabel_40 : TxtLabel_40;
            UIElement? t50 = isOverview ? O_TxtLabel_50 : TxtLabel_50;
            UIElement? t60 = isOverview ? O_TxtLabel_60 : TxtLabel_60;
            UIElement? t70 = isOverview ? O_TxtLabel_70 : TxtLabel_70;
            UIElement? t80 = isOverview ? O_TxtLabel_80 : TxtLabel_80;
            UIElement? t90 = isOverview ? O_TxtLabel_90 : TxtLabel_90;

            double labelOffset = isOverview ? 10.0 : 12.0;

            if (t30 != null) Canvas.SetLeft(t30, nodeXs[0] - labelOffset);
            if (t40 != null) Canvas.SetLeft(t40, nodeXs[1] - labelOffset);
            if (t50 != null) Canvas.SetLeft(t50, nodeXs[2] - labelOffset);
            if (t60 != null) Canvas.SetLeft(t60, nodeXs[3] - labelOffset);
            if (t70 != null) Canvas.SetLeft(t70, nodeXs[4] - labelOffset);
            if (t80 != null) Canvas.SetLeft(t80, nodeXs[5] - labelOffset);
            if (t90 != null) Canvas.SetLeft(t90, nodeXs[6] - labelOffset);
        }

        private void FanCurveCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RedrawFanCurveGraph();
        private void OverviewFanCurveCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RedrawFanCurveGraph();

        private void HandleCanvasPointerPressed(Canvas? canvas, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e, bool isOverview)
        {
            if (ViewModel == null || canvas == null) return;
            var pt = e.GetCurrentPoint(canvas).Position;

            double width = canvas.ActualWidth;
            double marginX = 15;
            double usableW = Math.Max(10, width - (2 * marginX));
            double stepX = usableW / 6.0;

            int closestIndex = -1;
            double minDist = 35;

            for (int i = 0; i < 7; i++)
            {
                double nodeX = marginX + (i * stepX);
                double distX = Math.Abs(pt.X - nodeX);
                if (distX < minDist)
                {
                    minDist = distX;
                    closestIndex = i;
                }
            }

            if (closestIndex >= 0)
            {
                _draggingNodeIndex = closestIndex;
                _activeDraggingCanvas = canvas;
                canvas.CapturePointer(e.Pointer);
                UpdateNodeFromPointer(pt.Y, isOverview);
            }
        }

        private void HandleCanvasPointerMoved(Canvas? canvas, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e, bool isOverview)
        {
            if (_draggingNodeIndex >= 0 && _activeDraggingCanvas == canvas && canvas != null)
            {
                var pt = e.GetCurrentPoint(canvas).Position;
                UpdateNodeFromPointer(pt.Y, isOverview);
            }
        }

        private void HandleCanvasPointerReleased(Canvas? canvas, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (_draggingNodeIndex >= 0 && _activeDraggingCanvas == canvas && canvas != null)
            {
                _draggingNodeIndex = -1;
                _activeDraggingCanvas = null;
                canvas.ReleasePointerCapture(e.Pointer);
            }
        }

        private void FanCurveCanvas_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) => HandleCanvasPointerPressed(FanCurveCanvas, e, false);
        private void FanCurveCanvas_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) => HandleCanvasPointerMoved(FanCurveCanvas, e, false);
        private void FanCurveCanvas_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) => HandleCanvasPointerReleased(FanCurveCanvas, e);

        private void OverviewFanCurveCanvas_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) => HandleCanvasPointerPressed(OverviewFanCurveCanvas, e, true);
        private void OverviewFanCurveCanvas_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) => HandleCanvasPointerMoved(OverviewFanCurveCanvas, e, true);
        private void OverviewFanCurveCanvas_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) => HandleCanvasPointerReleased(OverviewFanCurveCanvas, e);

        private void UpdateNodeFromPointer(double pointerY, bool isOverview)
        {
            double topY = isOverview ? 10.0 : 15.0;
            double bottomY = isOverview ? 150.0 : 185.0;
            double rangeY = bottomY - topY;

            int newPwm = (int)Math.Clamp(Math.Round((bottomY - pointerY) / rangeY * 100.0), 0, 100);

            switch (_draggingNodeIndex)
            {
                case 0: ViewModel.CurveP30 = newPwm; break;
                case 1: ViewModel.CurveP40 = newPwm; break;
                case 2: ViewModel.CurveP50 = newPwm; break;
                case 3: ViewModel.CurveP60 = newPwm; break;
                case 4: ViewModel.CurveP70 = newPwm; break;
                case 5: ViewModel.CurveP80 = newPwm; break;
                case 6: ViewModel.CurveP90 = newPwm; break;
            }

            RedrawFanCurveGraph();
        }
    }
}
