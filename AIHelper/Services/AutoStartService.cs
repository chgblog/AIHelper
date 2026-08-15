// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace AIHelper.Services
{
    /// <summary>
    /// Service for managing Windows auto start settings
    /// </summary>
    public static class AutoStartService
    {
        private const string AppName = "AIHelper";
        private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>
        /// Enables or disables auto start with Windows
        /// </summary>
        /// <param name="enable">True to enable, false to disable</param>
        public static void SetAutoStart(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            string exePath = Process.GetCurrentProcess().MainModule.FileName;
                            key.SetValue(AppName, $"\"{exePath}\" --minimized");
                            Logger.LogInfo("AutoStart enabled with --minimized flag.");
                        }
                        else
                        {
                            if (key.GetValue(AppName) != null)
                            {
                                key.DeleteValue(AppName, false);
                                Logger.LogInfo("AutoStart disabled.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to set auto start", ex);
                Debug.WriteLine($"Failed to set auto start: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if auto start with Windows is enabled in registry
        /// </summary>
        public static bool IsAutoStartEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key != null)
                    {
                        var value = key.GetValue(AppName) as string;
                        return !string.IsNullOrEmpty(value);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to check auto start status: {ex.Message}");
            }
            return false;
        }
    }
}
