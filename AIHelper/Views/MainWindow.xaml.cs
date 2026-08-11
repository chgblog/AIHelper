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
        private SelectionToolbarWindow _selectionToolbar;

        public MainWindow()
        {
            InitializeComponent();
            this.SourceInitialized += MainWindow_SourceInitialized;
        }

        private async void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            try
            {
                _settings = SettingsService.Instance.Load();

                HotkeyService.Instance.Initialize(this);
                HotkeyService.Instance.HotkeyPressed += HotkeyService_HotkeyPressed;
                RegisterHotkeys();

                _selectionToolbar = new SelectionToolbarWindow();
                _selectionToolbar.ActionRequested += SelectionToolbar_ActionRequested;

                TextSelectionService.Instance.TextSelected += TextSelectionService_TextSelected;
                UpdateTextSelectionServiceState();

                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                var source = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
                source?.AddHook(WndProc);

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
            catch (Exception ex)
            {
                Logger.LogError("Error in MainWindow_SourceInitialized", ex);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == App.WM_SHOWFIRSTINSTANCE)
            {
                Logger.LogInfo("WM_SHOWFIRSTINSTANCE received in MainWindow. Restoring window.");
                Dispatcher.Invoke(() => ShowAndActivate());
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void UpdateTextSelectionServiceState()
        {
            TextSelectionService.Instance.IsEnabled = _settings?.EnableSelectionToolbar ?? false;
            if (_settings?.EnableSelectionToolbar == true)
            {
                TextSelectionService.Instance.Install();
            }
            else
            {
                TextSelectionService.Instance.Uninstall();
            }
        }

        private void TextSelectionService_TextSelected(string selectedText, Point screenPos)
        {
            Dispatcher.Invoke(() =>
            {
                if (_settings?.Actions != null && _settings.Actions.Count > 0)
                {
                    _selectionToolbar?.ShowAt(selectedText, screenPos, _settings.Actions);
                }
            });
        }

        private void SelectionToolbar_ActionRequested(ActionItem action, string text)
        {
            ShowAndActivate();
            string prompt = action.Prompt.Replace("{content}", text);
            ExecutePrompt(prompt);
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
                UpdateStatus(LanguageManager.Instance["Main_Status_WaitingBrowser"]);
                await _webViewInitTcs.Task;
                return webView.CoreWebView2 != null;
            }
            catch (Exception ex)
            {
                UpdateStatus(LanguageManager.Instance.GetString("Main_Status_BrowserInitFailed", ex.Message));
                return false;
            }
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                CoreWebView2Environment env = null;
                CoreWebView2EnvironmentOptions options = null;
                if (!string.IsNullOrWhiteSpace(_settings?.ProxyServer))
                {
                    options = new CoreWebView2EnvironmentOptions
                    {
                        AdditionalBrowserArguments = $"--proxy-server=\"{_settings.ProxyServer.Trim()}\""
                    };
                }

                try
                {
                    string userDataFolder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AIHelper", "WebView2Data");
                    env = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CreateAsync custom folder failed, fallback to default: {ex.Message}");
                }

                if (env == null && options != null)
                {
                    try
                    {
                        env = await CoreWebView2Environment.CreateAsync(null, null, options);
                    }
                    catch { }
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
                    UpdateStatus(LanguageManager.Instance.GetString("Main_Status_NavigatedTo", active.Name));
                }
                else
                {
                    UpdateStatus(LanguageManager.Instance["Main_Status_Ready"]);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("WebView2 initialization failed", ex);
                System.Diagnostics.Debug.WriteLine($"WebView2 initialization failed: {ex.Message}");
                UpdateStatus(LanguageManager.Instance.GetString("Main_Status_WebView2InitFailed", ex.Message));
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
            UpdateStatus(LanguageManager.Instance.GetString("Main_Status_Executing", action.Name));
            var platform = _settings.GetActivePlatform();
            bool autoSubmit = _settings?.AutoSubmit ?? true;
            var result = await _pageInjector.InjectAndSubmitAsync(webView, prompt, platform?.InputSelector, platform?.SubmitSelector, platform?.NewChatSelector, autoSubmit);
            UpdateStatus(result.Success ? LanguageManager.Instance.GetString("Main_Status_Success", result.Message) : LanguageManager.Instance.GetString("Main_Status_Failed", result.Message));
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
            bool autoSubmit = _settings?.AutoSubmit ?? true;
            UpdateStatus(autoSubmit ? LanguageManager.Instance["Main_Status_Submitting"] : LanguageManager.Instance["Main_Status_Injecting"]);
            var platform = _settings.GetActivePlatform();
            var result = await _pageInjector.InjectAndSubmitAsync(webView, prompt, platform?.InputSelector, platform?.SubmitSelector, platform?.NewChatSelector, autoSubmit);
            UpdateStatus(result.Success ? (autoSubmit ? LanguageManager.Instance["Main_Status_SubmitSuccess"] : LanguageManager.Instance["Main_Status_InjectSuccess"]) : LanguageManager.Instance.GetString("Main_Status_OpFailed", result.Message));
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
                        UpdateStatus(LanguageManager.Instance.GetString("Main_Status_NavigatingTo", platform.Name));
                    }
                }
            }
        }

        private void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
                UpdateStatus(LanguageManager.Instance["Main_Status_PageLoadSuccess"]);
            else
                UpdateStatus(LanguageManager.Instance.GetString("Main_Status_PageLoadFailed", e.WebErrorStatus));
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
                UpdateTextSelectionServiceState();
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
