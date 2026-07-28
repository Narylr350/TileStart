<div align="center">

<img src="src/TileStart.Host/Assets/TileStart-icon-master.png" alt="TileStart 图标" width="144">

# TileStart

**把 Windows 10 的磁贴开始菜单带回 Windows 10 / 11。**

[![Release](https://img.shields.io/github/v/release/Narylr350/TileStart?display_name=tag&style=flat-square)](https://github.com/Narylr350/TileStart/releases/latest)
![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078D4?style=flat-square&logo=windows)
![Runtime](https://img.shields.io/badge/runtime-.NET%208-512BD4?style=flat-square&logo=dotnet)
![License](https://img.shields.io/badge/license-Apache--2.0-1D76DB?style=flat-square)
![Tests](https://img.shields.io/badge/tests-451%20passed-2EA44F?style=flat-square)

[下载安装器](https://github.com/Narylr350/TileStart/releases/latest/download/TileStart-Setup-win-x64.exe) ·
[下载便携版](https://github.com/Narylr350/TileStart/releases/latest/download/TileStart-portable-win-x64.zip) ·
[查看最新发布](https://github.com/Narylr350/TileStart/releases/latest)

</div>

Windows 11 把磁贴开始菜单整个移除了。TileStart 在 Windows 10 / 11 x64 上重新实现了这套东西——应用列表、磁贴分组、拖放布局、Shell 接管，视觉和交互以真实的 Windows 10 扩展开始菜单为准。

> [!WARNING]
> 当前安装器和可执行文件尚未进行 Authenticode 代码签名，Windows Defender SmartScreen 可能显示"未知发布者"。请只从本仓库的 [Releases](https://github.com/Narylr350/TileStart/releases) 页面下载，并用随 Release 提供的 `SHA256SUMS.txt` 校验文件。

![TileStart 深色主题下的 Win10 风格开始菜单](docs/tilestart-win10-start-menu.png)

## 为什么做 TileStart

Windows 10 的磁贴开始菜单在 Windows 11 里消失了。习惯用分组磁贴管理常用程序的人，很难接受那排小图标作为替代。TileStart 最初就是为填这个缺口而写的。

除了把磁贴工作区搬回来，它也补上了原版长期没做好的几件事：把便携软件、普通文件、脚本和网址放进同一个开始菜单，让图标和背景真正能改，以及在 Shell 注入失败时不会把原生开始菜单也一并弄坏。配置全保存在本地，支持完整备份和跨机迁移。

## 功能

### 开始菜单与应用列表

- 单独按 `Win` 或点击任务栏开始按钮打开 / 关闭 TileStart。
- 保留 `Win+E/R/D/L/I/数字/方向键/Shift+S` 等系统组合键。
- 扫描用户与公共开始菜单，显示 Win32、UWP/MSIX 应用。
- 最近添加、应用文件夹、字母索引和 Windows Search 转交。
- 便携应用可加入应用列表，并提供单独的取消固定操作。
- Explorer 重启后自动恢复接管。

### 磁贴工作区

- Win10 风格磁贴组、组命名和二维组布局。
- 小 `1×1`、中 `2×2`、宽 `4×2`、大 `4×4` 四种磁贴尺寸。
- 组内重排、跨组移动、自动让位、边缘滚动和整组拖动。
- 把磁贴组成文件夹，管理文件夹内容，或从现有组拆分为新组。文件夹预览随实际内容更新；展开时只推动发生重叠的后续分组列。
- 支持固定 `.exe`、`.lnk`、普通文件、文件夹、批处理、PowerShell、URL、UWP/MSIX 与自定义命令，可设置启动参数、工作目录和管理员运行。

### 图标与外观

- 使用应用默认图标、程序资源或本地图片，支持 PNG、JPEG、BMP、ICO、GIF、SVG，也可主动下载网络图标。
- 静态图片与 GIF 可作为磁贴背景。
- 界面风格和颜色模式分别设置：Windows 10 / 11 风格，随系统 / 浅色 / 深色模式。
- 自定义背景色、文字色、图标大小与位置、标题显示和背景缩放。
- 磁贴设置窗口有实时预览，支持一键恢复默认外观，以及"应用"和"保存并关闭"两种提交方式。

### 备份与恢复

从通知区域右键 TileStart 图标，选择 **"备份与恢复…"**：

- 一键创建完整 `.tilestartbackup` 备份，可分类选择只恢复磁贴布局、应用列表、隐藏状态、窗口偏好、图标资源或任务栏快捷方式。
- 自动收集外部本地图标、GIF 与背景图片，换电脑迁移时不需要单独处理图片资源。
- 恢复前自动创建当前状态的安全快照；日志和旧备份不会被递归打包进去。
- 对恢复归档执行路径、文件数量和体积检查。

### 托盘与系统集成

- 暂停 / 恢复 Shell 接管，或主动打开原生开始菜单。
- 切换登录自启动。
- 资源管理器右键"添加到 TileStart 应用列表"或"添加到 TileStart 磁贴区"。
- 设置二级页面支持返回上一级；"关于项目"中可主动检查 GitHub 最新 Release。
- 可从设置页导出包含系统信息、版本和运行日志的诊断包，用于兼容问题反馈。
- 更新包下载后必须通过 SHA-256 校验；安装版启动安装器，便携版打开下载目录供手动覆盖。
- Host、IPC 或 Hook 不可用时采用 fail-open，放行原生行为。

## 下载与安装

最新版本：**v0.1.12**

| 文件 | 用途 |
| --- | --- |
| [`TileStart-Setup-win-x64.exe`](https://github.com/Narylr350/TileStart/releases/latest/download/TileStart-Setup-win-x64.exe) | 推荐。管理员安装，默认进入 `C:\Program Files\TileStart`，向导可修改目录 |
| [`TileStart-portable-win-x64.zip`](https://github.com/Narylr350/TileStart/releases/latest/download/TileStart-portable-win-x64.zip) | Self-contained 便携版，无需另装 .NET 运行时 |
| [`SHA256SUMS.txt`](https://github.com/Narylr350/TileStart/releases/latest/download/SHA256SUMS.txt) | Release 附件的 SHA-256 校验值 |

### 安装版

1. 从 Releases 下载 `TileStart-Setup-win-x64.exe`。
2. 运行安装器并通过 UAC 确认。
3. 按需修改安装目录、登录自启动和桌面快捷方式选项。
4. 安装后运行 TileStart；程序会常驻通知区域。

升级时直接运行新版本安装器即可。卸载会删除程序文件、Shell 右键菜单和安装器创建的自启动项，但 `%LOCALAPPDATA%\TileStart` 中的用户配置会保留。

### 便携版

1. 解压 `TileStart-portable-win-x64.zip` 到普通可写目录。
2. 运行 `TileStart.Host.exe`。
3. 不要只复制 Host；`TileStart.Injector.exe` 与 `TileStart.ShellHook.dll` 必须留在同一目录。

## 快速使用

- **打开菜单**：按一次 `Win`，或点击任务栏开始按钮。
- **固定应用**：在左侧应用列表右键应用，添加到磁贴区域。
- **固定便携软件**：在资源管理器右键 `.exe` / `.lnk`，添加到应用列表或磁贴区域。
- **固定任意项目**：将文件、文件夹、脚本或网址拖入磁贴区。
- **调整布局**：拖动磁贴或磁贴组；把磁贴拖到另一块上可组成文件夹。
- **自定义磁贴**：右键磁贴打开设置。
- **恢复原版**：右键通知区域图标，暂停接管或直接打开原生开始菜单。
- **备份配置**：右键通知区域图标，打开备份与恢复。

## 数据位置

TileStart 的用户数据保存在：

```text
%LOCALAPPDATA%\TileStart
```

主要内容：

```text
layout.json          磁贴与分组布局
custom-apps.json     手动添加的应用
hidden-apps.json     应用隐藏状态
window.json          窗口尺寸
navigation.json      导航偏好
icons\               网络、SVG 与恢复后的受管图标
backups\             恢复前自动创建的安全快照
TileStart.log        本地诊断日志
ShellHook.log        Explorer Hook 诊断日志
```

## 兼容性

Windows 10 基线验证环境：

```text
Windows 10 Pro for Workstations
22H2 build 19045 x64
2560 × 1600 / 150% DPI / 240 Hz
任务栏位于底部
```

目标平台是 Windows 10 / 11 x64，但 Shell 接管与 Windows build 强相关：

- Windows 10 build 19045 已完成基线实机验证。
- Windows 11 当前以 25H2 build 26200.8875 为验证基准，使用独立的 Win11 适配路径。build 26200 已验证 Injector 能选择 Win11-modern 适配器并完成 ShellHook 注入。
- 不同 Windows build、任务栏布局和全屏场景仍需持续实机回归。build 号用于选择最接近的适配器族，不作为精确白名单门禁；未知或未来 build 会先尝试兼容路径，并在日志中记录实际 build 与适配器选择。
- Host 或 Hook 不可用时必须保留原生开始菜单作为回退。

## 已知限制

- 安装器和可执行文件尚未进行 Authenticode 代码签名。
- 不同 Windows build、任务栏布局、DPI 和全屏场景的 Shell 接管覆盖仍在持续扩大。
- 全屏游戏中单独 `Win` 键接管的行为仍需继续验证。
- 高刷新率屏幕上的动画流畅度仍低于 Windows 原生开始菜单。
- NVIDIA App Overlay 的自动 DRS 配置 helper 尚未集成到安装器。
- 当前不提供后台自动更新、云同步、插件市场或视频背景；更新检查仅在用户从托盘主动触发时联网。

## 问题反馈与诊断

请通过 [GitHub Issues](https://github.com/Narylr350/TileStart/issues/new/choose) 提交问题。Bug 表单会要求 TileStart 版本、完整 Windows build、其他 Shell 增强软件、复现步骤以及预期和实际结果。

在 **设置 → 维护与信息 → 诊断日志** 中可以导出 `TileStart-diagnostics-*.zip`，包含：

- `system-info.txt`：TileStart 版本、Windows 版本/build 和进程架构。
- `TileStart.log`：Host 启动、Injector 适配器选择、注入结果和托管异常。
- `ShellHook.log`：开始按钮、Win 键、IPC 与 fail-open 路径记录。
- `README.txt`：诊断包内容和隐私提醒。

诊断包不包含磁贴布局、图标或备份，但日志仍可能出现本地文件路径和应用名称，公开上传前请先检查内容。若 `TileStart.Host.exe` 或 `explorer.exe` 崩溃，可额外附上 `%LOCALAPPDATA%\CrashDumps` 中的 `.dmp`，或使用 Windows 11 任务管理器的"创建实时内存转储文件"；转储可能包含敏感内存内容，不建议未经检查公开上传。

## 安全与故障回退

TileStart 把系统接管限制在最小原生组件内：

```mermaid
flowchart LR
    Explorer[Windows Explorer] --> Hook[TileStart.ShellHook.dll]
    Hook -->|Named Pipe| Host[TileStart.Host.exe]
    Injector[TileStart.Injector.exe] --> Hook
    Hook -->|Host 或 IPC 不可用| Native[放行原生开始菜单]
```

- `TileStart.ShellHook.dll` 不加载 WPF、.NET 或业务配置。
- 应用扫描、窗口、磁贴布局和设置都运行在独立 Host 进程。
- Injector 负责 Explorer 生命周期与 Hook 挂载 / 卸载。
- Host 崩溃、IPC 超时或 Hook 安装/运行失败时，不应阻断原生行为。
- 卸载时会停止 Host / Injector，并清理安装器注册的 Shell 项。

## 从源码构建

### 环境要求

- Windows 10 / 11 x64
- [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0)（`global.json` 固定 `8.0.408`，允许滚动到最新补丁）
- Visual Studio 或 Build Tools，包含 MSBuild 与 MSVC x64 工具链
- Inno Setup 6（仅生成安装程序时需要）

### 托管代码与测试

```powershell
dotnet restore tests\TileStart.Host.Tests\TileStart.Host.Tests.csproj
dotnet build src\TileStart.Host\TileStart.Host.csproj -c Release
dotnet test tests\TileStart.Host.Tests\TileStart.Host.Tests.csproj -c Release
```

### 完整混合解决方案

从 Visual Studio Developer PowerShell 运行：

```powershell
msbuild TileStart.sln /restore /m /p:Configuration=Release /p:Platform=x64
```

不要使用 `dotnet build TileStart.sln`：.NET SDK MSBuild 不包含 Visual C++ targets。

### 生成便携包与安装器

```powershell
.\scripts\Build-Package.ps1
```

输出：

```text
artifacts\package\TileStart-portable-win-x64.zip
artifacts\installer\TileStart-Setup-win-x64.exe
```

只生成便携包：

```powershell
.\scripts\Build-Package.ps1 -SkipInstaller
```

`artifacts/` 是本地构建输出，不提交到 Git。

### 自动发布

推送符合 `v主版本.次版本.修订号` 格式的标签后，GitHub Actions 会自动运行测试、构建完整 x64 解决方案、生成便携包与安装器、计算 SHA-256，并创建 GitHub Release：

```powershell
git tag v0.1.12
git push origin v0.1.12
```

也可以在 GitHub 仓库的 **Actions → Release → Run workflow** 中输入 `0.1.12`。手动运行会在当前 `main` 提交上创建对应标签和 Release。

本地为指定版本生成相同产物：

```powershell
.\scripts\Build-Package.ps1 -Version 0.1.12
```

## 项目结构

```text
src/TileStart.Host/          WPF Host、应用扫描、磁贴、设置、备份与托盘
src/TileStart.ShellHook/     Explorer 内的最小原生 Hook
src/TileStart.Injector/      Hook 挂载、兼容适配与 Explorer 生命周期
src/TileStart.ShellProbe/    Shell / IPC 验证工具
tests/TileStart.Host.Tests/  托管单元、行为与 XAML 回归测试
installer/                   Inno Setup 安装配置
scripts/                     构建和打包脚本
docs/                        设计、验证和用户反馈资料
```

## 参与开发

欢迎通过 Issues 或 Pull Requests 提交可复现的问题、Windows build 信息、性能采样和改进建议。涉及 Shell 接管、动画、视觉或不同 DPI 的变更，请同时说明 Windows build、DPI、任务栏位置和验证方式。

## 许可证

TileStart 使用 [Apache License 2.0](LICENSE)，允许个人和商业使用、修改与再发布，但必须保留许可证和版权声明，并遵守 Apache-2.0 的专利与署名条款。项目版权声明见 [NOTICE](NOTICE)。
