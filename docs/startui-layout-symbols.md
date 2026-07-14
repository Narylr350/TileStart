# StartUI 布局符号与反编译证据

> 本文只保存可复核的结论、符号地址和边界判断。原始 DLL、PDB、Ghidra 工程、反编译文本和工具二进制均保留在 Windows 临时目录，不进入仓库。

## 1. 分析对象

### StartUI.dll

```text
路径：C:\Windows\SystemApps\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\StartUI.dll
文件版本：10.0.19041.6456 (WinBuild.160101.0800)
文件大小：8,489,472 bytes
SHA-256：C6AF5A4E4B38F7DB883B6BB93A63A051F4A3EC46F71F763457CAD77E4D570F86
```

### 匹配的公开 PDB

```text
文件：StartUI.pdb
GUID：7B21D1C1-9038-D36F-DA43-F1BBFF31EC03
Age：1
Symbol key：7B21D1C19038D36FDA43F1BBFF31EC031
文件大小：39,399,424 bytes
SHA-256：BA49EC7111A7E85E755DE22231B37505CDC41604C3F255D9AF8BA47D6DA4F404
```

Ghidra 12.1.2 的 `PDB Universal` 和 `PDB Function Internals` 分析器成功载入该 PDB。选定布局相关关键词后导出约 2,087 个匹配函数，说明该 PDB 足以恢复大量类名、方法名、源码文件名和调用关系，但不能恢复完整源码、可靠局部变量名或所有虚调用目标。

### 分析工具

```text
Ghidra：12.1.2 PUBLIC 20260605
官方 ZIP SHA-256：B62E81A0390618466C019C60D8C2F796CED2509C4C1AEA4A37644A77272CF99D
Java：OpenJDK 21.0.9
```

## 2. 已确认的磁贴几何

原版导出的 `reference/win10-start/native-layout.xml` 明确给出：

- `StartTileGroupCellWidth="8"`
- `GroupCellWidth="8"`
- 四种布局跨度：`1x1`、`2x2`、`4x2`、`4x4`

结合 150% DPI 原版截图的重复测量，TileStart 的自主实现采用：

| 项目 | DIP |
|---|---:|
| 基础单元 | 48 |
| 单元间隙 | 4 |
| 网格步长 | 52 |
| 小磁贴 | 48×48 |
| 中磁贴 | 100×100 |
| 宽磁贴 | 204×100 |
| 大磁贴 | 204×204 |
| 8 单元分组内容宽度 | 412 |

这些几何值由原生布局 XML 与实机像素测量共同支持；当前反编译没有提供比该证据更直接的尺寸常量。

## 3. TileMetrics

### 关键符号

| 地址 | 符号 |
|---|---|
| `0x1800B3D44` | `StartUI::TileMetrics::TileMetrics` |
| `0x180481DCC` | `StartUI::TileMetrics::GetGridMetrics` |
| `0x180481E70` | `GetMetroCountFontSizeForTileSize` |
| `0x180481FD0` | `GetMetroCountPixelSizeForTileSize` |
| `0x180482200` | `ReservedBrandingSpace::get` |

### 已确认行为

- `TileMetrics` 持有 `GridMetrics` 的弱引用，避免形成所有权环。
- 构造函数初始化三项 `16.0f` 标量、两个 `Thickness` 值和两个枚举值：
  - `Thickness(0, 0, 0, 5)`
  - `Thickness(0, 0, 0, 1)`
  - 枚举值 `1` 与 `2`
- PDB 把相关属性恢复为 `TileLogoHeight`、`TileLogoWidth`、自定义图片对应项、`TileLogoMargin` 和两种 `TileLogoStretch`。由于调用经过 CFG 间接分派，当前反编译不能把每一次构造赋值与具体属性一一可靠绑定，因此实现时不把该映射写成已确认事实。
- `ReservedBrandingSpace` 首次读取 XAML 资源键 `TileReservedBrandingSpace`，把资源中的 `double` 转换为整数并缓存；它不是固定在 `StartUI.dll` 中的常量。

### 通知数字尺寸

`GetMetroCountFontSizeForTileSize` 区分 `1×1` 与其他尺寸，并根据一个值为 `125` 的网格/缩放状态选择字号：

| 条件 | 普通状态 | 值为 125 的状态 |
|---|---:|---:|
| `1×1` | 16 | 20 |
| 其他尺寸 | 28 | 36 |

`GetMetroCountPixelSizeForTileSize` 返回的候选像素尺寸为：

| 条件 | 普通状态 | 值为 125 的状态 |
|---|---|---|
| 与内部小尺寸常量相等 | `24×36` | `32×48` |
| 其他尺寸 | `32×48` | `40×60` |

内部小尺寸常量 `DAT_1805E4A70` 的语义尚未通过数据类型或调用方确认，因此以上只记录分支和返回值，不把它直接命名为某一种 Win10 磁贴尺寸。

## 4. GridMetrics 与 FrameMetrics

### 关键符号

| 地址 | 符号 |
|---|---|
| `0x1800B3B94` | `StartUI::GridMetrics::GridMetrics` |
| `0x1800B4018` | `StartUI::FrameMetrics::FrameMetrics` |
| `0x180067600` | `FrameMetrics::GetGridSidePadding` |
| `0x180067750` | `FrameMetrics::UpdateMetrics` |
| `0x180123BF0` | `FrameMetrics::GetSplitViewFrameWidthConstraints` |
| `0x1804817A0` | `FrameMetrics::GetTotalFullScreenTopMargin` |

### 已确认行为

- `GridMetrics` 创建独立的 `GridLayoutMetrics` 对象，并把它保存为 `LayoutMetrics`。
- `GridMetrics` 初始化一个宽度为 `16.0f` 的 `Windows::Foundation::Size`；高度来自 `GridLayoutMetrics` 的虚方法。当前无法可靠命名该尺寸属性。
- `FrameMetrics::UpdateMetrics` 只在可用宽度字段非零时工作：
  1. 解析 `GridMetrics` 弱引用。
  2. 调用 `GridLayoutMetrics::OnAvailableWidthChanged(availableWidth)`。
  3. 通过 `Events::Fire` 发送事件编号 `0x18`。
- `FrameMetrics::GetGridSidePadding` 不保存独立常量，而是转发到 `GridMetrics`/`GridLayoutMetrics` 的属性。
- `GetTotalFullScreenTopMargin` 的已确认公式为：

```text
max(firstMetric × 0.05,
    secondMetric × 0.45 - thirdMetric × 0.5)
```

三个虚调用返回值的确切属性名尚未确认。

### 分栏宽度约束

`GetSplitViewFrameWidthConstraints` 的重要边界：

- 步长为零时抛出 `0x80070057`（无效参数）。
- 一种布局状态直接返回 `[0, +∞)`。
- 布局状态值 `3` 返回固定宽度，公式包含 `base + 576.0`。
- 其他状态根据左右区域宽度、最大可用宽度和列步长计算范围。
- 列吸附采用 `floor((available - fixedParts) / step)`，且列数不小于零。

结构字段与布局状态枚举名尚未恢复，TileStart 不直接复制该函数；后续窗口尺寸实现只采用“固定区域 + 整列步长吸附”的已确认模型，并用实机行为校准字段含义。

## 5. 布局解析器边界

### 关键符号

| 地址 | 符号 |
|---|---|
| `0x18006FD38` | `StartViewModel::PrepareGroupsLayoutResolverForNewLayout` |
| `0x18008145C` | `StartViewModel::AddGroupToGroupsLayoutResolver` |
| `0x1800D9400` | `StartViewModel::CreateAndPopulateGroupsLayoutResolver` |
| `0x180070388` | `TileGroupViewModel::InitializeLayoutResolver` |
| `0x18012DA20` | `ItemLayoutResolverWrapper::GetLayoutBounds` |
| `0x18012FF50` | `ItemLayoutResolverWrapper::AddItem` |
| `0x180130770` | `ItemLayoutResolverWrapper::GetMaxCellBounds` |
| `0x1801319A0` | `ItemLayoutResolverWrapper::SetMaxCellBounds` |
| `0x180131DF0` | `ItemLayoutResolverWrapper::GetLastOccupiedCellInColumn` |
| `0x1801320B0` | `ItemLayoutResolverWrapper::GetItemBounds` |
| `0x1801320D0` | `ItemLayoutResolverWrapper::GetItemByCell` |
| `0x1801320F0` | `ItemLayoutResolverWrapper::AddContainer` |

### 已确认的委托关系

`StartUI.dll` 中没有恢复出一个独立实现占位算法的 `StartUI::LayoutResolver` 类。调用关系显示：

1. `StartViewModel` 和 `TileGroupViewModel` 调用 `GetLayoutFactoryInstance()`。
2. 工厂创建 `IItemLayoutResolver`。
3. StartUI 用 `ItemLayoutResolverWrapper` 包装该 COM 接口。
4. StartUI 通过 `LayoutResolverCallbackProxy` 接收 `ItemBoundsUpdated`、移除和重排回调。
5. `ItemLayoutResolverWrapper` 的大部分方法只把参数原样转发给内部接口。

这说明磁贴/分组占位、碰撞和未提交变更算法的主体位于工厂返回的私有布局组件中，而不是当前 `StartUI.dll` 的包装层。当前证据不足以断言该组件具体位于哪个 DLL。

### 已确认的初始化方式

- 分组级 resolver 和组内磁贴 resolver 都通过同一工厂创建。
- 两者都把最大纵向单元边界设置为 `INT_MAX`（`0x7fffffff`）。
- 另一维度来自 `VisualTileInfo`：值为零时回退为 `1`，否则使用原值。
- resolver 创建后注册 `LayoutResolverCallbackProxy`。
- `AddGroupToGroupsLayoutResolver` 读取组的持久化 `Location`，尝试按首选位置加入；首选位置无效时记录 `0x80070057`，随后走不带首选位置的加入路径。
- 每个组作为 container 加入外层 resolver；container 的内部 resolver 通过 `AsQueriedInspectable<IItemLayoutResolver>` 传入。
- `ItemLayoutResolverWrapper::AddItem` 明确把 GUID 与 `RECT(left, top, right, bottom)` 转发给内部 resolver。

### 包装器暴露的能力

PDB 恢复出的接口包含：

- `AddNewItem` / `AddNewContainer`
- `InsertItemUncommitted` / `InsertContainerUncommitted`
- `MoveItemUncommitted`
- `ResizeItemUncommitted`
- `RemoveItemUncommitted`
- `SwapItemsUncommitted`
- `RepairLayoutUncommitted`
- `CommitChanges` / `AbandonChanges`
- `Collapse` / `Expand`
- `GetItemByCell`
- `GetItemBounds`
- `GetLastOccupiedCellInColumn`
- `GetLayoutBoundsWithoutItem`
- `GetGutterHitTarget`

该接口形状证明原版使用“暂存变更 → 回调更新边界 → 提交/放弃”的布局事务模型，能够支撑拖动时实时让位，而不是仅在鼠标释放后重新排序。

## 6. 对 TileStart 实现的约束

1. `Win10TileMetrics` 使用原生 XML 与截图已确认的 48/4/52 DIP 几何，不从当前物理分辨率反推硬编码像素。
2. `Win10GroupLayout` 使用 8 单元宽的组内占位图，并保存逻辑单元坐标。
3. 分组外层布局与组内磁贴布局分开：外层负责组宽、组高和自动换行，内层负责尺寸、碰撞和空位查找。
4. 拖动实现采用事务式 API：预览移动不立即写入配置，释放时提交，取消或失焦时放弃。
5. 持久化位置无效时不得破坏布局；应按稳定顺序寻找下一个可用位置。
6. 当前不依赖或调用私有 `IItemLayoutResolver`，所有算法在 TileStart C# 源码中独立实现。

## 7. 尚未确认

- `GridLayoutMetrics` 各属性与 `GridMetrics` 构造函数中间接虚调用的精确对应关系。
- `VisualTileInfo` 中决定 resolver 最大横向边界的具体属性名及不同场景值。
- 私有布局工厂和 `IItemLayoutResolver` 的实际实现模块。
- 原版组间水平/垂直间距、标题高度和换行阈值的精确 DIP。
- 拖动让位的扫描方向、同距离候选位置优先级、动画时长和缓动曲线。
- 窗口宽度布局状态枚举与 `SplitViewFrameLayoutInfo` 各字段的完整映射。

这些问题必须继续通过调用方反编译、运行时 Visual Tree、原版录像或行为测试确认，不能从当前符号名直接推测。
