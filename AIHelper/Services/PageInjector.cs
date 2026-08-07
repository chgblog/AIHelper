using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;

namespace AIHelper.Services
{
    public class InjectionResult
    {
        public bool Success { get; set; }
        public string Reason { get; set; }
        public string Message { get; set; }
    }

    internal class InjectionScriptResult
    {
        public bool success { get; set; }
        public string reason { get; set; }
    }

    /// <summary>
    /// Service for injecting scripts and text into WebView2 pages
    /// </summary>
    public class PageInjector
    {
        /// <summary>
        /// Injects text and auto-submits
        /// </summary>
        public async Task<InjectionResult> InjectAndSubmitAsync(WebView2 webView, string text)
        {
            if (webView == null || webView.CoreWebView2 == null)
            {
                return new InjectionResult { Success = false, Reason = "WEBVIEW_NOT_READY", Message = "浏览器组件未就绪" };
            }

            try
            {
                string injectorScript = string.Empty;
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream("AIHelper.Assets.injector.js"))
                {
                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            injectorScript = reader.ReadToEnd();
                        }
                    }
                }

                if (string.IsNullOrEmpty(injectorScript))
                {
                    string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "injector.js");
                    if (File.Exists(scriptPath))
                    {
                        injectorScript = File.ReadAllText(scriptPath);
                    }
                    else
                    {
                        return new InjectionResult { Success = false, Reason = "SCRIPT_NOT_FOUND", Message = "注入脚本丢失" };
                    }
                }

                // Serialize text to safely pass it to JS
                string jsonText = JsonConvert.SerializeObject(text);
                
                string finalScript = $"{injectorScript}\n return window.AiHelperInjector.inject({jsonText}, true);";

                string resultJson = await webView.CoreWebView2.ExecuteScriptAsync($"(function() {{ {finalScript} }})()");
                
                if (string.IsNullOrEmpty(resultJson) || resultJson == "null")
                {
                    return new InjectionResult { Success = false, Reason = "UNKNOWN_ERROR", Message = "注入失败，未获取到结果" };
                }

                string rawJson = resultJson;
                if (rawJson.StartsWith("\"") && rawJson.EndsWith("\""))
                {
                    try
                    {
                        rawJson = JsonConvert.DeserializeObject<string>(rawJson);
                    }
                    catch
                    {
                        // Fallback to raw string if unquoting fails
                    }
                }

                var result = JsonConvert.DeserializeObject<InjectionScriptResult>(rawJson);
                if (result == null)
                {
                    return new InjectionResult { Success = false, Reason = "UNKNOWN_ERROR", Message = "注入失败，结果格式错误" };
                }

                bool success = result.success;
                string reason = result.reason ?? "UNKNOWN";
                
                string message = success ? "发送成功" : (reason == "NOT_LOGGED_IN" ? "请先登录 AI 平台" : (reason == "INPUT_NOT_FOUND" ? "无法找到输入框，页面可能已更新" : "注入失败"));

                return new InjectionResult { Success = success, Reason = reason, Message = message };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Injection error: {ex.Message}");
                return new InjectionResult { Success = false, Reason = "EXCEPTION", Message = $"注入异常: {ex.Message}" };
            }
        }
    }
}
