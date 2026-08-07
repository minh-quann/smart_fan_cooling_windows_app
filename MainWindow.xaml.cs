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
            this.Title = "Llano Smart Fan Cooling System - WinUI 3 Native";
            ViewModel = new MainViewModel();
            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.DataContext = ViewModel;
            }
        }
    }
}
