// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AIHelper.Services
{
    public enum ClipboardContentKind
    {
        Empty = 0,
        Text = 1,
        Image = 2,
        FileDropList = 3,
        Other = 4
    }

    public sealed class ClipboardSnapshot
    {
        public ClipboardContentKind Kind { get; }
        public string Text { get; }
        public IReadOnlyList<string> FilePaths { get; }

        private ClipboardSnapshot(ClipboardContentKind kind, string text, IEnumerable<string> filePaths)
        {
            Kind = kind;
            Text = text ?? string.Empty;
            FilePaths = new ReadOnlyCollection<string>((filePaths ?? Enumerable.Empty<string>()).ToList());
        }

        public static ClipboardSnapshot Empty()
        {
            return new ClipboardSnapshot(ClipboardContentKind.Empty, string.Empty, null);
        }

        public static ClipboardSnapshot FromText(string text)
        {
            return string.IsNullOrEmpty(text)
                ? Empty()
                : new ClipboardSnapshot(ClipboardContentKind.Text, text, null);
        }

        public static ClipboardSnapshot FromImage()
        {
            return new ClipboardSnapshot(ClipboardContentKind.Image, string.Empty, null);
        }

        public static ClipboardSnapshot FromFileDropList(IEnumerable<string> filePaths)
        {
            var list = filePaths == null ? new List<string>() : filePaths.Where(path => !string.IsNullOrWhiteSpace(path)).ToList();
            return list.Count == 0
                ? Empty()
                : new ClipboardSnapshot(ClipboardContentKind.FileDropList, string.Empty, list);
        }
    }
}
