using Windows.UI;
using Microsoft.UI.Xaml.Media;

namespace SmartFanCooling.Styles
{
    /// <summary>
    /// Full-Spectrum Design System Color Palette (Numeric 50-950 Scales).
    /// Mirror of Styles/Colors.xaml.
    /// Standard Tailwind CSS / Radix UI / Windows Fluent Palette System.
    /// </summary>
    public static class AppColors
    {
        // ===================================================================
        // 1. NEUTRAL & DARK CYBER GRAY SCALE
        // ===================================================================
        public const string Gray50Hex  = "#FFFFFF";
        public const string Gray100Hex = "#E2E8F0";
        public const string Gray200Hex = "#C4C9D4";
        public const string Gray300Hex = "#8A92A6";
        public const string Gray400Hex = "#5A6B85";
        public const string Gray500Hex = "#3B4A60";
        public const string Gray600Hex = "#26364D";
        public const string Gray700Hex = "#1C2638";
        public const string Gray800Hex = "#141E2B";
        public const string Gray900Hex = "#0D111A";
        public const string Gray950Hex = "#0B111E";

        public static Color Gray50  => Color.FromArgb(255, 255, 255, 255);
        public static Color Gray100 => Color.FromArgb(255, 226, 232, 240);
        public static Color Gray200 => Color.FromArgb(255, 196, 201, 212);
        public static Color Gray300 => Color.FromArgb(255, 138, 146, 166);
        public static Color Gray400 => Color.FromArgb(255, 90, 107, 133);
        public static Color Gray500 => Color.FromArgb(255, 59, 74, 96);
        public static Color Gray600 => Color.FromArgb(255, 38, 54, 77);
        public static Color Gray700 => Color.FromArgb(255, 28, 38, 56);
        public static Color Gray800 => Color.FromArgb(255, 20, 30, 43);
        public static Color Gray900 => Color.FromArgb(255, 13, 17, 26);
        public static Color Gray950 => Color.FromArgb(255, 11, 17, 30);

        public static SolidColorBrush Gray50Brush  { get; } = new SolidColorBrush(Gray50);
        public static SolidColorBrush Gray300Brush { get; } = new SolidColorBrush(Gray300);
        public static SolidColorBrush Gray500Brush { get; } = new SolidColorBrush(Gray500);
        public static SolidColorBrush Gray700Brush { get; } = new SolidColorBrush(Gray700);
        public static SolidColorBrush Gray800Brush { get; } = new SolidColorBrush(Gray800);
        public static SolidColorBrush Gray900Brush { get; } = new SolidColorBrush(Gray900);
        public static SolidColorBrush Gray950Brush { get; } = new SolidColorBrush(Gray950);

        // ===================================================================
        // 2. RED SCALE (Pure Vivid Red)
        // ===================================================================
        public const string Red500Hex = "#EF4444";
        public const string Red600Hex = "#DC2626";
        public static Color Red500 => Color.FromArgb(255, 239, 68, 68);
        public static SolidColorBrush Red500Brush { get; } = new SolidColorBrush(Red500);

        // ===================================================================
        // 3. ORANGE SCALE (Vivid Orange)
        // ===================================================================
        public const string Orange500Hex = "#F97316";
        public const string Orange600Hex = "#EA580C";
        public static Color Orange500 => Color.FromArgb(255, 249, 115, 22);
        public static SolidColorBrush Orange500Brush { get; } = new SolidColorBrush(Orange500);

        // ===================================================================
        // 4. AMBER SCALE (Warm Amber / Yellow-Orange)
        // ===================================================================
        public const string Amber500Hex = "#FFB300";
        public const string Amber600Hex = "#D97706";
        public static Color Amber500 => Color.FromArgb(255, 255, 179, 0);
        public static SolidColorBrush Amber500Brush { get; } = new SolidColorBrush(Amber500);

        // ===================================================================
        // 5. YELLOW SCALE (Pure Bright Yellow)
        // ===================================================================
        public const string Yellow500Hex = "#EAB308";
        public static Color Yellow500 => Color.FromArgb(255, 234, 179, 8);
        public static SolidColorBrush Yellow500Brush { get; } = new SolidColorBrush(Yellow500);

        // ===================================================================
        // 6. LIME SCALE (Neon Electric Lime)
        // ===================================================================
        public const string Lime500Hex = "#84CC16";
        public static Color Lime500 => Color.FromArgb(255, 132, 204, 22);
        public static SolidColorBrush Lime500Brush { get; } = new SolidColorBrush(Lime500);

        // ===================================================================
        // 7. GREEN SCALE (Standard Pure Green)
        // ===================================================================
        public const string Green500Hex = "#22C55E";
        public static Color Green500 => Color.FromArgb(255, 34, 197, 94);
        public static SolidColorBrush Green500Brush { get; } = new SolidColorBrush(Green500);

        // ===================================================================
        // 8. EMERALD SCALE (Cyber Neon Mint / Emerald)
        // ===================================================================
        public const string Emerald500Hex = "#00FF88";
        public const string Emerald600Hex = "#059669";
        public static Color Emerald500 => Color.FromArgb(255, 0, 255, 136);
        public static SolidColorBrush Emerald500Brush { get; } = new SolidColorBrush(Emerald500);

        // ===================================================================
        // 9. CYAN ACCENT SCALE (Primary Cyber HUD Accent)
        // ===================================================================
        public const string Cyan500Hex = "#00F0FF";
        public const string Cyan600Hex = "#00C4D4";
        public static Color Cyan500 => Color.FromArgb(255, 0, 240, 255);
        public static SolidColorBrush Cyan500Brush { get; } = new SolidColorBrush(Cyan500);

        // ===================================================================
        // 10. SKY SCALE (Light Blue)
        // ===================================================================
        public const string Sky500Hex = "#0EA5E9";
        public static Color Sky500 => Color.FromArgb(255, 14, 165, 233);
        public static SolidColorBrush Sky500Brush { get; } = new SolidColorBrush(Sky500);

        // ===================================================================
        // 11. BLUE SCALE (Standard Royal Blue)
        // ===================================================================
        public const string Blue500Hex = "#3B82F6";
        public static Color Blue500 => Color.FromArgb(255, 59, 130, 246);
        public static SolidColorBrush Blue500Brush { get; } = new SolidColorBrush(Blue500);

        // ===================================================================
        // 12. INDIGO SCALE
        // ===================================================================
        public const string Indigo500Hex = "#6366F1";
        public static Color Indigo500 => Color.FromArgb(255, 99, 102, 241);
        public static SolidColorBrush Indigo500Brush { get; } = new SolidColorBrush(Indigo500);

        // ===================================================================
        // 13. VIOLET SCALE
        // ===================================================================
        public const string Violet500Hex = "#8B5CF6";
        public static Color Violet500 => Color.FromArgb(255, 139, 92, 246);
        public static SolidColorBrush Violet500Brush { get; } = new SolidColorBrush(Violet500);

        // ===================================================================
        // 14. PURPLE SCALE
        // ===================================================================
        public const string Purple500Hex = "#A855F7";
        public static Color Purple500 => Color.FromArgb(255, 168, 85, 247);
        public static SolidColorBrush Purple500Brush { get; } = new SolidColorBrush(Purple500);

        // ===================================================================
        // 15. PINK SCALE
        // ===================================================================
        public const string Pink500Hex = "#EC4899";
        public static Color Pink500 => Color.FromArgb(255, 236, 72, 153);
        public static SolidColorBrush Pink500Brush { get; } = new SolidColorBrush(Pink500);

        // ===================================================================
        // 16. ROSE SCALE (Pink-Red Cyber Neon)
        // ===================================================================
        public const string Rose500Hex = "#FF2A6D";
        public static Color Rose500 => Color.FromArgb(255, 255, 42, 109);
        public static SolidColorBrush Rose500Brush { get; } = new SolidColorBrush(Rose500);

        // ===================================================================
        // HELPER UTILITIES
        // ===================================================================

        /// <summary>
        /// Evaluates fan status color hex based on RPM and PWM values.
        /// </summary>
        public static string GetFanStatusColorHex(int rpm, int pwm)
        {
            if (rpm <= 0) return Gray300Hex;
            if (pwm <= 35) return Cyan500Hex;
            if (pwm <= 65) return Emerald500Hex;
            if (pwm <= 85) return Amber500Hex;
            return Rose500Hex;
        }
    }
}
