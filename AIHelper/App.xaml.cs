// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AIHelper.Services;
using AIHelper.Views;

namespace AIHelper
{
    public partial class App : Application
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int RegisterWindowMessage(string message);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);

        public const int HWND_BROADCAST = 0xffff;
        public static readonly int WM_SHOWFIRSTINSTANCE = RegisterWindowMessage("AIHelper_ShowFirstInstance");

        private Mutex _mutex;
        private System.Windows.Forms.NotifyIcon _notifyIcon;
        private System.Drawing.Icon _trayIcon;
        private MainWindow _mainWindow;

        public MainWindow MainWindowInstance
        {
            get
            {
                if (_mainWindow == null || _mainWindow.IsClosed)
                {
                    Logger.LogWarning("MainWindow was closed or null. Creating new instance.");
                    _mainWindow = new MainWindow();
                    new System.Windows.Interop.WindowInteropHelper(_mainWindow).EnsureHandle();
                }
                return _mainWindow;
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            SetupExceptionHandling();
            Logger.LogInfo($"App starting up with args: {string.Join(" ", e.Args)}");

            if (e.Args != null)
            {
                if (e.Args.Any(a => string.Equals(a, "--register-context-menu", StringComparison.OrdinalIgnoreCase)))
                {
                    FileContextMenuService.RegisterDirect(machineWide: true, enable: true);
                    Shutdown(0);
                    return;
                }
                if (e.Args.Any(a => string.Equals(a, "--unregister-context-menu", StringComparison.OrdinalIgnoreCase)))
                {
                    FileContextMenuService.RegisterDirect(machineWide: true, enable: false);
                    Shutdown(0);
                    return;
                }
            }

            bool createdNew;
            _mutex = new Mutex(true, "AIHelper_SingleInstance", out createdNew);

            if (!createdNew)
            {
                bool sent = false;
                if (e.Args != null && e.Args.Length > 0)
                {
                    sent = SingleInstanceIpcService.SendArgsToRunningInstance(e.Args);
                }

                if (!sent)
                {
                    BringToFront();
                }
                Shutdown();
                return;
            }

            // 启动单实例 IPC 服务，监听后续从右键菜单等发来的参数
            SingleInstanceIpcService.StartServer(args =>
            {
                Dispatcher.Invoke(() =>
                {
                    HandleCommandLineArgs(args);
                });
            });

            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            InitializeTrayIcon();

            _mainWindow = MainWindowInstance;

            var settings = SettingsService.Instance.Load();

            string initialFile = ExtractFileArg(e.Args);
            bool hasInitialFile = !string.IsNullOrEmpty(initialFile);

            bool startVisible = e.Args != null && e.Args.Any(arg => 
                string.Equals(arg, "--show", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(arg, "-show", StringComparison.OrdinalIgnoreCase));

            bool startMinimized = e.Args != null && e.Args.Any(arg => 
                string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(arg, "-minimized", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--hide", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-hide", StringComparison.OrdinalIgnoreCase));

            bool shouldShow = !hasInitialFile && (startVisible || (!startMinimized && settings.ShowMainWindowOnStartup));

            if (shouldShow)
            {
                Logger.LogInfo("Starting with main window visible.");
                _mainWindow.ShowAndActivate();
            }
            else
            {
                Logger.LogInfo("Starting hidden in system tray.");
            }

            if (hasInitialFile)
            {
                _mainWindow.HandleFileContextMenu(initialFile);
            }

            base.OnStartup(e);

            // 启动后台自动检测新版本
            if (settings.AutoCheckUpdate)
            {
                UpdateCheckService.StartDelayedCheck(settings);
            }
        }

        private void HandleCommandLineArgs(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                MainWindowInstance.ShowAndActivate();
                return;
            }

            string file = ExtractFileArg(args);
            if (!string.IsNullOrEmpty(file))
            {
                MainWindowInstance.HandleFileContextMenu(file);
                return;
            }

            bool show = args.Any(arg => 
                string.Equals(arg, "--show", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(arg, "-show", StringComparison.OrdinalIgnoreCase));
            if (show)
            {
                MainWindowInstance.ShowAndActivate();
            }
        }

        public static string ExtractFileArg(string[] args)
        {
            return SingleInstanceIpcService.ExtractFileArg(args);
        }

        private void SetupExceptionHandling()
        {
            this.DispatcherUnhandledException += (s, e) =>
            {
                Logger.LogCrash($"DispatcherUnhandledException (Thread={Thread.CurrentThread.ManagedThreadId})", e.Exception);
                MessageBox.Show($"程序遇到未处理的异常：\n{e.Exception.Message}\n\n详细日志已保存至:\n{Logger.GetLogFolderPath()}", "AI助手 错误", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                // 进程即将被终止，先落盘日志再弹窗，避免弹窗阻塞导致什么都没记下来
                var ex = e.ExceptionObject as Exception;
                Logger.LogCrash($"AppDomain.UnhandledException (IsTerminating={e.IsTerminating}, Thread={Thread.CurrentThread.ManagedThreadId})", ex);

                if (ex == null)
                {
                    Logger.LogCrash("AppDomain.UnhandledException (non-Exception object)", new Exception(e.ExceptionObject?.ToString() ?? "null"));
                    return;
                }

                try
                {
                    MessageBox.Show($"程序遇到严重内部错误：\n{ex.Message}\n\n详细日志已保存至:\n{Logger.GetLogFolderPath()}", "AI助手 致命错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch { }
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Logger.LogError("UnobservedTaskException", e.Exception);
                e.SetObserved();
            };
        }

        private System.Windows.Forms.ToolStripMenuItem _showItem;
        private System.Windows.Forms.ToolStripMenuItem _settingsItem;
        private System.Windows.Forms.ToolStripMenuItem _selectionToolbarItem;
        private System.Windows.Forms.ToolStripMenuItem _exitItem;

        private void InitializeTrayIcon()
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            
            try
            {
                var iconStreamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/icon.ico"));
                if (iconStreamInfo != null)
                {
                    using (var stream = iconStreamInfo.Stream)
                    using (var tempIcon = new System.Drawing.Icon(stream))
                    {
                        _trayIcon = (System.Drawing.Icon)tempIcon.Clone();
                    }
                    _notifyIcon.Icon = _trayIcon;
                }
                else
                {
                    _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load tray icon", ex);
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            }

            _notifyIcon.Visible = true;
            _notifyIcon.Text = "AIHelper";
            _notifyIcon.DoubleClick += NotifyIcon_DoubleClick;

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            
            _showItem = new System.Windows.Forms.ToolStripMenuItem();
            _showItem.Click += (s, e) => MainWindowInstance.ShowAndActivate();
            contextMenu.Items.Add(_showItem);

            _settingsItem = new System.Windows.Forms.ToolStripMenuItem();
            _settingsItem.Click += (s, e) => MainWindowInstance.ShowSettings();
            contextMenu.Items.Add(_settingsItem);

            _selectionToolbarItem = new System.Windows.Forms.ToolStripMenuItem();
            _selectionToolbarItem.Click += (s, e) =>
            {
                var settings = SettingsService.Instance.Load();
                settings.EnableSelectionToolbar = !settings.EnableSelectionToolbar;
                SettingsService.Instance.Save(settings);
                MainWindowInstance.RefreshSettings();
                UpdateTrayMenuText();
            };
            contextMenu.Items.Add(_selectionToolbarItem);

            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            _exitItem = new System.Windows.Forms.ToolStripMenuItem();
            _exitItem.Click += (s, e) => 
            {
                if (_mainWindow != null)
                {
                    _mainWindow.IsExiting = true;
                    _mainWindow.Close();
                }
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                Shutdown();
            };
            contextMenu.Items.Add(_exitItem);

            contextMenu.Opening += (s, e) => UpdateTrayMenuText();

            _notifyIcon.ContextMenuStrip = contextMenu;

            LanguageManager.Instance.LanguageChanged += (s, e) => UpdateTrayMenuText();
            UpdateTrayMenuText();
        }

        public void UpdateTrayMenu()
        {
            UpdateTrayMenuText();
        }

        private void UpdateTrayMenuText()
        {
            var settings = SettingsService.Instance.Load();
            if (_showItem != null) _showItem.Text = LanguageManager.Instance["Tray_Show"];
            if (_settingsItem != null) _settingsItem.Text = LanguageManager.Instance["Tray_Settings"];
            if (_selectionToolbarItem != null)
            {
                _selectionToolbarItem.Text = LanguageManager.Instance["Tray_SelectionToolbar"];
                _selectionToolbarItem.Checked = settings?.EnableSelectionToolbar ?? true;
            }
            if (_exitItem != null) _exitItem.Text = LanguageManager.Instance["Tray_Exit"];
        }

        private void NotifyIcon_DoubleClick(object sender, EventArgs e)
        {
            MainWindowInstance.ShowAndActivate();
        }

        private void BringToFront()
        {
            Logger.LogInfo("Another instance detected. Sending WM_SHOWFIRSTINSTANCE to bring existing window to front.");
            PostMessage((IntPtr)HWND_BROADCAST, WM_SHOWFIRSTINSTANCE, IntPtr.Zero, IntPtr.Zero);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            _trayIcon?.Dispose();
            TextSelectionService.Instance?.Dispose();
            HotkeyService.Instance?.UnregisterAll();
            SingleInstanceIpcService.StopServer();
            base.OnExit(e);
        }
    }
}

