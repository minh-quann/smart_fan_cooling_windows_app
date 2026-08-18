using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.UI.Xaml;
using SmartFanCooling.Services;

namespace SmartFanCooling
{
    /// <summary>
    /// Application entrypoint & global state manager.
    /// Enforces single-instance via named Mutex — second launch brings existing window to foreground.
    /// </summary>
    public partial class App : Application
    {
        public static Window? MainWindowInstance { get; private set; }

        // Named Mutex for single-instance enforcement
        private const string MUTEX_NAME = "Global\\SmartFanCoolingHub_SingleInstance";
        private static Mutex? _instanceMutex;

        // Win32 APIs to find and activate existing window
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;

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
            // Single-instance check: if another instance is already running, bring it to foreground and exit
            _instanceMutex = new Mutex(true, MUTEX_NAME, out bool isNewInstance);
            if (!isNewInstance)
            {
                BringExistingInstanceToForeground();
                System.Environment.Exit(0);
                return;
            }

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

        /// <summary>
        /// Finds the existing MainWindow by title and brings it to the foreground.
        /// Works even when the window is hidden in system tray or minimized.
        /// </summary>
        private static void BringExistingInstanceToForeground()
        {
            IntPtr hWnd = FindWindow(null, "Smart Fan Cooling Hub");
            if (hWnd != IntPtr.Zero)
            {
                // Restore if minimized, then bring to foreground
                if (IsIconic(hWnd))
                {
                    ShowWindow(hWnd, SW_RESTORE);
                }
                else
                {
                    ShowWindow(hWnd, SW_SHOW);
                }
                SetForegroundWindow(hWnd);
            }
        }
    }
}
