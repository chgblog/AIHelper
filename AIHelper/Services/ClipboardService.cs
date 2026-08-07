using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace AIHelper.Services
{
    /// <summary>
    /// Service for interacting with the system clipboard
    /// </summary>
    public static class ClipboardService
    {
        /// <summary>
        /// Gets text from clipboard with retry mechanism
        /// </summary>
        public static string GetText()
        {
            int maxRetries = 5;
            int delayMs = 50;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    if (Clipboard.ContainsText())
                    {
                        return Clipboard.GetText();
                    }
                    return string.Empty;
                }
                catch (COMException)
                {
                    if (i == maxRetries - 1)
                    {
                        System.Diagnostics.Debug.WriteLine("Failed to access clipboard after max retries.");
                        return string.Empty;
                    }
                    Thread.Sleep(delayMs);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error accessing clipboard: {ex.Message}");
                    return string.Empty;
                }
            }
            return string.Empty;
        }
    }
}
