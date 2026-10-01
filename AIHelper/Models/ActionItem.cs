using System;
using Newtonsoft.Json;

namespace AIHelper.Models
{
    /// <summary>
    /// 内置操作类型标记（程序内部使用，设置界面不显示也不可编辑）
    /// </summary>
    public static class ActionTypes
    {
        /// <summary>
        /// 批量生图：触发后选择 CSV 文件，逐行生成图片并保存
        /// </summary>
        public const string BatchImage = "BatchImage";
    }

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

        /// <summary>
        /// 指定使用的平台 Id，为空时使用当前激活平台
        /// </summary>
        public string PlatformId { get; set; } = "";

        /// <summary>
        /// 内置操作类型（见 <see cref="ActionTypes"/>），为空表示普通操作
        /// </summary>
        public string ActionType { get; set; } = "";

        /// <summary>
        /// 是否为批量生图操作（系统操作，不可删除）
        /// </summary>
        [JsonIgnore]
        public bool IsBatchImage => string.Equals(ActionType, ActionTypes.BatchImage, StringComparison.Ordinal);

        /// <summary>
        /// 创建默认的批量生图操作。提示词带"生成图片"的引导，否则平台可能只回复文字描述
        /// </summary>
        public static ActionItem CreateBatchImageAction(bool isEn, int sortOrder)
        {
            return new ActionItem
            {
                Name = isEn ? "Batch Image" : "批量生图",
                Prompt = isEn
                    ? "Generate an image based on the following description:\n\n{content}"
                    : "请根据以下描述生成一张图片：\n\n{content}",
                HotkeyModifiers = "",
                HotkeyKey = "",
                IsBuiltIn = true,
                SortOrder = sortOrder,
                Icon = "🖼️",
                ActionType = ActionTypes.BatchImage
            };
        }
    }
}
