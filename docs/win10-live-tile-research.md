# Win10 动态磁贴复刻研究

## 研究结论

Win10 动态磁贴不是单纯在静态磁贴上播放动画，而是一套由以下部分组成的内容系统：

1. 按磁贴尺寸提供不同内容绑定；
2. 使用文本、图片、分组和品牌信息组成内容状态；
3. 通过即时更新、计划更新、周期更新或通知队列切换内容；
4. 在磁贴被点击时，允许把当前内容对应的参数传回应用；
5. 对部分模板使用系统内置的特殊动画，例如照片模板的缩放和交叉淡化。

TileStart 应优先实现**独立的 Win10 风格动态磁贴引擎**，不读取 Windows 原生 Live Tile 数据。这样可以复刻视觉和交互，同时保持当前项目“不读取 Windows 原生 Live Tile 数据”的 Non-goal，不引入 UWP 应用身份、系统通知权限和原生磁贴存储兼容问题。

## 官方机制事实

### 自适应内容模型

Windows 10 动态磁贴的通知内容使用 XML 描述。一个视觉内容包含多个按尺寸区分的 `binding`，例如 Medium、Wide 和 Large；每个 binding 可以使用不同的文本、图片、分组和品牌设置。Adaptive Tile 模板面向不同屏幕密度和尺寸适配，而不是让应用自己为每个 DPI 写死一套像素布局。

参考：

- Microsoft Learn：[Create adaptive tiles](https://learn.microsoft.com/en-us/windows/uwp/launch-resume/create-adaptive-tiles)
- Microsoft Learn：[Adaptive tile schema and templates](https://learn.microsoft.com/en-us/windows/apps/design/shell/tiles-and-notifications/adaptive-tiles-schema)

### 更新、计划和周期

Windows 10 通过 `TileUpdater` 更新指定的主磁贴或次级磁贴。官方接口包括：

- 立即 `Update`；
- `AddToSchedule` 计划更新；
- `StartPeriodicUpdate` 周期更新；
- 清除内容；
- 查询和移除计划更新；
- 启用通知队列。

通知队列最多自动轮播五条内容，通常按 FIFO 工作；相同 Tag 的新内容可以替换队列中原有的同 Tag 内容。这个规则适合映射为 TileStart 的“有限内容帧队列”，而不是无限制地不断创建动画对象。

参考：

- Microsoft Learn：[TileUpdater](https://learn.microsoft.com/en-us/uwp/api/Windows.UI.Notifications.TileUpdater)
- Microsoft Learn：[EnableNotificationQueue](https://learn.microsoft.com/en-us/uwp/api/windows.ui.notifications.tileupdater.enablenotificationqueue)
- Microsoft Learn：[Send a local tile notification](https://learn.microsoft.com/en-us/windows/uwp/launch-resume/sending-a-local-tile-notification)

### 特殊模板和照片轮播

Windows 10 提供部分特殊模板，不完全等同于 Adaptive Tile。照片模板可以在磁贴中展示最多 12 张图片，并使用缩放与交叉淡化动画循环切换；People 模板则使用图片集合进行移动展示。

这说明 TileStart 第一阶段最值得复刻的是：

- 照片轮播；
- 图片与文字组合；
- 受控的缩放、淡入淡出；
- 不同尺寸使用不同内容密度。

参考：

- Microsoft Learn：[Special tile templates](https://learn.microsoft.com/en-us/windows/uwp/launch-resume/special-tile-templates-catalog)

### 主磁贴与次级磁贴

原生 Live Tile 区分主磁贴和次级磁贴。次级磁贴可以代表应用内的具体位置或功能入口，更新目标与应用身份绑定。TileStart 当前的磁贴是独立的文件、应用、文件夹或命令入口，不应直接照搬原生主/次级磁贴的身份模型。

### 平台边界

Microsoft 文档明确说明 Live Tiles 是 Windows 10 能力，后续 Windows 版本不再支持该原生能力。TileStart 的目标是复刻 Win10 体验，因此应把“Win10 动态磁贴视觉复刻”和“读取后续 Windows 原生 Live Tile 数据”分开处理。

## 当前 TileStart 的差距

当前 `TileItem` 已经保存：

- 名称；
- 启动目标；
- 图标和背景；
- 图标位置与大小；
- 磁贴尺寸；
- 文件夹子磁贴。

但还没有动态内容模型：

- 没有内容帧列表；
- 没有动态文本/图片状态；
- 没有更新时间或过期时间；
- 没有通知队列；
- 没有周期计划；
- 没有可暂停的内容更新调度器；
- 没有按 TileSize 选择内容绑定的渲染层。

现有 `Win10ReorderMotion`、`Win10FolderMotion` 等主要负责交互和布局动画，不应直接承担动态内容更新职责。

## 推荐实现边界

### 第一阶段：独立动态磁贴 MVP

不连接 Windows 原生 Live Tile API，只实现 TileStart 自己的本地内容定义：

```text
TileItem
└── LiveContent
    ├── Enabled
    ├── Frames[]
    │   ├── Text
    │   ├── ImagePath
    │   ├── BackgroundColor
    │   ├── Duration
    │   └── Tag
    ├── QueueMode
    ├── CurrentFrame
    └── ExpireAt
```

首批支持：

1. 静态磁贴作为无动态内容的 fallback；
2. 2～5 个本地图片/文字帧；
3. 按磁贴尺寸选择不同内容密度；
4. 图片缩放和交叉淡化；
5. 磁贴不可见时暂停或降低更新频率；
6. 更新失败时保留上一次有效帧；
7. 用户关闭动态内容后立即回退到静态磁贴。

### 第二阶段：内容调度器

将内容更新从 WPF 视觉树中分离出来：

- `LiveTileRuntime`：管理帧、队列和过期状态；
- `LiveTileScheduler`：管理周期和可见性；
- `LiveTileRenderer`：只负责把当前状态渲染到 TileItem 视觉层；
- `LiveTileStore`：保存 JSON 定义和资源路径。

必须保持的时序约束：

- 先更新状态，再通知 UI；
- 不在 UI 线程读取图片文件；
- 不为每次轮播创建永久计时器或 Storyboard；
- 窗口隐藏时停止高频轮播；
- 关闭磁贴或删除磁贴时释放调度资源。

### 第三阶段：可选的原生兼容研究

只有用户明确需要兼容原生 Live Tile 数据时，才研究：

- UWP/MSIX 应用身份；
- `TileUpdater` 和通知 XML；
- 系统磁贴缓存；
- 主/次级磁贴映射；
- 原生队列和计划更新的读取边界。

这会改变当前 Non-goal，必须先更新项目基线后再实现，不应作为第一阶段的隐式扩展。

## 验证计划

### 视觉验证

- 1×1、2×2、4×2、4×4 磁贴尺寸；
- 100%、150% DPI；
- Win10 19045 原生开始菜单对照；
- 图片轮播的缩放、交叉淡化和裁切；
- 长文本、空图片、损坏图片和缺失资源 fallback。

### 生命周期验证

- 打开开始菜单时开始或恢复轮播；
- 隐藏开始菜单时暂停高频轮播；
- 切换组、打开文件夹、拖动磁贴时不产生额外更新线程；
- 删除磁贴后没有残留计时器；
- 重启后恢复当前帧或按定义重新开始；
- 配置损坏时回退到静态磁贴，不破坏整个布局。

### 性能验证

- 核显和低性能 CPU 环境；
- 只有一列和多列磁贴区；
- 多个动态磁贴同时存在；
- 拖动期间不因动态内容刷新产生明显延迟；
- 图片缓存和内存上限。

## 当前下一步

先实现一个不依赖系统通知的本地 JSON 动态磁贴原型，只覆盖图片/文字帧、有限队列、缩放交叉淡化和可见性暂停。完成原型后再决定是否扩展到更复杂的 Adaptive Tile 内容模型。
