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
            Run("action panel close functionality and event notification", TestActionPanelClose);
            Run("submit result deserialization and error routing", TestSubmitResultHandling);
            Run("submit ready result deserialization", TestSubmitReadyResultHandling);
            Run("selection toolbar point in rect hit testing", TestSelectionToolbarPointInRect);
            Run("selection toolbar left click outside dismiss routing", TestSelectionToolbarDismissOnLeftClick);
            Run("text selection service triggers dismiss on left click outside", TestTextSelectionServiceDismissOnLeftClick);
            Run("text selection service allows self window selection and ignores toolbar clicks", TestTextSelectionServiceSelfWindowAndToolbarClick);
            Run("selection toolbar more expansion exclusion back button and multi-line layout", TestSelectionToolbarMoreExpansionAndMultiLineLayout);
            Run("selection toolbar window interactive more and back toggle", TestSelectionToolbarWindowInteractiveMoreAndBack);
            Run("quick action start new chat settings and language keys", TestQuickActionStartNewChatSettingsAndLanguage);
            Run("simulate visit settings and language keys", TestSimulateVisitSettingsAndLanguage);

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

        private static void TestActionPanelClose()
        {
            var panel = new AIHelper.Views.ActionPanelControl();
            panel.Visibility = System.Windows.Visibility.Visible;

            bool closeRequestedFired = false;
            panel.CloseRequested += () =>
            {
                closeRequestedFired = true;
            };

            // Call Close()
            panel.Close();

            AssertTrue(panel.Visibility == System.Windows.Visibility.Collapsed, "panel Visibility should be Collapsed after Close()");
            AssertTrue(closeRequestedFired, "CloseRequested event should be fired");

            // Localized string verification
            string closeTextZh = LanguageManager.Instance["ActionPanel_Close"];
            AssertTrue(!string.IsNullOrEmpty(closeTextZh), "ActionPanel_Close should have localized string");
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

        private static void TestSelectionToolbarPointInRect()
        {
            var rect = new Win32Api.RECT { Left = 100, Top = 200, Right = 300, Bottom = 400 };

            // Inside
            AssertTrue(AIHelper.Views.SelectionToolbarWindow.IsPointInRect(new System.Windows.Point(150, 250), rect), "point inside rect should return true");
            AssertTrue(AIHelper.Views.SelectionToolbarWindow.IsPointInRect(new System.Windows.Point(100, 200), rect), "top-left border should return true");
            AssertTrue(AIHelper.Views.SelectionToolbarWindow.IsPointInRect(new System.Windows.Point(300, 400), rect), "bottom-right border should return true");

            // Outside
            AssertFalse(AIHelper.Views.SelectionToolbarWindow.IsPointInRect(new System.Windows.Point(99, 250), rect), "left of rect should return false");
            AssertFalse(AIHelper.Views.SelectionToolbarWindow.IsPointInRect(new System.Windows.Point(301, 250), rect), "right of rect should return false");
            AssertFalse(AIHelper.Views.SelectionToolbarWindow.IsPointInRect(new System.Windows.Point(150, 199), rect), "above rect should return false");
            AssertFalse(AIHelper.Views.SelectionToolbarWindow.IsPointInRect(new System.Windows.Point(150, 401), rect), "below rect should return false");

            // Negative coordinates (multi-monitor scenario)
            var multiMonitorRect = new Win32Api.RECT { Left = -1920, Top = -500, Right = -920, Bottom = 0 };
            AssertTrue(AIHelper.Views.SelectionToolbarWindow.IsPointInRect(new System.Windows.Point(-1500, -200), multiMonitorRect), "point inside multi-monitor rect");
            AssertFalse(AIHelper.Views.SelectionToolbarWindow.IsPointInRect(new System.Windows.Point(-2000, -200), multiMonitorRect), "point left of multi-monitor rect");
            AssertFalse(AIHelper.Views.SelectionToolbarWindow.IsPointInRect(new System.Windows.Point(0, 0), multiMonitorRect), "point right of multi-monitor rect");
        }

        private static void TestSelectionToolbarDismissOnLeftClick()
        {
            bool isToolbarVisible = false;
            var rect = new Win32Api.RECT { Left = 500, Top = 300, Right = 700, Bottom = 350 };
            Func<System.Windows.Point, bool> isPointInside = pt => AIHelper.Views.SelectionToolbarWindow.IsPointInRect(pt, rect);

            Func<System.Windows.Point, bool> shouldDismiss = pt => isToolbarVisible && !isPointInside(pt);

            // Toolbar not visible: always false
            isToolbarVisible = false;
            AssertFalse(shouldDismiss(new System.Windows.Point(100, 100)), "hidden toolbar should not dismiss on left click outside");
            AssertFalse(shouldDismiss(new System.Windows.Point(550, 320)), "hidden toolbar should not dismiss on left click inside");

            // Toolbar visible: inside should NOT dismiss
            isToolbarVisible = true;
            AssertFalse(shouldDismiss(new System.Windows.Point(550, 320)), "visible toolbar should not dismiss when left clicking inside");
            AssertFalse(shouldDismiss(new System.Windows.Point(500, 300)), "visible toolbar should not dismiss when left clicking border");

            // Toolbar visible: outside SHOULD dismiss
            AssertTrue(shouldDismiss(new System.Windows.Point(100, 100)), "visible toolbar must dismiss when left clicking outside");
            AssertTrue(shouldDismiss(new System.Windows.Point(800, 500)), "visible toolbar must dismiss when left clicking outside");
        }

        private static void TestTextSelectionServiceDismissOnLeftClick()
        {
            var service = TextSelectionService.Instance;
            bool dismissFired = false;
            Action onDismiss = () => { dismissFired = true; };
            service.DismissRequested += onDismiss;

            try
            {
                // When ShouldDismissOnLeftClick is null
                service.ShouldDismissOnLeftClick = null;
                dismissFired = false;
                service.HandleLeftButtonDownForTesting(new System.Windows.Point(100, 100));
                AssertFalse(dismissFired, "dismiss should not fire when ShouldDismissOnLeftClick is null");

                // When ShouldDismissOnLeftClick returns false (e.g. clicked inside toolbar)
                service.ShouldDismissOnLeftClick = pt => false;
                dismissFired = false;
                service.HandleLeftButtonDownForTesting(new System.Windows.Point(100, 100));
                AssertFalse(dismissFired, "dismiss should not fire when ShouldDismissOnLeftClick returns false");

                // When ShouldDismissOnLeftClick returns true (e.g. clicked outside toolbar)
                service.ShouldDismissOnLeftClick = pt => true;
                dismissFired = false;
                service.HandleLeftButtonDownForTesting(new System.Windows.Point(100, 100));
                AssertTrue(dismissFired, "dismiss must fire when ShouldDismissOnLeftClick returns true");
            }
            finally
            {
                service.DismissRequested -= onDismiss;
                service.ShouldDismissOnLeftClick = null;
            }
        }

        private static void TestTextSelectionServiceSelfWindowAndToolbarClick()
        {
            var service = TextSelectionService.Instance;
            uint currentPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;

            try
            {
                service.AppScopeMode = 0;
                service.IsPointInsideToolbar = null;

                // 1. Own process window must be in scope by default
                AssertTrue(service.IsProcessInScope(currentPid), "current process window should be in scope by default");

                // 2. Dragging/selecting inside own window should be allowed when toolbar is not clicked
                AssertTrue(service.ShouldProcessSelection(new System.Windows.Point(10, 10), new System.Windows.Point(50, 50), currentPid),
                    "selection inside self window should be allowed");

                // 3. Configure toolbar area: (100, 100) to (200, 150)
                service.IsPointInsideToolbar = pt => pt.X >= 100 && pt.X <= 200 && pt.Y >= 100 && pt.Y <= 150;

                // Mouse down inside toolbar
                AssertFalse(service.ShouldProcessSelection(new System.Windows.Point(120, 120), new System.Windows.Point(250, 250), currentPid),
                    "drag starting inside toolbar should be ignored");

                // Mouse up inside toolbar
                AssertFalse(service.ShouldProcessSelection(new System.Windows.Point(50, 50), new System.Windows.Point(150, 120), currentPid),
                    "drag ending inside toolbar should be ignored");

                // Drag completely outside toolbar in self window
                AssertTrue(service.ShouldProcessSelection(new System.Windows.Point(10, 10), new System.Windows.Point(50, 50), currentPid),
                    "selection outside toolbar in self window should be processed");
            }
            finally
            {
                service.IsPointInsideToolbar = null;
                service.AppScopeMode = 0;
            }
        }

        private static void TestSelectionToolbarMoreExpansionAndMultiLineLayout()
        {
            List<ActionItem> CreateActions(int count)
            {
                var list = new List<ActionItem>();
                for (int i = 1; i <= count; i++)
                {
                    list.Add(new ActionItem { Id = $"action_{i}", Name = $"操作{i}", SortOrder = i, Prompt = $"Prompt {i}" });
                }
                return list;
            }

            // 1. 默认状态，5项及以内，不出现“更多”，保留复制
            var actions3 = CreateActions(3);
            var layoutDefault3 = AIHelper.Views.SelectionToolbarWindow.ComputeButtonLayout(actions3, isExpanded: false, copyMode: 1);
            AssertEquals("1", layoutDefault3.Count.ToString(), "default <= 5 actions should be 1 row");
            AssertEquals("4", layoutDefault3[0].Count.ToString(), "row should have 4 items (copy + 3 actions)");
            AssertEquals("📋 复制", layoutDefault3[0][0], "first item should be copy when copyMode=1");
            AssertEquals("操作1", layoutDefault3[0][1], "second item should be action 1");

            // 2. 默认状态，超过5项，显示前5项与“更多 ▾”
            var actions8 = CreateActions(8);
            var layoutDefault8 = AIHelper.Views.SelectionToolbarWindow.ComputeButtonLayout(actions8, isExpanded: false, copyMode: 2);
            AssertEquals("1", layoutDefault8.Count.ToString(), "default > 5 actions should be 1 row");
            AssertEquals("7", layoutDefault8[0].Count.ToString(), "row should have 7 items (5 actions + more + copy)");
            AssertEquals("操作1", layoutDefault8[0][0], "first item should be action 1");
            AssertEquals("操作5", layoutDefault8[0][4], "fifth item should be action 5");
            AssertEquals("更多 ▾", layoutDefault8[0][5], "sixth item should be More");
            AssertEquals("📋 复制", layoutDefault8[0][6], "seventh item should be copy at last position");

            // 3. 点击“更多”展开：排除复制，排除前5项，第一项加返回
            // 剩余项 = 8 - 5 = 3 项 (<= 6)，应保持单行
            var layoutExpanded8 = AIHelper.Views.SelectionToolbarWindow.ComputeButtonLayout(actions8, isExpanded: true, copyMode: 2);
            AssertEquals("1", layoutExpanded8.Count.ToString(), "expanded with 3 remaining actions should be 1 row");
            AssertEquals("4", layoutExpanded8[0].Count.ToString(), "row should have 4 items (Back + 3 remaining actions)");
            AssertEquals("◀ 返回", layoutExpanded8[0][0], "first item must be Back button");
            AssertEquals("操作6", layoutExpanded8[0][1], "first remaining action should be action 6 (first 5 excluded)");
            AssertEquals("操作7", layoutExpanded8[0][2], "second remaining action should be action 7");
            AssertEquals("操作8", layoutExpanded8[0][3], "third remaining action should be action 8");
            AssertFalse(layoutExpanded8[0].Contains("📋 复制"), "copy button must be excluded in expanded view");

            // 4. 点击“更多”展开：剩余项恰好为 6 项 (总共 11 项，11 - 5 = 6)，未超过 6 项，应保持单行
            var actions11 = CreateActions(11);
            var layoutExpanded11 = AIHelper.Views.SelectionToolbarWindow.ComputeButtonLayout(actions11, isExpanded: true, copyMode: 1);
            AssertEquals("1", layoutExpanded11.Count.ToString(), "expanded with 6 remaining actions should still be 1 row");
            AssertEquals("7", layoutExpanded11[0].Count.ToString(), "row should have 7 items (Back + 6 remaining actions)");
            AssertEquals("◀ 返回", layoutExpanded11[0][0], "first item must be Back button");
            AssertEquals("操作6", layoutExpanded11[0][1], "second item action 6");
            AssertEquals("操作11", layoutExpanded11[0][6], "seventh item action 11");
            AssertFalse(layoutExpanded11[0].Contains("📋 复制"), "copy button must be excluded in expanded view");

            // 5. 点击“更多”展开：剩余项超过 6 项 (总共 12 项，剩余 7 项 > 6)，触发多行显示！
            // 行1: 返回 + 前6项剩余操作 (操作6~操作11)
            // 行2: 剩余第7项操作 (操作12)
            var actions12 = CreateActions(12);
            var layoutExpanded12 = AIHelper.Views.SelectionToolbarWindow.ComputeButtonLayout(actions12, isExpanded: true, copyMode: 1);
            AssertEquals("2", layoutExpanded12.Count.ToString(), "expanded with 7 remaining actions must be 2 rows");
            AssertEquals("7", layoutExpanded12[0].Count.ToString(), "row 1 should have 7 items (Back + 6 actions)");
            AssertEquals("◀ 返回", layoutExpanded12[0][0], "row 1 first item is Back");
            AssertEquals("操作6", layoutExpanded12[0][1], "row 1 has action 6");
            AssertEquals("操作11", layoutExpanded12[0][6], "row 1 has action 11");
            AssertEquals("1", layoutExpanded12[1].Count.ToString(), "row 2 should have 1 item (action 12)");
            AssertEquals("操作12", layoutExpanded12[1][0], "row 2 first item is action 12");
            AssertFalse(layoutExpanded12[0].Contains("📋 复制") || layoutExpanded12[1].Contains("📋 复制"), "copy button must be excluded in all rows");

            // 6. 点击“更多”展开：剩余项为 13 项 (总共 18 项，剩余 13 项)，超过 2 行容量 (6*2)，分为 3 行！
            // 行1: 返回 + 6项 (操作6~11)
            // 行2: 6项 (操作12~17)
            // 行3: 1项 (操作18)
            var actions18 = CreateActions(18);
            var layoutExpanded18 = AIHelper.Views.SelectionToolbarWindow.ComputeButtonLayout(actions18, isExpanded: true, copyMode: 0);
            AssertEquals("3", layoutExpanded18.Count.ToString(), "expanded with 13 remaining actions must be 3 rows");
            AssertEquals("7", layoutExpanded18[0].Count.ToString(), "row 1 has 7 items");
            AssertEquals("6", layoutExpanded18[1].Count.ToString(), "row 2 has 6 items");
            AssertEquals("1", layoutExpanded18[2].Count.ToString(), "row 3 has 1 item");
            AssertEquals("操作18", layoutExpanded18[2][0], "row 3 item is action 18");
        }

        private static void TestSelectionToolbarWindowInteractiveMoreAndBack()
        {
            var list = new List<ActionItem>();
            for (int i = 1; i <= 12; i++)
            {
                list.Add(new ActionItem { Id = $"act_{i}", Name = $"动作{i}", SortOrder = i, Prompt = $"P{i}" });
            }

            var toolbar = new AIHelper.Views.SelectionToolbarWindow();

            // 1. 初始化为默认未展开状态，copyMode = 1
            toolbar.SetupForTesting(list, copyMode: 1, isExpanded: false);
            AssertFalse(toolbar.IsExpandedForTesting, "initially not expanded");
            AssertEquals("1", toolbar.RowCountForTesting.ToString(), "initial view should have 1 row");

            var visualDefault = toolbar.GetCurrentVisualButtonLayoutForTesting();
            AssertEquals("1", visualDefault.Count.ToString(), "visual rows count should be 1");
            AssertEquals("📋 复制", visualDefault[0][0], "first button should be copy");
            AssertEquals("动作1", visualDefault[0][1], "second button should be action 1");
            AssertEquals("更多 ▾", visualDefault[0][6], "seventh button should be More");

            // 2. 模拟点击“更多 ▾”
            toolbar.TriggerMoreClickForTesting();
            AssertTrue(toolbar.IsExpandedForTesting, "after more click, isExpanded should be true");
            AssertEquals("2", toolbar.RowCountForTesting.ToString(), "12 actions (7 remaining > 6) should generate 2 rows");

            var visualExpanded = toolbar.GetCurrentVisualButtonLayoutForTesting();
            AssertEquals("2", visualExpanded.Count.ToString(), "expanded visual rows count should be 2");
            AssertEquals("◀ 返回", visualExpanded[0][0], "expanded row 1 first button should be Back");
            AssertEquals("动作6", visualExpanded[0][1], "expanded row 1 second button should be action 6 (actions 1-5 excluded)");
            AssertEquals("动作11", visualExpanded[0][6], "expanded row 1 last button should be action 11");
            AssertEquals("动作12", visualExpanded[1][0], "expanded row 2 first button should be action 12");
            AssertFalse(visualExpanded[0].Exists(t => t.Contains("复制")), "copy button should be excluded in row 1");
            AssertFalse(visualExpanded[1].Exists(t => t.Contains("复制")), "copy button should be excluded in row 2");

            // 3. 模拟点击“◀ 返回”返回默认操作
            toolbar.TriggerBackClickForTesting();
            AssertFalse(toolbar.IsExpandedForTesting, "after back click, isExpanded should be false");
            AssertEquals("1", toolbar.RowCountForTesting.ToString(), "restored default view should have 1 row");

            var visualRestored = toolbar.GetCurrentVisualButtonLayoutForTesting();
            AssertEquals("1", visualRestored.Count.ToString(), "restored visual rows count should be 1");
            AssertEquals("📋 复制", visualRestored[0][0], "first button should be copy again");
            AssertEquals("动作1", visualRestored[0][1], "second button should be action 1 again");
            AssertEquals("更多 ▾", visualRestored[0][6], "seventh button should be More again");
        }

        private static void TestQuickActionStartNewChatSettingsAndLanguage()
        {
            // 1. 默认设置检测
            var defaultSettings = AppSettings.CreateDefault();
            AssertTrue(defaultSettings.QuickActionStartNewChat, "QuickActionStartNewChat should default to true in CreateDefault");

            var newSettings = new AppSettings();
            AssertTrue(newSettings.QuickActionStartNewChat, "QuickActionStartNewChat should default to true in new instance");

            // 2. 序列化与反序列化（旧配置无此字段时反序列化自动保持默认 true）
            string jsonWithoutField = "{}";
            var deserializedWithout = Newtonsoft.Json.JsonConvert.DeserializeObject<AppSettings>(jsonWithoutField);
            AssertTrue(deserializedWithout.QuickActionStartNewChat, "Deserializing without QuickActionStartNewChat should default to true");

            // 3. 显式设置为 false 后的序列化反序列化
            deserializedWithout.QuickActionStartNewChat = false;
            string jsonWithFalse = Newtonsoft.Json.JsonConvert.SerializeObject(deserializedWithout);
            var deserializedWithFalse = Newtonsoft.Json.JsonConvert.DeserializeObject<AppSettings>(jsonWithFalse);
            AssertFalse(deserializedWithFalse.QuickActionStartNewChat, "QuickActionStartNewChat set to false should be preserved");

            // 4. 多语言键检测
            string originalLang = LanguageManager.Instance.CurrentLanguage;
            try
            {
                LanguageManager.Instance.CurrentLanguage = "zh";
                AssertEquals("开启新会话", LanguageManager.Instance["Main_NewChatOption"], "ZH Main_NewChatOption");
                AssertTrue(!string.IsNullOrEmpty(LanguageManager.Instance["Main_NewChatOption_Tip"]), "ZH Main_NewChatOption_Tip not empty");

                LanguageManager.Instance.CurrentLanguage = "en";
                AssertEquals("New Chat", LanguageManager.Instance["Main_NewChatOption"], "EN Main_NewChatOption");
                AssertTrue(!string.IsNullOrEmpty(LanguageManager.Instance["Main_NewChatOption_Tip"]), "EN Main_NewChatOption_Tip not empty");
            }
            finally
            {
                LanguageManager.Instance.CurrentLanguage = originalLang;
            }
        }

        private static void TestSimulateVisitSettingsAndLanguage()
        {
            // 1. Default settings validation
            var defaultSettings = AppSettings.CreateDefault();
            AssertEquals("0.5", defaultSettings.SimulateVisitMinIntervalSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), "Default min interval");
            AssertEquals("3.0", defaultSettings.SimulateVisitMaxIntervalSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), "Default max interval");
            AssertEquals("100", defaultSettings.SimulateVisitMinScrollDistance.ToString(), "Default min distance");
            AssertEquals("300", defaultSettings.SimulateVisitMaxScrollDistance.ToString(), "Default max distance");
            AssertTrue(!string.IsNullOrEmpty(defaultSettings.BrowserModeLastUrl), "Default BrowserModeLastUrl not empty");
            AssertTrue(!defaultSettings.SimulateVisitUseProxy, "Default SimulateVisitUseProxy is false (system proxy)");

            // 2. Serialization and deserialization
            defaultSettings.SimulateVisitMinIntervalSeconds = 1.2;
            defaultSettings.SimulateVisitMaxIntervalSeconds = 4.5;
            defaultSettings.SimulateVisitMinScrollDistance = 150;
            defaultSettings.SimulateVisitMaxScrollDistance = 450;
            defaultSettings.SimulateVisitUseProxy = true;
            defaultSettings.BrowserModeLastUrl = "https://github.com";

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(defaultSettings);
            var deserialized = Newtonsoft.Json.JsonConvert.DeserializeObject<AppSettings>(json);
            AssertEquals("1.2", deserialized.SimulateVisitMinIntervalSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), "Deserialized min interval");
            AssertEquals("4.5", deserialized.SimulateVisitMaxIntervalSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), "Deserialized max interval");
            AssertEquals("150", deserialized.SimulateVisitMinScrollDistance.ToString(), "Deserialized min distance");
            AssertEquals("450", deserialized.SimulateVisitMaxScrollDistance.ToString(), "Deserialized max distance");
            AssertTrue(deserialized.SimulateVisitUseProxy, "Deserialized SimulateVisitUseProxy is true");
            AssertEquals("https://github.com", deserialized.BrowserModeLastUrl, "Deserialized BrowserModeLastUrl");

            // 3. Multi-language keys test
            string originalLang = LanguageManager.Instance.CurrentLanguage;
            try
            {
                LanguageManager.Instance.CurrentLanguage = "zh";
                AssertEquals("更新", LanguageManager.Instance["Settings_Tab_About"], "ZH Settings_Tab_About is 更新");
                AssertEquals("模拟访问", LanguageManager.Instance["Settings_Tab_SimulateVisit"], "ZH Settings_Tab_SimulateVisit");
                AssertEquals("切换到浏览器模式", LanguageManager.Instance["Main_SwitchToBrowserMode"], "ZH Main_SwitchToBrowserMode");
                AssertEquals("切换回 AI 模式", LanguageManager.Instance["Main_SwitchToAiMode"], "ZH Main_SwitchToAiMode");
                AssertEquals("模拟真人访问", LanguageManager.Instance["Main_SimulateHuman"], "ZH Main_SimulateHuman");
                AssertTrue(!string.IsNullOrEmpty(LanguageManager.Instance["Settings_Simulate_Title"]), "ZH Settings_Simulate_Title not empty");
                AssertEquals("网络代理设置", LanguageManager.Instance["Settings_Simulate_ProxySection"], "ZH Settings_Simulate_ProxySection");
                AssertEquals("启用代理", LanguageManager.Instance["Settings_Simulate_UseProxy"], "ZH Settings_Simulate_UseProxy");
                AssertTrue(!string.IsNullOrEmpty(LanguageManager.Instance["Settings_Simulate_UseProxyTip"]), "ZH Settings_Simulate_UseProxyTip not empty");

                LanguageManager.Instance.CurrentLanguage = "en";
                AssertEquals("Updates", LanguageManager.Instance["Settings_Tab_About"], "EN Settings_Tab_About is Updates");
                AssertEquals("Simulation", LanguageManager.Instance["Settings_Tab_SimulateVisit"], "EN Settings_Tab_SimulateVisit");
                AssertEquals("Switch to Browser Mode", LanguageManager.Instance["Main_SwitchToBrowserMode"], "EN Main_SwitchToBrowserMode");
                AssertEquals("Switch back to AI Mode", LanguageManager.Instance["Main_SwitchToAiMode"], "EN Main_SwitchToAiMode");
                AssertEquals("Simulate Human Browsing", LanguageManager.Instance["Main_SimulateHuman"], "EN Main_SimulateHuman");
                AssertTrue(!string.IsNullOrEmpty(LanguageManager.Instance["Settings_Simulate_Title"]), "EN Settings_Simulate_Title not empty");
                AssertEquals("Network Proxy Settings", LanguageManager.Instance["Settings_Simulate_ProxySection"], "EN Settings_Simulate_ProxySection");
                AssertEquals("Enable Proxy", LanguageManager.Instance["Settings_Simulate_UseProxy"], "EN Settings_Simulate_UseProxy");
                AssertTrue(!string.IsNullOrEmpty(LanguageManager.Instance["Settings_Simulate_UseProxyTip"]), "EN Settings_Simulate_UseProxyTip not empty");
            }
            finally
            {
                LanguageManager.Instance.CurrentLanguage = originalLang;
            }
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
