using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AIHelper.Models
{
    /// <summary>
    /// Represents an AI platform configuration
    /// </summary>
    public class AiPlatform : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _name;
        private string _url;
        private bool _isActive;
        private string _newChatSelector;
        private string _inputSelector;
        private string _submitSelector;
        private bool _useProxy = true;
        private List<PageSetupStep> _batchSetupSteps = new List<PageSetupStep>();
        private bool _batchOpenLargeImage;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string Id
        {
            get => _id;
            set { if (_id != value) { _id = value; OnPropertyChanged(); } }
        }

        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; OnPropertyChanged(); } }
        }

        public string Url
        {
            get => _url;
            set { if (_url != value) { _url = value; OnPropertyChanged(); } }
        }

        public bool IsActive
        {
            get => _isActive;
            set { if (_isActive != value) { _isActive = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Custom CSS selector for the new chat button
        /// </summary>
        public string NewChatSelector
        {
            get => _newChatSelector;
            set { if (_newChatSelector != value) { _newChatSelector = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Custom CSS selector for the input element (textarea or contenteditable)
        /// </summary>
        public string InputSelector
        {
            get => _inputSelector;
            set { if (_inputSelector != value) { _inputSelector = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Custom CSS selector for the submit button
        /// </summary>
        public string SubmitSelector
        {
            get => _submitSelector;
            set { if (_submitSelector != value) { _submitSelector = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Whether to use proxy for this platform
        /// </summary>
        public bool UseProxy
        {
            get => _useProxy;
            set { if (_useProxy != value) { _useProxy = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// 批量生图页面预设：每次新建对话后按顺序重放的点击/表单操作（选择模型、比例等）
        /// </summary>
        public List<PageSetupStep> BatchSetupSteps
        {
            get => _batchSetupSteps;
            set { _batchSetupSteps = value ?? new List<PageSetupStep>(); OnPropertyChanged(); }
        }

        /// <summary>
        /// 批量生图保存前先点击图片打开大图（对话里只显示缩略图的网站）
        /// </summary>
        public bool BatchOpenLargeImage
        {
            get => _batchOpenLargeImage;
            set { if (_batchOpenLargeImage != value) { _batchOpenLargeImage = value; OnPropertyChanged(); } }
        }
    }
}
