// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AIHelper.Services;

namespace AIHelper.Models
{
    public enum BatchImageItemStatus
    {
        Pending,
        Running,
        Success,
        Failed,
        Skipped,
        Invalid
    }

    /// <summary>
    /// 批量生图中的一行（对应 CSV 的一个数据行）
    /// </summary>
    public class BatchImageItem : INotifyPropertyChanged
    {
        private BatchImageItemStatus _status = BatchImageItemStatus.Pending;
        private string _message = "";
        private AiPlatform _platform;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 数据行序号（从 1 开始，不含表头）
        /// </summary>
        public int Index { get; set; }

        public string Prompt { get; set; } = "";

        /// <summary>
        /// CSV 中填写的原始文件名
        /// </summary>
        public string RawFileName { get; set; } = "";

        /// <summary>
        /// 清洗、去重后的文件名（不含扩展名，扩展名由图片数据决定）
        /// </summary>
        public string FileName { get; set; } = "";

        /// <summary>
        /// CSV 中填写的原始平台（名称或 Id，可为空）
        /// </summary>
        public string RawPlatform { get; set; } = "";

        /// <summary>
        /// 解析后的目标平台，解析失败时为 null
        /// </summary>
        public AiPlatform Platform
        {
            get => _platform;
            set
            {
                if (_platform != value)
                {
                    _platform = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PlatformName));
                }
            }
        }

        public string PlatformName => _platform?.Name ?? RawPlatform;

        public BatchImageItemStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public string StatusText => LanguageManager.Instance["Batch_ItemStatus_" + _status];

        /// <summary>
        /// 切换界面语言后刷新状态文字
        /// </summary>
        public void RefreshStatusText()
        {
            OnPropertyChanged(nameof(StatusText));
        }

        /// <summary>
        /// 结果说明：成功时为保存的文件，失败时为原因
        /// </summary>
        public string Message
        {
            get => _message;
            set
            {
                if (_message != value)
                {
                    _message = value ?? "";
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 已保存的文件（相对输出目录的路径，第一项为主图）
        /// </summary>
        public List<string> SavedFiles { get; set; } = new List<string>();

        public double ElapsedSeconds { get; set; }
    }
}
