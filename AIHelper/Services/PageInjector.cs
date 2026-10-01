// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AIHelper.Models;
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

    internal class FocusResult
    {
        public bool focused { get; set; }
        public string reason { get; set; }
    }

    public class PasteWatchStartResult
    {
        public bool started { get; set; }
        public string token { get; set; }
        public string reason { get; set; }
    }

    internal class PasteWatchResult
    {
        public bool pasted { get; set; }
        public string reason { get; set; }
    }

    internal class SubmitReadyResult
    {
        public bool ready { get; set; }
        public string reason { get; set; }
    }

    internal class SubmitResult
    {
        public bool success { get; set; }
        public string reason { get; set; }
    }

    public class ImageProbeImage
    {
        public string src { get; set; }
        public int w { get; set; }
        public int h { get; set; }
    }

    /// <summary>
    /// 一次生图状态探测：是否仍在生成、已出现的新图片、仍在加载的图片数
    /// </summary>
    public class ImageProbeResult
    {
        public bool ok { get; set; }
        public string reason { get; set; }
        public bool generating { get; set; }
        public List<ImageProbeImage> images { get; set; } = new List<ImageProbeImage>();
        public int pending { get; set; }
        public int textLength { get; set; }
        public string snippet { get; set; }
    }

    public class FetchImageResult
    {
        public bool ok { get; set; }
        public string reason { get; set; }
        public string mime { get; set; }
        public string data { get; set; }
    }

    /// <summary>
    /// 一次录制轮询：页面上的录制器是否还在（页面刷新后会消失），以及新录到的步骤
    /// </summary>
    public class PresetRecordingPoll
    {
        public bool active { get; set; }
        public List<PageSetupStep> steps { get; set; } = new List<PageSetupStep>();
    }

    internal class SetupStepsScriptResult
    {
        public bool success { get; set; }
        public string reason { get; set; }
        public int index { get; set; }
        public string text { get; set; }
        public string message { get; set; }
        public int applied { get; set; }
        public int skipped { get; set; }
    }

    public class SetupStepsResult : InjectionResult
    {
        public int Applied { get; set; }
        public int Skipped { get; set; }
    }

    /// <summary>
    /// 点击图片打开大图的结果。clicked 表示页面上确实点过（之后需要关闭查看器）
    /// </summary>
    public class LargeImageResult
    {
        public bool ok { get; set; }
        public bool clicked { get; set; }
        public string reason { get; set; }
        public string src { get; set; }
        public int w { get; set; }
        public int h { get; set; }
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
        /// Puts the caret inside the platform input element. Must run right before a
        /// simulated Ctrl+V, otherwise the keystroke reaches the document instead of
        /// the composer and the clipboard payload is discarded.
        /// </summary>
        public async Task<bool> FocusInputAsync(WebView2 webView, string inputSelector = null)
        {
            if (webView == null || webView.CoreWebView2 == null) return false;

            string injectorScript = LoadInjectorScript();
            if (string.IsNullOrEmpty(injectorScript)) return false;

            try
            {
                string jsonInputSelector = string.IsNullOrEmpty(inputSelector) ? "null" : JsonConvert.SerializeObject(inputSelector);
                string rawJson = await EvalAsync(webView,
                    $"{injectorScript}\n return window.AiHelperInjector.focusInput({jsonInputSelector});");

                var result = string.IsNullOrEmpty(rawJson) ? null : JsonConvert.DeserializeObject<FocusResult>(rawJson);
                if (result == null || !result.focused)
                {
                    Logger.LogWarning($"FocusInput did not take effect: {result?.reason ?? "NO_RESULT"}");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("FocusInput failed", ex);
                return false;
            }
        }

        public async Task<PasteWatchStartResult> BeginPasteWatchAsync(WebView2 webView, string inputSelector = null)
        {
            if (webView == null || webView.CoreWebView2 == null)
            {
                return new PasteWatchStartResult { started = false, reason = "WEBVIEW_NOT_READY" };
            }

            string injectorScript = LoadInjectorScript();
            if (string.IsNullOrEmpty(injectorScript))
            {
                return new PasteWatchStartResult { started = false, reason = "SCRIPT_NOT_FOUND" };
            }

            try
            {
                string jsonInputSelector = string.IsNullOrEmpty(inputSelector) ? "null" : JsonConvert.SerializeObject(inputSelector);
                string rawJson = await EvalAsync(webView,
                    $"{injectorScript}\n return window.AiHelperInjector.beginPasteWatch({jsonInputSelector});");

                return string.IsNullOrEmpty(rawJson)
                    ? new PasteWatchStartResult { started = false, reason = "NO_RESULT" }
                    : JsonConvert.DeserializeObject<PasteWatchStartResult>(rawJson) ?? new PasteWatchStartResult { started = false, reason = "FORMAT_ERROR" };
            }
            catch (Exception ex)
            {
                Logger.LogError("BeginPasteWatch failed", ex);
                return new PasteWatchStartResult { started = false, reason = "EXCEPTION" };
            }
        }

        public async Task<InjectionResult> WaitForPasteAsync(WebView2 webView, string token, int timeoutMs = 5000)
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
                string jsonToken = JsonConvert.SerializeObject(token ?? string.Empty);
                string rawJson = await RunJobAsync(webView, injectorScript, "waitForPaste",
                    $"{jsonToken}, {timeoutMs}", timeoutMs + JobGraceMs);

                var result = string.IsNullOrEmpty(rawJson) ? null : JsonConvert.DeserializeObject<PasteWatchResult>(rawJson);
                if (result == null)
                {
                    return new InjectionResult { Success = false, Reason = "TIMEOUT", Message = LanguageManager.Instance["Inject_Failed"] };
                }

                return new InjectionResult
                {
                    Success = result.pasted,
                    Reason = result.reason ?? "UNKNOWN",
                    Message = result.pasted ? LanguageManager.Instance["Main_Status_PasteSuccess"] : LanguageManager.Instance["Inject_Failed"]
                };
            }
            catch (Exception ex)
            {
                Logger.LogError("WaitForPaste failed", ex);
                return new InjectionResult { Success = false, Reason = "EXCEPTION", Message = LanguageManager.Instance.GetString("Inject_Exception", ex.Message) };
            }
        }

        public async Task<InjectionResult> WaitForSubmitReadyAsync(WebView2 webView, string inputSelector = null, string submitSelector = null, int timeoutMs = 300000)
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
                string jsonSubmitSelector = string.IsNullOrEmpty(submitSelector) ? "null" : JsonConvert.SerializeObject(submitSelector);
                string rawJson = await RunJobAsync(webView, injectorScript, "waitForSubmitReady",
                    $"{jsonInputSelector}, {jsonSubmitSelector}, {timeoutMs}", timeoutMs + JobGraceMs);

                var result = string.IsNullOrEmpty(rawJson) ? null : JsonConvert.DeserializeObject<SubmitReadyResult>(rawJson);
                if (result == null)
                {
                    return new InjectionResult { Success = false, Reason = "TIMEOUT", Message = LanguageManager.Instance["Inject_Failed"] };
                }

                return new InjectionResult
                {
                    Success = result.ready,
                    Reason = result.reason ?? "UNKNOWN",
                    Message = result.ready ? LanguageManager.Instance["Main_Status_UploadReady"] : LanguageManager.Instance["Inject_Failed"]
                };
            }
            catch (Exception ex)
            {
                Logger.LogError("WaitForSubmitReady failed", ex);
                return new InjectionResult { Success = false, Reason = "EXCEPTION", Message = LanguageManager.Instance.GetString("Inject_Exception", ex.Message) };
            }
        }

        public async Task<InjectionResult> SubmitAsync(WebView2 webView, string inputSelector = null, string submitSelector = null)
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
                string jsonSubmitSelector = string.IsNullOrEmpty(submitSelector) ? "null" : JsonConvert.SerializeObject(submitSelector);
                string rawJson = await RunJobAsync(webView, injectorScript, "submit",
                    $"{jsonInputSelector}, {jsonSubmitSelector}", 10000);

                var result = string.IsNullOrEmpty(rawJson) ? null : JsonConvert.DeserializeObject<SubmitResult>(rawJson);
                if (result == null)
                {
                    return new InjectionResult { Success = false, Reason = "NO_RESULT", Message = LanguageManager.Instance["Inject_NoResult"] };
                }

                return new InjectionResult
                {
                    Success = result.success,
                    Reason = result.reason ?? "UNKNOWN",
                    Message = result.success ? LanguageManager.Instance["Inject_SendSuccess"] : LanguageManager.Instance["Inject_Failed"]
                };
            }
            catch (Exception ex)
            {
                Logger.LogError("SubmitAsync failed", ex);
                return new InjectionResult { Success = false, Reason = "EXCEPTION", Message = LanguageManager.Instance.GetString("Inject_Exception", ex.Message) };
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
        /// 记录页面上已有的图片，之后的探测只报告新出现的图片
        /// </summary>
        public async Task<bool> SnapshotImagesAsync(WebView2 webView)
        {
            if (webView == null || webView.CoreWebView2 == null) return false;

            string injectorScript = LoadInjectorScript();
            if (string.IsNullOrEmpty(injectorScript)) return false;

            try
            {
                string rawJson = await EvalAsync(webView, $"{injectorScript}\n return window.AiHelperInjector.snapshotImages();");
                return !string.IsNullOrEmpty(rawJson) && JObject.Parse(rawJson).Value<bool>("ok");
            }
            catch (Exception ex)
            {
                Logger.LogError("SnapshotImages failed", ex);
                return false;
            }
        }

        /// <summary>
        /// 探测一次生图状态。返回 null 表示页面正在切换、这次没能执行脚本。
        /// </summary>
        public async Task<ImageProbeResult> ProbeImagesAsync(WebView2 webView, string inputSelector, int minSize)
        {
            if (webView == null || webView.CoreWebView2 == null) return null;

            string injectorScript = LoadInjectorScript();
            if (string.IsNullOrEmpty(injectorScript)) return null;

            try
            {
                string jsonInputSelector = string.IsNullOrEmpty(inputSelector) ? "null" : JsonConvert.SerializeObject(inputSelector);
                string rawJson = await EvalAsync(webView,
                    $"{injectorScript}\n return window.AiHelperInjector.probeImages({jsonInputSelector}, {minSize});");
                return string.IsNullOrEmpty(rawJson) ? null : JsonConvert.DeserializeObject<ImageProbeResult>(rawJson);
            }
            catch (Exception ex)
            {
                Logger.LogError("ProbeImages failed", ex);
                return null;
            }
        }

        /// <summary>
        /// 在页面内读取图片数据（base64）。跨域且无 CORS 的图片会失败，由调用方改用网络捕获。
        /// </summary>
        public async Task<FetchImageResult> FetchImageAsync(WebView2 webView, string src, int timeoutMs = 60000)
        {
            if (webView == null || webView.CoreWebView2 == null)
            {
                return new FetchImageResult { ok = false, reason = "WEBVIEW_NOT_READY" };
            }

            string injectorScript = LoadInjectorScript();
            if (string.IsNullOrEmpty(injectorScript))
            {
                return new FetchImageResult { ok = false, reason = "SCRIPT_NOT_FOUND" };
            }

            try
            {
                string rawJson = await RunJobAsync(webView, injectorScript, "fetchImage",
                    JsonConvert.SerializeObject(src ?? string.Empty), timeoutMs);
                if (string.IsNullOrEmpty(rawJson))
                {
                    return new FetchImageResult { ok = false, reason = "TIMEOUT" };
                }
                return JsonConvert.DeserializeObject<FetchImageResult>(rawJson) ?? new FetchImageResult { ok = false, reason = "FORMAT_ERROR" };
            }
            catch (Exception ex)
            {
                Logger.LogError("FetchImage failed", ex);
                return new FetchImageResult { ok = false, reason = "EXCEPTION" };
            }
        }

        /// <summary>
        /// 开始录制页面预设：记录用户在页面上的点击和表单修改（输入框、发送、新建对话除外）
        /// </summary>
        public async Task<bool> StartRecordingAsync(WebView2 webView, AiPlatform platform, string badgeText)
        {
            if (webView == null || webView.CoreWebView2 == null || platform == null) return false;

            string injectorScript = LoadInjectorScript();
            if (string.IsNullOrEmpty(injectorScript)) return false;

            try
            {
                string args = string.Join(", ",
                    JsonOrNull(platform.InputSelector),
                    JsonOrNull(platform.SubmitSelector),
                    JsonOrNull(platform.NewChatSelector),
                    JsonConvert.SerializeObject(badgeText ?? ""));
                string rawJson = await EvalAsync(webView, $"{injectorScript}\n return window.AiHelperInjector.startRecording({args});");
                return !string.IsNullOrEmpty(rawJson) && JObject.Parse(rawJson).Value<bool>("started");
            }
            catch (Exception ex)
            {
                Logger.LogError("StartRecording failed", ex);
                return false;
            }
        }

        /// <summary>
        /// 取走页面上新录到的步骤。返回 null 表示这次没能执行脚本（页面正在切换）。
        /// </summary>
        public async Task<PresetRecordingPoll> TakeRecordedStepsAsync(WebView2 webView)
        {
            if (webView == null || webView.CoreWebView2 == null) return null;

            try
            {
                // Deliberately not prepending the injector: a reloaded page must report "inactive",
                // not get a fresh, empty recorder object
                string rawJson = await EvalAsync(webView,
                    "var a = window.AiHelperInjector; return a && a.takeRecordedSteps ? a.takeRecordedSteps() : { active: false, steps: [] };");
                return string.IsNullOrEmpty(rawJson) ? null : JsonConvert.DeserializeObject<PresetRecordingPoll>(rawJson);
            }
            catch (Exception ex)
            {
                Logger.LogError("TakeRecordedSteps failed", ex);
                return null;
            }
        }

        /// <summary>
        /// 停止录制并取回最后一批步骤
        /// </summary>
        public async Task<List<PageSetupStep>> StopRecordingAsync(WebView2 webView)
        {
            var empty = new List<PageSetupStep>();
            if (webView == null || webView.CoreWebView2 == null) return empty;

            try
            {
                string rawJson = await EvalAsync(webView,
                    "var a = window.AiHelperInjector; return a && a.stopRecording ? a.stopRecording() : { active: false, steps: [] };");
                var result = string.IsNullOrEmpty(rawJson) ? null : JsonConvert.DeserializeObject<PresetRecordingPoll>(rawJson);
                return result?.steps ?? empty;
            }
            catch (Exception ex)
            {
                Logger.LogError("StopRecording failed", ex);
                return empty;
            }
        }

        /// <summary>
        /// 按顺序重放页面预设。每步最多等 <paramref name="stepTimeoutMs"/> 让元素出现（下拉选项要等上一步展开）。
        /// </summary>
        public async Task<SetupStepsResult> ApplySetupStepsAsync(WebView2 webView, IList<PageSetupStep> steps, int stepTimeoutMs = 6000)
        {
            var lm = LanguageManager.Instance;
            if (webView == null || webView.CoreWebView2 == null)
            {
                return new SetupStepsResult { Success = false, Reason = "WEBVIEW_NOT_READY", Message = lm["Inject_WebviewNotReady"] };
            }
            if (steps == null || steps.Count == 0)
            {
                return new SetupStepsResult { Success = true, Reason = "NO_STEPS", Message = "" };
            }

            string injectorScript = LoadInjectorScript();
            if (string.IsNullOrEmpty(injectorScript))
            {
                return new SetupStepsResult { Success = false, Reason = "SCRIPT_NOT_FOUND", Message = lm["Inject_ScriptNotFound"] };
            }

            try
            {
                // camelCase for the script; settings.json keeps the PascalCase property names
                var payload = new List<object>();
                foreach (var s in steps)
                {
                    payload.Add(new { kind = s.Kind, selector = s.Selector, tag = s.Tag, text = s.Text, state = s.State, value = s.Value });
                }

                string rawJson = await RunJobAsync(webView, injectorScript, "applySetupSteps",
                    $"{JsonConvert.SerializeObject(payload)}, {stepTimeoutMs}", steps.Count * (stepTimeoutMs + 1000) + JobGraceMs);

                var result = string.IsNullOrEmpty(rawJson) ? null : JsonConvert.DeserializeObject<SetupStepsScriptResult>(rawJson);
                if (result == null)
                {
                    return new SetupStepsResult { Success = false, Reason = "TIMEOUT", Message = lm["Inject_PageNotReady"] };
                }
                if (result.success)
                {
                    return new SetupStepsResult { Success = true, Reason = result.reason, Message = "", Applied = result.applied, Skipped = result.skipped };
                }

                string message = result.reason == "STEP_NOT_FOUND"
                    ? lm.GetString("Batch_Preset_StepNotFound", result.index + 1, result.text)
                    : (string.IsNullOrEmpty(result.message) ? lm["Inject_Failed"] : lm.GetString("Inject_Exception", result.message));
                return new SetupStepsResult { Success = false, Reason = result.reason ?? "UNKNOWN", Message = message };
            }
            catch (Exception ex)
            {
                Logger.LogError("ApplySetupSteps failed", ex);
                return new SetupStepsResult { Success = false, Reason = "EXCEPTION", Message = lm.GetString("Inject_Exception", ex.Message) };
            }
        }

        /// <summary>
        /// 点击页面上的图片，等查看器显示大图后返回大图地址。图片外面包着指向图片的链接时直接用链接、不点击。
        /// </summary>
        public async Task<LargeImageResult> OpenLargeImageAsync(WebView2 webView, string src, int minSize, int timeoutMs = 15000)
        {
            if (webView == null || webView.CoreWebView2 == null)
            {
                return new LargeImageResult { ok = false, reason = "WEBVIEW_NOT_READY" };
            }

            string injectorScript = LoadInjectorScript();
            if (string.IsNullOrEmpty(injectorScript))
            {
                return new LargeImageResult { ok = false, reason = "SCRIPT_NOT_FOUND" };
            }

            try
            {
                string rawJson = await RunJobAsync(webView, injectorScript, "openLargeImage",
                    $"{JsonConvert.SerializeObject(src ?? string.Empty)}, {minSize}, {timeoutMs}", timeoutMs + JobGraceMs);
                if (string.IsNullOrEmpty(rawJson))
                {
                    // The click may have happened before the job got lost
                    return new LargeImageResult { ok = false, clicked = true, reason = "TIMEOUT" };
                }
                return JsonConvert.DeserializeObject<LargeImageResult>(rawJson) ?? new LargeImageResult { ok = false, clicked = true, reason = "FORMAT_ERROR" };
            }
            catch (Exception ex)
            {
                Logger.LogError("OpenLargeImage failed", ex);
                return new LargeImageResult { ok = false, clicked = true, reason = "EXCEPTION" };
            }
        }

        /// <summary>
        /// 关闭 <see cref="OpenLargeImageAsync"/> 打开的查看器。返回 false 表示查看器可能还开着。
        /// </summary>
        public async Task<bool> CloseImageViewerAsync(WebView2 webView)
        {
            if (webView == null || webView.CoreWebView2 == null) return false;

            string injectorScript = LoadInjectorScript();
            if (string.IsNullOrEmpty(injectorScript)) return false;

            try
            {
                string rawJson = await RunJobAsync(webView, injectorScript, "closeImageViewer", "", 10000);
                if (string.IsNullOrEmpty(rawJson)) return false;
                var result = JObject.Parse(rawJson);
                if (!result.Value<bool>("closed"))
                {
                    Logger.LogWarning($"Image viewer still open: {result.Value<string>("reason")}");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("CloseImageViewer failed", ex);
                return false;
            }
        }

        private static string JsonOrNull(string value)
        {
            return string.IsNullOrEmpty(value) ? "null" : JsonConvert.SerializeObject(value);
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
