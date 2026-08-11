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
            chkAutoSubmit.IsChecked = _settings.AutoSubmit;
            txtProxyServer.Text = _settings.ProxyServer ?? "";
            tbProjectUrl.Text = _settings.ProjectUrl;
            tbUpdateUrl.Text = _settings.UpdateUrl;
            var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            tbVersion.Text = $"v{ver?.Major}.{ver?.Minor}.{ver?.Build}";

            if (string.Equals(_settings.Language, "en", StringComparison.OrdinalIgnoreCase))
            {
                cbiLangEn.IsSelected = true;
            }
            else
            {
                cbiLangZh.IsSelected = true;
            }

            if (_settings.Platforms != null && _settings.Platforms.Count > 0)
            {
                var activePlatform = _settings.Platforms.FirstOrDefault(p => p.Id == _settings.ActivePlatformId)
                                  ?? _settings.Platforms.FirstOrDefault(p => p.IsActive)
                                  ?? _settings.Platforms[0];

                foreach (var p in _settings.Platforms)
                {
                    p.IsActive = (p == activePlatform);
                }
                _settings.ActivePlatformId = activePlatform.Id;
            }

            dgPlatforms.ItemsSource = _settings.Platforms;
            dgActions.ItemsSource = _settings.Actions;
            txtPanelHotkey.Text = FormatHotkey(_settings.PanelHotkeyModifiers, _settings.PanelHotkeyKey);
        }

        private void RbPlatformActive_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.DataContext is AiPlatform selectedPlatform)
            {
                foreach (var p in _settings.Platforms)
                {
                    p.IsActive = (p == selectedPlatform);
                }
                _settings.ActivePlatformId = selectedPlatform.Id;
            }
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
                MessageBox.Show(LanguageManager.Instance["Settings_Platform_EmptyWarn"], LanguageManager.Instance["Notice"], MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            string selectedLang = cbiLangEn.IsSelected ? "en" : "zh";
            _settings.Language = selectedLang;
            LanguageManager.Instance.CurrentLanguage = selectedLang;

            _settings.AutoStart = chkAutoStart.IsChecked == true;
            _settings.AutoSubmit = chkAutoSubmit.IsChecked == true;
            
            string newProxy = txtProxyServer.Text?.Trim() ?? "";
            bool proxyChanged = (_settings.ProxyServer ?? "") != newProxy;
            _settings.ProxyServer = newProxy;

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

            if (proxyChanged)
            {
                MessageBox.Show(LanguageManager.Instance["Settings_ProxyChangedNotice"], LanguageManager.Instance["Notice"], MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return true;
        }

        private void BtnOpenProject_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl(tbProjectUrl.Text?.Trim());
        }

        private void BtnOpenUpdate_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl(tbUpdateUrl.Text?.Trim());
        }

        private void TbProjectUrl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            OpenUrl(tbProjectUrl.Text?.Trim());
        }

        private void TbUpdateUrl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            OpenUrl(tbUpdateUrl.Text?.Trim());
        }

        private void OpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            try
            {
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://" + url;
                }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(LanguageManager.Instance.GetString("Settings_About_OpenUrlError", ex.Message), LanguageManager.Instance["Error"], MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            AddPlatform(LanguageManager.Instance["Settings_Platform_NewPlatform"], "https://");
        }

        private void BtnDeletePlatform_Click(object sender, RoutedEventArgs e)
        {
            if (dgPlatforms.SelectedItem is AiPlatform p)
            {
                bool wasActive = p.IsActive;
                _settings.Platforms.Remove(p);
                if (wasActive && _settings.Platforms.Count > 0)
                {
                    _settings.Platforms[0].IsActive = true;
                    _settings.ActivePlatformId = _settings.Platforms[0].Id;
                }
                dgPlatforms.Items.Refresh();
            }
        }

        private void BtnEditPlatform_Click(object sender, RoutedEventArgs e)
        {
            if (dgPlatforms.SelectedItem is AiPlatform platform)
            {
                var editWindow = new PlatformEditWindow(platform, LanguageManager.Instance["PlatformEdit_Title_Edit"]);
                editWindow.Owner = this;
                if (editWindow.ShowDialog() == true)
                {
                    dgPlatforms.Items.Refresh();
                }
            }
            else
            {
                MessageBox.Show(LanguageManager.Instance["Settings_Platform_SelectEditWarn"], LanguageManager.Instance["Notice"], MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnAddClaude_Click(object sender, RoutedEventArgs e) => AddPlatform("Claude", "https://claude.ai/new");
        private void BtnAddGemini_Click(object sender, RoutedEventArgs e) => AddPlatform("Gemini", "https://gemini.google.com/app");
        private void BtnAddDeepSeek_Click(object sender, RoutedEventArgs e) => AddPlatform("DeepSeek", "https://chat.deepseek.com/");

        private void AddPlatform(string name, string url)
        {
            var newPlatform = new AiPlatform
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Url = url,
                IsActive = _settings.Platforms.Count == 0
            };

            var editWindow = new PlatformEditWindow(newPlatform, LanguageManager.Instance["PlatformEdit_Title_Add"]);
            editWindow.Owner = this;
            if (editWindow.ShowDialog() == true)
            {
                if (newPlatform.IsActive)
                {
                    foreach (var p in _settings.Platforms)
                    {
                        p.IsActive = (p == newPlatform);
                    }
                    _settings.ActivePlatformId = newPlatform.Id;
                }
                _settings.Platforms.Add(newPlatform);
                dgPlatforms.Items.Refresh();
                dgPlatforms.SelectedItem = newPlatform;
            }
        }

        private void BtnAddAction_Click(object sender, RoutedEventArgs e)
        {
            var newAction = new ActionItem
            {
                Id = Guid.NewGuid().ToString(),
                Name = LanguageManager.Instance["Settings_Action_NewAction"],
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
            if (string.IsNullOrEmpty(key)) return LanguageManager.Instance["None"];
            if (string.IsNullOrEmpty(modifiers)) return key;
            return modifiers.Replace("+", " + ") + " + " + key;
        }
    }
}
