# Hotkey Clipboard Paste Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let hotkey-triggered AI actions handle text clipboard content with the existing injection path, and handle image/file clipboard content by focusing the input, pasting with `Ctrl+V`, waiting for upload readiness, then submitting.

**Architecture:** Keep the decision at the hotkey action entry point in `MainWindow.xaml.cs`. Extract clipboard classification and paste-wait logic into focused helpers so the UI flow stays readable, and extend the WebView injector only where DOM inspection is needed for paste/upload readiness.

**Tech Stack:** WPF on .NET Framework 4.8, WebView2, Win32 `SendInput`, existing `PageInjector` JS bridge, lightweight console-based test harness if no test runner is already present.

---

## Chunk 1: Clipboard Classification and Decision Flow

**Files:**
- Modify: `AIHelper/Services/ClipboardService.cs`
- Modify: `AIHelper/Views/MainWindow.xaml.cs`
- Test: `AIHelper.Tests/Program.cs`

- [ ] **Step 1: Write the failing test**

Cover clipboard classification for text, image, and file-drop cases with a pure helper surface.

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet run --project AIHelper.Tests/AIHelper.Tests.csproj`

- [ ] **Step 3: Write minimal implementation**

Add a clipboard snapshot/classifier and route hotkey action execution based on that snapshot.

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet run --project AIHelper.Tests/AIHelper.Tests.csproj`

- [ ] **Step 5: Commit**

```bash
git add AIHelper/Services/ClipboardService.cs AIHelper/Views/MainWindow.xaml.cs AIHelper.Tests
git commit -m "feat: support clipboard paste in hotkey actions"
```

## Chunk 2: Paste, Wait, and Submit Flow

**Files:**
- Modify: `AIHelper/Services/PageInjector.cs`
- Modify: `AIHelper/Assets/injector.js`
- Modify: `AIHelper/Services/LanguageManager.cs`
- Modify: `AIHelper/Views/MainWindow.xaml.cs`

- [ ] **Step 1: Write the failing test**

Add tests for the new upload-wait decision helpers and the prompt-building path that keeps text injection unchanged.

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet run --project AIHelper.Tests/AIHelper.Tests.csproj`

- [ ] **Step 3: Write minimal implementation**

Add JS helpers for paste-watch and submit-button state, then host-side polling and `Ctrl+V` handling with a 5-minute ceiling.

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet run --project AIHelper.Tests/AIHelper.Tests.csproj`

- [ ] **Step 5: Commit**

```bash
git add AIHelper/Services/PageInjector.cs AIHelper/Assets/injector.js AIHelper/Services/LanguageManager.cs AIHelper/Views/MainWindow.xaml.cs
git commit -m "feat: wait for clipboard uploads before sending"
```

