using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartFanCooling.Models;

namespace SmartFanCooling.ViewModels
{
    /// <summary>
    /// Partial class for App-to-Profile automatic mapping, foreground window process detection, and disk persistence.
    /// </summary>
    public partial class MainViewModel
    {
        // App Mappings (Win32 APIs GetForegroundWindow & GetWindowThreadProcessId are defined in MainViewModel.OsdHud.cs)
        public ObservableCollection<AppMapping> AppMappings { get; } = new();
        [ObservableProperty] private bool _isAutoAppSwitchEnabled = true;

        [ObservableProperty] private string _newAppName = "";
        [ObservableProperty] private string _newExePath = "";
        [ObservableProperty] private string _selectedMappingProfileName = "Turbo";

        [ObservableProperty] private bool _isAppPickerOpen = false;
        [ObservableProperty] private string _appPickerSearchText = "";
        public ObservableCollection<RunningAppInfo> RunningApps { get; } = new();
        public ObservableCollection<RunningAppInfo> FilteredRunningApps { get; } = new();

        private string? _activeAutoMappedApp = null;
        private string? _activeAutoMappedProcessName = null;
        private string? _activeAutoMappedExePath = null;
        private string _previousManualProfileName = "Balanced";
        private int _appSwitchThrottleCounter = 0;

        partial void OnIsAutoAppSwitchEnabledChanged(bool value)
        {
            SaveAppMappingsToDisk();
            if (!value && _activeAutoMappedApp != null)
            {
                _activeAutoMappedApp = null;
                _activeAutoMappedProcessName = null;
                _activeAutoMappedExePath = null;
            }
        }

        /// <summary>
        /// Periodic check executed on telemetry timer tick:
        /// - Detects when a mapped App/Game starts running and switches to its assigned Profile.
        /// - Keeps the profile active when user Alt+Tabs (does NOT revert on Alt+Tab).
        /// - Automatically reverts to previous profile ONLY when the mapped App/Game process is completely closed/terminated.
        /// </summary>
        public void CheckActiveAppAndAutoSwitchProfile()
        {
            if (!IsAutoAppSwitchEnabled || AppMappings.Count == 0) return;

            // Throttle process lifecycle check to once every ~1.5s (prevents CPU overhead)
            _appSwitchThrottleCounter++;
            if (_appSwitchThrottleCounter < 2) return;
            _appSwitchThrottleCounter = 0;

            try
            {
                // CASE 1: An auto-mapped app is currently active
                if (!string.IsNullOrEmpty(_activeAutoMappedApp))
                {
                    bool isStillRunning = false;

                    try
                    {
                        // Check if the mapped process is still alive on Windows
                        if (!string.IsNullOrEmpty(_activeAutoMappedProcessName))
                        {
                            var procs = Process.GetProcessesByName(_activeAutoMappedProcessName);
                            if (procs != null && procs.Length > 0)
                            {
                                isStillRunning = true;
                                foreach (var p in procs) p.Dispose();
                            }
                        }

                        if (!isStillRunning && !string.IsNullOrEmpty(_activeAutoMappedExePath))
                        {
                            string exeName = Path.GetFileNameWithoutExtension(_activeAutoMappedExePath);
                            if (!string.IsNullOrEmpty(exeName))
                            {
                                var procs = Process.GetProcessesByName(exeName);
                                if (procs != null && procs.Length > 0)
                                {
                                    isStillRunning = true;
                                    foreach (var p in procs) p.Dispose();
                                }
                            }
                        }
                    }
                    catch { }

                    // If the app is STILL RUNNING -> keep the profile (even if user Alt+Tabs to Chrome/Discord)
                    if (isStillRunning)
                    {
                        return;
                    }

                    // App has completely EXITED / CLOSED (Tắt hẳn) -> Revert to previous profile
                    string closedAppName = _activeAutoMappedApp;
                    _activeAutoMappedApp = null;
                    _activeAutoMappedProcessName = null;
                    _activeAutoMappedExePath = null;

                    var revertProfile = Profiles.FirstOrDefault(p =>
                        p.Name.Equals(_previousManualProfileName, StringComparison.OrdinalIgnoreCase)) ??
                        Profiles.FirstOrDefault(p => p.Name == "Balanced") ??
                        Profiles.FirstOrDefault();

                    if (revertProfile != null)
                    {
                        ApplyProfile(revertProfile);
                        StatusMessage = $"[Auto-Switch] Ứng dụng '{closedAppName}' đã tắt hoàn toàn -> Tự động quay về profile '{revertProfile.Name}'";
                    }
                    return;
                }

                // CASE 2: No auto-mapped app is active yet -> Check if any mapped app has started running
                // First: Check foreground window for instant launch response
                IntPtr hWnd = GetForegroundWindow();
                string activeProcName = "";
                string activeExePath = "";

                if (hWnd != IntPtr.Zero)
                {
                    GetWindowThreadProcessId(hWnd, out uint processId);
                    if (processId != 0 && processId != Environment.ProcessId)
                    {
                        try
                        {
                            using var proc = Process.GetProcessById((int)processId);
                            activeProcName = proc.ProcessName;
                            try { activeExePath = proc.MainModule?.FileName ?? ""; } catch { }
                        }
                        catch { }
                    }
                }

                // Look for match in AppMappings (from foreground window or running processes)
                AppMapping? matchedMapping = null;

                if (!string.IsNullOrEmpty(activeProcName))
                {
                    matchedMapping = AppMappings.FirstOrDefault(m =>
                        m.IsEnabled &&
                        (!string.IsNullOrEmpty(m.ProcessName) && (
                            activeProcName.Equals(m.ProcessName, StringComparison.OrdinalIgnoreCase) ||
                            activeProcName.Equals(Path.GetFileNameWithoutExtension(m.ProcessName), StringComparison.OrdinalIgnoreCase) ||
                            activeProcName.Equals(Path.GetFileNameWithoutExtension(m.ExecutablePath), StringComparison.OrdinalIgnoreCase)
                        ) ||
                        (!string.IsNullOrEmpty(m.ExecutablePath) && !string.IsNullOrEmpty(activeExePath) &&
                            activeExePath.Equals(m.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                        ));
                }

                // If foreground window wasn't a mapped app, check if any enabled mapped process is running
                if (matchedMapping == null)
                {
                    foreach (var mapping in AppMappings.Where(m => m.IsEnabled))
                    {
                        string targetProcName = !string.IsNullOrEmpty(mapping.ProcessName)
                            ? Path.GetFileNameWithoutExtension(mapping.ProcessName)
                            : Path.GetFileNameWithoutExtension(mapping.ExecutablePath);

                        if (!string.IsNullOrEmpty(targetProcName))
                        {
                            try
                            {
                                var procs = Process.GetProcessesByName(targetProcName);
                                if (procs != null && procs.Length > 0)
                                {
                                    matchedMapping = mapping;
                                    foreach (var p in procs) p.Dispose();
                                    break;
                                }
                            }
                            catch { }
                        }
                    }
                }

                if (matchedMapping != null)
                {
                    var targetProfile = Profiles.FirstOrDefault(p =>
                        p.Name.Equals(matchedMapping.ProfileName, StringComparison.OrdinalIgnoreCase));

                    if (targetProfile != null)
                    {
                        _previousManualProfileName = ActiveProfile?.Name ?? "Balanced";
                        _activeAutoMappedApp = matchedMapping.AppName;
                        _activeAutoMappedProcessName = !string.IsNullOrEmpty(matchedMapping.ProcessName)
                            ? Path.GetFileNameWithoutExtension(matchedMapping.ProcessName)
                            : Path.GetFileNameWithoutExtension(matchedMapping.ExecutablePath);
                        _activeAutoMappedExePath = matchedMapping.ExecutablePath;

                        ApplyProfile(targetProfile);
                        StatusMessage = $"[Auto-Switch] 🚀 Phát hiện '{matchedMapping.AppName}' đang chạy -> Tự động chuyển sang profile '{targetProfile.Name}'";
                    }
                }
            }
            catch { }
        }

        #region App Mappings Persistence

        private string GetAppMappingsConfigPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folder = Path.Combine(appData, "SmartFanCooling");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "app_mappings.json");
        }

        public void LoadAppMappings()
        {
            try
            {
                string path = GetAppMappingsConfigPath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var state = JsonSerializer.Deserialize<AppMappingsSaveState>(json);
                    if (state != null)
                    {
                        IsAutoAppSwitchEnabled = state.IsAutoAppSwitchEnabled;
                        AppMappings.Clear();
                        if (state.Mappings != null)
                        {
                            foreach (var item in state.Mappings)
                            {
                                var mapping = new AppMapping
                                {
                                    AppName = item.AppName,
                                    ExecutablePath = item.ExecutablePath,
                                    ProcessName = item.ProcessName,
                                    ProfileName = item.ProfileName,
                                    IsEnabled = item.IsEnabled
                                };
                                mapping.PropertyChanged += (s, e) => SaveAppMappingsToDisk();
                                AppMappings.Add(mapping);
                            }
                        }
                        return;
                    }
                }
            }
            catch { }
        }

        public void SaveAppMappingsToDisk()
        {
            try
            {
                string path = GetAppMappingsConfigPath();
                var state = new AppMappingsSaveState
                {
                    IsAutoAppSwitchEnabled = IsAutoAppSwitchEnabled,
                    Mappings = AppMappings.Select(m => new AppMappingDto
                    {
                        AppName = m.AppName,
                        ExecutablePath = m.ExecutablePath,
                        ProcessName = m.ProcessName,
                        ProfileName = m.ProfileName,
                        IsEnabled = m.IsEnabled
                    }).ToList()
                };

                string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch { }
        }

        #endregion

        #region App Picker Dialog & Commands

        [RelayCommand]
        public void OpenAppPicker()
        {
            RefreshRunningApps();
            IsAppPickerOpen = true;
        }

        [RelayCommand]
        public void CloseAppPicker()
        {
            IsAppPickerOpen = false;
        }

        [RelayCommand]
        public void RefreshRunningApps()
        {
            RunningApps.Clear();
            var apps = GetSystemRunningApps();
            foreach (var app in apps)
            {
                RunningApps.Add(app);
            }
            FilterRunningApps();
        }

        partial void OnAppPickerSearchTextChanged(string value)
        {
            FilterRunningApps();
        }

        private void FilterRunningApps()
        {
            FilteredRunningApps.Clear();
            string q = (AppPickerSearchText ?? "").Trim().ToLower();
            var matches = string.IsNullOrEmpty(q)
                ? RunningApps
                : RunningApps.Where(a => a.Name.ToLower().Contains(q) || a.ProcessName.ToLower().Contains(q) || a.ExecutablePath.ToLower().Contains(q));

            foreach (var app in matches)
            {
                FilteredRunningApps.Add(app);
            }
        }

        public void SelectRunningApp(RunningAppInfo app)
        {
            if (app == null) return;
            NewAppName = app.Name;
            NewExePath = string.IsNullOrEmpty(app.ExecutablePath) ? app.ProcessName : app.ExecutablePath;
            IsAppPickerOpen = false;
            StatusMessage = $"Đã chọn ứng dụng: {app.Name} ({app.ProcessName})";
        }

        private List<RunningAppInfo> GetSystemRunningApps()
        {
            var list = new List<RunningAppInfo>();
            try
            {
                var processes = Process.GetProcesses();
                int currentPid = Environment.ProcessId;

                foreach (var proc in processes)
                {
                    try
                    {
                        if (proc.Id == currentPid) continue;
                        if (string.IsNullOrWhiteSpace(proc.MainWindowTitle)) continue;

                        string procName = proc.ProcessName;
                        if (procName.Equals("explorer", StringComparison.OrdinalIgnoreCase) ||
                            procName.Equals("SearchHost", StringComparison.OrdinalIgnoreCase) ||
                            procName.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase) ||
                            procName.Equals("cmd", StringComparison.OrdinalIgnoreCase) ||
                            procName.Equals("powershell", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string exePath = "";
                        try { exePath = proc.MainModule?.FileName ?? ""; } catch { }

                        list.Add(new RunningAppInfo
                        {
                            Name = proc.MainWindowTitle,
                            ProcessName = procName + ".exe",
                            ExecutablePath = string.IsNullOrEmpty(exePath) ? procName + ".exe" : exePath,
                            MainWindowTitle = proc.MainWindowTitle
                        });
                    }
                    catch { }
                }
            }
            catch { }

            return list.OrderBy(a => a.Name).ToList();
        }

        [RelayCommand]
        public void AddAppMapping()
        {
            if (string.IsNullOrWhiteSpace(NewAppName)) return;
            var mapping = new AppMapping
            {
                AppName = NewAppName,
                ExecutablePath = NewExePath,
                ProcessName = System.IO.Path.GetFileNameWithoutExtension(NewExePath),
                ProfileName = SelectedMappingProfileName,
                IsEnabled = true
            };
            mapping.PropertyChanged += (s, e) => SaveAppMappingsToDisk();

            AppMappings.Add(mapping);
            NewAppName = "";
            NewExePath = "";
            SaveAppMappingsToDisk();
            StatusMessage = $"Đã thêm gán ứng dụng: {mapping.AppName} -> {mapping.ProfileName}";
        }

        [RelayCommand]
        public void RemoveAppMapping(AppMapping mapping)
        {
            if (mapping != null && AppMappings.Contains(mapping))
            {
                AppMappings.Remove(mapping);
                SaveAppMappingsToDisk();
                StatusMessage = $"Đã xóa gán ứng dụng: {mapping.AppName}";
            }
        }

        #endregion
    }

    public class AppMappingsSaveState
    {
        public bool IsAutoAppSwitchEnabled { get; set; } = true;
        public List<AppMappingDto> Mappings { get; set; } = new();
    }

    public class AppMappingDto
    {
        public string AppName { get; set; } = "";
        public string ExecutablePath { get; set; } = "";
        public string ProcessName { get; set; } = "";
        public string ProfileName { get; set; } = "";
        public bool IsEnabled { get; set; } = true;
    }
}
