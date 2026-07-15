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

## 13. 开源实现调研

### Startify

```text
仓库：https://github.com/PSGitHubUser1/Startify
核验提交：fc1a84eb6031c88e6fd908ea823f6adcedb4a025
许可证：MIT
```

它是 Windows 11 上的 Win10 布局原型。WPF 外层把整个 700 高窗口从 `Y=700` 移到 `Y=0`，持续 `225 ms`，使用 `ExponentialEase`。README 明确称项目仍是未完成原型。该动画与本次逆出的逐元素 Z 轴错峰结构不一致，不能作为原版参数来源；部分 Shell 监听和 Windows XAML Island 接线可单独评估，但没有必要替换 TileStart 已完成的 Host/Hook 架构。

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

可用于研究 Windows build 适配和 Shell Hook，但直接复制实现会引入 GPL 派生作品约束。TileStart 当前已有独立 Hook，默认只做行为和兼容策略参考。

### OhMyTile

```text
仓库：https://github.com/EnumaZannen/OhMyTile
核验提交：c557ded95c93bac8f0cef33495a4f913e4445f21
许可证：仓库根目录未发现许可证文件
```

它展示了自定义磁贴背景、图片切割和原生通知队列动态磁贴思路，但不是开始菜单替代 UI。没有明确许可证时，不复制其源码；仅把功能方向作为外部观察。

## 14. 对 TileStart 的直接影响

后续实现不能再采用“给 MainWindow 加一个 200 ms EaseOut”作为 Win10 Motion 阶段。最低重建顺序应为：

1. 为可见应用项和磁贴建立独立 Motion visual，支持 Z/Y 位移和透明度。
2. 实现 `StaggeredSplineAnimation` 等价构造器，参数直接来自本文，不散落魔法数。
3. 接入 Entrance、Dismiss、AppLaunch、ViewSwitch 四种状态机。
4. 拖动 reflow 增加 120 ms timer 与 3 DIP 抖动阈值。
5. 重排容器增加与 `ReorderThemeTransition`/`AddDeleteThemeTransition` 对应的位移动画。
6. 最后才处理 LauncherFrame 150 ms 整体退出包装层和页面翻转。

WPF 3D Transform 与 UWP `CompositeTransform3D` 并非一一对应。实现前应先制作最小 Motion prototype，验证 WPF 透视投影、Z 位移和逐元素动画的渲染成本；如果 WPF 无法稳定复现，再评估 Windows Composition interop，而不是先整体迁移技术栈。

## 15. 仍需继续逆向

- `TileGridView::CreateStoryboard` 中磁贴、文件夹和根元素分别传入哪一种 `EntranceAnimationType`。
- `EDGEUI_TRAYSTUCKPLACE` 数值与四边任务栏的准确映射。
- 页面翻转分支的 `CompositeTransform3D` 旋转中心、透视深度和角度。
- 文件夹展开动画中每类项目的具体起止值和 delay 公式。
- `ReorderThemeTransition` 在当前系统 XAML 实现中的默认持续时间与 easing。
- 导航轨展开、字母索引、上下文菜单和按压反馈的独立 VisualState/Storyboard。
- 系统“关闭动画效果”时各分支的精确降级行为。

上述开放项未完成前，不宣称 Win10 动画已经高度还原。
