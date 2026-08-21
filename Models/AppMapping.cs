using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartFanCooling.Models
{
    public partial class AppMapping : ObservableObject
    {
        [ObservableProperty] private string _appName = "";
        [ObservableProperty] private string _executablePath = "";
        [ObservableProperty] private string _processName = "";
        [ObservableProperty] private string _profileId = "";
        [ObservableProperty] private string _profileName = "";
        [ObservableProperty] private bool _isEnabled = true;
    }
}
