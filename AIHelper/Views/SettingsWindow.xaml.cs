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

        public SettingsWindow(int initialTabIndex = 0)
        {
            InitializeComponent();
            LanguageManager.Instance.LanguageChanged += LanguageManager_LanguageChanged;
            this.Unloaded += (s, e) => LanguageManager.Instance.LanguageChanged -= LanguageManager_LanguageChanged;
            LoadSettings();
            SelectTab(initialTabIndex);
        }

        public void SelectTab(int index)
        {
            if (tabControl != null && index >= 0 && index < tabControl.Items.Count)
            {
                tabControl.SelectedIndex = index;
            }
        }

        private void LanguageManager_LanguageChanged(object sender, EventArgs e)
        {
            UpdateAutoHideTip();
        }

        private void LoadSettings()
        {
            _settings = SettingsService.Instance.Load();
            chkShowMainWindow.IsChecked = _settings.ShowMainWindowOnStartup;
            chkAutoStart.IsChecked = _settings.AutoStart;
            chkAutoSubmit.IsChecked = _settings.AutoSubmit;
            chkEnableSelectionToolbar.IsChecked = _settings.EnableSelectionToolbar;
            chkEnableClipboardEnhancementToolbar.IsChecked = _settings.EnableClipboardEnhancementToolbar;

            int mode = _settings.SelectionAppScopeMode;
            if (mode == 1) rbScopeInclude.IsChecked = true;
            else if (mode == 2) rbScopeExclude.IsChecked = true;
            else rbScopeAll.IsChecked = true;

            txtSelectionAppScopeApps.Text = _settings.SelectionAppScopeApps ?? "";
            txtSelectionToolbarAutoHideSeconds.Text = (_settings.SelectionToolbarAutoHideSeconds > 0 ? _settings.SelectionToolbarAutoHideSeconds : 3).ToString();

            UpdateSelectionToolbarControlStates();
            UpdateAutoHideTip();
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

            if (_settings.Actions != null)
            {
                _settings.Actions = _settings.Actions.OrderBy(a => a.SortOrder).ToList();
                for (int i = 0; i < _settings.Actions.Count; i++)
                {
                    _settings.Actions[i].SortOrder = i + 1;
                }
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

        private bool SaveSettings()
        {

            if (_settings.Actions != null)
            {
                _settings.Actions = _settings.Actions.OrderBy(a => a.SortOrder).ToList();
            }

            if (_settings.Platforms.Count == 0)
            {
                MessageBox.Show(LanguageManager.Instance["Settings_Platform_EmptyWarn"], LanguageManager.Instance["Notice"], MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            string selectedLang = cbiLangEn.IsSelected ? "en" : "zh";
            _settings.Language = selectedLang;
            LanguageManager.Instance.CurrentLanguage = selectedLang;

            _settings.ShowMainWindowOnStartup = chkShowMainWindow.IsChecked == true;
            _settings.AutoStart = chkAutoStart.IsChecked == true;
            _settings.AutoSubmit = chkAutoSubmit.IsChecked == true;
            _settings.EnableSelectionToolbar = chkEnableSelectionToolbar.IsChecked == true;
            _settings.EnableClipboardEnhancementToolbar = chkEnableClipboardEnhancementToolbar.IsChecked == true;

            if (int.TryParse(txtSelectionToolbarAutoHideSeconds.Text?.Trim(), out int autoHideSec) && autoHideSec > 0)
            {
                _settings.SelectionToolbarAutoHideSeconds = autoHideSec;
            }
            else
            {
                _settings.SelectionToolbarAutoHideSeconds = 3;
            }

            int appScopeMode = 0;
            if (rbScopeInclude.IsChecked == true) appScopeMode = 1;
            else if (rbScopeExclude.IsChecked == true) appScopeMode = 2;
            _settings.SelectionAppScopeMode = appScopeMode;
            _settings.SelectionAppScopeApps = txtSelectionAppScopeApps.Text?.Trim() ?? "";
            
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

        private void ChkEnableSelectionToolbar_Click(object sender, RoutedEventArgs e)
        {
            UpdateSelectionToolbarControlStates();
        }

        private void RbScope_CheckedChanged(object sender, RoutedEventArgs e)
        {
            UpdateSelectionToolbarControlStates();
        }

        private void UpdateSelectionToolbarControlStates()
        {
            bool isSelectionEnabled = chkEnableSelectionToolbar.IsChecked == true;
            if (chkEnableClipboardEnhancementToolbar != null)
                chkEnableClipboardEnhancementToolbar.IsEnabled = isSelectionEnabled;

            if (rbScopeAll != null) rbScopeAll.IsEnabled = isSelectionEnabled;
            if (rbScopeInclude != null) rbScopeInclude.IsEnabled = isSelectionEnabled;
            if (rbScopeExclude != null) rbScopeExclude.IsEnabled = isSelectionEnabled;

            bool isCustomScope = isSelectionEnabled && (rbScopeInclude?.IsChecked == true || rbScopeExclude?.IsChecked == true);
            if (txtSelectionAppScopeApps != null)
                txtSelectionAppScopeApps.IsEnabled = isCustomScope;
            if (btnSelectApps != null)
                btnSelectApps.IsEnabled = isCustomScope;

            if (txtSelectionToolbarAutoHideSeconds != null)
                txtSelectionToolbarAutoHideSeconds.IsEnabled = isSelectionEnabled;
            if (tbSelectionToolbarAutoHideTip != null)
                tbSelectionToolbarAutoHideTip.IsEnabled = isSelectionEnabled;
        }

        private void TxtSelectionToolbarAutoHideSeconds_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateAutoHideTip();
        }

        private void UpdateAutoHideTip()
        {
            if (tbSelectionToolbarAutoHideTip == null) return;

            int sec = 3;
            if (txtSelectionToolbarAutoHideSeconds != null && int.TryParse(txtSelectionToolbarAutoHideSeconds.Text?.Trim(), out int parsed) && parsed > 0)
            {
                sec = parsed;
            }
            tbSelectionToolbarAutoHideTip.Text = LanguageManager.Instance.GetString("Settings_General_SelectionToolbarAutoHideTip", sec);
        }

        private void BtnSelectApps_Click(object sender, RoutedEventArgs e)
        {
            string currentText = txtSelectionAppScopeApps.Text ?? "";
            var dialog = new AppSelectionWindow(currentText)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true && dialog.SelectedProcessNames != null)
            {
                if (dialog.ResultMode == AppSelectionResultMode.Replace)
                {
                    txtSelectionAppScopeApps.Text = string.Join("\n", dialog.SelectedProcessNames);
                }
                else
                {
                    var existingItems = (txtSelectionAppScopeApps.Text ?? "")
                        .Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();

                    var newSet = new HashSet<string>(existingItems, StringComparer.OrdinalIgnoreCase);
                    foreach (var app in dialog.SelectedProcessNames)
                    {
                        newSet.Add(app);
                    }

                    txtSelectionAppScopeApps.Text = string.Join("\n", newSet);
                }
            }
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
            int nextSort = (_settings.Actions != null && _settings.Actions.Count > 0)
                ? _settings.Actions.Max(a => a.SortOrder) + 1
                : 1;

            var newAction = new ActionItem
            {
                Id = Guid.NewGuid().ToString(),
                Name = LanguageManager.Instance["Settings_Action_NewAction"],
                Prompt = "{content}",
                HotkeyModifiers = "",
                HotkeyKey = "",
                SortOrder = nextSort,
                Icon = "📋"
            };

            var editWindow = new ActionEditWindow(newAction, LanguageManager.Instance["ActionEdit_Title_Add"]);
            editWindow.Owner = this;
            if (editWindow.ShowDialog() == true)
            {
                _settings.Actions.Add(newAction);
                _settings.Actions = _settings.Actions.OrderBy(a => a.SortOrder).ToList();
                dgActions.ItemsSource = null;
                dgActions.ItemsSource = _settings.Actions;
                dgActions.SelectedItem = newAction;
            }
        }

        private void BtnEditAction_Click(object sender, RoutedEventArgs e)
        {
            if (dgActions.SelectedItem is ActionItem selectedAction)
            {
                var clone = new ActionItem
                {
                    Id = selectedAction.Id,
                    Name = selectedAction.Name,
                    Prompt = selectedAction.Prompt,
                    HotkeyModifiers = selectedAction.HotkeyModifiers,
                    HotkeyKey = selectedAction.HotkeyKey,
                    IsBuiltIn = selectedAction.IsBuiltIn,
                    SortOrder = selectedAction.SortOrder,
                    Icon = selectedAction.Icon
                };

                var editWindow = new ActionEditWindow(clone, LanguageManager.Instance["ActionEdit_Title_Edit"]);
                editWindow.Owner = this;
                if (editWindow.ShowDialog() == true)
                {
                    selectedAction.Name = clone.Name;
                    selectedAction.Prompt = clone.Prompt;
                    selectedAction.HotkeyModifiers = clone.HotkeyModifiers;
                    selectedAction.HotkeyKey = clone.HotkeyKey;
                    selectedAction.SortOrder = clone.SortOrder;
                    selectedAction.Icon = clone.Icon;

                    _settings.Actions = _settings.Actions.OrderBy(a => a.SortOrder).ToList();
                    dgActions.ItemsSource = null;
                    dgActions.ItemsSource = _settings.Actions;
                    dgActions.SelectedItem = selectedAction;
                }
            }
            else
            {
                MessageBox.Show(LanguageManager.Instance["Settings_Action_SelectEditWarn"], LanguageManager.Instance["Notice"], MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnDeleteAction_Click(object sender, RoutedEventArgs e)
        {
            if (dgActions.SelectedItem is ActionItem a)
            {
                _settings.Actions.Remove(a);
                for (int i = 0; i < _settings.Actions.Count; i++)
                {
                    _settings.Actions[i].SortOrder = i + 1;
                }
                dgActions.ItemsSource = null;
                dgActions.ItemsSource = _settings.Actions;
            }
        }

        private void ReorderAction(ActionItem action, int direction)
        {
            if (action == null || _settings.Actions == null) return;

            int currentIndex = _settings.Actions.IndexOf(action);
            if (currentIndex < 0) return;

            int newIndex = currentIndex + direction;
            if (newIndex < 0 || newIndex >= _settings.Actions.Count) return;

            var temp = _settings.Actions[currentIndex];
            _settings.Actions[currentIndex] = _settings.Actions[newIndex];
            _settings.Actions[newIndex] = temp;

            for (int i = 0; i < _settings.Actions.Count; i++)
            {
                _settings.Actions[i].SortOrder = i + 1;
            }

            dgActions.ItemsSource = null;
            dgActions.ItemsSource = _settings.Actions;
            dgActions.SelectedItem = action;
        }

        private void BtnMoveUpAction_Click(object sender, RoutedEventArgs e)
        {
            if (dgActions.SelectedItem is ActionItem action)
            {
                ReorderAction(action, -1);
            }
        }

        private void BtnMoveDownAction_Click(object sender, RoutedEventArgs e)
        {
            if (dgActions.SelectedItem is ActionItem action)
            {
                ReorderAction(action, 1);
            }
        }

        private void BtnRowMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ActionItem action)
            {
                ReorderAction(action, -1);
            }
        }

        private void BtnRowMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ActionItem action)
            {
                ReorderAction(action, 1);
            }
        }

        private void DgActions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedAction = dgActions.SelectedItem as ActionItem;
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
