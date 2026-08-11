# AIHelper

**English** | [中文](README.md)

**AIHelper** is a lightweight Windows desktop AI assistant tool. It seamlessly integrates web-based AI platforms (such as DeepSeek, ChatGPT, Claude, Gemini, etc.) into your daily workflow. Through text selection AI toolbar, global hotkeys, and intelligent web script injection, it enables quick translation, explanation, summarization, polishing, and grammar checking of clipboard and selected text.

---

## ✨ Key Features

- **✨ Text Selection AI Assistant Toolbar**
  - Select text in any application using your mouse, and a minimal floating AI toolbar will automatically appear next to your cursor.
  - One-click prompt execution directly from the floating toolbar without pressing hotkeys.
  - Easily toggle text selection monitoring on/off via the system tray menu or settings window, with smart position adjustments and animations.

- **🚀 Global Hotkeys & Action Panel**
  - Press the global hotkey (default `Ctrl+Alt+Space`) to bring up the quick action panel.
  - Select or copy text, then press a hotkey (e.g., `Ctrl+Alt+T`) to send it directly to AI for processing.
  - Supports **custom action ordering** and standalone dialog editing in settings.

- **🌐 Multi AI Platform Integration & Precise Element Selector**
  - Built-in preset support for 7 major AI platforms: **DeepSeek**, **Claude**, **Gemini**, **ChatGPT**, **Qwen**, **Zhipu**, and **Kimi**.
  - Easily add, edit, or delete custom AI platforms with single-click radio button activation.
  - **🎯 Visual DOM Element Picker**: Customize CSS selectors for "New Chat", "Input Box", and "Submit Button". Built-in WebView2 element picker allows clicking elements directly on live webpages to inspect and retrieve CSS selectors effortlessly.
  - **🔄 Automatic New Chat**: Supports configuring a "New Chat" selector to automatically click and initiate a fresh conversation before sending prompts.

- **⚡ Intelligent DOM Script Injection & Auto-Submit Control**
  - Built-in `injector.js` script that automatically identifies and locates text input fields on major AI platforms.
  - Auto-fills prompt templates and selected text, with configurable **Auto Submit** toggle (auto send vs. manual confirmation).

- **🛠️ Highly Customizable Prompt Actions**
  - Preset common actions: **Translate**, **Explain**, **Summarize**, **Polish**, **Grammar Check**.
  - Users can freely modify prompt templates, add/edit/delete actions, reorder actions, and assign custom hotkeys.

- **🌐 Multi-Language Interface (I18n)**
  - Built-in Language Manager supporting dynamic switching between **Simplified Chinese** and **English**.

- **🔧 Network Proxy & Update Configuration**
  - Supports custom HTTP / HTTPS / SOCKS network proxy settings for reliable connectivity.
  - Supports configuring project homepage and update check URLs.

- **💻 Modern & Lightweight UI with System Tray**
  - Built with WPF and Microsoft WebView2 for a smooth web browsing and interaction experience.
  - System tray context menu provides quick controls ("Open Settings", "Enable/Disable Selection Helper").
  - Local configuration storage (`%APPDATA%\AIHelper\settings.json`) to protect privacy and avoid repeated logins.

---

## ⌨️ Default Hotkeys

| Hotkey | Action | Prompt Description |
| :--- | :--- | :--- |
| `Ctrl + Alt + Space` | Show/Hide Action Panel | Opens the action list panel |
| `Ctrl + Alt + T` | Translate | Translates selected text to Chinese |
| `Ctrl + Alt + E` | Explain | Provides a detailed explanation of the selected text |
| `Ctrl + Alt + S` | Summarize | Extracts a core summary from the selected text |
| `Ctrl + Alt + R` | Polish | Polishes the selected text for fluency and professionalism |
| `Ctrl + Alt + G` | Grammar Check | Checks for grammatical errors and suggests corrections |

> *Note: All hotkeys can be reconfigured in the application's Settings window.*

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
    ├── Models/
    │   ├── ActionItem.cs       # Action item data model (with selection & ordering)
    │   ├── AiPlatform.cs       # AI platform data model (with New Chat & selectors)
    │   └── AppSettings.cs      # App settings & defaults (proxy, language, text selection)
    ├── Services/
    │   ├── AutoStartService.cs # Auto-start service
    │   ├── ClipboardService.cs # Clipboard access & key simulation service
    │   ├── HotkeyService.cs    # Global hotkey listener (Win32 API)
    │   ├── LanguageManager.cs  # Multi-language / I18n dynamic switching service
    │   ├── Logger.cs           # Logging service
    │   ├── PageInjector.cs     # Web JS script injection & execution service
    │   ├── SettingsService.cs  # Local JSON config loading & persistence
    │   └── TextSelectionService.cs # Text selection & floating toolbar listener service
    ├── Views/
    │   ├── ActionEditWindow.xaml   # Standalone action editing dialog
    │   ├── ActionPanelControl.xaml # Quick action floating panel view
    │   ├── ElementPickerWindow.xaml# Visual DOM element picker window
    │   ├── MainWindow.xaml         # Main window (with WebView2 control)
    │   ├── PlatformEditWindow.xaml # Platform editing & selector configuration window
    │   ├── SelectionToolbarWindow.xaml # Text selection AI floating toolbar window
    │   └── SettingsWindow.xaml     # Settings window (platform/action/selection/proxy/language)
    └── Converters/             # XAML data converters
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

This project integrates **Costura.Fody** to merge all dependency DLLs and `injector.js` static resources into a single standalone `.exe` file:

- **Build via command line**:
  ```bash
  dotnet build AIHelper/AIHelper.csproj -c Release
  ```
- **Output**:
  The packaged single file is located at `AIHelper/bin/Release/net48/AIHelper.exe`. You can copy `AIHelper.exe` (along with `AIHelper.exe.config`) to any location and run it independently — no additional DLLs or `Assets/` directory required.

---

## 📖 Usage Guide

1. **Text Selection AI Helper**:
   - Select text in any app, and the floating AI toolbar will automatically pop up near your cursor. Click any action icon to execute instantly.
   - Toggle text selection monitoring anytime via the system tray context menu or under "Settings -> Selection Settings".
2. **Configure & Manage AI Platforms**:
   - On first launch, the settings window opens automatically with built-in presets for DeepSeek, Claude, Gemini, ChatGPT, Qwen, Zhipu, Kimi, etc.
   - Click "Add" or "Edit" to modify platform URLs, "New Chat" selectors, input boxes, and submit button selectors. Click "🎯 Pick" to visually capture elements on live webpages.
   - Optionally toggle whether to auto-trigger a "New Chat" before sending prompts.
3. **Text Selection & Hotkey Processing**:
   - **Copy** or **select** text in any application (browser, Word, code editor, etc.).
   - Press the assigned hotkey (e.g., `Ctrl+Alt+T`) to wake up AIHelper, switch to the active AI platform, inject the prompt, and submit.
4. **Action Panel & Action Reordering**:
   - Press `Ctrl+Alt+Space` to bring up the quick action panel.
   - In "Settings -> Action Management", customized ordering (Move Up / Move Down) and standalone dialog editing (`ActionEditWindow`) are supported.
5. **Proxy & Language Settings**:
   - Switch UI language between English and Chinese, and configure HTTP/HTTPS/SOCKS proxy settings in the settings window.

---

## 📝 Changelog

### > v0.3.5 Updates Summary

- **✨ Text Selection AI Toolbar**
  - Added text selection listener service; floating AI toolbar automatically pops up upon text selection for one-click processing.
  - Optimized selection sensitivity, anti-overlap positioning, popup smoothness, and tray menu toggle.
- **🌐 Multi-Language Interface (I18n)**
  - Added seamless dynamic switching between Simplified Chinese and English.
- **🔄 Platform "New Chat" Automation**
  - Added "New Chat Selector" configuration to automatically open a fresh chat session before executing prompts.
- **⚡ Auto-Submit Control**
  - Added "Auto Submit" toggle in settings for automatic prompt submission or manual Enter confirmation.
- **⚙️ Action Management & Custom Ordering**
  - Added custom action ordering (Move Up / Move Down) in settings and refactored action editing into a standalone `ActionEditWindow` dialog.
- **🌐 Network Proxy & Update Configuration**
  - Added HTTP / HTTPS / SOCKS proxy support; added project homepage and update check URL settings.
- **📌 System Tray Menu Enhancements**
  - Added "Open Settings" and "Enable/Disable Selection Toolbar" toggle options to the system tray context menu.

---

## 📄 License

This project is licensed under the [Non-Commercial License](LICENSE). It is intended for personal study, research, and non-commercial use only. For commercial use, please contact the original author for authorization.

