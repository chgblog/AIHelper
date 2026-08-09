using System.Windows;
using AIHelper.Models;

namespace AIHelper.Views
{
    public partial class PlatformEditWindow : Window
    {
        private readonly AiPlatform _platform;

        public PlatformEditWindow(AiPlatform platform, string title = "编辑平台")
        {
            InitializeComponent();
            if (!string.IsNullOrEmpty(title))
            {
                this.Title = title;
            }
            _platform = platform;

            // Load current values
            txtName.Text = platform.Name ?? "";
            txtUrl.Text = platform.Url ?? "";
            txtInputSelector.Text = platform.InputSelector ?? "";
            txtSubmitSelector.Text = platform.SubmitSelector ?? "";
        }

        private void BtnPickInput_Click(object sender, RoutedEventArgs e)
        {
            string url = txtUrl.Text?.Trim();
            if (string.IsNullOrEmpty(url) || !url.StartsWith("http"))
            {
                MessageBox.Show("请先填写有效的平台 URL。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show("请先填写有效的平台 URL。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show("名称不能为空。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show("URL 不能为空。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _platform.Name = name;
            _platform.Url = url;
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
