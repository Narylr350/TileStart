# Windows 10 StartUI Motion 逆向记录

> 分析目标：Windows 10 22H2 build 19045 当前机器上的 `StartUI.dll`。
>
> 本文只保存可复核的符号、地址、控制流和参数结论。原始 DLL、PDB、Ghidra 工程、反编译文本、录像和第三方工具均位于 Windows 临时目录，不进入仓库。

## 1. 分析对象与工具

### StartUI.dll

```text
路径：C:\Windows\SystemApps\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\StartUI.dll
文件版本：10.0.19041.6456
大小：8,489,472 bytes
SHA-256：C6AF5A4E4B38F7DB883B6BB93A63A051F4A3EC46F71F763457CAD77E4D570F86
映像基址：0x180000000
```

### 匹配的公开 PDB

```text
文件：StartUI.pdb
GUID：7B21D1C1-9038-D36F-DA43-F1BBFF31EC03
Age：1
大小：39,399,424 bytes
SHA-256：BA49EC7111A7E85E755DE22231B37505CDC41604C3F255D9AF8BA47D6DA4F404
```

### 编译 XAML 资源

```text
路径：C:\Windows\SystemResources\Windows.UI.ShellCommon\Windows.UI.ShellCommon.pri
大小：2,231,880 bytes
SHA-256：FAE49AD9998F75C3A1246D2E7309146E96A4DDA94F01CAD7459CD98FFEAAD488
```

从 PRI dump 的 Base64 资源中提取了全部 40 个 `Files/StartUI/*.xbf`。其中 `NavigationPaneView.xbf`、`AllAppsPane.xbf` 和 `FolderAppSpaceVisual.xbf` 的文件头均为 `XBF\0`、版本 `2.1`。

系统控件默认模板来自：

```text
路径：C:\Windows\System32\Windows.UI.Xaml.Resources.19h1.dll
文件版本：10.0.19041.1
SHA-256：53E387754C28F6D1739A00323BBF1D56DBC95C4C702DB0CD6AB58B283E99685F
```

该 DLL 的 PE resource type `256` 包含 `STYLES.XBF` 和 `THEMERESOURCES.XBF`。

### XBF 反编译器

```text
工具：https://github.com/chausner/XbfTools
核验提交：dbeadcd75f30fb8dea3109039e0082854cb9a89d
```

`xbf2xaml` 成功把上述 40 个 StartUI XBF 及系统 `STYLES.XBF`、`THEMERESOURCES.XBF` 转为可读 XAML，未发生转换失败。XBF、XAML 输出、工具 clone 和 restore 产物只保存在 Windows 临时目录。

### Ghidra

```text
版本：12.1.2 PUBLIC 20260605
官方 ZIP SHA-256：B62E81A0390618466C019C60D8C2F796CED2509C4C1AEA4A37644A77272CF99D
Java：OpenJDK 21.0.9
```

`PDB Universal` 与 `PDB Universal Function Internals` 成功加载匹配 PDB。本文的地址、函数名和调用链均来自该分析对象，不跨 Windows build 外推。

## 2. 结论摘要

当前 TileStart 不能再被描述为已完成 Win10 Motion 还原。原版 StartUI 的核心行为是：

1. Shell 发送全局动画请求。
2. `SplitViewFrame` 把请求切回 UI 线程并按动画类型分派。
3. 应用列表和磁贴区各自创建 Storyboard。
4. Storyboard 遍历可见项目，按项目在视口中的归一化位置计算错峰延迟。
5. 菜单进入、退出和应用启动主要使用 `CompositeTransform3D.TranslateZ`，同时叠加透明度动画。
6. 视图切换使用 `CompositeTransform3D.TranslateY`。
7. 磁贴重排使用系统 `ReorderThemeTransition` 和 `AddDeleteThemeTransition`，拖动命中还经过独立的 120 ms reflow timer。
8. StartUI 的 40 个编译 XAML 已完整提取；导航轨、文件夹 chevron、SemanticZoom 和上下文菜单状态不再依赖外观猜测。
9. 系统关闭动画时仍执行状态更新，但 `StoryboardWrapper` 会把允许跳转的动画立即推进到 fill/end state。

这不是整窗统一 `TranslateY + Opacity` 能等价替代的结构。本轮针对 MVP Motion 的已知研究开放项已经闭环，但这只表示实现参数有证据来源，不表示 TileStart 当前 UI 已经高度还原。

## 3. 全局动画调用链

### 3.1 Shell 请求入口

| 地址 | 符号 |
|---|---|
| `0x1803BF7B0` | `StartUI::SplitViewFrame::OnGlobalAnimationRequested` |
| `0x1801629B8` | UI-thread lambda `operator()` |
| `0x1803C1764` | `SplitViewFrame::PlayGlobalAnimation` |
| `0x1803C14B8` | `SplitViewFrame::PlayContentPaneAnimation` |

已确认控制流：

```text
OnGlobalAnimationRequested
  -> RunOnUIThreadAsync
  -> UI-thread lambda
  -> SplitViewFrame::PlayGlobalAnimation
  -> SplitViewFrame::PlayContentPaneAnimation
  -> AllAppsPane::PlayGlobalAnimation 或 TileGridView::PlayGlobalAnimation
```

`SplitViewFrame::PlayGlobalAnimation` 会先终止仍在运行的旧 Storyboard，再创建新的 `StoryboardWrapper`。在进入动画时还会调用 `ResetViewSelectionWithoutAnimation`。

### 3.2 内容区分派

| 地址 | 符号 |
|---|---|
| `0x18033A1B4` | `AllAppsPane::PlayGlobalAnimation` |
| `0x1802B5944` | `AllAppsGridListView::PlayGlobalAnimation` |
| `0x1803E8A84` | `TileGridView::PlayGlobalAnimation` |
| `0x1802B3EE0` | `AllAppsGridListView::CreateStoryboard` |
| `0x1803E56C0` | `TileGridView::CreateStoryboard` |

应用列表与磁贴区都通过 `StoryboardWrapper` 延迟创建实际 Storyboard。磁贴区会遍历磁贴、文件夹及可见容器；应用列表会遍历当前 `AllAppsCollectionView`，随后把每个容器交给统一的元素动画构造函数。

`AppendGlobalAnimationForElement` 接收的进入类型条件不是逐磁贴属性，而是 Shell 本次全局请求携带的 `requestCode`。调用方统一传入：

```text
requestCode == 13
```

作为 `AppendEntranceAnimationForElement` 的非默认分支 bool。磁贴、文件夹和文件夹子项仍逐元素遍历，但共享同一请求级条件；根元素不走该函数，而只在全局动画类型 `5` 时由 `AppendGlobalAnimationForRoot` 处理。

## 4. 全局动画类型

`AppendGlobalAnimationForElement` 在 `0x1803B4848`。根据其 switch 分支和被调用函数，可以确认以下行为映射：

| 数值 | 分支行为 |
|---:|---|
| `0` | `AppendEntranceAnimationForElement` |
| `1` | `AppendDismissAnimationForElement` |
| `2` | `AppendAppLaunchAnimationForElement` |
| `3` | `AppendViewSwitchAnimationForElement` |
| `5` | `AppendEntrancePageTurnAnimationForElement` |

数值 `4` 在 UI-thread lambda 中只更新任务栏边缘相关状态，没有进入上述元素动画分支。公开 PDB 未恢复该枚举每个值的正式字段名，因此表中记录的是已确认的分支行为，不伪造枚举名称。

## 5. 通用错峰样条构造器

| 地址 | 符号 |
|---|---|
| `0x1804C1F18` | `StartUI::Animations::MakeStaggeredSplineAnimation` |
| `0x1804C18EC` | `StartUI::Animations::MakeStaggeredLinearAnimation` |

`MakeStaggeredSplineAnimation` 接收的结构体字段已由读写偏移确认：

```text
+0x00 float To
+0x04 float From
+0x08 int   DelayMilliseconds
+0x0C int   DurationMilliseconds
+0x10 wstring_view TargetProperty
+0x20 Point ControlPoint1
+0x28 Point ControlPoint2
```

它构造三个关键帧：

```text
t = 0                         -> From（Discrete）
t = DelayMilliseconds         -> From（Discrete）
t = Delay + Duration          -> To（Spline）
```

最后一个关键帧使用给定的两个 KeySpline 控制点。因此 StartUI 的“错峰”不是给 Timeline 设置 BeginTime，而是在每条动画内部保留一段 From 值平台期。

## 6. 菜单进入动画

| 地址 | 符号 |
|---|---|
| `0x1803B2F80` | `AppendEntranceAnimationForElement` |

`p` 为元素在可用高度中的归一化纵向位置，范围约为 `[0, 1]`。此前把函数最后一个 bool 误写成全局“动画启用”开关；重新检查调用点后确认，它是调用方从每个项目的 `ICollectionTile.CuratedTileInfo` 对象 vtable `+0x168` 读取的逐项目条件。公开符号没有恢复该属性的正式名称，因此本文不擅自命名。

该 bool 控制两条**互斥**路径，不是同时叠加 Z 和 Y 动画。

### 6.1 完整深度路径（bool = true）

目标属性：

```text
(UIElement.Transform3D).(CompositeTransform3D.TranslateZ)
UIElement.Opacity
```

```text
translationDelay = max(0, trunc((1 - p) * 133)) ms

类型 0:
  TranslateZ -900 -> 0
  Duration = 493 ms

非 0:
  TranslateZ +900 -> 0
  Duration = 667 ms

TranslateZ spline = cubic-bezier(0.1,0.9,0.2,1)

opacityDelay = translationDelay + max(26, trunc((1 - p) * 57 + 26)) ms
Opacity 0.01 -> 1
Duration = 94 ms
Opacity spline = cubic-bezier(0.33,0,0.67,1)
```

### 6.2 底部任务栏 fallback（bool = false）

false 分支不创建 Z 或 Opacity Storyboard。只有任务栏枚举为 `EUITSP_BOTTOM = 3` 时创建 Y 轴动画：

```text
目标属性：(UIElement.Transform3D).(CompositeTransform3D.TranslateY)
From = p * 110 + 60
To = 0
Delay = 17 ms
Duration = 500 ms
KeySpline = cubic-bezier(0.1,0.9,0.2,1)
```

之后该分支把 `TranslateZ` 直接重置为 `0`、把 `Opacity` 直接设为 `1`。因此当前 Win10 实机观察到的“从下方向上弹出、没有透视展开”对应 fallback 路径，不能用完整深度路径的 Z 数值自行换算 WPF 缩放。

ExplorerPatcher 公开接口确认枚举映射为：

```text
EUITSP_LEFT = 0
EUITSP_TOP = 1
EUITSP_RIGHT = 2
EUITSP_BOTTOM = 3
```

### 6.3 Win10 实机逐帧核验（2026-07-15）

在首个实机环境 `2560×1600 / 150% DPI / 底部任务栏` 录制原版开始菜单，以约 48 fps 的原始帧逐帧检查。录像包含多次一致的打开过程；其中三次可清晰定位为：

```text
38.647 s -> 39.095 s
61.416 s -> 61.902 s
67.988 s -> 68.454 s
```

这些区间是从菜单背景首次变化到元素位移进入亚像素尾段的观测范围，约 `448–486 ms`，与二进制恢复的 `17 ms delay + 500 ms duration` 一致。样条前段移动很快、后段保留较长的低速尾巴，因此不能仅凭肉眼把可见主位移误判成 200 ms 左右。

逐帧画面同时确认：

- 菜单背景直接以最终窗口边界出现，没有整窗缩放或从任务栏展开。
- 背景出现的第一批画面中，应用项和磁贴已经位于最终位置下方；内容在窗口边界内受裁剪并向上归位。
- 导航轨按钮也会向上运动，但各按钮共享同一轨迹。以汉堡按钮、用户、文档和设置按钮交叉跟踪，帧间偏移完全一致，不按按钮自身屏幕 Y 坐标分别放大。
- “最近添加”、`展开`、`#`、应用项和不同高度的磁贴具有不同起始位移，继续按逐元素位置计算。

因此 WPF 实现必须在 `Window.Show()` **之前**给目标元素写入起始 `TranslateY`，让第一个合成帧就是已下移并受裁剪的内容；先显示最终布局、再调用 `BeginAnimation` 会丢失原版动画的起始段，产生“时长虽长但观感过快”的错误。导航轨使用 fallback 的固定最小位置参数 `p = 0`，其他内容继续按元素位置计算。

本次录像和逐帧 PNG 仅作为本机临时研究材料，不进入仓库。

## 7. 菜单退出动画

| 地址 | 符号 |
|---|---|
| `0x1803B2BDC` | `AppendDismissAnimationForElement` |

归一化位置 `p` 控制错峰延迟：

```text
translationDelay = max(0, trunc(p * 117)) ms
opacityDelay = translationDelay + 17 ms
```

### 7.1 Z 轴位移

```text
目标属性：(UIElement.Transform3D).(CompositeTransform3D.TranslateZ)
From = 0
To = -900
Duration = 294 ms
KeySpline = (0.33,0.0) (0.83,0.83)
```

### 7.2 透明度

```text
目标属性：UIElement.Opacity
From = 1.0
To = 0.01
Duration = 94 ms
KeySpline = (0.33,0.0) (0.83,0.83)
```

退出同样是逐元素错峰，并非先统一淡出窗口再 `Hide()`。

## 8. 应用启动动画

| 地址 | 符号 |
|---|---|
| `0x1803B280C` | `AppendAppLaunchAnimationForElement` |

```text
baseDelay = max(0, trunc(p * 133))
非主目标元素额外增加 83 ms
```

Z 轴：

```text
目标属性：(UIElement.Transform3D).(CompositeTransform3D.TranslateZ)
From = 0
To = +900
Duration = 333 ms
KeySpline = (0.5,0.0) (0.6,1.0)
```

透明度：

```text
opacityDelay = baseDelay + max(33, trunc(p * 17 + 33))
From = 1.0
To = 0.01
Duration = 100 ms
KeySpline = (0.17,0.0) (0.83,0.83)
```

这条路径解释了原版点击应用后磁贴/应用项向观察者方向退出的立体感。

## 9. 视图切换动画

| 地址 | 符号 |
|---|---|
| `0x1803B56AC` | `AppendViewSwitchAnimationForElement` |

```text
translationDelay = max(0, trunc(p * 60))
opacityDelay = translationDelay + 17 ms
```

位移：

```text
目标属性：(UIElement.Transform3D).(CompositeTransform3D.TranslateY)
From = 100
To = 0
Duration = 333 ms
KeySpline = (0.1,0.9) (0.2,1.0)
```

透明度：

```text
From = 0.01
To = 1.0
Duration = 94 ms
KeySpline = (0.33,0.0) (0.67,1.0)
```

## 10. 根元素与页面翻转

| 地址 | 符号 |
|---|---|
| `0x1803B4B58` | `AppendGlobalAnimationForRoot` |
| `0x1803B3668` | `AppendEntrancePageTurnAnimationForElement` |

`AppendGlobalAnimationForRoot` 只在全局动画类型 `5` 执行。它确保根元素拥有 `CompositeTransform3D`，并设置：

```text
CenterOfRotationX = RenderSize.Width * 0.5
CenterOfRotationY = RenderSize.Height
```

该函数中没有设置额外的 `PerspectiveDepth` 属性。完整动画参数为：

```text
RotationX:
  From = 25°
  To = 0°
  Delay = 0
  Duration = 25 ms
  KeySpline = cubic-bezier(0,0,0,1)

根内容 Opacity:
  From = 0.01
  To = 1
  Delay = 50 ms
  Duration = 167 ms
  Linear

外层/承载元素 Opacity:
  From = 0.01
  To = 1
  Delay = 0
  Duration = 167 ms
  KeySpline = cubic-bezier(0.33,0,0.67,1)

TranslateX:
  From = -40
  To = 0
  Delay = 0
  Duration = 600 ms
  KeySpline = cubic-bezier(0.1,0.9,0.2,1)

TranslateY:
  From = -20
  To = 0
  Delay = 0
  Duration = 600 ms
  KeySpline = cubic-bezier(0.1,0.9,0.2,1)
```

三组 spline 常量已从映像地址 `0x1805EA610`、`0x1806225D0` 和 `0x180622600` 直接读取。

`StartUI::Animations::UpdateFramePerspective` 位于 `0x1800B2154`，由 `SplitViewFrame::OnSizeChanged` 等 frame size-change 入口调用。它不使用 XAML 默认 `PerspectiveTransform3D.Depth=1000`，而是直接给根元素的 Composition Visual 设置矩阵：

```text
D = 1931.8199462890625

[ 1, 0, 0, 0 ]
[ 0, 1, 0, 0 ]
[ -(ActualWidth/2)/D, -(CompactPaneLength/2)/D, 1, -1/D ]
[ 0, 0, 0, 1 ]
```

因此完整深度路径的透视原点与深度由 frame Composition 矩阵决定；此前使用 `Depth=1000` 和窗口中心作为 WPF 缩放中心没有二进制依据，已否决。

## 11. 磁贴重排与拖动让位

### 11.1 ItemsControl 转场

| 地址 | 符号 |
|---|---|
| `0x18007E4B4` | `TileListView::PopulateItemContainerTransitions` |

该函数创建 `TransitionCollection`，按顺序加入：

1. `ReorderThemeTransition`
2. `AddDeleteThemeTransition`

随后设置为磁贴列表的 `ItemContainerTransitions`。因此 TileStart 后续应优先复现这两种系统转场的行为，而不是只对布局坐标做瞬时更新。

### 11.2 Reflow Timer

| 地址 | 符号 |
|---|---|
| `0x1800B10BC` | `DragDropReflowTimer` 构造函数 |
| `0x1803736A8` | `DragDropReflowTimer::OnDrag` |
| `0x180374DE0` | `DragDropReflowTimer::OnReflowTimerTick` |
| `0x180375504` | `DragDropReflowTimer::Start` |
| `0x1803755E4` | `DragDropReflowTimer::Stop` |

已确认：

```text
DispatcherTimer interval = 1,200,000 × 100 ns = 120 ms
指针移动阈值：dx² + dy² > 9，即距离超过 3 DIP
```

超过阈值时，原版会停止并重新启动 timer；tick 时只有当前位置相对上次已提交位置发生变化，或占位磁贴要求重新渲染，才触发 reflow 事件。

当前 TileStart 的拖动事务虽然会实时更新目标位置，但缺少该 120 ms 稳定窗口、3 DIP 抖动过滤和系统重排转场，因此手感与原版不同。

## 12. LauncherFrame 退出包装器

| 地址 | 符号 |
|---|---|
| `0x1803D16A8` | `CreateExitContentStoryboard` |
| `0x1803D262C` | `CreateExitCortanaContentStoryboard` |
| `0x1803D2AB8` | `CreateExitFrameStoryboard` |

三条 Storyboard 都使用 `150 ms`。已确认目标属性包括：

- `UIElement.Opacity`
- `CompositeTransform.ScaleX`
- `CompositeTransform.ScaleY`
- `CompositeTransform.TranslateX`
- `CompositeTransform.TranslateY`

使用 `QuadraticEase`；Frame 分支显式设置 `EasingMode = 0`。这些函数属于 LauncherFrame 的退出包装层，不应与第 7 节逐元素 Dismiss 动画混成一条整窗动画。

### 12.1 TileStart 的 WPF 实测降级

在当前 Acrylic 大型视觉树上，直接把第 7 节参数翻译成 WPF 逐元素 `Opacity` 动画，每次退出只有 1 个 `CompositionTarget.Rendering` 帧，主渲染停顿约 `213–361 ms`。改为给 `MainSurface` 或 `Window.Opacity` 做单一 WPF 动画仍只有 1 帧，停顿约 `211–404 ms`。这两条方案均已否决，不能把 UWP Composition 的低成本动画等同为 WPF 元素透明度动画。

当前生产降级使用顶层 Win32 `AnimateWindow`：

```text
Duration = 150 ms
Flags = AW_BLEND | AW_HIDE
```

它复用本节已恢复的 LauncherFrame 退出包装层时长，并让系统窗口合成路径完成淡出，不再触发 WPF 对数百个元素的逐帧重绘。Win10 实机已确认淡出流畅、失焦时会先取消置顶、快速反复开关不会残留窗口。逐元素 Dismiss 的 Z/Opacity 参数继续作为未来 Windows Composition interop 的证据保留，不在 WPF 后端强行直译。

快速开关测试还暴露了磁贴拖动的嵌套消息循环重入：`DragDrop.DoDragDrop` 期间另一次鼠标移动可能改写 `_dragTransaction`。生产实现禁止嵌套拖动，并由局部事务对象负责释放，避免外层 `finally` 对已清空字段调用 `Dispose()`。

## 13. 导航轨悬停展开

| 地址 | 符号 |
|---|---|
| `0x18039D28C` | `NavigationPaneView::ExpandWithDelay` |
| `0x18039E740` | `NavigationPaneView::OnExpansionDelayTimerTick` |
| `0x18039E790` | `NavigationPaneView::PointerEntered` |
| `0x18039E800` | `NavigationPaneView::PointerExited` |
| `0x1801253AC` | `NavigationPaneView::UpdateVisualStates` |

`ExpandWithDelay` 首次调用时创建 `DispatcherTimer`，其 interval 为：

```text
5,000,000 × 100 ns = 500 ms
```

`PointerEntered` 先读取 `CoreCursorType`，只有取值满足 `pointerDeviceType - 1 < 2` 的指针类型才启动延迟展开；这对应 `Pen` 和 `Mouse`，触控板通常也按 `Mouse` 上报，而直接触摸不会触发。timer tick 会先取消延迟任务，再把展开目标 bool 属性设为 `true`。

`PointerExited` 使用相同的指针类型过滤，并根据当前模式决定是否立即调用 `Collapse`。因此 TileStart 不能把左栏实现为简单的 `MouseEnter => IsExpanded=true`。

`UpdateVisualStates` 已确认使用以下状态名：

```text
ClosedBackground
OpenBackground
EnableAccentAcrylic
EnableNormalAcrylic
Disable
```

编译 XAML 进一步确认导航轨由外层系统 `SplitView` 管理宽度切换：

```text
DisplayMode = CompactOverlay
CompactPaneLength = 48 DIP
Tablet CompactPaneLength = 56 DIP
OpenPaneLength = 270 DIP（Light / Dark）
HighContrast OpenPaneLength = 256 DIP
NavigationPane MaxWidth = 256 DIP
HamburgerButtonWidth = 48 DIP
```

`NavigationPaneView.xaml` 自身没有宽度动画，只对背景和阴影透明度做 VisualState 动画。

`ClosedBackground -> OpenBackground`：

```text
DropShadow.Opacity: 0 at 0 -> 1 at 350 ms
BackgroundElement.Opacity: 0 at 0 -> 1 at 350 ms
Spline: cubic-bezier(0.1,0.9,0.2,1)
```

`OpenBackground -> ClosedBackground` 的 XBF 原始字符串与 XbfTools 输出都是：

```text
1 at 0
1 at "12"
0 at 240 ms
Spline: cubic-bezier(0.1,0.9,0.2,1)
```

`"12"` 很可能是一个 hold 时间，但当前证据不能把它无解释地写成 `120 ms`；实现前必须用运行时 Timeline 对象值确认其解析语义。

## 14. Windows.UI.Xaml 系统重排转场

### 14.1 分析对象

```text
路径：C:\Windows\System32\Windows.UI.Xaml.dll
文件版本：10.0.19041.4522
大小：17,531,904 bytes
SHA-256：0C20B99C892CDC1E553B677692C6B5ABE552A23A0696800B752EFE4810A95CD0
映像基址：0x180000000
```

匹配公开 PDB：

```text
文件：windows.ui.xaml.pdb
GUID：6DA8743C-C8BC-A601-F71D-7989995A1E23
Age：1
大小：343,011,328 bytes
SHA-256：7A232E99D66FD1C135C4FD717A12BE6753507D4726B8D32E3C0CECB58D800D22
```

Ghidra 的 `PDB Universal` 成功完成，尽管部分不受支持的 PDB 类型产生报告项，目标函数、枚举和调用关系均已恢复。原始 DLL、PDB、分块、探针和 Ghidra 工程仍只保留在 Windows 临时目录。

### 14.2 转场不是在 XAML DLL 中硬编码一套 easing

| 地址 | 符号 |
|---|---|
| `0x1807D07E0` | `DirectUI::ReorderThemeTransition::CreateStoryboardImpl` |
| `0x180150B80` | `DirectUI::AddDeleteThemeTransition::CreateStoryboardImpl` |
| `0x1803466FC` | `AddDeleteRepositionStoryboardCreator` |
| `0x1803467A4` | `AddDeleteRepositionHelperLoad` |
| `0x180346A08` | `AddDeleteRepositionHelperUnload` |
| `0x1807CC618` | `AddDeleteRepositionHelperReparentAndLayout` |
| `0x1801F0768` | `AddTimelines` |

`ReorderThemeTransition` 和 `AddDeleteThemeTransition` 最终共用 `AddDeleteRepositionStoryboardCreator`。该函数再按 Load、Unload、Layout/Reparent 分派到三个 helper。

`AddTimelines` 打开两个 UxTheme class：

```text
animations
timingfunction
```

随后调用：

```text
GetThemeAnimationProperty
GetThemeAnimationTransform
GetThemeTimingFunction
```

它根据返回的 `TA_TRANSFORM_TYPE` 创建 Translate2D、Scale2D、Opacity 或 Clip Timeline。也就是说，系统转场的最终参数来自当前 Windows build 和主题数据；不能只在 `Windows.UI.Xaml.dll` 里搜索一个固定 `Duration` 常量。

### 14.3 当前机器主题数据的精确参数

以下数据由当前系统直接调用 UxTheme API 读取，并与 `vsanimation.h` 的官方枚举对应。类型含义：

```text
0 = TATT_TRANSLATE_2D
1 = TATT_SCALE_2D
2 = TATT_OPACITY

Timing 1 = cubic-bezier(0, 0, 1, 1)
Timing 4 = cubic-bezier(0.1, 0.9, 0.2, 1)
Timing 6 = cubic-bezier(0.11, 0.5, 0.24, 0.96)
```

`TAS_REPOSITION = 3`，target 1：

```text
Translate2D
Start=0 ms
Duration=367 ms
Timing=4
Flags=TATF_TARGETVALUES_USER
StaggerDelay=33 ms
StaggerDelayCap=250 ms
Animation flags=TAPF_HASSTAGGER | TAPF_ALLOWCOLLECTION
```

`TAS_ADDTOGRID = 8`：

| Target | Transform | Start | Duration | Timing | 关键值 |
|---:|---|---:|---:|---:|---|
| 1 Added | Scale2D | 166 ms | 333 ms | 4 | 初值 `0.9`，目标值 `1`，origin `(0.5,0.5)` |
| 1 Added | Opacity | 166 ms | 333 ms | 1 | 初值 `0`，目标值 `1` |
| 2 Affected | Translate2D | 0 | 400 ms | 4 | target delta 由调用方提供 |
| 3 RowOut | Translate2D | 0 | 400 ms | 4 | target delta 由调用方提供 |
| 4 RowIn | Translate2D | 0 | 400 ms | 4 | target delta 由调用方提供 |

这里结构体中的 Scale/Opacity 目标值会被 `ThemeGeneratorHelper` 根据方向和 override flags 解释；表中保留 UxTheme 原始值，避免把“新增”方向错误写反。

`TAS_DELETEFROMGRID = 9`：

| Target | Transform | Start | Duration | Timing | 关键值 |
|---:|---|---:|---:|---:|---|
| 1 Deleted | Scale2D | 0 | 100 ms | 6 | 目标值 `0.9`，origin `(0.5,0.5)`；起点取元素当前值 |
| 1 Deleted | Opacity | 0 | 100 ms | 1 | 目标值 `0`；起点取元素当前值 |
| 2 Remaining | Translate2D | 0 | 333 ms | 4 | target delta 由调用方提供 |
| 3 RowOut | Translate2D | 0 | 400 ms | 4 | target delta 由调用方提供 |
| 4 RowIn | Translate2D | 0 | 400 ms | 4 | target delta 由调用方提供 |

StartUI 当前使用的是 GridView，因此 `TileListView::PopulateItemContainerTransitions` 添加的系统转场会走上述 Grid storyboard，而不是凭感觉套一个统一 `200 ms EaseOut`。

### 14.4 ThemeTransitionContext 完整调度矩阵

本机 `Windows.UI.Xaml.dll` 的反编译控制流、常量和符号已与 Microsoft UI Xaml 官方源码 `LayoutTransition_partial.cpp` 交叉验证：

```text
仓库：https://github.com/microsoft/microsoft-ui-xaml
核验提交：3cae15f071f1ab8565f9a7592dbf27f04bafe651
```

Load 阶段允许 Add/Delete 动画的 context：

```text
SingleAddList / SingleDeleteList
SingleAddGrid / SingleDeleteGrid
MixedOperationsList / MixedOperationsGrid
MultipleAddList / MultipleDeleteList
MultipleAddGrid / MultipleDeleteGrid
```

`scheduleAffectedFirst` 为 `SingleAdd*`、`MultipleAdd*` 和 `MultipleReorder*`；`reorderingContext` 为 `SingleReorder*`、`MultipleReorder*`。

```text
Add target: TAS_ADDTOGRID / TA_ADDTOGRID_ADDED
附加时间:
  scheduleAffectedFirst -> 300 ms
  MixedOperations*      -> 600 ms
  其他                   -> 0 ms

Reorder Load fallback:
  TAS_ADDTOGRID / TA_ADDTOGRID_ADDED
  source = destination = 0
  delay = 0
```

Unload 阶段：

```text
Delete target: TAS_DELETEFROMGRID / TA_DELETEFROMGRID_DELETED
附加时间:
  scheduleAffectedFirst -> 300 ms
  其他                   -> 0 ms
```

Layout/Reparent 阶段按 Add、Delete、Reorder、Mixed 和 List/Grid 分类。直线移动使用：

```text
add/reorder -> TAS_ADDTOGRID / TA_ADDTOGRID_AFFECTED
delete/mixed -> TAS_DELETEFROMGRID / TA_DELETEFROMGRID_REMAINING

附加时间:
  add/reorder 且非 mixed -> 0 ms
  delete/mixed           -> 220 ms
```

单个 Grid 跨列且 portal 可用时，使用 `TAS_ADDTOGRID` 的 `TA_ADDTOGRID_ROWIN` / `ROWOUT` primary/secondary 两条 Storyboard；附加时间仍为 add/reorder `0 ms`，其他 `220 ms`。无法使用 portal 或属于 multiple Grid 时，改用：

```text
TAS_CROSSFADE
TA_CROSSFADE_INCOMING
TA_CROSSFADE_OUTGOING
```

其附加时间同样为 add/reorder `0 ms`，其他 `220 ms`。`ReorderThemeTransition` 只参与 `ReorderedItem`、`SingleReorderList/Grid` 和 `MultipleReorderList/Grid`。

这解释了为什么只复制 UxTheme 的 333/400 ms Translate 并不能完整复现 StartUI 重排：还必须重建 context 选择、等待、portal/crossfade fallback 和多 Storyboard 编排。

## 15. 磁贴按压、悬停与缩放反馈

| 地址 | 符号 |
|---|---|
| `0x180061F60` | `TileViewControl::UpdateTileScaling` |
| `0x180408E10` | `TileViewControl::OnPointerEntered` |
| `0x180408F80` | `TileViewControl::OnPointerExited` |

`UpdateTileScaling` 会查找磁贴的父 `ListViewItemPresenter`，确保其 `RenderTransform` 为 `ScaleTransform`，并把缩放中心设置为元素 `RenderSize` 的一半。缩放不是改 Width/Height，也不是只缩放磁贴内部图标。

`OnPointerEntered` 对直接触摸取值 `0` 不启用 hover；鼠标或笔进入后把 ViewModel 中的 hover bool 设为 `true`，退出时恢复为 `false`。

当前主题的系统按压转场由 UxTheme 实测得到：

```text
TAS_POINTERDOWN = 20, target 1
Scale: current -> 0.975
Origin: (0.5, 0.5)
Duration: 167 ms
Spline: cubic-bezier(0.1, 0.9, 0.2, 1)

TAS_POINTERUP = 21, target 1
Scale: current -> 1.0
Origin: (0.5, 0.5)
Duration: 167 ms
Spline: cubic-bezier(0.1, 0.9, 0.2, 1)
```

两者都带 `TAPF_HASPERSPECTIVE`。`Windows.UI.Xaml.dll` 中的 `PointerDownThemeAnimation` / `PointerUpThemeAnimation` 通过前述 `ThemeGenerator` 路径读取这些值；StartUI 的磁贴容器使用 `ListViewItemPresenter`，不能用瞬时颜色变化代替该按压缩放。

StartUI 自己还根据磁贴 CellSize 计算两组额外 Scale target。反编译常量已直接回读：

| CellSize | 收缩组 `(ScaleX, ScaleY)` | 抬升组 `(ScaleX, ScaleY)` |
|---|---|---|
| `1×1` | `(0.8958, 0.8958)` | `(1.08, 1.08)` |
| `2×2` | `(0.95, 0.95)` | `(1.04, 1.04)` |
| `4×2` | `(0.9755, 0.95)` | `(1.04, 1.04)` |
| `2×4` | `(0.95, 0.9755)` | `(1.04, 1.04)` |
| 其他 | `(0.9755, 0.9755)` | `(1.0, 1.0)` fallback |

按真实 vtable 起点回查后，状态顺序已经恢复：

```text
IRearrangeableViewModel +0x30 get_IsAnyItemRearranging
IRearrangeableViewModel +0x38 get_IsRearrangeable
ITileViewModel          +0xD8 get_IsHolding
ITileViewModel          +0xF0 get_IsInManageMode
ITileViewModel         +0x120 get_IsSelected
```

缩放选择顺序：

1. `IsAnyItemRearranging == true` -> 收缩组。
2. 否则 `IsRearrangeable == true` -> 抬升组。
3. 否则 `IsHolding || IsSelected` -> 抬升组。
4. 否则 `IsInManageMode` -> 收缩组。
5. 否则回到 `(1,1)`。

`TileViewControl` 有两个独立 wrapper：

```text
this + 0x208 -> Scale StoryboardWrapper
this + 0x210 -> Opacity StoryboardWrapper
```

Scale wrapper 包含 ScaleX/ScaleY 两条 `DoubleAnimation`，Opacity wrapper 包含 `UIElement.Opacity` 动画，时长都为 `2,000,000 * 100 ns = 200 ms`。目标属性字符串已直接提取。`UpdateTileScaling` 会读取当前值和正在运行 animation 的 `To`；两者都已等于新目标时不会重启，否则修改 ScaleX/ScaleY 的 `To` 后开始 wrapper。`UpdateTileOpacity` 修改目标透明度后开始另一个 wrapper；placeholder 的部分路径会直接写最终透明度。

### 15.1 系统关闭动画时的降级

`StartProperties::AreAnimationsEnabled` 直接返回 `Windows.UI.ViewManagement.UISettings.AnimationsEnabled`，并把结果传入 `StoryboardWrapper::Begin(target, bool)`。

wrapper 仍会停止旧 Storyboard、创建或复用新 Storyboard、设置 target 并执行 Begin。若动画关闭且 wrapper 没有禁止跳转的 flag，则立即推进到 fill/end state。因此关闭动画不会跳过 Scale、Opacity 或位置状态更新，只会压缩中间插值；完成回调仍由 wrapper 状态管理。实现时必须保留个别 wrapper flag 对自动跳转的影响。

## 16. 文件夹展开与折叠

| 地址 | 符号 |
|---|---|
| `0x18038171C` | `FolderAppSpaceVisual::UpdateExpandedState` |
| `0x18037FD00` | `FolderAppSpaceVisual::OnTileFallAnimationCompleted` |
| `0x1803825C8` | `FolderAppSpaceVisual::UpdateGridSize` |

`UpdateGridSize` 证明以下字段含义：

```text
H = folder app-space visual height（DIP，this+0x19C）
C = column count（this+0x1A8）
R = row count（this+0x1AC）
r = child attached Grid.Row
c = child attached Grid.Column
travel = H + 16 DIP
```

动画目标属性为：

```text
(UIElement.RenderTransform).(CompositeTransform.TranslateY)
```

每个 child 都有独立 `DoubleAnimationUsingKeyFrames`，但这里的行列错峰不是 BeginTime 或 Delay；`Delay=0`，差异写进每个 child 的 `Duration`。

### 16.1 展开

`param_1=true` 时，child 从原位置向下移动一个完整 folder app-space 高度加 16 DIP：

```text
From = 0
To = H + 16
Delay = 0

rowStep = trunc(10 / (R - 1))
columnStep = trunc(5 / (C - 1))

Duration =
    100
    + (R - r - 1) * rowStep
    + (C - c - 1) * columnStep

Spline = cubic-bezier(0.9, 0.1, 1.0, 0.2)
```

因此展开从右下方向左上反向分配时长，整体约从 `100 ms` 起步，而不是所有元素统一持续时间。

### 16.2 折叠

`param_1=false` 时方向相反：

```text
From = H + 16
To = 0
Delay = 0

rowStep = trunc(150 / (R - 1))
columnStep = trunc(150 / (C - 1))

Duration =
    300
    + r * rowStep
    + c * columnStep

Spline = cubic-bezier(0.1, 0.9, 0.2, 1.0)
```

折叠从左上向右下增加持续时间，基础时长为 `300 ms`。源码直接以 `R-1` 和 `C-1` 为除数，说明原版 folder grid 的有效布局先保证行列数量满足该算法，而不是在动画函数里提供单行/单列 fallback。

### 16.3 额外 app-space 元素

除普通 child 外，`this+0x168` 指向的额外元素也单独生成相同 TranslateY 动画。它使用：

```text
pivotRow = trunc(2 * R / 3)
```

展开：

```text
Duration = 100 + (R - pivotRow - 1) * trunc(10 / (R - 1))
```

折叠：

```text
Duration = 300 + pivotRow * trunc(150 / (R - 1))
```

### 16.4 XAML 资源 Storyboard 与完成处理

程序生成逐元素 Storyboard 的同时，还从 `FolderAppSpaceVisual` resources 启动：

```text
展开 -> AnimateDown
折叠 -> AnimateUp
```

资源 Storyboard 动画的是 `FolderChevron`，与程序生成的 child 移动 Storyboard 是两层独立动画。

`AnimateUp`：

```text
TranslateY: 0 at 0 -> -138 at 167 ms
Spline: cubic-bezier(0.1,0.9,0.2,1)
Opacity: 1 at 0
FillBehavior = Stop
```

`AnimateDown`：

```text
TranslateY: -138 at 267 ms -> 0 at 567 ms
Spline: cubic-bezier(0.1,0.9,0.2,1)
Opacity: 0 at 0 -> 1 at 267 ms
```

`UpdateExpandedState` 设置 animation-running flag；`OnTileFallAnimationCompleted` 清除该 flag。如果动画期间收到待处理的磁贴更新，完成回调再调用 `UpdateAppTiles`。因此文件夹内容刷新不能在移动过程中直接重建子项，否则会破坏原版的视觉连续性。

## 17. 字母索引与 SemanticZoom

| 地址 | 符号 |
|---|---|
| `0x18033A9C8` | `AllAppsPane::SetSemanticZoomView` |
| `0x18033C0C0` | `AllAppsPane::ZoomControl_OnViewChangeStarted` |
| `0x180336AB0` | `AllAppsPane::AppsList_HeaderActivate` |
| `0x180336D90` | `AllAppsPane::AppsList_OnKeyDown` |
| `0x180414CA8` | `AllAppsZoomListViewItem::SetSemanticZoomTargetItem` |
| `0x1804D8804` | `AllAppsViewModel::FindGroupByFirstLetter` |
| `0x1804D8E4C` | `AllAppsViewModel::FindItemByFirstLetter` |

原版字母索引不是单独弹出的字符面板，而是 `SemanticZoom` 的 zoomed-out view。已确认默认 dependency property：

```text
ZoomedOutHeaderWidthAndHeight = 48 DIP
ZoomedOutHeaderFontSize = 20 DIP
```

### 17.1 进入与退出索引

应用列表 group header 激活时调用：

```text
SetSemanticZoomView(false, group)
```

即进入 zoomed-out 字母索引，并把当前 group 作为定位目标。

键盘行为：

```text
Escape          : zoomed-out -> zoomed-in
Ctrl + -        : zoomed-in -> zoomed-out，保留当前 focus item
Ctrl + = / +    : zoomed-out -> zoomed-in，保留当前 focus item
```

事件被处理后会显式设置 `Handled=true`。

### 17.2 目标字母的传递

`AllAppsZoomListViewItem` 在以下入口先执行 `SetSemanticZoomTargetItem`，再调用基类处理：

```text
PointerPressed
KeyDown
AutomationPeer.Invoke
```

该函数：

1. 向上查找父 `SemanticZoom`。
2. 通过 `ItemsControlFromItemContainer` 找到所属 ItemsControl。
3. 将当前 container 映射回数据 item。
4. 临时写入 `SemanticZoom.Tag`。

`ZoomControl_OnViewChangeStarted` 读取这个 Tag，把它传给 SemanticZoom view-change event 的目标 item，然后立即清空 Tag。视图切换后会对目标执行 `ScrollIntoView` 和 `FocusItem`；首次进入相关 view 时还会 `UpdateLayout` 并重新获取内部 `ScrollViewer`。

这意味着 TileStart 的字母索引选择必须保持“按下的字母 -> 目标 group -> 返回列表后的滚动与焦点”这条链路，不能只切换页面再粗略设置 ScrollOffset。

### 17.3 直接键入字母

`AppsList_OnKeyDown` 使用当前按键文本执行 type-to-jump：

```text
当前为 zoomed-out : FindGroupByFirstLetter
当前为 zoomed-in  : FindItemByFirstLetter
```

找到结果后依次执行：

```text
ScrollIntoView
FocusItem
递增 type-to-jump telemetry counter
```

因此字母键在两个 view 中语义不同：索引视图按 group 跳转，普通应用列表按具体 app item 跳转。

StartUI 的 `AllAppsPane.xbf` 只声明 zoomed-in/out 两个 view，并为二者显式设置空 `TransitionCollection`，没有 StartUI 自定义的视图切换 Storyboard。系统 `SemanticZoom` 默认模板定义：

```text
ZoomInView:
  FadeOutThemeAnimation ZoomedOutPresenter
  FadeInThemeAnimation  ZoomedInPresenter

ZoomOutView:
  FadeOutThemeAnimation ZoomedInPresenter
  FadeInThemeAnimation  ZoomedOutPresenter
```

内部 `ScrollViewer` 设置 `MinZoomFactor=0.5`、`MaxZoomFactor=1`、`IsZoomInertiaEnabled=false`；两个 Presenter 的 `RenderTransformOrigin` 都是 `(0.5,0.5)` 并使用 `CompositeTransform`。所以索引切换由系统控件管理 `1.0/0.5` manipulation，并通过成对 FadeIn/FadeOut 状态切换 presenter。

zoom-out button 从 Visible 到 Hidden 的 transition `BeginTime=3 s`，随后执行 `FadeOutThemeAnimation`，并在 transition 开始时把 `IsHitTestVisible` 设为 `false`。Fade 动画的最终时长继续从系统 ThemeAnimation/UxTheme 取得，不在 TileStart 中凭感觉发明常量。

### 17.4 共享 manipulated surface 与 jump-list 特殊路径

系统默认模板不是分别移动两个 presenter。`OnApplyTemplate` 为同一个 manipulated element 设置固定 `2×` 缩放，并给 zoomed-in presenter 设置 `0.5×` 补偿；zoomed-out presenter 保持 `1×`：

```text
共享 manipulated surface：Scale = 2
ZoomedInPresenter       ：Scale = 0.5
ZoomedOutPresenter      ：Scale = 1

ScrollViewer zoom factor：zoomed-in 1.0 <-> zoomed-out 0.5
```

可见净比例为：

```text
进入索引：
ZoomedIn  1.0 -> 0.5
ZoomedOut 2.0 -> 1.0

返回列表：
ZoomedOut 1.0 -> 2.0
ZoomedIn  0.5 -> 1.0
```

`ResetViewsAndSnapToActiveView` 将 ScrollViewer manipulation extent 设置为可用显示器宽高的 `6×`：`2×` 用于固定缩放补偿，另外 `3×` 在屏幕中心周围保留 manipulation 空间。manipulated element 被移到该大画布中央，再通过 ScrollViewer offset 和 zoom factor 选择当前视图。它产生的是围绕同一共享中心收拢或展开的运动，不是组标题和字母格各自沿独立路径交换位置。

通用 `ListViewBase::StartViewChangeFromImpl` 会把源容器的完整 `X/Y/Width/Height` 写入 `SemanticZoomLocation.Bounds`。程序化切换随后按 manipulated surface 中心换算目标坐标：

```text
distanceToCenter = destinationCoordinate - surfaceSize / 2
distanceToCenter *= currentZoom / targetZoom
destinationCoordinate = distanceToCenter + surfaceSize / 2
```

但是 StartUI 的字母索引点击属于 jump-list 特殊路径。组标题通过 `SetSemanticZoomView(false, group)` 提供 requesting group 后，`StartViewChangeFromImpl` 使用该 group 查找目标，同时设置 jump-list alignment；该分支明确跳过源容器 bounds 映射。因此不能把“当前组标题中心 -> 对应字母格中心”作为 StartUI 的动画轨迹。

目标 view 的 `MakeVisible` 行为为：

- 进入 zoomed-out view 时，若索引自身可滚动，requesting group 可要求目标字母靠底部；当前全部字母可容纳时不会为了制造轨迹强行滚动。
- 从字母索引返回 grouped zoomed-in view 时，选中 group 使用 `ScrollIntoViewAlignment.Leading`，即先把目标组靠顶部，再完成反向共享缩放。

TileStart 的 WPF 重建因此使用同一公共 Scale/Translate 驱动两个 presenter，并保留 `0.5/1.0` 固定补偿和返回前的目标组顶部对齐。WPF 不需要为 DirectManipulation 额外创建真实 `6×` 可滚动 extent；在可见视口上使用等价的中心仿射变换即可得到相同的端点比例和共享运动方向。

### 17.5 当前 Win10 build 的系统动画数据

当前验证环境为 Windows 10 build 19045。`Windows.UI.Xaml.Resources.19h1.dll` 的系统 `SemanticZoom` 模板只声明 `FadeInThemeAnimation` / `FadeOutThemeAnimation`，实际透明度时间线来自 UxTheme：

```text
OpenThemeData(NULL, "Animations")
OpenThemeData(NULL, "timingfunction")

FadeIn  : TAS_FADEIN  / TA_FADEIN_SHOWN
          0 ms + 167 ms, opacity 0 -> 1, linear (0,0,1,1)

FadeOut : TAS_FADEOUT / TA_FADEOUT_HIDDEN
          0 ms + 167 ms, current opacity -> 0, linear (0,0,1,1)
```

这些值由当前系统的 `GetThemeAnimationProperty`、`GetThemeAnimationTransform` 和 `GetThemeTimingFunction` 直接读取，不是视频估算。

程序化视图切换的缩放与位移则由以下调用完成：

```text
SemanticZoom::ChangeViews
  -> ScrollViewer::BringIntoViewport(..., animate=true)
  -> IDirectManipulationViewport::ZoomToRect(..., TRUE)
```

公开 `ZoomToRect` 接口只提供是否动画的开关，没有 duration/easing 参数；当前微软开源 XAML 实现也没有在这条路径上追加固定 Storyboard。因此 TileStart 可以精确复刻 `1.0 <-> 0.5` 缩放关系、共享中心模型、jump-list 对齐规则和 167 ms presenter Fade，但 DirectManipulation 的缩放/位移时间曲线仍需用原版逐帧样本校准。当前 WPF 实现将该曲线明确保留为近似参数，不把它描述成已从 Win10 19045 精确提取。

当前 Win10 150% DPI 实机确认的 WPF 校准值为 `350 ms`、`cubic-bezier(0.15,0.75,0.25,1)`。切换期间两个 presenter 临时启用 `BitmapCache`，动画完成立即释放，使 WPF 主要在合成阶段处理公共 Scale/Translate 和 Opacity，避免每帧重绘完整应用列表。缓存纹理在非整数比例缩放时产生的采样柔化可能是当前观感更接近原版的原因之一；原版是否另有速度相关 motion-blur shader 尚无证据，不作为已还原事实，也不在当前实现中额外添加模糊效果。

## 18. 上下文菜单 VisualState

普通菜单样式位于 `TileStyles.xaml`：

```text
ContextMenu_MenuFlyoutItem
ContextMenu_ToggleMenuFlyoutItem
ContextMenu_MenuFlyoutSubItem
以及对应 Touch 派生样式
```

布局参数：

```text
ContextMenuFontSize = 12
ContextMenuItemPaddingMouse = 12,7
ContextMenuItemPaddingTouch = 12,11
CustomMenuFlyoutPresenter Padding = 0,4
Legacy presenter Padding = 0,8
```

普通和 Toggle item 都有 `Normal`、`PointerOver`、`Pressed`、`Disabled` 状态。PointerOver/Pressed 通过 Reveal setter 改变视觉，并分别调用 `PointerUpThemeAnimation` / `PointerDownThemeAnimation`；普通 item 的动画 target 为 `LayoutRoot`，Toggle item 为 `AnimationRoot`。

SubItem 的状态为 `Normal`、`PointerOver`、`Pressed`、`SubMenuOpened`、`Disabled`，只切换 Reveal/brush setter，没有独立 PointerDown/Up Storyboard。placeholder 状态为：

```text
NoPlaceholder
CheckPlaceholder
IconPlaceholder
CheckAndIconPlaceholder
```

键盘加速键文本另有 `KeyboardAcceleratorTextCollapsed` / `KeyboardAcceleratorTextVisible`。`BlockedOnCurrentScreenFlyoutControl.xbf` 和 `UninstallFlyoutControl.xbf` 是仅含文本与按钮的确认 flyout，没有独立开关动画。

## 19. 开源实现调研

### 10SM

```text
仓库：https://github.com/bbmaster123/10SM
核验提交：13562d52e63907e97d1ac41080c580c216df14f2
许可证：仓库根目录未发现许可证
```

这是目前最接近“原版 Win10 开始菜单”的公开项目，但它并没有重写 StartUI，也没有微软源码。仓库直接包含 `StartMenuExperienceHost.exe`、`StartUI.dll`、ShellExperiences、System32 和 SystemResources 等旧系统二进制，安装说明要求取得系统文件所有权并替换文件。它实现的是把原版组件移植/复活到早期 Windows 11 build，而不是可供 TileStart 修改的开始菜单源码。

README 记录测试范围为 build 22000.652 与 22610，并说明新 build 可能失效、退出较慢；项目后来建议使用 ExplorerPatcher。该仓库没有源码许可证，而且主要内容是微软二进制，因此只能作为版本与依赖清单线索，不能作为 TileStart 源码底座。

### Startify

```text
仓库：https://github.com/PSGitHubUser1/Startify
核验提交：fc1a84eb6031c88e6fd908ea823f6adcedb4a025
许可证：MIT
```

它是 Windows 11 上的 Win10 布局原型。WPF 外层把整个 700 高窗口从 `Y=700` 移到 `Y=0`，持续 `225 ms`，使用 `ExponentialEase`。README 明确称项目仍是未完成原型。该动画与本次逆出的逐元素 Z 轴错峰结构不一致，不能作为原版参数来源；部分 Shell 监听和 Windows XAML Island 接线可单独评估，但没有必要替换 TileStart 已完成的 Host/Hook 架构。

### AppTiles

```text
仓库：https://github.com/home-gihub/AppTiles
核验提交：ec15b7da3e5d4ecd641421124d9673076f0a7620
许可证：GPL-3.0
```

这是一个很小的 Raylib 磁贴渲染实验：从 XML 读取磁贴定义，在固定 `1920×1080` 窗口中绘制内容。README 当前明确表示没有后续计划。它不扫描 Windows 应用、不接管 Start、不实现 Win10 布局状态机，也不包含 StartUI 逆向结果，仅能说明“磁贴渲染器”类项目有人尝试过。

### Open-Shell

```text
仓库：https://github.com/Open-Shell/Open-Shell-Menu
核验提交：9518c3b6503c2ae64d2aecab7a04ef79b6d904e9
许可证：MIT
```

Open-Shell 的主要价值是成熟的开始按钮接管、菜单生命周期和皮肤系统；其产品目标是经典开始菜单，不是 Win10 StartUI 磁贴界面。适合作为 Shell 工程参考，不适合作为视觉源码底座。

### Cairo Shell

```text
仓库：https://github.com/cairoshell/cairoshell
核验提交：e1ce0b67caaf2e475b48c25f6b4105cad44c7c3d
许可证：Apache-2.0
```

Cairo 是完整替代 Shell，范围大于 TileStart，并且视觉模型不同。可参考应用枚举和 Shell 生命周期，但直接移植会扩大项目范围。

### ExplorerPatcher

```text
仓库：https://github.com/valinet/ExplorerPatcher
核验提交：0a88a6e0ef6b1752fea36e581cffff1097e862b0
许可证：GPL-2.0
```

ExplorerPatcher 曾通过下载并接回旧版系统组件，在部分 Windows 11 build 上复活原版 Win10 Start；这同样不是一份重新实现的 StartUI 源码。可用于研究 Windows build 适配和 Shell Hook，但直接复制实现会引入 GPL 派生作品约束。TileStart 当前已有独立 Hook，默认只做行为和兼容策略参考。

### OhMyTile

```text
仓库：https://github.com/EnumaZannen/OhMyTile
核验提交：c557ded95c93bac8f0cef33495a4f913e4445f21
许可证：仓库根目录未发现许可证文件
```

它展示了自定义磁贴背景、图片切割和原生通知队列动态磁贴思路，但不是开始菜单替代 UI。没有明确许可证时，不复制其源码；仅把功能方向作为外部观察。

## 20. 对 TileStart 的直接影响

后续实现不能再采用“给 MainWindow 加一个 200 ms EaseOut”作为 Win10 Motion 阶段。最低重建顺序应为：

1. 先按当前 Win10 实机使用的 fallback 路径，为可见应用项、磁贴、导航按钮和标题实现逐元素 Y 位移。
2. 完整深度路径必须使用 `UpdateFramePerspective` 的 Composition 矩阵，不能把 TranslateZ 直接等价成元素中心缩放。
3. 实现 `StaggeredSplineAnimation` 等价构造器，并接入 Entrance、Dismiss、AppLaunch、ViewSwitch 状态机。
4. 拖动 reflow 增加 120 ms timer 与 3 DIP 抖动阈值。
5. 重排容器增加与 `ReorderThemeTransition`/`AddDeleteThemeTransition` 对应的位移动画。
6. 最后才处理 LauncherFrame 150 ms 整体退出包装层和页面翻转。

WPF 3D Transform 与 UWP `CompositeTransform3D` 并非一一对应。MVP 先实现实机已观察到的 Y 轴 fallback；完整深度路径若要加入，优先评估 Windows Composition interop 复用同类矩阵，而不是再次用普通 WPF ScaleTransform 猜测视觉结果。

TileStart 的 WPF fallback 实现使用原始 `500 ms`；关键差异不是额外放慢，而是在 `Window.Show()` 前预置逐元素起始位移，使第一个合成帧保留完整运动起点。首次显示前还会在隐藏状态批量装载应用并预创建 Motion transform，避免首帧创建数百个视觉对象造成卡顿。该实现已在 Win10 实机通过 Win/A 双入口逐次对照。

## 21. 研究阶段结论与实现边界

本轮针对 MVP Motion 的已知开放项已经闭环：请求级入口条件、四边任务栏枚举、页面翻转、系统重排矩阵、导航轨编译 XAML、SemanticZoom、上下文菜单、TileViewControl 状态与 wrapper、文件夹资源 Storyboard，以及关闭动画降级都已有源码、二进制或编译资源证据。

这不代表所有 StartUI 私有行为已 100% 恢复，也不代表 TileStart 当前 UI 已高度还原。下一阶段应进入自主实现与原版对照测试；每项实现仍需用 Win10 实机样本验证视觉结果和状态切换。

保留两个实现期核验点：

- 导航轨关闭 transition 的 XBF 原始时间字符串为 `"12"`，在写死为具体毫秒值前必须读取运行时 Timeline 对象确认解析语义。
- SemanticZoom FadeIn/FadeOut 的实际时长继续沿用当前 build 的系统 ThemeAnimation/UxTheme 数据，不自行发明常量。

生产实现继续遵守独立重写边界：不提交微软 DLL/PDB/PRI/XBF、反编译数据库、反编译伪代码或逆向工具二进制，只提交 TileStart 自主源码、版本与哈希、可复核结论和行为测试。
