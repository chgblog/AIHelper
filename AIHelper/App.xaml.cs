using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using AIHelper.Services;
using AIHelper.Views;

namespace AIHelper
{
    public partial class App : Application
    {
        private Mutex _mutex;
        private System.Windows.Forms.NotifyIcon _notifyIcon;
        private MainWindow _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
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

            _mainWindow = new MainWindow();
            _mainWindow.Show();
            _mainWindow.Hide(); // Start minimized

            base.OnStartup(e);
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            _notifyIcon.Visible = true;
            _notifyIcon.Text = "AIHelper";
            _notifyIcon.DoubleClick += NotifyIcon_DoubleClick;

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            
            var showItem = new System.Windows.Forms.ToolStripMenuItem("显示主窗口");
            showItem.Click += (s, e) => _mainWindow?.ShowAndActivate();
            contextMenu.Items.Add(showItem);

            var exitItem = new System.Windows.Forms.ToolStripMenuItem("退出");
            exitItem.Click += (s, e) => 
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                Shutdown();
            };
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void NotifyIcon_DoubleClick(object sender, EventArgs e)
        {
            _mainWindow?.ShowAndActivate();
        }

        private void BringToFront()
        {
            var currentProcess = Process.GetCurrentProcess();
            var processes = Process.GetProcessesByName(currentProcess.ProcessName);
            var otherProcess = processes.FirstOrDefault(p => p.Id != currentProcess.Id);
            if (otherProcess != null)
            {
                // Basic logic, more complex Interop might be needed
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            HotkeyService.Instance?.UnregisterAll();
            base.OnExit(e);
        }
    }
}
