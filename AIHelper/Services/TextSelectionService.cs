// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Interop.UIAutomationClient;

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

        private static readonly Lazy<IUIAutomation> _automation = new Lazy<IUIAutomation>(CreateAutomationInstance);
        private const int UIA_TextPatternId = 10014;

        private static IUIAutomation CreateAutomationInstance()
        {
            try
            {
                return new CUIAutomation8();
            }
            catch
            {
                return new CUIAutomation();
            }
        }

        /// <summary>
        /// 划词成功时触发。参数: selectedText, screenPosition (鼠标位置)
        /// </summary>
        public event Action<string, Point> TextSelected;

        /// <summary>
        /// 请求关闭工具条时触发（如用户点击鼠标右键）
        /// </summary>
        public event Action DismissRequested;

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;

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

        /// <summary>
        /// 划词弹出工具条应用范围模式 (0: 全部应用 [默认], 1: 指定应用, 2: 排除应用)
        /// </summary>
        public int AppScopeMode { get; set; } = 0;

        /// <summary>
        /// 划词弹出工具条指定/排除的应用进程名列表 (例如: notepad.exe, chrome.exe，换行或逗号分隔)
        /// </summary>
        public string AppScopeApps { get; set; } = "";

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
            if (_hookId != IntPtr.Zero) return;

            try
            {
                _hookId = SetHook(_proc);
                if (_hookId == IntPtr.Zero)
                {
                    Logger.LogError($"TextSelectionService: SetWindowsHookEx failed. Win32Error={Marshal.GetLastWin32Error()}");
                }
                else
                {
                    Logger.LogInfo("TextSelectionService: Hook installed.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("TextSelectionService: Failed to install mouse hook.", ex);
            }
        }

        /// <summary>
        /// 卸载钩子
        /// </summary>
        public void Uninstall()
        {
            if (_hookId == IntPtr.Zero) return;

            try
            {
                Win32Api.UnhookWindowsHookEx(_hookId);
                Logger.LogInfo("TextSelectionService: Hook uninstalled.");
            }
            catch (Exception ex)
            {
                Logger.LogError("TextSelectionService: Failed to uninstall mouse hook.", ex);
            }
            finally
            {
                _hookId = IntPtr.Zero;
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

        /// <summary>
        /// 低级鼠标钩子回调。该方法由系统从非托管代码直接调用，
        /// 任何逃逸出去的异常都会穿过 user32 的调用栈直接终止进程，
        /// 因此这里必须捕获所有异常，绝不能向外抛出。
        /// </summary>
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                HandleMouseEvent(nCode, wParam, lParam);
            }
            catch (Exception ex)
            {
                Logger.LogError("TextSelectionService: Unhandled exception in mouse hook callback.", ex);
            }

            return Win32Api.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private void HandleMouseEvent(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && IsEnabled)
            {
                if (wParam == (IntPtr)WM_RBUTTONDOWN)
                {
                    _isMouseDown = false;
                    CancelPendingDebounce();
                    Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            DismissRequested?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("TextSelectionService: DismissRequested handler threw.", ex);
                        }
                    });
                }
                else if (wParam == (IntPtr)WM_LBUTTONDOWN)
                {
                    _isMouseDown = true;
                    Win32Api.MSLLHOOKSTRUCT hookStruct = (Win32Api.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Win32Api.MSLLHOOKSTRUCT));
                    _mouseDownPos = new Point(hookStruct.pt.x, hookStruct.pt.y);
                    
                    // 取消之前的等待
                    CancelPendingDebounce();
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

                        if (processId != (uint)_currentProcessId && IsProcessInScope(processId))
                        {
                            CancelPendingDebounce();
                            _debounceCts = new CancellationTokenSource();
                            var token = _debounceCts.Token;
                            int delayMs = isMultiClickSelection ? 150 : 250;

                            Task.Delay(delayMs, token).ContinueWith(t =>
                            {
                                try
                                {
                                    if (!t.IsCanceled)
                                    {
                                        ProcessSelectionAsync(mouseUpPos, token);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.LogError("TextSelectionService: Failed to start selection worker.", ex);
                                }
                            }, TaskScheduler.Default);
                        }
                        else
                        {
                            Debug.WriteLine("TextSelectionService: Ignored self window or out of scope app.");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 取消上一次待处理的划词请求。CancellationTokenSource 已被释放时 Cancel 会抛
        /// ObjectDisposedException，而调用方处在钩子回调里，所以这里必须吞掉异常。
        /// </summary>
        private void CancelPendingDebounce()
        {
            try
            {
                _debounceCts?.Cancel();
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                Logger.LogError("TextSelectionService: Failed to cancel pending selection.", ex);
            }
        }

        private void ProcessSelectionAsync(Point mousePos, CancellationToken token)
        {
            // Clipboard（WPF）只能在 STA 线程访问；而 UI Automation 客户端按微软文档必须
            // 运行在 MTA —— STA 下没有消息泵，COM 回调会挂死甚至连累调用方，
            // 所以按本次实际要走的分支选择套间模型。
            bool useClipboardFallback = EnableClipboardEnhancement;

            var thread = new Thread(() =>
            {
                try
                {
                    if (token.IsCancellationRequested) return;

                    Logger.LogDebug($"TextSelectionService: Processing selection at ({mousePos.X},{mousePos.Y}) in '{GetForegroundProcessName()}'.");

                    // 1. 优先尝试通过 UI Automation 获取选中的文本 (无键盘按键与剪贴板污染)
                    string uiAutomationText = GetSelectedTextViaUIAutomation(mousePos);
                    if (!string.IsNullOrWhiteSpace(uiAutomationText))
                    {
                        string trimmed = uiAutomationText.Trim();
                        if (trimmed.Length > 0)
                        {
                            Logger.LogDebug($"TextSelectionService: Selected text via UI Automation ({trimmed.Length} chars).");
                            RaiseTextSelected(trimmed, mousePos, token);
                            return;
                        }
                    }

                    if (!EnableClipboardEnhancement)
                    {
                        Logger.LogDebug("TextSelectionService: UI Automation yielded no text and Clipboard Enhancement is disabled. Skipping fallback.");
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
                            Logger.LogDebug($"TextSelectionService: Selected text via Clipboard ({trimmed.Length} chars).");
                            RaiseTextSelected(trimmed, mousePos, token);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("TextSelectionService: Selection worker failed.", ex);
                }
            });

            thread.Name = "AIHelper.TextSelection";
            thread.SetApartmentState(useClipboardFallback ? ApartmentState.STA : ApartmentState.MTA);
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>
        /// 取前台窗口的进程名，仅用于日志定位是哪个宿主程序导致的异常
        /// </summary>
        private static string GetForegroundProcessName()
        {
            try
            {
                IntPtr hwnd = Win32Api.GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return "?";

                Win32Api.GetWindowThreadProcessId(hwnd, out uint pid);
                using (var proc = Process.GetProcessById((int)pid))
                {
                    return proc.ProcessName;
                }
            }
            catch
            {
                return "?";
            }
        }

        /// <summary>
        /// 把划词结果切回 UI 线程。订阅方（工具条）抛出的异常在这里就地记录，
        /// 不让它冒泡到 Dispatcher 造成整个程序退出。
        /// </summary>
        private void RaiseTextSelected(string text, Point mousePos, CancellationToken token)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted)
            {
                Logger.LogWarning("TextSelectionService: Dispatcher unavailable, dropping selection.");
                return;
            }

            dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (!token.IsCancellationRequested)
                    {
                        TextSelected?.Invoke(text, mousePos);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("TextSelectionService: TextSelected handler threw.", ex);
                }
            });
        }

        /// <summary>
        /// 使用原生 COM IUIAutomation 获取选中的文本
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        private string GetSelectedTextViaUIAutomation(Point mousePos)
        {
            IUIAutomationElement element = null;
            try
            {
                var automation = _automation.Value;
                if (automation == null) return null;

                // 1. 优先尝试从鼠标所在的物理屏幕位置获取 AutomationElement
                tagPOINT pt = new tagPOINT { x = (int)mousePos.X, y = (int)mousePos.Y };
                try
                {
                    element = automation.ElementFromPoint(pt);
                }
                catch (COMException ex)
                {
                    Logger.LogWarning($"TextSelectionService: ElementFromPoint COMException (0x{ex.ErrorCode:X8}): {ex.Message}");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"TextSelectionService: ElementFromPoint failed at ({mousePos.X},{mousePos.Y}). {ex.GetType().Name}: {ex.Message}");
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
                IUIAutomationElement focusedElement = null;
                try
                {
                    focusedElement = automation.GetFocusedElement();
                    if (focusedElement != null)
                    {
                        string text = ExtractSelectedTextFromElement(focusedElement);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return text;
                        }
                    }
                }
                catch (COMException ex)
                {
                    Logger.LogWarning($"TextSelectionService: GetFocusedElement COMException (0x{ex.ErrorCode:X8}): {ex.Message}");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"TextSelectionService: UIAutomation FocusedElement failed. {ex.GetType().Name}: {ex.Message}");
                }
                finally
                {
                    if (focusedElement != null)
                    {
                        try { Marshal.ReleaseComObject(focusedElement); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("TextSelectionService: GetSelectedTextViaUIAutomation failed.", ex);
            }
            finally
            {
                if (element != null)
                {
                    try { Marshal.ReleaseComObject(element); } catch { }
                }
            }

            return null;
        }

        /// <summary>
        /// 从 IUIAutomationElement 中尝试提取 TextPattern 选中的文本
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        private string ExtractSelectedTextFromElement(IUIAutomationElement element)
        {
            if (element == null) return null;

            IUIAutomationTextPattern textPattern = null;
            IUIAutomationTextRangeArray selectionRanges = null;

            try
            {
                object patternObj = null;
                try
                {
                    patternObj = element.GetCurrentPattern(UIA_TextPatternId);
                }
                catch (COMException ex)
                {
                    Logger.LogWarning($"TextSelectionService: GetCurrentPattern COMException (0x{ex.ErrorCode:X8}): {ex.Message}");
                    return null;
                }

                textPattern = patternObj as IUIAutomationTextPattern;
                if (textPattern == null) return null;

                try
                {
                    selectionRanges = textPattern.GetSelection();
                }
                catch (COMException ex)
                {
                    Logger.LogWarning($"TextSelectionService: TextPattern.GetSelection COMException (0x{ex.ErrorCode:X8}): {ex.Message}");
                    return null;
                }

                if (selectionRanges != null && selectionRanges.Length > 0)
                {
                    StringBuilder sb = new StringBuilder();
                    int count = selectionRanges.Length;

                    for (int i = 0; i < count; i++)
                    {
                        IUIAutomationTextRange range = null;
                        try
                        {
                            range = selectionRanges.GetElement(i);
                            if (range != null)
                            {
                                // 限制单次获取最多 10000 字符，避免传入 -1 导致 Chromium 内部缓冲区异常
                                string rangeText = range.GetText(10000);
                                if (!string.IsNullOrEmpty(rangeText))
                                {
                                    sb.Append(rangeText);
                                }
                            }
                        }
                        catch (COMException ex)
                        {
                            Logger.LogWarning($"TextSelectionService: Range.GetText COMException (0x{ex.ErrorCode:X8}): {ex.Message}");
                        }
                        finally
                        {
                            if (range != null)
                            {
                                try { Marshal.ReleaseComObject(range); } catch { }
                            }
                        }
                    }

                    if (sb.Length > 0)
                    {
                        return sb.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"TextSelectionService: ExtractSelectedTextFromElement failed. {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (selectionRanges != null)
                {
                    try { Marshal.ReleaseComObject(selectionRanges); } catch { }
                }
                if (textPattern != null)
                {
                    try { Marshal.ReleaseComObject(textPattern); } catch { }
                }
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

        private bool IsProcessInScope(uint processId)
        {
            if (AppScopeMode == 0) return true; // 全部应用

            var rawApps = AppScopeApps;
            if (string.IsNullOrWhiteSpace(rawApps))
            {
                // 如果是指定应用模式且列表为空 -> 不触发；如果是排除应用模式且列表为空 -> 全部允许
                return AppScopeMode == 2;
            }

            var appList = rawApps.Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            var targetApps = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in appList)
            {
                string clean = item.Trim();
                if (clean.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    clean = clean.Substring(0, clean.Length - 4);
                }
                if (!string.IsNullOrEmpty(clean))
                {
                    targetApps.Add(clean);
                }
            }

            if (targetApps.Count == 0)
            {
                return AppScopeMode == 2;
            }

            string processName = null;
            try
            {
                using (var proc = Process.GetProcessById((int)processId))
                {
                    processName = proc.ProcessName;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TextSelectionService: Failed to get process name for PID {processId}: {ex.Message}");
            }

            if (string.IsNullOrEmpty(processName))
            {
                return true;
            }

            if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                processName = processName.Substring(0, processName.Length - 4);
            }

            bool isMatch = targetApps.Contains(processName);

            if (AppScopeMode == 1) // 指定应用
            {
                return isMatch;
            }
            else if (AppScopeMode == 2) // 排除应用
            {
                return !isMatch;
            }

            return true;
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            IsEnabled = false;

            try
            {
                Uninstall();

                var cts = _debounceCts;
                _debounceCts = null;
                cts?.Cancel();
                cts?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.LogError("TextSelectionService: Dispose failed.", ex);
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
