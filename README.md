# DesktopAssistantLite

轻量版 Windows 桌面助手，基于 `.NET 8 + WinForms + SQLite`。

## 已实现

- 托盘常驻
- 可隐藏悬浮球
- 双击托盘恢复悬浮球
- 桌面真实分类整理
- 最近一次整理恢复
- 收纳页内手动移动分类
- 本地文件搜索索引
- 本地待办
- 区域截图并保存/复制
- 安全加速
- 开机自启设置

## 本地运行

```powershell
# 先进入项目根目录
dotnet run --project .\DesktopAssistantLite.App\DesktopAssistantLite.App.csproj
```

或直接运行：

```powershell
.\run.bat
```

若无法启动，请先安装 `.NET 8 Windows Desktop Runtime (x64)`：

- 官方下载页：https://dotnet.microsoft.com/en-us/download/dotnet/8.0
- 当前直链：https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.24/windowsdesktop-runtime-8.0.24-win-x64.exe

## 编译

```powershell
dotnet build .\DesktopAssistantLite.sln
```

## 发布

```powershell
dotnet publish .\DesktopAssistantLite.App\DesktopAssistantLite.App.csproj -c Release -r win-x64 --self-contained false
```

发布输出默认在：

`DesktopAssistantLite.App\bin\Release\net8.0-windows\win-x64\publish`

当前推荐对外分发轻量版发布包。
使用前提：

- 目标机器已安装 `.NET 8 Windows Desktop Runtime (x64)`

## 当前测试入口

- 托盘右键：显示或隐藏悬浮球
- 托盘双击：恢复悬浮球
- 托盘右键：整理桌面 / 恢复布局
- 托盘右键：搜索文件 / 待办事项 / 截图 / 安全加速
- `Ctrl + Alt + A`：如果未被其他程序占用，则触发选框截图；若被 QQ 等程序占用，本程序自动让位
