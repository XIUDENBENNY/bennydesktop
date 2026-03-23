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
cd D:\benndydesktoptool
dotnet run --project .\DesktopAssistantLite.App\DesktopAssistantLite.App.csproj
```

## 编译

```powershell
cd D:\benndydesktoptool
dotnet build .\DesktopAssistantLite.sln
```

## 发布

```powershell
cd D:\benndydesktoptool
dotnet publish .\DesktopAssistantLite.App\DesktopAssistantLite.App.csproj -c Release -r win-x64 --self-contained false
```

发布输出默认在：

`DesktopAssistantLite.App\bin\Release\net8.0-windows\win-x64\publish`

## 当前测试入口

- 托盘右键：显示或隐藏悬浮球
- 托盘双击：恢复悬浮球
- 托盘右键：整理桌面 / 恢复布局
- 托盘右键：搜索文件 / 待办事项 / 截图 / 安全加速
- `Ctrl + Alt + A`：如果未被其他程序占用，则触发选框截图；若被 QQ 等程序占用，本程序自动让位
