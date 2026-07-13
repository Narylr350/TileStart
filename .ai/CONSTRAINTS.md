# Constraints and Working Rules

- 项目路径固定为 `D:\Narylr\TileStart`。
- Git 默认分支为 `main`，本基线属于项目主线。
- 当前为新项目，无旧工作流文件；`.ai/` 是项目长期基线事实源。
- 用户已确认允许 Explorer 注入和 Shell Hook。
- 接管效果和使用体验优先于降低 Windows 版本适配的维护成本。
- Windows 更新兼容属于项目需要解决的工作，不作为取消 Shell 接管的理由。
- Shell Hook 中不加载 WPF、.NET 或业务配置，只处理事件拦截、IPC 和原生放行。
- Host 崩溃、未启动、IPC 不可用或系统版本不受支持时，不得阻断原生开始菜单。
- 不支持的 Windows build 默认禁用 Hook，不使用未经验证的偏移或签名继续运行。
- Win10 和 Win11 使用独立适配器，每个受支持 build 都需要实机验证。
- UI 以当前 Win10 原生开始菜单为视觉和交互参考，不改成通用现代启动器风格。
- 布局使用 DIP，不硬编码当前 `2560×1600` 的物理像素；当前 150% DPI 是首个视觉验证环境。
- 配置、缓存和日志写入 `%LOCALAPPDATA%\TileStart`，第一版不依赖网络。
- 代码保持简洁，只实现 MVP 所需能力，不创建无关抽象。
- 引用第三方开源实现前检查许可证和必要性。
- project-init 只生成 `.ai/` 基线文档，不创建代码脚手架。
- 未接入外部执行层 skill。
- 未接入 finish 层 skill。
