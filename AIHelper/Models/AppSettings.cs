using System.Collections.Generic;
using System.Linq;

namespace AIHelper.Models
{
    /// <summary>
    /// Application settings
    /// </summary>
    public class AppSettings
    {
        public List<AiPlatform> Platforms { get; set; } = new List<AiPlatform>();
        public string ActivePlatformId { get; set; }
        public string PanelHotkeyModifiers { get; set; } = "Ctrl+Alt";
        public string PanelHotkeyKey { get; set; } = "Space";

        /// <summary>
        /// 打开主界面快捷键修饰键（默认 Shift）
        /// </summary>
        public string MainWindowHotkeyModifiers { get; set; } = "Shift";

        /// <summary>
        /// 打开主界面快捷键主键（默认 Space）
        /// </summary>
        public string MainWindowHotkeyKey { get; set; } = "Space";

        /// <summary>
        /// 当前配置架构与程序版本号
        /// </summary>
        public const string CurrentConfigVersion = "0.8.7";

        /// <summary>
        /// 配置版本号（用于设置平滑迁移，当前版本 0.8.7）
        /// </summary>
        public string ConfigVersion { get; set; }

        public List<ActionItem> Actions { get; set; } = new List<ActionItem>();
        public bool IsFirstRun { get; set; } = true;

        /// <summary>
        /// 程序运行后是否显示主窗口
        /// </summary>
        public bool ShowMainWindowOnStartup { get; set; } = true;

        public double WindowWidth { get; set; } = 520;
        public double WindowHeight { get; set; } = 800;
        public bool AutoStart { get; set; } = false;
        public bool AutoSubmit { get; set; } = true;

        /// <summary>
        /// 主窗口底部操作是否开启新会话（默认开启）
        /// </summary>
        public bool QuickActionStartNewChat { get; set; } = true;

        /// <summary>
        /// 是否启用自动检测新版本（默认选中）
        /// </summary>
        public bool AutoCheckUpdate { get; set; } = true;

        /// <summary>
        /// 是否启用 Windows 文件右键菜单
        /// </summary>
        public bool EnableContextMenu { get; set; } = false;

        public string ProxyServer { get; set; } = "";
        public string Language { get; set; }

        /// <summary>
        /// 是否启用划词弹出工具条
        /// </summary>
        public bool EnableSelectionToolbar { get; set; } = true;

        /// <summary>
        /// 划词工具条是否增加复制按钮 (0: 不开启 [默认], 1: 开启且在第一个, 2: 开启且在最后一个)
        /// </summary>
        public int SelectionToolbarCopyMode { get; set; } = 0;

        /// <summary>
        /// 划词弹出工具条应用范围模式 (0: 全部应用 [默认], 1: 指定应用, 2: 排除应用)
        /// </summary>
        public int SelectionAppScopeMode { get; set; } = 0;

        /// <summary>
        /// 划词弹出工具条指定/排除的应用进程名列表 (例如: notepad.exe, chrome.exe，换行或逗号分隔)
        /// </summary>
        public string SelectionAppScopeApps { get; set; } = "";

        /// <summary>
        /// 划词弹出工具条自动消失秒数 (默认 3 秒)
        /// </summary>
        public int SelectionToolbarAutoHideSeconds { get; set; } = 3;

        private string _projectUrl = "https://github.com/chgblog/AIHelper";
        public string ProjectUrl
        {
            get => string.IsNullOrWhiteSpace(_projectUrl) ? "https://github.com/chgblog/AIHelper" : _projectUrl;
            set => _projectUrl = value;
        }

        private string _updateUrl = "https://github.com/chgblog/AIHelper/releases";
        public string UpdateUrl
        {
            get => string.IsNullOrWhiteSpace(_updateUrl) ? "https://github.com/chgblog/AIHelper/releases" : _updateUrl;
            set => _updateUrl = value;
        }

        /// <summary>
        /// 模拟访问 - 每次滚动间隔最小时长（秒，支持小数，默认 0.5）
        /// </summary>
        public double SimulateVisitMinIntervalSeconds { get; set; } = 0.5;

        /// <summary>
        /// 模拟访问 - 每次滚动间隔最大时长（秒，支持小数，默认 3.0）
        /// </summary>
        public double SimulateVisitMaxIntervalSeconds { get; set; } = 3.0;

        /// <summary>
        /// 模拟访问 - 每次向下滚动最小移动间距（像素，默认 100）
        /// </summary>
        public int SimulateVisitMinScrollDistance { get; set; } = 100;

        /// <summary>
        /// 模拟访问 - 每次向下滚动最大移动间距（像素，默认 300）
        /// </summary>
        public int SimulateVisitMaxScrollDistance { get; set; } = 300;

        /// <summary>
        /// 模拟访问 - 是否启用代理（默认 false，与系统代理一致；选中则按常规设置中的代理访问）
        /// </summary>
        public bool SimulateVisitUseProxy { get; set; } = false;

        /// <summary>
        /// 浏览器模式最后访问的 URL（默认 "https://www.bing.com"）
        /// </summary>
        public string BrowserModeLastUrl { get; set; } = "https://www.bing.com";

        /// <summary>
        /// 自动访问配置列表
        /// </summary>
        public List<AutoVisitConfig> AutoVisitConfigs { get; set; } = new List<AutoVisitConfig>();

        /// <summary>
        /// 批量生图 - 默认保存路径（为空时使用 我的图片\AIHelper\BatchImages）
        /// </summary>
        public string BatchImageSavePath { get; set; } = "";

        /// <summary>
        /// 批量生图 - 单张图片等待超时（秒，默认 300）
        /// </summary>
        public int BatchImageTimeoutSeconds { get; set; } = 300;

        /// <summary>
        /// 批量生图 - 单行失败后的重试次数（默认 1）
        /// </summary>
        public int BatchImageRetryCount { get; set; } = 1;

        /// <summary>
        /// 批量生图 - 两张之间的最小间隔（秒，默认 3）
        /// </summary>
        public int BatchImageMinIntervalSeconds { get; set; } = 3;

        /// <summary>
        /// 批量生图 - 两张之间的最大间隔（秒，默认 8）
        /// </summary>
        public int BatchImageMaxIntervalSeconds { get; set; } = 8;

        /// <summary>
        /// 批量生图 - 上次选择 CSV 文件所在目录
        /// </summary>
        public string BatchImageLastCsvDirectory { get; set; } = "";


        /// <summary>
        /// Gets the active platform
        /// </summary>
        public AiPlatform GetActivePlatform()
        {
            return Platforms?.FirstOrDefault(p => p.Id == ActivePlatformId) ?? Platforms?.FirstOrDefault();
        }

        /// <summary>
        /// 批量生图保存根目录：未设置时取 我的图片\AIHelper\BatchImages，换用户/换电脑也不会失效
        /// </summary>
        public string GetBatchImageSaveRoot()
        {
            if (!string.IsNullOrWhiteSpace(BatchImageSavePath))
            {
                return BatchImageSavePath.Trim();
            }
            return GetDefaultBatchImageSaveRoot();
        }

        public static string GetDefaultBatchImageSaveRoot()
        {
            return System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures),
                "AIHelper", "BatchImages");
        }

        /// <summary>
        /// 确保系统内置操作（批量生图）存在且只有一个。老版本配置升级、恢复配置后都会经过这里。
        /// 返回 true 表示有改动需要保存。
        /// </summary>
        public bool EnsureSystemActions()
        {
            if (Actions == null)
            {
                Actions = new List<ActionItem>();
            }

            bool changed = false;
            var batchActions = Actions.Where(a => a != null && a.IsBatchImage).ToList();
            if (batchActions.Count == 0)
            {
                int nextSort = Actions.Count > 0 ? Actions.Where(a => a != null).Select(a => a.SortOrder).DefaultIfEmpty(0).Max() + 1 : 1;
                bool isEn = string.Equals(Language, "en", System.StringComparison.OrdinalIgnoreCase);
                Actions.Add(ActionItem.CreateBatchImageAction(isEn, nextSort));
                changed = true;
            }
            else if (batchActions.Count > 1)
            {
                foreach (var extra in batchActions.Skip(1))
                {
                    Actions.Remove(extra);
                }
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Creates default settings
        /// </summary>
        public static AppSettings CreateDefault()
        {
            var deepSeekId = System.Guid.NewGuid().ToString();
            var lang = Services.LanguageManager.GetDefaultLanguageByTimeZone();
            bool isEn = lang == "en";

            var settings = new AppSettings
            {
                ConfigVersion = CurrentConfigVersion,
                IsFirstRun = true,
                ShowMainWindowOnStartup = true,
                QuickActionStartNewChat = true,
                AutoCheckUpdate = true,
                Language = lang,
                ActivePlatformId = deepSeekId,
                Platforms = new List<AiPlatform>
                {
                    new AiPlatform
                    {
                        Name = "Claude",
                        Url = "https://claude.ai/new",
                        IsActive = false,
                        UseProxy = true,
                        InputSelector = "p.is-empty.is-editor-empty",
                        SubmitSelector = "#_r_b8_ > span.inline-flex.min-w-0 > span"
                    },
                    new AiPlatform
                    {
                        Name = "Gemini",
                        Url = "https://gemini.google.com/app",
                        IsActive = false,
                        UseProxy = true,
                        InputSelector = "div.ng-tns-c4151070770-5.single-line-format > div.text-input-field-main-area.ng-tns-c4151070770-5 > div.text-input-field_textarea-inner.ng-tns-c4151070770-5 > div.ng-tns-c4151070770-5.textarea-wrapper > rich-textarea.text-input-field_textarea.ql-container > div.ql-editor.ql-blank > p",
                        SubmitSelector = "div.trailing-actions-wrapper.ng-tns-c4151070770-6 > div.input-buttons-wrapper-bottom.persistent-mic > div.mat-mdc-tooltip-trigger.send-button-container > gem-icon-button.send-button.ng-tns-c4151070770-6 > button.mdc-icon-button.mat-mdc-icon-button > gem-icon > mat-icon.mat-icon.notranslate"
                    },
                    new AiPlatform
                    {
                        Id = deepSeekId,
                        Name = "DeepSeek",
                        Url = "https://chat.deepseek.com/",
                        IsActive = true,
                        UseProxy = false
                    },
                    new AiPlatform
                    {
                        Name = "ChatGPT",
                        Url = "https://chatgpt.com/",
                        IsActive = false,
                        UseProxy = true,
                        InputSelector = "p.placeholder",
                        SubmitSelector = "#composer-submit-button"
                    },
                    new AiPlatform
                    {
                        Name = isEn ? "Qwen" : "千问",
                        Url = "https://chat.qwen.ai/",
                        IsActive = false,
                        UseProxy = false,
                        InputSelector = "textarea[placeholder=\"有什么我能帮您的吗？\"]",
                        SubmitSelector = "div.message-input-container-area > div.message-input-right-button > div.message-input-right-button-send > div.chat-prompt-send-button > button.send-button > span.anticon.icon-send > svg"
                    },
                    new AiPlatform
                    {
                        Name = isEn ? "Zhipu" : "智谱",
                        Url = "https://chat.z.ai/",
                        IsActive = false,
                        UseProxy = false,
                        InputSelector = "#chat-input",
                        SubmitSelector = "#send-message-button"
                    },
                    new AiPlatform
                    {
                        Name = "Kimi",
                        Url = "https://www.kimi.com",
                        IsActive = false,
                        UseProxy = false,
                        InputSelector = "#chat-box > div.chat-editor > div.chat-editor-content > div.chat-input > div.chat-input-editor-container > div.chat-input-editor > p",
                        SubmitSelector = "div.send-button-container"
                    }
                },
                Actions = isEn ? new List<ActionItem>
                {
                    new ActionItem { Name = "Translate", Prompt = "Translate:\n\n{content}", HotkeyModifiers = "Ctrl+Alt", HotkeyKey = "T", IsBuiltIn = true, SortOrder = 1, Icon = "🔄" },
                    new ActionItem { Name = "Explain", Prompt = "Please explain the following content in detail:\n\n{content}", HotkeyModifiers = "Ctrl+Alt", HotkeyKey = "E", IsBuiltIn = true, SortOrder = 2, Icon = "📖" },
                    new ActionItem { Name = "Summary", Prompt = "Please extract a summary for the following content:\n\n{content}", HotkeyModifiers = "Ctrl+Alt", HotkeyKey = "S", IsBuiltIn = true, SortOrder = 3, Icon = "📝" },
                    new ActionItem { Name = "Polish", Prompt = "Please polish the following content to make it more fluent and professional:\n\n{content}", HotkeyModifiers = "Ctrl+Alt", HotkeyKey = "R", IsBuiltIn = true, SortOrder = 4, Icon = "✨" },
                    new ActionItem { Name = "Grammar Check", Prompt = "Please check the following content for grammar errors and provide suggestions:\n\n{content}", HotkeyModifiers = "Ctrl+Alt", HotkeyKey = "G", IsBuiltIn = true, SortOrder = 5, Icon = "✅" },
                    new ActionItem { Name = "Summarize", Prompt = "Please summarize the following content:\n\n{content}", HotkeyModifiers = "Ctrl+Alt", HotkeyKey = "O", IsBuiltIn = false, SortOrder = 6, Icon = "📋" }
                } : new List<ActionItem>
                {
                    new ActionItem { Name = "翻译", Prompt = "翻译：\n\n{content}", HotkeyModifiers = "Ctrl+Alt", HotkeyKey = "T", IsBuiltIn = true, SortOrder = 1, Icon = "🔄" },
                    new ActionItem { Name = "解释", Prompt = "请详细解释以下内容：\n\n{content}", HotkeyModifiers = "Ctrl+Alt", HotkeyKey = "E", IsBuiltIn = true, SortOrder = 2, Icon = "📖" },
                    new ActionItem { Name = "摘要", Prompt = "请为以下内容提取摘要：\n\n{content}", HotkeyModifiers = "Ctrl+Alt", HotkeyKey = "S", IsBuiltIn = true, SortOrder = 3, Icon = "📝" },
                    new ActionItem { Name = "润色", Prompt = "请润色以下内容，使其更加通顺专业：\n\n{content}", HotkeyModifiers = "Ctrl+Alt", HotkeyKey = "R", IsBuiltIn = true, SortOrder = 4, Icon = "✨" },
                    new ActionItem { Name = "语法检查", Prompt = "请检查以下内容的语法错误，并提供修改建议：\n\n{content}", HotkeyModifiers = "Ctrl+Alt", HotkeyKey = "G", IsBuiltIn = true, SortOrder = 5, Icon = "✅" },
                    new ActionItem { Name = "总结", Prompt = "请总结以下内容：\n\n{content}", HotkeyModifiers = "Ctrl+Alt", HotkeyKey = "O", IsBuiltIn = false, SortOrder = 6, Icon = "📋" }
                }
            };
            settings.EnsureSystemActions();
            return settings;
        }
    }
}
