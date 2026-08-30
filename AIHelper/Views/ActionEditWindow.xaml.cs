// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using AIHelper.Models;
using AIHelper.Services;

namespace AIHelper.Views
{
    public partial class ActionEditWindow : Window
    {
        private readonly ActionItem _action;
        private readonly AppSettings _currentSettings;
        private string _hotkeyModifiers = "";
        private string _hotkeyKey = "";
        private List<PlatformOption> _platformOptions;

        /// <summary>
        /// Helper class for platform ComboBox items
        /// </summary>
        private class PlatformOption
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        public ActionEditWindow(ActionItem action, string title = null, List<AiPlatform> platforms = null, AppSettings currentSettings = null)
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
            _currentSettings = currentSettings;

            // Load current values
            txtName.Text = _action.Name ?? "";
            txtIcon.Text = _action.Icon ?? "";
            txtSortOrder.Text = _action.SortOrder.ToString();
            _hotkeyModifiers = _action.HotkeyModifiers ?? "";
            _hotkeyKey = _action.HotkeyKey ?? "";
            txtHotkey.Text = HotkeyService.FormatHotkey(_hotkeyModifiers, _hotkeyKey);
            txtPrompt.Text = _action.Prompt ?? "";

            // Initialize platform ComboBox
            InitializePlatformComboBox(platforms, _action.PlatformId);

            ValidateHotkeyConflict();
        }

        private void InitializePlatformComboBox(List<AiPlatform> platforms, string selectedPlatformId)
        {
            _platformOptions = new List<PlatformOption>();
            _platformOptions.Add(new PlatformOption
            {
                Id = "",
                Name = LanguageManager.Instance["ActionEdit_Platform_Default"]
            });

            if (platforms != null)
            {
                foreach (var p in platforms)
                {
                    _platformOptions.Add(new PlatformOption { Id = p.Id, Name = p.Name });
                }
            }

            cmbPlatform.DisplayMemberPath = "Name";
            cmbPlatform.ItemsSource = _platformOptions;

            // Select the matching platform
            var selected = _platformOptions.FirstOrDefault(o => o.Id == (selectedPlatformId ?? ""));
            cmbPlatform.SelectedItem = selected ?? _platformOptions[0];
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
            txtHotkey.Text = HotkeyService.FormatHotkey(_hotkeyModifiers, _hotkeyKey);
            ValidateHotkeyConflict();
        }

        private void BtnClearHotkey_Click(object sender, RoutedEventArgs e)
        {
            _hotkeyModifiers = "";
            _hotkeyKey = "";
            txtHotkey.Text = LanguageManager.Instance["None"];
            ValidateHotkeyConflict();
        }

        private void ValidateHotkeyConflict()
        {
            if (tbHotkeyConflict == null) return;

            if (string.IsNullOrWhiteSpace(_hotkeyKey))
            {
                tbHotkeyConflict.Text = "";
                tbHotkeyConflict.Visibility = Visibility.Collapsed;
                return;
            }

            // 1. Check internal conflict
            var conflict = HotkeyService.CheckInternalConflict(_hotkeyModifiers, _hotkeyKey, "Action", _action?.Id, _currentSettings);
            if (conflict.HasConflict)
            {
                tbHotkeyConflict.Text = conflict.ErrorMessage;
                tbHotkeyConflict.Visibility = Visibility.Visible;
                return;
            }

            // 2. Check global probe availability
            if (!HotkeyService.Instance.TestGlobalHotkeyAvailability(_hotkeyModifiers, _hotkeyKey, out string globalErr))
            {
                tbHotkeyConflict.Text = globalErr ?? LanguageManager.Instance["Hotkey_Conflict_System"];
                tbHotkeyConflict.Visibility = Visibility.Visible;
                return;
            }

            tbHotkeyConflict.Text = "";
            tbHotkeyConflict.Visibility = Visibility.Collapsed;
        }

        private string GetModifiersString()
        {
            var parts = new List<string>();
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) parts.Add("Ctrl");
            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) parts.Add("Alt");
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) parts.Add("Shift");
            return string.Join("+", parts);
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            string name = txtName.Text?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(LanguageManager.Instance["ActionEdit_EmptyNameWarn"], LanguageManager.Instance["Notice"], MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate internal conflict before saving
            if (!string.IsNullOrWhiteSpace(_hotkeyKey))
            {
                var conflict = HotkeyService.CheckInternalConflict(_hotkeyModifiers, _hotkeyKey, "Action", _action?.Id, _currentSettings);
                if (conflict.HasConflict)
                {
                    MessageBox.Show(conflict.ErrorMessage, LanguageManager.Instance["Notice"], MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
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

            // Save selected platform
            if (cmbPlatform.SelectedItem is PlatformOption selectedPlatform)
            {
                _action.PlatformId = selectedPlatform.Id ?? "";
            }
            else
            {
                _action.PlatformId = "";
            }

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
