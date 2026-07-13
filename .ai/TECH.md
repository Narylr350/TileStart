# Tech Direction

## 技术方向

- Host 语言：C# 12
- Host 运行时：.NET 8
- Host UI：WPF
- Shell Hook：C++20 x64 DLL
- Injector：最小原生 Win32 程序
- IPC：Windows Named Pipe
- 配置：System.Text.Json
- 安装程序：Inno Setup
- 构建：dotnet + MSBuild/MSVC
- 测试：xUnit、原生组件测试和 Windows 实机集成验证
- 目标平台：Windows 10 22H2 / Windows 11 x64

采用 WPF 主程序与原生 C++ Shell Hook 的混合方案：WPF 负责磁贴界面、托盘、设置、应用扫描和配置；注入 Explorer 的原生组件只负责开始菜单触发拦截、IPC 和原生放行，不在 Explorer 内加载 WPF、.NET 或业务配置。

## 组件边界

```text
TileStart.Host.exe
├─ WPF 磁贴窗口
├─ 托盘与设置
├─ 应用扫描与启动
├─ Win 键状态管理
├─ 配置管理
└─ Named Pipe Server

TileStart.ShellHook.dll
├─ 开始按钮与开始菜单请求拦截
├─ Named Pipe Client
└─ Fail-open 原生放行

TileStart.Injector.exe
├─ 挂载和卸载 Hook
├─ Explorer 重启检测
└─ Windows build 兼容检查
```

## Fail-open

- Host 正常且 IPC 可用时，阻止原生开始菜单并通知 Host 打开 TileStart。
- Host 不可用、IPC 超时或当前 Windows build 不受支持时，不阻止原生行为。

## 系统适配

Win10 和 Win11 使用独立 Shell Adapter，避免把不同系统版本的 Hook 定位和行为混在同一实现中。

## 实现取舍

- Host 不引入 MVVM 框架，使用简单 ViewModel 和命令类。
- 第一版使用 JSON，不引入数据库。
- Windows Shell 功能使用最小范围的 COM/Win32 封装。
- 对外部目标统一通过 Windows Shell 启动。
- 不为 MVP 之外的能力建立扩展框架。
