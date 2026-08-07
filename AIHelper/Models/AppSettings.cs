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
                    new AiPlatform { Id = System.Guid.NewGuid().ToString(), Name = "Claude", Url = "https://claude.ai/new", IsActive = false },
                    new AiPlatform { Id = System.Guid.NewGuid().ToString(), Name = "Gemini", Url = "https://gemini.google.com/app", IsActive = false },
                    new AiPlatform { Id = deepSeekId, Name = "DeepSeek", Url = "https://chat.deepseek.com/", IsActive = true }
                },
                Actions = new List<ActionItem>
                {
                    new ActionItem { Name = "翻译", Prompt = "请将以下内容翻译为中文：\n\n{content}", HotkeyModifiers = "Ctrl+Alt", HotkeyKey = "T", IsBuiltIn = true, SortOrder = 1 },
                    new ActionItem { Name = "解释", Prompt = "请详细解释以下内容：\n\n{content}", HotkeyModifiers = "Ctrl+Alt", HotkeyKey = "E", IsBuiltIn = true, SortOrder = 2 },
                    new ActionItem { Name = "摘要", Prompt = "请为以下内容提取摘要：\n\n{content}", HotkeyModifiers = "Ctrl+Alt", HotkeyKey = "S", IsBuiltIn = true, SortOrder = 3 },
                    new ActionItem { Name = "润色", Prompt = "请润色以下内容，使其更加通顺专业：\n\n{content}", HotkeyModifiers = "Ctrl+Alt", HotkeyKey = "R", IsBuiltIn = true, SortOrder = 4 },
                    new ActionItem { Name = "语法检查", Prompt = "请检查以下内容的语法错误，并提供修改建议：\n\n{content}", HotkeyModifiers = "Ctrl+Alt", HotkeyKey = "G", IsBuiltIn = true, SortOrder = 5 }
                }
            };
            return settings;
        }
    }
}
