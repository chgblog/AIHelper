// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using AIHelper.Models;

namespace AIHelper.Services
{
    public enum HotkeyConflictSource
    {
        None,
        Panel,
        MainWindow,
        Action,
        System
    }

    public class HotkeyConflictInfo
    {
        public HotkeyConflictSource Source { get; set; } = HotkeyConflictSource.None;
        public string ConflictTargetName { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
        public bool HasConflict => Source != HotkeyConflictSource.None;
    }

    /// <summary>
    /// Service for registering and handling global hotkeys
    /// </summary>
    public class HotkeyService : IDisposable
    {
        private static readonly Lazy<HotkeyService> _instance = new Lazy<HotkeyService>(() => new HotkeyService());
        public static HotkeyService Instance => _instance.Value;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;
        private const int WM_HOTKEY = 0x0312;

        private IntPtr _hWnd;
        private int _currentId = 9000;
        private readonly Dictionary<int, (uint modifiers, uint key)> _registeredHotkeys = new Dictionary<int, (uint, uint)>();

        /// <summary>
        /// Event fired when a registered hotkey is pressed
        /// </summary>
        public event Action<int> HotkeyPressed;

        /// <summary>
        /// Initializes the hotkey service with the given window
        /// </summary>
        public void Initialize(Window window)
        {
            var helper = new WindowInteropHelper(window);
            _hWnd = helper.Handle;
            HwndSource source = HwndSource.FromHwnd(_hWnd);
            source?.AddHook(HwndHook);
        }

        /// <summary>
        /// Registers a hotkey
        /// </summary>
        public int RegisterHotkey(string modifiers, string keyStr)
        {
            uint mod = ParseModifiers(modifiers) | MOD_NOREPEAT;
            uint key = ParseKey(keyStr);

            if (key == 0)
            {
                Logger.LogError($"Invalid hotkey key specified: '{keyStr}'");
                return -1;
            }

            int id = ++_currentId;
            bool success = RegisterHotKey(_hWnd, id, mod, key);

            if (success)
            {
                _registeredHotkeys[id] = (mod, key);
                return id;
            }
            
            int errorCode = Marshal.GetLastWin32Error();
            Logger.LogError($"Failed to register hotkey '{modifiers}+{keyStr}' (Win32 Error Code: {errorCode})");
            System.Diagnostics.Debug.WriteLine($"Failed to register hotkey {modifiers}+{keyStr}, Win32 Error: {errorCode}");
            return -1;
        }

        /// <summary>
        /// Unregisters a hotkey by ID
        /// </summary>
        public void UnregisterHotkey(int id)
        {
            if (_registeredHotkeys.ContainsKey(id))
            {
                UnregisterHotKey(_hWnd, id);
                _registeredHotkeys.Remove(id);
            }
        }

        /// <summary>
        /// Unregisters all registered hotkeys
        /// </summary>
        public void UnregisterAll()
        {
            foreach (var id in _registeredHotkeys.Keys)
            {
                UnregisterHotKey(_hWnd, id);
            }
            _registeredHotkeys.Clear();
        }

        public static uint ParseModifiers(string modifiersStr)
        {
            uint mod = 0;
            if (string.IsNullOrEmpty(modifiersStr)) return mod;
            
            var parts = modifiersStr.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var p = part.Trim().ToLowerInvariant();
                if (p == "ctrl" || p == "control") mod |= MOD_CONTROL;
                else if (p == "alt") mod |= MOD_ALT;
                else if (p == "shift") mod |= MOD_SHIFT;
                else if (p == "win" || p == "windows") mod |= MOD_WIN;
            }
            return mod;
        }

        public static uint ParseKey(string keyStr)
        {
            if (Enum.TryParse(keyStr, true, out Key key))
            {
                return (uint)KeyInterop.VirtualKeyFromKey(key);
            }
            return 0;
        }

        /// <summary>
        /// Checks whether two hotkey configurations are equal (normalizing modifier order & key case)
        /// </summary>
        public static bool AreHotkeysEqual(string mod1, string key1, string mod2, string key2)
        {
            if (string.IsNullOrWhiteSpace(key1) || string.IsNullOrWhiteSpace(key2)) return false;
            uint m1 = ParseModifiers(mod1);
            uint m2 = ParseModifiers(mod2);
            uint k1 = ParseKey(key1);
            uint k2 = ParseKey(key2);
            return k1 != 0 && k1 == k2 && m1 == m2;
        }

        /// <summary>
        /// Formats a hotkey combination for display
        /// </summary>
        public static string FormatHotkey(string modifiers, string key)
        {
            if (string.IsNullOrEmpty(key)) return LanguageManager.Instance["None"];
            string keyText = FormatKeyName(key);
            if (string.IsNullOrEmpty(modifiers)) return keyText;
            return modifiers.Replace("+", " + ") + " + " + keyText;
        }

        /// <summary>
        /// Formats raw key names (e.g. D1 -> 1, NumPad1 -> Num 1)
        /// </summary>
        public static string FormatKeyName(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            if (key.Length == 2 && key[0] == 'D' && char.IsDigit(key[1]))
                return key.Substring(1);
            if (key.StartsWith("NumPad") && key.Length > 6)
                return "Num " + key.Substring(6);
            return key;
        }

        /// <summary>
        /// Checks for internal application hotkey conflicts against Panel, MainWindow, and Actions
        /// </summary>
        /// <param name="modifiers">Modifiers string</param>
        /// <param name="key">Key string</param>
        /// <param name="currentSource">Source being tested: "Panel", "MainWindow", or "Action"</param>
        /// <param name="excludeActionId">Action ID to exclude if editing an existing action</param>
        /// <param name="settings">AppSettings containing current configuration</param>
        public static HotkeyConflictInfo CheckInternalConflict(string modifiers, string key, string currentSource, string excludeActionId, AppSettings settings)
        {
            var result = new HotkeyConflictInfo();
            if (string.IsNullOrWhiteSpace(key) || settings == null) return result;

            // 1. Check against Panel Hotkey
            if (currentSource != "Panel" && AreHotkeysEqual(modifiers, key, settings.PanelHotkeyModifiers, settings.PanelHotkeyKey))
            {
                result.Source = HotkeyConflictSource.Panel;
                result.ConflictTargetName = LanguageManager.Instance["Settings_Hotkey_PanelKey"].TrimEnd(':');
                result.ErrorMessage = LanguageManager.Instance["Hotkey_Conflict_Panel"];
                return result;
            }

            // 2. Check against MainWindow Hotkey
            if (currentSource != "MainWindow" && AreHotkeysEqual(modifiers, key, settings.MainWindowHotkeyModifiers, settings.MainWindowHotkeyKey))
            {
                result.Source = HotkeyConflictSource.MainWindow;
                result.ConflictTargetName = LanguageManager.Instance["Settings_Hotkey_MainWindowKey"].TrimEnd(':');
                result.ErrorMessage = LanguageManager.Instance["Hotkey_Conflict_MainWindow"];
                return result;
            }

            // 3. Check against Actions
            if (settings.Actions != null)
            {
                foreach (var action in settings.Actions)
                {
                    if (!string.IsNullOrEmpty(excludeActionId) && action.Id == excludeActionId) continue;
                    if (AreHotkeysEqual(modifiers, key, action.HotkeyModifiers, action.HotkeyKey))
                    {
                        result.Source = HotkeyConflictSource.Action;
                        result.ConflictTargetName = action.Name;
                        result.ErrorMessage = LanguageManager.Instance.GetString("Hotkey_Conflict_Action", action.Name);
                        return result;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Tests if a hotkey can be registered globally in the operating system (probe test)
        /// </summary>
        public bool TestGlobalHotkeyAvailability(string modifiers, string keyStr, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(keyStr)) return true;

            uint mod = ParseModifiers(modifiers) | MOD_NOREPEAT;
            uint key = ParseKey(keyStr);
            if (key == 0) return false;

            // If it's already registered by ourselves, it is valid
            foreach (var pair in _registeredHotkeys.Values)
            {
                if (pair.modifiers == mod && pair.key == key)
                {
                    return true;
                }
            }

            int testId = 99999;
            bool success = RegisterHotKey(_hWnd, testId, mod, key);
            if (success)
            {
                UnregisterHotKey(_hWnd, testId);
                return true;
            }

            int errorCode = Marshal.GetLastWin32Error();
            errorMessage = LanguageManager.Instance["Hotkey_Conflict_System"];
            Logger.LogWarning($"Global hotkey probe failed for '{modifiers}+{keyStr}' (Win32 Error: {errorCode})");
            return false;
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (_registeredHotkeys.ContainsKey(id))
                {
                    HotkeyPressed?.Invoke(id);
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            UnregisterAll();
        }
    }
}
