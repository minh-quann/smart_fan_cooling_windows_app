namespace SmartFanCooling.Services.Interfaces
{
    /// <summary>
    /// Contract for generating 128x64 monochrome bitmap graphics
    /// and encoding them into Hex streams for OLED displays.
    /// </summary>
    public interface IOledCanvasService
    {
        string GenerateOled1CustomCanvas(string title, string mainStat, string subStat, int scale, bool showBar, int barPercent);
        string GenerateOled2CustomCanvas(string headerText, string cpuInfo, string gpuInfo, string footerText);
        string GenerateDynamicOledCanvas(string row1, string row2, string row3, string row4, int rowCount, bool showTopDivider, bool showBottomDivider, bool showProgressBar, int pwmPercent);
    }
}
