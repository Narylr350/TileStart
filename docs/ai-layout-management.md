# AI 辅助布局管理

TileStart 将完整的运行时布局保存在 `%LOCALAPPDATA%\TileStart\layout.json`。
布局始终由用户控制：TileStart 不会强制规定分类、顺序、容量或溢出文件夹策略。

当 AI 或本地自动化需要安全检查或替换布局时，请使用 `scripts\Manage-Layout.ps1`。

## 命令

```powershell
# 输出权威数据路径。
powershell -ExecutionPolicy Bypass -File scripts\Manage-Layout.ps1 -Action Paths

# 生成精简、适合 AI 阅读的摘要，同时保留完整磁贴 ID 和启动目标。
powershell -ExecutionPolicy Bypass -File scripts\Manage-Layout.ps1 -Action Summary -OutputPath "$env:TEMP\TileStart-layout-summary.json"

# 应用前校验完整的候选布局。
powershell -ExecutionPolicy Bypass -File scripts\Manage-Layout.ps1 -Action Validate -InputPath "$env:TEMP\layout-candidate.json"

# 校验候选布局、备份当前布局、停止正在运行的 Host、原子替换文件，并重新启动同一个 Host 可执行文件。
powershell -ExecutionPolicy Bypass -File scripts\Manage-Layout.ps1 -Action Apply -InputPath "$env:TEMP\layout-candidate.json"
```

`Apply` 会将安全备份保存在 `%LOCALAPPDATA%\TileStart\layout-backups`。
候选文件使用与 `layout.json` 相同的版本化结构，因此可以保留自定义磁贴外观、命令、文件夹和稳定 ID。

## 本机偏好

可选的个人布局整理偏好可以保存在：

```text
%LOCALAPPDATA%\TileStart\ai-layout-preferences.json
```

工具会在 `Summary` 中包含该文件的内容，但工具和 TileStart 都不会将它解释为产品默认规则。它只为当前用户和 AI 提供参考。个人规则必须保持可选、允许明确例外，并且不得作为 TileStart 默认值提交到仓库。
