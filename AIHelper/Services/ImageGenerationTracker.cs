// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Collections.Generic;
using System.Linq;

namespace AIHelper.Services
{
    public enum ImageWaitOutcome
    {
        Waiting,
        Completed,
        NoImage
    }

    /// <summary>
    /// 根据页面探测结果判断"生图是否完成"。时间判断全部在宿主侧做：窗口隐藏时页面里的
    /// 计时器会被节流，页面内循环等待不可靠，所以页面只负责报告当前状态。
    /// </summary>
    public class ImageGenerationTracker
    {
        private readonly DateTime _start;
        private DateTime _lastImageActivity;
        private DateTime _lastAnyActivity;
        private DateTime _lastSignatureChange;
        private string _lastSignature;
        private int _lastTextLength = -1;
        private int _lastPending;

        /// <summary>
        /// 检测到过"生成中"状态时，新图需要保持不变的时长
        /// </summary>
        public TimeSpan StableDuration { get; set; } = TimeSpan.FromSeconds(3);

        /// <summary>
        /// 从未检测到"生成中"状态（停止按钮识别不到）时，用更长的稳定时长兜底
        /// </summary>
        public TimeSpan UnconfirmedStableDuration { get; set; } = TimeSpan.FromSeconds(8);

        /// <summary>
        /// 判定"回复结束但没有图"前，页面需要安静的时长
        /// </summary>
        public TimeSpan NoImageQuietDuration { get; set; } = TimeSpan.FromSeconds(8);

        /// <summary>
        /// 从未检测到"生成中"状态时，至少等待这么久才判定没有图。识别不到停止按钮的平台上，
        /// "回复结束但没图"和"图还在渲染"从页面上看不出区别，所以批量生图会把它设成用户配置的单张超时。
        /// </summary>
        public TimeSpan NoSignalGrace { get; set; } = TimeSpan.FromSeconds(20);

        public bool SeenGenerating { get; private set; }
        public List<ImageProbeImage> Images { get; private set; } = new List<ImageProbeImage>();
        public string Snippet { get; private set; } = "";

        public ImageGenerationTracker(DateTime start)
        {
            _start = start;
            _lastImageActivity = start;
            _lastAnyActivity = start;
            _lastSignatureChange = start;
        }

        /// <summary>
        /// 喂入一次探测结果。<paramref name="probe"/> 为 null 表示这次没探测到（页面正在切换），视为仍在等待。
        /// </summary>
        public ImageWaitOutcome Update(ImageProbeResult probe, DateTime now)
        {
            if (probe == null) return ImageWaitOutcome.Waiting;

            var images = probe.images ?? new List<ImageProbeImage>();
            string signature = string.Join("|", images.Select(i => $"{i.src}#{i.w}x{i.h}")) + "|p" + probe.pending;

            if (probe.generating)
            {
                SeenGenerating = true;
                _lastImageActivity = now;
                _lastAnyActivity = now;
            }
            if (signature != _lastSignature)
            {
                _lastSignature = signature;
                _lastSignatureChange = now;
                _lastImageActivity = now;
                _lastAnyActivity = now;
            }
            if (probe.textLength != _lastTextLength)
            {
                _lastTextLength = probe.textLength;
                _lastAnyActivity = now;
            }

            Images = images;
            Snippet = probe.snippet ?? "";
            _lastPending = probe.pending;

            if (probe.generating || probe.pending > 0) return ImageWaitOutcome.Waiting;

            if (images.Count > 0)
            {
                var required = SeenGenerating ? StableDuration : UnconfirmedStableDuration;
                return now - _lastImageActivity >= required ? ImageWaitOutcome.Completed : ImageWaitOutcome.Waiting;
            }

            if (!SeenGenerating && now - _start < NoSignalGrace) return ImageWaitOutcome.Waiting;
            return now - _lastAnyActivity >= NoImageQuietDuration ? ImageWaitOutcome.NoImage : ImageWaitOutcome.Waiting;
        }

        /// <summary>
        /// 超时兜底：已有图片且已长时间不变（停止按钮误判一直"生成中"时）也可视为完成。
        /// 这里只看图片本身是否变化，不看"生成中"状态。
        /// </summary>
        public bool HasStableImages(DateTime now, TimeSpan minStable)
        {
            return Images.Count > 0 && _lastPending == 0 && now - _lastSignatureChange >= minStable;
        }
    }
}
