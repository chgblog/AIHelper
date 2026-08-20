// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AIHelper.Models;
using AIHelper.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace AIHelper.Views
{
    public partial class MainWindow : Window
    {
        private const int WM_GETMINMAXINFO = 0x0024;
        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
            public POINT(int x, int y) { this.x = x; this.y = y; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MONITORINFO
        {
            public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            public RECT rcMonitor = new RECT();
            public RECT rcWork = new RECT();
            public int dwFlags = 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFO lpmi);

        // Max time to wait for a top-level navigation to complete
        private const int NavigationTimeoutMs = 30000;
        // A page is considered settled when no new navigation starts within this window
        // (sites often bounce through a client-side redirect right after loading)
        private const int NavigationQuietMs = 800;
        // How long to watch for a reload triggered by the new chat button. A click that
        // reloads the page starts navigating almost immediately, while a client side
        // (SPA) new chat never navigates at all — so this window is waited out in full
        // on most platforms and is kept short on purpose.
        private const int NewChatObserveMs = 800;

        private AppSettings _settings;
        private readonly PageInjector _pageInjector = new PageInjector();
        private int _panelHotkeyId = -1;
        private int _mainWindowHotkeyId = -1;
        private readonly Dictionary<int, ActionItem> _hotkeyActionMap = new Dictionary<int, ActionItem>();
        private TaskCompletionSource<bool> _webViewInitTcs = new TaskCompletionSource<bool>();
        private SelectionToolbarWindow _selectionToolbar;
        private SettingsWindow _currentSettingsWindow;
        private WebView2 webView;
        private bool _currentWebViewUsesProxy;
        private UpdateCheckService.UpdateInfo _availableUpdate;

        public bool IsExiting { get; set; } = false;
        public bool IsClosed { get; private set; } = false;

        public MainWindow()
        {
            InitializeComponent();
            this.SourceInitialized += MainWindow_SourceInitialized;
            this.StateChanged += MainWindow_StateChanged;
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

                // 新版本提示（后台检测完成后可能早于/晚于本窗口创建）
                UpdateCheckService.UpdateAvailable += UpdateCheckService_UpdateAvailable;
                LanguageManager.Instance.LanguageChanged += LanguageManager_LanguageChanged;
                ShowUpdateIndicator(UpdateCheckService.AvailableUpdate);

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
            else if (msg == WM_GETMINMAXINFO)
            {
                WmGetMinMaxInfo(hwnd, lParam);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            var mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));
            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                MONITORINFO monitorInfo = new MONITORINFO();
                GetMonitorInfo(monitor, monitorInfo);
                RECT rcWorkArea = monitorInfo.rcWork;
                RECT rcMonitorArea = monitorInfo.rcMonitor;

                mmi.ptMaxPosition.x = Math.Abs(rcWorkArea.left - rcMonitorArea.left);
                mmi.ptMaxPosition.y = Math.Abs(rcWorkArea.top - rcMonitorArea.top);
                mmi.ptMaxSize.x = Math.Abs(rcWorkArea.right - rcWorkArea.left);
                mmi.ptMaxSize.y = Math.Abs(rcWorkArea.bottom - rcWorkArea.top);
            }
            Marshal.StructureToPtr(mmi, lParam, true);
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

        private async void SelectionToolbar_ActionRequested(ActionItem action, string text)
        {
            ShowAndActivate();
            string prompt = action.Prompt.Replace("{content}", text);
            var platform = GetPlatformForAction(action);
            await EnsurePlatformAndExecuteAsync(platform, prompt, action.Name);
        }

        private void RegisterHotkeys()
        {
            HotkeyService.Instance.UnregisterAll();
            _hotkeyActionMap.Clear();
            _panelHotkeyId = -1;
            _mainWindowHotkeyId = -1;

            // Register Panel Hotkey
            _panelHotkeyId = HotkeyService.Instance.RegisterHotkey(_settings.PanelHotkeyModifiers, _settings.PanelHotkeyKey);
            if (_panelHotkeyId < 0)
            {
                Logger.LogError($"Failed to register main panel hotkey: {_settings.PanelHotkeyModifiers}+{_settings.PanelHotkeyKey}");
            }

            // Register Main Window Hotkey (打开主界面)
            if (!string.IsNullOrEmpty(_settings.MainWindowHotkeyKey))
            {
                _mainWindowHotkeyId = HotkeyService.Instance.RegisterHotkey(_settings.MainWindowHotkeyModifiers, _settings.MainWindowHotkeyKey);
                if (_mainWindowHotkeyId < 0)
                {
                    Logger.LogError($"Failed to register main window hotkey: {_settings.MainWindowHotkeyModifiers}+{_settings.MainWindowHotkeyKey}");
                }
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

                // Navigate to target platform and wait for it — callers inject right after
                if (targetPlatform != null && !string.IsNullOrEmpty(targetPlatform.Url))
                {
                    UpdateStatus(LanguageManager.Instance.GetString("Main_Status_NavigatingTo", targetPlatform.Name));
                    await NavigateAndWaitAsync(targetPlatform.Url);
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
            else if (id == _mainWindowHotkeyId)
            {
                ShowAndActivate();
            }
            else if (_hotkeyActionMap.TryGetValue(id, out var action))
            {
                ExecuteAction(action);
            }
        }

        public void ShowAndActivate()
        {
            this.Show();
            if (this.WindowState == WindowState.Minimized)
            {
                this.WindowState = WindowState.Normal;
            }
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
            string content = ClipboardService.GetText();
            string prompt = action.Prompt.Replace("{content}", content);
            var platform = GetPlatformForAction(action);
            await EnsurePlatformAndExecuteAsync(platform, prompt, action.Name);
        }

        private async void ActionPanel_ActionSubmitted(ActionItem action, string text)
        {
            actionPanel.Visibility = Visibility.Collapsed;
            if (action == null) return;
            string prompt = action.Prompt.Replace("{content}", text);
            var platform = GetPlatformForAction(action);
            await EnsurePlatformAndExecuteAsync(platform, prompt, action.Name);
        }

        /// <summary>
        /// Gets the platform to use for the given action.
        /// If the action has a specified PlatformId, returns that platform;
        /// otherwise returns the active platform.
        /// </summary>
        private AiPlatform GetPlatformForAction(ActionItem action)
        {
            if (action != null && !string.IsNullOrEmpty(action.PlatformId))
            {
                var specified = _settings.Platforms?.FirstOrDefault(p => p.Id == action.PlatformId);
                if (specified != null) return specified;
            }
            return _settings.GetActivePlatform();
        }

        /// <summary>
        /// Ensures the WebView2 is on the correct platform and executes the prompt.
        /// Handles platform switching including proxy reinitializtion if needed.
        /// </summary>
        private async Task EnsurePlatformAndExecuteAsync(AiPlatform platform, string prompt, string actionName = null)
        {
            if (platform == null)
            {
                UpdateStatus(LanguageManager.Instance.GetString("Main_Status_Failed", "No platform"));
                return;
            }

            bool needProxy = ShouldUseProxy(platform);
            if (needProxy != _currentWebViewUsesProxy)
            {
                // Proxy setting differs, need to reinitialize WebView2 (this also navigates and waits)
                await ReinitializeWebViewAsync(platform);
            }
            else if (await EnsureWebViewReadyAsync())
            {
                // Only navigate when the WebView is on a different site — an already
                // loaded page of the same platform is injected into directly
                if (!string.IsNullOrEmpty(platform.Url) && !IsSameSite(webView.CoreWebView2.Source, platform.Url))
                {
                    UpdateStatus(LanguageManager.Instance.GetString("Main_Status_NavigatingTo", platform.Name));
                    await NavigateAndWaitAsync(platform.Url);
                }
            }
            else
            {
                return;
            }

            if (!await EnsureWebViewReadyAsync()) return;

            // Update platform dropdown to reflect current platform
            var currentSelected = cmbPlatforms.SelectedItem as AiPlatform;
            if (currentSelected?.Id != platform.Id)
            {
                cmbPlatforms.SelectionChanged -= CmbPlatforms_SelectionChanged;
                cmbPlatforms.SelectedItem = platform;
                cmbPlatforms.SelectionChanged += CmbPlatforms_SelectionChanged;
            }

            // The document navigation being complete does not mean the SPA is usable yet —
            // wait until the input element actually exists and stopped being re-created.
            UpdateStatus(LanguageManager.Instance["Main_Status_WaitingPage"]);
            var ready = await _pageInjector.WaitPageReadyAsync(webView, platform.InputSelector);
            if (!ready.Success)
            {
                Logger.LogError($"Page not ready before injection ({platform.Name}): {ready.Reason}");
                UpdateStatus(LanguageManager.Instance.GetString("Main_Status_Failed", ready.Message));
                return;
            }

            // Starting a new chat can reload the whole page, which would wipe the prompt,
            // so it happens as its own step with its own wait.
            if (!await StartNewChatAndWaitAsync(platform)) return;

            bool autoSubmit = _settings?.AutoSubmit ?? true;
            string statusMsg = !string.IsNullOrEmpty(actionName)
                ? LanguageManager.Instance.GetString("Main_Status_Executing", actionName)
                : (autoSubmit ? LanguageManager.Instance["Main_Status_Submitting"] : LanguageManager.Instance["Main_Status_Injecting"]);
            UpdateStatus(statusMsg);

            var result = await _pageInjector.InjectAndSubmitAsync(webView, prompt, platform?.InputSelector, platform?.SubmitSelector, autoSubmit);
            if (!result.Success)
            {
                Logger.LogError($"Injection failed ({platform.Name}): {result.Reason}");
            }
            UpdateStatus(result.Success
                ? LanguageManager.Instance.GetString("Main_Status_Success", result.Message)
                : LanguageManager.Instance.GetString("Main_Status_Failed", result.Message));
        }

        /// <summary>
        /// True when the WebView is already on the platform's site. Only the host is
        /// compared, because these sites are SPAs that rewrite the path as soon as a
        /// conversation exists ("/new" → "/chat/&lt;id&gt;", "/" → "/a/chat/s/&lt;id&gt;").
        /// Comparing the full URL would make every action reload the whole page for
        /// nothing — starting a fresh conversation is <see cref="StartNewChatAndWaitAsync"/>'s job.
        /// </summary>
        private static bool IsSameSite(string currentUrl, string platformUrl)
        {
            if (string.IsNullOrEmpty(currentUrl) || string.IsNullOrEmpty(platformUrl)) return false;
            if (string.Equals(currentUrl.TrimEnd('/'), platformUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                return true;

            if (!Uri.TryCreate(currentUrl, UriKind.Absolute, out var current) ||
                !Uri.TryCreate(platformUrl, UriKind.Absolute, out var target))
            {
                return false;
            }

            // about:blank and the like have no host — never treat those as "already there"
            if (string.IsNullOrEmpty(current.Host) || string.IsNullOrEmpty(target.Host)) return false;

            return string.Equals(StripWww(current.Host), StripWww(target.Host), StringComparison.OrdinalIgnoreCase);
        }

        private static string StripWww(string host)
        {
            return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host.Substring(4) : host;
        }

        /// <summary>
        /// Navigates and waits until the navigation completed and the page stopped
        /// navigating (client-side redirects) for <see cref="NavigationQuietMs"/>.
        /// Returns false on timeout or a failed navigation; callers keep going anyway
        /// because the readiness probe is the real gate.
        /// </summary>
        private async Task<bool> NavigateAndWaitAsync(string url, int timeoutMs = NavigationTimeoutMs)
        {
            var core = webView?.CoreWebView2;
            if (core == null || string.IsNullOrEmpty(url)) return false;

            var navTcs = new TaskCompletionSource<bool>();
            DateTime lastNavStart = DateTime.UtcNow;
            bool lastSuccess = false;

            void onStarting(object s, CoreWebView2NavigationStartingEventArgs args) { lastNavStart = DateTime.UtcNow; }
            void onCompleted(object s, CoreWebView2NavigationCompletedEventArgs args)
            {
                lastSuccess = args.IsSuccess;
                navTcs.TrySetResult(args.IsSuccess);
            }

            // Subscribe before navigating so a fast completion cannot be missed
            core.NavigationStarting += onStarting;
            core.NavigationCompleted += onCompleted;
            try
            {
                core.Navigate(url);

                if (await Task.WhenAny(navTcs.Task, Task.Delay(timeoutMs)) != navTcs.Task)
                {
                    Logger.LogError($"Navigation to {url} timed out after {timeoutMs}ms");
                    return false;
                }

                // A redirect right after loading aborts the first navigation and starts
                // another one; wait until nothing new has started for a moment.
                var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                while (DateTime.UtcNow < deadline &&
                       (DateTime.UtcNow - lastNavStart).TotalMilliseconds < NavigationQuietMs)
                {
                    await Task.Delay(100);
                }

                return lastSuccess;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Navigation to {url} failed", ex);
                return false;
            }
            finally
            {
                core.NavigationStarting -= onStarting;
                core.NavigationCompleted -= onCompleted;
            }
        }

        /// <summary>
        /// Clicks the platform's new chat button, then waits out whatever it triggered:
        /// some platforms reload the page, which would discard a prompt injected too early.
        /// Returns false only when the page did not come back in a usable state.
        /// </summary>
        private async Task<bool> StartNewChatAndWaitAsync(AiPlatform platform)
        {
            var core = webView?.CoreWebView2;
            if (core == null) return false;

            string token = Guid.NewGuid().ToString("N");
            bool navStarted = false;
            DateTime lastNavStart = DateTime.MinValue;
            var navCompletedTcs = new TaskCompletionSource<bool>();

            void onStarting(object s, CoreWebView2NavigationStartingEventArgs args)
            {
                navStarted = true;
                lastNavStart = DateTime.UtcNow;
            }
            void onCompleted(object s, CoreWebView2NavigationCompletedEventArgs args) { navCompletedTcs.TrySetResult(args.IsSuccess); }

            core.NavigationStarting += onStarting;
            core.NavigationCompleted += onCompleted;
            try
            {
                bool clicked = await _pageInjector.StartNewChatAsync(webView, platform.NewChatSelector, token);
                if (!clicked)
                {
                    // Either there is no new chat button, or the click already destroyed the
                    // script context before it could answer. The marker tells the two apart:
                    // it only survives as long as the original document does.
                    string marker = await _pageInjector.GetPageTokenAsync(webView);
                    if (!navStarted && marker == token)
                    {
                        // Nothing happened, the current page is still the one to inject into
                        return true;
                    }
                }

                UpdateStatus(LanguageManager.Instance["Main_Status_NewChat"]);

                // Watch for a reload caused by the click
                var observeDeadline = DateTime.UtcNow.AddMilliseconds(NewChatObserveMs);
                while (DateTime.UtcNow < observeDeadline && !navStarted)
                {
                    await Task.Delay(100);
                }

                if (!navStarted)
                {
                    // Backstop for document swaps that raise no navigation event:
                    // the marker only survives inside the original document.
                    string current = await _pageInjector.GetPageTokenAsync(webView);
                    navStarted = current != token;
                }

                if (navStarted)
                {
                    await Task.WhenAny(navCompletedTcs.Task, Task.Delay(NavigationTimeoutMs));

                    var deadline = DateTime.UtcNow.AddMilliseconds(NavigationTimeoutMs);
                    while (DateTime.UtcNow < deadline &&
                           (DateTime.UtcNow - lastNavStart).TotalMilliseconds < NavigationQuietMs)
                    {
                        await Task.Delay(100);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("New chat handling failed", ex);
            }
            finally
            {
                core.NavigationStarting -= onStarting;
                core.NavigationCompleted -= onCompleted;
            }

            // The DOM was rebuilt (either by the reload or by the SPA) — wait for it again
            UpdateStatus(LanguageManager.Instance["Main_Status_WaitingPage"]);
            var ready = await _pageInjector.WaitPageReadyAsync(webView, platform.InputSelector);
            if (!ready.Success)
            {
                Logger.LogError($"Page not ready after new chat ({platform.Name}): {ready.Reason}");
                UpdateStatus(LanguageManager.Instance.GetString("Main_Status_Failed", ready.Message));
                return false;
            }
            return true;
        }

        private async void ExecutePrompt(string prompt)
        {
            var platform = _settings.GetActivePlatform();
            await EnsurePlatformAndExecuteAsync(platform, prompt);
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
                    if (!IsSameSite(webView.CoreWebView2.Source, platform.Url))
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

        private Point _dragStartPoint;
        private bool _isPotentialDrag;

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                _isPotentialDrag = false;
                ToggleMaximize();
                return;
            }

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                if (this.WindowState == WindowState.Maximized)
                {
                    _isPotentialDrag = true;
                    _dragStartPoint = e.GetPosition(this);
                }
                else
                {
                    this.DragMove();
                }
            }
        }

        private void TitleBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPotentialDrag && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPoint = e.GetPosition(this);
                Vector diff = _dragStartPoint - currentPoint;
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isPotentialDrag = false;

                    var screenPos = PointToScreen(currentPoint);
                    double percentX = currentPoint.X / this.ActualWidth;

                    this.WindowState = WindowState.Normal;
                    this.Left = screenPos.X - (this.RestoreBounds.Width * percentX);
                    this.Top = screenPos.Y - _dragStartPoint.Y;

                    this.DragMove();
                }
            }
        }

        private void TitleBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isPotentialDrag = false;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void ToggleMaximize()
        {
            this.WindowState = this.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            UpdateMaximizeRestoreState();
        }

        private void UpdateMaximizeRestoreState()
        {
            if (this.WindowState == WindowState.Maximized)
            {
                if (pathMaximize != null)
                    pathMaximize.Data = Geometry.Parse("M 2.5,0.5 L 9.5,0.5 L 9.5,7.5 L 7.5,7.5 M 2.5,2.5 L 2.5,0.5 M 0.5,2.5 L 7.5,2.5 L 7.5,9.5 L 0.5,9.5 Z");
                if (btnMaximize != null)
                    btnMaximize.ToolTip = LanguageManager.Instance["Main_Restore"];
                if (mainBorder != null)
                {
                    mainBorder.CornerRadius = new CornerRadius(0);
                    mainBorder.BorderThickness = new Thickness(0);
                }
            }
            else if (this.WindowState == WindowState.Normal)
            {
                if (pathMaximize != null)
                    pathMaximize.Data = Geometry.Parse("M 0.5,0.5 L 9.5,0.5 L 9.5,9.5 L 0.5,9.5 Z");
                if (btnMaximize != null)
                    btnMaximize.ToolTip = LanguageManager.Instance["Main_Maximize"];
                if (mainBorder != null)
                {
                    mainBorder.CornerRadius = new CornerRadius(8);
                    mainBorder.BorderThickness = new Thickness(1);
                }
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            ShowSettings();
        }

        private void UpdateCheckService_UpdateAvailable(object sender, UpdateCheckService.UpdateInfo info)
        {
            Dispatcher.Invoke(() => ShowUpdateIndicator(info));
        }

        private void LanguageManager_LanguageChanged(object sender, EventArgs e)
        {
            // 提示文案带版本号，无法通过绑定自动刷新
            ShowUpdateIndicator(_availableUpdate);
            UpdateMaximizeRestoreState();
        }

        /// <summary>
        /// 显示/隐藏标题栏的新版本提示
        /// </summary>
        private void ShowUpdateIndicator(UpdateCheckService.UpdateInfo info)
        {
            _availableUpdate = info;

            if (info == null)
            {
                btnUpdate.Visibility = Visibility.Collapsed;
                btnUpdate.ToolTip = null;
                return;
            }

            btnUpdate.ToolTip = LanguageManager.Instance.GetString("Main_UpdateAvailable_Tip", info.LatestVersion, info.CurrentVersion);
            btnUpdate.Visibility = Visibility.Visible;
        }

        private void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            UpdateCheckService.OpenUpdatePage(_availableUpdate?.UpdateUrl);
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
            this.StateChanged -= MainWindow_StateChanged;
            // 静态事件会持有窗口引用，窗口可能被重建，必须解绑
            UpdateCheckService.UpdateAvailable -= UpdateCheckService_UpdateAvailable;
            LanguageManager.Instance.LanguageChanged -= LanguageManager_LanguageChanged;
            base.OnClosed(e);
        }

        private void UpdateStatus(string message)
        {
            txtStatus.Text = message;
        }
    }
}
