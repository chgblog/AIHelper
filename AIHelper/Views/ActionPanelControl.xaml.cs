// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AIHelper.Models;
using AIHelper.Services;

namespace AIHelper.Views
{
    public partial class ActionPanelControl : UserControl
    {
        private List<ActionItem> _actionItems;
        private ActionItem _selectedAction;

        private ClipboardSnapshot _currentSnapshot;

        public ClipboardSnapshot CurrentSnapshot => _currentSnapshot;

        public event Action<ActionItem> ActionClicked;
        public event Action<ActionItem, string, ClipboardSnapshot> ActionSubmitted;
        public event Action CloseRequested;

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

        public void SetSnapshot(ClipboardSnapshot snapshot)
        {
            _currentSnapshot = snapshot;

            if (snapshot == null || snapshot.Kind == ClipboardContentKind.Empty)
            {
                ShowTextInput(string.Empty);
            }
            else if (snapshot.Kind == ClipboardContentKind.Text)
            {
                ShowTextInput(snapshot.Text);
            }
            else if (snapshot.Kind == ClipboardContentKind.Image)
            {
                ShowAttachment(ClipboardContentKind.Image, null);
            }
            else if (snapshot.Kind == ClipboardContentKind.FileDropList)
            {
                ShowAttachment(ClipboardContentKind.FileDropList, snapshot.FilePaths);
            }
            else
            {
                ShowTextInput(string.Empty);
            }
        }

        public void SetContent(string text)
        {
            SetSnapshot(ClipboardSnapshot.FromText(text));
        }

        public string GetContent()
        {
            return txtInput.Text;
        }

        private void ShowTextInput(string text)
        {
            _currentSnapshot = null;
            borderAttachment.Visibility = Visibility.Collapsed;
            iconImage.Visibility = Visibility.Collapsed;
            iconFile.Visibility = Visibility.Collapsed;
            txtInput.Visibility = Visibility.Visible;
            txtInput.Text = text ?? string.Empty;
            txtInput.Focus();
            if (!string.IsNullOrEmpty(txtInput.Text))
            {
                txtInput.SelectAll();
            }
        }

        private void ShowAttachment(ClipboardContentKind kind, IReadOnlyList<string> filePaths)
        {
            txtInput.Visibility = Visibility.Collapsed;
            txtInput.Text = string.Empty;
            borderAttachment.Visibility = Visibility.Visible;

            bool isImage = kind == ClipboardContentKind.Image ||
                           (kind == ClipboardContentKind.FileDropList && filePaths != null && filePaths.Count > 0 && filePaths.All(IsImageFile));

            if (isImage)
            {
                iconImage.Visibility = Visibility.Visible;
                iconFile.Visibility = Visibility.Collapsed;
                borderAttachment.ToolTip = (filePaths != null && filePaths.Count > 0)
                    ? string.Join(Environment.NewLine, filePaths)
                    : LanguageManager.Instance["ActionPanel_Image"];
            }
            else
            {
                iconImage.Visibility = Visibility.Collapsed;
                iconFile.Visibility = Visibility.Visible;
                borderAttachment.ToolTip = (filePaths != null && filePaths.Count > 0)
                    ? string.Join(Environment.NewLine, filePaths)
                    : LanguageManager.Instance["ActionPanel_File"];
            }
        }

        private static bool IsImageFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string ext = System.IO.Path.GetExtension(path)?.ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".bmp" || ext == ".webp" || ext == ".ico";
        }

        public void ClearAttachment()
        {
            ShowTextInput(string.Empty);
        }

        private void BtnClearAttachment_Click(object sender, RoutedEventArgs e)
        {
            ClearAttachment();
        }

        public void Close()
        {
            this.Visibility = Visibility.Collapsed;
            CloseRequested?.Invoke();
        }

        private void BtnClosePanel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ActionPanel_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }

        public void LoadActions(List<ActionItem> actions)
        {
            actionsWrapPanel.Children.Clear();
            if (actions == null) return;

            var sortedActions = actions.OrderBy(a => a.SortOrder).ToList();
            foreach (var action in sortedActions)
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

                if (_currentSnapshot != null && HotkeyActionPlanner.IsAttachmentClipboard(_currentSnapshot))
                {
                    ActionSubmitted?.Invoke(action, string.Empty, _currentSnapshot);
                }
                else
                {
                    ActionSubmitted?.Invoke(action, txtInput.Text, null);
                }
            }
        }
    }
}
