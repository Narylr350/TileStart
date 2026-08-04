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
- Host 不可用、IPC 超时、Hook 安装失败或运行时请求失败时，不阻止原生行为。未知或未来 Windows build 会选择最接近的适配器族并记录兼容路径，而不是仅因缺少精确白名单就禁用接管。

## 系统适配

Win10、Win11 legacy 和 Win11 modern 使用独立 Shell Adapter，避免把不同系统版本的 Hook 定位和行为混在同一实现中。Injector 根据 build 范围选择适配器族，ShellHook 校验并记录实际选择；适配失败仍必须 fail-open。

## Win10 StartUI 源码重建路线

生产版本不直接托管、修改或依赖微软 `StartUI.dll` 的内部实现。使用当前 Win10 原版二进制、公开 PDB、运行时 XAML Visual Tree 和实机行为作为证据，选择性重建布局与交互算法，并将重写后的源码保存在 TileStart 内。

首批重建范围：

- `TileMetrics`、`GridMetrics` 和 `FrameMetrics`：磁贴、分组和窗口几何。
- `TileGridLayoutElement`、`LayoutResolver`、`GroupsLayoutResolver`：8 单元网格、组内占位和分组自动换行。
- `TileDragDropRearrangeEngine`、`KeyboardTileRearrangeEngine`：实时让位、跨组移动和键盘重排。
- `StartSizingFrame`、`AllAppsPane`、`NavigationPaneView`：窗口缩放、应用列表和导航轨行为。

逆向证据链：

1. 固定目标 `StartUI.dll` 的文件版本和 SHA-256。
2. 从 PE RSDS 信息取得 PDB GUID/Age，并从 Microsoft Symbol Server 下载匹配的公开 PDB。
3. 使用 DIA/Ghidra 将 PDB 函数名、类型名和地址映射到反编译结果。
4. 使用 UWPSpy/XAML Diagnostics 导出运行时 Visual Tree、属性和 Visual State。
5. 用 WinDbg、ETW 和输入录制补充状态转换与调用顺序。
6. 只重写已取得证据的最小算法，建立原版布局样本和行为测试。
7. 反编译伪代码只作为理解材料，不直接作为可提交源码。

该路线保留现有 WPF Host、Shell Hook、IPC、扫描、启动和配置底座；UI 与算法最终均由 TileStart 自己编译和发布。

## 实现取舍

- Host 不引入 MVVM 框架，使用简单 ViewModel 和命令类。
- 第一版使用 JSON，不引入数据库。
- Windows Shell 功能使用最小范围的 COM/Win32 封装。
- 对外部目标统一通过 Windows Shell 启动。
- 不为 MVP 之外的能力建立扩展框架。

## 性能诊断与热点基线（2026-08-04）

日常性能筛选使用 Rider Monitoring；需要确认调用次数、Self Time、线程与等待状态时使用 Rider 集成 dotTrace Timeline；涉及 Explorer、Shell、DWM、磁盘或系统高负载时使用 WPR/WPA 的 ETW 记录。监测列表中的方法持续时间可能是采样区间累计值或模态窗口存活时间，不能在没有调用树和线程证据时直接解释为单次操作延迟。

当前已确认的最高收益热点位于 `ApplicationPaneController.LoadTileVisuals`：每个磁贴通过 `FirstOrDefault` 线性扫描全部应用，并为候选目标重复调用 `LaunchTargetIdentity.GetKey`。在 95 个磁贴、333 个应用的当前布局下，最坏约 31,635 次身份计算；`.lnk` 路径还会重复创建 `WScript.Shell` COM 对象。运行日志记录的完整磁贴视觉恢复耗时为 34.7–55.9 秒。

首选修复是为单次视觉恢复建立 operation-local 应用身份字典，保留“相同身份按现有顺序取第一个”的语义，把复杂度降为 `O(磁贴数 + 应用数)`。不要先引入永久全局缓存或提高所有后台线程优先级；先消除重复工作，再复测是否需要有界 Shell 图标缓存。磁贴设置窗口还应缓存窗口生命周期内未变化的图标和背景，避免滑块每帧同步读取文件或 Shell 图标。

## NVIDIA App Overlay 兼容（2026-07-22）

当前 Win10 实机上，硬件加速的 `TileStart.Host.exe` 会触发 NVIDIA App 信息悬浮窗，并加载 Overlay 捕获模块 `nvspcap64.dll`。已通过 NVIDIA DRS 创建只关联 `TileStart.Host.exe` 的 `TileStart` 应用 Profile，并实测以下两个隐藏设置需要同时启用：

```text
0x90DE9159 = 0x00000001
0x809D5F60 = 0x10000000
```

单独设置 `0x90DE9159=1` 时，`nvspcap64.dll` 仍会延迟注入。两个设置同时写入并重启 NVIDIA Overlay 后，当前 Release 连续启动两次均不再显示信息悬浮窗，等待 20 秒也未加载 `nvspcap64.dll`。

安装器现已通过 Host 的一次性管理员兼容命令创建或更新该应用 Profile；卸载时只删除 TileStart 自己的 Profile，不全局关闭 NVIDIA Overlay。该设置属于隐藏、驱动版本相关的 DRS 参数，驱动或 NVIDIA App 更新后需要重新验证，必要时重新应用。
