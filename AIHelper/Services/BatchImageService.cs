// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AIHelper.Models;

namespace AIHelper.Services
{
    public class BatchImageCsvResult
    {
        public List<BatchImageItem> Items { get; set; } = new List<BatchImageItem>();

        /// <summary>
        /// 整个文件无法使用时的错误说明（单行问题记录在行的 Message 上）
        /// </summary>
        public string Error { get; set; }
    }

    public class ExtractedImage
    {
        public byte[] Data { get; set; }
        public string Extension { get; set; }
        public string Source { get; set; }
    }

    /// <summary>
    /// 批量生图运行参数（来自设置，已做范围修正）
    /// </summary>
    public class BatchImageOptions
    {
        public int TimeoutSeconds { get; set; } = 300;
        public int RetryCount { get; set; } = 1;
        public int MinIntervalSeconds { get; set; } = 3;
        public int MaxIntervalSeconds { get; set; } = 8;

        public const int MinTimeout = 30;
        public const int MaxTimeout = 1800;
        public const int MaxRetry = 5;
        public const int MaxInterval = 600;

        public static BatchImageOptions FromSettings(AppSettings settings)
        {
            var options = new BatchImageOptions();
            if (settings == null) return options;

            options.TimeoutSeconds = Clamp(settings.BatchImageTimeoutSeconds, MinTimeout, MaxTimeout);
            options.RetryCount = Clamp(settings.BatchImageRetryCount, 0, MaxRetry);
            options.MinIntervalSeconds = Clamp(settings.BatchImageMinIntervalSeconds, 0, MaxInterval);
            options.MaxIntervalSeconds = Clamp(settings.BatchImageMaxIntervalSeconds, options.MinIntervalSeconds, MaxInterval);
            return options;
        }

        public static bool IsValid(int timeout, int retry, int minInterval, int maxInterval)
        {
            return timeout >= MinTimeout && timeout <= MaxTimeout &&
                   retry >= 0 && retry <= MaxRetry &&
                   minInterval >= 0 && minInterval <= MaxInterval &&
                   maxInterval >= 0 && maxInterval <= MaxInterval &&
                   minInterval <= maxInterval;
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : (value > max ? max : value);
        }
    }

    /// <summary>
    /// 一次批量生图任务（一个 CSV 文件对应一个输出目录）
    /// </summary>
    public class BatchImageSession
    {
        public string CsvPath { get; set; }
        public string OutputDirectory { get; set; }
        public string ActionPrompt { get; set; } = "";
        public string AlternatesDirName { get; set; } = "Alternates";
        public BatchImageOptions Options { get; set; } = new BatchImageOptions();
        public IList<BatchImageItem> AllItems { get; set; } = new List<BatchImageItem>();

        /// <summary>
        /// 当前步骤说明（批量窗口显示）
        /// </summary>
        public Action<string> StatusChanged { get; set; }

        public string ReportPath => string.IsNullOrEmpty(OutputDirectory) ? null : Path.Combine(OutputDirectory, BatchImageService.ReportFileName);

        public void ReportStatus(string message)
        {
            try
            {
                StatusChanged?.Invoke(message);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Batch status callback failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 每处理完一行就重写报告，程序中途退出也能看到已完成的部分。
        /// 报告被 Excel 占用时写入会失败，这里只记录日志不打断任务。
        /// </summary>
        public void WriteReport()
        {
            string path = ReportPath;
            if (path == null) return;
            try
            {
                BatchImageService.WriteReport(path, AllItems);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to write batch report '{path}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 运行控制：停止（取消）与暂停（在两行之间生效）。只在 UI 线程上使用。
    /// </summary>
    public sealed class BatchImageRunState : IDisposable
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private TaskCompletionSource<bool> _resumeTcs;

        public CancellationToken Token => _cts.Token;
        public bool IsPaused => _resumeTcs != null;
        public bool IsCancellationRequested => _cts.IsCancellationRequested;

        public void Pause()
        {
            if (_resumeTcs == null && !_cts.IsCancellationRequested)
            {
                _resumeTcs = new TaskCompletionSource<bool>();
            }
        }

        public void Resume()
        {
            var tcs = _resumeTcs;
            _resumeTcs = null;
            tcs?.TrySetResult(true);
        }

        public void Cancel()
        {
            try { _cts.Cancel(); } catch (ObjectDisposedException) { }
            Resume();
        }

        /// <summary>
        /// 暂停时一直等到继续或停止；停止时抛出 OperationCanceledException
        /// </summary>
        public async Task WaitIfPausedAsync()
        {
            var tcs = _resumeTcs;
            if (tcs != null)
            {
                using (_cts.Token.Register(() => tcs.TrySetResult(false)))
                {
                    await tcs.Task;
                }
            }
            _cts.Token.ThrowIfCancellationRequested();
        }

        public void Dispose()
        {
            _cts.Dispose();
        }
    }

    /// <summary>
    /// 批量生图的纯逻辑部分：CSV 读取、提示词合并、文件命名、图片格式判断、保存与报告
    /// </summary>
    public static class BatchImageService
    {
        public const string ReportFileName = "_report.csv";
        public const string SourceCopyFileName = "_source.csv";

        private const int MaxFileNameLength = 120;

        private static readonly string[] PromptHeaders = { "提示词", "生图提示词", "描述", "prompt", "prompts" };
        private static readonly string[] FileNameHeaders = { "文件名", "文件名称", "保存文件名", "filename", "file name", "file_name", "file", "name" };
        private static readonly string[] PlatformHeaders = { "平台", "ai平台", "platform" };

        private static readonly string[] KnownImageExtensions = { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp", ".avif" };

        private static readonly HashSet<string> ReservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        #region CSV

        /// <summary>
        /// 读取 CSV 并解析成待执行的行。平台列为空时使用 <paramref name="defaultPlatform"/>。
        /// </summary>
        public static BatchImageCsvResult ReadCsv(string path, IList<AiPlatform> platforms, AiPlatform defaultPlatform)
        {
            var result = new BatchImageCsvResult();
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                string text = DecodeText(bytes);
                return BuildItems(ParseCsv(text), platforms, defaultPlatform);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to read batch CSV '{path}'", ex);
                result.Error = LanguageManager.Instance.GetString("Batch_Csv_ReadFailed", ex.Message);
                return result;
            }
        }

        /// <summary>
        /// 中文 Excel 默认把 CSV 存成 GBK，所以没有 BOM 时先按严格 UTF-8 解码，失败再退回 GB18030
        /// </summary>
        public static string DecodeText(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return string.Empty;

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

            try
            {
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding(54936).GetString(bytes);
            }
        }

        /// <summary>
        /// RFC 4180 风格解析：支持引号包裹的字段、字段内逗号、换行和 "" 转义。
        /// 首行只有制表符没有逗号时按制表符分隔（Excel 导出的 Unicode 文本）。
        /// </summary>
        public static List<List<string>> ParseCsv(string text)
        {
            var rows = new List<List<string>>();
            if (string.IsNullOrEmpty(text)) return rows;

            if (text[0] == '﻿') text = text.Substring(1);

            char delimiter = ',';
            int firstLineEnd = text.IndexOfAny(new[] { '\r', '\n' });
            string firstLine = firstLineEnd >= 0 ? text.Substring(0, firstLineEnd) : text;
            if (firstLine.IndexOf(',') < 0 && firstLine.IndexOf('\t') >= 0)
            {
                delimiter = '\t';
            }

            var row = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;
            int i = 0;

            while (i < text.Length)
            {
                char c = text[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i += 2;
                            continue;
                        }
                        inQuotes = false;
                        i++;
                        continue;
                    }
                    field.Append(c);
                    i++;
                    continue;
                }

                if (c == '"' && field.ToString().Trim().Length == 0)
                {
                    field.Clear();
                    inQuotes = true;
                    i++;
                    continue;
                }

                if (c == delimiter)
                {
                    row.Add(field.ToString());
                    field.Clear();
                    i++;
                    continue;
                }

                if (c == '\r' || c == '\n')
                {
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = new List<string>();
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    i++;
                    continue;
                }

                field.Append(c);
                i++;
            }

            if (field.Length > 0 || row.Count > 0 || inQuotes)
            {
                row.Add(field.ToString());
                rows.Add(row);
            }

            return rows;
        }

        /// <summary>
        /// 把解析后的行转换成任务行。有表头时按列名对应（列顺序随意），
        /// 没有表头时固定为「提示词, 文件名, 平台」。
        /// </summary>
        public static BatchImageCsvResult BuildItems(List<List<string>> rows, IList<AiPlatform> platforms, AiPlatform defaultPlatform)
        {
            var result = new BatchImageCsvResult();
            var dataRows = (rows ?? new List<List<string>>()).Where(r => r != null && r.Any(c => !string.IsNullOrWhiteSpace(c))).ToList();
            if (dataRows.Count == 0)
            {
                result.Error = LanguageManager.Instance["Batch_Csv_Empty"];
                return result;
            }

            int promptCol = 0, fileCol = 1, platformCol = 2;
            if (IsHeaderRow(dataRows[0]))
            {
                var header = dataRows[0].Select(NormalizeHeader).ToList();
                promptCol = header.FindIndex(h => PromptHeaders.Contains(h));
                fileCol = header.FindIndex(h => FileNameHeaders.Contains(h));
                platformCol = header.FindIndex(h => PlatformHeaders.Contains(h));
                dataRows.RemoveAt(0);

                if (promptCol < 0)
                {
                    result.Error = LanguageManager.Instance["Batch_Csv_NoPromptColumn"];
                    return result;
                }
                if (dataRows.Count == 0)
                {
                    result.Error = LanguageManager.Instance["Batch_Csv_Empty"];
                    return result;
                }
            }

            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int index = 0;
            foreach (var row in dataRows)
            {
                index++;
                var item = new BatchImageItem
                {
                    Index = index,
                    Prompt = Cell(row, promptCol).Trim(),
                    RawFileName = Cell(row, fileCol).Trim(),
                    RawPlatform = Cell(row, platformCol).Trim()
                };

                item.FileName = MakeUniqueFileName(SanitizeFileName(item.RawFileName) ?? index.ToString("D3"), index, usedNames);
                ApplyPlatform(item, platforms, defaultPlatform);

                if (item.Status != BatchImageItemStatus.Invalid && string.IsNullOrWhiteSpace(item.Prompt))
                {
                    item.Status = BatchImageItemStatus.Invalid;
                    item.Message = LanguageManager.Instance["Batch_Msg_EmptyPrompt"];
                }

                result.Items.Add(item);
            }

            return result;
        }

        /// <summary>
        /// 解析行的目标平台；找不到时把行标为无效
        /// </summary>
        public static void ApplyPlatform(BatchImageItem item, IList<AiPlatform> platforms, AiPlatform defaultPlatform)
        {
            if (item == null) return;

            var platform = ResolvePlatform(item.RawPlatform, platforms, defaultPlatform);
            item.Platform = platform;
            if (platform == null)
            {
                item.Status = BatchImageItemStatus.Invalid;
                item.Message = string.IsNullOrWhiteSpace(item.RawPlatform)
                    ? LanguageManager.Instance["Batch_Msg_NoPlatform"]
                    : LanguageManager.Instance.GetString("Batch_Msg_PlatformNotFound", item.RawPlatform);
            }
        }

        /// <summary>
        /// 平台列：按名称（忽略大小写）或 Id 匹配；为空时使用默认平台；找不到返回 null
        /// </summary>
        public static AiPlatform ResolvePlatform(string raw, IList<AiPlatform> platforms, AiPlatform defaultPlatform)
        {
            if (string.IsNullOrWhiteSpace(raw)) return defaultPlatform;
            if (platforms == null) return null;

            string key = raw.Trim();
            return platforms.FirstOrDefault(p => p != null && string.Equals((p.Name ?? "").Trim(), key, StringComparison.OrdinalIgnoreCase))
                ?? platforms.FirstOrDefault(p => p != null && string.Equals(p.Id, key, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsHeaderRow(List<string> row)
        {
            return row.Select(NormalizeHeader).Any(h =>
                PromptHeaders.Contains(h) || FileNameHeaders.Contains(h) || PlatformHeaders.Contains(h));
        }

        private static string NormalizeHeader(string cell)
        {
            return (cell ?? "").Trim().ToLowerInvariant();
        }

        private static string Cell(List<string> row, int col)
        {
            return col >= 0 && col < row.Count ? (row[col] ?? "") : "";
        }

        #endregion

        #region Prompt & file names

        /// <summary>
        /// 操作提示词为空时直接用 CSV 提示词；含 {content} 时替换；否则拼在后面
        /// </summary>
        public static string MergePrompt(string actionPrompt, string csvPrompt)
        {
            string content = csvPrompt ?? "";
            if (string.IsNullOrWhiteSpace(actionPrompt)) return content;
            if (actionPrompt.Contains("{content}")) return actionPrompt.Replace("{content}", content);
            return actionPrompt.TrimEnd() + "\n\n" + content;
        }

        /// <summary>
        /// 清洗 CSV 中的文件名：只保留文件名部分，替换非法字符，去掉图片扩展名
        /// （实际扩展名由图片数据决定）。结果为空时返回 null。
        /// </summary>
        public static string SanitizeFileName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            string name = raw.Trim().Replace('/', '\\');
            int slash = name.LastIndexOf('\\');
            if (slash >= 0) name = name.Substring(slash + 1);

            var invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                sb.Append(invalid.Contains(c) || char.IsControl(c) ? '_' : c);
            }
            name = sb.ToString().Trim().TrimEnd('.', ' ');

            foreach (var ext in KnownImageExtensions)
            {
                if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(0, name.Length - ext.Length).Trim().TrimEnd('.', ' ');
                    break;
                }
            }

            if (name.Length > MaxFileNameLength)
            {
                name = name.Substring(0, MaxFileNameLength).Trim().TrimEnd('.', ' ');
            }

            if (name.Length == 0) return null;

            int dot = name.IndexOf('.');
            string stem = dot >= 0 ? name.Substring(0, dot) : name;
            if (ReservedNames.Contains(stem)) name = "_" + name;

            return name;
        }

        /// <summary>
        /// 同一批次内文件名重复时追加 _行号（Windows 文件名不区分大小写）
        /// </summary>
        public static string MakeUniqueFileName(string name, int index, HashSet<string> usedNames)
        {
            string candidate = name;
            if (usedNames.Contains(candidate))
            {
                candidate = $"{name}_{index}";
                int n = 2;
                while (usedNames.Contains(candidate))
                {
                    candidate = $"{name}_{index}_{n++}";
                }
            }
            usedNames.Add(candidate);
            return candidate;
        }

        /// <summary>
        /// 解码 base64 形式的 data:image 地址。不是这种地址或内容损坏时返回 false。
        /// </summary>
        public static bool TryDecodeDataUrl(string url, out byte[] data, out string mime)
        {
            data = null;
            mime = null;
            if (string.IsNullOrEmpty(url) || !url.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)) return false;

            int comma = url.IndexOf(',');
            if (comma < 0) return false;
            string header = url.Substring(5, comma - 5);
            if (header.IndexOf(";base64", StringComparison.OrdinalIgnoreCase) < 0) return false;

            mime = header.Split(';')[0].Trim();
            string payload = url.Substring(comma + 1);
            try
            {
                if (payload.IndexOf('%') >= 0) payload = Uri.UnescapeDataString(payload);
                data = Convert.FromBase64String(payload);
                return data.Length > 0;
            }
            catch (FormatException)
            {
                data = null;
                return false;
            }
        }

        /// <summary>
        /// 按文件头判断图片格式，识别不了时参考 MIME。不是图片（例如返回了错误页）时返回 false。
        /// </summary>
        public static bool TryGetImageExtension(byte[] data, string mime, out string extension)
        {
            extension = null;
            if (data == null || data.Length < 4) return false;

            if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 &&
                data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A)
                extension = ".png";
            else if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
                extension = ".jpg";
            else if (data[0] == 'G' && data[1] == 'I' && data[2] == 'F' && data[3] == '8')
                extension = ".gif";
            else if (data.Length >= 12 && data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F' &&
                     data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P')
                extension = ".webp";
            else if (data.Length >= 12 && data[4] == 'f' && data[5] == 't' && data[6] == 'y' && data[7] == 'p' &&
                     data[8] == 'a' && data[9] == 'v' && data[10] == 'i')
                extension = ".avif";
            else if (data[0] == 'B' && data[1] == 'M')
                extension = ".bmp";

            if (extension != null) return true;

            string m = (mime ?? "").Split(';')[0].Trim().ToLowerInvariant();
            switch (m)
            {
                case "image/png": extension = ".png"; break;
                case "image/jpeg":
                case "image/jpg": extension = ".jpg"; break;
                case "image/webp": extension = ".webp"; break;
                case "image/gif": extension = ".gif"; break;
                case "image/avif": extension = ".avif"; break;
                case "image/bmp": extension = ".bmp"; break;
            }
            return extension != null;
        }

        #endregion

        #region Output

        /// <summary>
        /// 在保存根目录下按时间新建本次批量的子目录（同一秒内重复时追加 _2、_3）
        /// </summary>
        public static string CreateOutputDirectory(string root, DateTime now)
        {
            string baseDir = Path.Combine(root, now.ToString("yyyyMMdd_HHmmss"));
            string dir = baseDir;
            int n = 2;
            while (Directory.Exists(dir))
            {
                dir = baseDir + "_" + n++;
            }
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>
        /// 第一张保存到输出目录，其余保存到备选子目录（name_2、name_3…）。返回相对输出目录的路径。
        /// </summary>
        public static List<string> SaveImages(string outputDir, string alternatesDirName, string fileName, IList<ExtractedImage> images)
        {
            var saved = new List<string>();
            if (images == null) return saved;

            for (int i = 0; i < images.Count; i++)
            {
                var image = images[i];
                bool isMain = i == 0;
                string dir = isMain ? outputDir : Path.Combine(outputDir, alternatesDirName);
                Directory.CreateDirectory(dir);

                string name = (isMain ? fileName : $"{fileName}_{i + 1}") + image.Extension;
                File.WriteAllBytes(Path.Combine(dir, name), image.Data);
                saved.Add(isMain ? name : Path.Combine(alternatesDirName, name));
            }
            return saved;
        }

        /// <summary>
        /// 写执行报告（UTF-8 BOM，Excel 打开中文不乱码）
        /// </summary>
        public static void WriteReport(string path, IEnumerable<BatchImageItem> items)
        {
            var sb = new StringBuilder();
            sb.AppendLine(LanguageManager.Instance["Batch_Report_Header"]);

            foreach (var item in items ?? Enumerable.Empty<BatchImageItem>())
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    item.Index.ToString(),
                    EscapeCsv(item.Prompt),
                    EscapeCsv(item.FileName),
                    EscapeCsv(item.PlatformName),
                    EscapeCsv(item.StatusText),
                    EscapeCsv(string.Join("; ", item.SavedFiles ?? new List<string>())),
                    EscapeCsv(item.Message),
                    item.ElapsedSeconds > 0 ? item.ElapsedSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) : ""
                }));
            }

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        public static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }

        #endregion
    }
}
