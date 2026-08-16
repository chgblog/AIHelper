// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AIHelper.Models;
using Newtonsoft.Json.Linq;

namespace AIHelper.Services
{
    /// <summary>
    /// 后台自动检测新版本服务
    /// </summary>
    public static class UpdateCheckService
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/chgblog/AIHelper/releases/latest";
        private const string DefaultUpdateUrl = "https://github.com/chgblog/AIHelper/releases";
        private const int DelayMinutes = 1;
        private const int TimeoutSeconds = 30;

        /// <summary>
        /// 检测到的新版本信息
        /// </summary>
        public class UpdateInfo
        {
            public string LatestVersion { get; set; }
            public string CurrentVersion { get; set; }
            public string UpdateUrl { get; set; }
        }

        /// <summary>
        /// 已检测到的新版本；无新版本时为 null。
        /// 供检测完成后才创建的窗口读取初始状态。
        /// </summary>
        public static UpdateInfo AvailableUpdate { get; private set; }

        /// <summary>
        /// 检测到新版本时触发（在 UI 线程）
        /// </summary>
        public static event EventHandler<UpdateInfo> UpdateAvailable;

        /// <summary>
        /// 在默认浏览器中打开更新页面
        /// </summary>
        public static void OpenUpdatePage(string updateUrl = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(updateUrl))
                {
                    updateUrl = AvailableUpdate?.UpdateUrl;
                }
                if (string.IsNullOrWhiteSpace(updateUrl))
                {
                    updateUrl = DefaultUpdateUrl;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = updateUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("UpdateCheckService: Failed to open update page", ex);
            }
        }

        /// <summary>
        /// 启动延迟检测（非阻塞，后台执行）
        /// </summary>
        public static void StartDelayedCheck(AppSettings settings)
        {
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(DelayMinutes));
                    await CheckForUpdateAsync(settings);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"UpdateCheckService: Unhandled error: {ex.Message}");
                }
            });
        }

        private static async Task CheckForUpdateAsync(AppSettings settings)
        {
            HttpClient client = null;
            try
            {
                var handler = new HttpClientHandler();

                // 配置代理
                string proxyServer = settings.ProxyServer?.Trim();
                if (!string.IsNullOrEmpty(proxyServer))
                {
                    handler.Proxy = new WebProxy(proxyServer);
                    handler.UseProxy = true;
                }
                else
                {
                    // 未设置代理则使用系统代理（HttpClientHandler 默认行为）
                    handler.UseProxy = true;
                    handler.Proxy = WebRequest.GetSystemWebProxy();
                }

                client = new HttpClient(handler)
                {
                    Timeout = TimeSpan.FromSeconds(TimeoutSeconds)
                };

                // GitHub API 要求 User-Agent
                client.DefaultRequestHeaders.UserAgent.ParseAdd("AIHelper-UpdateChecker/1.0");

                // 尝试访问 GitHub API
                HttpResponseMessage response;
                try
                {
                    response = await client.GetAsync(GitHubApiUrl);
                }
                catch (TaskCanceledException)
                {
                    // 超时，静默放弃
                    Debug.WriteLine("UpdateCheckService: Request timed out, aborting update check.");
                    return;
                }
                catch (HttpRequestException ex)
                {
                    // 网络不可达，静默放弃
                    Debug.WriteLine($"UpdateCheckService: Cannot reach GitHub: {ex.Message}");
                    return;
                }

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"UpdateCheckService: GitHub API returned {response.StatusCode}, aborting.");
                    return;
                }

                // 解析响应
                string json = await response.Content.ReadAsStringAsync();
                var release = JObject.Parse(json);
                string tagName = release["tag_name"]?.ToString();

                if (string.IsNullOrWhiteSpace(tagName))
                {
                    Debug.WriteLine("UpdateCheckService: No tag_name found in release.");
                    return;
                }

                // 去掉 v 前缀
                string versionStr = tagName.TrimStart('v', 'V');

                // 解析远程版本
                if (!TryParseVersion(versionStr, out Version remoteVersion))
                {
                    Debug.WriteLine($"UpdateCheckService: Cannot parse remote version: {versionStr}");
                    return;
                }

                // 获取当前版本
                var currentAssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
                // 只比较 Major.Minor.Build
                var currentVersion = new Version(currentAssemblyVersion.Major, currentAssemblyVersion.Minor, currentAssemblyVersion.Build);

                if (remoteVersion <= currentVersion)
                {
                    Debug.WriteLine($"UpdateCheckService: Current version {currentVersion} is up to date (remote: {remoteVersion}).");
                    return;
                }

                // 有新版本，不再弹窗，仅记录状态并通知界面显示标题栏提示
                string updateUrl = settings.UpdateUrl;
                if (string.IsNullOrWhiteSpace(updateUrl))
                {
                    updateUrl = DefaultUpdateUrl;
                }

                var info = new UpdateInfo
                {
                    LatestVersion = remoteVersion.ToString(),
                    CurrentVersion = currentVersion.ToString(),
                    UpdateUrl = updateUrl
                };

                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    try
                    {
                        AvailableUpdate = info;
                        UpdateAvailable?.Invoke(null, info);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"UpdateCheckService: Error notifying update availability: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateCheckService: Error during update check: {ex.Message}");
            }
            finally
            {
                client?.Dispose();
            }
        }

        /// <summary>
        /// 尝试将版本字符串解析为 Version 对象（支持 2-4 段版本号）
        /// </summary>
        private static bool TryParseVersion(string versionStr, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(versionStr)) return false;

            // 处理可能的后缀如 "-beta"
            int dashIndex = versionStr.IndexOf('-');
            if (dashIndex >= 0)
            {
                versionStr = versionStr.Substring(0, dashIndex);
            }

            return Version.TryParse(versionStr, out version);
        }
    }
}
