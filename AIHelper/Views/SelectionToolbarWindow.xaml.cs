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
        private const int MaxActionsPerRowInMore = 6;

        private readonly IntPtr _hwnd;
        private volatile bool _isToolbarVisible;
        private bool _isHiding;
        private int _hideAnimationId;

        public event Action<ActionItem, string> ActionRequested;
        public event Action<ActionItem, string> FileActionRequested;

        public SelectionToolbarWindow()
        {
            InitializeComponent();

            _hwnd = new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle();

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
        /// 计算工具条在不同状态下的布局结构（行与各按钮名称），供自动化测试与逻辑校验
        /// </summary>
        internal static List<List<string>> ComputeButtonLayout(List<ActionItem> actions, bool isExpanded, int copyMode)
        {
            var result = new List<List<string>>();
            var sortedActions = actions?.Where(a => a != null).OrderBy(a => a.SortOrder).ToList() ?? new List<ActionItem>();
            bool hasActions = sortedActions.Count > 0;

            if (!hasActions && (copyMode == 0 || isExpanded))
            {
                return result;
            }

            if (isExpanded)
            {
                var row = new List<string> { "◀ 返回" };
                var remaining = sortedActions.Skip(MaxInitialActions).ToList();
                int firstRowCount = Math.Min(remaining.Count, MaxActionsPerRowInMore);
                for (int i = 0; i < firstRowCount; i++)
                {
                    row.Add(remaining[i].Name);
                }
                result.Add(row);

                if (remaining.Count > MaxActionsPerRowInMore)
                {
                    for (int i = MaxActionsPerRowInMore; i < remaining.Count; i += MaxActionsPerRowInMore)
                    {
                        var nextRow = new List<string>();
                        int count = Math.Min(MaxActionsPerRowInMore, remaining.Count - i);
                        for (int j = 0; j < count; j++)
                        {
                            nextRow.Add(remaining[i + j].Name);
                        }
                        result.Add(nextRow);
                    }
                }
            }
            else
            {
                var row = new List<string>();
                if (copyMode == 1)
                {
                    row.Add("📋 复制");
                }

                if (hasActions)
                {
                    bool hasMore = sortedActions.Count > MaxInitialActions;
                    var display = hasMore ? sortedActions.Take(MaxInitialActions) : sortedActions;
                    foreach (var a in display)
                    {
                        row.Add(a.Name);
                    }
                    if (hasMore)
                    {
                        row.Add("更多 ▾");
                    }
                }

                if (copyMode == 2)
                {
                    row.Add("📋 复制");
                }

                result.Add(row);
            }

            return result;
        }

        internal int RowCountForTesting => buttonPanel?.Children.Count ?? 0;
        internal bool IsExpandedForTesting => _isExpanded;

        internal List<List<string>> GetCurrentVisualButtonLayoutForTesting()
        {
            var result = new List<List<string>>();
            if (buttonPanel == null) return result;

            foreach (UIElement child in buttonPanel.Children)
            {
                if (child is StackPanel rowPanel)
                {
                    var rowTexts = new List<string>();
                    foreach (UIElement rowChild in rowPanel.Children)
                    {
                        if (rowChild is Button btn && btn.Content is string text)
                        {
                            rowTexts.Add(text);
                        }
                    }
                    result.Add(rowTexts);
                }
            }
            return result;
        }

        internal void TriggerMoreClickForTesting()
        {
            _isExpanded = true;
            BuildButtons();
        }

        internal void TriggerBackClickForTesting()
        {
            _isExpanded = false;
            BuildButtons();
        }

        internal void SetupForTesting(List<ActionItem> actions, int copyMode = 0, bool isExpanded = false)
        {
            _actions = actions?.Where(a => a != null).OrderBy(a => a.SortOrder).ToList();
            _copyMode = copyMode;
            _isExpanded = isExpanded;
            BuildButtons();
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
                _isHiding = false;
                ++_hideAnimationId;
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

                this.BeginAnimation(Window.OpacityProperty, null);
                this.Show();
                _isToolbarVisible = true;
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
                _isHiding = false;
                ++_hideAnimationId;
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

                this.BeginAnimation(Window.OpacityProperty, null);
                this.Show();
                _isToolbarVisible = true;
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
            if (this.Visibility != Visibility.Visible || _isHiding) return;
            _isHiding = true;
            _isToolbarVisible = false;
            StopAutoHideTimer();
            int currentHideId = ++_hideAnimationId;
            PlayHideAnimation(() =>
            {
                if (_hideAnimationId == currentHideId)
                {
                    this.Hide();
                    _isHiding = false;
                }
            });
        }

        /// <summary>
        /// 获取工具条当前是否处于显示状态（非隐藏且非正在退出隐藏中）
        /// </summary>
        public bool IsToolbarVisible => _isToolbarVisible && !_isHiding;

        /// <summary>
        /// 判断屏幕物理坐标点是否在指定矩形范围内
        /// </summary>
        internal static bool IsPointInRect(Point screenPoint, Win32Api.RECT rect)
        {
            return screenPoint.X >= rect.Left && screenPoint.X <= rect.Right &&
                   screenPoint.Y >= rect.Top && screenPoint.Y <= rect.Bottom;
        }

        /// <summary>
        /// 判断屏幕物理坐标点是否在工具条窗口区域内
        /// </summary>
        public bool IsPointInside(Point screenPoint)
        {
            if (!IsToolbarVisible || _hwnd == IntPtr.Zero) return false;

            try
            {
                if (Win32Api.GetWindowRect(_hwnd, out Win32Api.RECT rect))
                {
                    return IsPointInRect(screenPoint, rect);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("SelectionToolbarWindow: IsPointInside check failed.", ex);
            }

            return false;
        }

        protected override void OnClosed(EventArgs e)
        {
            _isToolbarVisible = false;
            base.OnClosed(e);
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
            if (!hasActions && (_copyMode == 0 || _isExpanded)) return;

            StackPanel currentRow = null;
            bool isFirstInRow = true;

            StackPanel CreateRow()
            {
                var row = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    SnapsToDevicePixels = true,
                    UseLayoutRounding = true
                };
                if (buttonPanel.Children.Count > 0)
                {
                    row.Margin = new Thickness(0, 3, 0, 0);
                }
                buttonPanel.Children.Add(row);
                isFirstInRow = true;
                return row;
            }

            void AddButtonToRow(StackPanel row, Button btn)
            {
                if (!isFirstInRow)
                {
                    var sep = new Rectangle { Style = (Style)FindResource("SeparatorStyle") };
                    row.Children.Add(sep);
                }
                row.Children.Add(btn);
                isFirstInRow = false;
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

            Button CreateActionButton(ActionItem action)
            {
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

                return btn;
            }

            Button CreateMoreButton()
            {
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

                return moreBtn;
            }

            Button CreateBackButton()
            {
                string backText = LanguageManager.Instance["SelectionToolbar_Back"];
                if (string.IsNullOrEmpty(backText)) backText = "返回";
                string backContent = backText.StartsWith("◀") || backText.StartsWith("◂") || backText.StartsWith("←")
                    ? backText
                    : "◀ " + backText;

                var backBtn = new Button
                {
                    Content = backContent,
                    Style = (Style)FindResource("ToolbarButtonStyle")
                };

                backBtn.Click += (s, e) => {
                    try
                    {
                        _isExpanded = false;
                        BuildButtons();
                        PositionWindow(_currentScreenPos);
                        _autoHideTimer.Interval = TimeSpan.FromSeconds(_autoHideSeconds);
                        StartAutoHideTimer();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("SelectionToolbarWindow: Back to default failed.", ex);
                    }
                };

                return backBtn;
            }

            if (_isExpanded)
            {
                // 点击更多后：排除复制和前5项操作，第一项加返回，点击显示默认操作，剩余项操作超过6项显示多行
                var remainingActions = hasActions ? _actions.Skip(MaxInitialActions).ToList() : new List<ActionItem>();

                // 第一项加返回
                currentRow = CreateRow();
                AddButtonToRow(currentRow, CreateBackButton());

                if (remainingActions.Count > 0)
                {
                    bool isMultiLine = remainingActions.Count > MaxActionsPerRowInMore;

                    // 第一行最多放置 6 个剩余操作项
                    int firstRowActionCount = Math.Min(remainingActions.Count, MaxActionsPerRowInMore);
                    for (int i = 0; i < firstRowActionCount; i++)
                    {
                        AddButtonToRow(currentRow, CreateActionButton(remainingActions[i]));
                    }

                    // 剩余项操作超过6项显示多行：后续操作按每行最多 6 项换行显示
                    if (isMultiLine)
                    {
                        for (int i = MaxActionsPerRowInMore; i < remainingActions.Count; i += MaxActionsPerRowInMore)
                        {
                            currentRow = CreateRow();
                            int countInThisRow = Math.Min(MaxActionsPerRowInMore, remainingActions.Count - i);
                            for (int j = 0; j < countInThisRow; j++)
                            {
                                AddButtonToRow(currentRow, CreateActionButton(remainingActions[i + j]));
                            }
                        }
                    }
                }
            }
            else
            {
                currentRow = CreateRow();

                // 1. Copy at first position
                if (_copyMode == 1)
                {
                    AddButtonToRow(currentRow, CreateCopyButton());
                }

                // 2. Action buttons
                if (hasActions)
                {
                    bool hasMore = _actions.Count > MaxInitialActions;
                    var displayActions = hasMore ? _actions.Take(MaxInitialActions) : _actions;

                    foreach (var action in displayActions)
                    {
                        AddButtonToRow(currentRow, CreateActionButton(action));
                    }

                    if (hasMore)
                    {
                        AddButtonToRow(currentRow, CreateMoreButton());
                    }
                }

                // 3. Copy at last position
                if (_copyMode == 2)
                {
                    AddButtonToRow(currentRow, CreateCopyButton());
                }
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

