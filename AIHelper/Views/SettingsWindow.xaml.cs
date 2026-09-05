// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Collections.Generic;
using System.IO;
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
            chkEnableContextMenu.IsChecked = _settings.EnableContextMenu;
            chkEnableSelectionToolbar.IsChecked = _settings.EnableSelectionToolbar;
            int copyMode = _settings.SelectionToolbarCopyMode;
            if (copyMode == 1) cbiCopyFirst.IsSelected = true;
            else if (copyMode == 2) cbiCopyLast.IsSelected = true;
            else cbiCopyDisabled.IsSelected = true;
            chkAutoCheckUpdate.IsChecked = _settings.AutoCheckUpdate;

            int mode = _settings.SelectionAppScopeMode;
            if (mode == 1) rbScopeInclude.IsChecked = true;
            else if (mode == 2) rbScopeExclude.IsChecked = true;
            else rbScopeAll.IsChecked = true;

            txtSelectionAppScopeApps.Text = _settings.SelectionAppScopeApps ?? "";
            txtSelectionToolbarAutoHideSeconds.Text = (_settings.SelectionToolbarAutoHideSeconds > 0 ? _settings.SelectionToolbarAutoHideSeconds : 3).ToString();

            txtSimulateMinInterval.Text = (_settings.SimulateVisitMinIntervalSeconds > 0 ? _settings.SimulateVisitMinIntervalSeconds : 0.5).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            txtSimulateMaxInterval.Text = (_settings.SimulateVisitMaxIntervalSeconds > 0 ? _settings.SimulateVisitMaxIntervalSeconds : 3.0).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            txtSimulateMinDistance.Text = (_settings.SimulateVisitMinScrollDistance > 0 ? _settings.SimulateVisitMinScrollDistance : 100).ToString();
            txtSimulateMaxDistance.Text = (_settings.SimulateVisitMaxScrollDistance > 0 ? _settings.SimulateVisitMaxScrollDistance : 300).ToString();
            chkSimulateUseProxy.IsChecked = _settings.SimulateVisitUseProxy;

            UpdateSelectionToolbarControlStates();
            UpdateAutoHideTip();
            txtProxyServer.Text = _settings.ProxyServer ?? "";
            tbProjectUrl.Text = _settings.ProjectUrl;
            tbUpdateUrl.Text = _settings.UpdateUrl;
            var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            if (ver != null)
            {
                tbVersion.Text = ver.Revision > 0 
                    ? $"v{ver.Major}.{ver.Minor}.{ver.Build}.{ver.Revision}" 
                    : $"v{ver.Major}.{ver.Minor}.{ver.Build}";
            }

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
            txtPanelHotkey.Text = HotkeyService.FormatHotkey(_settings.PanelHotkeyModifiers, _settings.PanelHotkeyKey);
            txtMainWindowHotkey.Text = HotkeyService.FormatHotkey(_settings.MainWindowHotkeyModifiers, _settings.MainWindowHotkeyKey);

            ValidatePanelHotkeyConflict();
            ValidateMainWindowHotkeyConflict();

            // Set up platform name converter for actions DataGrid
            UpdateActionPlatformColumnBinding();
        }

        private void UpdateActionPlatformColumnBinding()
        {
            if (colActionPlatform != null && _settings?.Platforms != null)
            {
                var converter = new Converters.PlatformIdToNameConverter { Platforms = _settings.Platforms };
                var binding = new System.Windows.Data.Binding("PlatformId") { Converter = converter };
                colActionPlatform.Binding = binding;
            }
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
            // Validate all hotkey conflicts before saving
            var conflicts = CheckAllHotkeyConflicts();
            if (conflicts.Count > 0)
            {
                MessageBox.Show(
                    LanguageManager.Instance.GetString("Hotkey_Conflict_SaveError", string.Join("\n", conflicts)),
                    LanguageManager.Instance["Notice"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

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
            _settings.EnableContextMenu = chkEnableContextMenu.IsChecked == true;
            _settings.EnableSelectionToolbar = chkEnableSelectionToolbar.IsChecked == true;
            int copyMode = 0;
            if (cbiCopyFirst.IsSelected) copyMode = 1;
            else if (cbiCopyLast.IsSelected) copyMode = 2;
            _settings.SelectionToolbarCopyMode = copyMode;
            _settings.AutoCheckUpdate = chkAutoCheckUpdate.IsChecked == true;

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

            // Validate and save simulation settings
            string rawMinInt = txtSimulateMinInterval.Text?.Trim();
            string rawMaxInt = txtSimulateMaxInterval.Text?.Trim();
            string rawMinDist = txtSimulateMinDistance.Text?.Trim();
            string rawMaxDist = txtSimulateMaxDistance.Text?.Trim();

            if (!double.TryParse(rawMinInt, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double minInterval) &&
                !double.TryParse(rawMinInt, out minInterval))
            {
                minInterval = -1;
            }

            if (!double.TryParse(rawMaxInt, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double maxInterval) &&
                !double.TryParse(rawMaxInt, out maxInterval))
            {
                maxInterval = -1;
            }

            if (!int.TryParse(rawMinDist, out int minDistance))
            {
                minDistance = -1;
            }

            if (!int.TryParse(rawMaxDist, out int maxDistance))
            {
                maxDistance = -1;
            }

            if (minInterval <= 0 || maxInterval < minInterval || minDistance <= 0 || maxDistance < minDistance)
            {
                MessageBox.Show(
                    LanguageManager.Instance["Settings_Simulate_ValidateError"],
                    LanguageManager.Instance["Notice"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            _settings.SimulateVisitMinIntervalSeconds = minInterval;
            _settings.SimulateVisitMaxIntervalSeconds = maxInterval;
            _settings.SimulateVisitMinScrollDistance = minDistance;
            _settings.SimulateVisitMaxScrollDistance = maxDistance;
            _settings.SimulateVisitUseProxy = chkSimulateUseProxy.IsChecked == true;
            
            string newProxy = txtProxyServer.Text?.Trim() ?? "";
            bool proxyChanged = (_settings.ProxyServer ?? "") != newProxy;
            _settings.ProxyServer = newProxy;

            AutoStartService.SetAutoStart(_settings.AutoStart);
            FileContextMenuService.SetContextMenuEnabled(_settings.EnableContextMenu, _settings.Actions);

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

        private void ChkEnableContextMenu_Click(object sender, RoutedEventArgs e)
        {
            bool enable = chkEnableContextMenu.IsChecked == true;
            bool success = FileContextMenuService.SetContextMenuEnabled(enable, _settings.Actions);
            if (!success)
            {
                chkEnableContextMenu.IsChecked = !enable;
                MessageBox.Show(
                    LanguageManager.Instance["ContextMenu_AuthFailed"],
                    LanguageManager.Instance["Notice"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                _settings.EnableContextMenu = enable;
            }
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
            if (tbSelectionToolbarCopy != null)
                tbSelectionToolbarCopy.IsEnabled = isSelectionEnabled;
            if (cmbSelectionToolbarCopy != null)
                cmbSelectionToolbarCopy.IsEnabled = isSelectionEnabled;

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
                Icon = ""
            };

            var editWindow = new ActionEditWindow(newAction, LanguageManager.Instance["ActionEdit_Title_Add"], _settings.Platforms, _settings);
            editWindow.Owner = this;
            if (editWindow.ShowDialog() == true)
            {
                _settings.Actions.Add(newAction);
                _settings.Actions = _settings.Actions.OrderBy(a => a.SortOrder).ToList();
                dgActions.ItemsSource = null;
                dgActions.ItemsSource = _settings.Actions;
                dgActions.SelectedItem = newAction;
                ValidatePanelHotkeyConflict();
                ValidateMainWindowHotkeyConflict();
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
                    Icon = selectedAction.Icon,
                    PlatformId = selectedAction.PlatformId
                };

                var editWindow = new ActionEditWindow(clone, LanguageManager.Instance["ActionEdit_Title_Edit"], _settings.Platforms, _settings);
                editWindow.Owner = this;
                if (editWindow.ShowDialog() == true)
                {
                    selectedAction.Name = clone.Name;
                    selectedAction.Prompt = clone.Prompt;
                    selectedAction.HotkeyModifiers = clone.HotkeyModifiers;
                    selectedAction.HotkeyKey = clone.HotkeyKey;
                    selectedAction.SortOrder = clone.SortOrder;
                    selectedAction.Icon = clone.Icon;
                    selectedAction.PlatformId = clone.PlatformId;

                    _settings.Actions = _settings.Actions.OrderBy(a => a.SortOrder).ToList();
                    dgActions.ItemsSource = null;
                    dgActions.ItemsSource = _settings.Actions;
                    dgActions.SelectedItem = selectedAction;
                    ValidatePanelHotkeyConflict();
                    ValidateMainWindowHotkeyConflict();
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
                ValidatePanelHotkeyConflict();
                ValidateMainWindowHotkeyConflict();
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

        private void BtnBackupConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var configDir = SettingsService.Instance.GetSettingsDirectory();
                var configFile = Path.Combine(configDir, "settings.json");

                if (!File.Exists(configFile))
                {
                    MessageBox.Show(LanguageManager.Instance["Settings_Other_NoConfigFile"], LanguageManager.Instance["Notice"], MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = LanguageManager.Instance["Settings_Other_BackupConfig"],
                    Filter = "JSON Files (*.json)|*.json",
                    FileName = $"AIHelper_settings_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                };

                if (saveDialog.ShowDialog() == true)
                {
                    File.Copy(configFile, saveDialog.FileName, true);
                    MessageBox.Show(LanguageManager.Instance["Settings_Other_BackupSuccess"], LanguageManager.Instance["Notice"], MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(LanguageManager.Instance.GetString("Settings_Other_BackupFailed", ex.Message), LanguageManager.Instance["Error"], MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRestoreConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = LanguageManager.Instance["Settings_Other_RestoreConfig"],
                    Filter = "JSON Files (*.json)|*.json",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                };

                if (openDialog.ShowDialog() == true)
                {
                    var result = MessageBox.Show(
                        LanguageManager.Instance["Settings_Other_RestoreConfirm"],
                        LanguageManager.Instance["Notice"],
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        var configDir = SettingsService.Instance.GetSettingsDirectory();
                        var configFile = Path.Combine(configDir, "settings.json");

                        File.Copy(openDialog.FileName, configFile, true);
                        LoadSettings();
                        MessageBox.Show(LanguageManager.Instance["Settings_Other_RestoreSuccess"], LanguageManager.Instance["Notice"], MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(LanguageManager.Instance.GetString("Settings_Other_RestoreFailed", ex.Message), LanguageManager.Instance["Error"], MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnOpenConfigDir_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var configDir = SettingsService.Instance.GetSettingsDirectory();
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = configDir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(LanguageManager.Instance.GetString("Settings_Other_OpenDirFailed", ex.Message), LanguageManager.Instance["Error"], MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            txtPanelHotkey.Text = HotkeyService.FormatHotkey(modifiers, keyStr);
            ValidatePanelHotkeyConflict();
            ValidateMainWindowHotkeyConflict();
        }

        private void TxtMainWindowHotkey_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin)
                return;

            string modifiers = GetModifiersString();
            string keyStr = key.ToString();

            _settings.MainWindowHotkeyModifiers = modifiers;
            _settings.MainWindowHotkeyKey = keyStr;
            txtMainWindowHotkey.Text = HotkeyService.FormatHotkey(modifiers, keyStr);
            ValidateMainWindowHotkeyConflict();
            ValidatePanelHotkeyConflict();
        }

        private void ValidatePanelHotkeyConflict()
        {
            if (tbPanelHotkeyConflict == null) return;

            if (string.IsNullOrWhiteSpace(_settings?.PanelHotkeyKey))
            {
                tbPanelHotkeyConflict.Text = "";
                tbPanelHotkeyConflict.Visibility = Visibility.Collapsed;
                return;
            }

            var conflict = HotkeyService.CheckInternalConflict(_settings.PanelHotkeyModifiers, _settings.PanelHotkeyKey, "Panel", null, _settings);
            if (conflict.HasConflict)
            {
                tbPanelHotkeyConflict.Text = conflict.ErrorMessage;
                tbPanelHotkeyConflict.Visibility = Visibility.Visible;
                return;
            }

            if (!HotkeyService.Instance.TestGlobalHotkeyAvailability(_settings.PanelHotkeyModifiers, _settings.PanelHotkeyKey, out string globalErr))
            {
                tbPanelHotkeyConflict.Text = globalErr ?? LanguageManager.Instance["Hotkey_Conflict_System"];
                tbPanelHotkeyConflict.Visibility = Visibility.Visible;
                return;
            }

            tbPanelHotkeyConflict.Text = "";
            tbPanelHotkeyConflict.Visibility = Visibility.Collapsed;
        }

        private void ValidateMainWindowHotkeyConflict()
        {
            if (tbMainWindowHotkeyConflict == null) return;

            if (string.IsNullOrWhiteSpace(_settings?.MainWindowHotkeyKey))
            {
                tbMainWindowHotkeyConflict.Text = "";
                tbMainWindowHotkeyConflict.Visibility = Visibility.Collapsed;
                return;
            }

            var conflict = HotkeyService.CheckInternalConflict(_settings.MainWindowHotkeyModifiers, _settings.MainWindowHotkeyKey, "MainWindow", null, _settings);
            if (conflict.HasConflict)
            {
                tbMainWindowHotkeyConflict.Text = conflict.ErrorMessage;
                tbMainWindowHotkeyConflict.Visibility = Visibility.Visible;
                return;
            }

            if (!HotkeyService.Instance.TestGlobalHotkeyAvailability(_settings.MainWindowHotkeyModifiers, _settings.MainWindowHotkeyKey, out string globalErr))
            {
                tbMainWindowHotkeyConflict.Text = globalErr ?? LanguageManager.Instance["Hotkey_Conflict_System"];
                tbMainWindowHotkeyConflict.Visibility = Visibility.Visible;
                return;
            }

            tbMainWindowHotkeyConflict.Text = "";
            tbMainWindowHotkeyConflict.Visibility = Visibility.Collapsed;
        }

        private List<string> CheckAllHotkeyConflicts()
        {
            var conflicts = new List<string>();

            // 1. Panel vs MainWindow
            if (!string.IsNullOrWhiteSpace(_settings.PanelHotkeyKey) && !string.IsNullOrWhiteSpace(_settings.MainWindowHotkeyKey))
            {
                if (HotkeyService.AreHotkeysEqual(_settings.PanelHotkeyModifiers, _settings.PanelHotkeyKey, _settings.MainWindowHotkeyModifiers, _settings.MainWindowHotkeyKey))
                {
                    conflicts.Add($"• 【{LanguageManager.Instance["Settings_Hotkey_PanelKey"].TrimEnd(':')}】与【{LanguageManager.Instance["Settings_Hotkey_MainWindowKey"].TrimEnd(':')}】({HotkeyService.FormatHotkey(_settings.PanelHotkeyModifiers, _settings.PanelHotkeyKey)})");
                }
            }

            // 2. Panel vs Actions
            if (!string.IsNullOrWhiteSpace(_settings.PanelHotkeyKey) && _settings.Actions != null)
            {
                foreach (var action in _settings.Actions)
                {
                    if (!string.IsNullOrWhiteSpace(action.HotkeyKey) && HotkeyService.AreHotkeysEqual(_settings.PanelHotkeyModifiers, _settings.PanelHotkeyKey, action.HotkeyModifiers, action.HotkeyKey))
                    {
                        conflicts.Add($"• 【{LanguageManager.Instance["Settings_Hotkey_PanelKey"].TrimEnd(':')}】与动作【{action.Name}】({HotkeyService.FormatHotkey(action.HotkeyModifiers, action.HotkeyKey)})");
                    }
                }
            }

            // 3. MainWindow vs Actions
            if (!string.IsNullOrWhiteSpace(_settings.MainWindowHotkeyKey) && _settings.Actions != null)
            {
                foreach (var action in _settings.Actions)
                {
                    if (!string.IsNullOrWhiteSpace(action.HotkeyKey) && HotkeyService.AreHotkeysEqual(_settings.MainWindowHotkeyModifiers, _settings.MainWindowHotkeyKey, action.HotkeyModifiers, action.HotkeyKey))
                    {
                        conflicts.Add($"• 【{LanguageManager.Instance["Settings_Hotkey_MainWindowKey"].TrimEnd(':')}】与动作【{action.Name}】({HotkeyService.FormatHotkey(action.HotkeyModifiers, action.HotkeyKey)})");
                    }
                }
            }

            // 4. Actions vs Actions
            if (_settings.Actions != null)
            {
                for (int i = 0; i < _settings.Actions.Count; i++)
                {
                    var a1 = _settings.Actions[i];
                    if (string.IsNullOrWhiteSpace(a1.HotkeyKey)) continue;

                    for (int j = i + 1; j < _settings.Actions.Count; j++)
                    {
                        var a2 = _settings.Actions[j];
                        if (string.IsNullOrWhiteSpace(a2.HotkeyKey)) continue;

                        if (HotkeyService.AreHotkeysEqual(a1.HotkeyModifiers, a1.HotkeyKey, a2.HotkeyModifiers, a2.HotkeyKey))
                        {
                            conflicts.Add($"• 动作【{a1.Name}】与动作【{a2.Name}】({HotkeyService.FormatHotkey(a1.HotkeyModifiers, a1.HotkeyKey)})");
                        }
                    }
                }
            }

            return conflicts;
        }

        private string GetModifiersString()
        {
            var parts = new List<string>();
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) parts.Add("Ctrl");
            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) parts.Add("Alt");
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) parts.Add("Shift");
            return string.Join("+", parts);
        }
    }
}
