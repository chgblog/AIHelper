using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Shapes;
using AIHelper.Models;

namespace AIHelper.Views
{
    public partial class SelectionToolbarWindow : Window
    {
        private string _selectedText;
        private List<ActionItem> _actions;
        private DispatcherTimer _autoHideTimer;

        public event Action<ActionItem, string> ActionRequested;

        public SelectionToolbarWindow()
        {
            InitializeComponent();

            _autoHideTimer = new DispatcherTimer();
            _autoHideTimer.Interval = TimeSpan.FromSeconds(5);
            _autoHideTimer.Tick += (s, e) => HideToolbar();
        }

        /// <summary>
        /// 显示工具条
        /// </summary>
        /// <param name="text">选中的文字</param>
        /// <param name="screenPos">鼠标屏幕坐标</param>
        /// <param name="actions">可用操作列表</param>
        public void ShowAt(string text, System.Windows.Point screenPos, List<ActionItem> actions)
        {
            _selectedText = text;
            _actions = actions;
            BuildButtons();
            
            PositionWindow(screenPos);
            
            this.Show();
            _autoHideTimer.Interval = TimeSpan.FromSeconds(5);
            StartAutoHideTimer();
            PlayShowAnimation();
        }

        /// <summary>
        /// 隐藏工具条
        /// </summary>
        public void HideToolbar()
        {
            StopAutoHideTimer();
            PlayHideAnimation(() => this.Hide());
        }

        private void BuildButtons()
        {
            buttonPanel.Children.Clear();
            bool isFirst = true;

            foreach (var action in _actions)
            {
                if (!isFirst)
                {
                    var sep = new Rectangle { Style = (Style)FindResource("SeparatorStyle") };
                    buttonPanel.Children.Add(sep);
                }

                var btn = new Button
                {
                    Content = (string.IsNullOrEmpty(action.Icon) ? "" : action.Icon + " ") + action.Name,
                    Tag = action,
                    Style = (Style)FindResource("ToolbarButtonStyle")
                };

                btn.Click += (s, e) => {
                    ActionRequested?.Invoke(action, _selectedText);
                    HideToolbar();
                };

                buttonPanel.Children.Add(btn);
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

            double x = screenPos.X * dpiScaleX;
            double y = screenPos.Y * dpiScaleY - this.ActualHeight - 10;

            var screenBounds = SystemParameters.WorkArea;

            // Bounds adjustment
            if (y < screenBounds.Top) y = screenPos.Y * dpiScaleY + 20;
            if (x + this.ActualWidth > screenBounds.Right) x = screenBounds.Right - this.ActualWidth;
            if (x < screenBounds.Left) x = screenBounds.Left;

            this.Left = x;
            this.Top = y;
        }

        private void PlayShowAnimation()
        {
            var sb = new Storyboard();

            var opacityAnim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(opacityAnim, this);
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(Window.OpacityProperty));

            var scaleXAnim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(scaleXAnim, WindowScale);
            Storyboard.SetTargetProperty(scaleXAnim, new PropertyPath(ScaleTransform.ScaleXProperty));

            var scaleYAnim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(scaleYAnim, WindowScale);
            Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath(ScaleTransform.ScaleYProperty));

            sb.Children.Add(opacityAnim);
            sb.Children.Add(scaleXAnim);
            sb.Children.Add(scaleYAnim);

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
            _autoHideTimer.Interval = TimeSpan.FromSeconds(3);
            StartAutoHideTimer();
        }
    }
}
