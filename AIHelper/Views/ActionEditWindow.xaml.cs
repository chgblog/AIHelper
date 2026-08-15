// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using AIHelper.Models;
using AIHelper.Services;

namespace AIHelper.Views
{
    public partial class ActionEditWindow : Window
    {
        private readonly ActionItem _action;
        private string _hotkeyModifiers = "";
        private string _hotkeyKey = "";

        public ActionEditWindow(ActionItem action, string title = null)
        {
            InitializeComponent();
            if (!string.IsNullOrEmpty(title))
            {
                this.Title = title;
            }
            else
            {
                this.Title = LanguageManager.Instance["ActionEdit_Title_Edit"];
            }

            _action = action ?? throw new ArgumentNullException(nameof(action));

            // Load current values
            txtName.Text = _action.Name ?? "";
            txtIcon.Text = _action.Icon ?? "";
            txtSortOrder.Text = _action.SortOrder.ToString();
            _hotkeyModifiers = _action.HotkeyModifiers ?? "";
            _hotkeyKey = _action.HotkeyKey ?? "";
            txtHotkey.Text = FormatHotkey(_hotkeyModifiers, _hotkeyKey);
            txtPrompt.Text = _action.Prompt ?? "";
        }

        private void TxtHotkey_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            
            // Ignore pure modifier keys
            if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin)
                return;

            _hotkeyModifiers = GetModifiersString();
            _hotkeyKey = key.ToString();
            txtHotkey.Text = FormatHotkey(_hotkeyModifiers, _hotkeyKey);
        }

        private void BtnClearHotkey_Click(object sender, RoutedEventArgs e)
        {
            _hotkeyModifiers = "";
            _hotkeyKey = "";
            txtHotkey.Text = LanguageManager.Instance["None"];
        }

        private string GetModifiersString()
        {
            var parts = new List<string>();
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) parts.Add("Ctrl");
            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) parts.Add("Alt");
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) parts.Add("Shift");
            return string.Join("+", parts);
        }

        private string FormatHotkey(string modifiers, string key)
        {
            if (string.IsNullOrEmpty(key)) return LanguageManager.Instance["None"];
            if (string.IsNullOrEmpty(modifiers)) return key;
            return modifiers.Replace("+", " + ") + " + " + key;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            string name = txtName.Text?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(LanguageManager.Instance["ActionEdit_EmptyNameWarn"], LanguageManager.Instance["Notice"], MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _action.Name = name;
            _action.Icon = txtIcon.Text?.Trim() ?? "";
            if (int.TryParse(txtSortOrder.Text?.Trim(), out int sortOrder))
            {
                _action.SortOrder = sortOrder;
            }
            _action.HotkeyModifiers = _hotkeyModifiers;
            _action.HotkeyKey = _hotkeyKey;
            _action.Prompt = txtPrompt.Text ?? "";

            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
