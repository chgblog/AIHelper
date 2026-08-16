// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
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
using Microsoft.Web.WebView2.Wpf;

namespace AIHelper.Views
{
    public partial class MainWindow : Window
    {
        private AppSettings _settings;
        private readonly PageInjector _pageInjector = new PageInjector();
        private int _panelHotkeyId = -1;
        private readonly Dictionary<int, ActionItem> _hotkeyActionMap = new Dictionary<int, ActionItem>();
        private TaskCompletionSource<bool> _webViewInitTcs = new TaskCompletionSource<bool>();
        private SelectionToolbarWindow _selectionToolbar;
        private SettingsWindow _currentSettingsWindow;
        private WebView2 webView;
        private bool _currentWebViewUsesProxy;

        public bool IsExiting { get; set; } = false;
        public bool IsClosed { get; private set; } = false;

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
                    ShowSettings(2); // 平台设置界面
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
            TextSelectionService.Instance.EnableClipboardEnhancement = _settings?.EnableClipboardEnhancementToolbar ?? false;
            TextSelectionService.Instance.AppScopeMode = _settings?.SelectionAppScopeMode ?? 0;
            TextSelectionService.Instance.AppScopeApps = _settings?.SelectionAppScopeApps ?? "";

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
                    int autoHideSeconds = _settings.SelectionToolbarAutoHideSeconds > 0 ? _settings.SelectionToolbarAutoHideSeconds : 3;
                    _selectionToolbar?.ShowAt(selectedText, screenPos, _settings.Actions, autoHideSeconds);
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
            if (_panelHotkeyId < 0)
            {
                Logger.LogError($"Failed to register main panel hotkey: {_settings.PanelHotkeyModifiers}+{_settings.PanelHotkeyKey}");
            }

            // Register Action Hotkeys
            if (_settings?.Actions != null)
            {
                foreach (var action in _settings.Actions.OrderBy(a => a.SortOrder))
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
        }

        /// <summary>
        /// Determines whether the given platform should use proxy based on its UseProxy flag and global proxy config
        /// </summary>
        private bool ShouldUseProxy(AiPlatform platform)
        {
            return (platform?.UseProxy ?? true) && !string.IsNullOrWhiteSpace(_settings?.ProxyServer);
        }

        /// <summary>
        /// Gets the appropriate userDataFolder path based on whether proxy is used.
        /// WebView2 requires separate userDataFolder for different environment options.
        /// </summary>
        private string GetUserDataFolder(bool useProxy)
        {
            string basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AIHelper", "WebView2Data");
            return useProxy ? basePath + "_Proxy" : basePath + "_Direct";
        }

        /// <summary>
        /// Creates a new WebView2 control and adds it to the container
        /// </summary>
        private WebView2 CreateWebView()
        {
            var wv = new WebView2();
            wv.NavigationCompleted += WebView_NavigationCompleted;
            webViewContainer.Children.Add(wv);
            return wv;
        }

        /// <summary>
        /// Destroys the current WebView2 control
        /// </summary>
        private void DestroyWebView()
        {
            if (webView != null)
            {
                webView.NavigationCompleted -= WebView_NavigationCompleted;
                webViewContainer.Children.Remove(webView);
                var wv = webView;
                webView = null;
                try
                {
                    // Stop any pending navigation before dispose
                    if (wv.CoreWebView2 != null)
                    {
                        wv.CoreWebView2.Stop();
                    }
                }
                catch { }
                try
                {
                    wv.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error disposing WebView2", ex);
                }
            }
        }

        private async Task<bool> EnsureWebViewReadyAsync()
        {
            if (webView?.CoreWebView2 != null) return true;

            try
            {
                UpdateStatus(LanguageManager.Instance["Main_Status_WaitingBrowser"]);
                await _webViewInitTcs.Task;
                return webView?.CoreWebView2 != null;
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
                var activePlatform = _settings?.GetActivePlatform();
                bool useProxy = ShouldUseProxy(activePlatform);
                _currentWebViewUsesProxy = useProxy;

                CoreWebView2Environment env = null;
                CoreWebView2EnvironmentOptions options = null;
                if (useProxy)
                {
                    options = new CoreWebView2EnvironmentOptions
                    {
                        AdditionalBrowserArguments = $"--proxy-server=\"{_settings.ProxyServer.Trim()}\""
                    };
                }

                // Create WebView2 control dynamically
                webView = CreateWebView();

                try
                {
                    string userDataFolder = GetUserDataFolder(useProxy);
                    env = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CreateAsync custom folder failed, fallback to default: {ex.Message}");
                }

                if (env == null)
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
                if (activePlatform != null && !string.IsNullOrEmpty(activePlatform.Url))
                {
                    webView.CoreWebView2.Navigate(activePlatform.Url);
                    UpdateStatus(LanguageManager.Instance.GetString("Main_Status_NavigatedTo", activePlatform.Name));
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

        /// <summary>
        /// Reinitializes WebView2 with new proxy settings and navigates to the specified URL
        /// </summary>
        private async Task ReinitializeWebViewAsync(AiPlatform targetPlatform)
        {
            try
            {
                UpdateStatus(LanguageManager.Instance["Main_Status_WaitingBrowser"]);

                // Destroy old WebView2
                DestroyWebView();

                // Wait for the browser process to release resources
                await Task.Delay(500);

                // Reset the init task
                _webViewInitTcs = new TaskCompletionSource<bool>();

                bool useProxy = ShouldUseProxy(targetPlatform);
                _currentWebViewUsesProxy = useProxy;

                CoreWebView2EnvironmentOptions options = null;
                if (useProxy)
                {
                    options = new CoreWebView2EnvironmentOptions
                    {
                        AdditionalBrowserArguments = $"--proxy-server=\"{_settings.ProxyServer.Trim()}\""
                    };
                }

                // Create new WebView2 control
                webView = CreateWebView();

                // Retry environment creation with delay to handle transient COM errors
                CoreWebView2Environment env = null;
                string userDataFolder = GetUserDataFolder(useProxy);
                const int maxRetries = 3;
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        env = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
                        break;
                    }
                    catch (System.Runtime.InteropServices.COMException ex) when (attempt < maxRetries)
                    {
                        Logger.LogError($"WebView2 CreateAsync attempt {attempt} failed, retrying...", ex);
                        await Task.Delay(1000 * attempt);
                    }
                    catch (Exception ex) when (attempt < maxRetries)
                    {
                        System.Diagnostics.Debug.WriteLine($"CreateAsync attempt {attempt} failed: {ex.Message}");
                        await Task.Delay(1000 * attempt);
                    }
                }

                // Fallback to default userDataFolder if custom folder failed
                if (env == null)
                {
                    try
                    {
                        env = await CoreWebView2Environment.CreateAsync(null, null, options);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("WebView2 CreateAsync fallback also failed", ex);
                    }
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

                // Navigate to target platform
                if (targetPlatform != null && !string.IsNullOrEmpty(targetPlatform.Url))
                {
                    webView.CoreWebView2.Navigate(targetPlatform.Url);
                    UpdateStatus(LanguageManager.Instance.GetString("Main_Status_NavigatingTo", targetPlatform.Name));
                }
                else
                {
                    UpdateStatus(LanguageManager.Instance["Main_Status_Ready"]);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("WebView2 reinitialization failed", ex);
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

                bool needProxy = ShouldUseProxy(platform);
                if (needProxy != _currentWebViewUsesProxy)
                {
                    // Proxy setting changed, need to reinitialize WebView2
                    await ReinitializeWebViewAsync(platform);
                }
                else if (await EnsureWebViewReadyAsync() && !string.IsNullOrEmpty(platform.Url))
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

        public async void ShowSettings(int initialTabIndex = 0)
        {
            try
            {
                if (_currentSettingsWindow != null && _currentSettingsWindow.IsLoaded)
                {
                    _currentSettingsWindow.SelectTab(initialTabIndex);
                    _currentSettingsWindow.Activate();
                    return;
                }

                if (!this.IsVisible)
                {
                    ShowAndActivate();
                }

                _currentSettingsWindow = new SettingsWindow(initialTabIndex);
                _currentSettingsWindow.Owner = this;
                if (_currentSettingsWindow.ShowDialog() == true)
                {
                    _settings = SettingsService.Instance.Load();
                    RegisterHotkeys();
                    UpdateTextSelectionServiceState();
                    LoadPlatforms();

                    ((App)Application.Current)?.UpdateTrayMenu();

                    // Check if proxy settings changed for active platform
                    var active = _settings.GetActivePlatform();
                    if (active != null && !string.IsNullOrEmpty(active.Url))
                    {
                        bool needProxy = ShouldUseProxy(active);
                        if (needProxy != _currentWebViewUsesProxy)
                        {
                            // Proxy config changed, reinitialize WebView2
                            await ReinitializeWebViewAsync(active);
                        }
                        else if (await EnsureWebViewReadyAsync())
                        {
                            webView.CoreWebView2.Navigate(active.Url);
                        }
                    }
                }
                _currentSettingsWindow = null;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in ShowSettings", ex);
            }
        }

        public void RefreshSettings()
        {
            _settings = SettingsService.Instance.Load();
            UpdateTextSelectionServiceState();
            ((App)Application.Current)?.UpdateTrayMenu();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!IsExiting)
            {
                e.Cancel = true;
                this.Hide();
                return;
            }
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            IsClosed = true;
            base.OnClosed(e);
        }

        private void UpdateStatus(string message)
        {
            txtStatus.Text = message;
        }
    }
}
