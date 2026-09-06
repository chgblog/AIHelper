// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Text.RegularExpressions;
using System.Windows;
using AIHelper.Models;
using AIHelper.Services;

namespace AIHelper.Views
{
    public partial class AutoVisitEditWindow : Window
    {
        private readonly AutoVisitConfig _config;

        public AutoVisitEditWindow(AutoVisitConfig config, bool isNew = false)
        {
            InitializeComponent();
            _config = config;

            this.Title = isNew
                ? LanguageManager.Instance["AutoVisit_Edit_Title_Add"]
                : LanguageManager.Instance["AutoVisit_Edit_Title_Edit"];

            // Load values
            txtUrl.Text = config.Url ?? "";
            txtLinkMatchRegex.Text = config.LinkMatchRegex ?? "";
            txtMaxVisitCount.Text = (config.MaxVisitCount > 0 ? config.MaxVisitCount : 50).ToString();
            txtNextPageSelector.Text = config.NextPageSelector ?? "";
            txtMinLinkDelay.Text = (config.MinLinkDelaySeconds >= 0 ? config.MinLinkDelaySeconds : 3).ToString();
            txtMaxLinkDelay.Text = (config.MaxLinkDelaySeconds >= config.MinLinkDelaySeconds ? config.MaxLinkDelaySeconds : 8).ToString();
            txtMinRefreshInterval.Text = (config.MinRefreshIntervalMinutes > 0 ? config.MinRefreshIntervalMinutes : 30).ToString();
            txtMaxRefreshInterval.Text = (config.MaxRefreshIntervalMinutes >= config.MinRefreshIntervalMinutes ? config.MaxRefreshIntervalMinutes : 60).ToString();
            txtExcludedUrls.Text = config.ExcludedUrls ?? "";
            chkIsEnabled.IsChecked = config.IsEnabled;
        }

        private void BtnPickNextPage_Click(object sender, RoutedEventArgs e)
        {
            string url = txtUrl.Text?.Trim();
            if (string.IsNullOrWhiteSpace(url) || (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(
                    LanguageManager.Instance["PlatformEdit_InvalidUrlWarn"],
                    LanguageManager.Instance["Notice"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                var picker = new ElementPickerWindow(url);
                picker.Owner = this;
                if (picker.ShowDialog() == true && !string.IsNullOrWhiteSpace(picker.PickedSelector))
                {
                    txtNextPageSelector.Text = picker.PickedSelector;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error opening ElementPickerWindow for auto visit next page", ex);
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            string url = txtUrl.Text?.Trim();
            string regex = txtLinkMatchRegex.Text?.Trim();
            string rawLimit = txtMaxVisitCount.Text?.Trim();
            string rawMinDelay = txtMinLinkDelay.Text?.Trim();
            string rawMaxDelay = txtMaxLinkDelay.Text?.Trim();
            string rawMinInt = txtMinRefreshInterval.Text?.Trim();
            string rawMaxInt = txtMaxRefreshInterval.Text?.Trim();

            // 1. URL validation
            if (string.IsNullOrWhiteSpace(url) || (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(
                    LanguageManager.Instance["AutoVisit_Edit_InvalidUrl"],
                    LanguageManager.Instance["Notice"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                txtUrl.Focus();
                return;
            }

            // 2. Regex validation
            if (string.IsNullOrWhiteSpace(regex))
            {
                MessageBox.Show(
                    LanguageManager.Instance.GetString("AutoVisit_Edit_InvalidRegex", "规则不能为空"),
                    LanguageManager.Instance["Notice"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                txtLinkMatchRegex.Focus();
                return;
            }

            try
            {
                _ = new Regex(regex, RegexOptions.IgnoreCase);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    LanguageManager.Instance.GetString("AutoVisit_Edit_InvalidRegex", ex.Message),
                    LanguageManager.Instance["Notice"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                txtLinkMatchRegex.Focus();
                return;
            }

            // 3. Limit count validation
            if (!int.TryParse(rawLimit, out int limit) || limit <= 0)
            {
                MessageBox.Show(
                    LanguageManager.Instance["AutoVisit_Edit_InvalidLimit"],
                    LanguageManager.Instance["Notice"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                txtMaxVisitCount.Focus();
                return;
            }

            // 4. Link delay validation
            if (!int.TryParse(rawMinDelay, out int minDelay) || minDelay < 0 ||
                !int.TryParse(rawMaxDelay, out int maxDelay) || maxDelay < minDelay)
            {
                MessageBox.Show(
                    LanguageManager.Instance["AutoVisit_Edit_InvalidLinkDelay"],
                    LanguageManager.Instance["Notice"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                txtMinLinkDelay.Focus();
                return;
            }

            // 5. Refresh interval validation
            if (!int.TryParse(rawMinInt, out int minInterval) || minInterval <= 0 ||
                !int.TryParse(rawMaxInt, out int maxInterval) || maxInterval < minInterval)
            {
                MessageBox.Show(
                    LanguageManager.Instance["AutoVisit_Edit_InvalidInterval"],
                    LanguageManager.Instance["Notice"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                txtMinRefreshInterval.Focus();
                return;
            }

            // Save to config
            _config.Url = url;
            _config.LinkMatchRegex = regex;
            _config.MaxVisitCount = limit;
            _config.NextPageSelector = txtNextPageSelector.Text?.Trim() ?? "";
            _config.MinLinkDelaySeconds = minDelay;
            _config.MaxLinkDelaySeconds = maxDelay;
            _config.MinRefreshIntervalMinutes = minInterval;
            _config.MaxRefreshIntervalMinutes = maxInterval;
            _config.ExcludedUrls = txtExcludedUrls.Text?.Trim() ?? "";
            _config.IsEnabled = chkIsEnabled.IsChecked == true;

            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
