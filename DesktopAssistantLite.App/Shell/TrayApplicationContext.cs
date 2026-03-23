using System.Diagnostics;
using DesktopAssistantLite.App.Forms;
using DesktopAssistantLite.App.Models;
using DesktopAssistantLite.App.Services;
using DesktopAssistantLite.App.Storage;

namespace DesktopAssistantLite.App.Shell;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private const int ScreenshotHotkeyId = 1001;
    private static readonly (Keys Modifiers, Keys Key, string Display)[] PrimaryScreenshotHotkeys =
    [
        (Keys.Control | Keys.Alt, Keys.A, "Ctrl + Alt + A"),
    ];

    private static readonly (Keys Modifiers, Keys Key, string Display)[] BackupScreenshotHotkeys =
    [
        (Keys.Control | Keys.Alt, Keys.S, "Ctrl + Alt + S"),
        (Keys.Control | Keys.Alt, Keys.D, "Ctrl + Alt + D"),
        (Keys.Control | Keys.Alt, Keys.Q, "Ctrl + Alt + Q"),
    ];

    private readonly AppPaths _paths;
    private readonly LogService _logService;
    private readonly SettingsService _settingsService;
    private readonly DatabaseService _databaseService;
    private readonly StartupService _startupService;
    private readonly DesktopOrganizerService _desktopOrganizerService;
    private readonly SearchIndexService _searchIndexService;
    private readonly TodoService _todoService;
    private readonly ScreenshotService _screenshotService;
    private readonly MemoryBoostService _memoryBoostService;
    private readonly Icon _trayIcon;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _toggleFloatingBallItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly ToolStripMenuItem _captureMenuItem;
    private readonly System.Windows.Forms.Timer _memoryTimer;
    private readonly System.Windows.Forms.Timer _todoReminderTimer;
    private readonly System.Windows.Forms.Timer _hotkeyMonitorTimer;
    private readonly FloatingBallForm _floatingBallForm;
    private readonly HotkeyWindow _hotkeyWindow;

    private AppSettings _settings;
    private AppToastForm? _toastForm;
    private OrganizerForm? _organizerForm;
    private SearchForm? _searchForm;
    private TodoForm? _todoForm;
    private SettingsForm? _settingsForm;
    private string? _activeScreenshotHotkeyDisplay;
    private bool _prefersQqScreenshotHotkey;
    private bool _boostInProgress;

    public TrayApplicationContext()
    {
        _paths = new AppPaths();
        _paths.EnsureDirectories();
        _logService = new LogService(_paths.LogDirectory);
        _settingsService = new SettingsService(_paths.SettingsPath, _paths);
        _settings = _settingsService.Load();
        _settings.FloatingBallVisible = false;
        _databaseService = new DatabaseService(_paths.DatabasePath);
        _databaseService.Initialize();
        _startupService = new StartupService();
        _desktopOrganizerService = new DesktopOrganizerService(_databaseService, _logService);
        _searchIndexService = new SearchIndexService(_databaseService, _logService);
        _todoService = new TodoService(_databaseService, _logService);
        _screenshotService = new ScreenshotService(_paths, _logService);
        _memoryBoostService = new MemoryBoostService(_paths, _logService);
        _trayIcon = CreateTrayIcon();

        var contextMenu = new ContextMenuStrip();
        _toggleFloatingBallItem = new ToolStripMenuItem("隐藏悬浮球", null, (_, _) => ToggleFloatingBallVisibility());
        _captureMenuItem = new ToolStripMenuItem("选框截图", null, async (_, _) => await CaptureAsync());

        contextMenu.Items.Add(_toggleFloatingBallItem);
        contextMenu.Items.Add(new ToolStripMenuItem("桌面收纳盒", null, async (_, _) => await OpenOrganizerAsync()));
        contextMenu.Items.Add(new ToolStripMenuItem("整理桌面", null, async (_, _) => await OrganizeDesktopAsync()));
        contextMenu.Items.Add(new ToolStripMenuItem("恢复布局", null, async (_, _) => await RestoreLayoutAsync()));
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(new ToolStripMenuItem("搜索文件", null, async (_, _) => await OpenSearchAsync()));
        contextMenu.Items.Add(new ToolStripMenuItem("待办事项", null, async (_, _) => await OpenTodoAsync()));
        contextMenu.Items.Add(_captureMenuItem);
        contextMenu.Items.Add(new ToolStripMenuItem("安全加速", null, async (_, _) => await RunBoostAsync()));
        contextMenu.Items.Add(new ToolStripSeparator());
        _startupItem = new ToolStripMenuItem("开机启动", null, (_, _) => ToggleStartup())
        {
            Checked = _settings.StartupEnabled,
        };
        contextMenu.Items.Add(_startupItem);
        contextMenu.Items.Add(new ToolStripMenuItem("设置", null, (_, _) => OpenSettings()));
        contextMenu.Items.Add(new ToolStripMenuItem("退出", null, (_, _) => Exit()));

        _notifyIcon = new NotifyIcon
        {
            Icon = _trayIcon,
            Text = "Desktop Assistant Lite",
            Visible = true,
            ContextMenuStrip = contextMenu,
        };
        _notifyIcon.DoubleClick += async (_, _) => await OpenOrganizerAsync();

        _floatingBallForm = new FloatingBallForm
        {
            ContextMenuStrip = contextMenu,
            AutoHideEnabled = _settings.FloatingBallAutoHide,
        };
        _floatingBallForm.PrimaryActionRequested += async (_, _) => await RunBoostAsync();
        PositionFloatingBall();
        _toggleFloatingBallItem.Text = "显示悬浮球";

        _hotkeyWindow = new HotkeyWindow();
        _hotkeyWindow.HotkeyPressed += async (_, id) =>
        {
            if (id == ScreenshotHotkeyId)
            {
                await CaptureAsync();
            }
        };
        TryRegisterScreenshotHotkey();

        _memoryTimer = new System.Windows.Forms.Timer();
        _memoryTimer.Tick += (_, _) => UpdateMemoryDisplay();
        ApplyMemoryRefreshInterval();
        _memoryTimer.Start();

        _todoReminderTimer = new System.Windows.Forms.Timer { Interval = 60000 };
        _todoReminderTimer.Tick += async (_, _) => await CheckTodoRemindersAsync();
        _todoReminderTimer.Start();

        _hotkeyMonitorTimer = new System.Windows.Forms.Timer { Interval = 4000 };
        _hotkeyMonitorTimer.Tick += (_, _) => RefreshScreenshotHotkeyRegistration(showToastOnChange: false);
        _hotkeyMonitorTimer.Start();

        ApplyStartupSetting();
        _ = InitializeSearchAsync();
        UpdateMemoryDisplay();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _memoryTimer.Dispose();
            _todoReminderTimer.Dispose();
            _hotkeyMonitorTimer.Dispose();
            _hotkeyWindow.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _trayIcon.Dispose();
            _floatingBallForm.Dispose();
            _toastForm?.Dispose();
            _organizerForm?.Dispose();
            _searchForm?.Dispose();
            _todoForm?.Dispose();
            _settingsForm?.Dispose();
            _searchIndexService.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task InitializeSearchAsync()
    {
        try
        {
            await _searchIndexService.InitializeAsync(_settings.SearchPaths);
        }
        catch (Exception ex)
        {
            _logService.Error("Search initialization failed.", ex);
            ShowToast("搜索索引初始化失败，稍后可手动重建。");
        }
    }

    private void TryRegisterScreenshotHotkey()
    {
        RefreshScreenshotHotkeyRegistration(showToastOnChange: false);
    }

    private void RefreshScreenshotHotkeyRegistration(bool showToastOnChange = true)
    {
        if (!_settings.ScreenshotHotkeyEnabled)
        {
            if (_activeScreenshotHotkeyDisplay is not null)
            {
                _hotkeyWindow.Unregister(ScreenshotHotkeyId);
                _activeScreenshotHotkeyDisplay = null;
                _prefersQqScreenshotHotkey = false;
                _logService.Info("Screenshot hotkey disabled by settings.");
                if (showToastOnChange)
                {
                    ShowToast("截图快捷键已关闭，可继续通过托盘菜单使用截图。");
                }
            }

            UpdateCaptureMenuText();
            return;
        }

        var qqRunning = IsQqRunning();
        var yieldToQq = _settings.ScreenshotHotkeyYieldToQq && qqRunning;
        if (_activeScreenshotHotkeyDisplay is not null && yieldToQq == _prefersQqScreenshotHotkey)
        {
            UpdateCaptureMenuText();
            return;
        }

        var previousHotkeyDisplay = _activeScreenshotHotkeyDisplay;
        _hotkeyWindow.Unregister(ScreenshotHotkeyId);
        _activeScreenshotHotkeyDisplay = null;

        var candidates = yieldToQq
            ? BackupScreenshotHotkeys
            : PrimaryScreenshotHotkeys.Concat(BackupScreenshotHotkeys).ToArray();

        foreach (var candidate in candidates)
        {
            if (_hotkeyWindow.Register(ScreenshotHotkeyId, candidate.Modifiers, candidate.Key))
            {
                _activeScreenshotHotkeyDisplay = candidate.Display;
                _prefersQqScreenshotHotkey = yieldToQq;
                UpdateCaptureMenuText();

                if (yieldToQq)
                {
                    _logService.Info($"QQ is running. Screenshot hotkey switched to {_activeScreenshotHotkeyDisplay}.");
                    if (showToastOnChange)
                    {
                        ShowToast($"检测到 QQ 正在运行，截图快捷键已切换为 {_activeScreenshotHotkeyDisplay}。");
                    }
                }
                else
                {
                    _logService.Info($"Screenshot hotkey registered: {_activeScreenshotHotkeyDisplay}.");
                    if (showToastOnChange && !string.Equals(previousHotkeyDisplay, _activeScreenshotHotkeyDisplay, StringComparison.Ordinal))
                    {
                        ShowToast($"截图快捷键已切换为 {_activeScreenshotHotkeyDisplay}。");
                    }
                }

                return;
            }
        }

        _prefersQqScreenshotHotkey = yieldToQq;
        UpdateCaptureMenuText();
        _logService.Info("No available screenshot hotkey could be registered.");
        if (showToastOnChange)
        {
            ShowToast("截图快捷键当前都被占用，可继续通过托盘菜单使用截图。");
        }
    }

    private void UpdateCaptureMenuText()
    {
        if (!_settings.ScreenshotHotkeyEnabled)
        {
            _captureMenuItem.Text = "选框截图（快捷键已关闭）";
            return;
        }

        _captureMenuItem.Text = _activeScreenshotHotkeyDisplay is null
            ? "选框截图（无可用快捷键）"
            : $"选框截图（{_activeScreenshotHotkeyDisplay}）";
    }

    private static bool IsQqRunning()
    {
        return Process.GetProcessesByName("QQ").Length > 0;
    }

    private async Task OpenOrganizerAsync()
    {
        var groups = await _desktopOrganizerService.LoadCurrentGroupsAsync(_settings.CategoryRules, _settings.DefaultCategoryOrder);
        EnsureOrganizerForm().LoadGroups(groups, "桌面收纳盒 - 当前状态", DateTime.UtcNow);
        ShowOwnedForm(_organizerForm!);
    }

    private async Task OrganizeDesktopAsync()
    {
        try
        {
            var snapshot = await _desktopOrganizerService.OrganizeAsync(_settings.CategoryRules, _settings.DefaultCategoryOrder);
            var arranged = _desktopOrganizerService.ArrangeDesktopIcons();
            EnsureOrganizerForm().LoadGroups(snapshot.Groups, $"桌面收纳盒 - 快照 #{snapshot.SnapshotId}", snapshot.CreatedAtUtc);
            ShowOwnedForm(_organizerForm!);
            ShowToast(arranged
                ? "桌面项目已整理到分类目录，并按项目类型重新整理了桌面图标。"
                : "桌面项目已整理到分类目录，但桌面图标自动排序未执行成功。");
        }
        catch (Exception ex)
        {
            _logService.Error("Failed to organize desktop.", ex);
            ShowToast("整理桌面失败，请查看日志。");
        }
    }

    private async Task RestoreLayoutAsync()
    {
        try
        {
            var snapshot = await _desktopOrganizerService.RestoreLatestSnapshotAsync(_settings.DefaultCategoryOrder);
            if (snapshot is null)
            {
                ShowToast("还没有可恢复的桌面快照。");
                return;
            }

            EnsureOrganizerForm().LoadGroups(snapshot.Value.Groups, $"恢复快照 #{snapshot.Value.SnapshotId}", snapshot.Value.CreatedAtUtc);
            ShowOwnedForm(_organizerForm!);
            ShowToast("最近一次整理已恢复到桌面根目录。");
        }
        catch (Exception ex)
        {
            _logService.Error("Failed to restore desktop snapshot.", ex);
            ShowToast("恢复布局失败，请查看日志。");
        }
    }

    private async Task RefreshOrganizerAsync()
    {
        await OpenOrganizerAsync();
    }

    private async Task OpenSearchAsync()
    {
        var form = EnsureSearchForm();
        ShowOwnedForm(form);
        await form.FocusSearchAsync();
    }

    private async Task OpenTodoAsync()
    {
        var form = EnsureTodoForm();
        ShowOwnedForm(form);
        await form.RefreshDataAsync();
    }

    private async Task CaptureAsync()
    {
        var floatingVisible = _floatingBallForm.Visible;
        if (floatingVisible)
        {
            _floatingBallForm.Hide();
            await Task.Delay(70);
        }

        try
        {
            using var image = CaptureOverlayForm.CaptureRegion();
            if (image is null)
            {
                return;
            }

            Clipboard.SetImage(image);
            var filePath = _screenshotService.SaveScreenshot(image, _settings.ScreenshotSaveDir);
            ShowToast($"截图已保存并复制到剪贴板：{Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            _logService.Error("Screenshot failed.", ex);
            ShowToast("截图失败，请重试。");
        }
        finally
        {
            if (floatingVisible && _settings.FloatingBallVisible)
            {
                _floatingBallForm.Show();
                _floatingBallForm.SnapToWorkingArea();
            }
        }

        await Task.CompletedTask;
    }

    private async Task RunBoostAsync()
    {
        if (_boostInProgress)
        {
            return;
        }

        _boostInProgress = true;
        _floatingBallForm.StartBoostAnimation();

        try
        {
            var result = await _memoryBoostService.RunSafeBoostAsync(_settings.BoostExplorerRestartEnabled);
            _floatingBallForm.CompleteBoostAnimation();
            ShowToast("本次清理已完成", BuildBoostSummaryMessage(result));
            UpdateMemoryDisplay();
        }
        catch (Exception ex)
        {
            _logService.Error("Memory boost failed.", ex);
            _floatingBallForm.FailBoostAnimation();
            ShowToast("这次清理没有完成", "刚才没能顺利跑完。\r\n你可以稍后再试一次，或到日志里查看原因。");
        }
        finally
        {
            _boostInProgress = false;
        }
    }

    private void ToggleFloatingBallVisibility(bool forceShow = false)
    {
        var shouldShow = forceShow || !_floatingBallForm.Visible;
        if (shouldShow)
        {
            _floatingBallForm.Show();
            _floatingBallForm.SnapToWorkingArea();
            _floatingBallForm.RevealFromEdge();
            _settings.FloatingBallVisible = true;
        }
        else
        {
            _floatingBallForm.Hide();
            _settings.FloatingBallVisible = false;
        }

        _toggleFloatingBallItem.Text = _settings.FloatingBallVisible ? "隐藏悬浮球" : "显示悬浮球";
        SaveSettings();
    }

    private void ToggleStartup()
    {
        _settings.StartupEnabled = !_settings.StartupEnabled;
        ApplyStartupSetting();
        SaveSettings();
    }

    private void OpenSettings()
    {
        var form = EnsureSettingsForm();
        form.LoadFromSettings(_settings.Clone());
        ShowOwnedForm(form);
    }

    private void Exit()
    {
        SaveSettings();
        ExitThread();
    }

    private void ApplyStartupSetting()
    {
        _startupService.SetEnabled(_settings.StartupEnabled, Application.ExecutablePath);
        _startupItem.Checked = _settings.StartupEnabled;
    }

    private void PositionFloatingBall()
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        _floatingBallForm.Location = new Point(area.Right - _floatingBallForm.Width - 8, area.Top + 180);
    }

    private void ApplyMemoryRefreshInterval()
    {
        _memoryTimer.Interval = Math.Max(2, _settings.MemoryRefreshIntervalSeconds) * 1000;
    }

    private void UpdateMemoryDisplay()
    {
        var usedBytes = Process.GetCurrentProcess().WorkingSet64;
        var totalBytes = new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory;
        var percentage = totalBytes == 0 ? 0 : (int)Math.Round((double)usedBytes / totalBytes * 100);
        _floatingBallForm.SetMemoryUsage(percentage, (long)(usedBytes / 1024d / 1024d));
    }

    private async Task CheckTodoRemindersAsync()
    {
        try
        {
            var dueItems = await _todoService.GetDueReminderItemsAsync(DateTime.UtcNow);
            foreach (var item in dueItems.Take(3))
            {
                ShowToast($"待办提醒：{item.Title}");
                await _todoService.MarkReminderShownAsync(item.Id, DateTime.UtcNow);
            }

            if (_todoForm is { IsDisposed: false, Visible: true })
            {
                await _todoForm.RefreshDataAsync();
            }
        }
        catch (Exception ex)
        {
            _logService.Error("Todo reminder check failed.", ex);
        }
    }

    private void ShowOwnedForm(Form form)
    {
        if (!form.Visible)
        {
            form.Show();
        }

        if (form.WindowState == FormWindowState.Minimized)
        {
            form.WindowState = FormWindowState.Normal;
        }

        form.BringToFront();
        form.Activate();
    }

    private void SaveSettings()
    {
        _settingsService.Save(_settings);
    }

    private void OnSettingsSaved(object? sender, AppSettings settings)
    {
        _settings = settings;
        SaveSettings();
        ApplyMemoryRefreshInterval();
        ApplyStartupSetting();
        _floatingBallForm.AutoHideEnabled = _settings.FloatingBallAutoHide;

        if (_settings.FloatingBallVisible && !_floatingBallForm.Visible)
        {
            _floatingBallForm.Show();
            _floatingBallForm.SnapToWorkingArea();
        }
        else if (!_settings.FloatingBallVisible && _floatingBallForm.Visible)
        {
            _floatingBallForm.Hide();
        }

        _toggleFloatingBallItem.Text = _settings.FloatingBallVisible ? "隐藏悬浮球" : "显示悬浮球";
        _ = _searchIndexService.InitializeAsync(_settings.SearchPaths);
        if (_searchForm is { IsDisposed: false })
        {
            _searchForm.Close();
            _searchForm = null;
        }

        RefreshScreenshotHotkeyRegistration(showToastOnChange: false);
        UpdateMemoryDisplay();
    }

    private OrganizerForm EnsureOrganizerForm()
    {
        if (_organizerForm is null || _organizerForm.IsDisposed)
        {
            _organizerForm = new OrganizerForm(
                _desktopOrganizerService.OpenItem,
                _desktopOrganizerService.OpenContainingFolder,
                _desktopOrganizerService.MoveItemToCategoryAsync,
                RefreshOrganizerAsync,
                OrganizeDesktopAsync,
                RestoreLayoutAsync);
        }

        return _organizerForm;
    }

    private SearchForm EnsureSearchForm()
    {
        if (_searchForm is null || _searchForm.IsDisposed)
        {
            _searchForm = new SearchForm(_searchIndexService, _settings.SearchPaths);
        }

        return _searchForm;
    }

    private TodoForm EnsureTodoForm()
    {
        if (_todoForm is null || _todoForm.IsDisposed)
        {
            _todoForm = new TodoForm(_todoService);
        }

        return _todoForm;
    }

    private SettingsForm EnsureSettingsForm()
    {
        if (_settingsForm is null || _settingsForm.IsDisposed)
        {
            _settingsForm = new SettingsForm();
            _settingsForm.SettingsSaved += OnSettingsSaved;
        }

        return _settingsForm;
    }

    private string BuildBoostSummaryMessage(MemoryBoostResult result)
    {
        var freedMb = result.EstimatedFreedBytes / 1024d / 1024d;
        var firstLine = freedMb >= 1
            ? $"预计为你腾出了 {freedMb:F1} MB 空间。"
            : "这次没有清出特别明显的空间。";

        var details = new List<string>();
        if (result.TrimmedProcessCount > 0)
        {
            details.Add($"处理了 {result.TrimmedProcessCount} 个占用较高的进程");
        }

        if (result.DeletedItemCount > 0)
        {
            details.Add($"删除了 {result.DeletedItemCount} 个临时文件");
        }

        if (result.ExplorerRestarted)
        {
            details.Add("已顺带重启资源管理器");
        }

        if (details.Count == 0)
        {
            details.Add("系统当前本来就比较干净");
        }

        return $"{firstLine}\r\n{string.Join("，", details)}。";
    }

    private void ShowToast(string message)
    {
        ShowToast("Desktop Assistant Lite", message);
    }

    private void ShowToast(string title, string message)
    {
        _toastForm?.Close();
        _toastForm = new AppToastForm(title, message);
        _toastForm.Show();
    }

    private static Icon CreateTrayIcon()
    {
        using var bitmap = new Bitmap(64, 64);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var backgroundBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
            new Rectangle(0, 0, 64, 64),
            Color.FromArgb(18, 48, 95),
            Color.FromArgb(26, 145, 213),
            System.Drawing.Drawing2D.LinearGradientMode.ForwardDiagonal);
        using var tileBrush = new SolidBrush(Color.White);
        using var accentBrush = new SolidBrush(Color.FromArgb(194, 230, 255));

        graphics.FillEllipse(backgroundBrush, 2, 2, 60, 60);
        FillRoundedTile(graphics, tileBrush, 16, 16, 13, 13, 3);
        FillRoundedTile(graphics, tileBrush, 34, 16, 13, 13, 3);
        FillRoundedTile(graphics, tileBrush, 16, 34, 13, 13, 3);
        FillRoundedTile(graphics, accentBrush, 34, 34, 13, 13, 3);

        var iconHandle = bitmap.GetHicon();
        try
        {
            using var tempIcon = Icon.FromHandle(iconHandle);
            return (Icon)tempIcon.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(iconHandle);
        }
    }

    private static void FillRoundedTile(Graphics graphics, Brush brush, int x, int y, int width, int height, int radius)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(x, y, diameter, diameter, 180, 90);
        path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
        path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
        path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }
}
