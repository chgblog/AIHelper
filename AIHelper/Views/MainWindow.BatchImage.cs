// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AIHelper.Models;
using AIHelper.Services;

namespace AIHelper.Views
{
    /// <summary>
    /// 批量生图：按 CSV 逐行新建对话、注入提示词、等待出图、提取图片并按文件名保存
    /// </summary>
    public partial class MainWindow
    {
        // 生图状态探测间隔。等待在宿主侧做，不依赖会被节流的页面计时器
        private const int BatchProbeIntervalMs = 1000;
        // 新图片宽高都小于这个值时视为图标、头像
        private const int BatchMinImageSize = 256;
        // 同一平台连续失败这么多行后跳过该平台剩余行（通常是额度用尽或掉线）
        private const int BatchMaxConsecutiveFailures = 3;
        // 等待超时时，图片若已经这么久没有变化，仍按成功处理
        private static readonly TimeSpan BatchTimeoutStableImages = TimeSpan.FromSeconds(10);

        private BatchImageWindow _batchImageWindow;
        private BatchImageRunState _batchRun;

        /// <summary>
        /// 批量生图正在使用 WebView：运行中，或正在录制/试运行页面预设
        /// </summary>
        internal bool IsBatchImageRunning => _batchRun != null || _presetBusy;

        private class BatchAttemptResult
        {
            public bool Success;
            // 平台级问题（未登录），不再重试，并跳过该平台剩余行
            public bool Fatal;
            public string Reason;
            public string Message;
            public List<ImageProbeImage> Images;
            public List<string> SavedFiles;
        }

        /// <summary>
        /// 批量生图入口：快捷栏、热键、划词工具条、操作面板、文件右键都走这里。
        /// 对 .csv 文件触发时直接使用该文件，否则先弹出文件选择。
        /// withoutCsv：不选文件直接打开窗口（设置页的「页面预设」入口：录制不需要 CSV）。
        /// </summary>
        public void OpenBatchImage(ActionItem action, string csvPath = null, bool withoutCsv = false)
        {
            try
            {
                if (action == null) return;
                bool hasCsv = IsCsvFile(csvPath);

                if (_batchImageWindow != null && _batchImageWindow.IsLoaded)
                {
                    if (hasCsv && !IsBatchImageRunning)
                    {
                        _batchImageWindow.LoadCsv(csvPath);
                    }
                    ActivateBatchImageWindow();
                    return;
                }

                // 生图期间主窗口需要可见，页面在隐藏窗口里会被节流
                ShowAndActivate();

                if (!hasCsv)
                {
                    csvPath = withoutCsv ? null : PickBatchCsvFile(this);
                    if (csvPath == null && !withoutCsv) return;
                }

                var window = new BatchImageWindow(this, action.Id, csvPath) { Owner = this };
                window.Closed += (s, e) =>
                {
                    if (_batchImageWindow == window) _batchImageWindow = null;
                };
                _batchImageWindow = window;
                window.Show();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error opening batch image window (action={action?.Name}, csv={csvPath})", ex);
            }
        }

        /// <summary>
        /// 选择 CSV 文件，并记住所在目录
        /// </summary>
        internal string PickBatchCsvFile(Window owner)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = LanguageManager.Instance["Batch_SelectCsvTitle"],
                Filter = "CSV (*.csv)|*.csv|" + LanguageManager.Instance["Batch_AllFiles"] + " (*.*)|*.*",
                CheckFileExists = true
            };

            string lastDir = _settings?.BatchImageLastCsvDirectory;
            if (!string.IsNullOrWhiteSpace(lastDir) && Directory.Exists(lastDir))
            {
                dialog.InitialDirectory = lastDir;
            }

            if (dialog.ShowDialog(owner) != true) return null;

            if (_settings != null)
            {
                _settings.BatchImageLastCsvDirectory = Path.GetDirectoryName(dialog.FileName);
                SettingsService.Instance.Save(_settings);
            }
            return dialog.FileName;
        }

        private static bool IsCsvFile(string path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase) &&
                   File.Exists(path);
        }

        private void ActivateBatchImageWindow()
        {
            var window = _batchImageWindow;
            if (window == null || !window.IsLoaded) return;
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }
            window.Show();
            window.Activate();
        }

        /// <summary>
        /// 批量生图运行时，其他操作会抢占同一个 WebView，直接拒绝并提示
        /// </summary>
        private bool RejectWhileBatchRunning()
        {
            if (!IsBatchImageRunning) return false;
            UpdateStatus(LanguageManager.Instance[_batchRun != null ? "Batch_Status_Busy" : "Batch_Preset_Busy"]);
            ActivateBatchImageWindow();
            return true;
        }

        private void SetBatchUiLocked(bool locked)
        {
            if (cmbPlatforms != null) cmbPlatforms.IsEnabled = !locked;
            if (btnSwitchToBrowser != null) btnSwitchToBrowser.IsEnabled = !locked;
        }

        /// <summary>
        /// 执行批量生图。按平台分组（组内保持 CSV 顺序），尽量减少平台切换——
        /// 代理平台与直连平台之间切换要重建 WebView2。
        /// </summary>
        internal async Task RunBatchImageAsync(BatchImageSession session, IList<BatchImageItem> items, BatchImageRunState run)
        {
            if (_batchRun != null || session == null || run == null || items == null || items.Count == 0) return;

            var lm = LanguageManager.Instance;
            _batchRun = run;
            SetBatchUiLocked(true);
            int total = items.Count;
            int processed = 0;
            Logger.LogInfo($"Batch image started: {total} row(s), output={session.OutputDirectory}");

            try
            {
                ShowAndActivate();

                var groups = items.GroupBy(i => i.Platform?.Id ?? string.Empty).ToList();
                foreach (var group in groups)
                {
                    var queue = group.ToList();
                    var platform = queue[0].Platform;
                    await WaitIfBatchPausedAsync(session, run);

                    var ready = await EnsurePlatformReadyAsync(platform);
                    run.Token.ThrowIfCancellationRequested();
                    if (!ready.Success)
                    {
                        string reason = ready.Reason == "NOT_LOGGED_IN"
                            ? lm["Batch_Msg_SkippedNotLoggedIn"]
                            : lm.GetString("Batch_Msg_PlatformNotReady", ready.Message);
                        Logger.LogError($"Batch image: platform {platform?.Name} not ready ({ready.Reason}), skipping {queue.Count} row(s)");
                        MarkBatchItems(queue, 0, BatchImageItemStatus.Skipped, reason);
                        processed += queue.Count;
                        session.WriteReport();
                        continue;
                    }

                    using (var capture = new NetworkImageCapture(webView?.CoreWebView2))
                    {
                        await SetPageKeepActiveAsync(true);
                        try
                        {
                            int consecutiveFailures = 0;
                            for (int k = 0; k < queue.Count; k++)
                            {
                                await WaitIfBatchPausedAsync(session, run);

                                var result = await RunBatchItemAsync(queue[k], platform, session, capture, processed + 1, total, run.Token);
                                processed++;
                                session.WriteReport();

                                consecutiveFailures = result.Success ? 0 : consecutiveFailures + 1;
                                string skipReason = null;
                                if (result.Fatal)
                                {
                                    skipReason = lm["Batch_Msg_SkippedNotLoggedIn"];
                                }
                                else if (consecutiveFailures >= BatchMaxConsecutiveFailures)
                                {
                                    skipReason = lm.GetString("Batch_Msg_SkippedConsecutive", consecutiveFailures);
                                }

                                if (skipReason != null && k + 1 < queue.Count)
                                {
                                    Logger.LogWarning($"Batch image: skipping remaining {queue.Count - k - 1} row(s) of {platform.Name}: {skipReason}");
                                    MarkBatchItems(queue, k + 1, BatchImageItemStatus.Skipped, skipReason);
                                    processed += queue.Count - k - 1;
                                    session.WriteReport();
                                    break;
                                }

                                if (processed < total)
                                {
                                    await DelayBetweenBatchItemsAsync(session, run.Token);
                                }
                            }
                        }
                        finally
                        {
                            await SetPageKeepActiveAsync(false);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.LogInfo("Batch image stopped by user");
            }
            catch (Exception ex)
            {
                Logger.LogError("Batch image run failed", ex);
            }
            finally
            {
                foreach (var item in items.Where(i => i.Status == BatchImageItemStatus.Running))
                {
                    item.Status = BatchImageItemStatus.Pending;
                    item.Message = lm["Batch_Msg_Stopped"];
                }
                session.WriteReport();

                _batchRun = null;
                SetBatchUiLocked(false);

                int ok = items.Count(i => i.Status == BatchImageItemStatus.Success);
                int failed = items.Count(i => i.Status == BatchImageItemStatus.Failed);
                int skipped = items.Count(i => i.Status == BatchImageItemStatus.Skipped);
                string summary = run.IsCancellationRequested
                    ? lm["Batch_Status_Stopped"]
                    : lm.GetString("Batch_Status_Finished", ok, failed, skipped);
                Logger.LogInfo($"Batch image finished: success={ok}, failed={failed}, skipped={skipped}, cancelled={run.IsCancellationRequested}");
                UpdateStatus(summary);
                session.ReportStatus(summary);
            }
        }

        private async Task WaitIfBatchPausedAsync(BatchImageSession session, BatchImageRunState run)
        {
            if (run.IsPaused)
            {
                string text = LanguageManager.Instance["Batch_Status_Paused"];
                UpdateStatus(text);
                session.ReportStatus(text);
            }
            await run.WaitIfPausedAsync();
        }

        private static void MarkBatchItems(IList<BatchImageItem> queue, int from, BatchImageItemStatus status, string message)
        {
            for (int i = from; i < queue.Count; i++)
            {
                queue[i].Status = status;
                queue[i].Message = message;
            }
        }

        /// <summary>
        /// 处理一行，失败时按设置重试（每次重试都从新建对话开始）
        /// </summary>
        private async Task<BatchAttemptResult> RunBatchItemAsync(BatchImageItem item, AiPlatform platform, BatchImageSession session,
            NetworkImageCapture capture, int position, int total, CancellationToken ct)
        {
            item.Status = BatchImageItemStatus.Running;
            item.Message = "";
            item.SavedFiles = new List<string>();

            var stopwatch = Stopwatch.StartNew();
            int attempts = 1 + Math.Max(0, session.Options.RetryCount);
            BatchAttemptResult result = null;
            try
            {
                for (int attempt = 1; attempt <= attempts; attempt++)
                {
                    ct.ThrowIfCancellationRequested();
                    if (attempt > 1)
                    {
                        Logger.LogWarning($"Batch image row {item.Index} failed ({result?.Reason}), retry {attempt - 1}");
                        ReportBatchStep(session, position, total, LanguageManager.Instance.GetString("Batch_Step_Retry", attempt - 1));
                        await Task.Delay(2000, ct);
                    }

                    try
                    {
                        result = await RunBatchAttemptAsync(item, platform, session, capture, position, total, ct);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        Logger.LogError($"Batch image row {item.Index} attempt {attempt} threw", ex);
                        result = BatchFail("EXCEPTION", ex.Message);
                    }
                    if (result.Success || result.Fatal) break;
                }
            }
            finally
            {
                item.ElapsedSeconds = stopwatch.Elapsed.TotalSeconds;
            }

            if (result.Success)
            {
                item.SavedFiles = result.SavedFiles ?? new List<string>();
                item.Message = result.Message;
                item.Status = BatchImageItemStatus.Success;
            }
            else
            {
                Logger.LogError($"Batch image row {item.Index} failed ({platform.Name}): {result.Reason}");
                item.Message = result.Message;
                item.Status = BatchImageItemStatus.Failed;
            }
            return result;
        }

        private async Task<BatchAttemptResult> RunBatchAttemptAsync(BatchImageItem item, AiPlatform platform, BatchImageSession session,
            NetworkImageCapture capture, int position, int total, CancellationToken ct)
        {
            var lm = LanguageManager.Instance;

            // 1. 每行都在新对话里生成，避免上下文影响下一张
            ReportBatchStep(session, position, total, lm["Batch_Step_NewChat"]);
            if (!await StartNewChatAndWaitAsync(platform))
            {
                return BatchFail("PAGE_NOT_READY", lm["Inject_PageNotReady"]);
            }
            ct.ThrowIfCancellationRequested();

            // 1.5 重放页面预设（选择模型、比例等）：新建对话或页面刷新可能把这些选项重置
            if (platform.BatchSetupSteps != null && platform.BatchSetupSteps.Count > 0)
            {
                ReportBatchStep(session, position, total, lm["Batch_Step_Preset"]);
                var preset = await _pageInjector.ApplySetupStepsAsync(webView, platform.BatchSetupSteps);
                if (!preset.Success)
                {
                    return BatchFail(preset.Reason, lm.GetString("Batch_Msg_PresetFailed", preset.Message));
                }
                ct.ThrowIfCancellationRequested();
            }

            // 2. 记录页面上已有的图片，之后只认新出现的图片（新建对话失败仍在旧对话里时也不会误取）
            capture?.Clear();
            await _pageInjector.SnapshotImagesAsync(webView);

            // 3. 注入合并后的提示词
            ReportBatchStep(session, position, total, lm["Batch_Step_Injecting"]);
            string prompt = BatchImageService.MergePrompt(session.ActionPrompt, item.Prompt);
            var injected = await _pageInjector.InjectAndSubmitAsync(webView, prompt, platform.InputSelector, platform.SubmitSelector, autoSubmit: false);
            if (!injected.Success)
            {
                return BatchFail(injected.Reason, injected.Message, injected.Reason == "NOT_LOGGED_IN");
            }
            ct.ThrowIfCancellationRequested();

            // 4. 单独提交并确认真的发出去了（不受"自动提交"设置影响）
            ReportBatchStep(session, position, total, lm["Batch_Step_Submitting"]);
            var submitted = await SubmitBatchPromptAsync(platform, ct);
            if (!submitted.Success)
            {
                return BatchFail(submitted.Reason, lm.GetString("Batch_Msg_SubmitFailed", submitted.Message), submitted.Reason == "NOT_LOGGED_IN");
            }

            // 5. 等待出图
            var waited = await WaitForGeneratedImagesAsync(platform, session, position, total, ct);
            if (!waited.Success) return waited;

            // 6. 提取图片数据
            ReportBatchStep(session, position, total, lm["Batch_Step_Extracting"]);
            var extracted = new List<ExtractedImage>();
            for (int i = 0; i < waited.Images.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var image = waited.Images[i];
                ExtractedImage data;
                if (platform.BatchOpenLargeImage)
                {
                    ReportBatchStep(session, position, total, lm.GetString("Batch_Step_OpenLarge", i + 1, waited.Images.Count));
                    data = await ExtractLargeBatchImageAsync(image, capture);
                }
                else
                {
                    data = await ExtractBatchImageAsync(image.src, capture);
                }
                if (data != null) extracted.Add(data);
            }
            if (extracted.Count == 0)
            {
                return BatchFail("EXTRACT_FAILED", lm["Batch_Msg_ExtractFailed"]);
            }

            // 7. 第一张存输出目录，其余存备选子目录
            try
            {
                var saved = BatchImageService.SaveImages(session.OutputDirectory, session.AlternatesDirName, item.FileName, extracted);
                string message = string.Join("; ", saved);
                int lost = waited.Images.Count - extracted.Count;
                if (lost > 0)
                {
                    message += " " + lm.GetString("Batch_Msg_PartialExtract", lost);
                }
                return new BatchAttemptResult { Success = true, Reason = "SAVED", SavedFiles = saved, Message = message };
            }
            catch (Exception ex)
            {
                Logger.LogError($"Saving batch images for row {item.Index} failed", ex);
                return BatchFail("SAVE_FAILED", lm.GetString("Batch_Msg_SaveFailed", ex.Message));
            }
        }

        private static BatchAttemptResult BatchFail(string reason, string message, bool fatal = false)
        {
            return new BatchAttemptResult { Success = false, Fatal = fatal, Reason = reason ?? "UNKNOWN", Message = message ?? reason ?? "" };
        }

        /// <summary>
        /// 提交并确认已发出。发送按钮在注入后要等页面状态更新才可用，所以短暂重试。
        /// </summary>
        private async Task<InjectionResult> SubmitBatchPromptAsync(AiPlatform platform, CancellationToken ct)
        {
            InjectionResult last = null;
            for (int i = 0; i < 20; i++)
            {
                ct.ThrowIfCancellationRequested();
                last = await _pageInjector.SubmitAsync(webView, platform.InputSelector, platform.SubmitSelector);
                if (last.Success || last.Reason == "NOT_LOGGED_IN") return last;

                // 点击已发出但页面没有明确反馈：可能已经提交，再点会重复发送，交给出图等待去判断
                if (last.Reason == "SUBMIT_UNACKNOWLEDGED")
                {
                    Logger.LogWarning($"Batch submit unacknowledged ({platform.Name}), waiting for images anyway");
                    return new InjectionResult { Success = true, Reason = last.Reason, Message = last.Message };
                }

                await Task.Delay(500, ct);
            }
            return last;
        }

        private async Task<BatchAttemptResult> WaitForGeneratedImagesAsync(AiPlatform platform, BatchImageSession session,
            int position, int total, CancellationToken ct)
        {
            var lm = LanguageManager.Instance;
            var start = DateTime.UtcNow;
            var deadline = start.AddSeconds(session.Options.TimeoutSeconds);
            var tracker = new ImageGenerationTracker(start);

            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(BatchProbeIntervalMs, ct);
                int waitedSeconds = (int)(DateTime.UtcNow - start).TotalSeconds;
                ReportBatchStep(session, position, total, lm.GetString("Batch_Step_Waiting", waitedSeconds));

                var probe = await _pageInjector.ProbeImagesAsync(webView, platform.InputSelector, BatchMinImageSize);
                if (probe != null && !probe.ok)
                {
                    if (probe.reason == "NOT_LOGGED_IN")
                    {
                        return BatchFail("NOT_LOGGED_IN", lm["Inject_NotLoggedIn"], fatal: true);
                    }
                    probe = null;
                }

                switch (tracker.Update(probe, DateTime.UtcNow))
                {
                    case ImageWaitOutcome.Completed:
                        return new BatchAttemptResult { Success = true, Images = tracker.Images.ToList() };
                    case ImageWaitOutcome.NoImage:
                        string snippet = string.IsNullOrWhiteSpace(tracker.Snippet) ? "-" : tracker.Snippet;
                        return BatchFail("NO_IMAGE", lm.GetString("Batch_Msg_NoImage", snippet));
                }
            }

            if (tracker.HasStableImages(DateTime.UtcNow, BatchTimeoutStableImages))
            {
                Logger.LogWarning($"Batch image wait timed out on {platform.Name}, but the images were stable — keeping them");
                return new BatchAttemptResult { Success = true, Images = tracker.Images.ToList() };
            }
            return BatchFail("TIMEOUT", lm["Batch_Msg_Timeout"]);
        }

        /// <summary>
        /// 先在页面内读取（blob/data/同源），失败再从网络捕获里取（跨域 CDN）
        /// </summary>
        private async Task<ExtractedImage> ExtractBatchImageAsync(string src, NetworkImageCapture capture)
        {
            string extension;

            // data: 地址直接解码。页面的 CSP（connect-src）常常不允许脚本 fetch data:，它也不经过网络捕获
            if (BatchImageService.TryDecodeDataUrl(src, out var inline, out string inlineMime) &&
                BatchImageService.TryGetImageExtension(inline, inlineMime, out extension))
            {
                return new ExtractedImage { Data = inline, Extension = extension, Source = "data:" + inlineMime };
            }

            var fetched = await _pageInjector.FetchImageAsync(webView, src);
            if (fetched != null && fetched.ok && !string.IsNullOrEmpty(fetched.data))
            {
                try
                {
                    byte[] data = Convert.FromBase64String(fetched.data);
                    if (BatchImageService.TryGetImageExtension(data, fetched.mime, out extension))
                    {
                        return new ExtractedImage { Data = data, Extension = extension, Source = src };
                    }
                }
                catch (FormatException)
                {
                    // Fall through to the network capture
                }
            }

            if (capture != null && capture.TryGet(src, out var captured) &&
                BatchImageService.TryGetImageExtension(captured.Data, captured.ContentType, out extension))
            {
                return new ExtractedImage { Data = captured.Data, Extension = extension, Source = src };
            }

            Logger.LogWarning($"Could not extract image {ShortenForLog(src)}: {fetched?.reason ?? "NO_RESULT"}");
            return null;
        }

        /// <summary>
        /// 点击缩略图打开查看器，提取其中的大图后关闭查看器。没有查看器、大图提取失败
        /// 或"大图"反而更小时退回缩略图。
        /// </summary>
        private async Task<ExtractedImage> ExtractLargeBatchImageAsync(ImageProbeImage image, NetworkImageCapture capture)
        {
            var opened = await _pageInjector.OpenLargeImageAsync(webView, image.src, BatchMinImageSize);
            try
            {
                // A link to the file reports no size (w = 0): trust it
                bool larger = opened.ok && !string.IsNullOrEmpty(opened.src) &&
                              (opened.w == 0 || (long)opened.w * opened.h >= (long)image.w * image.h);
                if (larger)
                {
                    var large = await ExtractBatchImageAsync(opened.src, capture);
                    if (large != null) return large;
                    Logger.LogWarning($"Full-size image could not be extracted, keeping the thumbnail: {ShortenForLog(opened.src)}");
                }
                else
                {
                    Logger.LogWarning($"No full-size image for {ShortenForLog(image.src)} ({opened.reason}), keeping the thumbnail");
                }
                return await ExtractBatchImageAsync(image.src, capture);
            }
            finally
            {
                // Read before closing: a viewer may revoke its blob: URL once it is gone
                if (opened.clicked && !await _pageInjector.CloseImageViewerAsync(webView))
                {
                    Logger.LogWarning("Image viewer could not be closed");
                }
            }
        }

        private static string ShortenForLog(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Length <= 120 ? value : value.Substring(0, 120) + "...";
        }

        private async Task DelayBetweenBatchItemsAsync(BatchImageSession session, CancellationToken ct)
        {
            int minMs = Math.Max(0, session.Options.MinIntervalSeconds) * 1000;
            int maxMs = Math.Max(minMs, session.Options.MaxIntervalSeconds * 1000);
            int delayMs = minMs == maxMs ? minMs : _random.Next(minMs, maxMs + 1);
            if (delayMs <= 0) return;

            string text = LanguageManager.Instance.GetString("Batch_Step_Interval", (delayMs / 1000.0).ToString("0.#"));
            UpdateStatus(text);
            session.ReportStatus(text);
            await Task.Delay(delayMs, ct);
        }

        private void ReportBatchStep(BatchImageSession session, int position, int total, string step)
        {
            string text = LanguageManager.Instance.GetString("Batch_Status_Item", position, total, step);
            UpdateStatus(text);
            session.ReportStatus(text);
        }

        /// <summary>
        /// 让页面在窗口失去焦点或被遮挡时仍按"活动"状态运行（尽力而为，失败不影响流程）
        /// </summary>
        private async Task SetPageKeepActiveAsync(bool active)
        {
            var core = webView?.CoreWebView2;
            if (core == null) return;

            try
            {
                await core.CallDevToolsProtocolMethodAsync("Emulation.setFocusEmulationEnabled",
                    active ? "{\"enabled\":true}" : "{\"enabled\":false}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"setFocusEmulationEnabled failed: {ex.Message}");
            }

            if (active)
            {
                try
                {
                    await core.CallDevToolsProtocolMethodAsync("Page.setWebLifecycleState", "{\"state\":\"active\"}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"setWebLifecycleState failed: {ex.Message}");
                }
            }
        }
    }
}
