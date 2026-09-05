// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace AIHelper.Services
{
    public class VisitedUrlRecord
    {
        public string Url { get; set; }
        public DateTime VisitedAtUtc { get; set; }
    }

    /// <summary>
    /// 管理 24 小时内的自动访问网址历史记录，防止重复进入
    /// </summary>
    public class AutoVisitHistoryService
    {
        private static readonly Lazy<AutoVisitHistoryService> _instance =
            new Lazy<AutoVisitHistoryService>(() => new AutoVisitHistoryService());

        public static AutoVisitHistoryService Instance => _instance.Value;

        private readonly object _lock = new object();
        private readonly string _filePath;
        private readonly Dictionary<string, DateTime> _visitedMap = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        private AutoVisitHistoryService()
        {
            try
            {
                string appDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AIHelper");
                if (!Directory.Exists(appDataDir))
                {
                    Directory.CreateDirectory(appDataDir);
                }
                _filePath = Path.Combine(appDataDir, "auto_visit_history.json");
                LoadFromDisk();
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize AutoVisitHistoryService", ex);
            }
        }

        /// <summary>
        /// 规范化网址，去除锚点并统一小写主机名与尾部斜杠
        /// </summary>
        public static string NormalizeUrl(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return string.Empty;
            string url = rawUrl.Trim();
            try
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                {
                    string path = uri.GetLeftPart(UriPartial.Path);
                    if (path.EndsWith("/") && uri.AbsolutePath.Length > 1)
                    {
                        path = path.TrimEnd('/');
                    }
                    return path + uri.Query;
                }
            }
            catch { }
            return url;
        }

        /// <summary>
        /// 检查指定 URL 是否在过去 24 小时内访问过
        /// </summary>
        public bool HasVisitedInLast24Hours(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return false;
            string normalized = NormalizeUrl(rawUrl);

            lock (_lock)
            {
                CleanExpiredInternal();
                if (_visitedMap.TryGetValue(normalized, out DateTime visitedUtc))
                {
                    if (DateTime.UtcNow - visitedUtc <= TimeSpan.FromHours(24))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// 记录一次访问（记录当前 UTC 时间并持久化）
        /// </summary>
        public void RecordVisit(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return;
            string normalized = NormalizeUrl(rawUrl);

            lock (_lock)
            {
                CleanExpiredInternal();
                _visitedMap[normalized] = DateTime.UtcNow;
                SaveToDiskInternal();
            }
        }

        /// <summary>
        /// 清除所有超过 24 小时的过期记录
        /// </summary>
        public void CleanExpired()
        {
            lock (_lock)
            {
                CleanExpiredInternal();
                SaveToDiskInternal();
            }
        }

        /// <summary>
        /// 清空全部历史记录
        /// </summary>
        public void ClearHistory()
        {
            lock (_lock)
            {
                _visitedMap.Clear();
                SaveToDiskInternal();
            }
        }

        /// <summary>
        /// 获取当前 24 小时内有效记录数量
        /// </summary>
        public int GetActiveCount()
        {
            lock (_lock)
            {
                CleanExpiredInternal();
                return _visitedMap.Count;
            }
        }

        private void CleanExpiredInternal()
        {
            DateTime cutoff = DateTime.UtcNow.AddHours(-24);
            var expiredKeys = _visitedMap
                .Where(kvp => kvp.Value < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _visitedMap.Remove(key);
            }
        }

        private void LoadFromDisk()
        {
            lock (_lock)
            {
                if (!File.Exists(_filePath)) return;
                try
                {
                    string json = File.ReadAllText(_filePath);
                    var list = JsonConvert.DeserializeObject<List<VisitedUrlRecord>>(json);
                    if (list != null)
                    {
                        DateTime cutoff = DateTime.UtcNow.AddHours(-24);
                        foreach (var item in list)
                        {
                            if (item != null && !string.IsNullOrWhiteSpace(item.Url) && item.VisitedAtUtc >= cutoff)
                            {
                                string norm = NormalizeUrl(item.Url);
                                _visitedMap[norm] = item.VisitedAtUtc;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error loading auto_visit_history.json", ex);
                }
            }
        }

        private void SaveToDiskInternal()
        {
            try
            {
                var list = _visitedMap.Select(kvp => new VisitedUrlRecord
                {
                    Url = kvp.Key,
                    VisitedAtUtc = kvp.Value
                }).ToList();

                string json = JsonConvert.SerializeObject(list, Formatting.Indented);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error saving auto_visit_history.json", ex);
            }
        }
    }
}
