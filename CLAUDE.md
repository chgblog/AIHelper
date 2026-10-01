# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build

Single project, no test suite. .NET Framework 4.8 WPF app (`net48`, `PlatformTarget` x64).

```bash
dotnet build AIHelper/AIHelper.csproj -c Release
```

CI (`.github/workflows/dotnet-desktop.yml`, on push to `main`) runs `nuget restore AIHelper.sln` then the same `dotnet build`, and uploads `AIHelper/bin/Release/net48/AIHelper.exe`.

Costura.Fody merges all dependency DLLs (including the native `WebView2Loader`, configured in `AIHelper/FodyWeavers.xml`) into that single EXE. `Assets/injector.js` and `Assets/element-picker.js` are `EmbeddedResource`, so the shipped EXE needs no `Assets/` folder next to it.

Running requires the Microsoft Edge WebView2 Runtime on the machine.

## Runtime state (outside the repo)

- `%APPDATA%\AIHelper\settings.json` — all persisted config (platforms, actions, hotkeys, proxy, language).
- `%APPDATA%\AIHelper\logs\app.log` and `crash.log` — `Logger` output.
- `%LOCALAPPDATA%\AIHelper\WebView2Data_Proxy` / `_Direct` — WebView2 user-data folders (see proxy note below).

Deleting `settings.json` resets to `AppSettings.CreateDefault()` and re-triggers the first-run flow (`IsFirstRun` → opens Settings on the Platforms tab).

## Architecture

The app is a tray-resident WPF host around a single WebView2 that drives real AI chat websites via DOM injection — there are no LLM API calls anywhere.

**End-to-end flow of one action:**

1. A trigger fires: a global hotkey (`HotkeyService`, `RegisterHotKey` + `WndProc` hook), the action panel (`Ctrl+Alt+Space`), or the floating selection toolbar (`TextSelectionService` → `SelectionToolbarWindow`).
2. `MainWindow` builds the prompt by substituting the captured text into the action's template at the literal `{content}` placeholder.
3. `GetPlatformForAction` picks the target platform — the action's own `PlatformId` if set, else the active platform.
4. `EnsurePlatformAndExecuteAsync` makes the WebView2 point at that platform (navigating, or fully reinitializing it for a proxy change), then calls `PageInjector`.
5. `PageInjector` reads `injector.js` from embedded resources, JSON-serializes the prompt and the platform's three CSS selectors into a call to `window.AiHelperInjector.inject(...)`, and runs it with `ExecuteScriptAsync`. The script returns `{success, reason}`; `reason` codes (`NOT_LOGGED_IN`, `INPUT_NOT_FOUND`, …) are mapped to localized status strings.

**`injector.js` two-layer selector strategy** — the user-configured selector is tried first; if it misses, the script falls back to hardcoded per-platform heuristics (detected from `window.location.href`: claude / gemini / deepseek / generic) and then to generic DOM heuristics (rightmost small SVG-bearing element near the input, with file-upload controls filtered out). Text injection differs by element type: `textarea` goes through the native value setter plus a React `_valueTracker` reset and synthetic `input`/`change`/`InputEvent`; contenteditable goes through `execCommand('insertText')`. Keep both paths working when editing.

**Selector configuration loop** — `PlatformEditWindow` launches `ElementPickerWindow`, which loads the platform URL in its own WebView2, injects `element-picker.js`, and receives the clicked element's generated CSS selector back over `chrome.webview.postMessage` → `WebMessageReceived`. Platforms store three selectors: `NewChatSelector`, `InputSelector`, `SubmitSelector`.

**Proxy is per-platform, and costs a WebView2 rebuild.** The proxy is applied as a Chromium `--proxy-server` browser argument at `CoreWebView2Environment` creation time, so it cannot be changed on a live control. `AiPlatform.UseProxy` combined with a non-empty global `AppSettings.ProxyServer` decides. Switching between a proxied and a direct platform runs `ReinitializeWebViewAsync`, which destroys and recreates the `WebView2` control (with retries for transient COM failures) and uses a *separate* user-data folder per mode — WebView2 forbids sharing one folder across different environment options. `_currentWebViewUsesProxy` tracks which mode is live; any code path that changes the platform must compare against it.

**Batch image** — a built-in action marked by `ActionItem.ActionType == "BatchImage"` (not shown/editable in settings, cannot be deleted; `AppSettings.EnsureSystemActions()` re-adds it on every `SettingsService.Load()`). Every trigger path checks `IsBatchImage` first and calls `MainWindow.OpenBatchImage`, which opens `BatchImageWindow` (CSV preview/controls); the run loop lives in `MainWindow.BatchImage.cs` because it drives the same `webView`. While it runs (`IsBatchImageRunning`), every other injection entry point, platform switching and post-settings navigation must bail out via `RejectWhileBatchRunning()` / the `IsBatchImageRunning` guards. Generation completion is decided host-side (`ImageGenerationTracker` fed by the synchronous `probeImages` in `injector.js` once per second), not by an in-page wait loop, because page timers are throttled when the window is hidden. Images are read in-page (`fetchImage`), falling back to `NetworkImageCapture` (cached `WebResourceResponseReceived` image bodies) for cross-origin CDNs. Pure logic (CSV, naming, report) is in `BatchImageService` and covered by `AIHelper.Tests`.

**Batch page preset & full-size images** — per platform, `AiPlatform.BatchSetupSteps` holds recorded clicks/form changes (`PageSetupStep`) that `RunBatchAttemptAsync` replays via `applySetupSteps` right after each new chat (to pick the model, ratio, etc.). Recording runs in the main `webView` (where the user is logged in), started from `BatchImageWindow` through `MainWindow.BatchPreset.cs`: `startRecording` in `injector.js` captures only trusted clicks/`change` events (prompt input, send, new chat and file pickers are ignored) and queues them on `window.__aiHelperRecorder`; the host drains them every 500 ms and re-installs the recorder after a reload. Each click step stores a selector that may match several elements plus the element text that disambiguates, and the element's on/off state after the click — replay skips elements already in that state so toggles are not flipped back. Recording and test runs set `_presetBusy`, which `IsBatchImageRunning` includes, so every other WebView entry point bails out as during a run. `AiPlatform.BatchOpenLargeImage` makes extraction click each image (`openLargeImage`), save the largest new image in the viewer, then `closeImageViewer` (Escape → close button → backdrop, only inside a detected fixed overlay); it falls back to the thumbnail. `data:` images are decoded host-side because page CSPs usually block `fetch` of them.

**Text selection capture** (`TextSelectionService`) — a low-level mouse hook (`WH_MOUSE_LL`) detects drag or multi-click selection, debounces, checks the foreground process against the app-scope allow/deny list (`SelectionAppScopeMode` 0=all / 1=include / 2=exclude), and then tries UI Automation first. The `Ctrl+C` simulation (`SendInput`) is only a fallback and only when `EnableClipboardEnhancement` is on, because it clobbers the clipboard; it saves and restores the original clipboard contents around the copy. Work happens on a dedicated thread, marshaling back via `Dispatcher`.

**Window lifecycle** — `ShutdownMode.OnExplicitShutdown`; the main window is hidden, never closed (`OnClosing` cancels unless `IsExiting`). Single-instance is a named `Mutex`; a second launch broadcasts the registered `AIHelper_ShowFirstInstance` message and exits. `App.MainWindowInstance` recreates the window if it was somehow closed. Startup args `--show` / `--minimized` / `--hide` override `ShowMainWindowOnStartup`.

## Conventions

- **Localization is code-based, not RESX.** `LanguageManager` holds two `Dictionary<string,string>` literals (`_zhDict`, `_enDict`) keyed by strings like `Settings_Tab_General`; missing English keys fall back to Chinese, then to the key itself. Adding UI text means adding the key to **both** dictionaries. XAML binds through the indexer: `{Binding [Key], Source={x:Static services:LanguageManager.Instance}}`; code uses `LanguageManager.Instance["Key"]` or `GetString("Key", args)` for `{0}` formatting. Language changes raise `PropertyChanged(null)` so all bindings refresh live — do not cache localized strings.
- Services are singletons via a static `Instance` property (`SettingsService`, `LanguageManager`, `HotkeyService`, `TextSelectionService`) or static classes (`Logger`, `ClipboardService`, `AutoStartService`, `UpdateCheckService`).
- `SettingsService.Load()` re-reads the JSON file every call and returns a fresh object — it is not cached. Mutate a loaded instance and `Save()` it; after saving, call `MainWindow.RefreshSettings()` (or reopen settings) so hotkeys, the selection hook, and the tray menu re-sync.
- After settings change, `RegisterHotkeys()` unregisters everything and re-registers from scratch; hotkey IDs are mapped to actions in `_hotkeyActionMap`.
- Source files carry a `// Copyright (C) 2026 chgblog` / `// SPDX-License-Identifier: GPL-3.0` header (project is GPL-3.0).
- Comments and user-facing Chinese strings are mixed into the code; match the surrounding file's language rather than normalizing.
- `README.md` and `README_ZH.md` are parallel — feature/changelog edits belong in both.
