// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
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

        /// <summary>
        /// Captures the clipboard content kind in a form that can be used for
        /// hotkey action routing.
        /// </summary>
        public static ClipboardSnapshot GetSnapshot()
        {
            int maxRetries = 5;
            int delayMs = 50;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    if (Clipboard.ContainsFileDropList())
                    {
                        StringCollection files = Clipboard.GetFileDropList();
                        var paths = new List<string>();
                        if (files != null)
                        {
                            foreach (string path in files)
                            {
                                if (!string.IsNullOrWhiteSpace(path))
                                {
                                    paths.Add(path);
                                }
                            }
                        }
                        return ClipboardSnapshot.FromFileDropList(paths);
                    }

                    if (Clipboard.ContainsImage())
                    {
                        return ClipboardSnapshot.FromImage();
                    }

                    if (Clipboard.ContainsText())
                    {
                        return ClipboardSnapshot.FromText(Clipboard.GetText());
                    }

                    return ClipboardSnapshot.Empty();
                }
                catch (COMException)
                {
                    if (i == maxRetries - 1)
                    {
                        System.Diagnostics.Debug.WriteLine("Failed to access clipboard snapshot after max retries.");
                        return ClipboardSnapshot.Empty();
                    }
                    Thread.Sleep(delayMs);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error accessing clipboard snapshot: {ex.Message}");
                    return ClipboardSnapshot.Empty();
                }
            }

            return ClipboardSnapshot.Empty();
        }

        /// <summary>
        /// Sets text to clipboard with retry mechanism
        /// </summary>
        public static bool SetText(string text)
        {
            if (text == null) return false;

            int maxRetries = 5;
            int delayMs = 50;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    Clipboard.SetDataObject(text, true);
                    return true;
                }
                catch (COMException)
                {
                    if (i == maxRetries - 1)
                    {
                        System.Diagnostics.Debug.WriteLine("Failed to set clipboard after max retries.");
                        return false;
                    }
                    Thread.Sleep(delayMs);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error setting clipboard: {ex.Message}");
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// Sets file drop list to clipboard with retry mechanism, setting both FileDrop and Text (path) formats.
        /// </summary>
        public static bool SetFileDropList(IEnumerable<string> filePaths)
        {
            if (filePaths == null) return false;

            var list = new StringCollection();
            foreach (var path in filePaths)
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    list.Add(path);
                }
            }

            if (list.Count == 0) return false;

            int maxRetries = 5;
            int delayMs = 50;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    var dataObject = new DataObject();
                    dataObject.SetFileDropList(list);
                    dataObject.SetText(string.Join(Environment.NewLine, list.Cast<string>()));
                    Clipboard.SetDataObject(dataObject, true);
                    return true;
                }
                catch (COMException)
                {
                    if (i == maxRetries - 1)
                    {
                        System.Diagnostics.Debug.WriteLine("Failed to set clipboard file drop list after max retries.");
                        return false;
                    }
                    Thread.Sleep(delayMs);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error setting clipboard file drop list: {ex.Message}");
                    return false;
                }
            }
            return false;
        }
    }
}
