using DesktopAssistantLite.App.Models;
using DesktopAssistantLite.App.Services;

namespace DesktopAssistantLite.App.Forms;

internal sealed class TodoForm : Form
{
    private readonly TodoService _todoService;
    private TextBox _titleTextBox = null!;
    private CheckBox _pinCheckBox = null!;
    private DateTimePicker _duePicker = null!;
    private CheckBox _enableDueCheckBox = null!;
    private ListView _todoListView = null!;

    public TodoForm(TodoService todoService)
    {
        _todoService = todoService;

        Text = "待办事项";
        Width = 1040;
        Height = 720;
        MinimumSize = new Size(920, 620);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(242, 245, 250);
        Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular);

        var rootPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22),
        };

        var headerCard = BuildHeaderCard();
        var listCard = BuildListCard();

        rootPanel.Controls.Add(listCard);
        rootPanel.Controls.Add(headerCard);
        Controls.Add(rootPanel);
    }

    public async Task RefreshDataAsync()
    {
        var items = await _todoService.GetAllAsync();
        _todoListView.Items.Clear();

        foreach (var todo in items)
        {
            var listItem = new ListViewItem(todo.Title);
            listItem.SubItems.Add(todo.DueAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "-");
            listItem.SubItems.Add(todo.IsCompleted ? "已完成" : todo.IsPinned ? "置顶" : "进行中");
            listItem.SubItems.Add(todo.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
            listItem.Tag = todo;
            _todoListView.Items.Add(listItem);
        }

        ResizeColumns();
    }

    private Control BuildHeaderCard()
    {
        var card = new Panel
        {
            Dock = DockStyle.Top,
            Height = 190,
            BackColor = Color.White,
            Padding = new Padding(22, 18, 22, 18),
        };

        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 34,
            Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Text = "待办事项",
        };

        var subTitleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = Color.FromArgb(100, 116, 139),
            Text = "支持提醒、置顶、完成和删除。",
        };

        var inputGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 86,
            ColumnCount = 5,
            Margin = new Padding(0),
            Padding = new Padding(0, 8, 0, 0),
        };
        inputGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        inputGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));
        inputGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220F));
        inputGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94F));
        inputGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));

        _titleTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 12, 8),
            Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Regular),
            PlaceholderText = "输入待办标题",
        };

        _enableDueCheckBox = new CheckBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 12, 12, 8),
            Text = "开启提醒",
        };

        _duePicker = new DateTimePicker
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 12, 8),
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy-MM-dd HH:mm",
            Enabled = false,
        };
        _enableDueCheckBox.CheckedChanged += (_, _) => _duePicker.Enabled = _enableDueCheckBox.Checked;

        _pinCheckBox = new CheckBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 12, 12, 8),
            Text = "置顶",
        };

        var addButton = CreateActionButton("新增", Color.FromArgb(25, 113, 194), Color.White);
        addButton.Click += async (_, _) => await AddAsync();

        inputGrid.Controls.Add(_titleTextBox, 0, 0);
        inputGrid.Controls.Add(_enableDueCheckBox, 1, 0);
        inputGrid.Controls.Add(_duePicker, 2, 0);
        inputGrid.Controls.Add(_pinCheckBox, 3, 0);
        inputGrid.Controls.Add(addButton, 4, 0);

        var actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 42,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
        };

        var refreshButton = CreateActionButton("刷新", Color.FromArgb(241, 245, 249), Color.FromArgb(30, 41, 59));
        refreshButton.Click += async (_, _) => await RefreshDataAsync();
        var toggleCompleteButton = CreateActionButton("切换完成", Color.FromArgb(241, 245, 249), Color.FromArgb(30, 41, 59));
        toggleCompleteButton.Click += async (_, _) => await ToggleCompletedAsync();
        var togglePinButton = CreateActionButton("切换置顶", Color.FromArgb(241, 245, 249), Color.FromArgb(30, 41, 59));
        togglePinButton.Click += async (_, _) => await TogglePinnedAsync();
        var deleteButton = CreateActionButton("删除", Color.FromArgb(254, 242, 242), Color.FromArgb(185, 28, 28));
        deleteButton.Click += async (_, _) => await DeleteAsync();
        actionPanel.Controls.AddRange([refreshButton, toggleCompleteButton, togglePinButton, deleteButton]);

        card.Controls.Add(actionPanel);
        card.Controls.Add(inputGrid);
        card.Controls.Add(subTitleLabel);
        card.Controls.Add(titleLabel);
        return WrapCard(card, true);
    }

    private Control BuildListCard()
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(18),
        };

        _todoListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
            BorderStyle = BorderStyle.None,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular),
        };
        _todoListView.Columns.Add("标题", 360);
        _todoListView.Columns.Add("提醒时间", 200);
        _todoListView.Columns.Add("状态", 120);
        _todoListView.Columns.Add("创建时间", 180);
        _todoListView.Resize += (_, _) => ResizeColumns();
        _todoListView.DoubleClick += async (_, _) => await ToggleCompletedAsync();

        card.Controls.Add(_todoListView);
        return WrapCard(card, false);
    }

    private static Button CreateActionButton(string text, Color backColor, Color foreColor)
    {
        var button = new Button
        {
            Text = text,
            Width = 108,
            Height = 34,
            Margin = new Padding(0, 0, 12, 0),
            BackColor = backColor,
            ForeColor = foreColor,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private static Control WrapCard(Control content, bool addBottomSpacing)
    {
        var host = new Panel
        {
            Dock = content.Dock,
            Height = content.Height + (addBottomSpacing ? 16 : 0),
            Padding = addBottomSpacing ? new Padding(0, 0, 0, 16) : Padding.Empty,
        };
        content.Dock = DockStyle.Fill;
        host.Controls.Add(content);
        return host;
    }

    private void ResizeColumns()
    {
        if (_todoListView.Columns.Count == 0)
        {
            return;
        }

        var width = Math.Max(_todoListView.ClientSize.Width - 6, 760);
        _todoListView.Columns[0].Width = (int)(width * 0.38);
        _todoListView.Columns[1].Width = (int)(width * 0.24);
        _todoListView.Columns[2].Width = (int)(width * 0.12);
        _todoListView.Columns[3].Width = width - _todoListView.Columns.Cast<ColumnHeader>().Take(3).Sum(column => column.Width);
    }

    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(_titleTextBox.Text))
        {
            MessageBox.Show(this, "请输入待办标题。", "待办事项", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        DateTime? due = _enableDueCheckBox.Checked ? _duePicker.Value : null;
        await _todoService.AddAsync(_titleTextBox.Text, due, _pinCheckBox.Checked);
        _titleTextBox.Clear();
        _enableDueCheckBox.Checked = false;
        _pinCheckBox.Checked = false;
        await RefreshDataAsync();
    }

    private async Task ToggleCompletedAsync()
    {
        if (!TryGetSelectedTodo(out var todo))
        {
            return;
        }

        await _todoService.ToggleCompletedAsync(todo.Id, !todo.IsCompleted);
        await RefreshDataAsync();
    }

    private async Task TogglePinnedAsync()
    {
        if (!TryGetSelectedTodo(out var todo))
        {
            return;
        }

        await _todoService.TogglePinnedAsync(todo.Id, !todo.IsPinned);
        await RefreshDataAsync();
    }

    private async Task DeleteAsync()
    {
        if (!TryGetSelectedTodo(out var todo))
        {
            return;
        }

        await _todoService.DeleteAsync(todo.Id);
        await RefreshDataAsync();
    }

    private bool TryGetSelectedTodo(out TodoItem todo)
    {
        todo = default!;
        if (_todoListView.SelectedItems.Count == 0 || _todoListView.SelectedItems[0].Tag is not TodoItem value)
        {
            return false;
        }

        todo = value;
        return true;
    }
}
