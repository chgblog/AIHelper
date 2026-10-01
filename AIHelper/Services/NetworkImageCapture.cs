// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Web.WebView2.Core;

namespace AIHelper.Services
{
    /// <summary>
    /// 批量生图期间缓存页面收到的图片响应。页面内 fetch 受 CORS 限制拿不到跨域 CDN 上的
    /// 图片时，从这里按 URL 取回原始字节。
    /// </summary>
    internal sealed class NetworkImageCapture : IDisposable
    {
        // 小于这个尺寸的多半是图标、头像
        private const int MinImageBytes = 4 * 1024;
        private const long MaxTotalBytes = 256L * 1024 * 1024;

        private readonly CoreWebView2 _core;
        private readonly object _lock = new object();
        private readonly Dictionary<string, CapturedImage> _images = new Dictionary<string, CapturedImage>(StringComparer.Ordinal);
        private long _totalBytes;
        private bool _disposed;

        public class CapturedImage
        {
            public byte[] Data { get; set; }
            public string ContentType { get; set; }
        }

        public NetworkImageCapture(CoreWebView2 core)
        {
            _core = core;
            if (_core != null)
            {
                _core.WebResourceResponseReceived += OnResponseReceived;
            }
        }

        private async void OnResponseReceived(object sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
        {
            try
            {
                var response = e.Response;
                if (_disposed || response == null || response.StatusCode < 200 || response.StatusCode >= 300) return;

                string contentType = response.Headers != null && response.Headers.Contains("Content-Type")
                    ? response.Headers.GetHeader("Content-Type") ?? ""
                    : "";
                if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                    contentType.IndexOf("svg", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return;
                }

                string url = e.Request?.Uri;
                if (string.IsNullOrEmpty(url)) return;

                using (var stream = await response.GetContentAsync())
                {
                    if (stream == null || _disposed) return;
                    using (var ms = new MemoryStream())
                    {
                        await stream.CopyToAsync(ms);
                        if (ms.Length < MinImageBytes) return;
                        Store(url, new CapturedImage { Data = ms.ToArray(), ContentType = contentType });
                    }
                }
            }
            catch (Exception ex)
            {
                // Content is not always retrievable (e.g. served from cache); the in-page fetch covers those
                System.Diagnostics.Debug.WriteLine($"Image capture failed: {ex.Message}");
            }
        }

        private void Store(string url, CapturedImage image)
        {
            lock (_lock)
            {
                if (_disposed) return;
                if (_totalBytes + image.Data.Length > MaxTotalBytes)
                {
                    _images.Clear();
                    _totalBytes = 0;
                }
                if (_images.TryGetValue(url, out var old))
                {
                    _totalBytes -= old.Data.Length;
                }
                _images[url] = image;
                _totalBytes += image.Data.Length;
            }
        }

        public bool TryGet(string url, out CapturedImage image)
        {
            image = null;
            if (string.IsNullOrEmpty(url)) return false;

            lock (_lock)
            {
                if (_images.TryGetValue(url, out image)) return true;

                int hash = url.IndexOf('#');
                return hash > 0 && _images.TryGetValue(url.Substring(0, hash), out image);
            }
        }

        /// <summary>
        /// 每行开始前清空，避免上一行的图片被误用
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _images.Clear();
                _totalBytes = 0;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (_core != null)
                {
                    _core.WebResourceResponseReceived -= OnResponseReceived;
                }
            }
            catch
            {
                // The CoreWebView2 may already be gone after a proxy rebuild
            }
            Clear();
        }
    }
}
