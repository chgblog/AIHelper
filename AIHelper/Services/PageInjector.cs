// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

    internal class ReadyScriptResult
    {
        public bool ready { get; set; }
        public string reason { get; set; }
    }

    internal class NewChatScriptResult
    {
        public bool clicked { get; set; }
        public string reason { get; set; }
    }

    internal class JobStartResult
    {
        public bool started { get; set; }
        public string reason { get; set; }
    }

    /// <summary>
    /// Service for injecting scripts and text into WebView2 pages
    /// </summary>
    public class PageInjector
    {
        // How often the host checks whether a script side job finished
        private const int JobPollIntervalMs = 150;
        // Extra time the host waits on top of the script's own deadline
        private const int JobGraceMs = 5000;
        // inject() retries for the input element internally, so give it room
        private const int InjectTimeoutMs = 20000;


        /// <summary>
        /// Waits until the page is actually usable (document parsed and the input
        /// element present and stable). NavigationCompleted alone is not enough for
        /// the SPA based platforms — the input box only appears after the app boots.
        /// </summary>
        public async Task<InjectionResult> WaitPageReadyAsync(WebView2 webView, string inputSelector = null, int timeoutMs = 25000)
        {
            if (webView == null || webView.CoreWebView2 == null)
            {
                return new InjectionResult { Success = false, Reason = "WEBVIEW_NOT_READY", Message = LanguageManager.Instance["Inject_WebviewNotReady"] };
            }

            string injectorScript = LoadInjectorScript();
            if (string.IsNullOrEmpty(injectorScript))
            {
                return new InjectionResult { Success = false, Reason = "SCRIPT_NOT_FOUND", Message = LanguageManager.Instance["Inject_ScriptNotFound"] };
            }

            try
            {
                string jsonInputSelector = string.IsNullOrEmpty(inputSelector) ? "null" : JsonConvert.SerializeObject(inputSelector);
                string rawJson = await RunJobAsync(webView, injectorScript, "waitReady",
                    $"{jsonInputSelector}, {timeoutMs}", timeoutMs + JobGraceMs);

                var result = string.IsNullOrEmpty(rawJson) ? null : JsonConvert.DeserializeObject<ReadyScriptResult>(rawJson);
                if (result == null)
                {
                    // Job timed out or the page was replaced under it
                    return new InjectionResult { Success = false, Reason = "TIMEOUT", Message = LanguageManager.Instance["Inject_PageNotReady"] };
                }

                string reason = result.reason ?? "UNKNOWN";
                string message = result.ready
                    ? LanguageManager.Instance["Main_Status_PageLoadSuccess"]
                    : (reason == "NOT_LOGGED_IN"
                        ? LanguageManager.Instance["Inject_NotLoggedIn"]
                        : LanguageManager.Instance["Inject_PageNotReady"]);

                return new InjectionResult { Success = result.ready, Reason = reason, Message = message };
            }
            catch (Exception ex)
            {
                Logger.LogError("WaitPageReady failed", ex);
                return new InjectionResult { Success = false, Reason = "EXCEPTION", Message = LanguageManager.Instance.GetString("Inject_Exception", ex.Message) };
            }
        }

        /// <summary>
        /// Clicks the new chat button and stamps <paramref name="token"/> on window so the
        /// caller can detect a full page reload triggered by that click.
        /// Returns true when a button was actually clicked.
        /// </summary>
        public async Task<bool> StartNewChatAsync(WebView2 webView, string newChatSelector, string token)
        {
            if (webView == null || webView.CoreWebView2 == null) return false;

            string injectorScript = LoadInjectorScript();
            if (string.IsNullOrEmpty(injectorScript)) return false;

            try
            {
                string jsonSelector = string.IsNullOrEmpty(newChatSelector) ? "null" : JsonConvert.SerializeObject(newChatSelector);
                string jsonToken = JsonConvert.SerializeObject(token ?? string.Empty);
                // startNewChat is synchronous, so its value comes straight back
                string rawJson = await EvalAsync(webView,
                    $"{injectorScript}\n return window.AiHelperInjector.startNewChat({jsonSelector}, {jsonToken});");

                var result = string.IsNullOrEmpty(rawJson) ? null : JsonConvert.DeserializeObject<NewChatScriptResult>(rawJson);
                return result != null && result.clicked;
            }
            catch (Exception ex)
            {
                Logger.LogError("StartNewChat failed", ex);
                return false;
            }
        }

        /// <summary>
        /// Reads back the marker written by <see cref="StartNewChatAsync"/>. Returns null when
        /// the document was replaced (reload) or the script could not be evaluated.
        /// </summary>
        public async Task<string> GetPageTokenAsync(WebView2 webView)
        {
            if (webView == null || webView.CoreWebView2 == null) return null;

            try
            {
                string raw = await webView.CoreWebView2.ExecuteScriptAsync("window.__aiHelperToken || ''");
                if (string.IsNullOrEmpty(raw) || raw == "null") return null;
                return JsonConvert.DeserializeObject<string>(raw);
            }
            catch
            {
                // The page is navigating away — treat it as "marker gone"
                return null;
            }
        }

        /// <summary>
        /// Injects text and auto-submits, using optional custom CSS selectors.
        /// The page must already be settled — call <see cref="WaitPageReadyAsync"/> first.
        /// </summary>
        public async Task<InjectionResult> InjectAndSubmitAsync(WebView2 webView, string text, string inputSelector = null, string submitSelector = null, bool autoSubmit = true)
        {
            if (webView == null || webView.CoreWebView2 == null)
            {
                return new InjectionResult { Success = false, Reason = "WEBVIEW_NOT_READY", Message = LanguageManager.Instance["Inject_WebviewNotReady"] };
            }

            string injectorScript = LoadInjectorScript();
            if (string.IsNullOrEmpty(injectorScript))
            {
                return new InjectionResult { Success = false, Reason = "SCRIPT_NOT_FOUND", Message = LanguageManager.Instance["Inject_ScriptNotFound"] };
            }

            try
            {
                // Serialize text and selectors to safely pass them to JS
                string jsonText = JsonConvert.SerializeObject(text);
                string jsonInputSelector = string.IsNullOrEmpty(inputSelector) ? "null" : JsonConvert.SerializeObject(inputSelector);
                string jsonSubmitSelector = string.IsNullOrEmpty(submitSelector) ? "null" : JsonConvert.SerializeObject(submitSelector);
                string jsonAutoSubmit = autoSubmit ? "true" : "false";

                string rawJson = await RunJobAsync(webView, injectorScript, "inject",
                    $"{jsonText}, {jsonAutoSubmit}, {jsonInputSelector}, {jsonSubmitSelector}", InjectTimeoutMs);

                if (string.IsNullOrEmpty(rawJson))
                {
                    return new InjectionResult { Success = false, Reason = "UNKNOWN_ERROR", Message = LanguageManager.Instance["Inject_NoResult"] };
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
                            : (reason == "INJECT_LOST" ? LanguageManager.Instance["Inject_TextLost"]
                                : LanguageManager.Instance["Inject_Failed"])));

                return new InjectionResult { Success = success, Reason = reason, Message = message };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Injection error: {ex.Message}");
                return new InjectionResult { Success = false, Reason = "EXCEPTION", Message = LanguageManager.Instance.GetString("Inject_Exception", ex.Message) };
            }
        }

        /// <summary>
        /// Starts an async injector method as a job and polls until it finishes.
        /// ExecuteScriptAsync does not await promises — an async function evaluates to an
        /// empty object — so the result is picked up from window.__aiHelperJob instead.
        /// Returns the JSON payload of the job result, or null on timeout / lost page.
        /// </summary>
        private async Task<string> RunJobAsync(WebView2 webView, string injectorScript, string method, string argsJs, int timeoutMs)
        {
            string jsonId = JsonConvert.SerializeObject(Guid.NewGuid().ToString("N"));
            string jsonMethod = JsonConvert.SerializeObject(method);

            string startRaw = await EvalAsync(webView,
                $"{injectorScript}\n return window.AiHelperInjector.run({jsonId}, {jsonMethod}, [{argsJs}]);");

            var start = string.IsNullOrEmpty(startRaw) ? null : JsonConvert.DeserializeObject<JobStartResult>(startRaw);
            if (start == null || !start.started)
            {
                Logger.LogError($"Injector job '{method}' failed to start: {start?.reason ?? "NO_RESULT"}");
                return null;
            }

            // The poll expression deliberately touches only the job global, so it keeps
            // working even if the page replaced window.AiHelperInjector meanwhile.
            string pollScript = $"var j = window.__aiHelperJob;" +
                                $" if (!j || j.id !== {jsonId}) return {{ state: \"LOST\" }};" +
                                $" return j.done ? {{ state: \"DONE\", result: j.result }} : {{ state: \"PENDING\" }};";

            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(JobPollIntervalMs);

                string pollRaw = await EvalAsync(webView, pollScript);
                if (string.IsNullOrEmpty(pollRaw)) continue;

                JObject poll;
                try
                {
                    poll = JObject.Parse(pollRaw);
                }
                catch
                {
                    continue;
                }

                string state = (string)poll["state"];
                if (state == "DONE") return poll["result"]?.ToString(Formatting.None);
                if (state == "LOST")
                {
                    Logger.LogError($"Injector job '{method}' lost (page was replaced)");
                    return null;
                }
            }

            Logger.LogError($"Injector job '{method}' timed out after {timeoutMs}ms");
            return null;
        }

        /// <summary>
        /// Evaluates a synchronous script body and returns the unwrapped JSON result.
        /// The injector definition is prepended by callers that need it, because the page
        /// may have reloaded since the last call.
        /// </summary>
        private async Task<string> EvalAsync(WebView2 webView, string body)
        {
            string resultJson;
            try
            {
                resultJson = await webView.CoreWebView2.ExecuteScriptAsync($"(function() {{ {body} }})()");
            }
            catch (Exception ex)
            {
                // Thrown while the page is navigating away
                System.Diagnostics.Debug.WriteLine($"ExecuteScript failed: {ex.Message}");
                return null;
            }

            if (string.IsNullOrEmpty(resultJson) || resultJson == "null") return null;

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
            return rawJson;
        }

        private string LoadInjectorScript()
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
            }

            return injectorScript;
        }
    }
}
