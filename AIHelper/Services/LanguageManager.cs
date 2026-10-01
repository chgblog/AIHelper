// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
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
            { "Main_Minimize", "最小化" },
            { "Main_Maximize", "最大化" },
            { "Main_Restore", "向下还原" },
            { "Main_Close", "关闭" },
            { "Main_Settings", "⚙ 设置" },
            { "Main_UpdateAvailable", "有更新" },
            { "Main_UpdateAvailable_Tip", "发现新版本 {0}（当前版本 {1}），点击打开下载页面" },
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
            { "Main_Status_Pasting", "正在粘贴..." },
            { "Main_Status_PasteSuccess", "粘贴成功" },
            { "Main_Status_WaitingUpload", "正在等待上传完成..." },
            { "Main_Status_UploadReady", "上传完成" },
            { "Main_Status_AttachmentMaybeUnsupported", "当前平台可能不支持此类文件/附件，或上传时间过长" },
            { "Main_Status_SubmitSuccess", "提交成功" },
            { "Main_Status_InjectSuccess", "注入成功" },
            { "Main_Status_OpFailed", "操作失败: {0}" },
            { "Main_Status_NavigatingTo", "正在导航到 {0}..." },
            { "Main_Status_PageLoadSuccess", "页面加载完成" },
            { "Main_Status_PageLoadFailed", "页面加载失败: {0}" },
            { "Main_Status_WaitingPage", "正在等待页面就绪..." },
            { "Main_Status_NewChat", "正在新建会话..." },
            { "Main_QuickActions_More", "更多 ▾" },
            { "Main_QuickActions_AllTitle", "更多操作" },
            { "Main_NewChatOption", "开启新会话" },
            { "Main_NewChatOption_Tip", "勾选后点击底部操作会先新建会话再注入提示词；未勾选时直接注入到当前会话" },
            { "Main_SwitchToBrowserMode", "切换到浏览器模式" },
            { "Main_SwitchToAiMode", "切换回 AI 模式" },
            { "Main_BrowserUrl_Placeholder", "输入网址访问 (例如: https://...)" },
            { "Main_BrowserGo", "访问" },
            { "Main_BrowserRefresh", "刷新" },
            { "Main_SimulateHuman", "模拟真人访问" },
            { "Main_SimulateHuman_Tip", "开启后将按照设置的间隔时长和移动间距自动随机向下滚动浏览网页" },
            { "Main_Status_SimulateActive", "正在模拟真人浏览 (当前间隔: {0}秒, 滚动: {1}px)" },
            { "Main_Status_SimulateStopped", "模拟真人浏览已停止" },
            { "Main_Status_BrowserMode", "已进入浏览器模式" },

            // ActionPanelControl
            { "ActionPanel_ContentHeader", "📋 内容" },
            { "ActionPanel_Close", "关闭快捷面板" },
            { "ActionPanel_AvailableActions", "可用操作:" },
            { "ActionPanel_Image", "图片" },
            { "ActionPanel_File", "文件" },
            { "ActionPanel_ClearAttachment", "清除附件并切换为文本输入" },

            // SettingsWindow
            { "Settings_Title", "设置" },
            { "Settings_Tab_General", "常规设置" },
            { "Settings_Tab_SelectionToolbar", "划词设置" },
            { "Settings_Tab_Platforms", "平台管理" },
            { "Settings_Tab_Actions", "操作管理" },
            { "Settings_Tab_SimulateVisit", "模拟访问" },
            { "Settings_Tab_Hotkeys", "其他设置" },
            { "Settings_Tab_About", "更新" },

            { "Settings_Simulate_Title", "模仿真人浏览网页设置" },
            { "Settings_Simulate_Desc", "在浏览器模式下开启「模拟真人访问」后，系统将按照以下设定的时间间隔和滚动幅度，平滑向下滚动页面以模拟人工浏览。" },
            { "Settings_Simulate_IntervalSection", "滚动间隔时长设置" },
            { "Settings_Simulate_MinInterval", "最小时长 (秒):" },
            { "Settings_Simulate_MaxInterval", "最大时长 (秒):" },
            { "Settings_Simulate_IntervalTip", "每次滚动将在最小时长与最大时长之间随机选择一个间隔时长（单位：秒，支持小数，例如：0.5 - 3.0）。" },
            { "Settings_Simulate_DistanceSection", "滚动移动间距设置" },
            { "Settings_Simulate_MinDistance", "最小长度 (像素):" },
            { "Settings_Simulate_MaxDistance", "最大长度 (像素):" },
            { "Settings_Simulate_DistanceTip", "每次向下滚动将在最小长度与最大长度之间随机选择一个距离平滑滚动（单位：像素，例如：100 - 300）。" },
            { "Settings_Simulate_ValidateError", "模拟访问设置有误：最小时长必须大于 0 且小于等于最大时长；最小间距必须大于等于 1 且小于等于最大间距。" },
            { "Settings_Simulate_ProxySection", "网络代理设置" },
            { "Settings_Simulate_UseProxy", "启用代理" },
            { "Settings_Simulate_UseProxyTip", "默认与系统代理一致；选中启用代理后将使用常规设置中配置的代理服务器访问网页。" },

            // Auto Visit
            { "Settings_AutoVisit_Section", "自动访问管理" },
            { "Settings_AutoVisit_SectionTip", "在浏览器模式下打开指定网址并开启模拟真人访问时，系统将自动根据规则提取当前屏幕链接、深入浏览子页面、翻屏翻页并循环访问。" },
            { "Settings_AutoVisit_ManageBtn", "自动访问管理..." },
            { "AutoVisit_Manager_Title", "自动访问管理" },
            { "AutoVisit_Col_Url", "自动访问网址" },
            { "AutoVisit_Col_Regex", "正则匹配规则" },
            { "AutoVisit_Col_Limit", "访问链接数" },
            { "AutoVisit_Col_NextPage", "下一页定位" },
            { "AutoVisit_Col_LinkDelay", "下一链接延迟 (秒)" },
            { "AutoVisit_Col_Refresh", "停止后刷新间隔 (分)" },
            { "AutoVisit_Col_Enabled", "启用" },
            { "AutoVisit_Manager_ClearHistory", "清空24小时访问历史" },
            { "AutoVisit_Manager_ClearHistoryConfirm", "确定要清空过去 24 小时内的所有已访问链接记录吗？" },
            { "AutoVisit_Manager_ClearHistoryDone", "已清空访问历史记录。" },
            { "AutoVisit_Manager_DeleteConfirm", "确定要删除此自动访问网址配置吗？" },
            { "AutoVisit_Manager_SelectEditWarn", "请先选择要编辑的自动访问配置。" },
            { "AutoVisit_Manager_SelectDeleteWarn", "请先选择要删除的自动访问配置。" },

            { "AutoVisit_Edit_Title_Add", "添加自动访问网址" },
            { "AutoVisit_Edit_Title_Edit", "编辑自动访问网址" },
            { "AutoVisit_Edit_Url", "自动访问网址:" },
            { "AutoVisit_Edit_UrlTT", "需要执行自动访问的主页面网址 (例如: https://news.ycombinator.com)" },
            { "AutoVisit_Edit_Regex", "正则匹配规则:" },
            { "AutoVisit_Edit_RegexTT", "自动访问链接正则匹配规则 (仅访问符合该正则的链接，例如: .*item\\?id=\\d+)" },
            { "AutoVisit_Edit_Limit", "访问链接数:" },
            { "AutoVisit_Edit_LimitTT", "达到设置的访问链接数则停止当前轮次并等待刷新 (默认 50)" },
            { "AutoVisit_Edit_NextPage", "下一页定位:" },
            { "AutoVisit_Edit_NextPageTT", "下一页按钮的 CSS 选择器，滚动到底部后点击翻页；未设置则到底部停止" },
            { "AutoVisit_Edit_Pick", "定位" },
            { "AutoVisit_Edit_PickTT", "在网页中可视化拾取下一页元素" },
            { "AutoVisit_Edit_LinkDelay", "下一链接延迟:" },
            { "AutoVisit_Edit_LinkDelayTT", "每个链接访问完成（滚动到底部）后，随机延迟指定秒数再访问下一个链接 (默认 3-8 秒)" },
            { "AutoVisit_Edit_LinkDelayUnit", "秒 (随机区间)" },
            { "AutoVisit_Edit_RefreshInterval", "停止后刷新间隔:" },
            { "AutoVisit_Edit_RefreshUnit", "分钟 (随机区间)" },
            { "AutoVisit_Edit_RefreshTip", "达到访问链接数或滚动到底部后，将在此区间内随机等待指定分钟数，然后自动刷新重新从头访问 (单位: 分钟，默认 30-60)" },
            { "AutoVisit_Edit_ExcludedUrls", "排除访问链接:" },
            { "AutoVisit_Edit_ExcludedUrlsTT", "设置排除访问的链接地址，支持设置多个（每行一个，也支持逗号或分号分隔）。匹配这些地址的链接将被跳过不予访问。" },
            { "AutoVisit_Edit_Enabled", "启用此自动访问规则" },
            { "AutoVisit_Edit_InvalidUrl", "请输入有效的自动访问网址 (必须以 http:// 或 https:// 开头)。" },
            { "AutoVisit_Edit_InvalidRegex", "链接正则表达式格式无效: {0}" },
            { "AutoVisit_Edit_InvalidLimit", "自动访问链接数必须是大于 0 的整数。" },
            { "AutoVisit_Edit_InvalidLinkDelay", "下一链接延迟时长有误：起始秒数不能小于 0，且截止秒数必须大于等于起始秒数。" },
            { "AutoVisit_Edit_InvalidInterval", "停止后刷新间隔有误：最小间隔必须大于 0，且小于等于最大间隔。" },

            { "AutoVisit_Status_Matching", "自动访问: 正在获取当前屏幕匹配链接 (本轮已访问: {0}/{1})..." },
            { "AutoVisit_Status_VisitingChild", "自动访问 [{0}/{1}]: 正在访问 {2}" },
            { "AutoVisit_Status_ScrollingChild", "自动访问: 正在模拟真人滚动浏览子页面..." },
            { "AutoVisit_Status_ReturnMain", "自动访问: 已达子页面底部，返回主页面..." },
            { "AutoVisit_Status_WaitingNextLink", "自动访问: 当前链接访问完成，等待 {0} 秒后访问下一个链接..." },
            { "AutoVisit_Status_ScrollNextScreen", "自动访问: 当前屏幕链接已访问完，向下滚动到下一屏..." },
            { "AutoVisit_Status_NextPage", "自动访问: 主页面已达底部，正在翻到下一页..." },
            { "AutoVisit_Status_WaitingRefresh", "自动访问: 本轮完成 (共访问 {0} 个链接)，将在 {1} 分钟后 ({2}) 刷新重新访问..." },
            { "AutoVisit_Status_WaitingCountdown", "自动访问等待刷新重访中，剩余 {0} 分 {1} 秒..." },
            { "AutoVisit_Status_Stopped", "自动访问已停止" },


            { "Settings_General_Options", "常规选项" },
            { "Settings_General_Language", "界面语言:" },
            { "Settings_General_Lang_ZH", "中文 (Chinese)" },
            { "Settings_General_Lang_EN", "English" },
            { "Settings_General_ShowMainWindow", "程序运行后显示主窗口" },
            { "Settings_General_ShowMainWindowTip", "勾选后启动程序自动显示主窗口；未勾选则隐藏在系统托盘运行" },
            { "Settings_General_AutoStart", "开机自动启动 AIHelper" },
            { "Settings_General_AutoStartTip", "勾选后，Windows 登录时将自动在后台运行 AIHelper" },
            { "Settings_General_AutoSubmit", "自动提交提示词" },
            { "Settings_General_AutoSubmitTip", "勾选后注入提示词后自动点击发送；未勾选时仅点击新会话并注入提示词，不点击发送按钮" },
            { "Settings_General_EnableContextMenu", "启用文件右键菜单" },
            { "Settings_General_EnableContextMenuTip", "在图片、Office文档、PDF、文本、代码等文件的右键菜单中添加快捷入口，点击即可唤起 AI 工具条快速处理" },
            { "ContextMenu_Title", "使用 AIHelper 处理" },
            { "ContextMenu_More", "更多..." },
            { "ContextMenu_AuthFailed", "请求管理员授权失败或已取消，无法更改文件右键菜单设置。" },
            { "Settings_General_ProxySettings", "网络代理设置" },
            { "Settings_General_ProxyServer", "代理服务器:" },
            { "Settings_General_ProxyTip", "例如: http://127.0.0.1:7890 或 socks5://127.0.0.1:1080。留空则自动使用 Windows 系统代理" },

            { "Settings_Platform_Name", "名称" },
            { "Settings_Platform_Url", "URL" },
            { "Settings_Platform_Active", "激活" },
            { "Settings_Platform_Proxy", "代理" },
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
            { "Settings_Hotkey_MainWindowKey", "打开主界面快捷键:" },
            { "Settings_Hotkey_Tip", "请点击输入框并按下快捷键组合" },
            { "Hotkey_Conflict_Panel", "⚠️ 该快捷键与【面板唤醒快捷键】冲突" },
            { "Hotkey_Conflict_MainWindow", "⚠️ 该快捷键与【打开主界面快捷键】冲突" },
            { "Hotkey_Conflict_Action", "⚠️ 该快捷键与动作【{0}】冲突" },
            { "Hotkey_Conflict_System", "⚠️ 该快捷键已被系统或其他软件占用" },
            { "Hotkey_Conflict_SaveError", "快捷键设置存在冲突，请修改后再保存：\n\n{0}" },
            { "Main_Status_HotkeyFailed", "快捷键注册失败: {0}（可能已被占用）" },

            { "Settings_About_Title", "更新信息" },
            { "Settings_About_ProjectUrl", "项目地址" },
            { "Settings_About_OpenProject", "进入项目主页" },
            { "Settings_About_UpdateUrl", "更新地址" },
            { "Settings_About_OpenUpdate", "进入 Release 列表" },
            { "Settings_About_Tip", "提示：点击地址链接或按钮即可在默认浏览器中打开对应页面。" },
            { "Settings_About_OpenUrlError", "无法打开链接: {0}" },
            { "Settings_About_AutoCheckUpdate", "自动检测新版本" },
            { "Settings_About_AutoCheckUpdateTip", "启动程序 1 分钟后在后台检测 GitHub Release 是否有新版本" },

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
            { "PlatformEdit_UseProxy", "使用代理:" },
            { "PlatformEdit_UseProxyTT", "勾选后访问该平台时使用常规设置中配置的代理服务器" },

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
            { "ActionEdit_Platform", "使用平台:" },
            { "ActionEdit_Platform_Default", "默认（激活平台）" },
            { "Settings_Action_Platform", "平台" },
            { "Settings_Action_Platform_Default", "默认" },

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
            { "Inject_PageNotReady", "页面未就绪（加载超时），请重试" },
            { "Inject_TextLost", "注入的内容被页面清除，请重试" },
            { "Inject_Failed", "注入失败" },

            // Selection Toolbar
            { "Settings_General_SelectionToolbar", "划词工具条设置" },
            { "Settings_General_EnableSelectionToolbar", "启用划词弹出工具条" },
            { "Settings_General_SelectionToolbarTip", "在任意应用中选中文字后，自动弹出操作工具条，快速调用 AI 操作" },
            { "Settings_General_SelectionToolbarCopy", "工具条是否增加复制:" },
            { "Settings_General_SelectionToolbarCopy_Disabled", "不开启（默认）" },
            { "Settings_General_SelectionToolbarCopy_First", "开启且在第一个" },
            { "Settings_General_SelectionToolbarCopy_Last", "开启且在最后一个" },
            { "SelectionToolbar_Copy", "复制" },
            { "Settings_General_SelectionToolbarAutoHide", "划词工具条自动消失时间 (秒):" },
            { "Settings_General_SelectionToolbarAutoHideTip", "划词工具条弹出后若无操作，将在 {0} 秒后自动消失" },
            { "Settings_Selection_AppScopeTitle", "应用范围设置" },
            { "Settings_Selection_AppScopeAll", "全部应用（默认）" },
            { "Settings_Selection_AppScopeInclude", "指定应用" },
            { "Settings_Selection_AppScopeExclude", "排除应用" },
            { "Settings_Selection_AppScopeAppsTip", "请输入应用程序进程名称，多个应用请换行或用逗号分隔（例如：notepad.exe, chrome.exe 或 devenv）" },
            { "Settings_Selection_SelectApps", "选择应用..." },
            { "AppSelection_Title", "选择应用" },
            { "AppSelection_SearchPlaceholder", "搜索应用名称或进程名..." },
            { "AppSelection_OnlyWindowApps", "仅显示带窗口的应用 (过滤系统与后台服务)" },
            { "AppSelection_Refresh", "刷新" },
            { "AppSelection_SelectAll", "全选" },
            { "AppSelection_InvertSelect", "反选" },
            { "AppSelection_ClearSelect", "清空" },
            { "AppSelection_SelectedCount", "已选择 {0} 个应用" },
            { "AppSelection_Append", "追加到列表" },
            { "AppSelection_Replace", "覆盖当前列表" },
            { "AppSelection_NoAppsFound", "未找到匹配的运行中应用" },
            { "SelectionToolbar_Sending", "正在发送到 {0}..." },
            { "SelectionToolbar_More", "更多 ▾" },
            { "SelectionToolbar_Back", "返回" },

            { "Inject_Exception", "注入异常: {0}" },

            // Other Settings - Config Management
            { "Settings_Other_ConfigManagement", "配置管理" },
            { "Settings_Other_ConfigManagementTip", "备份当前配置到文件，或从备份文件恢复配置。" },
            { "Settings_Other_BackupConfig", "📦 备份配置" },
            { "Settings_Other_RestoreConfig", "📂 恢复配置" },
            { "Settings_Other_OpenConfigDir", "📁 打开配置目录" },
            { "Settings_Other_NoConfigFile", "未找到配置文件" },
            { "Settings_Other_BackupSuccess", "配置备份成功！" },
            { "Settings_Other_BackupFailed", "配置备份失败: {0}" },
            { "Settings_Other_RestoreConfirm", "确定要恢复配置吗？当前配置将被覆盖。" },
            { "Settings_Other_RestoreSuccess", "配置恢复成功！" },
            { "Settings_Other_RestoreFailed", "配置恢复失败: {0}" },
            { "Settings_Other_OpenDirFailed", "打开配置目录失败: {0}" },
            // Batch Image
            { "Settings_Tab_BatchImage", "批量生图" },
            { "Settings_Batch_Title", "批量生图设置" },
            { "Settings_Batch_Desc", "通过「批量生图」操作选择 CSV 文件（列：提示词, 文件名, 平台），逐行新建对话、注入 AI 平台生成图片，并按文件名保存。" },
            { "Settings_Batch_SavePathSection", "保存路径" },
            { "Settings_Batch_Browse", "浏览..." },
            { "Settings_Batch_Open", "打开" },
            { "Settings_Batch_SavePathTip", "留空则使用默认路径：{0}\n每次批量会在此路径下按时间新建子目录（如 20261001_143000）；同一提示词生成多张时，第一张存子目录，其余存其中的「备选」目录。" },
            { "Settings_Batch_BrowseTitle", "选择批量生图保存路径" },
            { "Settings_Batch_RunSection", "运行参数" },
            { "Settings_Batch_Timeout", "单张超时（秒）：" },
            { "Settings_Batch_Retry", "失败重试次数：" },
            { "Settings_Batch_Interval", "每张间隔（秒）：" },
            { "Settings_Batch_RunTip", "超时 30~1800 秒，重试 0~5 次，间隔 0~600 秒且最小值不大于最大值。两张之间在间隔范围内随机等待，降低触发平台风控的概率。" },
            { "Settings_Batch_ValidateError", "批量生图参数无效：超时 30~1800 秒，重试 0~5 次，间隔 0~600 秒且最小值不能大于最大值，保存路径须为完整路径（或留空）。" },
            { "Settings_Batch_OpenDirFailed", "打开目录失败: {0}" },
            { "Settings_Batch_PresetSection", "页面预设（录制模型、比例等选项）" },
            { "Settings_Batch_PresetDesc", "有些网站生图前要先选模型、比例、张数。在批量生图窗口顶部的「页面预设」区选好平台，点「● 录制」，到主窗口页面上像平时一样操作（不要输入提示词或发送），再点「✔ 完成录制」，之后每行生图前都会自动重放。同一区域还可以勾选「点击图片打开大图后再保存」。录制不需要 CSV 文件。" },
            { "Settings_Batch_PresetOpen", "保存设置并打开批量生图窗口" },
            { "Settings_Action_CannotDeleteBatch", "「批量生图」是系统内置操作，不能删除。" },
            { "ActionEdit_BatchPromptHint", "批量生图：{content} 会被替换为 CSV 中的提示词；不含 {content} 时 CSV 提示词接在后面；留空则直接发送 CSV 中的提示词。\n模型、比例等页面选项的录制在「批量生图」窗口顶部的「页面预设」区（设置 → 批量生图页也有入口）。" },
            { "Batch_Title", "批量生图" },
            { "Batch_SelectCsvTitle", "选择批量生图 CSV 文件" },
            { "Batch_AllFiles", "所有文件" },
            { "Batch_CsvFile", "CSV 文件：" },
            { "Batch_PickCsv", "选择..." },
            { "Batch_DefaultPlatform", "默认平台（平台列为空时）：{0}" },
            { "Batch_OutputDir", "保存目录：{0}" },
            { "Batch_OutputDirPending", "保存目录：开始后在 {0} 下按时间新建" },
            { "Batch_Summary", "共 {0} 行，有效 {1} 行，无效 {2} 行" },
            { "Batch_Progress", "进度 {0}/{1}　成功 {2}　失败 {3}　跳过 {4}" },
            { "Batch_Tip", "运行期间请保持主窗口可见，不要手动操作 AI 页面；暂停在当前这张完成后生效。" },
            { "Batch_Col_Index", "行号" },
            { "Batch_Col_Prompt", "提示词" },
            { "Batch_Col_FileName", "文件名" },
            { "Batch_Col_Platform", "平台" },
            { "Batch_Col_Status", "状态" },
            { "Batch_Col_Result", "结果" },
            { "Batch_Start", "▶ 开始" },
            { "Batch_Pause", "⏸ 暂停" },
            { "Batch_Resume", "▶ 继续" },
            { "Batch_Stop", "⏹ 停止" },
            { "Batch_RetryFailed", "重试失败项" },
            { "Batch_OpenOutput", "打开输出目录" },
            { "Batch_Settings", "参数设置" },
            { "Batch_Close", "关闭" },
            { "Batch_ItemStatus_Pending", "等待" },
            { "Batch_ItemStatus_Running", "生成中" },
            { "Batch_ItemStatus_Success", "成功" },
            { "Batch_ItemStatus_Failed", "失败" },
            { "Batch_ItemStatus_Skipped", "跳过" },
            { "Batch_ItemStatus_Invalid", "无效" },
            { "Batch_Msg_EmptyPrompt", "提示词为空" },
            { "Batch_Msg_PlatformNotFound", "平台不存在：{0}" },
            { "Batch_Msg_NoPlatform", "没有可用的平台" },
            { "Batch_Msg_NoImage", "回复中未发现图片：{0}" },
            { "Batch_Msg_Timeout", "等待出图超时" },
            { "Batch_Msg_ExtractFailed", "图片提取失败" },
            { "Batch_Msg_SaveFailed", "保存失败：{0}" },
            { "Batch_Msg_SubmitFailed", "提交失败：{0}" },
            { "Batch_Msg_PlatformNotReady", "平台加载失败，已跳过：{0}" },
            { "Batch_Msg_SkippedNotLoggedIn", "平台未登录，已跳过" },
            { "Batch_Msg_SkippedConsecutive", "该平台连续失败 {0} 次，已跳过" },
            { "Batch_Msg_Stopped", "已停止" },
            { "Batch_Msg_PartialExtract", "（{0} 张提取失败）" },
            { "Batch_Status_Busy", "批量生图进行中，请先停止或等待完成" },
            { "Batch_Status_Item", "批量生图 {0}/{1}：{2}" },
            { "Batch_Status_Ready", "就绪" },
            { "Batch_Status_Pausing", "将在当前这张完成后暂停" },
            { "Batch_Status_Paused", "已暂停，点击继续恢复" },
            { "Batch_Status_Resumed", "已继续" },
            { "Batch_Status_Stopping", "正在停止..." },
            { "Batch_Status_Stopped", "批量生图已停止" },
            { "Batch_Status_Finished", "批量生图完成：成功 {0}，失败 {1}，跳过 {2}" },
            { "Batch_Step_NewChat", "新建对话" },
            { "Batch_Step_Injecting", "注入提示词" },
            { "Batch_Step_Submitting", "提交" },
            { "Batch_Step_Waiting", "等待出图（{0} 秒）" },
            { "Batch_Step_Extracting", "提取图片" },
            { "Batch_Step_Retry", "第 {0} 次重试" },
            { "Batch_Step_Interval", "等待 {0} 秒后继续" },
            { "Batch_Csv_ReadFailed", "读取 CSV 失败：{0}" },
            { "Batch_Csv_NoPromptColumn", "表头中未找到提示词列（提示词 / prompt）" },
            { "Batch_Csv_Empty", "CSV 中没有数据行" },
            { "Batch_NothingToRun", "没有待执行的行" },
            { "Batch_OutputDirFailed", "创建输出目录失败：{0}" },
            { "Batch_ConfirmStopOnClose", "批量生图正在进行，确定停止并关闭吗？" },
            { "Batch_AlternatesFolder", "备选" },
            { "Batch_Report_Header", "行号,提示词,文件名,平台,状态,保存文件,说明,耗时(秒)" },
            { "Batch_Step_Preset", "应用页面预设" },
            { "Batch_Step_OpenLarge", "打开大图 {0}/{1}" },
            { "Batch_Msg_PresetFailed", "页面预设失败：{0}" },
            { "Batch_Preset_Title", "页面预设" },
            { "Batch_Preset_Record", "● 录制" },
            { "Batch_Preset_Finish", "✔ 完成录制" },
            { "Batch_Preset_Cancel", "取消" },
            { "Batch_Preset_Test", "试运行" },
            { "Batch_Preset_Clear", "清除" },
            { "Batch_Preset_RemoveStep", "删除这一步" },
            { "Batch_Preset_OpenLarge", "点击图片打开大图后再保存" },
            { "Batch_Preset_OpenLargeTip", "对话里只显示缩略图、点开后才加载原图的网站请勾选：保存前会逐张点击图片，保存查看器里的大图，再关闭查看器。打不开大图时仍保存缩略图。" },
            { "Batch_Preset_None", "未设置：直接使用页面当前的模型和选项。需要先选模型或生图选项的网站，点「录制」后在主窗口的页面上选好，每次新建对话后会自动重放。" },
            { "Batch_Preset_Steps", "每次新建对话后自动重放以下 {0} 步（已处于录制时状态的按钮不会重复点击）：" },
            { "Batch_Preset_Starting", "正在打开 {0} 并新建对话，准备录制..." },
            { "Batch_Preset_RecordingHint", "录制中，已记录 {0} 步。请在主窗口的页面上依次选择模型、比例等生图选项（不要输入提示词或发送），完成后点「完成录制」。录制结果会替换原有预设。" },
            { "Batch_Preset_RecordingStatus", "正在录制页面预设" },
            { "Batch_Preset_Badge", "● AIHelper 正在录制页面预设" },
            { "Batch_Preset_BarStarting", "正在打开「{0}」并新建对话，请稍候……" },
            { "Batch_Preset_BarRecording", "● 正在录制「{0}」：请在下方页面依次选择模型、比例等生图选项（不要输入提示词或发送），已记录 {1} 步。选好后点右侧「完成录制」。" },
            { "Batch_Preset_BarBack", "返回批量生图窗口" },
            { "Batch_Preset_RecordFailed", "无法开始录制：{0}" },
            { "Batch_Preset_Saved", "已保存「{0}」的页面预设（{1} 步）" },
            { "Batch_Preset_NothingRecorded", "没有录制到任何操作，原有预设保持不变" },
            { "Batch_Preset_RecordCancelled", "已取消录制" },
            { "Batch_Preset_ConfirmClear", "确定清除「{0}」的页面预设吗？" },
            { "Batch_Preset_Testing", "正在试运行页面预设..." },
            { "Batch_Preset_TestOk", "试运行完成：执行 {0} 步，跳过 {1} 步（已是目标状态）。请在主窗口确认页面上的选项。" },
            { "Batch_Preset_TestFailed", "试运行失败：{0}" },
            { "Batch_Preset_Busy", "正在录制或试运行页面预设，请先完成" },
            { "Batch_Preset_StepNotFound", "第 {0} 步「{1}」在页面上找不到" },
            { "Batch_Preset_Step_Click", "点击「{0}」" },
            { "Batch_Preset_Step_Select", "「{0}」选择「{1}」" },
            { "Batch_Preset_Step_Check", "勾选「{0}」" },
            { "Batch_Preset_Step_Uncheck", "取消勾选「{0}」" },
            { "Batch_Preset_Step_Input", "「{0}」填写 {1}" }
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
            { "Main_Minimize", "Minimize" },
            { "Main_Maximize", "Maximize" },
            { "Main_Restore", "Restore" },
            { "Main_Close", "Close" },
            { "Main_Settings", "⚙ Settings" },
            { "Main_UpdateAvailable", "Update" },
            { "Main_UpdateAvailable_Tip", "New version {0} is available (current: {1}). Click to open the download page." },
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
            { "Main_Status_Pasting", "Pasting..." },
            { "Main_Status_PasteSuccess", "Paste detected" },
            { "Main_Status_WaitingUpload", "Waiting for upload to finish..." },
            { "Main_Status_UploadReady", "Upload ready" },
            { "Main_Status_AttachmentMaybeUnsupported", "This platform may not support this file type, or the upload is taking too long" },
            { "Main_Status_SubmitSuccess", "Submission successful" },
            { "Main_Status_InjectSuccess", "Injection successful" },
            { "Main_Status_OpFailed", "Operation failed: {0}" },
            { "Main_Status_NavigatingTo", "Navigating to {0}..." },
            { "Main_Status_PageLoadSuccess", "Page loaded successfully" },
            { "Main_Status_PageLoadFailed", "Page load failed: {0}" },
            { "Main_Status_WaitingPage", "Waiting for the page to be ready..." },
            { "Main_Status_NewChat", "Starting a new chat..." },
            { "Main_QuickActions_More", "More ▾" },
            { "Main_QuickActions_AllTitle", "More Actions" },
            { "Main_NewChatOption", "New Chat" },
            { "Main_NewChatOption_Tip", "When checked, clicking bottom actions will create a new chat before injecting prompt; when unchecked, injects directly into current chat" },
            { "Main_SwitchToBrowserMode", "Switch to Browser Mode" },
            { "Main_SwitchToAiMode", "Switch back to AI Mode" },
            { "Main_BrowserUrl_Placeholder", "Enter URL to visit (e.g. https://...)" },
            { "Main_BrowserGo", "Go" },
            { "Main_BrowserRefresh", "Refresh" },
            { "Main_SimulateHuman", "Simulate Human Browsing" },
            { "Main_SimulateHuman_Tip", "When enabled, automatically scrolls down the page at randomized intervals and distances" },
            { "Main_Status_SimulateActive", "Simulating human browsing (Current interval: {0}s, scroll: {1}px)" },
            { "Main_Status_SimulateStopped", "Human browsing simulation stopped" },
            { "Main_Status_BrowserMode", "Switched to Browser Mode" },

            // ActionPanelControl
            { "ActionPanel_ContentHeader", "📋 Content" },
            { "ActionPanel_Close", "Close Panel" },
            { "ActionPanel_AvailableActions", "Available Actions:" },
            { "ActionPanel_Image", "Image" },
            { "ActionPanel_File", "File" },
            { "ActionPanel_ClearAttachment", "Clear attachment and switch to text input" },

            // SettingsWindow
            { "Settings_Title", "Settings" },
            { "Settings_Tab_General", "General Settings" },
            { "Settings_Tab_SelectionToolbar", "Selection Settings" },
            { "Settings_Tab_Platforms", "Platforms" },
            { "Settings_Tab_Actions", "Actions" },
            { "Settings_Tab_SimulateVisit", "Simulation" },
            { "Settings_Tab_Hotkeys", "Other Settings" },
            { "Settings_Tab_About", "Updates" },

            { "Settings_Simulate_Title", "Human Browsing Simulation Settings" },
            { "Settings_Simulate_Desc", "When 'Simulate Human Browsing' is enabled in Browser Mode, the page will automatically scroll down smoothly according to the randomized parameters below." },
            { "Settings_Simulate_IntervalSection", "Scroll Interval Duration" },
            { "Settings_Simulate_MinInterval", "Min Duration (sec):" },
            { "Settings_Simulate_MaxInterval", "Max Duration (sec):" },
            { "Settings_Simulate_IntervalTip", "Each scroll randomly picks an interval between min and max duration (in seconds, decimals allowed, e.g. 0.5 - 3.0)." },
            { "Settings_Simulate_DistanceSection", "Scroll Distance" },
            { "Settings_Simulate_MinDistance", "Min Distance (px):" },
            { "Settings_Simulate_MaxDistance", "Max Distance (px):" },
            { "Settings_Simulate_DistanceTip", "Each scroll randomly picks a distance between min and max to smoothly scroll down (in pixels, e.g. 100 - 300)." },
            { "Settings_Simulate_ValidateError", "Invalid simulation settings: min duration must be > 0 and <= max duration; min distance must be >= 1 and <= max distance." },
            { "Settings_Simulate_ProxySection", "Network Proxy Settings" },
            { "Settings_Simulate_UseProxy", "Enable Proxy" },
            { "Settings_Simulate_UseProxyTip", "Defaults to system proxy; when checked, pages will be accessed using the proxy server configured in General Settings." },

            // Auto Visit
            { "Settings_AutoVisit_Section", "Auto Visit Management" },
            { "Settings_AutoVisit_SectionTip", "When opening configured URLs in Browser Mode with human simulation enabled, links will be extracted and visited automatically." },
            { "Settings_AutoVisit_ManageBtn", "Auto Visit Management..." },
            { "AutoVisit_Manager_Title", "Auto Visit Management" },
            { "AutoVisit_Col_Url", "Target URL" },
            { "AutoVisit_Col_Regex", "Regex Rule" },
            { "AutoVisit_Col_Limit", "Visit Limit" },
            { "AutoVisit_Col_NextPage", "Next Page Locator" },
            { "AutoVisit_Col_LinkDelay", "Link Delay (s)" },
            { "AutoVisit_Col_Refresh", "Refresh Interval (min)" },
            { "AutoVisit_Col_Enabled", "Enabled" },
            { "AutoVisit_Manager_ClearHistory", "Clear 24h Visited History" },
            { "AutoVisit_Manager_ClearHistoryConfirm", "Are you sure you want to clear all visited links history from the last 24 hours?" },
            { "AutoVisit_Manager_ClearHistoryDone", "Visited history has been cleared." },
            { "AutoVisit_Manager_DeleteConfirm", "Are you sure you want to delete this auto visit configuration?" },
            { "AutoVisit_Manager_SelectEditWarn", "Please select an auto visit configuration to edit." },
            { "AutoVisit_Manager_SelectDeleteWarn", "Please select an auto visit configuration to delete." },

            { "AutoVisit_Edit_Title_Add", "Add Auto Visit URL" },
            { "AutoVisit_Edit_Title_Edit", "Edit Auto Visit URL" },
            { "AutoVisit_Edit_Url", "Target URL:" },
            { "AutoVisit_Edit_UrlTT", "The main page URL to perform auto visit (e.g. https://news.ycombinator.com)" },
            { "AutoVisit_Edit_Regex", "Link Regex Rule:" },
            { "AutoVisit_Edit_RegexTT", "Only visit links matching this regex (e.g. .*item\\?id=\\d+)" },
            { "AutoVisit_Edit_Limit", "Visit Link Limit:" },
            { "AutoVisit_Edit_LimitTT", "Stop current round and wait for refresh once this number of links is visited (default 50)" },
            { "AutoVisit_Edit_NextPage", "Next Page Locator:" },
            { "AutoVisit_Edit_NextPageTT", "CSS selector for the next page button. Clicks to flip page at bottom. Leave empty to stop at bottom" },
            { "AutoVisit_Edit_Pick", "Pick" },
            { "AutoVisit_Edit_PickTT", "Pick the next page element visually in the web page" },
            { "AutoVisit_Edit_LinkDelay", "Next Link Delay:" },
            { "AutoVisit_Edit_LinkDelayTT", "Random delay in seconds before visiting next link after completing each link visit (scrolled to bottom) (default 3-8s)" },
            { "AutoVisit_Edit_LinkDelayUnit", "sec (random range)" },
            { "AutoVisit_Edit_RefreshInterval", "Stop Refresh Interval:" },
            { "AutoVisit_Edit_RefreshUnit", "min (random range)" },
            { "AutoVisit_Edit_RefreshTip", "After reaching the link limit or page bottom, randomly waits within this interval (minutes, default 30-60), then reloads and restarts from beginning." },
            { "AutoVisit_Edit_ExcludedUrls", "Exclude URLs:" },
            { "AutoVisit_Edit_ExcludedUrlsTT", "Set URLs to exclude from visiting, multiple URLs allowed (one per line, or comma/semicolon separated). Matching links will be skipped." },
            { "AutoVisit_Edit_Enabled", "Enable this auto visit rule" },
            { "AutoVisit_Edit_InvalidUrl", "Please enter a valid target URL (must start with http:// or https://)." },
            { "AutoVisit_Edit_InvalidRegex", "Invalid regex pattern: {0}" },
            { "AutoVisit_Edit_InvalidLimit", "Visit link limit must be an integer greater than 0." },
            { "AutoVisit_Edit_InvalidLinkDelay", "Invalid next link delay: min seconds cannot be less than 0, and max seconds must be greater than or equal to min seconds." },
            { "AutoVisit_Edit_InvalidInterval", "Invalid refresh interval: min duration must be > 0 and <= max duration." },

            { "AutoVisit_Status_Matching", "Auto Visit: Fetching matching links on current screen (visited: {0}/{1})..." },
            { "AutoVisit_Status_VisitingChild", "Auto Visit [{0}/{1}]: Visiting {2}" },
            { "AutoVisit_Status_ScrollingChild", "Auto Visit: Simulating human browsing on child page..." },
            { "AutoVisit_Status_ReturnMain", "Auto Visit: Reached bottom of child page, returning to main page..." },
            { "AutoVisit_Status_WaitingNextLink", "Auto Visit: Link completed, waiting {0}s before next link..." },
            { "AutoVisit_Status_ScrollNextScreen", "Auto Visit: Screen links completed, scrolling to next screen..." },
            { "AutoVisit_Status_NextPage", "Auto Visit: Reached bottom of main page, clicking next page..." },
            { "AutoVisit_Status_WaitingRefresh", "Auto Visit: Round completed ({0} links visited), will restart in {1} minutes ({2})..." },
            { "AutoVisit_Status_WaitingCountdown", "Auto Visit waiting for refresh, {0}m {1}s remaining..." },
            { "AutoVisit_Status_Stopped", "Auto Visit stopped" },


            { "Settings_General_Options", "General Options" },
            { "Settings_General_Language", "Language:" },
            { "Settings_General_Lang_ZH", "中文 (Chinese)" },
            { "Settings_General_Lang_EN", "English" },
            { "Settings_General_ShowMainWindow", "Show main window on startup" },
            { "Settings_General_ShowMainWindowTip", "When checked, the main window will automatically be shown on startup; when unchecked, it will start hidden in system tray" },
            { "Settings_General_AutoStart", "Auto-start AIHelper on boot" },
            { "Settings_General_AutoStartTip", "When checked, AIHelper will automatically run in background on Windows startup" },
            { "Settings_General_AutoSubmit", "Auto-submit prompt" },
            { "Settings_General_AutoSubmitTip", "When checked, automatically clicks send after injecting prompt; when unchecked, creates new chat and injects prompt without sending" },
            { "Settings_General_EnableContextMenu", "Enable file context menu" },
            { "Settings_General_EnableContextMenuTip", "Adds a context menu shortcut for images, Office docs, PDFs, text, and code files to quickly summon the AI toolbar" },
            { "ContextMenu_Title", "Process with AIHelper" },
            { "ContextMenu_More", "More..." },
            { "ContextMenu_AuthFailed", "Administrator authorization failed or was canceled. Context menu settings could not be changed." },
            { "Settings_General_ProxySettings", "Network Proxy Settings" },
            { "Settings_General_ProxyServer", "Proxy Server:" },
            { "Settings_General_ProxyTip", "Example: http://127.0.0.1:7890 or socks5://127.0.0.1:1080. Leave empty to use Windows system proxy" },

            { "Settings_Platform_Name", "Name" },
            { "Settings_Platform_Url", "URL" },
            { "Settings_Platform_Active", "Active" },
            { "Settings_Platform_Proxy", "Proxy" },
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
            { "Settings_Hotkey_MainWindowKey", "Open Main Window Hotkey:" },
            { "Settings_Hotkey_Tip", "Click the text box and press key combination" },
            { "Hotkey_Conflict_Panel", "⚠️ Conflicts with [Panel Hotkey]" },
            { "Hotkey_Conflict_MainWindow", "⚠️ Conflicts with [Open Main Window Hotkey]" },
            { "Hotkey_Conflict_Action", "⚠️ Conflicts with action [{0}]" },
            { "Hotkey_Conflict_System", "⚠️ This hotkey is already in use by the system or another application" },
            { "Hotkey_Conflict_SaveError", "Hotkey conflicts detected, please resolve before saving:\n\n{0}" },
            { "Main_Status_HotkeyFailed", "Failed to register hotkey(s): {0} (may already be in use)" },

            { "Settings_About_Title", "Update Information" },
            { "Settings_About_ProjectUrl", "Project URL" },
            { "Settings_About_OpenProject", "Visit Project Page" },
            { "Settings_About_UpdateUrl", "Release URL" },
            { "Settings_About_OpenUpdate", "View Releases" },
            { "Settings_About_Tip", "Tip: Click the link or button to open the page in your default browser." },
            { "Settings_About_OpenUrlError", "Cannot open link: {0}" },
            { "Settings_About_AutoCheckUpdate", "Auto-check for updates" },
            { "Settings_About_AutoCheckUpdateTip", "Check for new versions on GitHub Releases 1 minute after app startup" },

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
            { "PlatformEdit_UseProxy", "Use Proxy:" },
            { "PlatformEdit_UseProxyTT", "When checked, use the proxy server configured in General Settings for this platform" },

            // ActionEditWindow
            { "ActionEdit_Title_Edit", "Edit Action" },
            { "ActionEdit_Title_Add", "Add Action" },
            { "ActionEdit_Name", "Name:" },
            { "ActionEdit_Icon", "Icon (Emoji):" },
            { "ActionEdit_SortOrder", "Sort Order:" },
            { "ActionEdit_Hotkey", "Hotkey:" },
            { "ActionEdit_Prompt", "Prompt:" },
            { "ActionEdit_EmptyNameWarn", "Name cannot be empty." },
            { "ActionEdit_ClearHotkey", "Clear" },
            { "ActionEdit_Platform", "Platform:" },
            { "ActionEdit_Platform_Default", "Default (Active Platform)" },
            { "Settings_Action_Platform", "Platform" },
            { "Settings_Action_Platform_Default", "Default" },

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
            { "Inject_PageNotReady", "Page is not ready (load timed out), please retry" },
            { "Inject_TextLost", "The injected text was cleared by the page, please retry" },
            { "Inject_Failed", "Injection failed" },

            // Selection Toolbar
            { "Settings_General_SelectionToolbar", "Selection Toolbar" },
            { "Settings_General_EnableSelectionToolbar", "Enable Selection Toolbar" },
            { "Settings_General_SelectionToolbarTip", "After selecting text in any app, a toolbar pops up for quick AI actions" },
            { "Settings_General_SelectionToolbarCopy", "Toolbar Copy Button:" },
            { "Settings_General_SelectionToolbarCopy_Disabled", "Disabled (Default)" },
            { "Settings_General_SelectionToolbarCopy_First", "Enabled (First Position)" },
            { "Settings_General_SelectionToolbarCopy_Last", "Enabled (Last Position)" },
            { "SelectionToolbar_Copy", "Copy" },
            { "Settings_General_SelectionToolbarAutoHide", "Toolbar Auto-Hide Duration (seconds):" },
            { "Settings_General_SelectionToolbarAutoHideTip", "The selection toolbar will auto-hide after {0} second(s) of inactivity" },
            { "Settings_Selection_AppScopeTitle", "Application Scope Settings" },
            { "Settings_Selection_AppScopeAll", "All Applications (Default)" },
            { "Settings_Selection_AppScopeInclude", "Specified Applications" },
            { "Settings_Selection_AppScopeExclude", "Excluded Applications" },
            { "Settings_Selection_AppScopeAppsTip", "Enter application process names, separated by newline or comma (e.g. notepad.exe, chrome.exe, or devenv)" },
            { "Settings_Selection_SelectApps", "Select Applications..." },
            { "AppSelection_Title", "Select Applications" },
            { "AppSelection_SearchPlaceholder", "Search app name or process..." },
            { "AppSelection_OnlyWindowApps", "Only show apps with windows (Filter system services)" },
            { "AppSelection_Refresh", "Refresh" },
            { "AppSelection_SelectAll", "Select All" },
            { "AppSelection_InvertSelect", "Invert Selection" },
            { "AppSelection_ClearSelect", "Clear Selection" },
            { "AppSelection_SelectedCount", "Selected {0} app(s)" },
            { "AppSelection_Append", "Append to List" },
            { "AppSelection_Replace", "Replace Current List" },
            { "AppSelection_NoAppsFound", "No matching running applications found" },
            { "SelectionToolbar_Sending", "Sending to {0}..." },
            { "SelectionToolbar_More", "More ▾" },
            { "SelectionToolbar_Back", "Back" },

            { "Inject_Exception", "Injection exception: {0}" },

            // Other Settings - Config Management
            { "Settings_Other_ConfigManagement", "Config Management" },
            { "Settings_Other_ConfigManagementTip", "Backup current configuration to a file, or restore from a backup file." },
            { "Settings_Other_BackupConfig", "📦 Backup Config" },
            { "Settings_Other_RestoreConfig", "📂 Restore Config" },
            { "Settings_Other_OpenConfigDir", "📁 Open Config Dir" },
            { "Settings_Other_NoConfigFile", "Configuration file not found." },
            { "Settings_Other_BackupSuccess", "Configuration backup successful!" },
            { "Settings_Other_BackupFailed", "Configuration backup failed: {0}" },
            { "Settings_Other_RestoreConfirm", "Are you sure you want to restore configuration? Current settings will be overwritten." },
            { "Settings_Other_RestoreSuccess", "Configuration restored successfully!" },
            { "Settings_Other_RestoreFailed", "Configuration restore failed: {0}" },
            { "Settings_Other_OpenDirFailed", "Failed to open config directory: {0}" },
            // Batch Image
            { "Settings_Tab_BatchImage", "Batch Image" },
            { "Settings_Batch_Title", "Batch Image Settings" },
            { "Settings_Batch_Desc", "Use the \"Batch Image\" action to pick a CSV file (columns: prompt, filename, platform). Each row starts a new chat, asks the AI platform for an image and saves it under the given file name." },
            { "Settings_Batch_SavePathSection", "Save Location" },
            { "Settings_Batch_Browse", "Browse..." },
            { "Settings_Batch_Open", "Open" },
            { "Settings_Batch_SavePathTip", "Leave empty to use the default: {0}\nEach batch creates a timestamped subfolder here (e.g. 20261001_143000). When one prompt yields several images, the first goes into that subfolder and the rest into its \"Alternates\" folder." },
            { "Settings_Batch_BrowseTitle", "Choose the batch image save location" },
            { "Settings_Batch_RunSection", "Run Options" },
            { "Settings_Batch_Timeout", "Timeout per image (s):" },
            { "Settings_Batch_Retry", "Retries on failure:" },
            { "Settings_Batch_Interval", "Interval between images (s):" },
            { "Settings_Batch_RunTip", "Timeout 30-1800 s, retries 0-5, interval 0-600 s with min not greater than max. A random wait within the interval is inserted between images to reduce the chance of platform rate limiting." },
            { "Settings_Batch_ValidateError", "Invalid batch image settings: timeout 30-1800 s, retries 0-5, interval 0-600 s with min not greater than max, and the save location must be a full path (or empty)." },
            { "Settings_Batch_OpenDirFailed", "Failed to open folder: {0}" },
            { "Settings_Batch_PresetSection", "Page preset (record model, ratio and other options)" },
            { "Settings_Batch_PresetDesc", "Some sites need a model, ratio or image count chosen before generating. In the \"Page preset\" area at the top of the Batch Image window, pick the platform and click \"● Record\", then make your choices on the page in the main window as usual (do not type a prompt or send), and click \"✔ Finish\". The choices are replayed before every row. The same area also has \"Open the full-size image before saving\". Recording does not need a CSV file." },
            { "Settings_Batch_PresetOpen", "Save settings and open the Batch Image window" },
            { "Settings_Action_CannotDeleteBatch", "\"Batch Image\" is a built-in system action and cannot be deleted." },
            { "ActionEdit_BatchPromptHint", "Batch Image: {content} is replaced with the prompt from the CSV; without {content} the CSV prompt is appended; leave empty to send the CSV prompt as is.\nRecording the model, ratio and other page options is done in the \"Page preset\" area at the top of the Batch Image window (Settings → Batch Image has a shortcut too)." },
            { "Batch_Title", "Batch Image" },
            { "Batch_SelectCsvTitle", "Choose a batch image CSV file" },
            { "Batch_AllFiles", "All Files" },
            { "Batch_CsvFile", "CSV file:" },
            { "Batch_PickCsv", "Choose..." },
            { "Batch_DefaultPlatform", "Default platform (when the platform column is empty): {0}" },
            { "Batch_OutputDir", "Output folder: {0}" },
            { "Batch_OutputDirPending", "Output folder: a timestamped folder under {0} is created on start" },
            { "Batch_Summary", "{0} row(s), {1} valid, {2} invalid" },
            { "Batch_Progress", "Progress {0}/{1}   Succeeded {2}   Failed {3}   Skipped {4}" },
            { "Batch_Tip", "Keep the main window visible and do not interact with the AI page while running. Pause takes effect after the current image." },
            { "Batch_Col_Index", "Row" },
            { "Batch_Col_Prompt", "Prompt" },
            { "Batch_Col_FileName", "File Name" },
            { "Batch_Col_Platform", "Platform" },
            { "Batch_Col_Status", "Status" },
            { "Batch_Col_Result", "Result" },
            { "Batch_Start", "▶ Start" },
            { "Batch_Pause", "⏸ Pause" },
            { "Batch_Resume", "▶ Resume" },
            { "Batch_Stop", "⏹ Stop" },
            { "Batch_RetryFailed", "Retry Failed" },
            { "Batch_OpenOutput", "Open Output Folder" },
            { "Batch_Settings", "Settings" },
            { "Batch_Close", "Close" },
            { "Batch_ItemStatus_Pending", "Pending" },
            { "Batch_ItemStatus_Running", "Running" },
            { "Batch_ItemStatus_Success", "Succeeded" },
            { "Batch_ItemStatus_Failed", "Failed" },
            { "Batch_ItemStatus_Skipped", "Skipped" },
            { "Batch_ItemStatus_Invalid", "Invalid" },
            { "Batch_Msg_EmptyPrompt", "Prompt is empty" },
            { "Batch_Msg_PlatformNotFound", "Platform not found: {0}" },
            { "Batch_Msg_NoPlatform", "No platform available" },
            { "Batch_Msg_NoImage", "No image found in the reply: {0}" },
            { "Batch_Msg_Timeout", "Timed out waiting for the image" },
            { "Batch_Msg_ExtractFailed", "Failed to extract the image" },
            { "Batch_Msg_SaveFailed", "Save failed: {0}" },
            { "Batch_Msg_SubmitFailed", "Submit failed: {0}" },
            { "Batch_Msg_PlatformNotReady", "Platform failed to load, skipped: {0}" },
            { "Batch_Msg_SkippedNotLoggedIn", "Not logged in to the platform, skipped" },
            { "Batch_Msg_SkippedConsecutive", "Skipped after {0} consecutive failures on this platform" },
            { "Batch_Msg_Stopped", "Stopped" },
            { "Batch_Msg_PartialExtract", "({0} image(s) could not be extracted)" },
            { "Batch_Status_Busy", "Batch image is running, stop it or wait for it to finish first" },
            { "Batch_Status_Item", "Batch image {0}/{1}: {2}" },
            { "Batch_Status_Ready", "Ready" },
            { "Batch_Status_Pausing", "Will pause after the current image" },
            { "Batch_Status_Paused", "Paused, click Resume to continue" },
            { "Batch_Status_Resumed", "Resumed" },
            { "Batch_Status_Stopping", "Stopping..." },
            { "Batch_Status_Stopped", "Batch image stopped" },
            { "Batch_Status_Finished", "Batch image finished: {0} succeeded, {1} failed, {2} skipped" },
            { "Batch_Step_NewChat", "starting a new chat" },
            { "Batch_Step_Injecting", "injecting the prompt" },
            { "Batch_Step_Submitting", "submitting" },
            { "Batch_Step_Waiting", "waiting for the image ({0} s)" },
            { "Batch_Step_Extracting", "extracting images" },
            { "Batch_Step_Retry", "retry {0}" },
            { "Batch_Step_Interval", "Continuing in {0} s" },
            { "Batch_Csv_ReadFailed", "Failed to read the CSV: {0}" },
            { "Batch_Csv_NoPromptColumn", "No prompt column found in the header (prompt / 提示词)" },
            { "Batch_Csv_Empty", "The CSV has no data rows" },
            { "Batch_NothingToRun", "There are no pending rows" },
            { "Batch_OutputDirFailed", "Failed to create the output folder: {0}" },
            { "Batch_ConfirmStopOnClose", "Batch image is running. Stop it and close?" },
            { "Batch_AlternatesFolder", "Alternates" },
            { "Batch_Report_Header", "Row,Prompt,File Name,Platform,Status,Saved Files,Message,Elapsed (s)" },
            { "Batch_Step_Preset", "applying the page preset" },
            { "Batch_Step_OpenLarge", "opening full-size image {0}/{1}" },
            { "Batch_Msg_PresetFailed", "Page preset failed: {0}" },
            { "Batch_Preset_Title", "Page preset" },
            { "Batch_Preset_Record", "● Record" },
            { "Batch_Preset_Finish", "✔ Finish" },
            { "Batch_Preset_Cancel", "Cancel" },
            { "Batch_Preset_Test", "Test" },
            { "Batch_Preset_Clear", "Clear" },
            { "Batch_Preset_RemoveStep", "Remove this step" },
            { "Batch_Preset_OpenLarge", "Open the full-size image before saving" },
            { "Batch_Preset_OpenLargeTip", "For sites that show only a thumbnail in the chat and load the original when it is clicked: each image is clicked, the image in the viewer is saved, then the viewer is closed. The thumbnail is saved when no full-size view opens." },
            { "Batch_Preset_None", "Not set: the page's current model and options are used. If the site needs a model or image options chosen first, click Record and choose them on the page in the main window; they are replayed after every new chat." },
            { "Batch_Preset_Steps", "These {0} step(s) are replayed after every new chat (buttons already in their recorded state are not clicked again):" },
            { "Batch_Preset_Starting", "Opening {0} and starting a new chat for recording..." },
            { "Batch_Preset_RecordingHint", "Recording, {0} step(s) so far. Choose the model, aspect ratio and other options on the page in the main window (do not type a prompt or send), then click Finish. The recording replaces the existing preset." },
            { "Batch_Preset_RecordingStatus", "Recording the page preset" },
            { "Batch_Preset_Badge", "● AIHelper is recording the page preset" },
            { "Batch_Preset_BarStarting", "Opening {0} and starting a new chat, please wait..." },
            { "Batch_Preset_BarRecording", "● Recording {0}: choose the model, aspect ratio and other options on the page below (do not type a prompt or send). {1} step(s) so far. Click Finish on the right when done." },
            { "Batch_Preset_BarBack", "Back to the Batch Image window" },
            { "Batch_Preset_RecordFailed", "Could not start recording: {0}" },
            { "Batch_Preset_Saved", "Saved the page preset of {0} ({1} step(s))" },
            { "Batch_Preset_NothingRecorded", "Nothing was recorded; the existing preset is unchanged" },
            { "Batch_Preset_RecordCancelled", "Recording cancelled" },
            { "Batch_Preset_ConfirmClear", "Clear the page preset of {0}?" },
            { "Batch_Preset_Testing", "Testing the page preset..." },
            { "Batch_Preset_TestOk", "Test finished: {0} step(s) performed, {1} skipped (already in place). Check the options on the page in the main window." },
            { "Batch_Preset_TestFailed", "Test failed: {0}" },
            { "Batch_Preset_Busy", "A page preset is being recorded or tested, finish it first" },
            { "Batch_Preset_StepNotFound", "Step {0} (\"{1}\") was not found on the page" },
            { "Batch_Preset_Step_Click", "Click \"{0}\"" },
            { "Batch_Preset_Step_Select", "Select \"{1}\" in \"{0}\"" },
            { "Batch_Preset_Step_Check", "Check \"{0}\"" },
            { "Batch_Preset_Step_Uncheck", "Uncheck \"{0}\"" },
            { "Batch_Preset_Step_Input", "Enter {1} in \"{0}\"" }
        };
    }
}
