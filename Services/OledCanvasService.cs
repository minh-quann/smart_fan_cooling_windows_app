using System;
using System.Text;

namespace smart_fan_cooling_windows_app.Services
{
    /// <summary>
    /// Service for generating 128x64 monochrome bitmap graphics in C#
    /// and encoding them into Hex streams for independent OLED 1 (1.3") and OLED 2 (0.96") displays.
    /// </summary>
    public class OledCanvasService
    {
        public const int Width = 128;
        public const int Height = 64;
        public const int BufferSize = (Width * Height) / 8; // 1024 bytes

        // Basic 5x7 ASCII font table subset for 1-bit rendering
        private static readonly byte[][] BasicFont = new byte[128][];

        static OledCanvasService()
        {
            InitializeFont();
        }

        private static void InitializeFont()
        {
            // Fallback font init for basic ASCII characters
            for (int i = 0; i < 128; i++)
            {
                BasicFont[i] = new byte[5] { 0x00, 0x00, 0x00, 0x00, 0x00 };
            }

            // Fill digits '0'-'9'
            BasicFont['0'] = new byte[5] { 0x3E, 0x51, 0x49, 0x45, 0x3E };
            BasicFont['1'] = new byte[5] { 0x00, 0x42, 0x7F, 0x40, 0x00 };
            BasicFont['2'] = new byte[5] { 0x42, 0x61, 0x51, 0x49, 0x46 };
            BasicFont['3'] = new byte[5] { 0x21, 0x41, 0x45, 0x4B, 0x31 };
            BasicFont['4'] = new byte[5] { 0x18, 0x14, 0x12, 0x7F, 0x10 };
            BasicFont['5'] = new byte[5] { 0x27, 0x45, 0x45, 0x45, 0x39 };
            BasicFont['6'] = new byte[5] { 0x3C, 0x4A, 0x49, 0x49, 0x30 };
            BasicFont['7'] = new byte[5] { 0x01, 0x71, 0x09, 0x05, 0x03 };
            BasicFont['8'] = new byte[5] { 0x36, 0x49, 0x49, 0x49, 0x36 };
            BasicFont['9'] = new byte[5] { 0x06, 0x49, 0x49, 0x29, 0x1E };
            BasicFont[' '] = new byte[5] { 0x00, 0x00, 0x00, 0x00, 0x00 };
            BasicFont[':'] = new byte[5] { 0x00, 0x36, 0x36, 0x00, 0x00 };
            BasicFont['%'] = new byte[5] { 0x23, 0x13, 0x08, 0x64, 0x62 };
            BasicFont['C'] = new byte[5] { 0x3E, 0x41, 0x41, 0x41, 0x22 };
            BasicFont['P'] = new byte[5] { 0x7F, 0x09, 0x09, 0x09, 0x06 };
            BasicFont['U'] = new byte[5] { 0x3F, 0x40, 0x40, 0x40, 0x3F };
            BasicFont['G'] = new byte[5] { 0x3E, 0x41, 0x49, 0x49, 0x7A };
            BasicFont['R'] = new byte[5] { 0x7F, 0x09, 0x19, 0x29, 0x46 };
            BasicFont['M'] = new byte[5] { 0x7F, 0x02, 0x0C, 0x02, 0x7F };
            BasicFont['L'] = new byte[5] { 0x7F, 0x40, 0x40, 0x40, 0x40 };
            BasicFont['A'] = new byte[5] { 0x7C, 0x12, 0x11, 0x12, 0x7C };
            BasicFont['N'] = new byte[5] { 0x7F, 0x04, 0x08, 0x10, 0x7F };
            BasicFont['O'] = new byte[5] { 0x3E, 0x41, 0x41, 0x41, 0x3E };
            BasicFont['W'] = new byte[5] { 0x3F, 0x40, 0x38, 0x40, 0x3F };
            BasicFont['F'] = new byte[5] { 0x7F, 0x09, 0x09, 0x09, 0x01 };
            BasicFont['T'] = new byte[5] { 0x01, 0x01, 0x7F, 0x01, 0x01 };
            BasicFont['-'] = new byte[5] { 0x08, 0x08, 0x08, 0x08, 0x08 };
        }

        /// <summary>
        /// Set a pixel in the 128x64 1-bit buffer (Adafruit SSD1306 / SH1106 memory layout)
        /// </summary>
        public static void SetPixel(byte[] buffer, int x, int y, bool color)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return;
            int page = y / 8;
            int bit = y % 8;
            int index = x + (page * Width);

            if (color)
                buffer[index] |= (byte)(1 << bit);
            else
                buffer[index] &= (byte)~(1 << bit);
        }

        /// <summary>
        /// Draw text string with scaling factor
        /// </summary>
        public static void DrawString(byte[] buffer, int x, int y, string text, int scale = 1)
        {
            if (string.IsNullOrEmpty(text)) return;
            int cursorX = x;

            foreach (char c in text)
            {
                char ch = (c < 128) ? c : '?';
                byte[] glyph = BasicFont[ch] ?? BasicFont['?'];

                for (int col = 0; col < 5; col++)
                {
                    byte b = glyph[col];
                    for (int row = 0; row < 7; row++)
                    {
                        bool pixelOn = ((b >> row) & 1) == 1;
                        if (pixelOn)
                        {
                            for (int sx = 0; sx < scale; sx++)
                            {
                                for (int sy = 0; sy < scale; sy++)
                                {
                                    SetPixel(buffer, cursorX + (col * scale) + sx, y + (row * scale) + sy, true);
                                }
                            }
                        }
                    }
                }
                cursorX += 6 * scale;
            }
        }

        /// <summary>
        /// Draw horizontal line
        /// </summary>
        public static void DrawHLine(byte[] buffer, int x, int y, int length)
        {
            for (int i = 0; i < length; i++)
            {
                SetPixel(buffer, x + i, y, true);
            }
        }

        /// <summary>
        /// Draw rectangle frame
        /// </summary>
        public static void DrawRect(byte[] buffer, int x, int y, int w, int h)
        {
            DrawHLine(buffer, x, y, w);
            DrawHLine(buffer, x, y + h - 1, w);
            for (int i = 0; i < h; i++)
            {
                SetPixel(buffer, x, y + i, true);
                SetPixel(buffer, x + w - 1, y + i, true);
            }
        }

        /// <summary>
        /// Fill rectangle
        /// </summary>
        public static void FillRect(byte[] buffer, int x, int y, int w, int h)
        {
            for (int i = 0; i < w; i++)
            {
                for (int j = 0; j < h; j++)
                {
                    SetPixel(buffer, x + i, y + j, true);
                }
            }
        }

        /// <summary>
        /// Convert 1024-byte buffer into 2048-character Hex string for fast USB Serial transmission
        /// </summary>
        public static string BufferToHex(byte[] buffer)
        {
            StringBuilder sb = new StringBuilder(buffer.Length * 2);
            foreach (byte b in buffer)
            {
                sb.AppendFormat("{0:X2}", b);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Generate independent Custom Bitmap Hex for OLED 1 (1.3" Screen)
        /// </summary>
        public string GenerateOled1CustomCanvas(string title, string mainStat, string subStat, int scale, bool showBar, int barPercent)
        {
            byte[] buf = new byte[BufferSize];

            // 1. Draw Title bar
            DrawString(buf, 0, 0, title.ToUpper(), 1);
            DrawHLine(buf, 0, 10, Width);

            // 2. Draw Main stat (Size 2 or 3)
            DrawString(buf, 0, 16, mainStat, scale);

            // 3. Draw Sub stat (Size 1 or 2)
            DrawString(buf, 70, 16, subStat, 1);

            // 4. Draw optional progress bar
            if (showBar)
            {
                DrawHLine(buf, 0, 42, Width);
                DrawString(buf, 0, 46, "PWM:", 1);
                DrawRect(buf, 30, 45, 82, 8);
                int fillW = (Math.Clamp(barPercent, 0, 100) * 80) / 100;
                if (fillW > 0) FillRect(buf, 31, 46, fillW, 6);
            }

            return BufferToHex(buf);
        }

        /// <summary>
        /// Generate independent Custom Bitmap Hex for OLED 2 (0.96" Screen)
        /// </summary>
        public string GenerateOled2CustomCanvas(string headerText, string cpuInfo, string gpuInfo, string footerText)
        {
            byte[] buf = new byte[BufferSize];

            // 1. Top Header Zone (Large text)
            DrawString(buf, 0, 2, headerText, 2);

            // 2. Mid Zone - CPU Stats
            DrawString(buf, 0, 22, cpuInfo, 1);

            // 3. Mid Zone - GPU Stats
            DrawString(buf, 0, 38, gpuInfo, 1);

            // 4. Bottom Zone - Status Footer
            DrawHLine(buf, 0, 50, Width);
            DrawString(buf, 0, 54, footerText, 1);

            return BufferToHex(buf);
        }
    }
}
