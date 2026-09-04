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
using AIHelper.Models;

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
            try
            {
                using (var process = Process.GetCurrentProcess())
                {
                    string processExe = process.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(processExe) &&
                        string.Equals(Path.GetFileName(processExe), "AIHelper.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        return processExe;
                    }
                }
            }
            catch { }

            try
            {
                string baseDirExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AIHelper.exe");
                if (File.Exists(baseDirExe))
                {
                    return baseDirExe;
                }
            }
            catch { }

            try
            {
                var running = Process.GetProcessesByName("AIHelper").FirstOrDefault();
                if (running != null && !running.HasExited)
                {
                    string runningExe = running.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(runningExe) && File.Exists(runningExe))
                    {
                        return runningExe;
                    }
                }
            }
            catch { }

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

        public static string GetMoreTitle()
        {
            try
            {
                string title = LanguageManager.Instance["ContextMenu_More"];
                if (!string.IsNullOrWhiteSpace(title) && title != "ContextMenu_More")
                {
                    return title;
                }
            }
            catch
            {
            }
            return "更多...";
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
        public static bool SetContextMenuEnabled(bool enable, List<ActionItem> actions = null)
        {
            try
            {
                RegisterInternal(Registry.CurrentUser, enable, actions);
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
        /// Synchronously updates the context menu subitems if the context menu is currently registered.
        /// </summary>
        public static bool SyncContextMenu(List<ActionItem> actions = null)
        {
            try
            {
                if (!IsContextMenuRegistered())
                {
                    return false;
                }
                return SetContextMenuEnabled(true, actions);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to sync context menu: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Directly registers or unregisters on the specified root (HKCU or HKLM).
        /// </summary>
        public static bool RegisterDirect(bool machineWide, bool enable = true, List<ActionItem> actions = null)
        {
            bool anySuccess = false;
            try
            {
                RegisterInternal(Registry.CurrentUser, enable, actions);
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
                    RegisterInternal(Registry.LocalMachine, enable, actions);
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

        private static void RegisterInternal(RegistryKey root, bool enable, List<ActionItem> actions = null)
        {
            string exePath = GetExePath();
            string title = GetMenuTitle();
            string iconStr = $"\"{exePath}\",0";

            List<ActionItem> effectiveActions = null;
            if (enable)
            {
                if (actions != null)
                {
                    effectiveActions = actions;
                }
                else
                {
                    try
                    {
                        effectiveActions = SettingsService.Instance?.Load()?.Actions;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Failed to load actions for context menu: {ex.Message}");
                    }
                }
            }

            var orderedActions = (effectiveActions ?? new List<ActionItem>())
                .Where(a => a != null)
                .OrderBy(a => a.SortOrder)
                .ToList();
            var top10 = orderedActions.Take(10).ToList();
            string moreTitle = GetMoreTitle();

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
                                    // 级联父菜单项绝不能设置 (Default) 默认值，必须清除可能残留的默认值；
                                    // 否则 Windows Shell 在处理 WM_INITMENUPOPUP 时会误将其视作单级叶子命令，
                                    // 导致二级子菜单项数量为 0、右键菜单中的二级菜单完全无法展开或点击。
                                    try
                                    {
                                        verbKey.DeleteValue("", false);
                                    }
                                    catch { }

                                    verbKey.SetValue("MUIVerb", title, RegistryValueKind.String);
                                    verbKey.SetValue("Icon", iconStr, RegistryValueKind.String);
                                    verbKey.SetValue("SubCommands", "", RegistryValueKind.String);

                                    // 清理旧版本可能存在的直接 command 键（避免阻碍级联子菜单）
                                    try
                                    {
                                        verbKey.DeleteSubKeyTree("command", false);
                                    }
                                    catch { }

                                    using (var nestedShellKey = verbKey.CreateSubKey("shell"))
                                    {
                                        if (nestedShellKey != null)
                                        {
                                            // 清除旧的子菜单项，避免残留已删除或已换位的操作
                                            foreach (var subKeyName in nestedShellKey.GetSubKeyNames())
                                            {
                                                try
                                                {
                                                    nestedShellKey.DeleteSubKeyTree(subKeyName, false);
                                                }
                                                catch { }
                                            }

                                            // 1. 添加前 10 项快捷操作作为二级菜单
                                            for (int i = 0; i < top10.Count; i++)
                                            {
                                                var action = top10[i];
                                                string itemKeyName = $"{i + 1:D2}_{action.Id}";
                                                string itemTitle = string.IsNullOrWhiteSpace(action.Icon)
                                                    ? action.Name
                                                    : $"{action.Icon} {action.Name}";
                                                string itemCommand = $"\"{exePath}\" --file \"%1\" --action \"{action.Id}\"";

                                                using (var itemKey = nestedShellKey.CreateSubKey(itemKeyName))
                                                {
                                                    if (itemKey != null)
                                                    {
                                                        itemKey.SetValue("", itemTitle, RegistryValueKind.String);
                                                        itemKey.SetValue("MUIVerb", itemTitle, RegistryValueKind.String);
                                                        using (var cmdKey = itemKey.CreateSubKey("command"))
                                                        {
                                                            cmdKey?.SetValue("", itemCommand, RegistryValueKind.String);
                                                        }
                                                    }
                                                }
                                            }

                                            // 2. 超过 10 项时添加“更多...”二级菜单
                                            if (orderedActions.Count > 10)
                                            {
                                                string moreKeyName = "99_more";
                                                string moreCommand = $"\"{exePath}\" --file \"%1\" --more";

                                                using (var moreKey = nestedShellKey.CreateSubKey(moreKeyName))
                                                {
                                                    if (moreKey != null)
                                                    {
                                                        moreKey.SetValue("", moreTitle, RegistryValueKind.String);
                                                        moreKey.SetValue("MUIVerb", moreTitle, RegistryValueKind.String);
                                                        using (var cmdKey = moreKey.CreateSubKey("command"))
                                                        {
                                                            cmdKey?.SetValue("", moreCommand, RegistryValueKind.String);
                                                        }
                                                    }
                                                }
                                            }
                                        }
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
