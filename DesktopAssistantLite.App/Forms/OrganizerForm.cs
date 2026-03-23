using System.Drawing.Drawing2D;
using DesktopAssistantLite.App.Models;

namespace DesktopAssistantLite.App.Forms;

internal sealed class OrganizerForm : Form
{
    private readonly Action<string> _openItemAction;
    private readonly Action<string> _openContainingFolderAction;
    private readonly Func<DesktopItem, string, Task> _moveItemAction;
    private readonly Func<Task> _refreshAction;
    private readonly Func<Task> _organizeAction;
    private readonly Func<Task> _restoreAction;

    private Label _windowTagLabel = null!;
    private Label _titleLabel = null!;
    private Label _summaryLabel = null!;
    private Label _hintLabel = null!;
    private Label _sectionTitleLabel = null!;
    private Label _emptyStateLabel = null!;
    private FlowLayoutPanel _categoryFlowPanel = null!;
    private DataGridView _itemGrid = null!;
    private Button _organizeButton = null!;
    private Button _restoreButton = null!;
    private Button _refreshButton = null!;
    private ContextMenuStrip _itemContextMenu = null!;
    private ToolStripMenuItem _moveMenuItem = null!;

    private Dictionary<string, List<DesktopItem>> _groups = new(StringComparer.OrdinalIgnoreCase);
    private string? _selectedCategory;

    public OrganizerForm(
        Action<string> openItemAction,
        Action<string> openContainingFolderAction,
        Func<DesktopItem, string, Task> moveItemAction,
        Func<Task> refreshAction,
        Func<Task> organizeAction,
        Func<Task> restoreAction)
    {
        _openItemAction = openItemAction;
        _openContainingFolderAction = openContainingFolderAction;
        _moveItemAction = moveItemAction;
        _refreshAction = refreshAction;
        _organizeAction = organizeAction;
        _restoreAction = restoreAction;

        Text = "桌面收纳盒";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1240, 820);
        Size = new Size(1480, 920);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(242, 245, 250);
        Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular);

        BuildLayout();
    }

    public void LoadGroups(IReadOnlyDictionary<string, List<DesktopItem>> groups, string title, DateTime createdAtUtc)
    {
        Text = title;
        _windowTagLabel.Text = title.Contains("快照", StringComparison.Ordinal) ? "快照视图" : "当前状态";
        _titleLabel.Text = "桌面收纳盒";
        _summaryLabel.Text = $"状态时间 {createdAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}    共 {groups.Sum(pair => pair.Value.Count)} 个项目";
        _groups = groups.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        var previousCategory = _selectedCategory;
        _selectedCategory = null;

        foreach (var pair in _groups)
        {
            _selectedCategory ??= pair.Key;
        }

        if (_groups.Count == 0)
        {
            RebuildCategoryCards();
            BindSelectedCategoryItems();
            return;
        }

        if (!string.IsNullOrWhiteSpace(previousCategory))
        {
            foreach (var category in _groups.Keys)
            {
                if (string.Equals(category, previousCategory, StringComparison.OrdinalIgnoreCase))
                {
                    _selectedCategory = category;
                    break;
                }
            }
        }

        RebuildCategoryCards();
        BindSelectedCategoryItems();
    }

    private void BuildLayout()
    {
        var rootPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22),
        };

        var heroPanel = BuildHeroPanel();
        var contentPanel = BuildContentPanel();

        rootPanel.Controls.Add(contentPanel);
        rootPanel.Controls.Add(heroPanel);
        Controls.Add(rootPanel);
    }

    private Control BuildHeroPanel()
    {
        var heroPanel = new GradientPanel
        {
            Dock = DockStyle.Top,
            Height = 210,
            Padding = new Padding(28, 26, 28, 22),
            StartColor = Color.FromArgb(16, 34, 64),
            EndColor = Color.FromArgb(28, 109, 193),
        };

        var topRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 118,
            ColumnCount = 2,
            BackColor = Color.Transparent,
        };
        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68F));
        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));

        var textPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = false,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };

        _windowTagLabel = new Label
        {
            AutoSize = true,
            BackColor = Color.FromArgb(34, 255, 255, 255),
            ForeColor = Color.FromArgb(226, 232, 240),
            Padding = new Padding(10, 4, 10, 4),
            Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Bold),
            Text = "当前状态",
            Margin = new Padding(0, 0, 0, 10),
        };

        _titleLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei UI", 26F, FontStyle.Bold),
            Text = "桌面收纳盒",
            Margin = new Padding(0, 0, 0, 10),
        };

        _summaryLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(220, 235, 252),
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular),
            Text = "正在读取桌面项目...",
            MaximumSize = new Size(680, 0),
            Margin = new Padding(0),
        };

        textPanel.Controls.Add(_windowTagLabel);
        textPanel.Controls.Add(_titleLabel);
        textPanel.Controls.Add(_summaryLabel);

        var summaryCard = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 255, 255, 255),
            Margin = new Padding(18, 0, 0, 0),
            Padding = new Padding(18, 16, 18, 16),
        };

        var summaryTitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = Color.FromArgb(214, 231, 255),
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
            Text = "当前操作说明",
        };

        var summaryBody = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = false,
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular),
            Text = "整理桌面会把文件移动到分类目录。\r\n快捷方式保留在桌面，可直接双击或右键管理。",
        };

        summaryCard.Controls.Add(summaryBody);
        summaryCard.Controls.Add(summaryTitle);

        topRow.Controls.Add(textPanel, 0, 0);
        topRow.Controls.Add(summaryCard, 1, 0);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
        };

        _organizeButton = CreateHeroButton("桌面整理", Color.FromArgb(255, 255, 255), Color.FromArgb(18, 84, 160));
        _organizeButton.Click += async (_, _) => await _organizeAction();

        _restoreButton = CreateHeroButton("恢复布局", Color.FromArgb(34, 255, 255, 255), Color.White);
        _restoreButton.Click += async (_, _) => await _restoreAction();

        _refreshButton = CreateHeroButton("刷新当前桌面", Color.FromArgb(34, 255, 255, 255), Color.White);
        _refreshButton.Click += async (_, _) => await _refreshAction();

        buttonPanel.Controls.AddRange([_organizeButton, _restoreButton, _refreshButton]);

        heroPanel.Controls.Add(buttonPanel);
        heroPanel.Controls.Add(topRow);
        return heroPanel;
    }

    private Control BuildContentPanel()
    {
        var contentPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(0, 20, 0, 0),
        };
        contentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260F));
        contentPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var sidebarCard = new CardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 18, 18, 16),
            Margin = new Padding(0, 0, 16, 0),
        };

        var sidebarTitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Text = "分类导航",
        };

        var sidebarSubTitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 48,
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular),
            Text = "左侧切换分类，右侧查看详情。\r\n快捷方式固定归到“桌面保留”。",
        };

        _categoryFlowPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.White,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0, 6, 0, 0),
        };

        sidebarCard.Controls.Add(_categoryFlowPanel);
        sidebarCard.Controls.Add(sidebarSubTitle);
        sidebarCard.Controls.Add(sidebarTitle);

        var mainCard = new CardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22, 20, 22, 18),
            Margin = new Padding(0),
        };

        var cardHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 86,
        };

        _sectionTitleLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Text = "当前分类",
            Dock = DockStyle.Top,
        };

        _hintLabel = new Label
        {
            AutoSize = false,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
            Text = "双击可以直接打开，右键可以打开所在位置或移动到其他分类。",
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(0, 8, 0, 0),
        };

        cardHeader.Controls.Add(_hintLabel);
        cardHeader.Controls.Add(_sectionTitleLabel);

        _itemGrid = CreateItemGrid();
        _emptyStateLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184),
            Text = "当前分类没有项目。",
            Visible = false,
        };

        var gridHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 8, 0, 0),
        };
        gridHost.Controls.Add(_emptyStateLabel);
        gridHost.Controls.Add(_itemGrid);

        mainCard.Controls.Add(gridHost);
        mainCard.Controls.Add(cardHeader);

        contentPanel.Controls.Add(sidebarCard, 0, 0);
        contentPanel.Controls.Add(mainCard, 1, 0);
        return contentPanel;
    }

    private DataGridView CreateItemGrid()
    {
        var grid = new SmoothDataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AllowUserToResizeColumns = false,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ClipboardCopyMode = DataGridViewClipboardCopyMode.Disable,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            ColumnHeadersHeight = 46,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            EnableHeadersVisualStyles = false,
            MultiSelect = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        };

        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(248, 250, 252),
            ForeColor = Color.FromArgb(30, 41, 59),
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(12, 6, 12, 6),
            SelectionBackColor = Color.FromArgb(248, 250, 252),
            SelectionForeColor = Color.FromArgb(30, 41, 59),
            WrapMode = DataGridViewTriState.False,
        };
        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 41, 59),
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(10, 12, 10, 12),
            SelectionBackColor = Color.FromArgb(232, 240, 254),
            SelectionForeColor = Color.FromArgb(16, 34, 64),
            WrapMode = DataGridViewTriState.False,
        };
        grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(250, 252, 255),
            ForeColor = Color.FromArgb(30, 41, 59),
            SelectionBackColor = Color.FromArgb(232, 240, 254),
            SelectionForeColor = Color.FromArgb(16, 34, 64),
            Padding = new Padding(10, 12, 10, 12),
            WrapMode = DataGridViewTriState.False,
        };
        grid.GridColor = Color.FromArgb(232, 238, 247);
        grid.RowTemplate.Height = 48;

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "名称",
            FillWeight = 32,
            MinimumWidth = 260,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "类型",
            FillWeight = 10,
            MinimumWidth = 90,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "当前位置",
            FillWeight = 18,
            MinimumWidth = 150,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "状态",
            FillWeight = 18,
            MinimumWidth = 170,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "修改时间",
            FillWeight = 22,
            MinimumWidth = 180,
        });

        grid.CellDoubleClick += (_, args) =>
        {
            if (args.RowIndex >= 0)
            {
                OpenSelectedItem();
            }
        };
        grid.CellMouseDown += (_, args) =>
        {
            if (args.RowIndex < 0 || args.Button != MouseButtons.Right)
            {
                return;
            }

            grid.ClearSelection();
            grid.Rows[args.RowIndex].Selected = true;
            grid.CurrentCell = grid.Rows[args.RowIndex].Cells[0];
        };

        _itemContextMenu = new ContextMenuStrip
        {
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular),
        };
        _itemContextMenu.Items.Add(new ToolStripMenuItem("打开", null, (_, _) => OpenSelectedItem()));
        _itemContextMenu.Items.Add(new ToolStripMenuItem("打开所在位置", null, (_, _) => OpenSelectedItemFolder()));
        _itemContextMenu.Items.Add(new ToolStripSeparator());
        _moveMenuItem = new ToolStripMenuItem("移动到");
        _itemContextMenu.Items.Add(_moveMenuItem);
        _itemContextMenu.Opening += ItemContextMenuOnOpening;
        grid.ContextMenuStrip = _itemContextMenu;

        return grid;
    }

    private static Button CreateHeroButton(string text, Color backColor, Color foreColor)
    {
        var button = new Button
        {
            Text = text,
            Width = 126,
            Height = 38,
            Margin = new Padding(0, 0, 12, 0),
            BackColor = backColor,
            ForeColor = foreColor,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand,
        };

        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private void RebuildCategoryCards()
    {
        _categoryFlowPanel.SuspendLayout();
        _categoryFlowPanel.Controls.Clear();

        foreach (var pair in _groups)
        {
            var description = string.Equals(pair.Key, "桌面保留", StringComparison.OrdinalIgnoreCase)
                ? "快捷方式保留在桌面"
                : $"{pair.Value.Count} 个项目";
            var selected = string.Equals(pair.Key, _selectedCategory, StringComparison.OrdinalIgnoreCase);
            _categoryFlowPanel.Controls.Add(CreateCategoryCard(pair.Key, pair.Value.Count, description, selected));
        }

        _categoryFlowPanel.ResumeLayout();
    }

    private void BindSelectedCategoryItems()
    {
        _itemGrid.Rows.Clear();

        if (string.IsNullOrWhiteSpace(_selectedCategory))
        {
            _sectionTitleLabel.Text = "当前分类";
            _hintLabel.Text = "左侧选择一个分类后，这里会显示详细项目。";
            UpdateGridEmptyState();
            return;
        }

        var category = _selectedCategory;
        _sectionTitleLabel.Text = category;

        if (!_groups.TryGetValue(category, out var items))
        {
            UpdateGridEmptyState();
            return;
        }

        _hintLabel.Text = string.Equals(category, "桌面保留", StringComparison.OrdinalIgnoreCase)
            ? "这里显示保留在桌面的快捷方式。它们不参与整理，也不能移动分类。"
            : "双击项目可直接打开，右键可打开所在位置或移动到其他分类。";

        foreach (var item in items)
        {
            var rowIndex = _itemGrid.Rows.Add(
                item.Name,
                item.ItemType,
                item.LocationLabel,
                item.StatusLabel,
                item.LastWriteTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            _itemGrid.Rows[rowIndex].Tag = item;
        }

        UpdateGridEmptyState();
    }

    private void UpdateGridEmptyState()
    {
        var hasRows = _itemGrid.Rows.Count > 0;
        _itemGrid.Visible = hasRows;
        _emptyStateLabel.Visible = !hasRows;
    }

    private Control CreateCategoryCard(string category, int count, string description, bool selected)
    {
        var card = new CategoryCardPanel
        {
            Width = 196,
            Height = 78,
            Margin = new Padding(6, 0, 6, 10),
            Padding = new Padding(18, 14, 18, 12),
            Cursor = Cursors.Hand,
            IsSelected = selected,
        };

        var titleLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
            ForeColor = selected ? Color.FromArgb(18, 84, 160) : Color.FromArgb(30, 41, 59),
            Text = $"{category} ({count})",
        };

        var descriptionLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
            ForeColor = selected ? Color.FromArgb(58, 104, 171) : Color.FromArgb(100, 116, 139),
            Text = description,
        };

        void SelectCard(object? _, EventArgs __)
        {
            _selectedCategory = category;
            RebuildCategoryCards();
            BindSelectedCategoryItems();
        }

        card.Click += SelectCard;
        titleLabel.Click += SelectCard;
        descriptionLabel.Click += SelectCard;

        card.Controls.Add(descriptionLabel);
        card.Controls.Add(titleLabel);
        return card;
    }

    private void ItemContextMenuOnOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var item = GetSelectedItem();
        if (item is null)
        {
            e.Cancel = true;
            return;
        }

        _moveMenuItem.DropDownItems.Clear();
        var movableCategories = _groups.Keys
            .Where(category => !string.Equals(category, "桌面保留", StringComparison.OrdinalIgnoreCase))
            .Where(category => !string.Equals(category, item.Category, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var category in movableCategories)
        {
            var menuItem = new ToolStripMenuItem(category);
            menuItem.Click += async (_, _) => await MoveItemAsync(item, category);
            _moveMenuItem.DropDownItems.Add(menuItem);
        }

        _moveMenuItem.Enabled = item.CanMove && _moveMenuItem.DropDownItems.Count > 0;
    }

    private async Task MoveItemAsync(DesktopItem item, string targetCategory)
    {
        try
        {
            await _moveItemAction(item, targetCategory);
            await _refreshAction();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "桌面收纳盒", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OpenSelectedItem()
    {
        var item = GetSelectedItem();
        if (item is null)
        {
            return;
        }

        _openItemAction(item.FullPath);
    }

    private void OpenSelectedItemFolder()
    {
        var item = GetSelectedItem();
        if (item is null)
        {
            return;
        }

        _openContainingFolderAction(item.FullPath);
    }

    private DesktopItem? GetSelectedItem()
    {
        if (_itemGrid.SelectedRows.Count == 0)
        {
            return null;
        }

        return _itemGrid.SelectedRows[0].Tag as DesktopItem;
    }

    private static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private sealed class GradientPanel : Panel
    {
        public Color StartColor { get; init; } = Color.FromArgb(16, 34, 64);

        public Color EndColor { get; init; } = Color.FromArgb(28, 109, 193);

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (ClientRectangle.Width <= 1 || ClientRectangle.Height <= 1)
            {
                using var solidBrush = new SolidBrush(StartColor);
                e.Graphics.FillRectangle(solidBrush, ClientRectangle);
                return;
            }

            using var brush = new LinearGradientBrush(ClientRectangle, StartColor, EndColor, LinearGradientMode.Horizontal);
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }
    }

    private sealed class CardPanel : Panel
    {
        public CardPanel()
        {
            BackColor = Color.White;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (Width <= 1 || Height <= 1)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var borderBounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = CreateRoundedRectangle(borderBounds, 18);
            using var pen = new Pen(Color.FromArgb(226, 232, 240));
            using var brush = new SolidBrush(BackColor);

            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
        }
    }

    private sealed class CategoryCardPanel : Panel
    {
        public bool IsSelected { get; init; }

        public CategoryCardPanel()
        {
            BackColor = Color.White;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (Width <= 1 || Height <= 1)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var borderBounds = new Rectangle(0, 0, Width - 1, Height - 1);
            var backColor = IsSelected ? Color.FromArgb(235, 243, 255) : Color.FromArgb(248, 250, 252);
            var borderColor = IsSelected ? Color.FromArgb(83, 142, 247) : Color.FromArgb(232, 238, 247);

            using var path = CreateRoundedRectangle(borderBounds, 18);
            using var pen = new Pen(borderColor, IsSelected ? 2F : 1F);
            using var brush = new SolidBrush(backColor);

            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
        }
    }

    private sealed class SmoothDataGridView : DataGridView
    {
        public SmoothDataGridView()
        {
            DoubleBuffered = true;
        }
    }
}
