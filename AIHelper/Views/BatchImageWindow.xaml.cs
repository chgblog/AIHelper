// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AIHelper.Models;
using AIHelper.Services;

namespace AIHelper.Views
{
    /// <summary>
    /// 批量生图窗口：预览 CSV、开始/暂停/停止、查看每行结果。实际执行在 MainWindow（共用其 WebView）。
    /// </summary>
    public partial class BatchImageWindow : Window
    {
        private readonly MainWindow _host;
        private readonly string _actionId;
        private readonly ObservableCollection<BatchImageItem> _items = new ObservableCollection<BatchImageItem>();
        private string _csvPath;
        private BatchImageSession _session;
        private BatchImageRunState _run;
        // 开始前显示的保存根目录（取自最近一次读取的设置）
        private string _saveRootPreview;

        // 页面预设：下拉框中所选平台的步骤；录制中显示实时录到的步骤
        private List<PageSetupStep> _presetSteps = new List<PageSetupStep>();
        private List<PageSetupStep> _presetBeforeRecording;
        private bool _presetStarting;
        private bool _presetRecording;
        private bool _presetTesting;

        private bool PresetBusy => _presetStarting || _presetRecording || _presetTesting;

        private class BatchContext
        {
            public AppSettings Settings;
            public ActionItem Action;
            public AiPlatform DefaultPlatform;
        }

        public class PresetStepView
        {
            public int Index { get; set; }
            public string Display { get; set; }
        }

        public BatchImageWindow(MainWindow host, string actionId, string csvPath)
        {
            InitializeComponent();
            _host = host;
            _actionId = actionId;
            dgItems.ItemsSource = _items;

            LanguageManager.Instance.LanguageChanged += LanguageManager_LanguageChanged;
            Closed += (s, e) => LanguageManager.Instance.LanguageChanged -= LanguageManager_LanguageChanged;
            // Coming back (from the tray, a trigger…) means the test-result bar on the main window is no longer needed
            IsVisibleChanged += (s, e) =>
            {
                if (IsVisible && !_presetStarting && !_presetRecording) _host?.HidePresetBar();
            };

            tbStatus.Text = LanguageManager.Instance["Batch_Status_Ready"];
            LoadCsv(csvPath);
            if (_csvPath == null)
            {
                // 没有 CSV（从设置页进入，或文件读取失败）时页面预设仍然可用
                var context = LoadContext();
                _saveRootPreview = context.Settings.GetBatchImageSaveRoot();
                tbDefaultPlatform.Text = LanguageManager.Instance.GetString("Batch_DefaultPlatform", context.DefaultPlatform?.Name ?? "-");
                PopulatePresetPlatforms(context.Settings, context.DefaultPlatform);
            }
            UpdateUi();
        }

        /// <summary>
        /// 每次都读最新设置：操作提示词、平台配置、保存路径都可能在窗口打开期间被修改
        /// </summary>
        private BatchContext LoadContext()
        {
            var settings = SettingsService.Instance.Load();
            var action = settings.Actions?.FirstOrDefault(a => a.Id == _actionId)
                         ?? settings.Actions?.FirstOrDefault(a => a.IsBatchImage);

            AiPlatform defaultPlatform = null;
            if (action != null && !string.IsNullOrEmpty(action.PlatformId))
            {
                defaultPlatform = settings.Platforms?.FirstOrDefault(p => p.Id == action.PlatformId);
            }
            if (defaultPlatform == null)
            {
                defaultPlatform = settings.GetActivePlatform();
            }

            return new BatchContext { Settings = settings, Action = action, DefaultPlatform = defaultPlatform };
        }

        public void LoadCsv(string path)
        {
            if (_run != null || string.IsNullOrWhiteSpace(path)) return;

            var context = LoadContext();
            var result = BatchImageService.ReadCsv(path, context.Settings.Platforms, context.DefaultPlatform);
            if (!string.IsNullOrEmpty(result.Error))
            {
                MessageBox.Show(result.Error, LanguageManager.Instance["Notice"], MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var old in _items)
            {
                old.PropertyChanged -= Item_PropertyChanged;
            }
            _items.Clear();
            foreach (var item in result.Items)
            {
                item.PropertyChanged += Item_PropertyChanged;
                _items.Add(item);
            }

            _csvPath = path;
            _saveRootPreview = context.Settings.GetBatchImageSaveRoot();
            // 新文件开始时会新建输出目录
            _session = null;
            txtCsvPath.Text = path;
            tbDefaultPlatform.Text = LanguageManager.Instance.GetString("Batch_DefaultPlatform", context.DefaultPlatform?.Name ?? "-");
            PopulatePresetPlatforms(context.Settings, context.DefaultPlatform);
            SetStatus(LanguageManager.Instance["Batch_Status_Ready"]);
            UpdateUi();
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BatchImageItem.Status))
            {
                UpdateProgress();
            }
        }

        private void LanguageManager_LanguageChanged(object sender, EventArgs e)
        {
            foreach (var item in _items)
            {
                item.RefreshStatusText();
            }
            UpdateUi();
        }

        private void SetStatus(string text)
        {
            tbStatus.Text = text ?? "";
        }

        private void UpdateUi()
        {
            var lm = LanguageManager.Instance;
            bool running = _run != null;
            bool idle = !running && !PresetBusy;

            btnStart.IsEnabled = idle && _items.Any(i => i.Status == BatchImageItemStatus.Pending);
            btnPause.IsEnabled = running;
            btnPause.Content = running && _run.IsPaused ? lm["Batch_Resume"] : lm["Batch_Pause"];
            btnStop.IsEnabled = running;
            btnRetryFailed.IsEnabled = idle && _items.Any(i => i.Status == BatchImageItemStatus.Failed || i.Status == BatchImageItemStatus.Skipped);
            btnPickCsv.IsEnabled = idle;

            if (!string.IsNullOrEmpty(_session?.OutputDirectory))
            {
                tbOutputDir.Text = lm.GetString("Batch_OutputDir", _session.OutputDirectory);
            }
            else
            {
                tbOutputDir.Text = lm.GetString("Batch_OutputDirPending", _saveRootPreview ?? AppSettings.GetDefaultBatchImageSaveRoot());
            }

            UpdatePresetUi();
            UpdateProgress();
        }

        private void UpdateProgress()
        {
            int valid = _items.Count(i => i.Status != BatchImageItemStatus.Invalid);
            int success = _items.Count(i => i.Status == BatchImageItemStatus.Success);
            int failed = _items.Count(i => i.Status == BatchImageItemStatus.Failed);
            int skipped = _items.Count(i => i.Status == BatchImageItemStatus.Skipped);
            int done = success + failed + skipped;

            progressBar.Maximum = Math.Max(1, valid);
            progressBar.Value = done;
            tbProgress.Text = LanguageManager.Instance.GetString("Batch_Progress", done, valid, success, failed, skipped);
            tbSummary.Text = LanguageManager.Instance.GetString("Batch_Summary", _items.Count, valid, _items.Count - valid);
        }

        private void BtnPickCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_run != null || _host == null) return;
            string path = _host.PickBatchCsvFile(this);
            if (path != null)
            {
                LoadCsv(path);
            }
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            await StartAsync();
        }

        private async Task StartAsync()
        {
            if (_run != null || _host == null || PresetBusy) return;

            var lm = LanguageManager.Instance;
            if (_host.IsBatchImageRunning)
            {
                MessageBox.Show(this, lm["Batch_Status_Busy"], lm["Notice"], MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var context = LoadContext();
                _saveRootPreview = context.Settings.GetBatchImageSaveRoot();

                // 以最新的平台配置为准重新解析（窗口打开期间可能改过平台）
                foreach (var item in _items.Where(i => i.Status == BatchImageItemStatus.Pending))
                {
                    BatchImageService.ApplyPlatform(item, context.Settings.Platforms, context.DefaultPlatform);
                }

                var pending = _items.Where(i => i.Status == BatchImageItemStatus.Pending).ToList();
                if (pending.Count == 0)
                {
                    UpdateUi();
                    MessageBox.Show(this, lm["Batch_NothingToRun"], lm["Notice"], MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (_session == null)
                {
                    string dir;
                    try
                    {
                        dir = BatchImageService.CreateOutputDirectory(context.Settings.GetBatchImageSaveRoot(), DateTime.Now);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to create batch output directory", ex);
                        MessageBox.Show(this, lm.GetString("Batch_OutputDirFailed", ex.Message), lm["Error"], MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    try
                    {
                        File.Copy(_csvPath, Path.Combine(dir, BatchImageService.SourceCopyFileName), true);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Failed to copy batch CSV to output: {ex.Message}");
                    }

                    _session = new BatchImageSession
                    {
                        CsvPath = _csvPath,
                        OutputDirectory = dir,
                        AllItems = _items.ToList(),
                        AlternatesDirName = BatchImageService.SanitizeFileName(lm["Batch_AlternatesFolder"]) ?? "Alternates"
                    };
                }

                _session.ActionPrompt = context.Action?.Prompt ?? "";
                _session.Options = BatchImageOptions.FromSettings(context.Settings);
                _session.StatusChanged = SetStatus;

                _run = new BatchImageRunState();
                UpdateUi();
                await _host.RunBatchImageAsync(_session, pending, _run);
            }
            catch (Exception ex)
            {
                Logger.LogError("Batch image start failed", ex);
            }
            finally
            {
                if (_run != null)
                {
                    _run.Dispose();
                    _run = null;
                }
                if (IsLoaded)
                {
                    UpdateUi();
                }
            }
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            if (_run == null) return;

            if (_run.IsPaused)
            {
                _run.Resume();
                SetStatus(LanguageManager.Instance["Batch_Status_Resumed"]);
            }
            else
            {
                _run.Pause();
                SetStatus(LanguageManager.Instance["Batch_Status_Pausing"]);
            }
            UpdateUi();
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            if (_run == null) return;
            _run.Cancel();
            SetStatus(LanguageManager.Instance["Batch_Status_Stopping"]);
            UpdateUi();
        }

        private async void BtnRetryFailed_Click(object sender, RoutedEventArgs e)
        {
            if (_run != null || PresetBusy) return;

            foreach (var item in _items.Where(i => i.Status == BatchImageItemStatus.Failed || i.Status == BatchImageItemStatus.Skipped))
            {
                item.Message = "";
                item.SavedFiles.Clear();
                item.ElapsedSeconds = 0;
                item.Status = BatchImageItemStatus.Pending;
            }
            await StartAsync();
        }

        private void BtnOpenOutput_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string dir = _session?.OutputDirectory;
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                {
                    dir = SettingsService.Instance.Load().GetBatchImageSaveRoot();
                    Directory.CreateDirectory(dir);
                }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, LanguageManager.Instance.GetString("Settings_Batch_OpenDirFailed", ex.Message), LanguageManager.Instance["Error"], MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            // The settings dialog is modal: this returns once it is closed
            _host?.ShowSettings(SettingsWindow.BatchImageTabIndex);

            // Platforms may have been added, renamed or deleted
            if (_host != null && IsLoaded && _run == null && !PresetBusy)
            {
                var context = LoadContext();
                PopulatePresetPlatforms(context.Settings, context.DefaultPlatform);
                UpdateUi();
            }
        }

        #region Page preset

        /// <summary>
        /// 这个窗口是主窗口的从属窗口，总是盖在主窗口上面。录制、试运行时要让用户看到并操作平台页面，
        /// 所以先把本窗口隐藏，由主窗口上方的操作条代替按钮（见 MainWindow.ShowPresetBar）。
        /// </summary>
        private void HideForPage()
        {
            if (IsLoaded) Hide();
        }

        private void RestoreFromPage()
        {
            if (!IsLoaded) return;
            Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
        }

        private void PopulatePresetPlatforms(AppSettings settings, AiPlatform preferred)
        {
            string keepId = (cmbPresetPlatform.SelectedItem as AiPlatform)?.Id ?? preferred?.Id;
            var platforms = settings?.Platforms ?? new List<AiPlatform>();
            cmbPresetPlatform.ItemsSource = platforms;
            cmbPresetPlatform.SelectedItem = platforms.FirstOrDefault(p => p.Id == keepId)
                                             ?? platforms.FirstOrDefault(p => p.Id == preferred?.Id)
                                             ?? platforms.FirstOrDefault();
            LoadPresetFromSelection();
        }

        private void CmbPresetPlatform_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PresetBusy) return;
            LoadPresetFromSelection();
            UpdatePresetUi();
        }

        private void LoadPresetFromSelection()
        {
            var platform = cmbPresetPlatform.SelectedItem as AiPlatform;
            _presetSteps = platform?.BatchSetupSteps?.ToList() ?? new List<PageSetupStep>();
            chkOpenLarge.IsChecked = platform?.BatchOpenLargeImage == true;
        }

        private void UpdatePresetUi()
        {
            var lm = LanguageManager.Instance;
            var platform = cmbPresetPlatform.SelectedItem as AiPlatform;
            bool canUse = platform != null && _host != null && _run == null;
            bool idle = canUse && !PresetBusy;

            cmbPresetPlatform.IsEnabled = _run == null && !PresetBusy;
            chkOpenLarge.IsEnabled = idle;
            btnPresetRecord.Content = _presetRecording ? lm["Batch_Preset_Finish"] : lm["Batch_Preset_Record"];
            btnPresetRecord.IsEnabled = canUse && !_presetStarting && !_presetTesting;
            btnPresetCancel.Visibility = _presetStarting || _presetRecording ? Visibility.Visible : Visibility.Collapsed;
            btnPresetTest.IsEnabled = idle && _presetSteps.Count > 0;
            btnPresetClear.IsEnabled = idle && _presetSteps.Count > 0;
            icPresetSteps.IsEnabled = idle;
            icPresetSteps.ItemsSource = _presetSteps
                .Select((s, i) => new PresetStepView { Index = i, Display = $"{i + 1}. {PageSetupService.Describe(s)}" })
                .ToList();

            if (_presetStarting)
            {
                tbPresetInfo.Text = lm.GetString("Batch_Preset_Starting", platform?.Name ?? "-");
            }
            else if (_presetRecording)
            {
                tbPresetInfo.Text = lm.GetString("Batch_Preset_RecordingHint", _presetSteps.Count);
            }
            else
            {
                tbPresetInfo.Text = _presetSteps.Count == 0 ? lm["Batch_Preset_None"] : lm.GetString("Batch_Preset_Steps", _presetSteps.Count);
            }
        }

        /// <summary>
        /// 写回设置文件（每次都读最新的，避免覆盖别处的改动），并同步下拉框里的平台对象和主窗口
        /// </summary>
        private void SavePreset(AiPlatform platform, Action<AiPlatform> update)
        {
            if (platform == null) return;
            try
            {
                var settings = SettingsService.Instance.Load();
                var stored = settings.Platforms?.FirstOrDefault(p => p.Id == platform.Id);
                if (stored == null) return;

                update(stored);
                update(platform);
                SettingsService.Instance.Save(settings);
                // The main window saves its own settings copy later; it must not write the old preset back
                _host?.RefreshSettings();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Saving the page preset of {platform.Name} failed", ex);
            }
        }

        private async void BtnPresetRecord_Click(object sender, RoutedEventArgs e)
        {
            if (_presetRecording)
            {
                await FinishPresetRecordingAsync(true);
                return;
            }

            var platform = cmbPresetPlatform.SelectedItem as AiPlatform;
            if (platform == null || _host == null || _run != null || PresetBusy) return;

            var lm = LanguageManager.Instance;
            _presetBeforeRecording = _presetSteps;
            _presetSteps = new List<PageSetupStep>();
            _presetStarting = true;
            UpdateUi();
            HideForPage();

            InjectionResult started;
            try
            {
                started = await _host.StartPresetRecordingAsync(platform, steps =>
                {
                    if (!_presetRecording) return;
                    _presetSteps = steps.ToList();
                    UpdatePresetUi();
                },
                () => { _ = FinishPresetRecordingAsync(true); },
                () => { _ = FinishPresetRecordingAsync(false); });
            }
            finally
            {
                _presetStarting = false;
            }

            if (started.Success)
            {
                _presetRecording = true;
                SetStatus(lm["Batch_Preset_RecordingStatus"]);
            }
            else
            {
                RestoreFromPage();
                if (started.Reason != "CANCELLED")
                {
                    _presetSteps = _presetBeforeRecording ?? new List<PageSetupStep>();
                    MessageBox.Show(this, lm.GetString("Batch_Preset_RecordFailed", started.Message), lm["Notice"], MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            if (IsLoaded) UpdateUi();
        }

        private async void BtnPresetCancel_Click(object sender, RoutedEventArgs e)
        {
            await FinishPresetRecordingAsync(false);
        }

        private async Task FinishPresetRecordingAsync(bool save)
        {
            if (_host == null || (!_presetRecording && !_presetStarting)) return;

            var lm = LanguageManager.Instance;
            var platform = cmbPresetPlatform.SelectedItem as AiPlatform;
            // Cancelling while still starting: the start call sees it and reports CANCELLED
            var steps = await _host.StopPresetRecordingAsync();
            _presetRecording = false;

            if (save && steps.Count > 0 && platform != null)
            {
                SavePreset(platform, p => p.BatchSetupSteps = steps.ToList());
                _presetSteps = steps;
                SetStatus(lm.GetString("Batch_Preset_Saved", platform.Name, steps.Count));
            }
            else
            {
                _presetSteps = _presetBeforeRecording ?? new List<PageSetupStep>();
                SetStatus(save ? lm["Batch_Preset_NothingRecorded"] : lm["Batch_Preset_RecordCancelled"]);
            }
            _presetBeforeRecording = null;
            if (IsLoaded)
            {
                RestoreFromPage();
                UpdateUi();
            }
        }

        private void BtnPresetRemoveStep_Click(object sender, RoutedEventArgs e)
        {
            var platform = cmbPresetPlatform.SelectedItem as AiPlatform;
            if (PresetBusy || platform == null || !((sender as FrameworkElement)?.Tag is PresetStepView view)) return;
            if (view.Index < 0 || view.Index >= _presetSteps.Count) return;

            _presetSteps.RemoveAt(view.Index);
            var steps = _presetSteps.ToList();
            SavePreset(platform, p => p.BatchSetupSteps = steps.ToList());
            UpdatePresetUi();
        }

        private void BtnPresetClear_Click(object sender, RoutedEventArgs e)
        {
            var platform = cmbPresetPlatform.SelectedItem as AiPlatform;
            if (PresetBusy || platform == null || _presetSteps.Count == 0) return;

            var lm = LanguageManager.Instance;
            if (MessageBox.Show(this, lm.GetString("Batch_Preset_ConfirmClear", platform.Name), lm["Notice"],
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            _presetSteps = new List<PageSetupStep>();
            SavePreset(platform, p => p.BatchSetupSteps = new List<PageSetupStep>());
            UpdatePresetUi();
        }

        private void ChkOpenLarge_Click(object sender, RoutedEventArgs e)
        {
            var platform = cmbPresetPlatform.SelectedItem as AiPlatform;
            if (platform == null) return;
            bool open = chkOpenLarge.IsChecked == true;
            SavePreset(platform, p => p.BatchOpenLargeImage = open);
        }

        private async void BtnPresetTest_Click(object sender, RoutedEventArgs e)
        {
            var platform = cmbPresetPlatform.SelectedItem as AiPlatform;
            if (_host == null || platform == null || PresetBusy || _run != null || _presetSteps.Count == 0) return;

            var lm = LanguageManager.Instance;
            _presetTesting = true;
            UpdateUi();
            SetStatus(lm["Batch_Preset_Testing"]);
            HideForPage();
            bool barShown = false;
            try
            {
                var result = await _host.TestPagePresetAsync(platform, _presetSteps.ToList());
                string text = result.Success
                    ? lm.GetString("Batch_Preset_TestOk", result.Applied, result.Skipped)
                    : lm.GetString("Batch_Preset_TestFailed", result.Message);
                SetStatus(text);
                // Stay on the page until the user has looked at the result
                _host.ShowPresetBar(text, lm["Batch_Preset_BarBack"], RestoreFromPage, null, null);
                barShown = true;
            }
            finally
            {
                _presetTesting = false;
                if (!barShown) RestoreFromPage();
                if (IsLoaded) UpdateUi();
            }
        }

        #endregion

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_run != null)
            {
                // Exiting from the tray closes this owned window too — stop without asking then
                if (_host == null || !_host.IsExiting)
                {
                    var lm = LanguageManager.Instance;
                    var answer = MessageBox.Show(this, lm["Batch_ConfirmStopOnClose"], lm["Notice"], MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (answer != MessageBoxResult.Yes)
                    {
                        e.Cancel = true;
                        return;
                    }
                }
                _run.Cancel();
            }
            else if (_presetRecording || _presetStarting)
            {
                // Closing discards an unfinished recording and hands the WebView back
                _presetRecording = false;
                _ = _host?.StopPresetRecordingAsync();
            }
            base.OnClosing(e);
        }
    }
}
