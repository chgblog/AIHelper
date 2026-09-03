// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Runtime.InteropServices;

namespace AIHelper.Services
{
    /// <summary>
    /// 向前台窗口注入按键。用于把剪贴板中的图片/文件粘贴进 WebView2 里的输入框。
    /// </summary>
    public static class KeyboardInputService
    {
        private const ushort VK_CONTROL = 0x11;
        private const ushort VK_LCONTROL = 0xA2;
        private const ushort VK_RCONTROL = 0xA3;
        private const ushort VK_MENU = 0x12;
        private const ushort VK_LMENU = 0xA4;
        private const ushort VK_RMENU = 0xA5;
        private const ushort VK_SHIFT = 0x10;
        private const ushort VK_LSHIFT = 0xA0;
        private const ushort VK_RSHIFT = 0xA1;
        private const ushort VK_LWIN = 0x5B;
        private const ushort VK_RWIN = 0x5C;
        private const ushort VK_V = 0x56;

        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint INPUT_KEYBOARD = 1;

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        // 三个成员都要保留：SendInput 要求 cbSize == sizeof(INPUT)，
        // 只放 KEYBDINPUT 会让结构体在 x64 下变成 32 字节（正确值是 40），
        // 调用会直接以 ERROR_INVALID_PARAMETER 失败且不注入任何按键。
        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private static INPUT Key(ushort vk, bool up)
        {
            var input = new INPUT { type = INPUT_KEYBOARD };
            input.u.ki.wVk = vk;
            input.u.ki.dwFlags = up ? KEYEVENTF_KEYUP : 0;
            return input;
        }

        private static bool IsDown(ushort vk)
        {
            return (GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        private static bool Send(INPUT[] inputs)
        {
            if (inputs == null || inputs.Length == 0) return true;

            uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            if (sent != inputs.Length)
            {
                Logger.LogError($"SendInput injected {sent}/{inputs.Length} events (Win32 error {Marshal.GetLastWin32Error()})");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 释放仍被物理按住的修饰键。快捷键（如 Ctrl+Alt+X）触发本流程时用户往往还没松手，
        /// 此时直接发 Ctrl+V，Chromium 收到的是 Ctrl+Alt+V，粘贴不会发生。
        /// </summary>
        public static void ReleaseModifiers()
        {
            ushort[] modifiers =
            {
                VK_LMENU, VK_RMENU, VK_MENU,
                VK_LSHIFT, VK_RSHIFT, VK_SHIFT,
                VK_LWIN, VK_RWIN,
                VK_LCONTROL, VK_RCONTROL, VK_CONTROL
            };

            var ups = new System.Collections.Generic.List<INPUT>();
            foreach (ushort vk in modifiers)
            {
                if (IsDown(vk)) ups.Add(Key(vk, true));
            }

            if (ups.Count > 0)
            {
                Send(ups.ToArray());
            }
        }

        /// <summary>
        /// 模拟一次 Ctrl+V。返回是否成功把按键注入到输入队列。
        /// </summary>
        public static bool SendCtrlV()
        {
            ReleaseModifiers();

            INPUT[] inputs =
            {
                Key(VK_CONTROL, false),
                Key(VK_V, false),
                Key(VK_V, true),
                Key(VK_CONTROL, true)
            };

            return Send(inputs);
        }
    }
}
