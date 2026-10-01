// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
namespace AIHelper.Models
{
    /// <summary>
    /// 页面预设中的一步：录制时用户在 AI 页面上的一次点击或表单修改。
    /// 批量生图每次新建对话后按顺序重放，用来选择模型、比例等生图选项。
    /// </summary>
    public class PageSetupStep
    {
        public const string KindClick = "click";
        public const string KindSelect = "select";
        public const string KindCheck = "check";
        public const string KindInput = "input";

        public const string StateOn = "on";
        public const string StateOff = "off";

        /// <summary>
        /// click / select / check / input
        /// </summary>
        public string Kind { get; set; } = KindClick;

        /// <summary>
        /// 录制时生成的 CSS 选择器。可能匹配多个元素（例如同一组比例按钮），由 Text 区分
        /// </summary>
        public string Selector { get; set; }

        public string Tag { get; set; }

        /// <summary>
        /// 点击：元素文字；选择/勾选/填写：表单控件的标签。用于定位元素和显示
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// 点击后元素的状态：on / off / 空（无法判断）。重放时元素已处于该状态就不再点击，
        /// 避免把开关点回去、把已展开的下拉框收起
        /// </summary>
        public string State { get; set; }

        /// <summary>
        /// select / input 的值；check 为 "true" / "false"
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// select 选中项的文字，仅用于显示
        /// </summary>
        public string ValueText { get; set; }
    }
}
