// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System.Windows;
using AIHelper.Models;
using AIHelper.Services;

namespace AIHelper.Views
{
    public partial class PlatformEditWindow : Window
    {
        private readonly AiPlatform _platform;

        public PlatformEditWindow(AiPlatform platform, string title = null)
        {
            InitializeComponent();
            if (!string.IsNullOrEmpty(title))
            {
                this.Title = title;
            }
            else
            {
                this.Title = LanguageManager.Instance["PlatformEdit_Title_Edit"];
            }
            _platform = platform;

            // Load current values
            txtName.Text = platform.Name ?? "";
            txtUrl.Text = platform.Url ?? "";
            txtNewChatSelector.Text = platform.NewChatSelector ?? "";
            txtInputSelector.Text = platform.InputSelector ?? "";
            txtSubmitSelector.Text = platform.SubmitSelector ?? "";
        }

        private void BtnPickNewChat_Click(object sender, RoutedEventArgs e)
        {
            string url = txtUrl.Text?.Trim();
            if (string.IsNullOrEmpty(url) || !url.StartsWith("http"))
            {
                MessageBox.Show(LanguageManager.Instance["PlatformEdit_InvalidUrlWarn"], LanguageManager.Instance["Notice"], MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var picker = new ElementPickerWindow(url);
            picker.Owner = this;
            if (picker.ShowDialog() == true && !string.IsNullOrEmpty(picker.PickedSelector))
            {
                txtNewChatSelector.Text = picker.PickedSelector;
            }
        }

        private void BtnPickInput_Click(object sender, RoutedEventArgs e)
        {
            string url = txtUrl.Text?.Trim();
            if (string.IsNullOrEmpty(url) || !url.StartsWith("http"))
            {
                MessageBox.Show(LanguageManager.Instance["PlatformEdit_InvalidUrlWarn"], LanguageManager.Instance["Notice"], MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var picker = new ElementPickerWindow(url);
            picker.Owner = this;
            if (picker.ShowDialog() == true && !string.IsNullOrEmpty(picker.PickedSelector))
            {
                txtInputSelector.Text = picker.PickedSelector;
            }
        }

        private void BtnPickSubmit_Click(object sender, RoutedEventArgs e)
        {
            string url = txtUrl.Text?.Trim();
            if (string.IsNullOrEmpty(url) || !url.StartsWith("http"))
            {
                MessageBox.Show(LanguageManager.Instance["PlatformEdit_InvalidUrlWarn"], LanguageManager.Instance["Notice"], MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var picker = new ElementPickerWindow(url);
            picker.Owner = this;
            if (picker.ShowDialog() == true && !string.IsNullOrEmpty(picker.PickedSelector))
            {
                txtSubmitSelector.Text = picker.PickedSelector;
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            string name = txtName.Text?.Trim();
            string url = txtUrl.Text?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(LanguageManager.Instance["PlatformEdit_EmptyNameWarn"], LanguageManager.Instance["Notice"], MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show(LanguageManager.Instance["PlatformEdit_EmptyUrlWarn"], LanguageManager.Instance["Notice"], MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _platform.Name = name;
            _platform.Url = url;
            _platform.NewChatSelector = txtNewChatSelector.Text?.Trim();
            _platform.InputSelector = txtInputSelector.Text?.Trim();
            _platform.SubmitSelector = txtSubmitSelector.Text?.Trim();

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
