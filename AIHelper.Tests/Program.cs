using System;
using AIHelper.Services;

namespace AIHelper.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            Run("text clipboard keeps prompt injection", TestTextClipboard);
            Run("image clipboard requests paste flow", TestImageClipboard);
            Run("file clipboard requests paste flow", TestFileClipboard);
            Run("attachment clipboard stops when paste is not detected", TestAttachmentPasteGate);
            Run("attachment timeout maps to warning status", TestAttachmentTimeoutStatus);
            Run("context menu supported extensions", TestContextMenuSupportedExtensions);
            Run("cli extract file argument", TestCliExtractFileArg);
            Run("context menu registration lifecycle in HKCU", TestContextMenuRegistrationLifecycle);
            Run("windows 11 detection helper", TestWindows11Detection);

            Console.WriteLine("All tests passed.");
            return 0;
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine("[PASS] " + name);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[FAIL] " + name);
                Console.Error.WriteLine(ex.Message);
                Environment.Exit(1);
            }
        }

        private static void TestTextClipboard()
        {
            var snapshot = ClipboardSnapshot.FromText("hello");
            var plan = HotkeyActionPlanner.Build("Prompt: {content}", snapshot);

            AssertFalse(plan.ShouldPasteClipboard, "text should not paste clipboard");
            AssertEquals("Prompt: hello", plan.PromptText, "text prompt replacement");
        }

        private static void TestImageClipboard()
        {
            var snapshot = ClipboardSnapshot.FromImage();
            var plan = HotkeyActionPlanner.Build("Prompt: {content}", snapshot);

            AssertTrue(plan.ShouldPasteClipboard, "image should paste clipboard");
            AssertTrue(plan.ShouldWaitForUpload, "image should wait for upload");
            AssertEquals("Prompt: ", plan.PromptText, "image prompt should clear content placeholder");
        }

        private static void TestFileClipboard()
        {
            var snapshot = ClipboardSnapshot.FromFileDropList(new[] { @"C:\temp\a.pdf" });
            var plan = HotkeyActionPlanner.Build("Prompt: {content}", snapshot);

            AssertTrue(plan.ShouldPasteClipboard, "file should paste clipboard");
            AssertTrue(plan.ShouldWaitForUpload, "file should wait for upload");
            AssertEquals("Prompt: ", plan.PromptText, "file prompt should clear content placeholder");
        }

        private static void TestAttachmentPasteGate()
        {
            var imageSnapshot = ClipboardSnapshot.FromImage();
            var textSnapshot = ClipboardSnapshot.FromText("hello");

            AssertFalse(HotkeyActionPlanner.ShouldProceedAfterPaste(imageSnapshot, false), "image flow must stop when paste is not detected");
            AssertTrue(HotkeyActionPlanner.ShouldProceedAfterPaste(imageSnapshot, true), "image flow may continue after paste");
            AssertTrue(HotkeyActionPlanner.ShouldProceedAfterPaste(textSnapshot, false), "text flow should not depend on paste detection");
        }

        private static void TestAttachmentTimeoutStatus()
        {
            var imageStatus = HotkeyActionPlanner.GetUploadTimeoutStatusKey(ClipboardSnapshot.FromImage());
            var fileStatus = HotkeyActionPlanner.GetUploadTimeoutStatusKey(ClipboardSnapshot.FromFileDropList(new[] { @"C:\temp\a.pdf" }));
            var textStatus = HotkeyActionPlanner.GetUploadTimeoutStatusKey(ClipboardSnapshot.FromText("hello"));

            AssertEquals("Main_Status_AttachmentMaybeUnsupported", imageStatus, "image timeout status");
            AssertEquals("Main_Status_AttachmentMaybeUnsupported", fileStatus, "file timeout status");
            AssertEquals("Inject_Failed", textStatus, "text timeout status");
        }

        private static void TestContextMenuSupportedExtensions()
        {
            // 图片
            AssertTrue(FileContextMenuService.IsSupportedExtension(".png"), "png supported");
            AssertTrue(FileContextMenuService.IsSupportedExtension("photo.JPG"), "jpg case-insensitive supported");
            AssertTrue(FileContextMenuService.IsSupportedExtension("C:\\photos\\img.webp"), "webp path supported");

            // Office
            AssertTrue(FileContextMenuService.IsSupportedExtension(".docx"), "docx supported");
            AssertTrue(FileContextMenuService.IsSupportedExtension(".xlsx"), "xlsx supported");
            AssertTrue(FileContextMenuService.IsSupportedExtension(".pptx"), "pptx supported");

            // PDF, TXT, JSON, JS, HTML, HTM
            AssertTrue(FileContextMenuService.IsSupportedExtension(".pdf"), "pdf supported");
            AssertTrue(FileContextMenuService.IsSupportedExtension(".txt"), "txt supported");
            AssertTrue(FileContextMenuService.IsSupportedExtension(".json"), "json supported");
            AssertTrue(FileContextMenuService.IsSupportedExtension(".js"), "js supported");
            AssertTrue(FileContextMenuService.IsSupportedExtension(".html"), "html supported");
            AssertTrue(FileContextMenuService.IsSupportedExtension(".htm"), "htm supported");

            // 不支持的格式
            AssertFalse(FileContextMenuService.IsSupportedExtension(".exe"), "exe not supported");
            AssertFalse(FileContextMenuService.IsSupportedExtension(".dll"), "dll not supported");
            AssertFalse(FileContextMenuService.IsSupportedExtension(".zip"), "zip not supported");
            AssertFalse(FileContextMenuService.IsSupportedExtension(""), "empty not supported");
        }

        private static void TestCliExtractFileArg()
        {
            AssertEquals(@"C:\docs\a.pdf", SingleInstanceIpcService.ExtractFileArg(new[] { "--file", @"C:\docs\a.pdf" }), "--file space arg");
            AssertEquals(@"C:\docs\b.png", SingleInstanceIpcService.ExtractFileArg(new[] { "--file=C:\\docs\\b.png" }), "--file= arg");
            AssertEquals(@"C:\docs\c.txt", SingleInstanceIpcService.ExtractFileArg(new[] { "-file", @"C:\docs\c.txt" }), "-file space arg");
            AssertEquals(null, SingleInstanceIpcService.ExtractFileArg(new[] { "--show" }), "no file arg");
            AssertEquals(null, SingleInstanceIpcService.ExtractFileArg(new string[0]), "empty args");
        }

        private static void TestContextMenuRegistrationLifecycle()
        {
            try
            {
                bool enabled = FileContextMenuService.SetContextMenuEnabled(true);
                AssertTrue(enabled, "SetContextMenuEnabled(true) returned true");
                AssertTrue(FileContextMenuService.IsContextMenuRegistered(), "context menu is registered in HKCU");

                bool disabled = FileContextMenuService.SetContextMenuEnabled(false);
                AssertTrue(disabled, "SetContextMenuEnabled(false) returned true");
                AssertFalse(FileContextMenuService.IsContextMenuRegistered(), "context menu is unregistered in HKCU");
            }
            finally
            {
                FileContextMenuService.SetContextMenuEnabled(false);
            }
        }

        private static void TestWindows11Detection()
        {
            // On this system (Windows 11 Build 26200), IsWindows11OrGreater should return true
            bool isWin11 = FileContextMenuService.IsWindows11OrGreater();
            AssertTrue(isWin11, "IsWindows11OrGreater should return true on Windows 11");
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void AssertFalse(bool condition, string message)
        {
            if (condition) throw new InvalidOperationException(message);
        }

        private static void AssertEquals(string expected, string actual, string message)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"{message}. Expected '{expected}', got '{actual}'");
            }
        }
    }
}
