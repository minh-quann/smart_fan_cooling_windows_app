using Microsoft.UI.Xaml;
using SmartFanCooling.ViewModels;

namespace SmartFanCooling
{
    /// <summary>
    /// Main Window for Smart Fan Cooling Application.
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            this.InitializeComponent();
            ViewModel = new MainViewModel();
            this.Content.DataContext = ViewModel;
        }
    }
}
