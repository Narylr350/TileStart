# Validation

## 构建环境

项目需要：

- .NET 8 SDK
- Visual Studio Build Tools / MSVC x64 工具链
- Inno Setup
- Windows 10 或 Windows 11 x64 实机环境

缺少可逆的项目级环境配置时由 AI 直接修复；缺少依赖时先尝试恢复。涉及管理员权限或全局软件安装时再请求用户确认。

## 托管代码

```powershell
dotnet restore tests\TileStart.Host.Tests\TileStart.Host.Tests.csproj
dotnet build src\TileStart.Host\TileStart.Host.csproj -c Release
dotnet test tests\TileStart.Host.Tests\TileStart.Host.Tests.csproj -c Release
```

仓库根目录的 `global.json` 固定 .NET 8 SDK。不要用 `dotnet build TileStart.sln` 构建混合解决方案；.NET SDK MSBuild 不包含 Visual C++ targets。

## 完整解决方案

```powershell
msbuild TileStart.sln /restore /m /p:Configuration=Release /p:Platform=x64
```

该命令同时构建托管项目、测试项目和三个原生 x64 项目，必须从 Visual Studio Developer PowerShell 执行，或使用 Visual Studio 安装目录中的 `MSBuild.exe`。

## 发布

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

使用 Inno Setup 生成安装程序，同时保留便携测试版本。

## 自动测试

至少覆盖：

- `1×1`、`2×2`、`4×2`、`4×4` 磁贴占位。
- 8 格分组内的碰撞、空位查找、拖动和跨组移动。
- 快捷方式、文件、文件夹、脚本和 URL 的类型判断。
- 配置保存和重新加载。
- 完整与部分备份恢复、外部图标/背景自包含、恢复前安全快照，以及恶意归档路径拒绝。
- 无效目标不会破坏已有布局。
- IPC 超时和 Host 不可用时进入原生放行路径。
- 未知 Windows build 会选择最接近的 Win10 / Win11 适配器族，而不是被精确 build 白名单拦截。
- 诊断包包含系统/版本信息、Host 日志和 ShellHook 日志，不包含布局或图标资源，并限制单个日志的导出体积。

## 已验证实机环境

### Windows 10 基线

```text
Windows 10 Pro for Workstations
22H2 build 19045
2560 × 1600
150% DPI
240Hz
任务栏位于底部
```

### 当前 Windows 11 适配环境

```text
Windows 11 25H2 build 26200.8875
StartMenuExperienceHost 10.0.26100.4768
2560 × 1600
150% DPI
任务栏位于底部
```

注册表 `ProductName` 可能仍显示升级前的 Windows 10 字符串；平台判断和验证记录以系统 build 为准。

Win11 开始菜单由 `StartDocked.dll` 实现，不是 Win10 的 `StartUI.dll`；私有研究档案中的 Win10 符号、常量和布局公式不可直接迁移到 Win11。必要的原版截图、布局导出和逆向证据保存在仓库外，不随公共源码分发。

当前 Windows 11 环境已确认 Computer Use 可用，需要桌面自动化时按对应 skill 初始化后直接使用。只有实际失败、恢复后仍阻塞验证时才记录具体限制和未覆盖项，不在每轮重复说明可用性或降级策略。

以下 Shell 接管结论来自升级前的 build 22631，尚未在当前 26200 实机复验：Windows 11 build 22631 上已确认 Win11 Shell Adapter 可由 Release Injector 自动注入：单独 `Win` 键和任务栏开始按钮均可打开 TileStart，`Win+E` 保持系统行为；Explorer 重启后 Watcher 能向新的桌面 Shell 自动重新注入，Host 正常退出后 Injector 随之退出并卸载 ShellHook，Explorer 保持响应。

Windows build 不再作为精确白名单门禁。Injector 根据 build 范围选择 Win10、Win11 legacy 或 Win11 modern 适配器；未知或未来 build 使用最接近的兼容路径并记录选择结果。该策略只取消“未验证即禁用”的限制，不把未知 build 视为已经验证，实机结论仍按具体 build 记录。

## Shell 集成验证

- 单独按 `Win` 打开 TileStart，再按一次关闭。
- 点击任务栏开始按钮打开 TileStart，原生开始菜单不闪现。
- `Win+E/R/D/L/I/数字/方向键/Shift+S` 保持系统原行为。
- Host 被强制结束、Hook 加载失败或 IPC 超时时，原生开始菜单可用。
- 在非精确验证 build 上确认 Injector 日志记录了 compatibility fallback、实际 build 和适配器族。
- 从“设置 → 维护与信息 → 诊断日志”导出诊断包，确认包含 `system-info.txt`、隐私说明及现有日志。
- Explorer 重启后 Hook 自动恢复。
- 暂停接管后原生开始菜单可用。
- 卸载后无残留 Hook、自启动和托盘进程。
- 多显示器下在触发所在屏幕打开。
- 任务栏自动隐藏和位于不同边缘时行为正确。
- 100%、125%、150%、175%、200% DPI 分别验证。
- 全屏游戏运行时不误弹出。
- 锁屏、注销和重新登录后行为正常。

编译成功不代表 Shell 接管验证通过，必须完成真实 Windows 桌面交互测试。
