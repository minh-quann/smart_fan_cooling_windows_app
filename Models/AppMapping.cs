namespace SmartFanCooling.Models
{
    public class AppMapping
    {
        public string AppName { get; set; } = "";
        public string ExecutablePath { get; set; } = "";
        public string ProcessName { get; set; } = "";
        public string ProfileId { get; set; } = "";
        public string ProfileName { get; set; } = "";
        public bool IsEnabled { get; set; } = true;
    }
}
