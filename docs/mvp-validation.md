# TileStart MVP 验证记录

本文记录可重复验证证据和仍需实机完成的项目。完成状态以当前 Git、构建输出和实际桌面行为为准，不以计划或推测代替。

## 当前环境

```text
Windows 10 Pro for Workstations 22H2 build 19045 x64
2560 × 1600
150% DPI（144 DPI）
单显示器
任务栏位于底部
```

## 已验证

| 范围 | 证据 |
| --- | --- |
| 托管构建 | `dotnet build src\TileStart.Host\TileStart.Host.csproj -c Release`，0 警告、0 错误 |
| 自动测试 | 75 项 xUnit 测试通过，0 项失败 |
| 完整解决方案 | Visual Studio MSBuild `Release|x64` 构建 Host、Tests、Injector、ShellHook、ShellProbe 通过 |
| 自包含便携版 | 最新 main 生成 `72,419,463` 字节 ZIP；SHA-256 `9A119613FF40C2E36BFDA758839CCE78FB4BAA14263BAEF706B71E4F9327FE60` |
| 安装程序 | 最新 main 生成 `51,225,363` 字节 EXE；SHA-256 `5A4FD527461A00D6BF456FAAAA16F75321464FED46944068F51D45F5E3753DE7` |
| 安装/卸载 | 静默安装、启动、卸载实测通过；安装目录、自启动项、Host、Injector 和 Explorer 模块无残留 |
| 安装自启动任务 | `HKCU\...\Run\TileStart` 写入安装路径，卸载后删除 |
| Host 生命周期 | `--shutdown` 通过 IPC 请求正常退出；Host、Injector 和远程 DLL 均被清理 |
| IPC 可用 | ShellProbe 获得确认，实测往返约 13.6 ms |
| IPC 不可用 | ShellProbe 返回 fail-open，原版开始菜单不被阻断 |
| Explorer 重启 | Shell Explorer PID 改变后 Injector 自动重新注入；Host 退出后新 Explorer 中 DLL 被卸载 |
| 任务栏开始按钮 | 第一次点击显示 TileStart，第二次点击隐藏；原版开始菜单未显示 |
| 窗口定位 | 150% DPI 下当前保存尺寸定位为物理矩形 `(0,460)-(2420,1540)`，底边贴合任务栏工作区 |
| 任务栏边缘 | 实际切换左、上、右、下四个位置后，TileStart 分别贴合任务栏内侧；每次 Explorer 重启均重新注入 Hook |
| 任务栏自动隐藏 | 底部自动隐藏时任务栏退至屏幕外沿，TileStart 底边贴合完整工作区；测试结束恢复原设置 |
| Win 键状态机 | 左/右 Win、单独释放、Win+E、Win+Shift+S、普通按键序列由自动测试覆盖 |
| 托盘菜单接线 | 真实 STA NotifyIcon 验证打开、暂停/恢复、原版开始菜单、自启动入口和退出回调 |
| 磁贴布局 | 四种尺寸、8 单元占位、碰撞、自动换行、拖动事务和跨组移动由测试覆盖 |
| 任意目标 | 应用、文件、文件夹、批处理、PowerShell、URL 和自定义命令分类/启动参数由测试覆盖 |
| 持久化 | 分组、磁贴位置、启动参数和视觉设置 JSON 往返由测试覆盖 |
| Win10 对照证据 | 最新 TileStart 截图保存为 `docs/reference/win10-start/tilestart-mvp-2560x1600-150pct.png`，与同目录原版截图使用相同分辨率和 DPI；该项只证明证据已保存，不代表视觉一致性通过 |

## 当前开放项

以下项目没有足够源码、反编译或实机证据，不能标记为完成：

1. 按 `docs/win10-start-motion-reverse.md` 重建逐元素 Entrance、Dismiss、AppLaunch 和 ViewSwitch 动画，并用原版同帧对照验证。
2. 重建拖动 120 ms reflow、3 DIP 抖动过滤和与 `ReorderThemeTransition`/`AddDeleteThemeTransition` 对应的让位动画。
3. 完成背景材质、字体、图标、应用列表密度、磁贴文字基线、导航轨和滚动条等静态视觉校准。
4. 采集并恢复导航轨展开、应用文件夹、字母索引、上下文菜单、磁贴 hover/press/release 等状态和动画。
5. 使用物理键盘逐项确认单独 `Win` 和 `Win+E/R/D/L/I/数字/方向键/Shift+S`。
6. 使用鼠标实际操作通知区域菜单，确认暂停、恢复和打开原版开始菜单的桌面体验。
7. 在 100%、125%、175%、200% DPI 环境验证窗口、菜单和设置窗口。
8. 在多显示器及混合 DPI 环境验证触发显示器选择和副任务栏定位。
9. 验证锁屏、注销、重新登录和登录自启动后的完整生命周期。
10. 验证全屏游戏或独占全屏应用期间不会误弹。
11. 在 Windows 11 对目标 build 建立独立任务栏适配器并完成实机验证；当前仅允许 build 19045 注入。

## 完成判定

只有上述视觉、Motion、交互和环境开放项均取得对应证据，且最终满足以下条件时，MVP 才可宣告完成。构建通过、自动测试通过或保存一张对照截图，都不能单独替代 Win10 体验验收：

```text
branch = main
working tree = clean
feature branches = none
TileStart.Host processes = 0
TileStart.Injector processes = 0
Explorer TileStart.ShellHook modules = 0
push/tag/release = not performed
```
