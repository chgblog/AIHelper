// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Collections.Generic;
using System.Linq;

namespace AIHelper.Models
{
    /// <summary>
    /// 自动访问配置项
    /// </summary>
    public class AutoVisitConfig
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 自动访问网址（主页面 URL）
        /// </summary>
        public string Url { get; set; } = "";

        /// <summary>
        /// 自动访问链接正则匹配规则
        /// </summary>
        public string LinkMatchRegex { get; set; } = "";

        /// <summary>
        /// 单轮最大自动访问链接数（达到后停止）
        /// </summary>
        public int MaxVisitCount { get; set; } = 50;

        /// <summary>
        /// 下一页定位（CSS 选择器，留空则到底部停止）
        /// </summary>
        public string NextPageSelector { get; set; } = "";

        /// <summary>
        /// 停止后刷新最小间隔（分钟，默认 30）
        /// </summary>
        public int MinRefreshIntervalMinutes { get; set; } = 30;

        /// <summary>
        /// 停止后刷新最大间隔（分钟，默认 60）
        /// </summary>
        public int MaxRefreshIntervalMinutes { get; set; } = 60;

        /// <summary>
        /// 是否启用此自动访问规则
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 访问每个链接完成（滚动到底部）访问下一个链接的最小延迟（秒，默认 3）
        /// </summary>
        public int MinLinkDelaySeconds { get; set; } = 3;

        /// <summary>
        /// 访问每个链接完成（滚动到底部）访问下一个链接的最大延迟（秒，默认 8）
        /// </summary>
        public int MaxLinkDelaySeconds { get; set; } = 8;

        /// <summary>
        /// 用于界面列表显示的刷新间隔文本
        /// </summary>
        public string RefreshIntervalDisplay => $"{MinRefreshIntervalMinutes} ~ {MaxRefreshIntervalMinutes}";

        /// <summary>
        /// 用于界面列表显示的下一链接延迟文本
        /// </summary>
        public string LinkDelayDisplay => $"{MinLinkDelaySeconds} ~ {MaxLinkDelaySeconds}";

        /// <summary>
        /// 排除访问的链接（支持设置多个链接地址，每行一个或逗号/分号分隔；支持精确匹配、前缀匹配及通配符匹配）
        /// </summary>
        public string ExcludedUrls { get; set; } = "";

        /// <summary>
        /// 将排除链接配置文本解析为去重后的链接列表
        /// </summary>
        public static List<string> ParseExcludedUrls(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
            return raw.Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(s => s.Trim())
                      .Where(s => !string.IsNullOrEmpty(s))
                      .Distinct(StringComparer.OrdinalIgnoreCase)
                      .ToList();
        }

        public AutoVisitConfig Clone()
        {
            return new AutoVisitConfig
            {
                Id = this.Id,
                Url = this.Url,
                LinkMatchRegex = this.LinkMatchRegex,
                MaxVisitCount = this.MaxVisitCount,
                NextPageSelector = this.NextPageSelector,
                MinRefreshIntervalMinutes = this.MinRefreshIntervalMinutes,
                MaxRefreshIntervalMinutes = this.MaxRefreshIntervalMinutes,
                MinLinkDelaySeconds = this.MinLinkDelaySeconds,
                MaxLinkDelaySeconds = this.MaxLinkDelaySeconds,
                IsEnabled = this.IsEnabled,
                ExcludedUrls = this.ExcludedUrls
            };
        }
    }
}
