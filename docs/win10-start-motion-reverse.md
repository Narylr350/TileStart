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

这不是整窗统一 `TranslateY + Opacity` 能等价替代的结构。

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

对启用全局动画的普通元素，`p` 为元素在可用高度中的归一化纵向位置，范围约为 `[0, 1]`。

### 6.1 Z 轴位移

目标属性已从二进制字符串指针确认：

```text
(UIElement.Transform3D).(CompositeTransform3D.TranslateZ)
```

错峰延迟：

```text
translationDelay = max(0, trunc((1 - p) * 133)) ms
```

两种进入方向：

| 类型分支 | From | To | Duration | KeySpline |
|---:|---:|---:|---:|---|
| `0` | `-900` | `0` | `493 ms` | `(0.1,0.9) (0.2,1.0)` |
| 非 `0` | `+900` | `0` | `667 ms` | `(0.1,0.9) (0.2,1.0)` |

这说明原版的主体进入效果是从 Z 轴深处/近处运动到平面，而不是把整个开始菜单从任务栏下方推上来。

### 6.2 透明度

目标属性：

```text
UIElement.Opacity
```

参数：

```text
opacityDelay = translationDelay + max(26, trunc((1 - p) * 57 + 26)) ms
From = 0.01
To = 1.0
Duration = 94 ms
KeySpline = (0.33,0.0) (0.67,1.0)
```

每个项目的透明度动画晚于其 Z 轴动画开始，而且项目纵向位置不同会产生不同延迟。

### 6.3 特定任务栏边缘的补充分支

函数中还存在一个只在 `EDGEUI_TRAYSTUCKPLACE == 3` 时执行的 Y 轴动画：

```text
目标属性：(UIElement.Transform3D).(CompositeTransform3D.TranslateY)
From = p * 110 + 60
To = 0
Delay = 17 ms
Duration = 500 ms
KeySpline = (0.1,0.9) (0.2,1.0)
```

该分支与“动画启用”参数的另一条路径相关。任务栏边缘枚举的准确语义仍需结合调用方参数和四边原版录像确认，当前不把数值 `3` 直接写成某个边缘名称。

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

`AppendGlobalAnimationForRoot` 在页面翻转类型中确保根元素拥有 `CompositeTransform3D`，并使用根元素尺寸、透视和 `TranslateX/TranslateY` 构造额外动画。当前已确认其中一组参数：

```text
TranslateX: From = -40, To = 0, Duration = 600 ms
TranslateY: From = -20, To = 0, Duration = 600 ms
KeySpline = (0.1,0.9) (0.2,1.0)
```

页面翻转还包含 3D 旋转/透视设置，当前反编译结果中的间接 WinRT 属性调用尚未全部可靠命名，后续不能只依据调用顺序猜字段。

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

公开 PDB 只能恢复状态选择逻辑；实际宽度、位移和背景 Storyboard 位于编译 XAML 资源中，当前还没有提取，所以本节只确认 500 ms 行为门槛和状态机入口，不伪造展开宽度动画参数。

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

### 14.4 StartUI 对系统参数的附加调度

`AddDeleteRepositionHelperLoad` 明确调用 storyboard `8`，`AddDeleteRepositionHelperUnload` 调用 storyboard `9`。helper 会根据 `ThemeTransitionContext` 选择 target，并在系统主题时序上再加额外等待：

```text
普通路径：0 ms
部分 reorder context：300 ms
MixedOperationsList / MixedOperationsGrid：600 ms
```

Layout/Reparent helper 还会为不同 target 构造独立 Storyboard，并在特定分支增加 `220 ms` 的额外 opacity/drag-source 衔接。由于公开 PDB 恢复出的 context 枚举和局部变量仍有别名混淆，本轮只记录已经由分支常量和调用参数确认的 `0/300/600/220 ms`，暂不把每个 context 与每个 target 的完整矩阵写成定论。

这解释了为什么只复制 UxTheme 的 333/400 ms Translate 并不能完整复现 StartUI 重排：还必须重建 StartUI 自己的 context 选择、等待和多 Storyboard 编排。

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

这两组值与 `IRearrangeableViewModel` 状态、两个独立 `StoryboardWrapper` 和当前正在运行的 animation target 联动。进一步按 WinRT ABI vtable slot 回查后，`UpdateTileScaling` 在 `+0x30` 调用的是 `get_IsAnyItemRearranging`；返回 `true` 时进入上述收缩组。`+0x38` 对应 `get_IsRearrangeable`。抬升组还受 TileViewModel 其他属性控制，相关大 vtable 虚调用尚未恢复出可靠名称，因此暂不擅自命名其具体状态。

## 16. 开源实现调研

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

## 17. 对 TileStart 的直接影响

后续实现不能再采用“给 MainWindow 加一个 200 ms EaseOut”作为 Win10 Motion 阶段。最低重建顺序应为：

1. 为可见应用项和磁贴建立独立 Motion visual，支持 Z/Y 位移和透明度。
2. 实现 `StaggeredSplineAnimation` 等价构造器，参数直接来自本文，不散落魔法数。
3. 接入 Entrance、Dismiss、AppLaunch、ViewSwitch 四种状态机。
4. 拖动 reflow 增加 120 ms timer 与 3 DIP 抖动阈值。
5. 重排容器增加与 `ReorderThemeTransition`/`AddDeleteThemeTransition` 对应的位移动画。
6. 最后才处理 LauncherFrame 150 ms 整体退出包装层和页面翻转。

WPF 3D Transform 与 UWP `CompositeTransform3D` 并非一一对应。实现前应先制作最小 Motion prototype，验证 WPF 透视投影、Z 位移和逐元素动画的渲染成本；如果 WPF 无法稳定复现，再评估 Windows Composition interop，而不是先整体迁移技术栈。

## 18. 仍需继续逆向

- `TileGridView::CreateStoryboard` 中磁贴、文件夹和根元素分别传入哪一种 `EntranceAnimationType`。
- `EDGEUI_TRAYSTUCKPLACE` 数值与四边任务栏的准确映射。
- 页面翻转分支的 `CompositeTransform3D` 旋转中心、透视深度和角度。
- 文件夹展开动画中每类项目的具体起止值和 delay 公式。
- `ReorderThemeTransition` 的完整 `ThemeTransitionContext -> target -> additional delay` 分支矩阵。
- 导航轨编译 XAML 中的实际宽度 Storyboard、字母索引和上下文菜单的独立 VisualState/Storyboard。
- `TileViewControl` 抬升 Scale target 对应的 TileViewModel 属性，以及两个 `StoryboardWrapper` 的完整时序。
- 系统“关闭动画效果”时各分支的精确降级行为。

上述开放项未完成前，不宣称 Win10 动画已经高度还原。
