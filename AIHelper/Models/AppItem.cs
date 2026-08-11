using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace AIHelper.Models
{
    /// <summary>
    /// 运行中应用程序的数据模型
    /// </summary>
    public class AppItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        /// <summary>
        /// 应用图标
        /// </summary>
        public ImageSource Icon { get; set; }

        /// <summary>
        /// 应用显示名称（例如: Google Chrome, 记事本, Visual Studio Code）
        /// </summary>
        public string AppName { get; set; }

        /// <summary>
        /// 进程可执行文件名（例如: chrome.exe, notepad.exe）
        /// </summary>
        public string ProcessName { get; set; }

        /// <summary>
        /// 主窗口标题（如果有）
        /// </summary>
        public string MainWindowTitle { get; set; }

        /// <summary>
        /// 可执行文件绝对路径
        /// </summary>
        public string ExecutablePath { get; set; }

        /// <summary>
        /// 是否包含可见主窗口
        /// </summary>
        public bool HasWindow { get; set; }

        /// <summary>
        /// 关联的进程 ID
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// 是否在 UI 中被选中
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
