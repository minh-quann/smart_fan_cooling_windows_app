using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartFanCooling.ViewModels;
using SmartFanCooling.Models;

namespace SmartFanCooling.Views
{
    public sealed partial class EspConnectionDialogView : UserControl
    {
        public EspConnectionDialogView()
        {
            this.InitializeComponent();
        }

        private void BleDeviceBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is BleDeviceItem device && DataContext is MainViewModel vm)
            {
                vm.ConnectBleDevice(device);
            }
        }
    }
}
