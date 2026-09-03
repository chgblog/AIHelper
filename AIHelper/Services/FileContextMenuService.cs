// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;

namespace AIHelper.Services
{
    /// <summary>
    /// Service for managing Windows Explorer context menu registrations for files.
    /// </summary>
    public static class FileContextMenuService
    {
        public const string VerbName = "AIHelper";

        private const uint SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_IDLIST = 0x0000;

        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        /// <summary>
        /// Supported file extensions grouped by category.
        /// </summary>
        public static readonly IReadOnlyList<string> SupportedExtensions = new List<string>
        {
            // 图片
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg", ".ico",
            // Office
            ".docx", ".doc", ".xlsx", ".xls", ".csv", ".pptx", ".ppt",
            // PDF
            ".pdf",
            // TXT
            ".txt", ".md",
            // JSON
            ".json",
            // JS
            ".js", ".mjs", ".cjs", ".ts",
            // HTML, HTM
            ".html", ".htm"
        }.AsReadOnly();

        private static readonly HashSet<string> _extensionSet = new HashSet<string>(
            SupportedExtensions,
            StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Checks whether the given extension or file path is supported.
        /// </summary>
        public static bool IsSupportedExtension(string extensionOrPath)
        {
            if (string.IsNullOrWhiteSpace(extensionOrPath)) return false;
            string ext = extensionOrPath.StartsWith(".")
                ? extensionOrPath
                : Path.GetExtension(extensionOrPath);

            return !string.IsNullOrEmpty(ext) && _extensionSet.Contains(ext);
        }

        public static string GetExePath()
        {
            using (var process = Process.GetCurrentProcess())
            {
                return process.MainModule.FileName;
            }
        }

        public static string GetMenuTitle()
        {
            try
            {
                string title = LanguageManager.Instance["ContextMenu_Title"];
                if (!string.IsNullOrWhiteSpace(title) && title != "ContextMenu_Title")
                {
                    return title;
                }
            }
            catch
            {
            }
            return "使用 AIHelper 处理";
        }

        /// <summary>
        /// Checks if the context menu is registered in HKCU for target extensions.
        /// </summary>
        public static bool IsContextMenuRegistered()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\SystemFileAssociations\.txt\shell\" + VerbName, false))
                {
                    if (key != null) return true;
                }
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\SystemFileAssociations\.pdf\shell\" + VerbName, false))
                {
                    if (key != null) return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to check context menu status: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Enables or disables the context menu for supported file extensions.
        /// Tries HKCU first; if permissions are denied, requests UAC elevation.
        /// </summary>
        public static bool SetContextMenuEnabled(bool enable)
        {
            try
            {
                RegisterInternal(Registry.CurrentUser, enable);
                NotifyShell();
                Logger.LogInfo($"File context menu {(enable ? "enabled" : "disabled")} in HKCU.");
                return true;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is SecurityException)
            {
                Logger.LogWarning($"Permission denied while updating context menu in HKCU ({ex.Message}). Requesting UAC elevation...");
                return ElevateAndSet(enable);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to set context menu: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Directly registers or unregisters on the specified root (HKCU or HKLM).
        /// </summary>
        public static bool RegisterDirect(bool machineWide, bool enable = true)
        {
            bool anySuccess = false;
            try
            {
                RegisterInternal(Registry.CurrentUser, enable);
                anySuccess = true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Direct context menu on HKCU: {ex.Message}");
            }

            if (machineWide)
            {
                try
                {
                    RegisterInternal(Registry.LocalMachine, enable);
                    anySuccess = true;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Direct context menu on HKLM: {ex.Message}");
                }
            }

            if (anySuccess)
            {
                NotifyShell();
                Logger.LogInfo($"Direct context menu {(enable ? "registered" : "unregistered")} completed (machineWide={machineWide}).");
            }
            return anySuccess;
        }

        /// <summary>
        /// Checks if the operating system is Windows 11 or greater (build >= 22000).
        /// </summary>
        public static bool IsWindows11OrGreater()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (key != null)
                    {
                        var buildObj = key.GetValue("CurrentBuild") ?? key.GetValue("CurrentBuildNumber");
                        if (buildObj != null && int.TryParse(buildObj.ToString(), out int buildNumber))
                        {
                            return buildNumber >= 22000;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to check Windows build number: {ex.Message}");
            }
            return Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 22000;
        }

        /// <summary>
        /// Registers or unregisters the Windows 11 modern context menu via MSIX Sparse Package.
        /// Only executed when explicitly requested via --register-win11-menu or dedicated scripts.
        /// </summary>
        public static bool RegisterWin11Menu(bool enable)
        {
            try
            {
                // Always ensure traditional registry keys are aligned
                RegisterDirect(machineWide: false, enable: enable);

                if (enable)
                {
                    string manifestPath = FindAppxManifestPath();
                    if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
                    {
                        Logger.LogWarning("AppxManifest.xml not found. Cannot register Win11 sparse package.");
                        return false;
                    }

                    string appDir = Path.GetDirectoryName(GetExePath());
                    string script = $"Add-AppxPackage -Path '{manifestPath}' -Register -ExternalLocation '{appDir}'";
                    bool success = RunPowerShellCommand(script);
                    if (success)
                    {
                        NotifyShell();
                        Logger.LogInfo("Win11 sparse package registered successfully.");
                    }
                    return success;
                }
                else
                {
                    string script = "Get-AppxPackage *AIHelper* | Remove-AppxPackage";
                    bool success = RunPowerShellCommand(script);
                    NotifyShell();
                    Logger.LogInfo("Win11 sparse package unregistered.");
                    return success;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"RegisterWin11Menu failed: {ex.Message}", ex);
                return false;
            }
        }

        private static string FindAppxManifestPath()
        {
            string exeDir = Path.GetDirectoryName(GetExePath());
            string[] candidates = new[]
            {
                Path.Combine(exeDir, "AppxManifest.xml"),
                Path.Combine(exeDir, "packaging", "windows11", "AppxManifest.xml"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "packaging", "windows11", "AppxManifest.xml"),
                Path.Combine(Directory.GetCurrentDirectory(), "packaging", "windows11", "AppxManifest.xml"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\packaging\windows11\AppxManifest.xml")
            };

            foreach (var candidate in candidates)
            {
                try
                {
                    if (File.Exists(candidate))
                    {
                        return Path.GetFullPath(candidate);
                    }
                }
                catch { }
            }
            return null;
        }

        private static bool RunPowerShellCommand(string command)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{command}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc == null) return false;
                    string output = proc.StandardOutput.ReadToEnd();
                    string error = proc.StandardError.ReadToEnd();
                    proc.WaitForExit();

                    if (proc.ExitCode != 0)
                    {
                        Logger.LogWarning($"PowerShell command failed (exit code {proc.ExitCode}): {error} {output}");
                        return false;
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"RunPowerShellCommand execution failed: {ex.Message}", ex);
                return false;
            }
        }

        private static void RegisterInternal(RegistryKey root, bool enable)
        {
            string exePath = GetExePath();
            string title = GetMenuTitle();
            string commandStr = $"\"{exePath}\" --file \"%1\"";
            string iconStr = $"\"{exePath}\",0";

            foreach (var ext in SupportedExtensions)
            {
                string basePath = $@"Software\Classes\SystemFileAssociations\{ext}\shell";

                if (enable)
                {
                    using (var shellKey = root.CreateSubKey(basePath))
                    {
                        if (shellKey != null)
                        {
                            using (var verbKey = shellKey.CreateSubKey(VerbName))
                            {
                                if (verbKey != null)
                                {
                                    verbKey.SetValue("", title, RegistryValueKind.String);
                                    verbKey.SetValue("Icon", iconStr, RegistryValueKind.String);

                                    using (var cmdKey = verbKey.CreateSubKey("command"))
                                    {
                                        cmdKey?.SetValue("", commandStr, RegistryValueKind.String);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    try
                    {
                        using (var shellKey = root.OpenSubKey(basePath, true))
                        {
                            if (shellKey != null)
                            {
                                shellKey.DeleteSubKeyTree(VerbName, false);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Failed to remove context menu key for {ext}: {ex.Message}");
                    }
                }
            }
        }

        private static bool ElevateAndSet(bool enable)
        {
            try
            {
                string exePath = GetExePath();
                string arg = enable ? "--register-context-menu" : "--unregister-context-menu";

                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = arg,
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc == null) return false;
                    proc.WaitForExit();
                    bool success = proc.ExitCode == 0;
                    if (success)
                    {
                        NotifyShell();
                    }
                    return success;
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED by user in UAC
            {
                Logger.LogWarning("UAC elevation was cancelled by the user.");
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError("UAC elevation execution failed.", ex);
                return false;
            }
        }

        public static void NotifyShell()
        {
            try
            {
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"SHChangeNotify failed: {ex.Message}");
            }
        }
    }
}
