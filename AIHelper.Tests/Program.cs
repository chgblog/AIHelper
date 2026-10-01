using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
            Run("auto visit config, history and matching tests", TestAutoVisitFeatures);
            Run("auto visit select first unvisited link on return", TestAutoVisitFirstUnvisitedLinkSelection);
            Run("auto check update default settings and language keys", TestAutoCheckUpdateDefaultSettingsAndLanguage);
            Run("batch image csv parsing, header mapping and platform resolution", TestBatchImageCsvParsing);
            Run("batch image csv encoding detection", TestBatchImageCsvEncoding);
            Run("batch image prompt merge", TestBatchImagePromptMerge);
            Run("batch image file name sanitizing and dedupe", TestBatchImageFileNames);
            Run("batch image extension detection", TestBatchImageExtensionDetection);
            Run("batch image output folder, alternates and report", TestBatchImageSaveAndReport);
            Run("batch image system action and settings", TestBatchImageSystemActionAndSettings);
            Run("batch image language keys in both languages", TestBatchImageLanguageKeys);
            Run("batch image generation tracker", TestImageGenerationTracker);
            Run("batch image pause and stop", TestBatchImageRunState);
            Run("batch image window and action edit hint load", TestBatchImageWindowsLoad);
            Run("batch image page preset merge, description and persistence", TestPagePresetSteps);
            Run("batch image data url decoding", TestBatchImageDataUrl);

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

        private static void TestAutoVisitFeatures()
        {
            // 1. Test AutoVisitConfig defaults and clone
            var cfg = new AutoVisitConfig
            {
                Url = "https://news.ycombinator.com",
                LinkMatchRegex = @".*item\?id=\d+",
                MaxVisitCount = 50,
                NextPageSelector = "a.morelink",
                MinRefreshIntervalMinutes = 30,
                MaxRefreshIntervalMinutes = 60,
                IsEnabled = true
            };

            AssertEquals("https://news.ycombinator.com", cfg.Url, "cfg.Url matches");
            AssertEquals(50.ToString(), cfg.MaxVisitCount.ToString(), "cfg.MaxVisitCount default 50");
            AssertEquals(3.ToString(), cfg.MinLinkDelaySeconds.ToString(), "cfg.MinLinkDelaySeconds default 3");
            AssertEquals(8.ToString(), cfg.MaxLinkDelaySeconds.ToString(), "cfg.MaxLinkDelaySeconds default 8");
            AssertEquals("3 ~ 8", cfg.LinkDelayDisplay, "LinkDelayDisplay formatting");
            AssertEquals("30 ~ 60", cfg.RefreshIntervalDisplay, "RefreshIntervalDisplay formatting");

            var clone = cfg.Clone();
            AssertEquals(cfg.Id, clone.Id, "Clone Id matches");
            AssertEquals(cfg.Url, clone.Url, "Clone Url matches");
            AssertEquals(cfg.LinkMatchRegex, clone.LinkMatchRegex, "Clone LinkMatchRegex matches");
            AssertEquals(cfg.MaxVisitCount.ToString(), clone.MaxVisitCount.ToString(), "Clone MaxVisitCount matches");
            AssertEquals(cfg.NextPageSelector, clone.NextPageSelector, "Clone NextPageSelector matches");
            AssertEquals(cfg.MinLinkDelaySeconds.ToString(), clone.MinLinkDelaySeconds.ToString(), "Clone MinLinkDelaySeconds matches");
            AssertEquals(cfg.MaxLinkDelaySeconds.ToString(), clone.MaxLinkDelaySeconds.ToString(), "Clone MaxLinkDelaySeconds matches");
            AssertEquals(cfg.LinkDelayDisplay, clone.LinkDelayDisplay, "Clone LinkDelayDisplay matches");
            AssertEquals(cfg.MinRefreshIntervalMinutes.ToString(), clone.MinRefreshIntervalMinutes.ToString(), "Clone MinRefreshIntervalMinutes matches");
            AssertEquals(cfg.MaxRefreshIntervalMinutes.ToString(), clone.MaxRefreshIntervalMinutes.ToString(), "Clone MaxRefreshIntervalMinutes matches");
            AssertTrue(clone.IsEnabled, "Clone IsEnabled is true");
            AssertEquals(cfg.ExcludedUrls, clone.ExcludedUrls, "Clone ExcludedUrls matches");

            // 2. Test AutoVisitHistoryService URL normalization & 24h tracking
            string url1 = "https://example.com/post/100#comments";
            string url1Clean = "https://example.com/post/100";
            AssertEquals(url1Clean, AutoVisitHistoryService.NormalizeUrl(url1), "NormalizeUrl strips fragments");

            string url2 = "https://example.com/items/?a=1";
            string url2Clean = "https://example.com/items?a=1";
            AssertEquals(url2Clean, AutoVisitHistoryService.NormalizeUrl(url2), "NormalizeUrl strips trailing slash before query");

            var history = AutoVisitHistoryService.Instance;
            history.ClearHistory();
            AssertFalse(history.HasVisitedInLast24Hours("https://example.com/article/1"), "URL not visited yet");

            history.RecordVisit("https://example.com/article/1#tag");
            AssertTrue(history.HasVisitedInLast24Hours("https://example.com/article/1"), "URL visited after recording (hash agnostic)");
            AssertTrue(history.HasVisitedInLast24Hours("https://example.com/article/1#diff"), "URL visited with different fragment");
            AssertFalse(history.HasVisitedInLast24Hours("https://example.com/article/2"), "Different URL not visited");

            history.ClearHistory();
            AssertFalse(history.HasVisitedInLast24Hours("https://example.com/article/1"), "URL cleared after ClearHistory");

            // 3. Test AutoVisitService FindMatchingConfig
            var configs = new List<AutoVisitConfig>
            {
                new AutoVisitConfig
                {
                    Url = "https://news.ycombinator.com",
                    LinkMatchRegex = ".*item.*",
                    IsEnabled = true
                },
                new AutoVisitConfig
                {
                    Url = "https://example.com/blog",
                    LinkMatchRegex = ".*post.*",
                    IsEnabled = false
                }
            };

            var match1 = AutoVisitService.Instance.FindMatchingConfig("https://news.ycombinator.com/", configs);
            AssertTrue(match1 != null, "Matches root url with trailing slash");
            AssertEquals("https://news.ycombinator.com", match1.Url, "Matched config Url");

            var match2 = AutoVisitService.Instance.FindMatchingConfig("https://news.ycombinator.com/news?p=2", configs);
            AssertTrue(match2 != null, "Matches subpage / query url");

            var matchDisabled = AutoVisitService.Instance.FindMatchingConfig("https://example.com/blog", configs);
            AssertTrue(matchDisabled == null, "Disabled config does not match");

            var matchNone = AutoVisitService.Instance.FindMatchingConfig("https://bing.com", configs);
            AssertTrue(matchNone == null, "Non-matching url returns null");

            // 4. Test IsSamePage URL equivalence and hash routing distinction
            AssertTrue(AutoVisitService.Instance.IsSamePage("https://example.com", "https://example.com/"), "Root trailing slash");
            AssertTrue(AutoVisitService.Instance.IsSamePage("https://example.com/page", "https://example.com/page/"), "Path trailing slash");
            AssertTrue(AutoVisitService.Instance.IsSamePage("https://example.com/page#section", "https://example.com/page"), "Anchor stripped");
            AssertTrue(AutoVisitService.Instance.IsSamePage("https://example.com/p?a=1", "https://example.com/p?a=1"), "Query match");
            AssertFalse(AutoVisitService.Instance.IsSamePage("https://example.com/p?a=1", "https://example.com/p?a=2"), "Different query");
            AssertFalse(AutoVisitService.Instance.IsSamePage("https://example.com/p1", "https://example.com/p2"), "Different path");
            AssertFalse(AutoVisitService.Instance.IsSamePage("https://example.com/#/list", "https://example.com/#/detail/1"), "SPA hash routing distinction");
            AssertTrue(AutoVisitService.Instance.IsSamePage("https://example.com/#/list", "https://example.com/#/list"), "SPA hash routing match");

            // 5. Test Excluded URLs parsing and matching
            var parsed = AutoVisitConfig.ParseExcludedUrls(" https://a.com \r\n https://b.com , https://c.com ; https://a.com ");
            AssertEquals("3", parsed.Count.ToString(), "ParseExcludedUrls count distinct");
            AssertEquals("https://a.com", parsed[0], "ParseExcludedUrls item 0");
            AssertEquals("https://b.com", parsed[1], "ParseExcludedUrls item 1");
            AssertEquals("https://c.com", parsed[2], "ParseExcludedUrls item 2");

            var excludeRules = new List<string>
            {
                "https://example.com/login",
                "https://example.com/post/100/",
                "https://example.com/ads/*",
                "https://example.com/category/",
                "/logout"
            };

            // Exact & IsSamePage matching
            AssertTrue(AutoVisitService.Instance.IsExcludedUrl("https://example.com/login", excludeRules), "Exact excluded url");
            AssertTrue(AutoVisitService.Instance.IsExcludedUrl("https://example.com/login/", excludeRules), "Trailing slash excluded url");
            AssertTrue(AutoVisitService.Instance.IsExcludedUrl("https://example.com/login#header", excludeRules), "Anchor excluded url");
            AssertTrue(AutoVisitService.Instance.IsExcludedUrl("https://example.com/post/100", excludeRules), "Path match without trailing slash");

            // Wildcard matching
            AssertTrue(AutoVisitService.Instance.IsExcludedUrl("https://example.com/ads/banner1.jpg", excludeRules), "Wildcard excluded url");
            AssertTrue(AutoVisitService.Instance.IsExcludedUrl("https://example.com/ads/test/index.html", excludeRules), "Wildcard subpath excluded url");

            // Directory prefix matching
            AssertTrue(AutoVisitService.Instance.IsExcludedUrl("https://example.com/category/tech", excludeRules), "Directory prefix excluded url");
            AssertFalse(AutoVisitService.Instance.IsExcludedUrl("https://example.com/category-other", excludeRules), "Non-directory prefix not excluded");

            // Relative path matching
            AssertTrue(AutoVisitService.Instance.IsExcludedUrl("https://example.com/logout", excludeRules), "Relative path excluded url");

            // Non-matching
            AssertFalse(AutoVisitService.Instance.IsExcludedUrl("https://example.com/post/101", excludeRules), "Non-excluded url passes");
            AssertFalse(AutoVisitService.Instance.IsExcludedUrl("https://other.com/login", excludeRules), "Different domain passes");

            // Test string overload
            AssertTrue(AutoVisitService.Instance.IsExcludedUrl("https://example.com/login", "https://example.com/login\nhttps://example.com/other"), "String overload matches");
            AssertFalse(AutoVisitService.Instance.IsExcludedUrl("https://example.com/safe", "https://example.com/login\nhttps://example.com/other"), "String overload non-matching");

            // 6. Test Language keys in ZH and EN
            string originalLang = LanguageManager.Instance.CurrentLanguage;
            try
            {
                LanguageManager.Instance.CurrentLanguage = "zh";
                AssertEquals("自动访问管理", LanguageManager.Instance["AutoVisit_Manager_Title"], "ZH AutoVisit_Manager_Title");
                AssertEquals("自动访问网址", LanguageManager.Instance["AutoVisit_Col_Url"], "ZH AutoVisit_Col_Url");
                AssertEquals("正则匹配规则", LanguageManager.Instance["AutoVisit_Col_Regex"], "ZH AutoVisit_Col_Regex");
                AssertEquals("访问链接数", LanguageManager.Instance["AutoVisit_Col_Limit"], "ZH AutoVisit_Col_Limit");
                AssertEquals("下一页定位", LanguageManager.Instance["AutoVisit_Col_NextPage"], "ZH AutoVisit_Col_NextPage");
                AssertEquals("下一链接延迟 (秒)", LanguageManager.Instance["AutoVisit_Col_LinkDelay"], "ZH AutoVisit_Col_LinkDelay");
                AssertEquals("下一链接延迟:", LanguageManager.Instance["AutoVisit_Edit_LinkDelay"], "ZH AutoVisit_Edit_LinkDelay");
                AssertEquals("排除访问链接:", LanguageManager.Instance["AutoVisit_Edit_ExcludedUrls"], "ZH AutoVisit_Edit_ExcludedUrls");
                AssertTrue(!string.IsNullOrEmpty(LanguageManager.Instance["AutoVisit_Edit_ExcludedUrlsTT"]), "ZH AutoVisit_Edit_ExcludedUrlsTT not empty");
                AssertTrue(!string.IsNullOrEmpty(LanguageManager.Instance["AutoVisit_Edit_RefreshTip"]), "ZH AutoVisit_Edit_RefreshTip not empty");

                LanguageManager.Instance.CurrentLanguage = "en";
                AssertEquals("Auto Visit Management", LanguageManager.Instance["AutoVisit_Manager_Title"], "EN AutoVisit_Manager_Title");
                AssertEquals("Target URL", LanguageManager.Instance["AutoVisit_Col_Url"], "EN AutoVisit_Col_Url");
                AssertEquals("Regex Rule", LanguageManager.Instance["AutoVisit_Col_Regex"], "EN AutoVisit_Col_Regex");
                AssertEquals("Visit Limit", LanguageManager.Instance["AutoVisit_Col_Limit"], "EN AutoVisit_Col_Limit");
                AssertEquals("Next Page Locator", LanguageManager.Instance["AutoVisit_Col_NextPage"], "EN AutoVisit_Col_NextPage");
                AssertEquals("Link Delay (s)", LanguageManager.Instance["AutoVisit_Col_LinkDelay"], "EN AutoVisit_Col_LinkDelay");
                AssertEquals("Next Link Delay:", LanguageManager.Instance["AutoVisit_Edit_LinkDelay"], "EN AutoVisit_Edit_LinkDelay");
                AssertEquals("Exclude URLs:", LanguageManager.Instance["AutoVisit_Edit_ExcludedUrls"], "EN AutoVisit_Edit_ExcludedUrls");
                AssertTrue(!string.IsNullOrEmpty(LanguageManager.Instance["AutoVisit_Edit_ExcludedUrlsTT"]), "EN AutoVisit_Edit_ExcludedUrlsTT not empty");
            }
            finally
            {
                LanguageManager.Instance.CurrentLanguage = originalLang;
            }
        }

        private static void TestAutoVisitFirstUnvisitedLinkSelection()
        {
            var history = AutoVisitHistoryService.Instance;
            history.ClearHistory();

            string mainUrl = "https://example.com/home";
            var excluded = new List<string> { "https://example.com/ads/*", "https://example.com/login" };

            var link1 = new LinkItem { Href = "https://example.com/post/1", Title = "Post 1" };
            var link2 = new LinkItem { Href = "https://example.com/post/2", Title = "Post 2" };
            var link3 = new LinkItem { Href = "https://example.com/post/3", Title = "Post 3" };
            var linkAd = new LinkItem { Href = "https://example.com/ads/promo", Title = "Ad" };
            var linkSamePage = new LinkItem { Href = "https://example.com/home#section", Title = "Same Page Section" };

            // 1. Initial screen: link1, link2, link3
            var screenLinks = new List<LinkItem> { link1, link2, link3 };
            var picked = AutoVisitService.Instance.SelectFirstUnvisitedLink(screenLinks, mainUrl, excluded);
            AssertTrue(picked != null, "Should pick a link");
            AssertEquals(link1.Href, picked.Href, "Should pick first unvisited link (link1)");

            // 2. Mark link1 as visited
            history.RecordVisit(link1.Href);
            picked = AutoVisitService.Instance.SelectFirstUnvisitedLink(screenLinks, mainUrl, excluded);
            AssertTrue(picked != null, "Should pick next unvisited link");
            AssertEquals(link2.Href, picked.Href, "Should skip visited link1 and pick link2");

            // 3. Simulate new content appeared at top of home page screen!
            var linkNew = new LinkItem { Href = "https://example.com/post/new-999", Title = "Breaking News" };
            var screenWithNew = new List<LinkItem> { linkNew, link1, link2, link3 };
            picked = AutoVisitService.Instance.SelectFirstUnvisitedLink(screenWithNew, mainUrl, excluded);
            AssertTrue(picked != null, "Should pick a link when new content appears");
            AssertEquals(linkNew.Href, picked.Href, "Must pick the first unvisited link at the top (linkNew), not previous link");

            // 4. Same page and excluded URLs should be skipped
            var screenWithAdsAndAnchors = new List<LinkItem> { linkAd, linkSamePage, link2, link3 };
            picked = AutoVisitService.Instance.SelectFirstUnvisitedLink(screenWithAdsAndAnchors, mainUrl, excluded);
            AssertTrue(picked != null, "Should pick link skipping ad and same page anchor");
            AssertEquals(link2.Href, picked.Href, "Should skip ad and same-page anchor and pick link2");

            // 5. Mark all links as visited
            history.RecordVisit(linkNew.Href);
            history.RecordVisit(link2.Href);
            history.RecordVisit(link3.Href);

            picked = AutoVisitService.Instance.SelectFirstUnvisitedLink(screenWithNew, mainUrl, excluded);
            AssertTrue(picked == null, "Should return null when all links on screen are visited");

            // 6. Null or empty candidates
            AssertTrue(AutoVisitService.Instance.SelectFirstUnvisitedLink(null, mainUrl, excluded) == null, "Null candidates returns null");
            AssertTrue(AutoVisitService.Instance.SelectFirstUnvisitedLink(new List<LinkItem>(), mainUrl, excluded) == null, "Empty candidates returns null");

            history.ClearHistory();
        }

        private static void TestAutoCheckUpdateDefaultSettingsAndLanguage()
        {
            // 1. AppSettings default value
            var defaultSettings = AppSettings.CreateDefault();
            AssertTrue(defaultSettings.AutoCheckUpdate, "AutoCheckUpdate should default to true in CreateDefault");
            AssertEquals(AppSettings.CurrentConfigVersion, defaultSettings.ConfigVersion, "ConfigVersion should default to CurrentConfigVersion in CreateDefault");
            AssertEquals("0.8.7", AppSettings.CurrentConfigVersion, "CurrentConfigVersion should be 0.8.7");

            var newSettings = new AppSettings();
            AssertTrue(newSettings.AutoCheckUpdate, "AutoCheckUpdate should default to true in new instance");

            // 2. Deserializing empty JSON preserves default true
            string jsonWithoutField = "{}";
            var deserializedWithout = Newtonsoft.Json.JsonConvert.DeserializeObject<AppSettings>(jsonWithoutField);
            AssertTrue(deserializedWithout.AutoCheckUpdate, "Deserializing without AutoCheckUpdate should default to true");

            // 3. Explicitly setting to false is preserved
            deserializedWithout.AutoCheckUpdate = false;
            deserializedWithout.ConfigVersion = AppSettings.CurrentConfigVersion;
            string jsonWithFalse = Newtonsoft.Json.JsonConvert.SerializeObject(deserializedWithout);
            var deserializedWithFalse = Newtonsoft.Json.JsonConvert.DeserializeObject<AppSettings>(jsonWithFalse);
            AssertFalse(deserializedWithFalse.AutoCheckUpdate, "AutoCheckUpdate set to false should be preserved");
            AssertEquals("0.8.7", deserializedWithFalse.ConfigVersion, "ConfigVersion 0.8.7 should be preserved");

            // 4. Config Migration: Old user config without ConfigVersion (AutoCheckUpdate was false by old default)
            string legacyOldJson = "{\"AutoCheckUpdate\": false}";
            var legacyOldSettings = Newtonsoft.Json.JsonConvert.DeserializeObject<AppSettings>(legacyOldJson);
            AssertFalse(legacyOldSettings.AutoCheckUpdate, "Legacy json has AutoCheckUpdate false");
            AssertTrue(string.IsNullOrEmpty(legacyOldSettings.ConfigVersion), "Legacy json has null ConfigVersion");
            bool migratedOld = SettingsService.Instance.MigrateSettingsIfNeeded(legacyOldSettings);
            AssertTrue(migratedOld, "Should migrate legacy config without ConfigVersion");
            AssertTrue(legacyOldSettings.AutoCheckUpdate, "Legacy config AutoCheckUpdate should be migrated to true");
            AssertEquals("0.8.7", legacyOldSettings.ConfigVersion, "Legacy config ConfigVersion should be updated to 0.8.7");

            // 5. Config Migration: User config from prior version e.g. 0.8.6 with AutoCheckUpdate false
            string v086Json = "{\"ConfigVersion\": \"0.8.6\", \"AutoCheckUpdate\": false}";
            var v086Settings = Newtonsoft.Json.JsonConvert.DeserializeObject<AppSettings>(v086Json);
            bool migrated086 = SettingsService.Instance.MigrateSettingsIfNeeded(v086Settings);
            AssertTrue(migrated086, "Should migrate config from version < 0.8.7");
            AssertTrue(v086Settings.AutoCheckUpdate, "0.8.6 config AutoCheckUpdate should be migrated to true");
            AssertEquals("0.8.7", v086Settings.ConfigVersion, "0.8.6 config should be updated to 0.8.7");

            // 6. Config Migration: User explicitly unchecked in 0.8.7 (ConfigVersion == 0.8.7, AutoCheckUpdate == false)
            string v087UncheckedJson = "{\"ConfigVersion\": \"0.8.7\", \"AutoCheckUpdate\": false}";
            var v087Settings = Newtonsoft.Json.JsonConvert.DeserializeObject<AppSettings>(v087UncheckedJson);
            bool migrated087 = SettingsService.Instance.MigrateSettingsIfNeeded(v087Settings);
            AssertFalse(migrated087, "Should NOT migrate config that is already 0.8.7");
            AssertFalse(v087Settings.AutoCheckUpdate, "User unchecked AutoCheckUpdate in 0.8.7 should remain false");
            AssertEquals("0.8.7", v087Settings.ConfigVersion, "ConfigVersion remains 0.8.7");

            // 7. Config Migration: Future version 0.9.0 with AutoCheckUpdate false should NOT be touched
            string v090Json = "{\"ConfigVersion\": \"0.9.0\", \"AutoCheckUpdate\": false}";
            var v090Settings = Newtonsoft.Json.JsonConvert.DeserializeObject<AppSettings>(v090Json);
            bool migrated090 = SettingsService.Instance.MigrateSettingsIfNeeded(v090Settings);
            AssertFalse(migrated090, "Should NOT migrate future config version 0.9.0");
            AssertFalse(v090Settings.AutoCheckUpdate, "Future config AutoCheckUpdate remains false");

            // 8. Language keys
            string originalLang = LanguageManager.Instance.CurrentLanguage;
            try
            {
                LanguageManager.Instance.CurrentLanguage = "zh";
                AssertEquals("自动检测新版本", LanguageManager.Instance["Settings_About_AutoCheckUpdate"], "ZH Settings_About_AutoCheckUpdate");
                AssertTrue(!string.IsNullOrEmpty(LanguageManager.Instance["Settings_About_AutoCheckUpdateTip"]), "ZH Settings_About_AutoCheckUpdateTip not empty");

                LanguageManager.Instance.CurrentLanguage = "en";
                AssertEquals("Auto-check for updates", LanguageManager.Instance["Settings_About_AutoCheckUpdate"], "EN Settings_About_AutoCheckUpdate");
                AssertTrue(!string.IsNullOrEmpty(LanguageManager.Instance["Settings_About_AutoCheckUpdateTip"]), "EN Settings_About_AutoCheckUpdateTip not empty");
            }
            finally
            {
                LanguageManager.Instance.CurrentLanguage = originalLang;
            }
        }

        private static void TestBatchImageCsvParsing()
        {
            string originalLang = LanguageManager.Instance.CurrentLanguage;
            LanguageManager.Instance.CurrentLanguage = "zh";
            try
            {
                var chatgpt = new AiPlatform { Id = "p-chatgpt", Name = "ChatGPT" };
                var gemini = new AiPlatform { Id = "p-gemini", Name = "Gemini" };
                var platforms = new List<AiPlatform> { chatgpt, gemini };

                // Header in a different column order; quoted field with comma, escaped quote and newline
                string csv = "文件名,平台,提示词\r\n" +
                             "cat,chatgpt,\"一只猫, 坐在\"\"月球\"\"上\nsecond line\"\r\n" +
                             ",,水墨江南\r\n" +
                             "\r\n" +
                             "dog,Unknown,一只狗\r\n" +
                             "empty,gemini,\r\n" +
                             "cat,p-gemini,另一只猫";
                var result = BatchImageService.BuildItems(BatchImageService.ParseCsv(csv), platforms, gemini);
                AssertTrue(result.Error == null, "csv should parse without error");
                AssertEquals("5", result.Items.Count.ToString(), "blank rows are skipped");

                var first = result.Items[0];
                AssertEquals("一只猫, 坐在\"月球\"上\nsecond line", first.Prompt, "quoted field keeps comma, quote and newline");
                AssertEquals("cat", first.FileName, "file name column mapped by header");
                AssertTrue(first.Platform == chatgpt, "platform matched by name ignoring case");
                AssertEquals("Pending", first.Status.ToString(), "valid row is pending");

                var second = result.Items[1];
                AssertEquals("002", second.FileName, "empty file name falls back to row number");
                AssertTrue(second.Platform == gemini, "empty platform uses the default platform");

                AssertEquals("Invalid", result.Items[2].Status.ToString(), "unknown platform is invalid");
                AssertTrue(result.Items[2].Message.Contains("Unknown"), "invalid platform message names the platform");
                AssertEquals("Invalid", result.Items[3].Status.ToString(), "empty prompt is invalid");

                var duplicate = result.Items[4];
                AssertTrue(duplicate.Platform == gemini, "platform matched by id");
                AssertEquals("cat_5", duplicate.FileName, "duplicate file name gets the row number");

                // Without header the order is prompt, filename, platform
                var noHeader = BatchImageService.BuildItems(BatchImageService.ParseCsv("a cat,c1,Gemini\nonly prompt"), platforms, chatgpt);
                AssertEquals("2", noHeader.Items.Count.ToString(), "no header keeps the first row as data");
                AssertEquals("a cat", noHeader.Items[0].Prompt, "no header: first column is the prompt");
                AssertEquals("c1", noHeader.Items[0].FileName, "no header: second column is the file name");
                AssertTrue(noHeader.Items[0].Platform == gemini, "no header: third column is the platform");
                AssertTrue(noHeader.Items[1].Platform == chatgpt, "missing platform cell uses the default platform");

                var noPromptColumn = BatchImageService.BuildItems(BatchImageService.ParseCsv("filename,platform\na,b"), platforms, chatgpt);
                AssertTrue(!string.IsNullOrEmpty(noPromptColumn.Error), "header without a prompt column is rejected");

                var empty = BatchImageService.BuildItems(BatchImageService.ParseCsv("\r\n\r\n"), platforms, chatgpt);
                AssertTrue(!string.IsNullOrEmpty(empty.Error), "csv without data rows is rejected");

                var noPlatforms = BatchImageService.BuildItems(BatchImageService.ParseCsv("x"), new List<AiPlatform>(), null);
                AssertEquals("Invalid", noPlatforms.Items[0].Status.ToString(), "row without any platform is invalid");

                var tsv = BatchImageService.ParseCsv("提示词\t文件名\nx\ty");
                AssertEquals("y", tsv[1][1], "tab separated when the first line has no comma");
            }
            finally
            {
                LanguageManager.Instance.CurrentLanguage = originalLang;
            }
        }

        private static void TestBatchImageCsvEncoding()
        {
            string text = "提示词,文件名\n一只猫,cat";
            byte[] utf8Bom = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray();
            byte[] utf16Bom = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(text)).ToArray();

            AssertEquals(text, BatchImageService.DecodeText(utf8Bom), "UTF-8 with BOM");
            AssertEquals(text, BatchImageService.DecodeText(new UTF8Encoding(false).GetBytes(text)), "UTF-8 without BOM");
            AssertEquals(text, BatchImageService.DecodeText(Encoding.GetEncoding(936).GetBytes(text)), "GBK saved by Chinese Excel");
            AssertEquals(text, BatchImageService.DecodeText(utf16Bom), "UTF-16 LE with BOM");
            AssertEquals("", BatchImageService.DecodeText(new byte[0]), "empty file");
        }

        private static void TestBatchImagePromptMerge()
        {
            AssertEquals("cat", BatchImageService.MergePrompt("", "cat"), "empty action prompt sends the csv prompt");
            AssertEquals("cat", BatchImageService.MergePrompt(null, "cat"), "null action prompt sends the csv prompt");
            AssertEquals("画：cat！", BatchImageService.MergePrompt("画：{content}！", "cat"), "{content} is replaced");
            AssertEquals("请生成图片\n\ncat", BatchImageService.MergePrompt("请生成图片\n", "cat"), "without {content} the csv prompt is appended");

            var zhDefault = ActionItem.CreateBatchImageAction(false, 1);
            AssertEquals("请根据以下描述生成一张图片：\n\ncat", BatchImageService.MergePrompt(zhDefault.Prompt, "cat"), "default zh prompt asks for an image");
            var enDefault = ActionItem.CreateBatchImageAction(true, 1);
            AssertEquals("Generate an image based on the following description:\n\ncat", BatchImageService.MergePrompt(enDefault.Prompt, "cat"), "default en prompt asks for an image");
        }

        private static void TestBatchImageFileNames()
        {
            AssertEquals("a_b_c", BatchImageService.SanitizeFileName("a:b*c"), "invalid characters are replaced");
            AssertEquals("cat", BatchImageService.SanitizeFileName("cat.png"), "image extension is stripped");
            AssertEquals("cat", BatchImageService.SanitizeFileName(@"..\..\cat.JPG"), "path traversal is removed");
            AssertEquals("photo", BatchImageService.SanitizeFileName("dir/sub/photo.webp"), "only the file name part is kept");
            AssertEquals("v1.2", BatchImageService.SanitizeFileName("v1.2"), "non image extension is kept");
            AssertEquals("_con", BatchImageService.SanitizeFileName("con"), "reserved device name is prefixed");
            AssertTrue(BatchImageService.SanitizeFileName("..") == null, "dots only is rejected");
            AssertTrue(BatchImageService.SanitizeFileName("   ") == null, "blank is rejected");
            AssertEquals("120", BatchImageService.SanitizeFileName(new string('x', 300)).Length.ToString(), "long names are truncated");

            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AssertEquals("cat", BatchImageService.MakeUniqueFileName("cat", 1, used), "first use keeps the name");
            AssertEquals("CAT_2", BatchImageService.MakeUniqueFileName("CAT", 2, used), "duplicates are case insensitive");
            used.Add("cat_3");
            AssertEquals("cat_3_2", BatchImageService.MakeUniqueFileName("cat", 3, used), "suffix collision gets a counter");
        }

        private static readonly byte[] PngBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
        private static readonly byte[] JpegBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0 };

        private static void TestBatchImageExtensionDetection()
        {
            byte[] webp = Encoding.ASCII.GetBytes("RIFF\0\0\0\0WEBPVP8 ");
            byte[] gif = Encoding.ASCII.GetBytes("GIF89a\0\0");
            byte[] unknown = { 1, 2, 3, 4, 5, 6 };
            byte[] html = Encoding.ASCII.GetBytes("<html>error</html>");

            AssertTrue(BatchImageService.TryGetImageExtension(PngBytes, null, out string ext) && ext == ".png", "png by magic bytes");
            AssertTrue(BatchImageService.TryGetImageExtension(JpegBytes, "image/png", out ext) && ext == ".jpg", "magic bytes win over mime");
            AssertTrue(BatchImageService.TryGetImageExtension(webp, null, out ext) && ext == ".webp", "webp by magic bytes");
            AssertTrue(BatchImageService.TryGetImageExtension(gif, null, out ext) && ext == ".gif", "gif by magic bytes");
            AssertTrue(BatchImageService.TryGetImageExtension(unknown, "image/jpeg; charset=binary", out ext) && ext == ".jpg", "falls back to mime");
            AssertFalse(BatchImageService.TryGetImageExtension(html, "text/html", out ext), "html error page is not an image");
            AssertFalse(BatchImageService.TryGetImageExtension(null, "image/png", out ext), "no data is not an image");
        }

        private static void TestBatchImageSaveAndReport()
        {
            string root = Path.Combine(Path.GetTempPath(), "AIHelperTests_" + Guid.NewGuid().ToString("N"));
            try
            {
                var now = new DateTime(2026, 10, 1, 14, 30, 0);
                string dir = BatchImageService.CreateOutputDirectory(root, now);
                AssertEquals("20261001_143000", Path.GetFileName(dir), "output folder is named by time");
                string dir2 = BatchImageService.CreateOutputDirectory(root, now);
                AssertEquals("20261001_143000_2", Path.GetFileName(dir2), "same second gets a suffix");

                var images = new List<ExtractedImage>
                {
                    new ExtractedImage { Data = PngBytes, Extension = ".png" },
                    new ExtractedImage { Data = JpegBytes, Extension = ".jpg" },
                    new ExtractedImage { Data = PngBytes, Extension = ".png" }
                };
                var saved = BatchImageService.SaveImages(dir, "备选", "cat", images);
                AssertEquals("3", saved.Count.ToString(), "all images saved");
                AssertEquals("cat.png", saved[0], "first image goes to the output folder");
                AssertEquals(Path.Combine("备选", "cat_2.jpg"), saved[1], "second image goes to alternates");
                AssertEquals(Path.Combine("备选", "cat_3.png"), saved[2], "third image goes to alternates");
                AssertTrue(File.Exists(Path.Combine(dir, "cat.png")), "main image exists");
                AssertTrue(File.Exists(Path.Combine(dir, "备选", "cat_2.jpg")), "alternate image exists");

                var item = new BatchImageItem
                {
                    Index = 1,
                    Prompt = "a, \"b\"",
                    FileName = "cat",
                    Status = BatchImageItemStatus.Success,
                    SavedFiles = saved,
                    ElapsedSeconds = 12.34
                };
                string reportPath = Path.Combine(dir, BatchImageService.ReportFileName);
                BatchImageService.WriteReport(reportPath, new[] { item });

                byte[] reportBytes = File.ReadAllBytes(reportPath);
                AssertTrue(reportBytes.Length > 3 && reportBytes[0] == 0xEF && reportBytes[1] == 0xBB && reportBytes[2] == 0xBF, "report has a UTF-8 BOM for Excel");
                string report = File.ReadAllText(reportPath, Encoding.UTF8);
                AssertTrue(report.Contains("\"a, \"\"b\"\"\""), "report escapes commas and quotes");
                AssertTrue(report.Contains("12.3"), "report contains elapsed seconds");

                var parsedBack = BatchImageService.ParseCsv(report.TrimStart('\uFEFF'));
                AssertEquals("2", parsedBack.Count(r => r.Count > 1).ToString(), "report parses back as header plus one row");
                AssertEquals("a, \"b\"", parsedBack[1][1], "report round trips the prompt");
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }

        private static void TestBatchImageSystemActionAndSettings()
        {
            var defaults = AppSettings.CreateDefault();
            AssertEquals("1", defaults.Actions.Count(a => a.IsBatchImage).ToString(), "default settings contain one batch image action");
            var batch = defaults.Actions.First(a => a.IsBatchImage);
            AssertTrue(batch.IsBuiltIn, "batch action is built in");
            AssertEquals(ActionTypes.BatchImage, batch.ActionType, "batch action type");
            AssertTrue(batch.Prompt.Contains("{content}"), "batch prompt has the content placeholder");
            AssertEquals("", batch.HotkeyKey, "batch action has no default hotkey");

            // Legacy config without the batch action gets it appended, exactly once
            var legacy = Newtonsoft.Json.JsonConvert.DeserializeObject<AppSettings>(
                "{\"Language\":\"en\",\"Actions\":[{\"Name\":\"Translate\",\"Prompt\":\"{content}\",\"SortOrder\":4}]}");
            AssertTrue(legacy.EnsureSystemActions(), "missing batch action is added");
            AssertEquals("2", legacy.Actions.Count.ToString(), "existing actions are kept");
            var added = legacy.Actions.Single(a => a.IsBatchImage);
            AssertEquals("Batch Image", added.Name, "added action follows the settings language");
            AssertEquals("5", added.SortOrder.ToString(), "added action goes last");
            AssertFalse(legacy.EnsureSystemActions(), "second call changes nothing");

            legacy.Actions.Add(ActionItem.CreateBatchImageAction(false, 9));
            AssertTrue(legacy.EnsureSystemActions(), "duplicate batch actions are removed");
            AssertEquals("1", legacy.Actions.Count(a => a.IsBatchImage).ToString(), "only one batch action remains");
            AssertTrue(legacy.Actions.Single(a => a.IsBatchImage) == added, "the first batch action is kept");

            var nullActions = new AppSettings { Actions = null };
            AssertTrue(nullActions.EnsureSystemActions(), "null action list is created");
            AssertEquals("1", nullActions.Actions[0].SortOrder.ToString(), "first action sort order");

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(batch);
            AssertTrue(json.Contains("\"ActionType\":\"BatchImage\""), "action type is persisted");
            AssertFalse(json.Contains("IsBatchImage"), "computed flag is not persisted");
            var roundTrip = Newtonsoft.Json.JsonConvert.DeserializeObject<ActionItem>(json);
            AssertTrue(roundTrip.IsBatchImage, "batch type survives a round trip");
            AssertFalse(new ActionItem().IsBatchImage, "normal actions are not batch actions");

            // Batch settings defaults and clamping
            AssertEquals("", defaults.BatchImageSavePath, "save path defaults to empty");
            AssertEquals("300", defaults.BatchImageTimeoutSeconds.ToString(), "default timeout");
            AssertEquals("1", defaults.BatchImageRetryCount.ToString(), "default retry");
            AssertEquals("3", defaults.BatchImageMinIntervalSeconds.ToString(), "default min interval");
            AssertEquals("8", defaults.BatchImageMaxIntervalSeconds.ToString(), "default max interval");
            AssertTrue(defaults.GetBatchImageSaveRoot().EndsWith(Path.Combine("AIHelper", "BatchImages")), "default save root under Pictures");
            defaults.BatchImageSavePath = @"  D:\Images  ";
            AssertEquals(@"D:\Images", defaults.GetBatchImageSaveRoot(), "custom save root is trimmed");

            var clamped = BatchImageOptions.FromSettings(new AppSettings
            {
                BatchImageTimeoutSeconds = 5,
                BatchImageRetryCount = 99,
                BatchImageMinIntervalSeconds = 10,
                BatchImageMaxIntervalSeconds = 2
            });
            AssertEquals("30", clamped.TimeoutSeconds.ToString(), "timeout clamped to minimum");
            AssertEquals("5", clamped.RetryCount.ToString(), "retry clamped to maximum");
            AssertEquals("10", clamped.MaxIntervalSeconds.ToString(), "max interval never below min");
            AssertTrue(BatchImageOptions.IsValid(300, 1, 3, 8), "default options are valid");
            AssertFalse(BatchImageOptions.IsValid(300, 1, 9, 3), "min interval above max is invalid");
            AssertFalse(BatchImageOptions.IsValid(10, 1, 3, 8), "timeout below 30 is invalid");
            AssertFalse(BatchImageOptions.IsValid(300, 6, 3, 8), "retry above 5 is invalid");
        }

        private static void TestBatchImageLanguageKeys()
        {
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            var zh = (Dictionary<string, string>)typeof(LanguageManager).GetField("_zhDict", flags).GetValue(null);
            var en = (Dictionary<string, string>)typeof(LanguageManager).GetField("_enDict", flags).GetValue(null);

            Func<string, bool> isBatchKey = k => k.StartsWith("Batch_") || k.StartsWith("Settings_Batch_") ||
                                                 k == "Settings_Tab_BatchImage" || k == "Settings_Action_CannotDeleteBatch" ||
                                                 k == "ActionEdit_BatchPromptHint";
            var zhKeys = zh.Keys.Where(isBatchKey).ToList();
            AssertTrue(zhKeys.Count > 50, "batch keys exist");
            foreach (var key in zhKeys)
            {
                AssertTrue(en.ContainsKey(key), "English dictionary is missing " + key);
            }
            foreach (var key in en.Keys.Where(isBatchKey))
            {
                AssertTrue(zh.ContainsKey(key), "Chinese dictionary is missing " + key);
            }
            foreach (BatchImageItemStatus status in Enum.GetValues(typeof(BatchImageItemStatus)))
            {
                AssertTrue(zh.ContainsKey("Batch_ItemStatus_" + status) && en.ContainsKey("Batch_ItemStatus_" + status), "status text for " + status);
            }
            AssertEquals("8", zh["Batch_Report_Header"].Split(',').Length.ToString(), "zh report header has 8 columns");
            AssertEquals("8", en["Batch_Report_Header"].Split(',').Length.ToString(), "en report header has 8 columns");
        }

        private static ImageProbeResult Probe(bool generating, int textLength, int pending = 0, params string[] srcs)
        {
            return new ImageProbeResult
            {
                ok = true,
                generating = generating,
                textLength = textLength,
                pending = pending,
                images = srcs.Select(s => new ImageProbeImage { src = s, w = 1024, h = 1024 }).ToList()
            };
        }

        private static void TestImageGenerationTracker()
        {
            var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // Normal flow: generating, image appears, generation ends, image stays stable
            var tracker = new ImageGenerationTracker(t0);
            AssertEquals("Waiting", tracker.Update(Probe(true, 50), t0.AddSeconds(1)).ToString(), "generating without image waits");
            AssertEquals("Waiting", tracker.Update(Probe(true, 60, 0, "img1"), t0.AddSeconds(5)).ToString(), "image while generating waits");
            AssertEquals("Waiting", tracker.Update(Probe(false, 60, 0, "img1"), t0.AddSeconds(6)).ToString(), "just finished waits for stability");
            AssertEquals("Completed", tracker.Update(Probe(false, 60, 0, "img1"), t0.AddSeconds(8.5)).ToString(), "stable image completes");
            AssertEquals("1", tracker.Images.Count.ToString(), "tracker reports the image");
            AssertTrue(tracker.SeenGenerating, "generating state was seen");

            // Reply finished without image
            var noImage = new ImageGenerationTracker(t0);
            noImage.Update(Probe(true, 50), t0.AddSeconds(1));
            AssertEquals("Waiting", noImage.Update(Probe(false, 100), t0.AddSeconds(2)).ToString(), "text just finished waits");
            AssertEquals("NoImage", noImage.Update(Probe(false, 100), t0.AddSeconds(11)).ToString(), "quiet page without image is no image");

            // Stop button never detected: grace period before declaring no image
            var noSignal = new ImageGenerationTracker(t0);
            AssertEquals("Waiting", noSignal.Update(Probe(false, 10), t0.AddSeconds(5)).ToString(), "early probe waits");
            AssertEquals("Waiting", noSignal.Update(Probe(false, 10), t0.AddSeconds(15)).ToString(), "within grace waits");
            AssertEquals("NoImage", noSignal.Update(Probe(false, 10), t0.AddSeconds(21)).ToString(), "after grace no image");

            // Stop button never detected and the grace is the configured timeout: a quiet page is
            // not "no image" before that (the image may still be rendering)
            var slow = new ImageGenerationTracker(t0) { NoSignalGrace = TimeSpan.FromSeconds(360) };
            slow.Update(Probe(false, 10), t0.AddSeconds(1));
            AssertEquals("Waiting", slow.Update(Probe(false, 10), t0.AddSeconds(30)).ToString(), "quiet page within the timeout keeps waiting");
            AssertEquals("Waiting", slow.Update(Probe(false, 10), t0.AddSeconds(300)).ToString(), "still waiting late in the timeout");
            slow.Update(Probe(false, 10, 0, "img1"), t0.AddSeconds(301));
            AssertEquals("Completed", slow.Update(Probe(false, 10, 0, "img1"), t0.AddSeconds(310)).ToString(), "image arriving late still completes");

            // Stop button never detected but image appears: longer stability required
            var unconfirmed = new ImageGenerationTracker(t0);
            unconfirmed.Update(Probe(false, 10, 0, "img1"), t0.AddSeconds(1));
            AssertEquals("Waiting", unconfirmed.Update(Probe(false, 10, 0, "img1"), t0.AddSeconds(5)).ToString(), "unconfirmed image needs longer stability");
            AssertEquals("Completed", unconfirmed.Update(Probe(false, 10, 0, "img1"), t0.AddSeconds(9.5)).ToString(), "unconfirmed image completes later");

            // Loading images block completion; a changed image restarts the stability window
            var pending = new ImageGenerationTracker(t0);
            pending.Update(Probe(true, 10), t0.AddSeconds(1));
            AssertEquals("Waiting", pending.Update(Probe(false, 10, 1, "img1"), t0.AddSeconds(2)).ToString(), "pending image waits");
            AssertEquals("Waiting", pending.Update(Probe(false, 10, 1, "img1"), t0.AddSeconds(20)).ToString(), "still pending keeps waiting");
            AssertEquals("Waiting", pending.Update(Probe(false, 10, 0, "img1", "img2"), t0.AddSeconds(21)).ToString(), "new image restarts stability");
            AssertEquals("Completed", pending.Update(Probe(false, 10, 0, "img1", "img2"), t0.AddSeconds(24)).ToString(), "both images complete");
            AssertEquals("2", pending.Images.Count.ToString(), "both images reported");

            // Null probe (page switching) never decides anything
            var nullProbe = new ImageGenerationTracker(t0);
            AssertEquals("Waiting", nullProbe.Update(null, t0.AddSeconds(100)).ToString(), "null probe waits");

            // Stop button stuck on "generating": timeout fallback accepts stable images
            var stuck = new ImageGenerationTracker(t0);
            stuck.Update(Probe(true, 10, 0, "img1"), t0.AddSeconds(1));
            AssertEquals("Waiting", stuck.Update(Probe(true, 10, 0, "img1"), t0.AddSeconds(20)).ToString(), "stuck generating waits");
            AssertTrue(stuck.HasStableImages(t0.AddSeconds(20), TimeSpan.FromSeconds(10)), "stable images accepted on timeout");
            AssertFalse(new ImageGenerationTracker(t0).HasStableImages(t0.AddSeconds(20), TimeSpan.FromSeconds(10)), "no images is not stable");
        }

        private static void TestBatchImageRunState()
        {
            using (var run = new BatchImageRunState())
            {
                AssertTrue(run.WaitIfPausedAsync().Wait(1000), "not paused completes immediately");

                run.Pause();
                AssertTrue(run.IsPaused, "paused");
                var waiting = run.WaitIfPausedAsync();
                AssertFalse(waiting.Wait(100), "paused wait blocks");
                run.Resume();
                AssertTrue(waiting.Wait(1000), "resume releases the wait");
                AssertFalse(run.IsPaused, "resumed");

                run.Pause();
                var cancelled = run.WaitIfPausedAsync();
                run.Cancel();
                bool threw = false;
                try
                {
                    cancelled.Wait(1000);
                }
                catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
                {
                    threw = true;
                }
                AssertTrue(threw, "stop releases a paused wait with cancellation");
                AssertTrue(run.IsCancellationRequested, "stop is recorded");

                run.Pause();
                AssertFalse(run.IsPaused, "cannot pause after stop");
            }
        }

        private static void TestBatchImageWindowsLoad()
        {
            // Window icons are application pack URIs: they need a WPF Application, and the
            // icon is linked into this test assembly as a resource (see the csproj)
            if (System.Windows.Application.Current == null)
            {
                new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
            }

            // Parses the XAML at runtime; no settings file is read when no CSV is given
            var batchWindow = new AIHelper.Views.BatchImageWindow(null, "missing-action", null);
            var startButton = (System.Windows.Controls.Button)batchWindow.FindName("btnStart");
            AssertFalse(startButton.IsEnabled, "start is disabled without rows");
            var outputText = (System.Windows.Controls.TextBlock)batchWindow.FindName("tbOutputDir");
            AssertTrue(outputText.Text.Contains(AppSettings.GetDefaultBatchImageSaveRoot()), "output folder preview shows the default root");
            var recordButton = (System.Windows.Controls.Button)batchWindow.FindName("btnPresetRecord");
            AssertFalse(recordButton.IsEnabled, "preset recording needs a platform");
            AssertFalse(((System.Windows.Controls.Button)batchWindow.FindName("btnPresetTest")).IsEnabled, "preset test needs steps");
            var presetInfo = (System.Windows.Controls.TextBlock)batchWindow.FindName("tbPresetInfo");
            AssertFalse(string.IsNullOrEmpty(presetInfo.Text), "preset hint is shown");
            batchWindow.Close();

            var settings = AppSettings.CreateDefault();
            var batchAction = settings.Actions.First(a => a.IsBatchImage);
            var batchEdit = new AIHelper.Views.ActionEditWindow(batchAction, null, settings.Platforms, settings);
            var batchHint = (System.Windows.Controls.TextBlock)batchEdit.FindName("tbPromptHint");
            AssertEquals("Visible", batchHint.Visibility.ToString(), "batch action shows the prompt hint");
            batchEdit.Close();

            var normalEdit = new AIHelper.Views.ActionEditWindow(new ActionItem { Name = "x", Prompt = "{content}" }, null, settings.Platforms, settings);
            var normalHint = (System.Windows.Controls.TextBlock)normalEdit.FindName("tbPromptHint");
            AssertEquals("Collapsed", normalHint.Visibility.ToString(), "normal action hides the prompt hint");
            normalEdit.Close();
        }

        private static PageSetupStep Click(string selector, string text, string state = "")
        {
            return new PageSetupStep { Kind = "click", Selector = selector, Text = text, State = state };
        }

        private static void TestPagePresetSteps()
        {
            var steps = new List<PageSetupStep>();

            // A recorded dropdown choice: trigger (opens), option, then two toggle buttons
            PageSetupService.Append(steps, new[]
            {
                Click("button.model-trigger", "GPT Image 2", "on"),
                Click("li.model-option", "GPT Image 2.5"),
                Click("button.ratio-btn", "16:9", "on"),
                Click("button.ratio-btn", "2", "on")
            });
            AssertEquals("4", steps.Count.ToString(), "distinct clicks are all kept");

            // The same toggle clicked again (on -> off): only the final state is kept
            PageSetupService.Append(steps, new[] { Click("button.ratio-btn", "2", "off") });
            AssertEquals("4", steps.Count.ToString(), "re-clicking a stateful toggle replaces it");
            AssertEquals("off", steps[3].State, "final toggle state kept");

            // Clicks without a known state are never merged: two clicks differ from one
            PageSetupService.Append(steps, new[] { Click("div.x", "Mode"), Click("div.x", "Mode") });
            AssertEquals("6", steps.Count.ToString(), "stateless clicks are not merged");

            // Form controls: only the last value per control survives, moved to the end
            PageSetupService.Append(steps, new[]
            {
                new PageSetupStep { Kind = "select", Selector = "select[name=\"size\"]", Text = "Size", Value = "512", ValueText = "512px" },
                Click("button.y", "Y"),
                new PageSetupStep { Kind = "SELECT", Selector = "select[name=\"size\"]", Text = "Size", Value = "1024", ValueText = "1024px" }
            });
            AssertEquals("8", steps.Count.ToString(), "one select step kept");
            AssertEquals("1024", steps[7].Value, "last select value wins");
            AssertEquals("select", steps[7].Kind, "kind is normalized");

            // Junk is dropped and unknown values are normalized
            PageSetupService.Append(steps, new[] { null, new PageSetupStep { Kind = "click", Selector = " ", Text = "" } });
            AssertEquals("8", steps.Count.ToString(), "empty steps are ignored");
            var odd = new List<PageSetupStep>();
            PageSetupService.Append(odd, new[] { new PageSetupStep { Kind = "hover", Selector = "a", State = "maybe" } });
            AssertEquals("click", odd[0].Kind, "unknown kind becomes click");
            AssertEquals("", odd[0].State, "unknown state becomes empty");

            // Hard cap against runaway recordings
            var many = new List<PageSetupStep>();
            PageSetupService.Append(many, Enumerable.Range(0, 50).Select(i => Click("b" + i, "B" + i)));
            AssertEquals(PageSetupService.MaxSteps.ToString(), many.Count.ToString(), "steps are capped");

            // Display text in both languages
            var lm = LanguageManager.Instance;
            string oldLanguage = lm.CurrentLanguage;
            try
            {
                lm.CurrentLanguage = "zh";
                AssertEquals("点击「16:9」", PageSetupService.Describe(Click("button.ratio-btn", "16:9")), "zh click text");
                AssertEquals("取消勾选「HD」", PageSetupService.Describe(new PageSetupStep { Kind = "check", Text = "HD", Value = "false" }), "zh uncheck text");
                lm.CurrentLanguage = "en";
                AssertEquals("Select \"1024px\" in \"Size\"", PageSetupService.Describe(steps[7]), "en select text");
                AssertEquals("Click \"button.icon\"", PageSetupService.Describe(Click("button.icon", "")), "selector shown when there is no text");
                AssertTrue(PageSetupService.Describe(Click("x", new string('a', 100))).EndsWith("…\""), "long labels are shortened");
            }
            finally
            {
                lm.CurrentLanguage = oldLanguage;
            }

            // Persistence: new platform fields round-trip, and legacy configs get safe defaults
            var platform = new AiPlatform { Name = "P", BatchOpenLargeImage = true, BatchSetupSteps = steps.Take(2).ToList() };
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(platform);
            var back = Newtonsoft.Json.JsonConvert.DeserializeObject<AiPlatform>(json);
            AssertTrue(back.BatchOpenLargeImage, "open large flag survives a round trip");
            AssertEquals("2", back.BatchSetupSteps.Count.ToString(), "steps survive a round trip");
            AssertEquals("GPT Image 2.5", back.BatchSetupSteps[1].Text, "step text survives a round trip");
            AssertTrue(json.Contains("\"Selector\""), "settings keep PascalCase names");

            var legacy = Newtonsoft.Json.JsonConvert.DeserializeObject<AiPlatform>("{\"Name\":\"Old\",\"Url\":\"https://x\"}");
            AssertFalse(legacy.BatchOpenLargeImage, "legacy platform does not open large images");
            AssertTrue(legacy.BatchSetupSteps != null && legacy.BatchSetupSteps.Count == 0, "legacy platform has an empty preset");
            var nulled = Newtonsoft.Json.JsonConvert.DeserializeObject<AiPlatform>("{\"BatchSetupSteps\":null}");
            AssertTrue(nulled.BatchSetupSteps != null, "null preset becomes an empty list");

            // Steps read back from the page script use camelCase keys
            var poll = Newtonsoft.Json.JsonConvert.DeserializeObject<PresetRecordingPoll>(
                "{\"active\":true,\"steps\":[{\"kind\":\"click\",\"selector\":\"li.model-option\",\"tag\":\"li\",\"text\":\"GPT Image 2.5\",\"state\":\"\",\"value\":\"\"}]}");
            AssertTrue(poll.active, "poll active flag");
            AssertEquals("li.model-option", poll.steps[0].Selector, "camelCase selector maps to the model");
        }

        private static void TestBatchImageDataUrl()
        {
            byte[] png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D };
            string url = "data:image/png;base64," + Convert.ToBase64String(png);
            AssertTrue(BatchImageService.TryDecodeDataUrl(url, out var data, out var mime), "png data url decodes");
            AssertEquals("image/png", mime, "data url mime");
            AssertEquals(png.Length.ToString(), data.Length.ToString(), "data url bytes");
            AssertTrue(BatchImageService.TryGetImageExtension(data, mime, out var ext) && ext == ".png", "decoded bytes are a png");

            string escaped = "data:image/png;base64," + Uri.EscapeDataString(Convert.ToBase64String(new byte[] { 0xFB, 0xFF, 0xFE }));
            AssertTrue(BatchImageService.TryDecodeDataUrl(escaped, out var unescaped, out _) && unescaped.Length == 3, "percent-encoded base64 decodes");

            AssertFalse(BatchImageService.TryDecodeDataUrl("https://x/a.png", out _, out _), "http url is not a data url");
            AssertFalse(BatchImageService.TryDecodeDataUrl("data:image/svg+xml,<svg/>", out _, out _), "non-base64 data url is skipped");
            AssertFalse(BatchImageService.TryDecodeDataUrl("data:image/png;base64,***", out _, out _), "corrupt base64 is rejected");
            AssertFalse(BatchImageService.TryDecodeDataUrl(null, out _, out _), "null is not a data url");
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
