using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartFanCooling.Models
{
    public partial class RpmPreset : ObservableObject
    {
        [ObservableProperty] private string _label = "";
        [ObservableProperty] private int _rpm = 0;

        public RpmPreset() { }

        public RpmPreset(string label, int rpm)
        {
            _label = label;
            _rpm = rpm;
        }
    }
}
