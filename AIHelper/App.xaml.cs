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

            bool createdNew;
            _mutex = new Mutex(true, "AIHelper_SingleInstance", out createdNew);

            if (!createdNew)
            {
                BringToFront();
                Shutdown();
                return;
            }

            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            InitializeTrayIcon();

            _mainWindow = MainWindowInstance;

            var settings = SettingsService.Instance.Load();

            bool startVisible = e.Args != null && e.Args.Any(arg => 
                string.Equals(arg, "--show", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(arg, "-show", StringComparison.OrdinalIgnoreCase));

            bool startMinimized = e.Args != null && e.Args.Any(arg => 
                string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(arg, "-minimized", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--hide", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-hide", StringComparison.OrdinalIgnoreCase));

            bool shouldShow = startVisible || (!startMinimized && settings.ShowMainWindowOnStartup);

            if (shouldShow)
            {
                Logger.LogInfo("Starting with main window visible.");
                _mainWindow.ShowAndActivate();
            }
            else
            {
                Logger.LogInfo("Starting hidden in system tray.");
            }

            base.OnStartup(e);

            // 启动后台自动检测新版本
            if (settings.AutoCheckUpdate)
            {
                UpdateCheckService.StartDelayedCheck(settings);
            }
        }

        private void SetupExceptionHandling()
        {
            this.DispatcherUnhandledException += (s, e) =>
            {
                Logger.LogCrash("DispatcherUnhandledException", e.Exception);
                MessageBox.Show($"程序遇到未处理的异常：\n{e.Exception.Message}\n\n详细日志已保存至:\n{Logger.GetLogFolderPath()}", "AI助手 错误", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    Logger.LogCrash("AppDomain.UnhandledException", ex);
                    MessageBox.Show($"程序遇到严重内部错误：\n{ex.Message}\n\n详细日志已保存至:\n{Logger.GetLogFolderPath()}", "AI助手 致命错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
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
            base.OnExit(e);
        }
    }
}

