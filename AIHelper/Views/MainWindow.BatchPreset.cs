// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AIHelper.Models;
using AIHelper.Services;

namespace AIHelper.Views
{
    /// <summary>
    /// 批量生图页面预设：在主窗口的页面上录制用户选择模型、比例等操作，以及试运行。
    /// 录制和试运行期间与批量运行一样独占 WebView（见 IsBatchImageRunning）。
    /// </summary>
    public partial class MainWindow
    {
        private const int PresetPollIntervalMs = 500;

        // 录制或试运行页面预设中
        private bool _presetBusy;
        private PresetRecording _presetRecording;

        private sealed class PresetRecording
        {
            public AiPlatform Platform;
            public readonly List<PageSetupStep> Steps = new List<PageSetupStep>();
            public Action<IList<PageSetupStep>> Changed;
            public DispatcherTimer Timer;
            public bool Polling;
        }

        // 页面预设操作条上两个按钮当前对应的动作
        private Action _presetBarPrimary;
        private Action _presetBarSecondary;

        /// <summary>
        /// 在主窗口页面上方显示操作条。批量窗口是主窗口的从属窗口，永远盖在它上面，
        /// 所以录制和试运行时批量窗口会先隐藏，由这条操作条代替它的按钮。按钮标签为 null 则隐藏该按钮。
        /// </summary>
        internal void ShowPresetBar(string text, string primaryLabel, Action primary, string secondaryLabel, Action secondary)
        {
            txtPresetBar.Text = text ?? "";
            _presetBarPrimary = primary;
            _presetBarSecondary = secondary;
            btnPresetBarPrimary.Content = primaryLabel;
            btnPresetBarPrimary.Visibility = primaryLabel != null ? Visibility.Visible : Visibility.Collapsed;
            btnPresetBarPrimary.IsEnabled = true;
            btnPresetBarSecondary.Content = secondaryLabel;
            btnPresetBarSecondary.Visibility = secondaryLabel != null ? Visibility.Visible : Visibility.Collapsed;
            btnPresetBarSecondary.IsEnabled = true;
            presetBar.Visibility = Visibility.Visible;
        }

        internal void SetPresetBarText(string text)
        {
            txtPresetBar.Text = text ?? "";
        }

        internal void HidePresetBar()
        {
            presetBar.Visibility = Visibility.Collapsed;
            _presetBarPrimary = null;
            _presetBarSecondary = null;
        }

        private void BtnPresetBarPrimary_Click(object sender, RoutedEventArgs e)
        {
            RunPresetBarAction(_presetBarPrimary);
        }

        private void BtnPresetBarSecondary_Click(object sender, RoutedEventArgs e)
        {
            RunPresetBarAction(_presetBarSecondary);
        }

        private void RunPresetBarAction(Action action)
        {
            if (action == null) return;
            // Stops a double click from finishing or cancelling twice
            btnPresetBarPrimary.IsEnabled = false;
            btnPresetBarSecondary.IsEnabled = false;
            action();
        }

        private string PresetBarRecordingText(AiPlatform platform, int steps)
        {
            return LanguageManager.Instance.GetString("Batch_Preset_BarRecording", platform?.Name ?? "-", steps);
        }

        /// <summary>
        /// 打开平台、新建对话（与批量运行时的起点一致），然后开始录制。
        /// 录到新步骤时在 UI 线程上调用 <paramref name="changed"/>；
        /// 主窗口上方的「完成录制」「取消」按钮分别调用 <paramref name="finishRequested"/>、<paramref name="cancelRequested"/>。
        /// </summary>
        internal async Task<InjectionResult> StartPresetRecordingAsync(AiPlatform platform, Action<IList<PageSetupStep>> changed,
            Action finishRequested, Action cancelRequested)
        {
            var lm = LanguageManager.Instance;
            if (platform == null) return PresetFail("NO_PLATFORM", lm["Batch_Msg_NoPlatform"]);
            if (IsBatchImageRunning) return PresetFail("BUSY", lm["Batch_Status_Busy"]);

            var recording = new PresetRecording { Platform = platform, Changed = changed };
            _presetRecording = recording;
            _presetBusy = true;
            SetBatchUiLocked(true);

            try
            {
                ShowAndActivate();
                ShowPresetBar(lm.GetString("Batch_Preset_BarStarting", platform.Name), null, null, lm["Batch_Preset_Cancel"], cancelRequested);

                var ready = await EnsurePlatformReadyAsync(platform);
                if (_presetRecording != recording) return PresetFail("CANCELLED", "");
                if (!ready.Success)
                {
                    EndPresetRecording(recording);
                    return ready;
                }

                if (!await StartNewChatAndWaitAsync(platform))
                {
                    if (_presetRecording != recording) return PresetFail("CANCELLED", "");
                    EndPresetRecording(recording);
                    return PresetFail("PAGE_NOT_READY", lm["Inject_PageNotReady"]);
                }
                if (_presetRecording != recording) return PresetFail("CANCELLED", "");

                bool started = await _pageInjector.StartRecordingAsync(webView, platform, lm["Batch_Preset_Badge"]);
                if (_presetRecording != recording)
                {
                    // Cancelled while the recorder was being installed: take it off the page again
                    await _pageInjector.StopRecordingAsync(webView);
                    return PresetFail("CANCELLED", "");
                }
                if (!started)
                {
                    EndPresetRecording(recording);
                    return PresetFail("SCRIPT_FAILED", lm["Inject_Failed"]);
                }

                recording.Timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(PresetPollIntervalMs) };
                recording.Timer.Tick += async (s, e) => await PollPresetRecordingAsync(recording);
                recording.Timer.Start();

                UpdateStatus(lm["Batch_Preset_RecordingStatus"]);
                ShowPresetBar(PresetBarRecordingText(platform, recording.Steps.Count),
                    lm["Batch_Preset_Finish"], finishRequested, lm["Batch_Preset_Cancel"], cancelRequested);
                webView?.Focus();
                Logger.LogInfo($"Page preset recording started on {platform.Name}");
                return new InjectionResult { Success = true, Reason = "RECORDING", Message = "" };
            }
            catch (Exception ex)
            {
                Logger.LogError("Starting page preset recording failed", ex);
                if (_presetRecording == recording) EndPresetRecording(recording);
                return PresetFail("EXCEPTION", ex.Message);
            }
        }

        private async Task PollPresetRecordingAsync(PresetRecording recording)
        {
            if (_presetRecording != recording)
            {
                recording.Timer?.Stop();
                return;
            }
            if (recording.Polling) return;
            recording.Polling = true;
            try
            {
                var poll = await _pageInjector.TakeRecordedStepsAsync(webView);
                if (poll == null || _presetRecording != recording) return;

                if (!poll.active)
                {
                    // The page reloaded (or navigated) and took the recorder with it
                    await _pageInjector.StartRecordingAsync(webView, recording.Platform, LanguageManager.Instance["Batch_Preset_Badge"]);
                    return;
                }

                if (poll.steps != null && poll.steps.Count > 0)
                {
                    PageSetupService.Append(recording.Steps, poll.steps);
                    SetPresetBarText(PresetBarRecordingText(recording.Platform, recording.Steps.Count));
                    recording.Changed?.Invoke(recording.Steps.ToList());
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Page preset poll failed: {ex.Message}");
            }
            finally
            {
                recording.Polling = false;
            }
        }

        /// <summary>
        /// 结束录制，返回录到的全部步骤（取消录制时调用方丢弃即可）
        /// </summary>
        internal async Task<List<PageSetupStep>> StopPresetRecordingAsync()
        {
            var recording = _presetRecording;
            if (recording == null) return new List<PageSetupStep>();

            recording.Timer?.Stop();
            try
            {
                var rest = await _pageInjector.StopRecordingAsync(webView);
                PageSetupService.Append(recording.Steps, rest);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Stopping page preset recording failed: {ex.Message}");
            }
            finally
            {
                EndPresetRecording(recording);
            }

            Logger.LogInfo($"Page preset recording stopped on {recording.Platform?.Name}: {recording.Steps.Count} step(s)");
            return recording.Steps.ToList();
        }

        private void EndPresetRecording(PresetRecording recording)
        {
            recording.Timer?.Stop();
            if (_presetRecording != recording) return;
            _presetRecording = null;
            _presetBusy = false;
            HidePresetBar();
            SetBatchUiLocked(false);
            UpdateStatus(LanguageManager.Instance["Main_Status_Ready"]);
        }

        /// <summary>
        /// 试运行：与批量运行一样先新建对话，再重放预设，让用户在页面上确认结果
        /// </summary>
        internal async Task<SetupStepsResult> TestPagePresetAsync(AiPlatform platform, IList<PageSetupStep> steps)
        {
            var lm = LanguageManager.Instance;
            if (platform == null) return new SetupStepsResult { Success = false, Reason = "NO_PLATFORM", Message = lm["Batch_Msg_NoPlatform"] };
            if (IsBatchImageRunning) return new SetupStepsResult { Success = false, Reason = "BUSY", Message = lm["Batch_Status_Busy"] };

            _presetBusy = true;
            SetBatchUiLocked(true);
            try
            {
                ShowAndActivate();

                var ready = await EnsurePlatformReadyAsync(platform);
                if (!ready.Success) return new SetupStepsResult { Success = false, Reason = ready.Reason, Message = ready.Message };

                if (!await StartNewChatAndWaitAsync(platform))
                {
                    return new SetupStepsResult { Success = false, Reason = "PAGE_NOT_READY", Message = lm["Inject_PageNotReady"] };
                }

                UpdateStatus(lm["Batch_Preset_Testing"]);
                var result = await _pageInjector.ApplySetupStepsAsync(webView, steps);
                UpdateStatus(result.Success
                    ? lm.GetString("Batch_Preset_TestOk", result.Applied, result.Skipped)
                    : lm.GetString("Batch_Preset_TestFailed", result.Message));
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError("Testing the page preset failed", ex);
                return new SetupStepsResult { Success = false, Reason = "EXCEPTION", Message = ex.Message };
            }
            finally
            {
                _presetBusy = false;
                SetBatchUiLocked(false);
            }
        }

        private static InjectionResult PresetFail(string reason, string message)
        {
            return new InjectionResult { Success = false, Reason = reason, Message = message ?? "" };
        }
    }
}
