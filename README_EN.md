# AIHelper

**English** | [中文](README.md)

**AIHelper** is a lightweight Windows desktop AI assistant tool. It seamlessly integrates web-based AI platforms (such as DeepSeek, ChatGPT, Claude, Gemini, etc.) into your daily workflow. Through global hotkeys and intelligent web script injection, it enables quick translation, explanation, summarization, polishing, and grammar checking of clipboard text.

---

## ✨ Key Features

- **🚀 Global Hotkeys & Action Panel**
  - Press the global hotkey (default `Ctrl+Alt+Space`) to bring up the quick action panel.
  - Select or copy text, then press a hotkey (e.g., `Ctrl+Alt+T`) to send it directly to AI for processing.

- **🌐 Multi AI Platform Integration & Precise Element Selector**
  - Built-in preset support for 7 major AI platforms: **DeepSeek**, **Claude**, **Gemini**, **ChatGPT**, **Qwen**, **Zhipu**, and **Kimi**.
  - Easily add, edit, or delete custom AI platforms with single-click radio button activation.
  - **🎯 Visual DOM Element Picker**: Customize CSS selectors for "New Chat", "Input Box", and "Submit Button". Built-in WebView2 element picker allows clicking elements directly on the webpage to inspect and retrieve CSS selectors effortlessly.

- **⚡ Intelligent DOM Script Injection**
  - Built-in `injector.js` script that automatically identifies and locates the text input fields on major AI platforms.
  - Auto-fills prompt templates and selected text, then triggers the send button — no manual pasting or clicking required.

- **🛠️ Highly Customizable Prompt Actions**
  - Preset common actions: **Translate**, **Explain**, **Summarize**, **Polish**, **Grammar Check**.
  - Users can freely modify prompt templates, add new actions, and assign custom hotkeys.

- **💻 Modern & Lightweight UI**
  - Built with WPF and Microsoft WebView2 for a smooth web browsing and interaction experience.
  - Local configuration storage (`settings.json` saved at `C:\Users\<your_username>\AppData\Roaming\AIHelper\settings.json`, i.e., `%APPDATA%\AIHelper\settings.json`) to protect privacy and avoid repeated logins.

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
- **Script Injection Bridge**: Vanilla JavaScript (`injector.js`)

---

## 📁 Project Structure

```text
AIHelper/
├── AIHelper.sln                # Visual Studio solution file
└── AIHelper/
    ├── AIHelper.csproj         # Project file
    ├── App.xaml / App.xaml.cs  # Application entry point & global resources
    ├── Assets/
    │   ├── injector.js         # JS script for web page automation injection
    │   └── element-picker.js   # JS script for visual DOM element picking
    ├── Models/
    │   ├── ActionItem.cs       # Action item data model
    │   ├── AiPlatform.cs       # AI platform data model (with custom selectors)
    │   └── AppSettings.cs      # Application settings & defaults
    ├── Services/
    │   ├── ClipboardService.cs # Clipboard access & key simulation service
    │   ├── HotkeyService.cs    # Global hotkey registration & listener (Win32 API)
    │   ├── PageInjector.cs     # Web JS script injection & execution service
    │   └── SettingsService.cs  # Local JSON config loading & persistence
    ├── Views/
    │   ├── ActionPanelControl.xaml # Quick action floating panel view
    │   ├── ElementPickerWindow.xaml# Visual DOM element picker window
    │   ├── MainWindow.xaml         # Main window (with WebView2 control)
    │   ├── PlatformEditWindow.xaml # Platform editing & selector configuration window
    │   └── SettingsWindow.xaml     # Settings window (platform/action/hotkey config)
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

1. **Configure & Manage AI Platforms**:
   - On first launch, the settings window opens automatically. Built-in presets include DeepSeek, Claude, Gemini, ChatGPT, Qwen, Zhipu, Kimi, and more.
   - Click "Add" or "Edit" platform to set platform URLs and custom selectors. Click the "🎯 Pick" button to visually highlight and select elements on live webpages to auto-fill input and submit selectors.
   - Use radio buttons to switch the active AI platform instantly, or use quick-add buttons to insert preset platforms.
2. **Text Selection & Quick Processing**:
   - **Copy** or **select** the text you want to process in any application (e.g., browser, Word, code editor).
   - Press the corresponding hotkey (e.g., `Ctrl+Alt+T`), and AIHelper will automatically activate, switch to the designated AI platform window, fill in the prompt, and submit.
3. **Panel Operations**:
   - Press `Ctrl+Alt+Space` to quickly summon the action panel and click the desired AI operation.

---

## 📄 License

This project is licensed under the [Non-Commercial License](LICENSE). It is intended for personal study, research, and non-commercial use only. For commercial use, please contact the original author for authorization.
