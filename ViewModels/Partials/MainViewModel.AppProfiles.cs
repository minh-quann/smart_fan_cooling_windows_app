using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartFanCooling.Models;

namespace SmartFanCooling.ViewModels
{
    /// <summary>
    /// Partial class for App-to-Profile automatic mapping and process picker.
    /// </summary>
    public partial class MainViewModel
    {
        // App Mappings
        public ObservableCollection<AppMapping> AppMappings { get; } = new();
        [ObservableProperty] private bool _isAutoAppSwitchEnabled = true;

        [ObservableProperty] private string _newAppName = "";
        [ObservableProperty] private string _newExePath = "";
        [ObservableProperty] private string _selectedMappingProfileName = "Turbo";

        [ObservableProperty] private bool _isAppPickerOpen = false;
        [ObservableProperty] private string _appPickerSearchText = "";
        public ObservableCollection<RunningAppInfo> RunningApps { get; } = new();
        public ObservableCollection<RunningAppInfo> FilteredRunningApps { get; } = new();

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
                int currentPid = Process.GetCurrentProcess().Id;

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
            AppMappings.Add(new AppMapping
            {
                AppName = NewAppName,
                ExecutablePath = NewExePath,
                ProcessName = System.IO.Path.GetFileNameWithoutExtension(NewExePath),
                ProfileName = SelectedMappingProfileName,
                IsEnabled = true
            });
            NewAppName = "";
            NewExePath = "";
            StatusMessage = "Đã thêm gán ứng dụng mới.";
        }

        [RelayCommand]
        public void RemoveAppMapping(AppMapping mapping)
        {
            if (mapping != null && AppMappings.Contains(mapping))
            {
                AppMappings.Remove(mapping);
                StatusMessage = $"Đã xóa gán ứng dụng: {mapping.AppName}";
            }
        }
    }
}
