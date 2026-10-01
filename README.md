# AIHelper

**English** | [中文](README_ZH.md)

AIHelper – Your Global AI Productivity Engine for Windows.

Stop copying, pasting, and switching tabs. AIHelper seamlessly embeds leading LLMs (ChatGPT, Claude, Gemini, DeepSeek, Qwen, etc.) directly into your workflow. With a selection-triggered floating toolbar, global hotkeys, and smart web injection, instantly translate, explain, summarize, polish, or grammar-check any text. Lightweight, unobtrusive, and always ready — making AI a native part of your desktop experience.

![Windows](https://img.shields.io/badge/Windows-0078D6?style=flat-square&logo=windows&logoColor=white)
![Lightweight](https://img.shields.io/badge/Lightweight-<1MB-brightgreen?style=flat-square)
![Multi-LLM](https://img.shields.io/badge/Multi--LLM-ChatGPT%20%7C%20Claude%20%7C%20Gemini%20%7C%20Other-blueviolet?style=flat-square)
<a href="https://github.com/chgblog/AIHelper/releases">
![GitHub Release](https://img.shields.io/github/v/release/chgblog/AIHelper?style=flat-square&include_prereleases&label=Latest)
</a>

https://github.com/user-attachments/assets/8a797c85-6701-4d8b-87dc-3e03127509cd

---

## 📸 Screenshots

### 1. Main Window (AI Result Interface)
![Main Window](assets/en/ZhuChuangKou.jpg)

### 2. Text Selection AI Toolbar
![Text Selection Toolbar](assets/en/HuaCiGongJuTiao.jpg)

### 3. Selection Settings
![Selection Settings](assets/en/HuaCiSheZhi.jpg)

### 4. Selection App Settings
![Selection App Settings](assets/en/HuaCiYingYongSheZhi.jpg)

### 5. Platform Management
![Platform Management](assets/en/PingTaiGuanLi.jpg)

### 6. Action Management
![Action Management](assets/en/CaoZuoGuanLi.jpg)

### 7. Action Editing
![Action Editing](assets/en/CaoZuoBianJi.jpg)

---

## ✨ Key Features

- **✨ Text Selection AI Assistant Toolbar**
  - Select text in any application using your mouse, and a minimal floating AI toolbar will automatically appear next to your cursor.
  - One-click prompt execution directly from the floating toolbar without pressing keyboard shortcuts.
  - **Intuitive Interactions & Customization**: Right-click anywhere on the toolbar to immediately dismiss it; optional "Copy" button (configurable: disabled, first position, or last position); customizable auto-hide countdown and anti-overlap positioning.
  - **Target Application Scope**: Filter by "All Applications", "Include Applications", or "Exclude Applications" with a visual process picker dialog.

- **🚀 Global Hotkeys & Action Panel**
  - Press the global hotkey (default `Ctrl+Alt+Space`) to bring up the quick action panel.
  - Press the open-main-window hotkey (default `Shift+Space`) to toggle the main window (press to activate/bring to front, press again to hide with cycle toggle support).
  - Select or copy text, then press a hotkey (e.g., `Ctrl+Alt+T`) to send it directly to AI for processing.
  - **🛡️ Real-Time Hotkey Conflict Detection**: Real-time validation when setting hotkeys against panel hotkey, main window hotkey, action shortcuts, or system/third-party occupied hotkeys to prevent failures.
  - Supports **custom action ordering** (Move Up / Move Down) and standalone dialog editing in settings.

- **🌐 Multi-AI Platform Integration, Per-Platform Proxy & Action Routing**
  - Built-in preset support for 7 major AI platforms: **DeepSeek**, **Claude**, **Gemini**, **ChatGPT**, **Qwen**, **Zhipu**, and **Kimi**.
  - **🔀 Action-Specific Platform Routing**: Each prompt action can be independently bound to a specific AI platform (e.g., DeepSeek for Translation, Claude for Polishing, or use Default active platform) for specialized multi-model workflows.
  - **🌐 Per-Platform Independent Proxy**: Each platform can independently toggle whether to route through the configured network proxy or connect directly.
  - **🎯 Visual DOM Element Picker**: Customize CSS selectors for "New Chat", "Input Box", and "Submit Button". Built-in WebView2 element picker allows clicking elements directly on live webpages to inspect and retrieve CSS selectors effortlessly.
  - **🔄 Automatic New Chat**: Supports configuring a "New Chat" selector to automatically click and initiate a fresh conversation before sending prompts.

- **⚡ Intelligent DOM Script Injection & Auto-Submit Control**
  - Built-in `injector.js` script that automatically identifies and locates text input fields on major AI platforms.
  - Robust retry, load-waiting, and injection mechanisms across platform switches, ensuring stable prompt injection and configurable **Auto Submit** toggle (auto send vs. manual confirmation).

- **🌐 Browser Mode & Human-Like Browsing Simulation**
  - **One-Click Mode Switching**: A browser icon next to the AI platform switcher switches to "Browser Mode"; an AI icon on the left of Browser Mode switches seamlessly back to AI mode without losing conversation state.
  - **Address Bar & Web Navigation**: Enter any URL to visit websites with auto-protocol completion and last-visited URL memory.
  - **Human-Like Randomized Scrolling**: A toggle next to the address bar enables "Simulate Human Browsing", which automatically scrolls down pages using randomized time intervals (e.g. 0.5s - 3s) and smooth scroll step distances (e.g. 100px - 300px).
  - **Dedicated Simulation Settings Tab**: Settings window includes a dedicated "Simulation" tab for customizing scroll durations, pixel step ranges, and a proxy toggle (defaults to system proxy, or routes via General Settings proxy when enabled).
  - **🤖 Auto Visit & Link Traversing (Auto Visit)**:
    - **Auto Visit Management**: A dedicated management dialog (similar to Platform Management) under the Simulation tab in Settings allows adding and maintaining auto-visit rules.
    - **Rule Configuration**: Configure target URL, link regex matching rule, max visit limit per round, next page CSS selector (with visual element picker support), and stop-refresh interval range (min-max minutes, default 30-60).
    - **Viewport Extraction & 24h De-duplication**: Matches visible links within the current screen viewport and filters out links visited in the last 24 hours.
    - **Child Page Browsing & Position Restoring**: Clicks matching links, smoothly scrolls child pages to the bottom using human-like parameters, and returns to the main page with previous scroll position preserved.
    - **Screen-by-Screen Traversal & Pagination**: Scrolls down to the next screen after visiting all links on the current screen; clicks the next-page locator when reaching the page bottom to continue on subsequent pages.
    - **Randomized Interval Reload & Revisit**: Automatically counts down a randomized wait interval (e.g. 30-60 minutes) when reaching the link limit or page bottom, then reloads the page and restarts from the beginning.


- **🖼️ Batch Image Generation**
  - **CSV driven**: Prepare a CSV with "prompt, filename, platform" columns (header optional; with a header the column order is free; an empty platform falls back to the action's platform or the active one). UTF-8 and GBK (Chinese Excel default) encodings are detected automatically.
  - **Built-in "Batch Image" action**: Always present in action management and cannot be deleted. Trigger it from the quick action bar, a hotkey, the selection toolbar, the action panel or the file context menu, then pick a CSV (right-clicking a `.csv` file uses that file directly). The batch window previews rows and offers start, pause, stop and retry-failed.
  - **Prompt merging**: `{content}` in the action prompt is replaced with the CSV prompt; without `{content}` the CSV prompt is appended; the action prompt may be empty. The default asks the platform to generate an image so it does not answer with text only.
  - **One new chat per row**: start a new chat → replay the page preset → inject and submit → wait for the image (stop button gone and new images stable) → extract it (read inside the page, cross-origin images recovered from network responses) → save it under the given name, with the extension taken from the actual image format.
  - **Page preset (choose the model / image options first)**: For sites where a model, aspect ratio, image count and so on must be chosen first, pick the platform under "Page preset" at the top of the batch window (opened by the Batch Image action, or from Settings → Batch Image → "Save settings and open the Batch Image window", which needs no CSV), click Record, choose the options on the page in the main window as you normally would (do not type a prompt or send), then click Finish; the preset is saved per platform. It is replayed after the new chat of every row; buttons already in their recorded state (an aspect ratio that is already selected, say) are not clicked again, so toggles are never switched back off. Test replays it on the page so you can check the result, and single steps or the whole preset can be removed.
  - **Open the full-size image before saving**: For sites that show only a thumbnail in the chat and load the original when it is clicked, tick "Open the full-size image before saving" under "Page preset". After generation each image is clicked, the largest image in the viewer is saved, and the viewer is closed again (Esc, then a close button, then the backdrop). The thumbnail is saved when no full-size view opens.
  - **Output layout**: Each batch creates a timestamped folder under the save location from "Settings → Batch Image" (default `Pictures\AIHelper\BatchImages`). When a prompt yields several images the first goes into that folder and the rest into an "Alternates" subfolder; a `_report.csv` run report and a `_source.csv` copy are written alongside.
  - **Robust runs**: Rows are grouped by platform to minimize proxy switches; timeout, retries and a random interval between images are configurable; a platform is skipped after 3 consecutive failures or when it is not logged in.

- **🖥️ Main Window Quick Actions & Modern UX**
  - **Status Bar Quick Action Bar**: Quick action buttons and a "More ▾" menu embedded right in the bottom status bar of the main window for one-click prompt execution, with a "New Chat" toggle on the left to control whether to trigger a new session before injecting prompts.
  - **Window Maximize & Restore**: Maximize/restore window controls and double-click title bar support.

- **📦 Configuration Management & Backup / Restore**
  - One-click export and backup of all platform, action, hotkey, and preference configurations to a JSON file, with easy restore and quick access to the config directory.

- **🔔 Automatic Update Check & Non-Intrusive Notifications**
  - Background auto-check for new GitHub releases; subtle "Update" badge and tip displayed next to the Settings button in the main window title bar without disruptive modal popups.

- **🌐 Multi-Language Interface (I18n)**
  - Built-in Language Manager supporting dynamic switching between **Simplified Chinese** and **English**, auto-adapting to local time zone.

- **💻 Modern & Lightweight UI with System Tray**
  - Built with WPF and Microsoft WebView2 for a smooth web browsing and interaction experience.
  - System tray context menu provides quick controls ("Open Settings", "Enable/Disable Selection Helper").
  - Local configuration storage (`%APPDATA%\AIHelper\settings.json`) to protect privacy and avoid repeated logins.

---

## ⌨️ Default Hotkeys

| Hotkey | Action | Prompt Description |
| :--- | :--- | :--- |
| `Shift + Space` | Show/Hide Main Window | Cycle toggles the main window visibility |
| `Ctrl + Alt + Space` | Show/Hide Action Panel | Opens the quick action list panel |
| `Ctrl + Alt + T` | Translate | Translates selected text to Chinese |
| `Ctrl + Alt + E` | Explain | Provides a detailed explanation of the selected text |
| `Ctrl + Alt + S` | Summarize | Extracts a core summary from the selected text |
| `Ctrl + Alt + R` | Polish | Polishes the selected text for fluency and professionalism |
| `Ctrl + Alt + G` | Grammar Check | Checks for grammatical errors and suggests corrections |
| `Ctrl + Alt + O` | Summary (Summarize) | Summarizes the selected text |

> *Note: All hotkeys can be reconfigured in the application's Settings window with real-time conflict detection.*

---

## 🛠️ Tech Stack & Dependencies

- **Runtime / Framework**: .NET Framework 4.8 / WPF (Windows Presentation Foundation)
- **Web Browser Engine**: [Microsoft.Web.WebView2](https://www.nuget.org/packages/Microsoft.Web.WebView2)
- **JSON Serialization**: [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json)
- **Single-File Packaging**: [Costura.Fody](https://www.nuget.org/packages/Costura.Fody)
- **Script Injection Bridge**: Vanilla JavaScript (`injector.js` / `element-picker.js`)

---

## 📁 Project Structure

```text
AIHelper/
├── AIHelper.sln                # Visual Studio solution file
└── AIHelper/
    ├── AIHelper.csproj         # Project file
    ├── App.xaml / App.xaml.cs  # Application entry point & global resources (with tray menu)
    ├── Assets/
    │   ├── injector.js         # JS script for web page automation injection
    │   └── element-picker.js   # JS script for visual DOM element picking
    ├── Converters/             # XAML data converters
    │   ├── BoolToVisibilityConverter.cs
    │   └── PlatformIdToNameConverter.cs
    ├── Models/
    │   ├── ActionItem.cs       # Action item data model (with platform binding/ordering)
    │   ├── AiPlatform.cs       # AI platform data model (with independent proxy/selectors)
    │   ├── AppItem.cs          # Application data model (for selection app scope filtering)
    │   ├── BatchImageItem.cs   # Batch image row model (status/result)
    │   ├── PageSetupStep.cs    # One page preset step (a recorded click/form change)
    │   └── AppSettings.cs      # App settings & defaults (proxy, language, text selection)
    ├── Services/
    │   ├── AppInfoService.cs   # App information & version service
    │   ├── AutoStartService.cs # Auto-start service
    │   ├── BatchImageService.cs # Batch image CSV parsing, file naming, saving & report
    │   ├── ClipboardService.cs # Clipboard access & key simulation service
    │   ├── HotkeyService.cs    # Global hotkey listener & conflict detection (Win32 API)
    │   ├── ImageGenerationTracker.cs # Decides when image generation is done
    │   ├── LanguageManager.cs  # Multi-language / I18n dynamic switching service
    │   ├── Logger.cs           # Logging service
    │   ├── NetworkImageCapture.cs # Caches image responses during a batch (cross-origin fallback)
    │   ├── PageSetupService.cs # Page preset step merging & display text
    │   ├── PageInjector.cs     # Web JS script injection & execution service
    │   ├── SettingsService.cs  # Local JSON config loading, persistence & backup/restore
    │   ├── TextSelectionService.cs # Text selection & floating toolbar listener service
    │   └── UpdateCheckService.cs   # GitHub Release update detection service
    └── Views/
        ├── ActionEditWindow.xaml   # Standalone action editing dialog (with platform binding)
        ├── ActionPanelControl.xaml # Quick action floating panel view
        ├── AppSelectionWindow.xaml # Visual application process picker dialog
        ├── BatchImageWindow.xaml   # Batch image window (preview/start/pause/stop/retry)
        ├── ElementPickerWindow.xaml# Visual DOM element picker window
        ├── MainWindow.xaml         # Main window (with WebView2 control & status action bar)
        ├── MainWindow.BatchImage.cs # Batch image run loop (new chat/inject/wait/extract/save)
        ├── MainWindow.BatchPreset.cs # Page preset recording & test run
        ├── PlatformEditWindow.xaml # Platform editing & selector configuration window (with proxy)
        ├── SelectionToolbarWindow.xaml # Text selection AI floating toolbar window
        └── SettingsWindow.xaml     # Settings window (platform/action/selection/proxy/language)
```

---

## 🚀 Build & Run

### 1. Prerequisites

- **Operating System**: Windows 10 / Windows 11
- **Development Tools**: Visual Studio 2019 / 2022 (with **.NET Desktop Development** workload installed) or .NET SDK
- **SDK Requirement**: .NET Framework 4.8 Developer Pack
- **Runtime Requirement**: [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (built-in on Win11, usually auto-installed with Edge on Win10)

### 2. Build Steps

1. Clone or download the project source code:
   ```bash
   git clone https://github.com/chgblog/AIHelper.git
   cd AIHelper
   ```
2. Open `AIHelper.sln` in Visual Studio.
3. Select `Any CPU` or `x64` platform and choose `Debug` or `Release` configuration.
4. Press `F5` to run or `Ctrl+Shift+B` to build the solution.

### 3. Single-File Packaging (Single EXE)

This project integrates **Costura.Fody** to merge all dependency DLLs and the embedded JavaScript resources (`injector.js` and `element-picker.js`) into a single standalone `.exe` file:

- **Build via command line**:
   ```bash
   dotnet build AIHelper/AIHelper.csproj -c Release
   ```
- **Output**:
   The packaged single file is located at `AIHelper/bin/Release/net48/AIHelper.exe`. You can copy `AIHelper.exe` (along with `AIHelper.exe.config`) to any location and run it independently — no additional DLLs or `Assets/` directory required.

---

## Verify GitHub Actions

Verify that the downloaded file is automatically compiled and packaged from source code by GitHub Actions.

- **Verify the file hash (Windows PowerShell):**
```
Get-FileHash .\AIHelper.zip -Algorithm SHA256
```

- **Verify the official GitHub build attestation (using GitHub CLI):**
```
gh attestation verify AIHelper.zip --repo chgblog/AIHelper
```

---

## 🔗 Community

- **[linux.do](https://linux.do)** - A community you always feel like visiting.

## 📄 License

Released under the [GNU General Public License v3.0](LICENSE).
