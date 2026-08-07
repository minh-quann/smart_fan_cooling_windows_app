using System;
using System.Runtime.InteropServices;

namespace SmartFanCooling.Services
{
    public class NativeOsdOverlay : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
            int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateDIBSection(
            IntPtr hdc, ref BITMAPINFO pbmi, uint usage, out IntPtr ppvBits, IntPtr hSection, uint dwOffset);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFontW(
            int cHeight, int cWidth, int cEscapement, int cOrientation, int cWeight,
            uint bItalic, uint bUnderline, uint bStrikeOut, uint iCharSet,
            uint iOutPrecision, uint iClipPrecision, uint iQuality, uint iPitchAndFamily, string pszFaceName);

        [DllImport("gdi32.dll")]
        private static extern int SetBkMode(IntPtr hdc, int mode);

        [DllImport("gdi32.dll")]
        private static extern uint SetTextColor(IntPtr hdc, uint color);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern bool ExtTextOutW(
            IntPtr hdc, int x, int y, uint options, IntPtr lprect, string lpString, uint c, IntPtr lpDx);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetTextExtentPoint32W(IntPtr hdc, string lpString, int c, out SIZE lpSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(
            IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pptSrc, uint crKey, ref BLENDFUNCTION pblend, uint dwFlags);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetCompositionTimingInfo(IntPtr hwnd, ref DWM_TIMING_INFO pTimingInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern ushort RegisterClassEx([In] ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr SetCapture(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int left; public int top; public int right; public int bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE { public int cx; public int cy; }

        [StructLayout(LayoutKind.Sequential)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public uint bmiColors;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DWM_TIMING_INFO
        {
            public uint cbSize;
            public uint rateRefresh_num;
            public uint rateRefresh_den;
            public ulong qpcRefreshPeriod;
            public uint rateCompose_num;
            public uint rateCompose_den;
            public ulong qpcVBlank;
            public ulong cRefresh;
            public uint cDXRefresh;
            public ulong qpcCompose;
            public ulong cFrame;
            public uint cDXPresent;
            public ulong cRefreshFrame;
            public ulong cFrameSubmitted;
            public uint cDXPresentSubmitted;
            public ulong cFrameRendered;
            public ulong cFrameDisplayed;
            public ulong qpcFrameComplete;
        }

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const int GWL_EXSTYLE = -20;
        private const int GWLP_WNDPROC = -4;
        private const uint WS_EX_LAYERED = 0x00080000;
        private const uint WS_EX_TRANSPARENT = 0x00000020;
        private const uint WS_EX_TOPMOST = 0x00000008;
        private const uint WS_EX_TOOLWINDOW = 0x00000080;
        private const uint WS_EX_NOACTIVATE = 0x08000000;
        private const uint WS_POPUP = 0x80000000;
        private const uint ULW_ALPHA = 0x00000002;
        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;

        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_NCLBUTTONDOWN = 0x00A1;
        private const uint WM_MOUSEMOVE = 0x0200;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_NCHITTEST = 0x0084;
        private const uint WM_MOVE = 0x0003;
        private const uint WM_MOVING = 0x0216;
        private const uint WM_EXITSIZEMOVE = 0x0232;
        private static readonly IntPtr HTCAPTION = new IntPtr(2);

        private IntPtr _hwnd = IntPtr.Zero;
        private int _posX = 0;
        private int _posY = 0;
        private string _presetPosition = "top_center";
        private bool _isClickThrough = true;

        private bool _isDragging = false;
        private POINT _dragStartCursor;
        private POINT _dragStartWinPos;

        private WndProcDelegate? _wndProcDelegate;
        private IntPtr _oldWndProc = IntPtr.Zero;

        public NativeOsdOverlay()
        {
            CreateOverlayWindow();
        }

        private static bool _classRegistered = false;
        private static readonly object _classLock = new object();

        private void CreateOverlayWindow()
        {
            lock (_classLock)
            {
                if (!_classRegistered)
                {
                    _wndProcDelegate = CustomWndProc;
                    IntPtr pFunc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);

                    WNDCLASSEX wc = new WNDCLASSEX();
                    wc.cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX));
                    wc.style = 0;
                    wc.lpfnWndProc = pFunc;
                    wc.cbClsExtra = 0;
                    wc.cbWndExtra = 0;
                    wc.hInstance = IntPtr.Zero;
                    wc.hIcon = IntPtr.Zero;
                    wc.hCursor = LoadCursor(IntPtr.Zero, 32512 /* IDC_ARROW */);
                    wc.hbrBackground = IntPtr.Zero;
                    wc.lpszMenuName = "";
                    wc.lpszClassName = "SmartFanOsdClass";
                    wc.hIconSm = IntPtr.Zero;

                    RegisterClassEx(ref wc);
                    _classRegistered = true;
                }
            }

            uint exStyle = WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            uint style = WS_POPUP;

            _hwnd = CreateWindowEx(exStyle, "SmartFanOsdClass", "SmartFanCooling Native OSD", style, 0, 0, 800, 30, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (_hwnd != IntPtr.Zero)
            {
                ShowWindow(_hwnd, 8); // SW_SHOWNA
                SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 800, 30, 0x0010 | 0x0040);
            }
        }

        private IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (!_isClickThrough)
            {
                if (msg == WM_LBUTTONDOWN)
                {
                    _isDragging = true;
                    GetCursorPos(out _dragStartCursor);
                    if (GetWindowRect(hWnd, out RECT r))
                    {
                        _dragStartWinPos.x = r.left;
                        _dragStartWinPos.y = r.top;
                    }
                    SetCapture(hWnd);
                    ReleaseCapture();
                    SendMessage(hWnd, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
                    return IntPtr.Zero;
                }
                else if (msg == WM_MOUSEMOVE && _isDragging)
                {
                    GetCursorPos(out POINT curCursor);
                    int dx = curCursor.x - _dragStartCursor.x;
                    int dy = curCursor.y - _dragStartCursor.y;
                    _posX = _dragStartWinPos.x + dx;
                    _posY = _dragStartWinPos.y + dy;
                    _presetPosition = "custom";
                    SetWindowPos(hWnd, HWND_TOPMOST, _posX, _posY, 0, 0, 0x0001 /* SWP_NOSIZE */ | 0x0040 /* SWP_SHOWWINDOW */);
                    return IntPtr.Zero;
                }
                else if (msg == WM_LBUTTONUP && _isDragging)
                {
                    _isDragging = false;
                    ReleaseCapture();
                    if (GetWindowRect(hWnd, out RECT r))
                    {
                        _posX = r.left;
                        _posY = r.top;
                        _presetPosition = "custom";
                    }
                    return IntPtr.Zero;
                }
                else if (msg == WM_NCHITTEST)
                {
                    return HTCAPTION; // Surface returns HTCAPTION for instant Windows native mouse dragging!
                }
            }

            if (msg == WM_EXITSIZEMOVE || msg == WM_MOVE || msg == WM_MOVING)
            {
                if (GetWindowRect(hWnd, out RECT rect))
                {
                    _posX = rect.left;
                    _posY = rect.top;
                    _presetPosition = "custom";
                }
            }
            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        public void SetPresetPosition(string preset)
        {
            _presetPosition = preset;
            if (_hwnd != IntPtr.Zero && preset != "custom")
            {
                int screenWidth = GetSystemMetrics(0);
                int screenHeight = GetSystemMetrics(1);
                int width = 400;
                int height = 30;
                if (GetWindowRect(_hwnd, out RECT rect))
                {
                    width = rect.right - rect.left;
                    height = rect.bottom - rect.top;
                }

                switch (preset)
                {
                    case "top_left": _posX = 0; _posY = 0; break;
                    case "top_center": _posX = (screenWidth - width) / 2; _posY = 0; break;
                    case "top_right": _posX = screenWidth - width; _posY = 0; break;
                    case "bottom_left": _posX = 0; _posY = screenHeight - height; break;
                    case "bottom_center": _posX = (screenWidth - width) / 2; _posY = screenHeight - height; break;
                    case "bottom_right": _posX = screenWidth - width; _posY = screenHeight - height; break;
                }

                SetWindowPos(_hwnd, HWND_TOPMOST, _posX, _posY, width, height, 0x0040 | 0x0010);
            }
        }

        public void SetClickThrough(bool clickThrough)
        {
            _isClickThrough = clickThrough;
            if (_hwnd == IntPtr.Zero) return;

            if (GetWindowRect(_hwnd, out RECT curRect))
            {
                _posX = curRect.left;
                _posY = curRect.top;
            }

            int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
            if (clickThrough)
            {
                // Locked: Enable WS_EX_TRANSPARENT and WS_EX_NOACTIVATE so mouse clicks pass through & games keep focus
                exStyle |= (int)(WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);
            }
            else
            {
                // Unlocked: Remove WS_EX_TRANSPARENT and WS_EX_NOACTIVATE so Windows allows mouse dragging!
                exStyle &= ~(int)(WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);
            }
            SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle);

            // SWP_FRAMECHANGED (0x0020) tells Windows DWM to immediately apply style changes!
            SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0020 | 0x0040);
        }

        public void ShowWindow()
        {
            if (_hwnd != IntPtr.Zero)
            {
                ShowWindow(_hwnd, 8); // SW_SHOWNA
            }
        }

        public void HideWindow()
        {
            if (_hwnd != IntPtr.Zero)
            {
                ShowWindow(_hwnd, 0); // SW_HIDE
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool EnumDisplaySettings(string? lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        private const int ENUM_CURRENT_SETTINGS = -1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public ushort dmSpecVersion;
            public ushort dmDriverVersion;
            public ushort dmSize;
            public ushort dmDriverExtra;
            public uint dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public uint dmDisplayOrientation;
            public uint dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public ushort dmLogPixels;
            public uint dmBitsPerPel;
            public uint dmPelsWidth;
            public uint dmPelsHeight;
            public uint dmDisplayFlags;
            public uint dmNup;
            public uint dmDisplayFrequency;
        }

        private int CalculateDisplayFps()
        {
            try
            {
                DEVMODE devMode = new DEVMODE();
                devMode.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));
                if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref devMode))
                {
                    if (devMode.dmDisplayFrequency > 0 && devMode.dmDisplayFrequency < 1000)
                    {
                        return (int)devMode.dmDisplayFrequency;
                    }
                }
            }
            catch { }

            try
            {
                DWM_TIMING_INFO timing = new DWM_TIMING_INFO();
                timing.cbSize = (uint)Marshal.SizeOf(typeof(DWM_TIMING_INFO));
                if (DwmGetCompositionTimingInfo(IntPtr.Zero, ref timing) == 0)
                {
                    if (timing.rateRefresh_den > 0 && timing.rateRefresh_num > 0)
                    {
                        int hz = (int)Math.Round((double)timing.rateRefresh_num / timing.rateRefresh_den);
                        if (hz > 0) return hz;
                    }
                }
            }
            catch { }

            return 240;
        }

        // Correct Win32 COLORREF format: 0x00BBGGRR -> R | (G << 8) | (B << 16)
        private uint ColorToColorRef(byte r, byte g, byte b)
        {
            return (uint)(r | (g << 8) | (b << 16));
        }

        // Dynamic colors for Usage % and Temp °C ONLY
        private uint GetDynamicColorRef(float val, float warnVal, float hotVal)
        {
            if (val >= hotVal)
                return ColorToColorRef(255, 23, 68); // Red #FF1744
            else if (val >= warnVal)
                return ColorToColorRef(255, 193, 7); // Yellow #FFC107
            else
                return ColorToColorRef(0, 230, 118); // Bright Green #00E676
        }

        private void DrawGdiText(IntPtr hdc, string text, ref int drawX, int drawY, uint colorBgr, int spacing, bool isTransparentMode)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (isTransparentMode)
            {
                // Subtle 1px Black Drop Shadow ONLY when background box is transparent (prevents washing out on bright white backgrounds)
                SetTextColor(hdc, ColorToColorRef(0, 0, 0));
                ExtTextOutW(hdc, drawX + 1, drawY + 1, 0, IntPtr.Zero, text, (uint)text.Length, IntPtr.Zero);
            }

            SetTextColor(hdc, colorBgr);
            ExtTextOutW(hdc, drawX, drawY, 0, IntPtr.Zero, text, (uint)text.Length, IntPtr.Zero);

            GetTextExtentPoint32W(hdc, text, text.Length, out SIZE sz);
            drawX += sz.cx + spacing;
        }

        public void UpdateTelemetry(
            bool showFps, bool isGameActive,
            bool showTime, string timeStr,
            bool showCpu, float cpuUsage, float cpuTemp, float cpuPower, float cpuClockGHz, bool showCpuClock, string cpuClockUnit, int cpuFanRpm, bool showCpuFan,
            bool showGpu, float gpuUsage, float gpuTemp, float gpuPower, float gpuClockMHz, bool showGpuClock, string gpuClockUnit, float gpuVramGB, bool showGpuVram, int gpuFanRpm, bool showGpuFan,
            bool showFan, int fanPwm, int fanRpm,
            bool showRam, float ramUsage,
            double transparency, string style)
        {
            if (_hwnd == IntPtr.Zero) return;

            // Compact & Sharp font sizing & 100% symmetrical padding
            int fontHeight = 16;
            int itemSpacing = 16;
            int groupSpacing = 24;
            int sidePadding = 6;
            int topPaddingY = -3;
            int verticalExtraBmp = 0;

            switch (style)
            {
                case "1080":
                    fontHeight = 13;
                    itemSpacing = 16;
                    groupSpacing = 22;
                    sidePadding = 6;
                    topPaddingY = -3;
                    verticalExtraBmp = 0;
                    break;
                case "2K":
                    fontHeight = 16;
                    itemSpacing = 16;
                    groupSpacing = 24;
                    sidePadding = 6;
                    topPaddingY = -3;
                    verticalExtraBmp = 0;
                    break;
                case "4K":
                    fontHeight = 20;
                    itemSpacing = 18;
                    groupSpacing = 30;
                    sidePadding = 8;
                    topPaddingY = -4;
                    verticalExtraBmp = 1;
                    break;
            }

            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);

            // Create Smooth High-Quality Win32 GDI Font (FW_BOLD = 700, ANTIALIASED_QUALITY = 4 for 100% smooth non-jagged font!)
            IntPtr hFont = CreateFontW(-fontHeight, 0, 0, 0, 700 /* FW_BOLD */, 0, 0, 0, 0, 0, 0, 4 /* ANTIALIASED_QUALITY */, 0, "Segoe UI");
            IntPtr oldFont = SelectObject(memDc, hFont);
            SetBkMode(memDc, 1); // TRANSPARENT BK MODE

            int fpsVal = CalculateDisplayFps();

            string fpsLabel = isGameActive ? "FPS |" : "Hz |";

            // Precise Width Measurement simulation pass
            int measuredX = sidePadding;
            if (showFps)
            {
                GetTextExtentPoint32W(memDc, fpsLabel, fpsLabel.Length, out SIZE s1);
                string fpsStr = $"{fpsVal}";
                GetTextExtentPoint32W(memDc, fpsStr, fpsStr.Length, out SIZE s2);
                measuredX += s1.cx + 8 + s2.cx + groupSpacing;
            }
            if (showCpu)
            {
                GetTextExtentPoint32W(memDc, "CPU |", 5, out SIZE sLbl);
                measuredX += sLbl.cx + 8;

                string uStr = $"{cpuUsage:F0}%";
                GetTextExtentPoint32W(memDc, uStr, uStr.Length, out SIZE sU);
                measuredX += sU.cx + itemSpacing;

                string tStr = $"{cpuTemp:F0}°C";
                GetTextExtentPoint32W(memDc, tStr, tStr.Length, out SIZE sT);
                measuredX += sT.cx + itemSpacing;

                if (showCpuClock)
                {
                    string clkStr = cpuClockUnit == "MHz" ? $"{cpuClockGHz * 1000f:F0}MHz" : $"{cpuClockGHz:F2}GHz";
                    GetTextExtentPoint32W(memDc, clkStr, clkStr.Length, out SIZE sClk);
                    measuredX += sClk.cx + itemSpacing;
                }

                string pwrStr = $"{cpuPower:F0}W";
                GetTextExtentPoint32W(memDc, pwrStr, pwrStr.Length, out SIZE sPwr);
                measuredX += sPwr.cx + itemSpacing;

                if (showCpuFan)
                {
                    string fanStr = $"{cpuFanRpm} RPM";
                    GetTextExtentPoint32W(memDc, fanStr, fanStr.Length, out SIZE sFan);
                    measuredX += sFan.cx + itemSpacing;
                }

                measuredX += groupSpacing - itemSpacing;
            }
            if (showGpu)
            {
                GetTextExtentPoint32W(memDc, "GPU |", 5, out SIZE sLbl);
                measuredX += sLbl.cx + 8;

                string uStr = $"{gpuUsage:F0}%";
                GetTextExtentPoint32W(memDc, uStr, uStr.Length, out SIZE sU);
                measuredX += sU.cx + itemSpacing;

                string tStr = $"{gpuTemp:F0}°C";
                GetTextExtentPoint32W(memDc, tStr, tStr.Length, out SIZE sT);
                measuredX += sT.cx + itemSpacing;

                if (showGpuClock)
                {
                    string clkStr = gpuClockUnit == "GHz" ? $"{gpuClockMHz / 1000f:F2}GHz" : $"{gpuClockMHz:F0}MHz";
                    GetTextExtentPoint32W(memDc, clkStr, clkStr.Length, out SIZE sClk);
                    measuredX += sClk.cx + itemSpacing;
                }

                string pwrStr = $"{gpuPower:F0}W";
                GetTextExtentPoint32W(memDc, pwrStr, pwrStr.Length, out SIZE sPwr);
                measuredX += sPwr.cx + itemSpacing;

                if (showGpuVram)
                {
                    string vramStr = $"{gpuVramGB:F1}GB";
                    GetTextExtentPoint32W(memDc, vramStr, vramStr.Length, out SIZE sVram);
                    measuredX += sVram.cx + itemSpacing;
                }

                if (showGpuFan)
                {
                    string fanStr = $"{gpuFanRpm} RPM";
                    GetTextExtentPoint32W(memDc, fanStr, fanStr.Length, out SIZE sFan);
                    measuredX += sFan.cx + itemSpacing;
                }

                measuredX += groupSpacing - itemSpacing;
            }
            if (showFan)
            {
                GetTextExtentPoint32W(memDc, "LLANO FAN |", 11, out SIZE sLbl);
                measuredX += sLbl.cx + 8;

                string pwmStr = $"{fanPwm}%";
                GetTextExtentPoint32W(memDc, pwmStr, pwmStr.Length, out SIZE sPwm);
                measuredX += sPwm.cx + itemSpacing;

                string rpmStr = $"{fanRpm} RPM";
                GetTextExtentPoint32W(memDc, rpmStr, rpmStr.Length, out SIZE sRpm);
                measuredX += sRpm.cx + groupSpacing;
            }
            if (showRam)
            {
                GetTextExtentPoint32W(memDc, "RAM |", 5, out SIZE sLbl);
                measuredX += sLbl.cx + 8;

                string ramStr = $"{ramUsage:F0}%";
                GetTextExtentPoint32W(memDc, ramStr, ramStr.Length, out SIZE sRam);
                measuredX += sRam.cx + groupSpacing;
            }

            // Subtract trailing groupSpacing and add sidePadding right margin (matching sidePadding left margin for 100% pixel-perfect symmetry!)
            int textContentWidth = Math.Max(10, measuredX - groupSpacing);
            int bmpWidth = Math.Max(80, textContentWidth + sidePadding);
            int bmpHeight = fontHeight + verticalExtraBmp;

            // Position calculation (Preserve custom mouse drag position)
            int screenWidth = GetSystemMetrics(0);
            int screenHeight = GetSystemMetrics(1);

            if (!_isClickThrough || _presetPosition == "custom")
            {
                if (GetWindowRect(_hwnd, out RECT curRect))
                {
                    _posX = curRect.left;
                    _posY = curRect.top;
                }
            }
            else
            {
                switch (_presetPosition)
                {
                    case "top_left": _posX = 0; _posY = 0; break;
                    case "top_center": _posX = (screenWidth - bmpWidth) / 2; _posY = 0; break;
                    case "top_right": _posX = screenWidth - bmpWidth; _posY = 0; break;
                    case "bottom_left": _posX = 0; _posY = screenHeight - bmpHeight; break;
                    case "bottom_center": _posX = (screenWidth - bmpWidth) / 2; _posY = screenHeight - bmpHeight; break;
                    case "bottom_right": _posX = screenWidth - bmpWidth; _posY = screenHeight - bmpHeight; break;
                }
            }

            // Create 32bpp ARGB DIBSection
            BITMAPINFO bmi = new BITMAPINFO();
            bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            bmi.bmiHeader.biWidth = bmpWidth;
            bmi.bmiHeader.biHeight = -bmpHeight; // Top-down DIB
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = 0; // BI_RGB

            IntPtr hBmp = CreateDIBSection(memDc, ref bmi, 0, out IntPtr pBits, IntPtr.Zero, 0);
            IntPtr oldBmp = SelectObject(memDc, hBmp);

            // Calculate background alpha: transparency == 1.0 (100%) -> bgAlpha = 0 (100% invisible!), transparency == 0.0 -> bgAlpha = 255 (100% FULL SOLID BLACK!)
            byte bgAlpha = (byte)Math.Clamp((int)((1.0 - transparency) * 255.0), 0, 255);

            // CRITICAL FIX: When unlocked for mouse dragging, ensure a minimum background alpha of 120 so Windows DWM captures mouse clicks across 100% of the HUD surface!
            if (!_isClickThrough)
            {
                bgAlpha = Math.Max((byte)120, bgAlpha);
            }

            // Fill background pixels in DIBSection memory
            int pixelCount = bmpWidth * bmpHeight;
            uint bgPixel = (uint)(bgAlpha << 24 | 14 << 16 | 18 << 8 | 26);
            if (bgAlpha == 0 && _isClickThrough) bgPixel = 0;

            unsafe
            {
                uint* pixels = (uint*)pBits.ToPointer();
                for (int i = 0; i < pixelCount; i++)
                {
                    pixels[i] = bgPixel;
                }

                // If unlocked, draw a crisp cyan drag box outline so user has a 100% clear visual drag handle!
                if (!_isClickThrough)
                {
                    uint borderPixel = 0xFF00D2FF; // Solid Cyan #00D2FF
                    for (int x = 0; x < bmpWidth; x++)
                    {
                        pixels[x] = borderPixel; // Top border
                        pixels[(bmpHeight - 1) * bmpWidth + x] = borderPixel; // Bottom border
                    }
                    for (int y = 0; y < bmpHeight; y++)
                    {
                        pixels[y * bmpWidth] = borderPixel; // Left border
                        pixels[y * bmpWidth + (bmpWidth - 1)] = borderPixel; // Right border
                    }
                }
            }

            // Standard Colors (Correct COLORREF format: R | (G << 8) | (B << 16))
            uint labelColorRef = ColorToColorRef(220, 225, 235); // Crisp Bright Off-White title labels
            uint whiteColorRef = ColorToColorRef(255, 255, 255); // Solid White
            uint cyanColorRef = ColorToColorRef(0, 210, 255);  // Solid Cyan for Power & Clock
            uint fanCyanColorRef = ColorToColorRef(0, 229, 255); // Solid Bright Cyan for Fan
            uint vramColorRef = ColorToColorRef(56, 189, 248);   // Solid Sky Blue for VRAM

            int drawX = sidePadding;
            int drawY = topPaddingY;
            bool isTransparentMode = (bgAlpha < 50);

            // 1. FPS / Hz
            if (showFps)
            {
                DrawGdiText(memDc, fpsLabel, ref drawX, drawY, labelColorRef, 8, isTransparentMode);
                uint fpsColor = GetDynamicColorRef(fpsVal >= 50 ? 40 : 80, 50f, 80f);
                DrawGdiText(memDc, $"{fpsVal}", ref drawX, drawY, fpsColor, groupSpacing, isTransparentMode);
            }

            // 2. CPU
            if (showCpu)
            {
                DrawGdiText(memDc, "CPU |", ref drawX, drawY, labelColorRef, 8, isTransparentMode);
                
                // CPU Usage % (Dynamic)
                uint cpuUColor = GetDynamicColorRef(cpuUsage, 50f, 80f);
                DrawGdiText(memDc, $"{cpuUsage:F0}%", ref drawX, drawY, cpuUColor, itemSpacing, isTransparentMode);

                // CPU Temp °C (Dynamic)
                uint cpuTColor = GetDynamicColorRef(cpuTemp, 65f, 80f);
                DrawGdiText(memDc, $"{cpuTemp:F0}°C", ref drawX, drawY, cpuTColor, itemSpacing, isTransparentMode);

                // CPU Clock (GHz / MHz)
                if (showCpuClock)
                {
                    string cpuClkStr = cpuClockUnit == "MHz" ? $"{cpuClockGHz * 1000f:F0}MHz" : $"{cpuClockGHz:F2}GHz";
                    DrawGdiText(memDc, cpuClkStr, ref drawX, drawY, cyanColorRef, itemSpacing, isTransparentMode);
                }

                // CPU Power W (Fixed Cyan)
                DrawGdiText(memDc, $"{cpuPower:F0}W", ref drawX, drawY, cyanColorRef, itemSpacing, isTransparentMode);

                // CPU Fan RPM (Fixed Fan Cyan)
                if (showCpuFan) DrawGdiText(memDc, $"{cpuFanRpm} RPM", ref drawX, drawY, fanCyanColorRef, itemSpacing, isTransparentMode);

                drawX += groupSpacing - itemSpacing;
            }

            // 3. GPU
            if (showGpu)
            {
                DrawGdiText(memDc, "GPU |", ref drawX, drawY, labelColorRef, 8, isTransparentMode);

                // GPU Usage % (Dynamic)
                uint gpuUColor = GetDynamicColorRef(gpuUsage, 50f, 80f);
                DrawGdiText(memDc, $"{gpuUsage:F0}%", ref drawX, drawY, gpuUColor, itemSpacing, isTransparentMode);

                // GPU Temp °C (Dynamic)
                uint gpuTColor = GetDynamicColorRef(gpuTemp, 65f, 80f);
                DrawGdiText(memDc, $"{gpuTemp:F0}°C", ref drawX, drawY, gpuTColor, itemSpacing, isTransparentMode);

                // GPU Clock (MHz / GHz)
                if (showGpuClock)
                {
                    string gpuClkStr = gpuClockUnit == "GHz" ? $"{gpuClockMHz / 1000f:F2}GHz" : $"{gpuClockMHz:F0}MHz";
                    DrawGdiText(memDc, gpuClkStr, ref drawX, drawY, cyanColorRef, itemSpacing, isTransparentMode);
                }

                // GPU Power W (Fixed Cyan)
                DrawGdiText(memDc, $"{gpuPower:F0}W", ref drawX, drawY, cyanColorRef, itemSpacing, isTransparentMode);

                // GPU VRAM GB (Fixed Sky Blue)
                if (showGpuVram) DrawGdiText(memDc, $"{gpuVramGB:F1}GB", ref drawX, drawY, vramColorRef, itemSpacing, isTransparentMode);

                // GPU Fan RPM (Fixed Fan Cyan)
                if (showGpuFan) DrawGdiText(memDc, $"{gpuFanRpm} RPM", ref drawX, drawY, fanCyanColorRef, itemSpacing, isTransparentMode);

                drawX += groupSpacing - itemSpacing;
            }

            // 4. LLANO FAN
            if (showFan)
            {
                DrawGdiText(memDc, "LLANO FAN |", ref drawX, drawY, labelColorRef, 8, isTransparentMode);
                DrawGdiText(memDc, $"{fanPwm}%", ref drawX, drawY, fanCyanColorRef, itemSpacing, isTransparentMode);
                DrawGdiText(memDc, $"{fanRpm} RPM", ref drawX, drawY, fanCyanColorRef, groupSpacing, isTransparentMode);
            }

            // 5. RAM
            if (showRam)
            {
                DrawGdiText(memDc, "RAM |", ref drawX, drawY, labelColorRef, 8, isTransparentMode);
                
                // RAM Usage % (Dynamic)
                uint ramColor = GetDynamicColorRef(ramUsage, 60f, 85f);
                DrawGdiText(memDc, $"{ramUsage:F0}%", ref drawX, drawY, ramColor, groupSpacing, isTransparentMode);
            }

            // Post-process DIBSection pixels for 100% solid alpha on text pixels
            unsafe
            {
                uint* pixels = (uint*)pBits.ToPointer();
                for (int i = 0; i < pixelCount; i++)
                {
                    uint px = pixels[i];
                    if (px != bgPixel && px != 0)
                    {
                        pixels[i] = px | 0xFF000000;
                    }
                }
            }

            // Send DIBSection ARGB directly to DWM via Win32 UpdateLayeredWindow
            POINT ptDst = new POINT { x = _posX, y = _posY };
            SIZE sizeDst = new SIZE { cx = bmpWidth, cy = bmpHeight };
            POINT ptSrc = new POINT { x = 0, y = 0 };

            BLENDFUNCTION blend = new BLENDFUNCTION
            {
                BlendOp = AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = AC_SRC_ALPHA
            };

            UpdateLayeredWindow(_hwnd, screenDc, ref ptDst, ref sizeDst, memDc, ref ptSrc, 0, ref blend, ULW_ALPHA);

            // Resize & position window dynamically without overriding active mouse drag
            if (_isClickThrough && _presetPosition != "custom")
            {
                SetWindowPos(_hwnd, HWND_TOPMOST, _posX, _posY, bmpWidth, bmpHeight, 0x0040 | 0x0010);
            }
            else
            {
                SetWindowPos(_hwnd, HWND_TOPMOST, _posX, _posY, bmpWidth, bmpHeight, 0x0040 | 0x0010);
            }

            // Cleanup GDI objects
            SelectObject(memDc, oldFont);
            DeleteObject(hFont);
            SelectObject(memDc, oldBmp);
            DeleteObject(hBmp);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }

        public void Dispose()
        {
            if (_hwnd != IntPtr.Zero)
            {
                DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }
        }
    }
}
