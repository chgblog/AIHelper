// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
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
    public enum AppSelectionResultMode
    {
        Append,
        Replace
    }

    public partial class AppSelectionWindow : Window
    {
        private List<AppItem> _allApps = new List<AppItem>();
        private List<AppItem> _filteredApps = new List<AppItem>();
        private HashSet<string> _initialSelectedApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 用户选择的结果模式（追加 / 覆盖）
        /// </summary>
        public AppSelectionResultMode ResultMode { get; private set; } = AppSelectionResultMode.Append;

        /// <summary>
        /// 用户最终勾选的进程名称列表
        /// </summary>
        public List<string> SelectedProcessNames { get; private set; } = new List<string>();

        public AppSelectionWindow(string currentRawAppList = null)
        {
            InitializeComponent();
            ParseInitialSelectedApps(currentRawAppList);
            LoadApplications();
        }

        private void ParseInitialSelectedApps(string rawList)
        {
            if (string.IsNullOrWhiteSpace(rawList)) return;
            var parts = rawList.Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                string clean = part.Trim();
                if (!clean.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    clean += ".exe";
                }
                if (!string.IsNullOrEmpty(clean))
                {
                    _initialSelectedApps.Add(clean);
                }
            }
        }

        private void LoadApplications()
        {
            bool onlyWithWindow = chkOnlyWindowApps.IsChecked == true;
            _allApps = AppInfoService.GetRunningApplications(onlyWithWindow);

            // 还原初始勾选状态
            foreach (var app in _allApps)
            {
                if (_initialSelectedApps.Contains(app.ProcessName))
                {
                    app.IsSelected = true;
                }
                app.PropertyChanged += App_PropertyChanged;
            }

            ApplyFilter();
        }

        private void App_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppItem.IsSelected))
            {
                UpdateSelectedCount();
            }
        }

        private void ApplyFilter()
        {
            string keyword = txtSearch.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(keyword))
            {
                _filteredApps = _allApps.ToList();
            }
            else
            {
                _filteredApps = _allApps.Where(a =>
                    (a.AppName != null && a.AppName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (a.ProcessName != null && a.ProcessName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (a.MainWindowTitle != null && a.MainWindowTitle.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                ).ToList();
            }

            lvApps.ItemsSource = _filteredApps;
            tbEmptyHint.Visibility = _filteredApps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateSelectedCount();
        }

        private void UpdateSelectedCount()
        {
            int selectedCount = _allApps.Count(a => a.IsSelected);
            string format = LanguageManager.Instance["AppSelection_SelectedCount"];
            tbSelectedCount.Text = string.Format(format, selectedCount);
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ChkOnlyWindowApps_Click(object sender, RoutedEventArgs e)
        {
            // 保存当前已勾选状态，重新加载
            var currentlyChecked = _allApps.Where(a => a.IsSelected).Select(a => a.ProcessName).ToList();
            foreach (var p in currentlyChecked)
            {
                _initialSelectedApps.Add(p);
            }

            LoadApplications();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            var currentlyChecked = _allApps.Where(a => a.IsSelected).Select(a => a.ProcessName).ToList();
            foreach (var p in currentlyChecked)
            {
                _initialSelectedApps.Add(p);
            }

            LoadApplications();
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var app in _filteredApps)
            {
                app.IsSelected = true;
            }
            UpdateSelectedCount();
        }

        private void BtnInvertSelect_Click(object sender, RoutedEventArgs e)
        {
            foreach (var app in _filteredApps)
            {
                app.IsSelected = !app.IsSelected;
            }
            UpdateSelectedCount();
        }

        private void BtnClearSelect_Click(object sender, RoutedEventArgs e)
        {
            foreach (var app in _allApps)
            {
                app.IsSelected = false;
            }
            UpdateSelectedCount();
        }

        private void LvApps_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lvApps.SelectedItem is AppItem item)
            {
                item.IsSelected = !item.IsSelected;
            }
        }

        private void BtnAppend_Click(object sender, RoutedEventArgs e)
        {
            ResultMode = AppSelectionResultMode.Append;
            ConfirmAndClose();
        }

        private void BtnReplace_Click(object sender, RoutedEventArgs e)
        {
            ResultMode = AppSelectionResultMode.Replace;
            ConfirmAndClose();
        }

        private void ConfirmAndClose()
        {
            SelectedProcessNames = _allApps
                .Where(a => a.IsSelected)
                .Select(a => a.ProcessName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
