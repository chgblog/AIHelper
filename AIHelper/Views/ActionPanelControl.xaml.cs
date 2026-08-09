using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AIHelper.Models;

namespace AIHelper.Views
{
    public partial class ActionPanelControl : UserControl
    {
        private List<ActionItem> _actionItems;
        private ActionItem _selectedAction;

        public event Action<ActionItem> ActionClicked;
        public event Action<ActionItem, string> ActionSubmitted;

        public List<ActionItem> ActionItems 
        { 
            get => _actionItems;
            set 
            {
                _actionItems = value;
                LoadActions(_actionItems);
            }
        }

        public ActionPanelControl()
        {
            InitializeComponent();
        }

        public void SetContent(string text)
        {
            txtInput.Text = text;
        }

        public string GetContent()
        {
            return txtInput.Text;
        }

        public void LoadActions(List<ActionItem> actions)
        {
            actionsWrapPanel.Children.Clear();
            if (actions == null) return;

            foreach (var action in actions)
            {
                var btn = new Button
                {
                    Content = action.Name,
                    Tag = action,
                    Style = (Style)FindResource("ActionButtonStyle")
                };
                
                btn.Click += ActionButton_Click;
                actionsWrapPanel.Children.Add(btn);
            }
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ActionItem action)
            {
                _selectedAction = action;
                
                // Highlight selected
                foreach (Button child in actionsWrapPanel.Children)
                {
                    if (child == btn)
                        child.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7c3aed"));
                    else
                        child.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3a3a5e"));
                }

                ActionClicked?.Invoke(action);
                ActionSubmitted?.Invoke(action, txtInput.Text);
            }
        }
    }
}
