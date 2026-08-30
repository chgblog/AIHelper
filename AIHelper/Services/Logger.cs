// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
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
        private const long MaxLogBytes = 2 * 1024 * 1024;

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

        /// <summary>
        /// 诊断级日志。与 Debug.WriteLine 不同，Release 版本同样会写入 app.log，
        /// 用于排查划词等只能在用户机器上复现的问题。
        /// </summary>
        public static void LogDebug(string message)
        {
            WriteLog(AppLogPath, "DEBUG", message);
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

        /// <summary>
        /// 日志超过 MaxLogBytes 时滚动为 .1 备份，避免长期运行写爆磁盘
        /// </summary>
        private static void RollIfTooLarge(string filePath)
        {
            try
            {
                var info = new FileInfo(filePath);
                if (!info.Exists || info.Length < MaxLogBytes) return;

                string backupPath = filePath + ".1";
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
                File.Move(filePath, backupPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to roll log: {ex.Message}");
            }
        }

        public static string GetLogFolderPath() => LogDirectory;
    }
}
