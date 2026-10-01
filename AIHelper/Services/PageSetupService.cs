// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Collections.Generic;
using AIHelper.Models;

namespace AIHelper.Services
{
    /// <summary>
    /// 页面预设（批量生图前重放的点击/表单操作）的纯逻辑部分：合并录制结果、生成显示文字
    /// </summary>
    public static class PageSetupService
    {
        /// <summary>
        /// 一个预设最多保留的步数，防止录制时误操作产生一长串步骤
        /// </summary>
        public const int MaxSteps = 30;

        private const int MaxLabelLength = 40;

        /// <summary>
        /// 把页面上新录制到的步骤追加到 <paramref name="steps"/>，并合并重复操作：
        /// 同一个表单控件改了多次只保留最后一次；连续点击同一个能判断状态的元素
        /// （开关、下拉框）只保留最后一次，以最终状态为准。
        /// </summary>
        public static void Append(IList<PageSetupStep> steps, IEnumerable<PageSetupStep> incoming)
        {
            if (steps == null || incoming == null) return;

            foreach (var raw in incoming)
            {
                var step = Normalize(raw);
                if (step == null) continue;

                if (step.Kind != PageSetupStep.KindClick)
                {
                    for (int i = steps.Count - 1; i >= 0; i--)
                    {
                        if (SameTarget(steps[i], step)) steps.RemoveAt(i);
                    }
                }
                else if (steps.Count > 0)
                {
                    var last = steps[steps.Count - 1];
                    // 状态未知的点击不能合并：两次点击和一次点击的结果不同
                    if (SameTarget(last, step) && !string.IsNullOrEmpty(last.State) && !string.IsNullOrEmpty(step.State))
                    {
                        steps.RemoveAt(steps.Count - 1);
                    }
                }

                if (steps.Count >= MaxSteps) continue;
                steps.Add(step);
            }
        }

        /// <summary>
        /// 一步操作的显示文字，例如：点击「16:9」
        /// </summary>
        public static string Describe(PageSetupStep step)
        {
            var lm = LanguageManager.Instance;
            if (step == null) return "";

            string label = Shorten(!string.IsNullOrWhiteSpace(step.Text) ? step.Text : step.Selector);
            switch (step.Kind)
            {
                case PageSetupStep.KindSelect:
                    return lm.GetString("Batch_Preset_Step_Select", label, Shorten(string.IsNullOrEmpty(step.ValueText) ? step.Value : step.ValueText));
                case PageSetupStep.KindCheck:
                    return lm.GetString(step.Value == "true" ? "Batch_Preset_Step_Check" : "Batch_Preset_Step_Uncheck", label);
                case PageSetupStep.KindInput:
                    return lm.GetString("Batch_Preset_Step_Input", label, Shorten(step.Value));
                default:
                    return lm.GetString("Batch_Preset_Step_Click", label);
            }
        }

        private static PageSetupStep Normalize(PageSetupStep step)
        {
            if (step == null) return null;
            if (string.IsNullOrWhiteSpace(step.Selector) && string.IsNullOrWhiteSpace(step.Text)) return null;

            string kind = (step.Kind ?? "").Trim().ToLowerInvariant();
            if (kind != PageSetupStep.KindSelect && kind != PageSetupStep.KindCheck && kind != PageSetupStep.KindInput)
            {
                kind = PageSetupStep.KindClick;
            }

            string state = (step.State ?? "").Trim().ToLowerInvariant();
            if (state != PageSetupStep.StateOn && state != PageSetupStep.StateOff) state = "";

            return new PageSetupStep
            {
                Kind = kind,
                Selector = step.Selector?.Trim() ?? "",
                Tag = step.Tag?.Trim() ?? "",
                Text = step.Text?.Trim() ?? "",
                State = state,
                Value = step.Value ?? "",
                ValueText = step.ValueText ?? ""
            };
        }

        private static bool SameTarget(PageSetupStep a, PageSetupStep b)
        {
            return a != null && b != null &&
                   string.Equals(a.Kind, b.Kind, StringComparison.Ordinal) &&
                   string.Equals(a.Selector ?? "", b.Selector ?? "", StringComparison.Ordinal) &&
                   string.Equals(a.Text ?? "", b.Text ?? "", StringComparison.Ordinal);
        }

        private static string Shorten(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            value = value.Trim();
            return value.Length <= MaxLabelLength ? value : value.Substring(0, MaxLabelLength) + "…";
        }
    }
}
