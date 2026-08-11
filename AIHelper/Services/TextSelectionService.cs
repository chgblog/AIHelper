using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace AIHelper.Services
{
    /// <summary>
    /// 全局划词检测服务
    /// </summary>
    public class TextSelectionService : IDisposable
    {
        private static readonly Lazy<TextSelectionService> _instance = new Lazy<TextSelectionService>(() => new TextSelectionService());
        
        /// <summary>
        /// 获取服务单例
        /// </summary>
        public static TextSelectionService Instance => _instance.Value;

        /// <summary>
        /// 划词成功时触发。参数: selectedText, screenPosition (鼠标位置)
        /// </summary>
        public event Action<string, Point> TextSelected;

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;

        private IntPtr _hookId = IntPtr.Zero;
        private Win32Api.LowLevelMouseProc _proc;

        private bool _isDisposed = false;
        private bool _isMouseDown = false;
        private Point _mouseDownPos;
        private DateTime _lastClickTime = DateTime.MinValue;
        private Point _lastClickPos;

        private CancellationTokenSource _debounceCts;
        private int _currentProcessId;

        /// <summary>
        /// 全局开关
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 是否允许使用剪贴板增强（Ctrl+C 降级方案）
        /// </summary>
        public bool EnableClipboardEnhancement { get; set; } = false;

        private TextSelectionService()
        {
            _proc = HookCallback;
            using (var process = Process.GetCurrentProcess())
            {
                _currentProcessId = process.Id;
            }
        }

        /// <summary>
        /// 安装钩子
        /// </summary>
        public void Install()
        {
            if (_hookId == IntPtr.Zero)
            {
                _hookId = SetHook(_proc);
                Debug.WriteLine("TextSelectionService: Hook installed.");
            }
        }

        /// <summary>
        /// 卸载钩子
        /// </summary>
        public void Uninstall()
        {
            if (_hookId != IntPtr.Zero)
            {
                Win32Api.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
                Debug.WriteLine("TextSelectionService: Hook uninstalled.");
            }
        }

        private IntPtr SetHook(Win32Api.LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return Win32Api.SetWindowsHookEx(WH_MOUSE_LL, proc, Win32Api.GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && IsEnabled)
            {
                if (wParam == (IntPtr)WM_LBUTTONDOWN)
                {
                    _isMouseDown = true;
                    Win32Api.MSLLHOOKSTRUCT hookStruct = (Win32Api.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Win32Api.MSLLHOOKSTRUCT));
                    _mouseDownPos = new Point(hookStruct.pt.x, hookStruct.pt.y);
                    
                    // 取消之前的等待
                    _debounceCts?.Cancel();
                }
                else if (wParam == (IntPtr)WM_LBUTTONUP && _isMouseDown)
                {
                    _isMouseDown = false;
                    Win32Api.MSLLHOOKSTRUCT hookStruct = (Win32Api.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Win32Api.MSLLHOOKSTRUCT));
                    Point mouseUpPos = new Point(hookStruct.pt.x, hookStruct.pt.y);
                    DateTime now = DateTime.Now;

                    double distance = Math.Sqrt(Math.Pow(mouseUpPos.X - _mouseDownPos.X, 2) + Math.Pow(mouseUpPos.Y - _mouseDownPos.Y, 2));

                    bool isDragSelection = distance >= 5;
                    bool isMultiClickSelection = false;

                    uint doubleClickTime = Win32Api.GetDoubleClickTime();
                    if (!isDragSelection)
                    {
                        double clickTimeDiff = (now - _lastClickTime).TotalMilliseconds;
                        double clickDist = Math.Sqrt(Math.Pow(mouseUpPos.X - _lastClickPos.X, 2) + Math.Pow(mouseUpPos.Y - _lastClickPos.Y, 2));

                        if (clickTimeDiff > 0 && clickTimeDiff <= doubleClickTime && clickDist < 10)
                        {
                            isMultiClickSelection = true;
                        }
                    }

                    _lastClickTime = now;
                    _lastClickPos = mouseUpPos;

                    if (isDragSelection || isMultiClickSelection)
                    {
                        IntPtr hwnd = Win32Api.GetForegroundWindow();
                        Win32Api.GetWindowThreadProcessId(hwnd, out uint processId);

                        if (processId != (uint)_currentProcessId)
                        {
                            _debounceCts?.Cancel();
                            _debounceCts = new CancellationTokenSource();
                            var token = _debounceCts.Token;
                            int delayMs = isMultiClickSelection ? 150 : 250;

                            Task.Delay(delayMs, token).ContinueWith(t =>
                            {
                                if (!t.IsCanceled)
                                {
                                    ProcessSelectionAsync(mouseUpPos, token);
                                }
                            }, TaskScheduler.Default);
                        }
                        else
                        {
                            Debug.WriteLine("TextSelectionService: Ignored self window.");
                        }
                    }
                }
            }
            return Win32Api.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private void ProcessSelectionAsync(Point mousePos, CancellationToken token)
        {
            var thread = new Thread(() =>
            {
                try
                {
                    if (token.IsCancellationRequested) return;

                    // 1. 优先尝试通过 UI Automation 获取选中的文本 (无键盘按键与剪贴板污染)
                    string uiAutomationText = GetSelectedTextViaUIAutomation(mousePos);
                    if (!string.IsNullOrWhiteSpace(uiAutomationText))
                    {
                        string trimmed = uiAutomationText.Trim();
                        if (trimmed.Length > 0)
                        {
                            Debug.WriteLine($"TextSelectionService: Selected text via UI Automation ({trimmed.Length} chars).");
                            Application.Current?.Dispatcher.InvokeAsync(() =>
                            {
                                if (!token.IsCancellationRequested)
                                {
                                    TextSelected?.Invoke(trimmed, mousePos);
                                }
                            });
                            return;
                        }
                    }

                    if (!EnableClipboardEnhancement)
                    {
                        Debug.WriteLine("TextSelectionService: UI Automation yielded no text and Clipboard Enhancement is disabled. Skipping fallback.");
                        return;
                    }

                    Debug.WriteLine("TextSelectionService: UI Automation yielded no text, falling back to Clipboard/Ctrl+C.");

                    // 2. 降级方案：安全抓取当前剪贴板状态（支持 DataObject 多格式及 Plain Text 备份）
                    IDataObject originalDataObject = null;
                    string originalText = null;
                    bool hadOriginalData = false;

                    try
                    {
                        IDataObject currentData = Clipboard.GetDataObject();
                        if (currentData != null)
                        {
                            string[] formats = currentData.GetFormats(false);
                            if (formats != null && formats.Length > 0)
                            {
                                DataObject clonedData = new DataObject();
                                foreach (string format in formats)
                                {
                                    try
                                    {
                                        object data = currentData.GetData(format, false);
                                        if (data != null)
                                        {
                                            clonedData.SetData(format, data);
                                        }
                                    }
                                    catch { }
                                }
                                if (clonedData.GetFormats().Length > 0)
                                {
                                    originalDataObject = clonedData;
                                    hadOriginalData = true;
                                }
                            }
                        }

                        if (Clipboard.ContainsText())
                        {
                            originalText = Clipboard.GetText();
                            hadOriginalData = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"TextSelectionService: Failed to read initial clipboard. {ex.Message}");
                    }

                    if (token.IsCancellationRequested) return;

                    // 3. 尝试清空剪贴板，以便精准识别 Ctrl+C 写入的新文本
                    try
                    {
                        Clipboard.Clear();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"TextSelectionService: Failed to clear clipboard. {ex.Message}");
                    }

                    // 4. 模拟 Ctrl+C
                    SimulateCtrlC();

                    // 5. 后台轮询等待应用写入剪贴板（最多 300ms，每 30ms 重试，不阻塞主线程）
                    string selectedText = null;
                    for (int i = 0; i < 10; i++)
                    {
                        if (token.IsCancellationRequested) break;
                        Thread.Sleep(30);

                        try
                        {
                            if (Clipboard.ContainsText())
                            {
                                string text = Clipboard.GetText();
                                if (!string.IsNullOrWhiteSpace(text))
                                {
                                    selectedText = text;
                                    break;
                                }
                            }
                        }
                        catch (COMException) { }
                        catch (ExternalException) { }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"TextSelectionService: Clipboard poll retry failed. {ex.Message}");
                        }
                    }

                    // 6. 抓取完成或超时后，无条件恢复用户原始剪贴板内容
                    RestoreClipboard(originalDataObject, originalText, hadOriginalData);

                    // 7. 成功抓取选中文本，抛出事件通知 UI 显示工具条
                    if (!token.IsCancellationRequested && !string.IsNullOrWhiteSpace(selectedText))
                    {
                        string trimmed = selectedText.Trim();
                        if (trimmed.Length > 0)
                        {
                            Debug.WriteLine($"TextSelectionService: Selected text via Clipboard: {trimmed.Length} chars.");
                            Application.Current?.Dispatcher.InvokeAsync(() =>
                            {
                                if (!token.IsCancellationRequested)
                                {
                                    TextSelected?.Invoke(trimmed, mousePos);
                                }
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"TextSelectionService Error: {ex}");
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>
        /// 使用 UI Automation 获取选中的文本
        /// </summary>
        private string GetSelectedTextViaUIAutomation(Point mousePos)
        {
            try
            {
                // 1. 优先尝试从鼠标所在的物理屏幕位置获取 AutomationElement
                AutomationElement element = null;
                try
                {
                    element = AutomationElement.FromPoint(new System.Windows.Point(mousePos.X, mousePos.Y));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"TextSelectionService UIAutomation FromPoint error: {ex.Message}");
                }

                if (element != null)
                {
                    string text = ExtractSelectedTextFromElement(element);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }

                // 2. 若根据 Point 未能提取选中文本，尝试从系统全局焦点元素获取
                try
                {
                    AutomationElement focusedElement = AutomationElement.FocusedElement;
                    if (focusedElement != null && focusedElement != element)
                    {
                        string text = ExtractSelectedTextFromElement(focusedElement);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return text;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"TextSelectionService UIAutomation FocusedElement error: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TextSelectionService GetSelectedTextViaUIAutomation error: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 从 AutomationElement 中尝试提取 TextPattern 选中的文本
        /// </summary>
        private string ExtractSelectedTextFromElement(AutomationElement element)
        {
            if (element == null) return null;

            try
            {
                // 尝试获取 TextPattern
                if (element.TryGetCurrentPattern(TextPattern.Pattern, out object patternObj))
                {
                    TextPattern textPattern = patternObj as TextPattern;
                    if (textPattern != null)
                    {
                        TextPatternRange[] selectionRanges = textPattern.GetSelection();
                        if (selectionRanges != null && selectionRanges.Length > 0)
                        {
                            StringBuilder sb = new StringBuilder();
                            foreach (var range in selectionRanges)
                            {
                                string rangeText = range.GetText(-1);
                                if (!string.IsNullOrEmpty(rangeText))
                                {
                                    sb.Append(rangeText);
                                }
                            }

                            if (sb.Length > 0)
                            {
                                return sb.ToString();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TextSelectionService ExtractSelectedTextFromElement error: {ex.Message}");
            }

            return null;
        }

        private void RestoreClipboard(IDataObject originalDataObject, string originalText, bool hadOriginalData)
        {
            const int maxRetries = 5;
            const int retryDelayMs = 30;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    if (hadOriginalData)
                    {
                        if (originalDataObject != null && originalDataObject.GetFormats().Length > 0)
                        {
                            Clipboard.SetDataObject(originalDataObject, true);
                            return;
                        }
                        else if (!string.IsNullOrEmpty(originalText))
                        {
                            Clipboard.SetText(originalText);
                            return;
                        }
                    }
                    else
                    {
                        Clipboard.Clear();
                        return;
                    }
                }
                catch (COMException) { }
                catch (ExternalException) { }
                catch (Exception ex)
                {
                    Debug.WriteLine($"TextSelectionService: Restore clipboard retry {i} failed. {ex.Message}");
                }
                Thread.Sleep(retryDelayMs);
            }
        }

        private void SimulateCtrlC()
        {
            const ushort VK_CONTROL = 0x11;
            const ushort VK_C = 0x43;
            const uint KEYEVENTF_KEYUP = 0x0002;

            Win32Api.INPUT[] inputs = new Win32Api.INPUT[4];

            // Ctrl down
            inputs[0].type = Win32Api.INPUT_KEYBOARD;
            inputs[0].u.ki.wVk = VK_CONTROL;

            // C down
            inputs[1].type = Win32Api.INPUT_KEYBOARD;
            inputs[1].u.ki.wVk = VK_C;

            // C up
            inputs[2].type = Win32Api.INPUT_KEYBOARD;
            inputs[2].u.ki.wVk = VK_C;
            inputs[2].u.ki.dwFlags = KEYEVENTF_KEYUP;

            // Ctrl up
            inputs[3].type = Win32Api.INPUT_KEYBOARD;
            inputs[3].u.ki.wVk = VK_CONTROL;
            inputs[3].u.ki.dwFlags = KEYEVENTF_KEYUP;

            Win32Api.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Win32Api.INPUT)));
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                Uninstall();
                _debounceCts?.Cancel();
                _debounceCts?.Dispose();
                _isDisposed = true;
            }
        }
    }

    /// <summary>
    /// Win32 API 辅助类
    /// </summary>
    internal static class Win32Api
    {
        public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern uint GetDoubleClickTime();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        public const int INPUT_KEYBOARD = 1;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }
    }
}
