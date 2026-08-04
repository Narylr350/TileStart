# TileStart 验证状态

> 最近同步：2026-08-04
>
> 本文记录当前可重复验证入口、最近已有证据和仍需实机完成的范围。具体 UI 实现矩阵见 `ui-implementation-status.md`。

## 1. 验证原则

- Git、源码、自动测试和当前实机行为优先于历史计划与旧截图说明。
- 构建或单元测试通过不能替代 Shell 接管、视觉布局和真实桌面交互验证。
- Win10 与 Win11 使用独立 Shell 适配器族；一个平台的实机结果不能替代另一个平台。
- Win10 外观只有在原版和 TileStart 使用同物理分辨率、同 DPI、同窗口尺寸的画面完成对照后，才能标记为精确复原。
- 动画按状态完整、视觉协调、符合直觉和响应系统动画设置验收，不要求逐帧复制 StartUI 私有 Storyboard。

## 2. 当前环境

### 当前主动开发环境

```text
Windows 11 25H2 build 26200.8875
StartMenuExperienceHost 10.0.26100.4768
2560 × 1600
150% DPI
240 Hz
任务栏位于底部、左对齐
```

### Windows 10 回归环境

```text
Windows 10 Pro for Workstations 22H2 build 19045
2560 × 1600
150% DPI
240 Hz
任务栏位于底部
```

当前日常开发、开发版运行和安装测试在 Windows 11 实体机进行。Windows 10 22H2 虚拟机已经完成当前一轮安装包、Win10 Light / Dark、默认布局和桌面交互基线确认，后续作为兼容性参考环境按需复验；它不能替代多显示器、混合 DPI、触摸或全屏游戏等真实硬件覆盖。

## 3. 可重复自动验证

### 托管项目

```powershell
dotnet restore tests\TileStart.Host.Tests\TileStart.Host.Tests.csproj
dotnet build src\TileStart.Host\TileStart.Host.csproj -c Release
dotnet test tests\TileStart.Host.Tests\TileStart.Host.Tests.csproj -c Release
```

### 完整 x64 解决方案

在 Visual Studio Developer PowerShell 中执行：

```powershell
msbuild TileStart.sln /restore /m /p:Configuration=Release /p:Platform=x64
```

不要使用 `dotnet build TileStart.sln` 构建混合解决方案；.NET SDK MSBuild 不包含 Visual C++ targets。

### 最近已记录结果

最近记录：

```text
cf81211：Release 全量测试 721/721 通过，完整 x64 混合解决方案构建通过
82375d0：Win11 设置窗口 Transient Acrylic、窗口所有权与响应式布局专项测试通过，并由用户在实体机验收
cf81211：原生开始菜单旁路、设置 Expander 动画和首次展开闪退修复由用户实机验收
d9d120e：响应式对话框定向测试 13/13 通过，磁贴设置窄窗口无整页水平滚动由用户验收
```

以上是已有提交和实机验收证据，不代表本文同步时重新执行了全量测试。每次修改生产代码后仍应按实际改动重新运行相应构建和测试。

## 4. 已有功能证据

| 范围 | 当前证据 |
| --- | --- |
| Shell fail-open | Host、IPC 或 Hook 不可用时放行原生开始菜单；具有自动测试和历史实机记录 |
| Win 键状态机 | 单独 Win 与常用 Win 组合键具有自动测试；当前 build 仍需扩大实机回归 |
| Explorer 生命周期 | 自动重新注入和正常卸载具有历史 Win10 / Win11 记录；build 26200 需正式复验完整范围 |
| 窗口定位 | 底部锚定、工作区定位、尺寸持久化、任务栏边缘和 DPI 路径已有源码与测试 |
| 磁贴布局 | 四种尺寸、8 单元网格、二维组布局、分组换行、碰撞和持久化具有自动测试 |
| 拖动事务 | 组内/跨组移动、文件夹、创建组、整组拖动、Drop、Cancel 和边缘滚动具有专项测试 |
| 字母索引 | 双层 SemanticZoom、尺寸、时长和返回定位具有专项测试，并有用户实机视觉验收 |
| 文件夹 | 应用文件夹、最近添加和磁贴文件夹动画具有专项测试；最近添加和字母索引有用户验收记录 |
| 右键菜单 | 顶层菜单、子菜单、方向解析、Win10/Win11 样式资源具有专项测试 |
| 主题 | Win10/Win11 Light/Dark 资源、主窗口 Acrylic、Popup Acrylic、对话框 Transient Acrylic、Accent 解析和主题化控件状态具有自动测试 |
| 任意目标 | 应用、文件、文件夹、脚本、URL、UWP/MSIX 和自定义命令具有分类与启动测试 |
| 持久化与备份 | 布局、视觉设置、外部资源、自包含备份、恢复前快照和恶意归档路径拒绝具有自动测试 |

## 5. UI 当前验收状态

### 已实现并可继续沿用

- Win10 主窗口、导航轨、All Apps、磁贴区和二维组布局。
- 开启动画、字母索引动画、应用/磁贴文件夹动画、最近添加动画。
- 拖动、120 ms reflow、让位重排、整组拖动和取消回滚。
- 右键菜单、导航轨展开、Press / Reveal 和平滑滚动。
- Win10/Win11 Light/Dark 主题；两种主题共享 Win10 主布局。
- Win10 主窗口、应用列表、磁贴材质、导航层、文件夹和右键菜单已在 Win10 22H2 虚拟机完成当前一轮视觉验收。
- Win11 磁贴、应用列表、菜单、导航层、公共对话框和二级编辑控件已完成当前一轮 Fluent 视觉覆盖并通过用户实机验收。
- Win11 设置和编辑窗口已统一使用系统 Transient Acrylic；磁贴设置、组设置与公共设置之间的 Owner/单实例调度、设置 Expander 动画以及磁贴设置窄窗口响应式布局已通过用户实机验收。
- 托盘“打开原生开始菜单”已在当前 Win11 实体机重新验收，连续 Shell 请求通过限时手动复位旁路窗口放行，不再回到 TileStart。

### 仍不能标记为精确完成

- Win10 主页面已达到肉眼对照基本一致，但未为所有 Hover、Pressed、Disabled、空布局和极端尺寸状态建立成套公开截图证据。
- Win11 当前一轮已覆盖主要可见表面；后续不再为覆盖率主动扩张，只处理实际使用中发现的明确遗留状态。
- Acrylic 的 Win32 AccentPolicy 映射是经过实机验证的近似，不宣称与 UWP AcrylicBrush 数学等价。

### 明确的行为缺口

- 当前直接输入仍打开 TileStart 内置搜索框并过滤应用，没有转交 Windows Search。
- All Apps 的字母 type-to-jump 尚未实现。
- 完整方向键、Tab、Enter、Space、Menu 键和跨区域焦点矩阵尚未实现。
- 触摸、笔、长按、Narrator、文本缩放和完整 High Contrast 主题尚未实现。
- 菜单关闭使用整窗淡出；应用启动后直接关闭，没有内容级退出动画。

## 6. Windows 设置响应验证

| 设置 | 当前状态 | 后续验证 |
| --- | --- | --- |
| 应用浅色/深色模式 | 已接入 | 在 System 模式切换 Windows 应用主题，确认 TileStart 自动重启并应用正确字典 |
| 动画效果 | 已接入 | 关闭 Windows 动画后，确认动画路径立即到达最终状态且不残留不可点击层 |
| 透明效果 | 已接入 | 开关透明效果后重新打开菜单，确认 Acrylic 与不透明回退切换 |
| 高对比度 | 部分接入 | 当前只确认禁用 Acrylic 和托盘回退；主界面完整视觉待补 |
| 系统强调色 | 已接入 | 启动时读取；系统颜色变化后自动重启并统一刷新 Accent 相关资源 |
| StartColorMenu / ColorPrevalence | 已接入 | 菜单显示和主题重载会重新解析材质；Win10 强调色开关、默认磁贴表面和实时缩放回退已有专项修复与用户验收 |
| Start 内容设置 | 未映射 | 最近添加、最常用、建议、应用列表、全屏、最近项目和文件夹入口待逐项研究 |

## 7. Shell 实机回归清单

- 单独按 `Win` 打开 TileStart，再按一次关闭。
- 点击任务栏开始按钮打开 TileStart，原生开始菜单不闪现。
- `Win+E/R/D/L/I/数字/方向键/Shift+S` 保持系统行为。
- Host 被强制结束、Hook 加载失败或 IPC 超时时，原生开始菜单可用。
- Explorer 重启后 Hook 自动恢复。
- 暂停接管后原生开始菜单可用。
- 正常退出或卸载后无残留 Hook、自启动和托盘进程。
- 多显示器下在触发所在屏幕打开。
- 任务栏自动隐藏和不同边缘时定位正确。
- 100%、125%、150%、175%、200% DPI 分别验证。
- 全屏游戏运行时不误弹出。
- 锁屏、注销和重新登录后行为正常。

## 8. Win10 视觉验收清单

必须在同一物理分辨率和 DPI 下采集原版与 TileStart：

1. 空磁贴区、单列组、多列组和多行换行布局。
2. 导航轨收起、Hover 展开和点击固定状态。
3. 最近添加收起/展开、应用文件夹和普通字母分组。
4. 字母索引进入、选择、返回和不可用字母状态。
5. 四种磁贴尺寸、不同图标和标题长度。
6. 空名称组、组名 Hover、编辑和整组拖动。
7. 右键菜单、子菜单、Hover、Pressed、Disabled 和 Check 状态。
8. ScrollBar 未出现、出现和 Hover 展宽状态。
9. 透明开启、透明关闭、高对比度和动画关闭状态。

每项应记录：原版证据、TileStart 截图、DIP/物理像素差异、判断和是否需要代码调整。只有整窗及关键状态完成该闭环，才能把 Win10 外观标记为精确复原。

## 9. Win11 主题验收清单

Win11 主题只验收视觉层，不验收 Win11 原生布局：

- 主布局、应用列表、磁贴和组位置必须与 Win10 主题一致。
- 字体切换为 Segoe UI Variable 系列。
- 窗口、弹窗、菜单、卡片和普通控件按层级使用正确圆角。
- 磁贴使用 4 DIP 圆角和弱 Subtle Fill，保持 Win10 磁贴尺寸与布局，不改成 Win11 Pinned App 卡片。
- Light/Dark 的文字、表面、描边、Hover、Pressed、Selected 和 Disabled 状态一致。
- Acrylic 能保留壁纸色参与，不硬编码截图中的最终合成色。
- 透明关闭、高对比度和 Accent 变化有合理回退。

## 10. 性能验证基线

当前可用工具链：

- Rider Monitoring：日常运行时自动发现长耗时方法、GC 和内存异常，只作为热点入口。
- Rider 集成 dotTrace Timeline：对单一场景查看调用次数、Total / Self Time、线程和等待状态。
- Windows Performance Recorder / Analyzer：分析 Explorer、Shell、DWM、磁盘、线程调度和高系统负载下的 ETW 事件；当前实体机已安装。

2026-08-04 基线审计记录：

```text
布局规模：95 个磁贴，333 个可启动应用
Application content ready → 完整磁贴视觉恢复：34.7–55.9 秒
主要热点：LaunchTargetIdentity.ResolveShortcutTarget / GetKey
根因：每个磁贴线性扫描应用并重复解析 .lnk，最坏约 31,635 次身份计算
```

优化后的首要验收目标是将该路径改为单次应用身份索引，使复杂度从 `O(磁贴数 × 应用数)` 降到 `O(磁贴数 + 应用数)`，并在同一布局与应用集合下复测。设置滑块场景应保证图标大小、位置和背景缩放变化不会重复解码未变化的图像路径。

## 11. 完成判定

单项工作完成时至少记录：

```text
Source evidence: 对应源码、规格或实机观察
Change: 实际修改范围
Automated verify: 构建与测试
Desktop verify: 必要的真实 Windows 交互或同尺度截图
Remaining uncertainty: 未覆盖平台、DPI、输入方式或近似实现
```

编译成功、测试通过或保存一张截图都不能单独替代 Win10 外观、Win11 主题和 Shell 接管的完整验收。
