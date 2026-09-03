// Copyright (C) 2026 chgblog
// SPDX-License-Identifier: GPL-3.0
using System;

namespace AIHelper.Services
{
    public sealed class HotkeyActionPlan
    {
        public string PromptText { get; set; }
        public bool ShouldPasteClipboard { get; set; }
        public bool ShouldWaitForUpload { get; set; }
    }

    public static class HotkeyActionPlanner
    {
        public static HotkeyActionPlan Build(string promptTemplate, ClipboardSnapshot clipboard)
        {
            string template = promptTemplate ?? string.Empty;
            clipboard = clipboard ?? ClipboardSnapshot.Empty();

            switch (clipboard.Kind)
            {
                case ClipboardContentKind.Image:
                case ClipboardContentKind.FileDropList:
                    return new HotkeyActionPlan
                    {
                        PromptText = template.Replace("{content}", string.Empty),
                        ShouldPasteClipboard = true,
                        ShouldWaitForUpload = true
                    };

                case ClipboardContentKind.Text:
                    return new HotkeyActionPlan
                    {
                        PromptText = template.Replace("{content}", clipboard.Text ?? string.Empty),
                        ShouldPasteClipboard = false,
                        ShouldWaitForUpload = false
                    };

                default:
                    return new HotkeyActionPlan
                    {
                        PromptText = template.Replace("{content}", string.Empty),
                        ShouldPasteClipboard = false,
                        ShouldWaitForUpload = false
                    };
            }
        }

        public static bool IsAttachmentClipboard(ClipboardSnapshot clipboard)
        {
            return clipboard != null &&
                   (clipboard.Kind == ClipboardContentKind.Image || clipboard.Kind == ClipboardContentKind.FileDropList);
        }

        public static string GetUploadTimeoutStatusKey(ClipboardSnapshot clipboard)
        {
            return IsAttachmentClipboard(clipboard)
                ? "Main_Status_AttachmentMaybeUnsupported"
                : "Inject_Failed";
        }

        public static bool ShouldProceedAfterPaste(ClipboardSnapshot clipboard, bool pasteSucceeded)
        {
            return !IsAttachmentClipboard(clipboard) || pasteSucceeded;
        }
    }
}
