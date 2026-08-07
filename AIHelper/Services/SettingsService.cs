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
        /// Loads settings from file or creates default
        /// </summary>
        public AppSettings Load()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    return JsonConvert.DeserializeObject<AppSettings>(json) ?? AppSettings.CreateDefault();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }
            return AppSettings.CreateDefault();
        }

        /// <summary>
        /// Saves settings to file
        /// </summary>
        public void Save(AppSettings settings)
        {
            try
            {
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
