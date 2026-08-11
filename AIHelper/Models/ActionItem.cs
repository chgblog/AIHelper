using System;

namespace AIHelper.Models
{
    /// <summary>
    /// Represents a quick action for the AI assistant
    /// </summary>
    public class ActionItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Prompt { get; set; }
        public string HotkeyModifiers { get; set; }
        public string HotkeyKey { get; set; }
        public bool IsBuiltIn { get; set; }
        public int SortOrder { get; set; }

        /// <summary>
        /// Emoji icon for display in selection toolbar
        /// </summary>
        public string Icon { get; set; } = "";
    }
}
