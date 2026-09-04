using System;
using System.Collections.Generic;
using Microsoft.Win32;
using AIHelper.Models;
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
            Run("cli extract action id and more argument", TestCliExtractActionIdAndMore);
            Run("context menu registration lifecycle in HKCU", TestContextMenuRegistrationLifecycle);
            Run("context menu two level cascading registration and sync in HKCU", TestContextMenuTwoLevelRegistration);
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

        private static void TestCliExtractActionIdAndMore()
        {
            AssertEquals("act-123", SingleInstanceIpcService.ExtractActionIdArg(new[] { "--action", "act-123" }), "--action space arg");
            AssertEquals("act-456", SingleInstanceIpcService.ExtractActionIdArg(new[] { "--action=act-456" }), "--action= arg");
            AssertEquals("act-789", SingleInstanceIpcService.ExtractActionIdArg(new[] { "-action", "act-789" }), "-action space arg");
            AssertEquals(null, SingleInstanceIpcService.ExtractActionIdArg(new[] { "--file", "test.txt" }), "no action arg");

            AssertTrue(SingleInstanceIpcService.HasMoreArg(new[] { "--more" }), "--more flag detected");
            AssertTrue(SingleInstanceIpcService.HasMoreArg(new[] { "-more" }), "-more flag detected");
            AssertFalse(SingleInstanceIpcService.HasMoreArg(new[] { "--file", "test.txt" }), "no more flag");
        }

        private static void TestContextMenuRegistrationLifecycle()
        {
            bool wasRegistered = FileContextMenuService.IsContextMenuRegistered();
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
                FileContextMenuService.SetContextMenuEnabled(wasRegistered);
            }
        }

        private static void TestContextMenuTwoLevelRegistration()
        {
            bool wasRegistered = FileContextMenuService.IsContextMenuRegistered();
            var actions = new List<ActionItem>();
            for (int i = 1; i <= 12; i++)
            {
                actions.Add(new ActionItem
                {
                    Id = $"id_{i}",
                    Name = $"动作_{i}",
                    Icon = "⚡",
                    SortOrder = i,
                    Prompt = "{content}"
                });
            }

            try
            {
                bool enabled = FileContextMenuService.SetContextMenuEnabled(true, actions);
                AssertTrue(enabled, "SetContextMenuEnabled with actions succeeded");

                string basePath = @"Software\Classes\SystemFileAssociations\.txt\shell\" + FileContextMenuService.VerbName;
                using (var rootKey = Registry.CurrentUser.OpenSubKey(basePath, false))
                {
                    AssertTrue(rootKey != null, "AIHelper root key exists");
                    AssertEquals("", rootKey.GetValue("SubCommands") as string, "SubCommands value is empty string");
                    AssertTrue(!string.IsNullOrEmpty(rootKey.GetValue("MUIVerb") as string), "MUIVerb is set");
                    AssertTrue(string.IsNullOrEmpty(rootKey.GetValue("") as string), "parent key (Default) must not be set to a command or title");
                    AssertTrue(rootKey.OpenSubKey("command") == null, "root key must not have direct command subkey");

                    using (var shellKey = rootKey.OpenSubKey("shell", false))
                    {
                        AssertTrue(shellKey != null, "nested shell key exists");
                        var subKeys = shellKey.GetSubKeyNames();
                        // 10 actions + 1 more = 11 items (> 10 items)
                        AssertEquals("11", subKeys.Length.ToString(), "nested shell contains 11 items (top 10 + more)");
                        AssertEquals("01_id_1", subKeys[0], "first item is 01_id_1");
                        AssertEquals("10_id_10", subKeys[9], "tenth item is 10_id_10");
                        AssertEquals("99_more", subKeys[10], "eleventh item is 99_more");

                        using (var firstItem = shellKey.OpenSubKey("01_id_1", false))
                        {
                            AssertTrue(firstItem != null, "01_id_1 exists");
                            AssertEquals("⚡ 动作_1", firstItem.GetValue("MUIVerb") as string, "01_id_1 title matches");
                            using (var cmd = firstItem.OpenSubKey("command", false))
                            {
                                AssertTrue(cmd != null, "01_id_1 has command");
                                string cmdVal = cmd.GetValue("") as string;
                                AssertTrue(cmdVal != null && cmdVal.Contains("--action \"id_1\""), "command contains action id");
                            }
                        }

                        using (var tenthItem = shellKey.OpenSubKey("10_id_10", false))
                        {
                            AssertTrue(tenthItem != null, "10_id_10 exists");
                            AssertEquals("⚡ 动作_10", tenthItem.GetValue("MUIVerb") as string, "10_id_10 title matches");
                        }

                        using (var moreItem = shellKey.OpenSubKey("99_more", false))
                        {
                            AssertTrue(moreItem != null, "99_more exists");
                            using (var cmd = moreItem.OpenSubKey("command", false))
                            {
                                AssertTrue(cmd != null, "99_more has command");
                                string cmdVal = cmd.GetValue("") as string;
                                AssertTrue(cmdVal != null && cmdVal.Contains("--more"), "command contains --more");
                            }
                        }
                    }
                }

                // Test sync with reordered actions (swap 1 and 2, remove rest, total 3 <= 10 items)
                var updated = new List<ActionItem>
                {
                    new ActionItem { Id = "id_2", Name = "动作_2", Icon = "⚡", SortOrder = 1 },
                    new ActionItem { Id = "id_1", Name = "动作_1", Icon = "⚡", SortOrder = 2 },
                    new ActionItem { Id = "id_4", Name = "动作_4", Icon = "⚡", SortOrder = 3 }
                };

                bool synced = FileContextMenuService.SyncContextMenu(updated);
                AssertTrue(synced, "SyncContextMenu succeeded");

                using (var rootKey = Registry.CurrentUser.OpenSubKey(basePath, false))
                {
                    using (var shellKey = rootKey.OpenSubKey("shell", false))
                    {
                        var subKeys = shellKey.GetSubKeyNames();
                        // 3 actions <= 10, so NO 'more' item => exactly 3 items
                        AssertEquals("3", subKeys.Length.ToString(), "nested shell contains exactly 3 items without more");
                        AssertEquals("01_id_2", subKeys[0], "reordered first item is 01_id_2");
                        AssertEquals("02_id_1", subKeys[1], "reordered second item is 02_id_1");
                        AssertEquals("03_id_4", subKeys[2], "reordered third item is 03_id_4");
                        AssertTrue(shellKey.OpenSubKey("99_more") == null, "99_more must not exist when items <= 10");
                    }
                }

                // Test boundary: exactly 10 actions => 10 items, no 99_more
                var tenActions = new List<ActionItem>();
                for (int i = 1; i <= 10; i++)
                {
                    tenActions.Add(new ActionItem { Id = $"b_{i}", Name = $"Action_{i}", SortOrder = i });
                }
                FileContextMenuService.SyncContextMenu(tenActions);
                using (var rootKey = Registry.CurrentUser.OpenSubKey(basePath, false))
                {
                    using (var shellKey = rootKey.OpenSubKey("shell", false))
                    {
                        var subKeys = shellKey.GetSubKeyNames();
                        AssertEquals("10", subKeys.Length.ToString(), "nested shell contains exactly 10 items when count is 10");
                        AssertTrue(shellKey.OpenSubKey("99_more") == null, "99_more must not exist when items == 10");
                    }
                }

                // Test boundary: 11 actions => 10 items + 99_more = 11 items
                var elevenActions = new List<ActionItem>(tenActions);
                elevenActions.Add(new ActionItem { Id = "b_11", Name = "Action_11", SortOrder = 11 });
                FileContextMenuService.SyncContextMenu(elevenActions);
                using (var rootKey = Registry.CurrentUser.OpenSubKey(basePath, false))
                {
                    using (var shellKey = rootKey.OpenSubKey("shell", false))
                    {
                        var subKeys = shellKey.GetSubKeyNames();
                        AssertEquals("11", subKeys.Length.ToString(), "nested shell contains 11 items when count is 11 (10 + more)");
                        AssertTrue(shellKey.OpenSubKey("99_more") != null, "99_more must exist when items == 11");
                    }
                }
            }
            finally
            {
                FileContextMenuService.SetContextMenuEnabled(wasRegistered);
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
