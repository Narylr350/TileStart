# TileStart

TileStart 是一个面向 Windows 10/11 x64 的 Win10 风格磁贴开始菜单。它保留开始菜单应用列表、字母索引、搜索、分组磁贴和四种磁贴尺寸，并允许固定文件、文件夹、脚本、网址、应用和自定义命令。

当前 Shell 适配器经过实机验证的平台是：

```text
Windows 10 22H2 build 19045 x64
```

其他 Windows build 默认不注入 Explorer，保持原版开始菜单可用。Windows 11 适配需要在对应 build 上完成独立验证后才能启用。

## 功能

- 单独 `Win` 键和任务栏开始按钮打开或关闭 TileStart。
- `Win+E/R/D/L/I/数字/方向键/Shift+S` 等组合键保留系统行为。
- 扫描用户和公共开始菜单，并显示 UWP/MSIX 应用。
- 最近添加、字母索引、直接输入搜索和应用文件夹。
- 8 单元磁贴分组及小、中、宽、大四种尺寸。
- 实时拖动让位、跨组移动、新建组、重命名、排序和删除。
- 固定 `.exe`、`.lnk`、文件、文件夹、`.bat`、`.cmd`、`.ps1`、`.url` 和自定义命令。
- 自定义标题、副标题、颜色、背景图片、图标、图标大小和位置。
- 托盘暂停接管、打开原版开始菜单、切换登录自启动和退出。
- Explorer 重启后恢复接管；Host、IPC 或 Hook 不可用时 fail-open。
- 本地 JSON 布局和窗口尺寸持久化。

## 构建

要求：

- .NET SDK 8.0.408（由 `global.json` 固定）
- Visual Studio C++ x64 工具链
- Inno Setup 6

托管代码：

```powershell
dotnet restore tests\TileStart.Host.Tests\TileStart.Host.Tests.csproj
dotnet build src\TileStart.Host\TileStart.Host.csproj -c Release
dotnet test tests\TileStart.Host.Tests\TileStart.Host.Tests.csproj -c Release
```

完整混合解决方案需要使用 Visual Studio MSBuild：

```powershell
msbuild TileStart.sln /restore /m /p:Configuration=Release /p:Platform=x64
```

不要使用 `dotnet build TileStart.sln`，因为 .NET SDK MSBuild 不包含 Visual C++ targets。

生成自包含便携包和安装程序：

```powershell
.\scripts\Build-Package.ps1
```

输出：

```text
artifacts\package\TileStart-portable-win-x64.zip
artifacts\installer\TileStart-Setup-win-x64.exe
```

`artifacts` 是本地构建输出，不提交到 Git。

## 使用

安装或解压后运行 `TileStart.Host.exe`。程序常驻通知区域，默认不显示主窗口。

- 点击任务栏开始按钮或单独按下 `Win`：打开/关闭 TileStart。
- 右键磁贴：取消固定、调整大小、管理员运行或打开 TileStart 设置。
- 将文件或文件夹拖入磁贴区：创建磁贴。
- 右键通知区域图标：暂停接管、打开原版开始菜单、切换登录自启动或退出。

配置与日志位于：

```text
%LOCALAPPDATA%\TileStart
```

安装器使用当前用户目录，不要求管理员权限。卸载会停止 Host 和 Injector、卸载 Explorer Hook，并删除登录自启动项。

## 安全边界

生产运行时不会修改或依赖微软 `StartUI.dll`。Explorer 内只加载最小原生 Shell Hook，用于开始按钮事件、Named Pipe 请求和 fail-open。Host 未运行、请求超时、注入失败或 Windows build 未验证时，原版开始菜单保持可用。

实机验证记录见 [`docs/mvp-validation.md`](docs/mvp-validation.md)。Win10 原版行为和逆向证据见 [`docs/win10-start-research.md`](docs/win10-start-research.md)。
