using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AIHelper.Models;
using AIHelper.Services;

namespace AIHelper.Views
{
    public partial class SettingsWindow : Window
    {
        private AppSettings _settings;
        private ActionItem _selectedAction;

        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            _settings = SettingsService.Instance.Load();
            chkAutoStart.IsChecked = _settings.AutoStart;
            dgPlatforms.ItemsSource = _settings.Platforms;
            dgActions.ItemsSource = _settings.Actions;
            txtPanelHotkey.Text = FormatHotkey(_settings.PanelHotkeyModifiers, _settings.PanelHotkeyKey);
        }

        private void ApplyCurrentActionEdit()
        {
            if (_selectedAction != null)
            {
                _selectedAction.Name = txtActionName.Text;
                _selectedAction.Prompt = txtActionPrompt.Text;
                dgActions.Items.Refresh();
            }
        }

        private bool SaveSettings()
        {
            ApplyCurrentActionEdit();

            if (_settings.Platforms.Count == 0)
            {
                MessageBox.Show("平台列表不能为空。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            _settings.AutoStart = chkAutoStart.IsChecked == true;
            AutoStartService.SetAutoStart(_settings.AutoStart);

            var activePlatform = _settings.Platforms.FirstOrDefault(p => p.IsActive);
            if (activePlatform != null)
            {
                _settings.ActivePlatformId = activePlatform.Id;
            }
            else if (_settings.Platforms.Count > 0)
            {
                _settings.Platforms[0].IsActive = true;
                _settings.ActivePlatformId = _settings.Platforms[0].Id;
            }

            SettingsService.Instance.Save(_settings);
            return true;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (SaveSettings())
            {
                this.DialogResult = true;
                this.Close();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void BtnAddPlatform_Click(object sender, RoutedEventArgs e)
        {
            _settings.Platforms.Add(new AiPlatform { Id = Guid.NewGuid().ToString(), Name = "新平台", Url = "https://" });
            dgPlatforms.Items.Refresh();
        }

        private void BtnDeletePlatform_Click(object sender, RoutedEventArgs e)
        {
            if (dgPlatforms.SelectedItem is AiPlatform p)
            {
                _settings.Platforms.Remove(p);
                dgPlatforms.Items.Refresh();
            }
        }

        private void BtnEditPlatform_Click(object sender, RoutedEventArgs e)
        {
            if (dgPlatforms.SelectedItem is AiPlatform platform)
            {
                var editWindow = new PlatformEditWindow(platform);
                editWindow.Owner = this;
                if (editWindow.ShowDialog() == true)
                {
                    dgPlatforms.Items.Refresh();
                }
            }
            else
            {
                MessageBox.Show("请先选择要编辑的平台。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnAddClaude_Click(object sender, RoutedEventArgs e) => AddPlatform("Claude", "https://claude.ai/new");
        private void BtnAddGemini_Click(object sender, RoutedEventArgs e) => AddPlatform("Gemini", "https://gemini.google.com/app");
        private void BtnAddDeepSeek_Click(object sender, RoutedEventArgs e) => AddPlatform("DeepSeek", "https://chat.deepseek.com/");

        private void AddPlatform(string name, string url)
        {
            _settings.Platforms.Add(new AiPlatform { Id = Guid.NewGuid().ToString(), Name = name, Url = url });
            dgPlatforms.Items.Refresh();
        }

        private void BtnAddAction_Click(object sender, RoutedEventArgs e)
        {
            var newAction = new ActionItem
            {
                Id = Guid.NewGuid().ToString(),
                Name = "新操作",
                Prompt = "{content}",
                HotkeyModifiers = "",
                HotkeyKey = ""
            };
            _settings.Actions.Add(newAction);
            dgActions.Items.Refresh();
            dgActions.SelectedItem = newAction;
        }

        private void BtnDeleteAction_Click(object sender, RoutedEventArgs e)
        {
            if (dgActions.SelectedItem is ActionItem a)
            {
                _settings.Actions.Remove(a);
                dgActions.Items.Refresh();
            }
        }

        private void DgActions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedAction = dgActions.SelectedItem as ActionItem;
            if (_selectedAction != null)
            {
                actionEditPanel.IsEnabled = true;
                txtActionName.Text = _selectedAction.Name;
                txtActionPrompt.Text = _selectedAction.Prompt;
                txtActionHotkey.Text = FormatHotkey(_selectedAction.HotkeyModifiers, _selectedAction.HotkeyKey);
            }
            else
            {
                actionEditPanel.IsEnabled = false;
                txtActionName.Clear();
                txtActionPrompt.Clear();
                txtActionHotkey.Clear();
            }
        }

        private void BtnApplyActionEdit_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void TxtActionHotkey_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            // Ignore pure modifier keys
            if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin)
                return;

            string modifiers = GetModifiersString();
            string keyStr = key.ToString();

            if (_selectedAction != null)
            {
                _selectedAction.HotkeyModifiers = modifiers;
                _selectedAction.HotkeyKey = keyStr;
            }
            txtActionHotkey.Text = FormatHotkey(modifiers, keyStr);
        }

        private void TxtPanelHotkey_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin)
                return;

            string modifiers = GetModifiersString();
            string keyStr = key.ToString();

            _settings.PanelHotkeyModifiers = modifiers;
            _settings.PanelHotkeyKey = keyStr;
            txtPanelHotkey.Text = FormatHotkey(modifiers, keyStr);
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
            if (string.IsNullOrEmpty(key)) return "无";
            if (string.IsNullOrEmpty(modifiers)) return key;
            return modifiers.Replace("+", " + ") + " + " + key;
        }
    }
}
