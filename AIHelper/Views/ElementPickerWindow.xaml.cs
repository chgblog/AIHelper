using System;
using System.IO;
using System.Windows;
using AIHelper.Services;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json.Linq;

namespace AIHelper.Views
{
    public partial class ElementPickerWindow : Window
    {
        private readonly string _url;

        /// <summary>
        /// The CSS selector picked by the user. Null if cancelled.
        /// </summary>
        public string PickedSelector { get; private set; }

        public ElementPickerWindow(string url)
        {
            InitializeComponent();
            _url = url;
            txtUrl.Text = url;
            Loaded += ElementPickerWindow_Loaded;
        }

        private async void ElementPickerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = SettingsService.Instance.Load();
                CoreWebView2EnvironmentOptions options = null;
                if (!string.IsNullOrWhiteSpace(settings?.ProxyServer))
                {
                    options = new CoreWebView2EnvironmentOptions
                    {
                        AdditionalBrowserArguments = $"--proxy-server=\"{settings.ProxyServer.Trim()}\""
                    };
                }

                string userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AIHelper", "WebView2Data");

                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
                await pickerWebView.EnsureCoreWebView2Async(env);

                pickerWebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                pickerWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                pickerWebView.CoreWebView2.Navigate(_url);
            }
            catch (Exception ex)
            {
                txtStatus.Text = LanguageManager.Instance.GetString("ElementPicker_Status_InitFailed", ex.Message);
            }
        }

        private async void CoreWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                txtStatus.Text = LanguageManager.Instance["ElementPicker_Status_LoadSuccess"];
                await InjectPickerScript();
            }
            else
            {
                txtStatus.Text = LanguageManager.Instance.GetString("ElementPicker_Status_LoadFailed", e.WebErrorStatus);
            }
        }

        private async System.Threading.Tasks.Task InjectPickerScript()
        {
            try
            {
                string script = string.Empty;
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream("AIHelper.Assets.element-picker.js"))
                {
                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            script = reader.ReadToEnd();
                        }
                    }
                }

                if (string.IsNullOrEmpty(script))
                {
                    string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "element-picker.js");
                    if (File.Exists(scriptPath))
                    {
                        script = File.ReadAllText(scriptPath);
                    }
                    else
                    {
                        txtStatus.Text = LanguageManager.Instance["ElementPicker_Status_ScriptMissing"];
                        return;
                    }
                }

                await pickerWebView.CoreWebView2.ExecuteScriptAsync(script);
                txtStatus.Text = LanguageManager.Instance["ElementPicker_Status_Active"];
            }
            catch (Exception ex)
            {
                txtStatus.Text = LanguageManager.Instance.GetString("ElementPicker_Status_InjectFailed", ex.Message);
            }
        }

        private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string json = e.TryGetWebMessageAsString();
                var msg = JObject.Parse(json);
                string type = msg["type"]?.ToString();

                if (type == "element_picked")
                {
                    PickedSelector = msg["selector"]?.ToString();
                    string tag = msg["tag"]?.ToString() ?? "";
                    string id = msg["id"]?.ToString() ?? "";

                    string display = string.IsNullOrEmpty(id) ? $"<{tag}>" : $"<{tag} id=\"{id}\">";
                    txtStatus.Text = LanguageManager.Instance.GetString("ElementPicker_Status_Selected", display, PickedSelector);

                    this.DialogResult = true;
                    this.Close();
                }
                else if (type == "element_pick_cancelled")
                {
                    this.DialogResult = false;
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebMessage parse error: {ex.Message}");
            }
        }

        private async void BtnRetry_Click(object sender, RoutedEventArgs e)
        {
            txtStatus.Text = LanguageManager.Instance["ElementPicker_Status_Reinjecting"];
            await InjectPickerScript();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
