using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace SmartFanCooling.Services
{
    /// <summary>
    /// Manages Windows auto-start via Task Scheduler (schtasks.exe).
    /// Supports configurable startup priority (High / Admin elevation, Normal, Low/Delayed).
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
        /// Creates a Windows Scheduled Task with configurable launch priority level.
        /// </summary>
        public static bool EnableStartup(string priorityLevel = "Cao (Khởi động trước - High Priority)")
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName
                    ?? Path.Combine(AppContext.BaseDirectory, "smart_fan_cooling_windows_app.exe");

                // Remove existing task first to avoid duplicates
                DisableStartup();

                // Build XML task definition for schtasks import with specified startup priority
                string taskXml = BuildTaskXml(exePath, priorityLevel);
                string tempXmlPath = Path.Combine(Path.GetTempPath(), $"{TaskName}.xml");
                File.WriteAllText(tempXmlPath, taskXml);

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
                try { File.Delete(tempXmlPath); } catch { }

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
        /// Builds a Windows Task Scheduler XML definition with target startup priority settings.
        /// </summary>
        private static string BuildTaskXml(string exePath, string priorityLevel)
        {
            string workingDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
            string userId = WindowsIdentity.GetCurrent().Name;

            // Configure delay, task execution priority, and admin elevation based on priority setting
            string delay = "PT1S";
            string taskPriority = "2";
            string runLevel = "HighestAvailable";

            if (priorityLevel != null && priorityLevel.StartsWith("Bình thường", StringComparison.OrdinalIgnoreCase))
            {
                delay = "PT5S";
                taskPriority = "5";
                runLevel = "HighestAvailable";
            }
            else if (priorityLevel != null && priorityLevel.StartsWith("Trì hoãn", StringComparison.OrdinalIgnoreCase))
            {
                delay = "PT15S";
                taskPriority = "7";
                runLevel = "LeastPrivilege";
            }
            else // Cao (High Priority)
            {
                delay = "PT1S";
                taskPriority = "2";
                runLevel = "HighestAvailable";
            }

            return $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.4"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>Smart Fan Cooling Hub - Auto start at Windows logon with configurable launch priority.</Description>
    <Author>{EscapeXml(userId)}</Author>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <UserId>{EscapeXml(userId)}</UserId>
      <Delay>{delay}</Delay>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <UserId>{EscapeXml(userId)}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>{runLevel}</RunLevel>
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
    <Priority>{taskPriority}</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{EscapeXml(exePath)}</Command>
      <Arguments>/autostart</Arguments>
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
