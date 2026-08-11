using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AIHelper.Models;

namespace AIHelper.Services
{
    /// <summary>
    /// 获取运行中应用程序信息的服务
    /// </summary>
    public class AppInfoService
    {
        private static readonly HashSet<string> SystemProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "system", "idle", "registry", "memory compression", "smss", "csrss", "wininit",
            "services", "lsass", "winlogon", "svchost", "fontdrvhost", "dwm", "sihost",
            "taskhostw", "spoolsv", "ctfmon", "conhost", "searchindexer", "searchhost",
            "runtimebroker", "applicationframehost", "systemsettings", "startmenuexperiencehost",
            "shellexperiencehost", "textinputhost", "securityhealthservice", "smartscreen",
            "audiodg", "wlanext", "compattelrunner", "sedlauncher", "sgrmbroker"
        };

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool QueryFullProcessImageName([In] IntPtr hProcess, [In] int dwFlags, [Out] StringBuilder lpExeName, ref int lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        /// <summary>
        /// 获取运行中应用程序列表
        /// </summary>
        /// <param name="onlyWithWindow">是否仅获取带有可见窗口的应用</param>
        /// <returns>排序好的应用列表</returns>
        public static List<AppItem> GetRunningApplications(bool onlyWithWindow = true)
        {
            var appDict = new Dictionary<string, AppItem>(StringComparer.OrdinalIgnoreCase);
            int currentPid = Process.GetCurrentProcess().Id;
            int currentSessionId = Process.GetCurrentProcess().SessionId;

            Process[] processes;
            try
            {
                processes = Process.GetProcesses();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AppInfoService: Failed to get processes: {ex.Message}");
                return new List<AppItem>();
            }

            foreach (var proc in processes)
            {
                using (proc)
                {
                    try
                    {
                        int pid = proc.Id;
                        if (pid <= 4 || pid == currentPid) continue;

                        // 过滤 Session 0 (服务进程)
                        try
                        {
                            if (proc.SessionId != currentSessionId) continue;
                        }
                        catch
                        {
                            // 无法访问 SessionId 则忽略
                            continue;
                        }

                        string rawProcessName = proc.ProcessName;
                        if (string.IsNullOrWhiteSpace(rawProcessName)) continue;
                        if (SystemProcessNames.Contains(rawProcessName)) continue;

                        string exeFileName = rawProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                            ? rawProcessName
                            : rawProcessName + ".exe";

                        IntPtr mainHwnd = IntPtr.Zero;
                        bool hasVisibleWindow = false;
                        string windowTitle = null;

                        try
                        {
                            mainHwnd = proc.MainWindowHandle;
                            if (mainHwnd != IntPtr.Zero && IsWindowVisible(mainHwnd))
                            {
                                hasVisibleWindow = true;
                                windowTitle = proc.MainWindowTitle;
                            }
                        }
                        catch { }

                        // 如果要求仅有窗口应用且此进程无可见主窗口
                        if (onlyWithWindow && !hasVisibleWindow)
                        {
                            // 如果字典中已有此进程名且已有窗口，保留；否则先略过
                            if (!appDict.TryGetValue(exeFileName, out _))
                            {
                                continue;
                            }
                        }

                        // 如果已包含此进程名，合并窗口状态与标题信息
                        if (appDict.TryGetValue(exeFileName, out AppItem existingItem))
                        {
                            if (hasVisibleWindow)
                            {
                                existingItem.HasWindow = true;
                                if (string.IsNullOrWhiteSpace(existingItem.MainWindowTitle) && !string.IsNullOrWhiteSpace(windowTitle))
                                {
                                    existingItem.MainWindowTitle = windowTitle;
                                }
                            }
                            continue;
                        }

                        // 获取可执行文件路径
                        string exePath = GetProcessExecutablePath(proc);

                        // 获取应用显示名称
                        string appName = GetApplicationName(exePath, windowTitle, rawProcessName);

                        // 提取图标
                        ImageSource iconSource = ExtractIconFromExe(exePath);

                        var appItem = new AppItem
                        {
                            ProcessName = exeFileName,
                            AppName = appName,
                            MainWindowTitle = windowTitle,
                            ExecutablePath = exePath,
                            HasWindow = hasVisibleWindow,
                            ProcessId = pid,
                            Icon = iconSource,
                            IsSelected = false
                        };

                        appDict[exeFileName] = appItem;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"AppInfoService: Error processing process {proc.ProcessName}: {ex.Message}");
                    }
                }
            }

            // 排序：有窗口的排前面，其次按 AppName/ProcessName
            return appDict.Values
                .OrderByDescending(a => a.HasWindow)
                .ThenBy(a => a.AppName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => a.ProcessName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string GetProcessExecutablePath(Process proc)
        {
            try
            {
                string path = proc.MainModule?.FileName;
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path;
            }
            catch { }

            // 备选方案: QueryFullProcessImageName
            IntPtr hProcess = IntPtr.Zero;
            try
            {
                hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, proc.Id);
                if (hProcess != IntPtr.Zero)
                {
                    int capacity = 1024;
                    StringBuilder sb = new StringBuilder(capacity);
                    if (QueryFullProcessImageName(hProcess, 0, sb, ref capacity))
                    {
                        string path = sb.ToString();
                        if (File.Exists(path)) return path;
                    }
                }
            }
            catch { }
            finally
            {
                if (hProcess != IntPtr.Zero) CloseHandle(hProcess);
            }

            return null;
        }

        private static string GetApplicationName(string exePath, string windowTitle, string processName)
        {
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                try
                {
                    FileVersionInfo info = FileVersionInfo.GetVersionInfo(exePath);
                    if (!string.IsNullOrWhiteSpace(info.FileDescription))
                    {
                        return info.FileDescription.Trim();
                    }
                    if (!string.IsNullOrWhiteSpace(info.ProductName))
                    {
                        return info.ProductName.Trim();
                    }
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(windowTitle))
            {
                return windowTitle.Trim();
            }

            return processName;
        }

        private static ImageSource ExtractIconFromExe(string exePath)
        {
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return null;

            try
            {
                using (Icon sysIcon = Icon.ExtractAssociatedIcon(exePath))
                {
                    if (sysIcon != null)
                    {
                        IntPtr hIcon = sysIcon.Handle;
                        ImageSource wpfImage = Imaging.CreateBitmapSourceFromHIcon(
                            hIcon,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        
                        // 冻结 BitmapSource 避免跨线程调度问题
                        if (wpfImage.CanFreeze)
                        {
                            wpfImage.Freeze();
                        }
                        
                        return wpfImage;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AppInfoService: Failed to extract icon from '{exePath}': {ex.Message}");
            }

            return null;
        }
    }
}
