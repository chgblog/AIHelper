using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace AIHelper.Services
{
    /// <summary>
    /// Singleton manager for application multi-language localization
    /// </summary>
    public class LanguageManager : INotifyPropertyChanged
    {
        private static LanguageManager _instance;
        private static readonly object _lock = new object();
        private string _currentLanguage = "zh";

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler LanguageChanged;

        public static LanguageManager Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new LanguageManager();
                    }
                    return _instance;
                }
            }
        }

        private LanguageManager()
        {
            _currentLanguage = GetDefaultLanguageByTimeZone();
        }

        public string CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                string lang = string.Equals(value, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "zh";
                if (_currentLanguage != lang)
                {
                    _currentLanguage = lang;
                    OnPropertyChanged("CurrentLanguage");
                    OnPropertyChanged("Item[]");
                    OnPropertyChanged(null);
                    LanguageChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public string this[string key] => GetString(key);

        public string GetString(string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            var dict = _currentLanguage == "en" ? _enDict : _zhDict;
            if (!dict.TryGetValue(key, out string val))
            {
                if (!_zhDict.TryGetValue(key, out val))
                {
                    val = key;
                }
            }

            if (args != null && args.Length > 0)
            {
                try
                {
                    return string.Format(val, args);
                }
                catch
                {
                    return val;
                }
            }
            return val;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Determines default language based on the local time zone.
        /// Mainland China, Hong Kong, Taiwan, and Macao default to Chinese ("zh").
        /// All other time zones default to English ("en").
        /// </summary>
        public static string GetDefaultLanguageByTimeZone()
        {
            try
            {
                var tz = TimeZoneInfo.Local;
                var id = tz.Id;
                var name = tz.DisplayName;

                string[] chineseTimeZoneIds = new[]
                {
                    "China Standard Time",
                    "Taipei Standard Time",
                    "Hong Kong Standard Time",
                    "Macau Standard Time",
                    "Asia/Shanghai",
                    "Asia/Chongqing",
                    "Asia/Harbin",
                    "Asia/Urumqi",
                    "Asia/Kashgar",
                    "Asia/Hong_Kong",
                    "Asia/Macau",
                    "Asia/Taipei"
                };

                if (chineseTimeZoneIds.Any(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase)))
                {
                    return "zh";
                }

                if (!string.IsNullOrEmpty(name))
                {
                    if (name.Contains("Beijing") || name.Contains("Shanghai") || name.Contains("Taipei") ||
                        name.Contains("Hong Kong") || name.Contains("Macau") || name.Contains("Urumqi") ||
                        name.Contains("北京") || name.Contains("重庆") || name.Contains("香港") ||
                        name.Contains("澳门") || name.Contains("台北"))
                    {
                        return "zh";
                    }
                }
            }
            catch
            {
            }

            return "en";
        }

        private static readonly Dictionary<string, string> _zhDict = new Dictionary<string, string>
        {
            // Common
            { "AppName", "AI助手" },
            { "OK", "确定" },
            { "Cancel", "取消" },
            { "Save", "保存" },
            { "Add", "添加" },
            { "Edit", "编辑" },
            { "Delete", "删除" },
            { "Notice", "提示" },
            { "Error", "错误" },
            { "None", "无" },

            // MainWindow
            { "Main_Settings", "⚙ 设置" },
            { "Main_Status_Ready", "就绪" },
            { "Main_Status_WaitingBrowser", "正在等待浏览器组件就绪..." },
            { "Main_Status_BrowserInitFailed", "浏览器组件初始化失败: {0}" },
            { "Main_Status_NavigatedTo", "已导航到 {0}" },
            { "Main_Status_WebView2InitFailed", "WebView2 初始化失败: {0}" },
            { "Main_Status_Executing", "正在执行: {0}..." },
            { "Main_Status_Success", "成功: {0}" },
            { "Main_Status_Failed", "失败: {0}" },
            { "Main_Status_Submitting", "正在提交..." },
            { "Main_Status_Injecting", "正在注入..." },
            { "Main_Status_SubmitSuccess", "提交成功" },
            { "Main_Status_InjectSuccess", "注入成功" },
            { "Main_Status_OpFailed", "操作失败: {0}" },
            { "Main_Status_NavigatingTo", "正在导航到 {0}..." },
            { "Main_Status_PageLoadSuccess", "页面加载完成" },
            { "Main_Status_PageLoadFailed", "页面加载失败: {0}" },

            // ActionPanelControl
            { "ActionPanel_ContentHeader", "📋 内容" },
            { "ActionPanel_AvailableActions", "可用操作:" },

            // SettingsWindow
            { "Settings_Title", "设置" },
            { "Settings_Tab_General", "常规设置" },
            { "Settings_Tab_SelectionToolbar", "划词设置" },
            { "Settings_Tab_Platforms", "平台管理" },
            { "Settings_Tab_Actions", "操作管理" },
            { "Settings_Tab_Hotkeys", "快捷键设置" },
            { "Settings_Tab_About", "项目与更新" },

            { "Settings_General_Options", "常规选项" },
            { "Settings_General_Language", "界面语言:" },
            { "Settings_General_Lang_ZH", "中文 (Chinese)" },
            { "Settings_General_Lang_EN", "English" },
            { "Settings_General_AutoStart", "开机自动启动 AIHelper" },
            { "Settings_General_AutoStartTip", "勾选后，Windows 登录时将自动在后台运行 AIHelper" },
            { "Settings_General_AutoSubmit", "自动提交提示词" },
            { "Settings_General_AutoSubmitTip", "勾选后注入提示词后自动点击发送；未勾选时仅点击新会话并注入提示词，不点击发送按钮" },
            { "Settings_General_ProxySettings", "网络代理设置" },
            { "Settings_General_ProxyServer", "代理服务器:" },
            { "Settings_General_ProxyTip", "例如: http://127.0.0.1:7890 或 socks5://127.0.0.1:1080。留空则自动使用 Windows 系统代理" },

            { "Settings_Platform_Name", "名称" },
            { "Settings_Platform_Url", "URL" },
            { "Settings_Platform_Active", "激活" },
            { "Settings_Platform_NewPlatform", "新平台" },
            { "Settings_Platform_EmptyWarn", "平台列表不能为空。" },
            { "Settings_Platform_SelectEditWarn", "请先选择要编辑的平台。" },
            { "Settings_ProxyChangedNotice", "代理设置已更改，将在重启应用后完全生效。" },

            { "Settings_Action_Name", "名称" },
            { "Settings_Action_Prompt", "提示词" },
            { "Settings_Action_SortOrder", "排序" },
            { "Settings_Action_Hotkey", "快捷键:" },
            { "Settings_Action_ApplyEdit", "应用修改" },
            { "Settings_Action_NewAction", "新操作" },
            { "Settings_Action_SelectEditWarn", "请先选择要编辑的操作。" },
            { "MoveUp", "上移" },
            { "MoveDown", "下移" },

            { "Settings_Hotkey_PanelKey", "面板唤醒快捷键:" },
            { "Settings_Hotkey_Tip", "请点击输入框并按下快捷键组合" },

            { "Settings_About_Title", "项目与更新信息" },
            { "Settings_About_ProjectUrl", "项目地址" },
            { "Settings_About_OpenProject", "进入项目主页" },
            { "Settings_About_UpdateUrl", "更新地址" },
            { "Settings_About_OpenUpdate", "进入 Release 列表" },
            { "Settings_About_Tip", "提示：点击地址链接或按钮即可在默认浏览器中打开对应页面。" },
            { "Settings_About_OpenUrlError", "无法打开链接: {0}" },

            // PlatformEditWindow
            { "PlatformEdit_Title_Edit", "编辑平台" },
            { "PlatformEdit_Title_Add", "添加平台" },
            { "PlatformEdit_Name", "名称:" },
            { "PlatformEdit_Url", "URL:" },
            { "PlatformEdit_NewChat", "新会话定位:" },
            { "PlatformEdit_Input", "输入框定位:" },
            { "PlatformEdit_Submit", "提交按钮定位:" },
            { "PlatformEdit_Pick", "🎯 拾取" },
            { "PlatformEdit_NewChatTT", "CSS 选择器，用于定位 AI 平台的新会话按钮。留空则使用自动检测。" },
            { "PlatformEdit_NewChatBtnTT", "打开平台页面，点击选择新会话按钮元素" },
            { "PlatformEdit_InputTT", "CSS 选择器，用于定位 AI 平台的输入框。留空则使用自动检测。" },
            { "PlatformEdit_InputBtnTT", "打开平台页面，点击选择输入框元素" },
            { "PlatformEdit_SubmitTT", "CSS 选择器，用于定位 AI 平台的提交按钮。留空则使用自动检测。" },
            { "PlatformEdit_SubmitBtnTT", "打开平台页面，点击选择提交按钮元素" },
            { "PlatformEdit_Tip", "提示: 选择器留空时将使用内置的自动检测逻辑。点击「拾取」可在平台页面上可视化选择元素。" },
            { "PlatformEdit_InvalidUrlWarn", "请先填写有效的平台 URL。" },
            { "PlatformEdit_EmptyNameWarn", "名称不能为空。" },
            { "PlatformEdit_EmptyUrlWarn", "URL 不能为空。" },

            // ActionEditWindow
            { "ActionEdit_Title_Edit", "编辑操作" },
            { "ActionEdit_Title_Add", "添加操作" },
            { "ActionEdit_Name", "名称:" },
            { "ActionEdit_Icon", "图标 (Emoji):" },
            { "ActionEdit_SortOrder", "排序号:" },
            { "ActionEdit_Hotkey", "快捷键:" },
            { "ActionEdit_Prompt", "提示词:" },
            { "ActionEdit_EmptyNameWarn", "名称不能为空。" },
            { "ActionEdit_ClearHotkey", "清除" },

            // ElementPickerWindow
            { "ElementPicker_Title", "元素选择器" },
            { "ElementPicker_Instruction", "请点击页面上的目标元素" },
            { "ElementPicker_Retry", "重新拾取" },
            { "ElementPicker_Status_Loading", "正在加载页面..." },
            { "ElementPicker_Status_InitFailed", "WebView2 初始化失败: {0}" },
            { "ElementPicker_Status_LoadSuccess", "页面加载完成，正在注入拾取脚本..." },
            { "ElementPicker_Status_LoadFailed", "页面加载失败: {0}" },
            { "ElementPicker_Status_ScriptMissing", "拾取脚本丢失" },
            { "ElementPicker_Status_Active", "✅ 拾取模式已激活 — 鼠标悬停查看元素，点击选择，按 Esc 取消" },
            { "ElementPicker_Status_InjectFailed", "注入脚本失败: {0}" },
            { "ElementPicker_Status_Selected", "已选择: {0} → {1}" },
            { "ElementPicker_Status_Reinjecting", "正在重新注入拾取脚本..." },

            // Tray Menu
            { "Tray_Show", "显示主窗口" },
            { "Tray_Settings", "打开设置" },
            { "Tray_SelectionToolbar", "开启划词搜索" },
            { "Tray_Exit", "退出" },

            // Injection Messages
            { "Inject_WebviewNotReady", "浏览器组件未就绪" },
            { "Inject_ScriptNotFound", "注入脚本丢失" },
            { "Inject_NoResult", "注入失败，未获取到结果" },
            { "Inject_FormatError", "注入失败，结果格式错误" },
            { "Inject_SendSuccess", "发送成功" },
            { "Inject_InjectSuccess", "注入成功" },
            { "Inject_NotLoggedIn", "请先登录 AI 平台" },
            { "Inject_InputNotFound", "无法找到输入框，页面可能已更新" },
            { "Inject_Failed", "注入失败" },

            // Selection Toolbar
            { "Settings_General_SelectionToolbar", "划词工具条设置" },
            { "Settings_General_EnableSelectionToolbar", "启用划词弹出工具条" },
            { "Settings_General_SelectionToolbarTip", "在任意应用中选中文字后，自动弹出操作工具条，快速调用 AI 操作" },
            { "Settings_General_EnableClipboardEnhancementToolbar", "使用剪贴板增强弹出工具条" },
            { "Settings_General_ClipboardEnhancementToolbarTip", "开启后，若 UI Automation 无法获取选中文本，将尝试通过剪贴板 (Ctrl+C) 获取文本" },
            { "Settings_Selection_AppScopeTitle", "应用范围设置" },
            { "Settings_Selection_AppScopeAll", "全部应用（默认）" },
            { "Settings_Selection_AppScopeInclude", "指定应用" },
            { "Settings_Selection_AppScopeExclude", "排除应用" },
            { "Settings_Selection_AppScopeAppsTip", "请输入应用程序进程名称，多个应用请换行或用逗号分隔（例如：notepad.exe, chrome.exe 或 devenv）" },
            { "SelectionToolbar_Sending", "正在发送到 {0}..." },
            { "SelectionToolbar_More", "更多 ▾" },

            { "Inject_Exception", "注入异常: {0}" }
        };

        private static readonly Dictionary<string, string> _enDict = new Dictionary<string, string>
        {
            // Common
            { "AppName", "AIHelper" },
            { "OK", "OK" },
            { "Cancel", "Cancel" },
            { "Save", "Save" },
            { "Add", "Add" },
            { "Edit", "Edit" },
            { "Delete", "Delete" },
            { "Notice", "Notice" },
            { "Error", "Error" },
            { "None", "None" },

            // MainWindow
            { "Main_Settings", "⚙ Settings" },
            { "Main_Status_Ready", "Ready" },
            { "Main_Status_WaitingBrowser", "Waiting for browser component..." },
            { "Main_Status_BrowserInitFailed", "Browser initialization failed: {0}" },
            { "Main_Status_NavigatedTo", "Navigated to {0}" },
            { "Main_Status_WebView2InitFailed", "WebView2 initialization failed: {0}" },
            { "Main_Status_Executing", "Executing: {0}..." },
            { "Main_Status_Success", "Success: {0}" },
            { "Main_Status_Failed", "Failed: {0}" },
            { "Main_Status_Submitting", "Submitting..." },
            { "Main_Status_Injecting", "Injecting..." },
            { "Main_Status_SubmitSuccess", "Submission successful" },
            { "Main_Status_InjectSuccess", "Injection successful" },
            { "Main_Status_OpFailed", "Operation failed: {0}" },
            { "Main_Status_NavigatingTo", "Navigating to {0}..." },
            { "Main_Status_PageLoadSuccess", "Page loaded successfully" },
            { "Main_Status_PageLoadFailed", "Page load failed: {0}" },

            // ActionPanelControl
            { "ActionPanel_ContentHeader", "📋 Content" },
            { "ActionPanel_AvailableActions", "Available Actions:" },

            // SettingsWindow
            { "Settings_Title", "Settings" },
            { "Settings_Tab_General", "General Settings" },
            { "Settings_Tab_SelectionToolbar", "Selection Settings" },
            { "Settings_Tab_Platforms", "Platforms" },
            { "Settings_Tab_Actions", "Actions" },
            { "Settings_Tab_Hotkeys", "Hotkeys" },
            { "Settings_Tab_About", "About & Updates" },

            { "Settings_General_Options", "General Options" },
            { "Settings_General_Language", "Language:" },
            { "Settings_General_Lang_ZH", "中文 (Chinese)" },
            { "Settings_General_Lang_EN", "English" },
            { "Settings_General_AutoStart", "Auto-start AIHelper on boot" },
            { "Settings_General_AutoStartTip", "When checked, AIHelper will automatically run in background on Windows startup" },
            { "Settings_General_AutoSubmit", "Auto-submit prompt" },
            { "Settings_General_AutoSubmitTip", "When checked, automatically clicks send after injecting prompt; when unchecked, creates new chat and injects prompt without sending" },
            { "Settings_General_ProxySettings", "Network Proxy Settings" },
            { "Settings_General_ProxyServer", "Proxy Server:" },
            { "Settings_General_ProxyTip", "Example: http://127.0.0.1:7890 or socks5://127.0.0.1:1080. Leave empty to use Windows system proxy" },

            { "Settings_Platform_Name", "Name" },
            { "Settings_Platform_Url", "URL" },
            { "Settings_Platform_Active", "Active" },
            { "Settings_Platform_NewPlatform", "New Platform" },
            { "Settings_Platform_EmptyWarn", "Platform list cannot be empty." },
            { "Settings_Platform_SelectEditWarn", "Please select a platform to edit." },
            { "Settings_ProxyChangedNotice", "Proxy settings changed. Will take full effect after restarting." },

            { "Settings_Action_Name", "Name" },
            { "Settings_Action_Prompt", "Prompt" },
            { "Settings_Action_SortOrder", "Sort Order" },
            { "Settings_Action_Hotkey", "Hotkey:" },
            { "Settings_Action_ApplyEdit", "Apply Changes" },
            { "Settings_Action_NewAction", "New Action" },
            { "Settings_Action_SelectEditWarn", "Please select an action to edit." },
            { "MoveUp", "Move Up" },
            { "MoveDown", "Move Down" },

            { "Settings_Hotkey_PanelKey", "Panel Hotkey:" },
            { "Settings_Hotkey_Tip", "Click the text box and press key combination" },

            { "Settings_About_Title", "Project & Update Information" },
            { "Settings_About_ProjectUrl", "Project URL" },
            { "Settings_About_OpenProject", "Visit Project Page" },
            { "Settings_About_UpdateUrl", "Release URL" },
            { "Settings_About_OpenUpdate", "View Releases" },
            { "Settings_About_Tip", "Tip: Click the link or button to open the page in your default browser." },
            { "Settings_About_OpenUrlError", "Cannot open link: {0}" },

            // PlatformEditWindow
            { "PlatformEdit_Title_Edit", "Edit Platform" },
            { "PlatformEdit_Title_Add", "Add Platform" },
            { "PlatformEdit_Name", "Name:" },
            { "PlatformEdit_Url", "URL:" },
            { "PlatformEdit_NewChat", "New Chat Selector:" },
            { "PlatformEdit_Input", "Input Selector:" },
            { "PlatformEdit_Submit", "Submit Selector:" },
            { "PlatformEdit_Pick", "🎯 Pick" },
            { "PlatformEdit_NewChatTT", "CSS selector for locating new chat button. Leave empty for auto-detection." },
            { "PlatformEdit_NewChatBtnTT", "Open platform page to select new chat button element" },
            { "PlatformEdit_InputTT", "CSS selector for locating input field. Leave empty for auto-detection." },
            { "PlatformEdit_InputBtnTT", "Open platform page to select input field element" },
            { "PlatformEdit_SubmitTT", "CSS selector for locating submit button. Leave empty for auto-detection." },
            { "PlatformEdit_SubmitBtnTT", "Open platform page to select submit button element" },
            { "PlatformEdit_Tip", "Tip: Leave empty to use auto-detection logic. Click 'Pick' to visually select element on platform page." },
            { "PlatformEdit_InvalidUrlWarn", "Please enter a valid platform URL first." },
            { "PlatformEdit_EmptyNameWarn", "Name cannot be empty." },
            { "PlatformEdit_EmptyUrlWarn", "URL cannot be empty." },

            // ElementPickerWindow
            { "ElementPicker_Title", "Element Picker" },
            { "ElementPicker_Instruction", "Please click target element on page" },
            { "ElementPicker_Retry", "Re-pick" },
            { "ElementPicker_Status_Loading", "Loading page..." },
            { "ElementPicker_Status_InitFailed", "WebView2 initialization failed: {0}" },
            { "ElementPicker_Status_LoadSuccess", "Page loaded, injecting picker script..." },
            { "ElementPicker_Status_LoadFailed", "Page load failed: {0}" },
            { "ElementPicker_Status_ScriptMissing", "Picker script missing" },
            { "ElementPicker_Status_Active", "✅ Picker mode active — Hover to inspect, click to select, press Esc to cancel" },
            { "ElementPicker_Status_InjectFailed", "Script injection failed: {0}" },
            { "ElementPicker_Status_Selected", "Selected: {0} → {1}" },
            { "ElementPicker_Status_Reinjecting", "Re-injecting picker script..." },

            // Tray Menu
            { "Tray_Show", "Show Main Window" },
            { "Tray_Settings", "Open Settings" },
            { "Tray_SelectionToolbar", "Enable Selection Search" },
            { "Tray_Exit", "Exit" },

            // Injection Messages
            { "Inject_WebviewNotReady", "Browser component not ready" },
            { "Inject_ScriptNotFound", "Injection script missing" },
            { "Inject_NoResult", "Injection failed, no result returned" },
            { "Inject_FormatError", "Injection failed, invalid result format" },
            { "Inject_SendSuccess", "Sent successfully" },
            { "Inject_InjectSuccess", "Injected successfully" },
            { "Inject_NotLoggedIn", "Please log in to the AI platform first" },
            { "Inject_InputNotFound", "Input box not found, page structure may have changed" },
            { "Inject_Failed", "Injection failed" },

            // Selection Toolbar
            { "Settings_General_SelectionToolbar", "Selection Toolbar" },
            { "Settings_General_EnableSelectionToolbar", "Enable Selection Toolbar" },
            { "Settings_General_SelectionToolbarTip", "After selecting text in any app, a toolbar pops up for quick AI actions" },
            { "Settings_General_EnableClipboardEnhancementToolbar", "Use Clipboard Enhancement for Toolbar" },
            { "Settings_General_ClipboardEnhancementToolbarTip", "When enabled, if UI Automation fails to get selected text, try getting text via clipboard (Ctrl+C)" },
            { "Settings_Selection_AppScopeTitle", "Application Scope Settings" },
            { "Settings_Selection_AppScopeAll", "All Applications (Default)" },
            { "Settings_Selection_AppScopeInclude", "Specified Applications" },
            { "Settings_Selection_AppScopeExclude", "Excluded Applications" },
            { "Settings_Selection_AppScopeAppsTip", "Enter application process names, separated by newline or comma (e.g. notepad.exe, chrome.exe, or devenv)" },
            { "SelectionToolbar_Sending", "Sending to {0}..." },
            { "SelectionToolbar_More", "More ▾" },

            { "Inject_Exception", "Injection exception: {0}" }
        };
    }
}
