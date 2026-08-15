using System;
using System.Linq;
using Microsoft.UI.Xaml;
using SmartFanCooling.Services;

namespace SmartFanCooling
{
    /// <summary>
    /// Application entrypoint & global state manager.
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
        /// Invoked when the application is launched normally by the end user or Windows auto-start.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                var settings = AppSettingsService.LoadSettings();
                string[] cmdArgs = Environment.GetCommandLineArgs();
                bool isAutoStart = cmdArgs.Any(a => a.Equals("/autostart", StringComparison.OrdinalIgnoreCase) ||
                                                     a.Equals("-autostart", StringComparison.OrdinalIgnoreCase) ||
                                                     a.Equals("/minimized", StringComparison.OrdinalIgnoreCase));

                bool startHidden = isAutoStart && settings.StartMinimizedToTray;
                MainWindowInstance = new MainWindow(startHidden);

                if (!startHidden)
                {
                    MainWindowInstance.Activate();
                }
            }
            catch (Exception ex)
            {
                string log = $"{DateTime.Now}: OnLaunched Error: {ex.Message}\n{ex}\n";
                System.IO.File.WriteAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "crash.log"), log);
            }
        }
    }
}
