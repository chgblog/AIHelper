using System;
using AIHelper.Services;

namespace AIHelper.Tests
{
    internal static class Program
    {
        [STAThread]
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
            Run("action panel snapshot routing for text image and file", TestActionPanelSnapshotRouting);
            Run("submit result deserialization and error routing", TestSubmitResultHandling);
            Run("submit ready result deserialization", TestSubmitReadyResultHandling);

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

        private static void TestActionPanelSnapshotRouting()
        {
            var panel = new AIHelper.Views.ActionPanelControl();

            // 1. Text snapshot
            var textSnapshot = ClipboardSnapshot.FromText("test text content");
            panel.SetSnapshot(textSnapshot);
            AssertTrue(panel.CurrentSnapshot == null, "text snapshot sets CurrentSnapshot to null");
            AssertEquals("test text content", panel.GetContent(), "text snapshot populates input text");

            // 2. Image snapshot
            var imageSnapshot = ClipboardSnapshot.FromImage();
            panel.SetSnapshot(imageSnapshot);
            AssertTrue(panel.CurrentSnapshot != null && panel.CurrentSnapshot.Kind == ClipboardContentKind.Image, "image snapshot sets CurrentSnapshot to Image");
            AssertEquals(string.Empty, panel.GetContent(), "image snapshot clears text input");
            var imagePlan = HotkeyActionPlanner.Build("Action: {content}", panel.CurrentSnapshot);
            AssertTrue(imagePlan.ShouldPasteClipboard, "image plan should paste");
            AssertEquals("Action: ", imagePlan.PromptText, "image plan prompt text");

            // 3. File snapshot
            var fileSnapshot = ClipboardSnapshot.FromFileDropList(new[] { @"C:\test\sample.pdf" });
            panel.SetSnapshot(fileSnapshot);
            AssertTrue(panel.CurrentSnapshot != null && panel.CurrentSnapshot.Kind == ClipboardContentKind.FileDropList, "file snapshot sets CurrentSnapshot to FileDropList");
            AssertEquals(string.Empty, panel.GetContent(), "file snapshot clears text input");
            var filePlan = HotkeyActionPlanner.Build("Analyze: {content}", panel.CurrentSnapshot);
            AssertTrue(filePlan.ShouldPasteClipboard, "file plan should paste");
            AssertEquals("Analyze: ", filePlan.PromptText, "file plan prompt text");

            // 4. Empty snapshot
            panel.SetSnapshot(ClipboardSnapshot.Empty());
            AssertTrue(panel.CurrentSnapshot == null, "empty snapshot sets CurrentSnapshot to null");
            AssertEquals(string.Empty, panel.GetContent(), "empty snapshot clears text input");

            // 5. Clear attachment restores text mode
            panel.SetSnapshot(imageSnapshot);
            AssertTrue(panel.CurrentSnapshot != null, "snapshot set before clear");
            panel.ClearAttachment();
            AssertTrue(panel.CurrentSnapshot == null, "snapshot cleared after ClearAttachment");
            AssertEquals(string.Empty, panel.GetContent(), "text cleared after ClearAttachment");
        }

        private static void TestSubmitResultHandling()
        {
            // Valid success result
            string successJson = "{\"success\":true,\"reason\":\"CLICKED\"}";
            var successResult = Newtonsoft.Json.JsonConvert.DeserializeObject<SubmitResult>(successJson);
            AssertTrue(successResult != null && successResult.success, "success submit result parsed");
            AssertEquals("CLICKED", successResult.reason, "success reason matches");

            // Unacknowledged submit (e.g. upload incomplete or ignored)
            string unackJson = "{\"success\":false,\"reason\":\"SUBMIT_UNACKNOWLEDGED\"}";
            var unackResult = Newtonsoft.Json.JsonConvert.DeserializeObject<SubmitResult>(unackJson);
            AssertTrue(unackResult != null && !unackResult.success, "unacknowledged submit result parsed as not successful");
            AssertEquals("SUBMIT_UNACKNOWLEDGED", unackResult.reason, "unacknowledged reason matches");

            // Exception or not ready submit
            string notReadyJson = "{\"success\":false,\"reason\":\"SUBMIT_NOT_READY\"}";
            var notReadyResult = Newtonsoft.Json.JsonConvert.DeserializeObject<SubmitResult>(notReadyJson);
            AssertTrue(notReadyResult != null && !notReadyResult.success, "not ready submit result parsed as not successful");
        }

        private static void TestSubmitReadyResultHandling()
        {
            string readyJson = "{\"ready\":true,\"reason\":\"READY\"}";
            var readyResult = Newtonsoft.Json.JsonConvert.DeserializeObject<SubmitReadyResult>(readyJson);
            AssertTrue(readyResult != null && readyResult.ready, "ready result parsed as true");
            AssertEquals("READY", readyResult.reason, "ready reason matches");

            string timeoutJson = "{\"ready\":false,\"reason\":\"TIMEOUT\"}";
            var timeoutResult = Newtonsoft.Json.JsonConvert.DeserializeObject<SubmitReadyResult>(timeoutJson);
            AssertTrue(timeoutResult != null && !timeoutResult.ready, "timeout ready result parsed as false");
            AssertEquals("TIMEOUT", timeoutResult.reason, "timeout reason matches");
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
