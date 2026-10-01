// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.IO;
using AIHelper.Models;
using Newtonsoft.Json;

namespace AIHelper.Services
{
    /// <summary>
    /// Service for managing application settings
    /// </summary>
    public class SettingsService
    {
        private static SettingsService _instance;
        private static readonly object _lock = new object();
        private readonly string _settingsFilePath;

        private SettingsService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "AIHelper");
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }
            _settingsFilePath = Path.Combine(appFolder, "settings.json");
        }

        /// <summary>
        /// Gets the singleton instance
        /// </summary>
        public static SettingsService Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new SettingsService();
                    }
                    return _instance;
                }
            }
        }

        /// <summary>
        /// Gets the directory where settings files are stored
        /// </summary>
        public string GetSettingsDirectory()
        {
            return Path.GetDirectoryName(_settingsFilePath);
        }

        private static readonly Version TargetConfigVersion = new Version(0, 8, 7);

        /// <summary>
        /// Loads settings from file or creates default
        /// </summary>
        public AppSettings Load()
        {
            AppSettings settings = null;
            bool needSave = false;
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    settings = JsonConvert.DeserializeObject<AppSettings>(json);
                    if (settings != null)
                    {
                        needSave = MigrateSettingsIfNeeded(settings);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }

            if (settings == null)
            {
                settings = AppSettings.CreateDefault();
            }

            if (string.IsNullOrWhiteSpace(settings.Language))
            {
                settings.Language = LanguageManager.GetDefaultLanguageByTimeZone();
            }

            if (string.IsNullOrWhiteSpace(settings.ConfigVersion))
            {
                settings.ConfigVersion = AppSettings.CurrentConfigVersion;
            }

            // 老版本配置或恢复的配置可能缺少内置的批量生图操作
            if (settings.EnsureSystemActions())
            {
                needSave = true;
            }

            LanguageManager.Instance.CurrentLanguage = settings.Language;

            if (needSave)
            {
                Save(settings);
            }

            return settings;
        }

        /// <summary>
        /// 检查并执行配置迁移（如 0.8.7 之前的老版本配置，将 AutoCheckUpdate 默认升级为 true）
        /// </summary>
        public bool MigrateSettingsIfNeeded(AppSettings settings)
        {
            if (settings == null) return false;

            bool needMigration = false;

            if (string.IsNullOrWhiteSpace(settings.ConfigVersion))
            {
                needMigration = true;
            }
            else if (TryParseVersion(settings.ConfigVersion, out var ver) && ver < TargetConfigVersion)
            {
                needMigration = true;
            }

            if (needMigration)
            {
                settings.AutoCheckUpdate = true;
                settings.ConfigVersion = AppSettings.CurrentConfigVersion;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 尝试将版本字符串解析为 Version 对象（支持 2-4 段版本号及去除 v 前缀）
        /// </summary>
        public static bool TryParseVersion(string versionStr, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(versionStr)) return false;

            string cleanStr = versionStr.Trim().TrimStart('v', 'V');
            int dashIndex = cleanStr.IndexOf('-');
            if (dashIndex >= 0)
            {
                cleanStr = cleanStr.Substring(0, dashIndex);
            }

            return Version.TryParse(cleanStr, out version);
        }

        /// <summary>
        /// Saves settings to file
        /// </summary>
        public void Save(AppSettings settings)
        {
            try
            {
                if (settings != null && string.IsNullOrWhiteSpace(settings.ConfigVersion))
                {
                    settings.ConfigVersion = AppSettings.CurrentConfigVersion;
                }
                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }
    }
}
