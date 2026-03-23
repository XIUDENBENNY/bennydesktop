using DesktopAssistantLite.App.Models;

namespace DesktopAssistantLite.App.Forms;

internal sealed class SettingsForm : Form
{
    private readonly CheckBox _startupCheckBox;
    private readonly CheckBox _floatingBallVisibleCheckBox;
    private readonly CheckBox _floatingBallAutoHideCheckBox;
    private readonly CheckBox _screenshotHotkeyEnabledCheckBox;
    private readonly CheckBox _screenshotHotkeyYieldToQqCheckBox;
    private readonly NumericUpDown _memoryRefreshNumeric;
    private readonly TextBox _searchPathsTextBox;
    private readonly TextBox _screenshotPathTextBox;
    private readonly TextBox _categoryRulesTextBox;
    private readonly CheckBox _restartExplorerCheckBox;

    public SettingsForm()
    {
        Text = "设置";
        Width = 940;
        Height = 760;
        MinimumSize = new Size(820, 660);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(242, 245, 250);
        Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular);

        var rootPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22),
        };

        var headerPanel = BuildHeaderPanel();
        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular),
        };

        var generalPage = new TabPage("桌面整理");
        var searchPage = new TabPage("搜索与截图");
        var advancedPage = new TabPage("悬浮球与启动");

        _categoryRulesTextBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 10F),
        };
        generalPage.Controls.Add(BuildSectionPanel("分类规则", "格式示例：文档=.txt,.docx,.pdf", _categoryRulesTextBox));

        _searchPathsTextBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 10F),
        };
        _screenshotPathTextBox = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 32,
        };
        _screenshotHotkeyEnabledCheckBox = new CheckBox
        {
            Text = "启用截图快捷键",
            Dock = DockStyle.Top,
            Height = 34,
        };
        _screenshotHotkeyYieldToQqCheckBox = new CheckBox
        {
            Text = "QQ 运行时让出 Ctrl + Alt + A",
            Dock = DockStyle.Top,
            Height = 34,
        };

        var browseButton = new Button
        {
            Text = "选择截图目录",
            Dock = DockStyle.Top,
            Height = 36,
        };
        browseButton.Click += (_, _) => BrowseScreenshotDirectory();

        var searchStack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 7,
            Padding = new Padding(18),
        };
        searchStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        searchStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        searchStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        searchStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        searchStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        searchStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        searchStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        searchStack.Controls.Add(CreateSectionTitle("截图保存目录", "截图会保存到这个目录。"), 0, 0);
        searchStack.Controls.Add(_screenshotPathTextBox, 0, 1);
        searchStack.Controls.Add(browseButton, 0, 2);
        searchStack.Controls.Add(_screenshotHotkeyEnabledCheckBox, 0, 3);
        searchStack.Controls.Add(_screenshotHotkeyYieldToQqCheckBox, 0, 4);
        searchStack.Controls.Add(CreateSectionTitle("索引目录", "每行一个目录，供本地搜索建立索引。"), 0, 5);
        searchStack.Controls.Add(_searchPathsTextBox, 0, 6);
        searchPage.Controls.Add(WrapCard(searchStack));

        _startupCheckBox = new CheckBox { Text = "开机启动", Dock = DockStyle.Top, Height = 34 };
        _floatingBallVisibleCheckBox = new CheckBox { Text = "显示悬浮球", Dock = DockStyle.Top, Height = 34 };
        _floatingBallAutoHideCheckBox = new CheckBox { Text = "悬浮球自动贴边隐藏", Dock = DockStyle.Top, Height = 34 };
        _restartExplorerCheckBox = new CheckBox { Text = "安全加速时允许重启 Explorer", Dock = DockStyle.Top, Height = 34 };
        _memoryRefreshNumeric = new NumericUpDown
        {
            Minimum = 2,
            Maximum = 30,
            Value = 5,
            Dock = DockStyle.Top,
            Height = 34,
        };

        var advancedStack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 6,
            Padding = new Padding(18),
        };
        for (var i = 0; i < 5; i++)
        {
            advancedStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        }

        advancedStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        advancedStack.Controls.Add(_startupCheckBox, 0, 0);
        advancedStack.Controls.Add(_floatingBallVisibleCheckBox, 0, 1);
        advancedStack.Controls.Add(_floatingBallAutoHideCheckBox, 0, 2);
        advancedStack.Controls.Add(_restartExplorerCheckBox, 0, 3);
        advancedStack.Controls.Add(CreateMemoryPanel(), 0, 4);
        advancedPage.Controls.Add(WrapCard(advancedStack));

        tabs.TabPages.Add(generalPage);
        tabs.TabPages.Add(searchPage);
        tabs.TabPages.Add(advancedPage);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 10, 12, 10),
        };

        var saveButton = new Button
        {
            Text = "保存",
            Width = 96,
            Height = 36,
            BackColor = Color.FromArgb(25, 113, 194),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
        };
        saveButton.FlatAppearance.BorderSize = 0;
        saveButton.Click += (_, _) => SaveAndClose();
        buttonPanel.Controls.Add(saveButton);

        rootPanel.Controls.Add(tabs);
        rootPanel.Controls.Add(buttonPanel);
        rootPanel.Controls.Add(headerPanel);
        Controls.Add(rootPanel);
    }

    public event EventHandler<AppSettings>? SettingsSaved;

    public void LoadFromSettings(AppSettings settings)
    {
        _startupCheckBox.Checked = settings.StartupEnabled;
        _screenshotHotkeyEnabledCheckBox.Checked = settings.ScreenshotHotkeyEnabled;
        _screenshotHotkeyYieldToQqCheckBox.Checked = settings.ScreenshotHotkeyYieldToQq;
        _floatingBallVisibleCheckBox.Checked = settings.FloatingBallVisible;
        _floatingBallAutoHideCheckBox.Checked = settings.FloatingBallAutoHide;
        _memoryRefreshNumeric.Value = settings.MemoryRefreshIntervalSeconds;
        _searchPathsTextBox.Text = string.Join(Environment.NewLine, settings.SearchPaths);
        _screenshotPathTextBox.Text = settings.ScreenshotSaveDir;
        _restartExplorerCheckBox.Checked = settings.BoostExplorerRestartEnabled;
        _categoryRulesTextBox.Text = ToRulesText(settings.CategoryRules);
    }

    private Control BuildHeaderPanel()
    {
        var card = new Panel
        {
            Dock = DockStyle.Top,
            Height = 112,
            BackColor = Color.White,
            Padding = new Padding(22, 18, 22, 18),
        };

        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 34,
            Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Text = "设置",
        };

        var hintLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = Color.FromArgb(100, 116, 139),
            Text = "配置搜索、截图、悬浮球和桌面整理行为。",
        };

        card.Controls.Add(hintLabel);
        card.Controls.Add(titleLabel);
        return WrapCard(card, addBottomSpacing: true);
    }

    private static Control BuildSectionPanel(string title, string description, Control content)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            Padding = new Padding(18),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.Controls.Add(CreateSectionTitle(title, description), 0, 0);
        layout.Controls.Add(content, 0, 1);
        return WrapCard(layout);
    }

    private static Control CreateSectionTitle(string title, string description)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
        };

        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Text = title,
        };

        var descriptionLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            ForeColor = Color.FromArgb(100, 116, 139),
            Text = description,
        };

        panel.Controls.Add(descriptionLabel);
        panel.Controls.Add(titleLabel);
        return panel;
    }

    private Control CreateMemoryPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 42,
        };

        var label = new Label
        {
            Dock = DockStyle.Left,
            Width = 180,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "内存刷新间隔（秒）",
        };

        panel.Controls.Add(_memoryRefreshNumeric);
        panel.Controls.Add(label);
        return panel;
    }

    private static Control WrapCard(Control content, bool addBottomSpacing = false)
    {
        var host = new Panel
        {
            Dock = content.Dock == DockStyle.Top ? DockStyle.Top : DockStyle.Fill,
            Height = content.Dock == DockStyle.Top ? content.Height + (addBottomSpacing ? 16 : 0) : 0,
            Padding = addBottomSpacing ? new Padding(0, 0, 0, 16) : Padding.Empty,
        };
        content.Dock = DockStyle.Fill;
        host.Controls.Add(content);
        return host;
    }

    private void BrowseScreenshotDirectory()
    {
        using var dialog = new FolderBrowserDialog
        {
            SelectedPath = _screenshotPathTextBox.Text,
            ShowNewFolderButton = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _screenshotPathTextBox.Text = dialog.SelectedPath;
        }
    }

    private void SaveAndClose()
    {
        var settings = new AppSettings
        {
            StartupEnabled = _startupCheckBox.Checked,
            ScreenshotHotkeyEnabled = _screenshotHotkeyEnabledCheckBox.Checked,
            ScreenshotHotkeyYieldToQq = _screenshotHotkeyYieldToQqCheckBox.Checked,
            FloatingBallVisible = _floatingBallVisibleCheckBox.Checked,
            FloatingBallAutoHide = _floatingBallAutoHideCheckBox.Checked,
            SearchPaths = _searchPathsTextBox.Lines.Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Trim()).ToList(),
            ScreenshotSaveDir = _screenshotPathTextBox.Text.Trim(),
            CategoryRules = ParseRules(_categoryRulesTextBox.Text),
            MemoryRefreshIntervalSeconds = (int)_memoryRefreshNumeric.Value,
            BoostExplorerRestartEnabled = _restartExplorerCheckBox.Checked,
        };

        SettingsSaved?.Invoke(this, settings);
        Hide();
    }

    private static string ToRulesText(Dictionary<string, List<string>> rules)
    {
        return string.Join(
            Environment.NewLine,
            rules.Select(pair => $"{pair.Key}={string.Join(",", pair.Value)}"));
    }

    private static Dictionary<string, List<string>> ParseRules(string text)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in text.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var name = line[..separatorIndex].Trim();
            var values = line[(separatorIndex + 1)..]
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => value.Length > 0)
                .ToList();
            result[name] = values;
        }

        return result.Count == 0 ? AppSettings.CreateDefaultCategoryRules() : result;
    }
}
