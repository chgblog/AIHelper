using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace AIHelper.Services
{
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

        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;
        private const int WM_HOTKEY = 0x0312;

        private IntPtr _hWnd;
        private int _currentId = 9000;
        private Dictionary<int, (uint modifiers, uint key)> _registeredHotkeys = new Dictionary<int, (uint, uint)>();

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
            source.AddHook(HwndHook);
        }

        /// <summary>
        /// Registers a hotkey
        /// </summary>
        public int RegisterHotkey(string modifiers, string keyStr)
        {
            uint mod = ParseModifiers(modifiers) | MOD_NOREPEAT;
            uint key = ParseKey(keyStr);

            if (key == 0) return -1;

            int id = ++_currentId;
            bool success = RegisterHotKey(_hWnd, id, mod, key);

            if (success)
            {
                _registeredHotkeys[id] = (mod, key);
                return id;
            }
            
            System.Diagnostics.Debug.WriteLine($"Failed to register hotkey {modifiers}+{keyStr}");
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

        private uint ParseModifiers(string modifiersStr)
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

        private uint ParseKey(string keyStr)
        {
            if (Enum.TryParse(keyStr, true, out Key key))
            {
                return (uint)KeyInterop.VirtualKeyFromKey(key);
            }
            return 0;
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
