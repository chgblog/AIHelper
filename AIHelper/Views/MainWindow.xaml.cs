using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AIHelper.Models;
using AIHelper.Services;
using Microsoft.Web.WebView2.Core;

namespace AIHelper.Views
{
    public partial class MainWindow : Window
    {
        private AppSettings _settings;
        private readonly PageInjector _pageInjector = new PageInjector();
        private int _panelHotkeyId = -1;
        private readonly Dictionary<int, ActionItem> _hotkeyActionMap = new Dictionary<int, ActionItem>();
        private readonly TaskCompletionSource<bool> _webViewInitTcs = new TaskCompletionSource<bool>();

        public MainWindow()
        {
            InitializeComponent();
            this.SourceInitialized += MainWindow_SourceInitialized;
        }

        private async void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            _settings = SettingsService.Instance.Load();

            HotkeyService.Instance.Initialize(this);
            HotkeyService.Instance.HotkeyPressed += HotkeyService_HotkeyPressed;
            RegisterHotkeys();

            // Start initializing WebView2 immediately in background
            var initTask = InitializeWebViewAsync();

            if (_settings.IsFirstRun)
            {
                _settings.IsFirstRun = false;
                SettingsService.Instance.Save(_settings);
                ShowSettings();
            }
            else if (_settings.AutoStart)
            {
                AutoStartService.SetAutoStart(true);
            }

            LoadPlatforms();

            await initTask;
        }

        private void RegisterHotkeys()
        {
            HotkeyService.Instance.UnregisterAll();
            _hotkeyActionMap.Clear();
            _panelHotkeyId = -1;

            // Register Panel Hotkey
            _panelHotkeyId = HotkeyService.Instance.RegisterHotkey(_settings.PanelHotkeyModifiers, _settings.PanelHotkeyKey);

            // Register Action Hotkeys
            foreach (var action in _settings.Actions)
            {
                if (!string.IsNullOrEmpty(action.HotkeyKey))
                {
                    int id = HotkeyService.Instance.RegisterHotkey(action.HotkeyModifiers, action.HotkeyKey);
                    if (id > 0)
                    {
                        _hotkeyActionMap[id] = action;
                    }
                }
            }
        }

        private async Task<bool> EnsureWebViewReadyAsync()
        {
            if (webView.CoreWebView2 != null) return true;

            try
            {
                UpdateStatus("正在等待浏览器组件就绪...");
                await _webViewInitTcs.Task;
                return webView.CoreWebView2 != null;
            }
            catch (Exception ex)
            {
                UpdateStatus($"浏览器组件初始化失败: {ex.Message}");
                return false;
            }
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                CoreWebView2Environment env = null;
                try
                {
                    string userDataFolder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AIHelper", "WebView2Data");
                    env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CreateAsync custom folder failed, fallback to default: {ex.Message}");
                }

                if (env != null)
                {
                    await webView.EnsureCoreWebView2Async(env);
                }
                else
                {
                    await webView.EnsureCoreWebView2Async(null);
                }

                _webViewInitTcs.TrySetResult(true);

                // Navigate to active platform
                var active = _settings?.GetActivePlatform();
                if (active != null && !string.IsNullOrEmpty(active.Url))
                {
                    webView.CoreWebView2.Navigate(active.Url);
                    UpdateStatus($"已导航到 {active.Name}");
                }
                else
                {
                    UpdateStatus("就绪");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebView2 initialization failed: {ex.Message}");
                UpdateStatus($"WebView2 初始化失败: {ex.Message}");
                _webViewInitTcs.TrySetException(ex);
            }
        }

        private void LoadPlatforms()
        {
            cmbPlatforms.ItemsSource = _settings.Platforms;
            var active = _settings.GetActivePlatform();
            if (active != null)
            {
                cmbPlatforms.SelectedItem = active;
            }
        }

        private void HotkeyService_HotkeyPressed(int id)
        {
            if (id == _panelHotkeyId)
            {
                ShowAndActivate();
                ToggleActionPanel();
            }
            else if (_hotkeyActionMap.TryGetValue(id, out var action))
            {
                ExecuteAction(action);
            }
        }

        public void ShowAndActivate()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
            this.Topmost = true;
            this.Topmost = false;
            this.Focus();
        }

        private void ToggleActionPanel()
        {
            if (actionPanel.Visibility == Visibility.Visible)
            {
                actionPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                actionPanel.Visibility = Visibility.Visible;
                actionPanel.LoadActions(_settings.Actions);
                actionPanel.SetContent(ClipboardService.GetText());
            }
        }

        private async void ExecuteAction(ActionItem action)
        {
            ShowAndActivate();
            if (!await EnsureWebViewReadyAsync())
            {
                return;
            }
            string content = ClipboardService.GetText();
            string prompt = action.Prompt.Replace("{content}", content);
            UpdateStatus($"正在执行: {action.Name}...");
            var platform = _settings.GetActivePlatform();
            var result = await _pageInjector.InjectAndSubmitAsync(webView, prompt, platform?.InputSelector, platform?.SubmitSelector);
            UpdateStatus(result.Success ? $"成功: {result.Message}" : $"失败: {result.Message}");
        }

        private void ActionPanel_ActionSubmitted(ActionItem action, string text)
        {
            actionPanel.Visibility = Visibility.Collapsed;
            if (action == null) return;
            string prompt = action.Prompt.Replace("{content}", text);
            ExecutePrompt(prompt);
        }

        private async void ExecutePrompt(string prompt)
        {
            if (!await EnsureWebViewReadyAsync())
            {
                return;
            }
            UpdateStatus("正在提交...");
            var platform = _settings.GetActivePlatform();
            var result = await _pageInjector.InjectAndSubmitAsync(webView, prompt, platform?.InputSelector, platform?.SubmitSelector);
            UpdateStatus(result.Success ? "提交成功" : $"提交失败: {result.Message}");
        }

        private async void CmbPlatforms_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbPlatforms.SelectedItem is AiPlatform platform)
            {
                foreach (var p in _settings.Platforms) p.IsActive = (p.Id == platform.Id);
                _settings.ActivePlatformId = platform.Id;
                SettingsService.Instance.Save(_settings);

                if (await EnsureWebViewReadyAsync() && !string.IsNullOrEmpty(platform.Url))
                {
                    if (webView.CoreWebView2.Source != platform.Url)
                    {
                        webView.CoreWebView2.Navigate(platform.Url);
                        UpdateStatus($"正在导航到 {platform.Name}...");
                    }
                }
            }
        }

        private void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
                UpdateStatus("页面加载完成");
            else
                UpdateStatus($"页面加载失败: {e.WebErrorStatus}");
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                this.DragMove();
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            ShowSettings();
        }

        private async void ShowSettings()
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            if (settingsWindow.ShowDialog() == true)
            {
                _settings = SettingsService.Instance.Load();
                RegisterHotkeys();
                LoadPlatforms();

                // Navigate to new active platform
                var active = _settings.GetActivePlatform();
                if (active != null && !string.IsNullOrEmpty(active.Url) && await EnsureWebViewReadyAsync())
                {
                    webView.CoreWebView2.Navigate(active.Url);
                }
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        private void UpdateStatus(string message)
        {
            txtStatus.Text = message;
        }
    }
}
