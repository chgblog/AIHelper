using System;
using System.IO;

namespace AIHelper.Services
{
    /// <summary>
    /// Thread-safe logger for application diagnostic and crash logging
    /// </summary>
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static readonly string LogDirectory;
        private static readonly string AppLogPath;
        private static readonly string CrashLogPath;

        static Logger()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                LogDirectory = Path.Combine(appData, "AIHelper", "logs");
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }

                AppLogPath = Path.Combine(LogDirectory, "app.log");
                CrashLogPath = Path.Combine(LogDirectory, "crash.log");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize Logger: {ex.Message}");
            }
        }

        public static void LogInfo(string message)
        {
            WriteLog(AppLogPath, "INFO", message);
        }

        public static void LogWarning(string message)
        {
            WriteLog(AppLogPath, "WARN", message);
        }

        public static void LogError(string message, Exception ex = null)
        {
            string content = ex == null ? message : $"{message}\nException: {ex.GetType().FullName}: {ex.Message}\nStackTrace:\n{ex.StackTrace}";
            if (ex?.InnerException != null)
            {
                content += $"\nInner Exception: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}";
            }
            WriteLog(AppLogPath, "ERROR", content);
        }

        public static void LogCrash(string source, Exception ex)
        {
            string content = $"[CRASH SOURCE: {source}]\nException: {ex?.GetType().FullName}: {ex?.Message}\nStackTrace:\n{ex?.StackTrace}";
            if (ex?.InnerException != null)
            {
                content += $"\nInner Exception: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}";
            }
            WriteLog(CrashLogPath, "FATAL", content);
            WriteLog(AppLogPath, "FATAL", content);
        }

        private static void WriteLog(string filePath, string level, string message)
        {
            try
            {
                lock (_lock)
                {
                    if (string.IsNullOrEmpty(filePath)) return;
                    string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    string logLine = $"[{time}] [{level}] {message}\n";
                    File.AppendAllText(filePath, logLine);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to write log: {ex.Message}");
            }
        }

        public static string GetLogFolderPath() => LogDirectory;
    }
}
