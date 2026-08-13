using System;
using System.Diagnostics;

namespace SmartFanCooling.Services
{
    /// <summary>
    /// Manages Windows auto-start via Task Scheduler (schtasks.exe).
    /// Uses a Scheduled Task with HIGHEST run level so the app launches elevated on boot
    /// without needing the UAC prompt workaround through Registry + AppCompatFlags.
    /// </summary>
    public static class StartupService
    {
        private const string TaskName = "SmartFanCoolingAutoStart";

        /// <summary>
        /// Checks whether the auto-start scheduled task currently exists.
        /// </summary>
        public static bool IsStartupTaskRegistered()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Query /TN \"{TaskName}\" /FO LIST",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(5000);
                return proc?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Creates a Windows Scheduled Task that runs the app at user logon with highest privileges.
        /// </summary>
        public static bool EnableStartup()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName
                    ?? System.IO.Path.Combine(AppContext.BaseDirectory, "smart_fan_cooling_windows_app.exe");

                // Remove existing task first to avoid duplicates
                DisableStartup();

                // Build XML task definition for schtasks import (allows setting RunLevel = Highest)
                string taskXml = BuildTaskXml(exePath);
                string tempXmlPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{TaskName}.xml");
                System.IO.File.WriteAllText(tempXmlPath, taskXml);

                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Create /TN \"{TaskName}\" /XML \"{tempXmlPath}\" /F",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(10000);

                // Clean up temp XML file
                try { System.IO.File.Delete(tempXmlPath); } catch { }

                return proc?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Removes the auto-start scheduled task.
        /// </summary>
        public static bool DisableStartup()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Delete /TN \"{TaskName}\" /F",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(5000);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Builds a Windows Task Scheduler XML definition that triggers at user logon
        /// and runs with highest privileges (admin elevation).
        /// </summary>
        private static string BuildTaskXml(string exePath)
        {
            string workingDir = System.IO.Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
            // Use current user SID for the logon trigger
            string userId = System.Security.Principal.WindowsIdentity.GetCurrent().Name;

            return $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.4"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>Smart Fan Cooling Hub - Auto start at Windows logon with admin privileges.</Description>
    <Author>{EscapeXml(userId)}</Author>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <UserId>{EscapeXml(userId)}</UserId>
      <Delay>PT5S</Delay>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <UserId>{EscapeXml(userId)}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>5</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{EscapeXml(exePath)}</Command>
      <WorkingDirectory>{EscapeXml(workingDir)}</WorkingDirectory>
    </Exec>
  </Actions>
</Task>";
        }

        /// <summary>
        /// Escapes special XML characters in strings for safe embedding.
        /// </summary>
        private static string EscapeXml(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }
}
