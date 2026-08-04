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

开发版默认直接运行 `artifacts\package\TileStart\TileStart.Host.exe`，不使用未发布安装器覆盖正式版。简单改动由用户在 Windows 11 实体机手动验收；只有布局、Shell 接管或真实桌面交互无法由自动测试覆盖，且当前任务提供桌面自动化工具时，才追加自动黑盒验证。

当前 build 26200 实体机已在日常使用中确认单独 `Win` 键、任务栏开始按钮、TileStart 正常退出以及托盘“打开原生开始菜单”旁路的主路径。连续 `SC_TASKLIST` 请求现由 Host 在限时窗口后主动复位旁路事件。Explorer 重启后的自动重新注入和完整 fail-open 矩阵仍保留 build 22631 的历史证据，后续需要在当前 build 做一次成套复验，不能用日常使用记录替代。

Windows build 不再作为精确白名单门禁。Injector 根据 build 范围选择 Win10、Win11 legacy 或 Win11 modern 适配器；未知或未来 build 使用最接近的兼容路径并记录选择结果。该策略只取消“未验证即禁用”的限制，不把未知 build 视为已经验证，实机结论仍按具体 build 记录。

## 性能分析与响应性验证

本机已具备 Rider Monitoring、Rider 集成 dotTrace 与 Windows Performance Toolkit（WPR/WPA）。使用规则：

- Rider Monitoring 用于日常自动发现长耗时方法；列表时间可能是累计值或方法存活时间，必须进入调用树确认调用次数、Self Time 和线程。
- dotTrace Timeline 用于三个独立场景：冷启动 60 秒、磁贴设置滑块连续拖动 5 秒、应用右键菜单连续打开 10 次。不同场景不得混在同一采样中。
- WPR/WPA 用于 Explorer/Shell/DWM、磁盘 I/O、线程 Ready/Wait 和高系统负载问题；不要用单纯 CPU 百分比推断 UI 卡顿。

当前性能基线：

- 95 个磁贴、332–333 个应用时，`Application content ready` 到完整磁贴视觉恢复曾耗时 34.7–55.9 秒。相同真实布局的一次性基准中，旧线性身份匹配耗时 56.70 秒、调用 `LaunchTargetIdentity.GetKey` 22588 次；按应用身份建立一次局部字典后耗时 0.99 秒、调用 427 次，约快 57 倍。完整批次现已使用 operation-local 身份索引，并保留应用扫描顺序中的第一个重复身份，文件夹子磁贴复用同一索引。真实 Release 开发构建连续三次完整批次耗时为 1.616、1.640、1.644 秒，均处理并应用 95 个磁贴；初始无应用表的本地图标批次为 2.517–2.551 秒。曾实验将该初始批次按组拆到 8 个 Lowest STA：纯加载由 825–854 ms 降到 327–332 ms，真实批次降到 625–657 ms，但启动后 1 秒打开菜单时出现 390–425 ms 长帧、p95 30.7–59.9 ms，完整应用批次也退化到 1.83–3.02 秒。该方案已撤销；不得用增加 Shell/COM 并行度换取后台数字而破坏首次交互。
- 89 个非文件夹磁贴目标与 333 个应用中，仅 16 个原始启动路径完全相同，规范身份相同为 47 个；跨批次 Shell 图标缓存覆盖率太低，而且 LNK、EXE、AppsFolder 可能有不同图标来源，不建立启动期全局缓存。应用图标加载中，285 个经典应用约需 3.05 秒，48 个 Packaged 应用仅约 117 ms。两组现按完成顺序处理：窗口隐藏时 Packaged 组在真实启动中约 142 ms 即应用；若菜单可见且经典组仍在加载，则暂存 Packaged 结果，待最后一组完成后统一应用。两次强制可见测试均得到 `deferred=True`，入口有效采样未出现 200 ms 级中段长帧。
- 应用目录变化后的刷新原先会先复用旧图标，再把全部应用重新送入 Shell 图标加载器；典型“333 个已有应用加 1 个新增项”仍会重复加载约 333 次。刷新批次现只加载图标为空的新增或来源变化项；经典应用按启动身份复用，Packaged 应用还要求 `AppUserModelId` 与包安装路径均未变化，包升级不会错误沿用旧图标。首次启动时所有图标仍为空，因此启动路径和并发度不变。
- Release 开发构建在 9 个组、72 个顶层磁贴、95 个总磁贴的真实布局上连续采样：`MainWindow` 构造耗时 711.78–795.40 ms，其中 `InitializeComponent` 为 213.03–239.90 ms，`RestoreSavedLayout` 为 435.76–486.83 ms，所有 Controller 构造合计约 23 ms。进一步分段确认，布局恢复中的 `_prepareMotionElements()` 强制隐藏视觉树 `Measure/Arrange/UpdateLayout` 占 443.83–530.77 ms，其余添加组、坐标检查和保存判断合计低于 3 ms。
- 不能简单删除初始视觉树预热：取消后 Host 启动约从 1.07 秒降到 0.65 秒，但第一次打开从约 217 ms 增到约 723 ms。初始预热现由 `StartWindowController` 以 `DispatcherPriority.ApplicationIdle` 排队，用户打开前会取消尚未执行的任务；真实 Release 开发构建三次 Host 启动为 616、654、633 ms，随后空闲预热耗时 390–441 ms。用户已在 Windows 11 实体机确认第一次打开流畅。Dispatcher 已开始执行的预热无法中途抢占，历史 A/B 中请求恰好落在该窗口时，从请求到 `Window.Show` 约 423 ms。
- `PrimeApplicationActivation` 的隐藏窗口预热约占 200 ms，但取消后首次 `Window.Show` 从约 197 ms 增到 331 ms；在没有更完整的前台激活回归证据前保留。
- 入口动画在正常负载下 5 次采样平均帧间隔为 5.05–6.79 ms、p95 为 7.92–20.16 ms；使用 14/16 个逻辑处理器的 Normal 优先级计算负载时，5 次 p95 为 10.46–20.11 ms，没有显著恶化，且主 Host 在窗口显示期间实测为 `AboveNormal`。因此不提高到 High/Realtime。帧序列显示第一次打开的 40–64 ms 长间隔集中在第 2–4 帧，属于首次 `Window.Show`/DWM 合成成本；后续约 20 ms 间隔多位于第 120 帧以后，即 517 ms 入口动画结束附近，不应当作为动画中段卡顿解读。`RenderFrameProbe` 在启用 `TILESTART_PROFILE_RENDER=1` 时会额外记录最长 5 个间隔及帧位置，并以 `Stopwatch` 绝对时长截断样本；Dispatcher 清理计时器即使延迟，也不会再把动画结束或窗口隐藏后的无渲染间隔算成长帧。
- 磁贴设置滑块原先每次 `ValueChanged` 都同步重载图标和背景。一次性基准中，普通 PNG/GIF 热路径约 0.05–0.16 ms，EXE 图标约 0.75 ms，简单 SVG 约 0.55 ms，但含 200 个图元的复杂 SVG 为 15.9–19.9 ms 平均、45–51 ms p95，足以阻塞 UI 帧。设置窗口现按路径、文件长度和最后修改时间缓存图标及背景；同一窗口内未变化文件只解码一次，文件被替换、删除或重新出现时自动失效。全量测试 729/729 通过，用户已确认开发版滑块与预览行为正常。

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
