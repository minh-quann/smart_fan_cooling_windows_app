using Microsoft.UI.Xaml;

namespace SmartFanCooling
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public static Window? MainWindowInstance { get; private set; }

        public App()
        {
            this.UnhandledException += (sender, e) =>
            {
                e.Handled = true;
                string log = $"{DateTime.Now}: {e.Message}\n{e.Exception}\n";
                System.IO.File.WriteAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "crash.log"), log);
            };
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                MainWindowInstance = new MainWindow();
                MainWindowInstance.Activate();
            }
            catch (Exception ex)
            {
                string log = $"{DateTime.Now}: OnLaunched Error: {ex.Message}\n{ex}\n";
                System.IO.File.WriteAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "crash.log"), log);
            }
        }
    }
}
