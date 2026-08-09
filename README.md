# AIHelper

**中文** | [English](README_EN.md)

**AIHelper** 是一款轻量级 Windows桌面AI助手工具。它旨在将网页端 AI 大模型平台（如 DeepSeek、ChatGPT、Claude、Gemini 等）无缝集成到日常工作中。通过全局快捷键和智能网页脚本注入，实现剪贴板文本的快速翻译、解释、摘要、润色与语法检查。

---

## ✨ 核心特性

- **🚀 全局快捷键与动作面板**
  - 按下全局快捷键（默认 `Ctrl+Alt+Space`）调出快捷动作面板。
  - 支持划词选中或复制文本后，直接通过快捷键（如 `Ctrl+Alt+T`）一键发送给 AI 处理。

- **🌐 多 AI 平台集成**
  - 内置预设支持 **DeepSeek**、**Claude**、**Gemini** 等网页版 AI 平台。
  - 支持在设置中轻松添加、修改或切换自定义网页 AI 平台。

- **⚡ 智能 DOM 脚本注入**
  - 内置 `injector.js` 脚本，可自动识别并定位各大 AI 平台的网页文本输入框。
  - 自动填充 Prompt 模板与选中文本，并自动触发发送按钮，无需手动粘贴与点击。

- **🛠️ 高度可定制的 Prompt 动作 (Actions)**
  - 预设常用动作：**翻译**、**解释**、**摘要**、**润色**、**语法检查**。
  - 用户可自由修改 Prompt 模板、添加新动作并自定义对应的快捷键。

- **💻 现代且轻量的 UI**
  - 基于 WPF 构建，结合 Microsoft WebView2 提供流畅的网页浏览与交互体验。
  - 本地化配置存储（`settings.json`），保护隐私且免去重复登录。

---

## ⌨️ 默认快捷键

| 快捷键 | 动作 / 功能 | 提示词说明 (Prompt) |
| :--- | :--- | :--- |
| `Ctrl + Alt + Space` | 唤醒/隐藏快捷动作面板 | 调出动作列表面板选择执行 |
| `Ctrl + Alt + T` | 翻译 (Translate) | 将选中文本翻译为中文 |
| `Ctrl + Alt + E` | 解释 (Explain) | 详细解释选中文本内容 |
| `Ctrl + Alt + S` | 摘要 (Summarize) | 为选中文本提取核心摘要 |
| `Ctrl + Alt + R` | 润色 (Polish) | 润色选中文本使其通顺专业 |
| `Ctrl + Alt + G` | 语法检查 (Grammar) | 检查语法错误并提供修改建议 |

> *注：所有快捷键均可在应用“设置”窗口中重新配置。*

---

## 🛠️ 技术栈与依赖

- **运行环境 / 框架**：.NET Framework 4.8 / WPF (Windows Presentation Foundation)
- **网页浏览器内核**：[Microsoft.Web.WebView2](https://www.nuget.org/packages/Microsoft.Web.WebView2)
- **JSON 序列化**：[Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json)
- **单文件打包**：[Costura.Fody](https://www.nuget.org/packages/Costura.Fody)
- **脚本注入桥梁**：Vanilla JavaScript (`injector.js`)

---

## 📁 项目结构

```text
AIHelper/
├── AIHelper.sln                # Visual Studio 解决方案文件
└── AIHelper/
    ├── AIHelper.csproj         # 项目工程文件
    ├── App.xaml / App.xaml.cs  # 应用入口与全局资源
    ├── Assets/
    │   └── injector.js         # 自动化注入网页的 JS 脚本
    ├── Models/
    │   ├── ActionItem.cs       # 快捷动作数据模型
    │   ├── AiPlatform.cs       # AI 平台数据模型
    │   └── AppSettings.cs      # 应用配置与默认设置
    ├── Services/
    │   ├── ClipboardService.cs # 剪贴板获取与模拟按键服务
    │   ├── HotkeyService.cs    # 全局 Hotkey 注册与监听服务 (Win32 API)
    │   ├── PageInjector.cs     # 网页 JS 脚本注入与执行服务
    │   └── SettingsService.cs   # 本地 JSON 配置加载与持久化
    ├── Views/
    │   ├── ActionPanelControl.xaml # 快捷动作悬浮面板视图
    │   ├── MainWindow.xaml         # 主界面（含 WebView2 控件）
    │   └── SettingsWindow.xaml     # 设置界面（平台/动作/快捷键配置）
    └── Converters/             # XAML 数据转换器
```

---

## 🚀 编译与运行

### 1. 前置条件

- **操作系统**：Windows 10 / Windows 11
- **开发工具**：Visual Studio 2019 / 2022（需安装 **.NET 桌面开发** 工作负载）或 .NET SDK
- **SDK 要求**：.NET Framework 4.8 Developer Pack
- **运行时需求**：[Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)（Win11 已内置，Win10 通常随 Edge 自动安装）

### 2. 构建步骤

1. 克隆或下载本项目源码至本地：
   ```bash
   git clone https://github.com/chgblog/AIHelper.git
   cd AIHelper
   ```
2. 使用 Visual Studio 打开 [AIHelper.sln](file:///e:/CHG/develop/AIHelper/AIHelper.sln)。
3. 在 Visual Studio 顶部菜单选择 `Any CPU` 或 `x64` 平台，解决方案配置选择 `Debug` 或 `Release`。
4. 按下 `F5` 键运行或按 `Ctrl+Shift+B` 编译解决方案。

### 3. 单文件打包 (Single EXE Packaging)

本项目已集成 **Costura.Fody**，支持将依赖 DLL 及 `injector.js` 静态资源全部合并为单个独立的 `.exe` 文件：

- **通过命令行编译发布**：
  ```bash
  dotnet build AIHelper/AIHelper.csproj -c Release
  ```
- **生成产物**：
  打包后的单文件位于 `AIHelper/bin/Release/net48/AIHelper.exe`。您可以直接将 `AIHelper.exe`（及其同级配置文件 `AIHelper.exe.config`）复制到任意位置独立运行，无需附带任何 DLL 或 `Assets/` 资源目录。

---

## 📖 使用指南

1. **配置 AI 平台**：
   - 首次运行会自动打开设置窗口，可选择当前启用的 AI 平台（如 DeepSeek）并登录您的账号。
2. **文本划词/快捷处理**：
   - 在任何软件（如浏览器、Word、代码编辑器）中**复制**或**选中**需要处理的文本。
   - 按下对应快捷键（例如 `Ctrl+Alt+T`），AIHelper 将自动唤醒、切换至指定 AI 平台窗口、填入提示词并自动提交。
3. **面板操作**：
   - 按下 `Ctrl+Alt+Space` 可快速召唤操作面板，点击需要的 AI 操作项即可。

---

## 📄 许可协议

本项目采用 [Non-Commercial License](LICENSE) (非商业用途许可协议)。仅供个人学习、研究与非商业用途使用。如需商业使用，请联系原作者获取授权。
