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
        public List<ActionItem> Actions { get; set; } = new List<ActionItem>();
        public bool IsFirstRun { get; set; } = true;
        public double WindowWidth { get; set; } = 520;
        public double WindowHeight { get; set; } = 800;
        public bool AutoStart { get; set; } = false;

        /// <summary>
        /// Gets the active platform
        /// </summary>
        public AiPlatform GetActivePlatform()
        {
            return Platforms?.FirstOrDefault(p => p.Id == ActivePlatformId) ?? Platforms?.FirstOrDefault();
        }

        /// <summary>
        /// Creates default settings
        /// </summary>
        public static AppSettings CreateDefault()
        {
            var deepSeekId = System.Guid.NewGuid().ToString();

            var settings = new AppSettings
            {
                IsFirstRun = true,
                ActivePlatformId = deepSeekId,
                Platforms = new List<AiPlatform>
                {
                    new AiPlatform
                    {
                        Name = "Claude",
                        Url = "https://claude.ai/new",
                        IsActive = false,
                        InputSelector = "p.is-empty.is-editor-empty",
                        SubmitSelector = "#_r_b8_ > span.inline-flex.min-w-0 > span"
                    },
                    new AiPlatform
                    {
                        Name = "Gemini",
                        Url = "https://gemini.google.com/app",
                        IsActive = false,
                        InputSelector = "div.ng-tns-c4151070770-5.single-line-format > div.text-input-field-main-area.ng-tns-c4151070770-5 > div.text-input-field_textarea-inner.ng-tns-c4151070770-5 > div.ng-tns-c4151070770-5.textarea-wrapper > rich-textarea.text-input-field_textarea.ql-container > div.ql-editor.ql-blank > p",
                        SubmitSelector = "div.trailing-actions-wrapper.ng-tns-c4151070770-6 > div.input-buttons-wrapper-bottom.persistent-mic > div.mat-mdc-tooltip-trigger.send-button-container > gem-icon-button.send-button.ng-tns-c4151070770-6 > button.mdc-icon-button.mat-mdc-icon-button > gem-icon > mat-icon.mat-icon.notranslate"
                    },
                    new AiPlatform
                    {
                        Id = deepSeekId,
                        Name = "DeepSeek",
                        Url = "https://chat.deepseek.com/",
                        IsActive = true
                    },
                    new AiPlatform
                    {
                        Name = "ChatGPT",
                        Url = "https://chatgpt.com/",
                        IsActive = false,
                        InputSelector = "p.placeholder",
                        SubmitSelector = "#composer-submit-button"
                    },
                    new AiPlatform
                    {
                        Name = "千问",
                        Url = "https://chat.qwen.ai/",
                        IsActive = false,
                        InputSelector = "textarea[placeholder=\"有什么我能帮您的吗？\"]",
                        SubmitSelector = "div.message-input-container-area > div.message-input-right-button > div.message-input-right-button-send > div.chat-prompt-send-button > button.send-button > span.anticon.icon-send > svg"
                    },
                    new AiPlatform
                    {
                        Name = "智谱",
                        Url = "https://chat.z.ai/",
                        IsActive = false,
                        InputSelector = "#chat-input",
                        SubmitSelector = "#send-message-button"
                    },
                    new AiPlatform
                    {
                        Name = "Kimi",
                        Url = "https://www.kimi.com",
                        IsActive = false,
                        InputSelector = "#chat-box > div.chat-editor > div.chat-editor-content > div.chat-input > div.chat-input-editor-container > div.chat-input-editor > p",
                        SubmitSelector = "div.send-button-container"
                    }
                },
                Actions = new List<ActionItem>
                {
                    new ActionItem { Name = "翻译", Prompt = "翻译以下内容（中文翻译为英文，英文翻译为中文）：\n\n{content}", HotkeyModifiers = "Ctrl+Alt", HotkeyKey = "T", IsBuiltIn = true, SortOrder = 1 },
                    new ActionItem { Name = "解释", Prompt = "请详细解释以下内容：\n\n{content}", HotkeyModifiers = "Ctrl+Alt", HotkeyKey = "E", IsBuiltIn = true, SortOrder = 2 },
                    new ActionItem { Name = "摘要", Prompt = "请为以下内容提取摘要：\n\n{content}", HotkeyModifiers = "Ctrl+Alt", HotkeyKey = "S", IsBuiltIn = true, SortOrder = 3 },
                    new ActionItem { Name = "润色", Prompt = "请润色以下内容，使其更加通顺专业：\n\n{content}", HotkeyModifiers = "Ctrl+Alt", HotkeyKey = "R", IsBuiltIn = true, SortOrder = 4 },
                    new ActionItem { Name = "语法检查", Prompt = "请检查以下内容的语法错误，并提供修改建议：\n\n{content}", HotkeyModifiers = "Ctrl+Alt", HotkeyKey = "G", IsBuiltIn = true, SortOrder = 5 },
                    new ActionItem { Name = "总结", Prompt = "请总结以下内容：\n\n{content}", HotkeyModifiers = "Ctrl+Alt", HotkeyKey = "O", IsBuiltIn = false, SortOrder = 0 }
                }
            };
            return settings;
        }
    }
}
