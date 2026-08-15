// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
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
        /// Injects text and auto-submits, using optional custom CSS selectors
        /// </summary>
        public async Task<InjectionResult> InjectAndSubmitAsync(WebView2 webView, string text, string inputSelector = null, string submitSelector = null, string newChatSelector = null, bool autoSubmit = true)
        {
            if (webView == null || webView.CoreWebView2 == null)
            {
                return new InjectionResult { Success = false, Reason = "WEBVIEW_NOT_READY", Message = LanguageManager.Instance["Inject_WebviewNotReady"] };
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
                        return new InjectionResult { Success = false, Reason = "SCRIPT_NOT_FOUND", Message = LanguageManager.Instance["Inject_ScriptNotFound"] };
                    }
                }

                // Serialize text and selectors to safely pass them to JS
                string jsonText = JsonConvert.SerializeObject(text);
                string jsonInputSelector = string.IsNullOrEmpty(inputSelector) ? "null" : JsonConvert.SerializeObject(inputSelector);
                string jsonSubmitSelector = string.IsNullOrEmpty(submitSelector) ? "null" : JsonConvert.SerializeObject(submitSelector);
                string jsonNewChatSelector = string.IsNullOrEmpty(newChatSelector) ? "null" : JsonConvert.SerializeObject(newChatSelector);
                string jsonAutoSubmit = autoSubmit ? "true" : "false";
                
                string finalScript = $"{injectorScript}\n return await window.AiHelperInjector.inject({jsonText}, {jsonAutoSubmit}, {jsonInputSelector}, {jsonSubmitSelector}, {jsonNewChatSelector});";

                string resultJson = await webView.CoreWebView2.ExecuteScriptAsync($"(async function() {{ {finalScript} }})()");
                
                if (string.IsNullOrEmpty(resultJson) || resultJson == "null")
                {
                    return new InjectionResult { Success = false, Reason = "UNKNOWN_ERROR", Message = LanguageManager.Instance["Inject_NoResult"] };
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
                    return new InjectionResult { Success = false, Reason = "UNKNOWN_ERROR", Message = LanguageManager.Instance["Inject_FormatError"] };
                }

                bool success = result.success;
                string reason = result.reason ?? "UNKNOWN";
                
                string message = success 
                    ? (autoSubmit ? LanguageManager.Instance["Inject_SendSuccess"] : LanguageManager.Instance["Inject_InjectSuccess"]) 
                    : (reason == "NOT_LOGGED_IN" ? LanguageManager.Instance["Inject_NotLoggedIn"] 
                        : (reason == "INPUT_NOT_FOUND" ? LanguageManager.Instance["Inject_InputNotFound"] 
                            : LanguageManager.Instance["Inject_Failed"]));

                return new InjectionResult { Success = success, Reason = reason, Message = message };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Injection error: {ex.Message}");
                return new InjectionResult { Success = false, Reason = "EXCEPTION", Message = LanguageManager.Instance.GetString("Inject_Exception", ex.Message) };
            }
        }
    }
}
