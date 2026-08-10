using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartFanCooling.ViewModels;
using SmartFanCooling.Models;

namespace SmartFanCooling.Views
{
    public sealed partial class AppProfilesView : UserControl
    {
        public AppProfilesView()
        {
            this.InitializeComponent();
        }

        private MainViewModel? ViewModel => DataContext as MainViewModel;

        private void RemoveAppBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is AppMapping mapping && ViewModel != null)
            {
                ViewModel.RemoveAppMapping(mapping);
            }
        }
    }
}
