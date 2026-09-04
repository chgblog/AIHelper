// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Shapes;
using AIHelper.Models;
using AIHelper.Services;

namespace AIHelper.Views
{
    public partial class SelectionToolbarWindow : Window
    {
        private string _selectedText;
        private string _currentFilePath;
        private bool _isForFile;
        private List<ActionItem> _actions;
        private DispatcherTimer _autoHideTimer;
        private Point _currentScreenPos;
        private bool _isExpanded;
        private int _autoHideSeconds = 3;
        private int _copyMode = 0;
        private const int MaxInitialActions = 5;

        public event Action<ActionItem, string> ActionRequested;
        public event Action<ActionItem, string> FileActionRequested;

        public SelectionToolbarWindow()
        {
            InitializeComponent();

            new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle();

            _autoHideTimer = new DispatcherTimer();
            _autoHideTimer.Interval = TimeSpan.FromSeconds(_autoHideSeconds);
            _autoHideTimer.Tick += (s, e) =>
            {
                try
                {
                    HideToolbar();
                }
                catch (Exception ex)
                {
                    Logger.LogError("SelectionToolbarWindow: Auto hide failed.", ex);
                }
            };
        }

        /// <summary>
        /// 显示划词工具条
        /// </summary>
        /// <param name="text">选中的文字</param>
        /// <param name="screenPos">鼠标屏幕坐标</param>
        /// <param name="actions">可用操作列表</param>
        /// <param name="autoHideSeconds">自动消失秒数</param>
        /// <param name="copyMode">复制按钮模式 (0: 不开启, 1: 开启且在第一个, 2: 开启且在最后一个)</param>
        public void ShowAt(string text, System.Windows.Point screenPos, List<ActionItem> actions, int autoHideSeconds = 3, int copyMode = 0)
        {
            try
            {
                _isForFile = false;
                _currentFilePath = null;
                _selectedText = text;
                _autoHideSeconds = autoHideSeconds > 0 ? autoHideSeconds : 3;
                _actions = actions?.Where(a => a != null).OrderBy(a => a.SortOrder).ToList();
                _currentScreenPos = screenPos;
                _isExpanded = false;
                _copyMode = copyMode;

                BuildButtons();
                PositionWindow(_currentScreenPos);

                this.Show();
                _autoHideTimer.Interval = TimeSpan.FromSeconds(_autoHideSeconds);
                StartAutoHideTimer();
                PlayShowAnimation();
            }
            catch (Exception ex)
            {
                Logger.LogError("SelectionToolbarWindow: ShowAt failed.", ex);
            }
        }

        /// <summary>
        /// 显示文件快捷处理工具条
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="screenPos">鼠标屏幕坐标</param>
        /// <param name="actions">可用操作列表</param>
        /// <param name="autoHideSeconds">自动消失秒数</param>
        /// <param name="copyMode">复制按钮模式 (0: 不开启, 1: 开启且在第一个, 2: 开启且在最后一个)</param>
        /// <param name="isExpanded">是否直接展开显示全部操作</param>
        public void ShowForFile(string filePath, System.Windows.Point screenPos, List<ActionItem> actions, int autoHideSeconds = 3, int copyMode = 0, bool isExpanded = false)
        {
            try
            {
                _isForFile = true;
                _currentFilePath = filePath;
                _selectedText = null;
                _autoHideSeconds = autoHideSeconds > 0 ? autoHideSeconds : 3;
                _actions = actions?.Where(a => a != null).OrderBy(a => a.SortOrder).ToList();
                _currentScreenPos = screenPos;
                _isExpanded = isExpanded;
                _copyMode = copyMode;

                BuildButtons();
                PositionWindow(_currentScreenPos);

                this.Show();
                _autoHideTimer.Interval = TimeSpan.FromSeconds(_autoHideSeconds);
                StartAutoHideTimer();
                PlayShowAnimation();
            }
            catch (Exception ex)
            {
                Logger.LogError("SelectionToolbarWindow: ShowForFile failed.", ex);
            }
        }

        /// <summary>
        /// 隐藏工具条
        /// </summary>
        public void HideToolbar()
        {
            if (this.Visibility != Visibility.Visible) return;
            StopAutoHideTimer();
            PlayHideAnimation(() => this.Hide());
        }

        private void Window_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            HideToolbar();
            e.Handled = true;
        }

        private void BuildButtons()
        {
            buttonPanel.Children.Clear();
            bool hasActions = _actions != null && _actions.Count > 0;
            if (!hasActions && _copyMode == 0) return;

            bool isFirst = true;

            void AddSeparator()
            {
                if (!isFirst)
                {
                    var sep = new Rectangle { Style = (Style)FindResource("SeparatorStyle") };
                    buttonPanel.Children.Add(sep);
                }
            }

            Button CreateCopyButton()
            {
                string copyText = LanguageManager.Instance["SelectionToolbar_Copy"];
                if (string.IsNullOrEmpty(copyText)) copyText = "复制";

                var btn = new Button
                {
                    Content = "📋 " + copyText,
                    Style = (Style)FindResource("ToolbarButtonStyle")
                };

                btn.Click += (s, e) => {
                    try
                    {
                        if (_isForFile)
                        {
                            ClipboardService.SetFileDropList(new[] { _currentFilePath });
                        }
                        else
                        {
                            ClipboardService.SetText(_selectedText);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("SelectionToolbarWindow: Copy failed.", ex);
                    }
                    HideToolbar();
                };

                return btn;
            }

            // 1. Copy at first position
            if (_copyMode == 1)
            {
                buttonPanel.Children.Add(CreateCopyButton());
                isFirst = false;
            }

            // 2. Action buttons
            if (hasActions)
            {
                bool hasMore = _actions.Count > MaxInitialActions && !_isExpanded;
                var displayActions = hasMore ? _actions.Take(MaxInitialActions) : _actions;

                foreach (var action in displayActions)
                {
                    AddSeparator();

                    var btn = new Button
                    {
                        Content = (string.IsNullOrEmpty(action.Icon) ? "" : action.Icon + " ") + action.Name,
                        Tag = action,
                        Style = (Style)FindResource("ToolbarButtonStyle")
                    };

                    btn.Click += (s, e) => {
                        try
                        {
                            if (_isForFile)
                            {
                                FileActionRequested?.Invoke(action, _currentFilePath);
                            }
                            else
                            {
                                ActionRequested?.Invoke(action, _selectedText);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"SelectionToolbarWindow: Action execution failed (action={action?.Name}, isForFile={_isForFile}).", ex);
                        }
                        HideToolbar();
                    };

                    buttonPanel.Children.Add(btn);
                    isFirst = false;
                }

                if (hasMore)
                {
                    AddSeparator();

                    string moreText = LanguageManager.Instance["SelectionToolbar_More"];
                    if (string.IsNullOrEmpty(moreText)) moreText = "更多 ▾";

                    var moreBtn = new Button
                    {
                        Content = moreText,
                        Style = (Style)FindResource("ToolbarButtonStyle")
                    };

                    moreBtn.Click += (s, e) => {
                        try
                        {
                            _isExpanded = true;
                            BuildButtons();
                            PositionWindow(_currentScreenPos);
                            _autoHideTimer.Interval = TimeSpan.FromSeconds(_autoHideSeconds);
                            StartAutoHideTimer();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("SelectionToolbarWindow: Expand failed.", ex);
                        }
                    };

                    buttonPanel.Children.Add(moreBtn);
                    isFirst = false;
                }
            }

            // 3. Copy at last position
            if (_copyMode == 2)
            {
                AddSeparator();
                buttonPanel.Children.Add(CreateCopyButton());
                isFirst = false;
            }
        }

        private void PositionWindow(System.Windows.Point screenPos)
        {
            // Measure actual size before showing
            this.UpdateLayout();

            // Handle DPI scaling
            double dpiScaleX = 1.0;
            double dpiScaleY = 1.0;

            var source = PresentationSource.FromVisual(this);
            if (source != null && source.CompositionTarget != null)
            {
                dpiScaleX = source.CompositionTarget.TransformFromDevice.M11;
                dpiScaleY = source.CompositionTarget.TransformFromDevice.M22;
            }

            // Center horizontally above the cursor position
            double x = (screenPos.X * dpiScaleX) - (this.ActualWidth / 2.0);
            double y = (screenPos.Y * dpiScaleY) - this.ActualHeight - 10;

            // 获取当前鼠标所在的屏幕工作区（支持多显示器）
            var currentScreen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)screenPos.X, (int)screenPos.Y));
            var workArea = currentScreen != null ? currentScreen.WorkingArea : System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;

            double workAreaLeft = workArea.Left * dpiScaleX;
            double workAreaTop = workArea.Top * dpiScaleY;
            double workAreaRight = workArea.Right * dpiScaleX;

            // Bounds adjustment
            if (y < workAreaTop) y = (screenPos.Y * dpiScaleY) + 20;
            if (x + this.ActualWidth > workAreaRight) x = workAreaRight - this.ActualWidth;
            if (x < workAreaLeft) x = workAreaLeft;

            // 对齐到物理设备像素，消除亚像素定位导致的整窗模糊
            if (dpiScaleX > 0 && dpiScaleY > 0)
            {
                x = Math.Round(x / dpiScaleX) * dpiScaleX;
                y = Math.Round(y / dpiScaleY) * dpiScaleY;
            }

            this.Left = x;
            this.Top = y;
        }

        private void PlayShowAnimation()
        {
            var sb = new Storyboard();

            var opacityAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(opacityAnim, this);
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(Window.OpacityProperty));

            var scaleXAnim = new DoubleAnimation(0.85, 1.0, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(scaleXAnim, WindowScale);
            Storyboard.SetTargetProperty(scaleXAnim, new PropertyPath(ScaleTransform.ScaleXProperty));

            var scaleYAnim = new DoubleAnimation(0.85, 1.0, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(scaleYAnim, WindowScale);
            Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath(ScaleTransform.ScaleYProperty));

            sb.Children.Add(opacityAnim);
            sb.Children.Add(scaleXAnim);
            sb.Children.Add(scaleYAnim);

            sb.Completed += (s, e) =>
            {
                // 动画完成后剥离动画时钟并赋予静态值，使 WPF 脱离过渡渲染并稳定在整数像素栅格
                WindowScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                WindowScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                WindowScale.ScaleX = 1.0;
                WindowScale.ScaleY = 1.0;
            };

            sb.Begin();
        }

        private void PlayHideAnimation(Action onCompleted)
        {
            var opacityAnim = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(100));
            opacityAnim.Completed += (s, e) => onCompleted?.Invoke();
            this.BeginAnimation(Window.OpacityProperty, opacityAnim);
        }

        private void StartAutoHideTimer()
        {
            _autoHideTimer.Stop();
            _autoHideTimer.Start();
        }

        private void StopAutoHideTimer()
        {
            _autoHideTimer.Stop();
        }

        private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            StopAutoHideTimer();
        }

        private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _autoHideTimer.Interval = TimeSpan.FromSeconds(_autoHideSeconds);
            StartAutoHideTimer();
        }
    }
}

