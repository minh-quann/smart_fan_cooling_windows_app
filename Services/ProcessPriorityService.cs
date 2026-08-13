using System;
using System.Diagnostics;

namespace SmartFanCooling.Services
{
    /// <summary>
    /// Manages the current process priority class.
    /// Allows users to set how Windows schedules CPU time for this application.
    /// </summary>
    public static class ProcessPriorityService
    {
        /// <summary>
        /// Sets the process priority for the current running application.
        /// </summary>
        /// <param name="priorityName">
        /// One of: "Realtime", "High", "AboveNormal", "Normal", "BelowNormal", "Idle"
        /// </param>
        /// <returns>True if successfully applied.</returns>
        public static bool SetPriority(string priorityName)
        {
            try
            {
                var priority = ParsePriority(priorityName);
                Process.GetCurrentProcess().PriorityClass = priority;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the current process priority class name.
        /// </summary>
        public static string GetCurrentPriority()
        {
            try
            {
                return Process.GetCurrentProcess().PriorityClass.ToString();
            }
            catch
            {
                return "Normal";
            }
        }

        /// <summary>
        /// Parses a user-friendly priority name string into a ProcessPriorityClass enum.
        /// </summary>
        private static ProcessPriorityClass ParsePriority(string name)
        {
            return name switch
            {
                "Realtime" => ProcessPriorityClass.RealTime,
                "High" => ProcessPriorityClass.High,
                "AboveNormal" => ProcessPriorityClass.AboveNormal,
                "Normal" => ProcessPriorityClass.Normal,
                "BelowNormal" => ProcessPriorityClass.BelowNormal,
                "Idle" => ProcessPriorityClass.Idle,
                _ => ProcessPriorityClass.Normal
            };
        }
    }
}
