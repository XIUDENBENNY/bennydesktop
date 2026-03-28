using System.Drawing.Drawing2D;
using DesktopAssistantLite.App.Models;

namespace DesktopAssistantLite.App.Forms;

internal sealed class OrganizerForm : Form
{
    private readonly Action<string> _openItemAction;
    private readonly Action<string> _openContainingFolderAction;
    private readonly Func<DesktopItem, string, Task> _moveItemAction;
    private readonly Func<DesktopItem, Task> _restoreToDesktopAction;
    private readonly Func<DesktopItem, Task> _moveToRecycleBinAction;
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
    private ToolStripMenuItem _restoreToDesktopMenuItem = null!;
    private ToolStripMenuItem _moveToRecycleBinMenuItem = null!;
    private readonly Dictionary<string, Image> _iconCache = new(StringComparer.OrdinalIgnoreCase);

    private Dictionary<string, List<DesktopItem>> _groups = new(StringComparer.OrdinalIgnoreCase);
    private string? _selectedCategory;

    public OrganizerForm(
        Action<string> openItemAction,
        Action<string> openContainingFolderAction,
        Func<DesktopItem, string, Task> moveItemAction,
        Func<DesktopItem, Task> restoreToDesktopAction,
        Func<DesktopItem, Task> moveToRecycleBinAction,
        Func<Task> refreshAction,
        Func<Task> organizeAction,
        Func<Task> restoreAction)
    {
        _openItemAction = openItemAction;
        _openContainingFolderAction = openContainingFolderAction;
        _moveItemAction = moveItemAction;
        _restoreToDesktopAction = restoreToDesktopAction;
        _moveToRecycleBinAction = moveToRecycleBinAction;
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var image in _iconCache.Values)
            {
                image.Dispose();
            }

            _iconCache.Clear();
        }

        base.Dispose(disposing);
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

        var heroPanel = BuildHeroPanelV2();
        var contentPanel = BuildContentPanel();

        rootPanel.Controls.Add(contentPanel);
        rootPanel.Controls.Add(heroPanel);
        Controls.Add(rootPanel);
    }

    private Control BuildHeroPanel()
    {
        var heroPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 316,
            Padding = new Padding(28, 22, 28, 22),
            BackColor = Color.FromArgb(29, 84, 158),
        };

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90F));

        _windowTagLabel = new Label
        {
            AutoSize = false,
            BackColor = Color.FromArgb(34, 255, 255, 255),
            ForeColor = Color.FromArgb(226, 232, 240),
            Width = 86,
            Height = 28,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(10, 4, 10, 4),
            Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Bold),
            Text = "当前状态",
            Margin = new Padding(0, 0, 0, 0),
        };

        _titleLabel = new Label
        {
            AutoSize = false,
            ForeColor = Color.FromArgb(245, 249, 255),
            Font = new Font("Microsoft YaHei UI", 26F, FontStyle.Bold),
            Text = "桌面收纳盒",
            TextAlign = ContentAlignment.BottomLeft,
            Margin = new Padding(0),
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
        };

        _summaryLabel = new Label
        {
            AutoSize = false,
            ForeColor = Color.FromArgb(220, 235, 252),
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular),
            Text = "正在读取桌面项目...",
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 2, 0, 0),
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
        };

        _organizeButton = CreateHeroButton("桌面整理", Color.FromArgb(255, 255, 255), Color.FromArgb(18, 84, 160));
        _organizeButton.Click += async (_, _) => await _organizeAction();

        _restoreButton = CreateHeroButton("恢复布局", Color.FromArgb(34, 255, 255, 255), Color.White);
        _restoreButton.Click += async (_, _) => await _restoreAction();

        _refreshButton = CreateHeroButton("刷新当前桌面", Color.FromArgb(34, 255, 255, 255), Color.White);
        _refreshButton.Click += async (_, _) => await _refreshAction();

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        buttonPanel.Controls.AddRange([_organizeButton, _restoreButton, _refreshButton]);

        var summaryHost = new Panel
        {
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
        };

        var summaryCard = new InfoStripPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(164, 191, 223),
            Padding = new Padding(14, 10, 14, 10),
            Margin = new Padding(0),
        };

        var summaryTitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 20,
            ForeColor = Color.FromArgb(44, 73, 112),
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
            Text = "当前操作说明",
        };

        var summaryBody = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = false,
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei UI", 9.6F, FontStyle.Regular),
            Padding = new Padding(0, 4, 0, 0),
            Text = "整理桌面会把文件移入分类目录，桌面项目统一归到“桌面”。\r\n右键可还原到桌面、放入回收站，并记住手动归类。",
        };

        summaryCard.Controls.Add(summaryBody);
        summaryCard.Controls.Add(summaryTitle);
        summaryHost.Controls.Add(summaryCard);

        rootLayout.Controls.Add(_windowTagLabel, 0, 0);
        rootLayout.Controls.Add(_titleLabel, 0, 1);
        rootLayout.Controls.Add(_summaryLabel, 0, 2);
        rootLayout.Controls.Add(buttonPanel, 0, 3);
        rootLayout.Controls.Add(summaryHost, 0, 4);
        heroPanel.Controls.Add(rootLayout);
        return heroPanel;
    }

    private Control BuildHeroPanelV2()
    {
        var heroPanel = new Panel
        {
            Dock = DockStyle.Top,
            Padding = new Padding(28, 22, 28, 20),
            BackColor = Color.FromArgb(15, 56, 120),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };

        var contentPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };

        _windowTagLabel = new Label
        {
            AutoSize = true,
            MinimumSize = new Size(82, 28),
            Padding = new Padding(10, 4, 10, 4),
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.FromArgb(48, 255, 255, 255),
            ForeColor = Color.FromArgb(235, 243, 255),
            Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "当前状态",
        };

        _titleLabel = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(247, 250, 255),
            Font = new Font("Microsoft YaHei", 26F, FontStyle.Bold),
            Text = "桌面收纳盒",
        };

        _summaryLabel = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12),
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(210, 225, 246),
            Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Regular),
            Text = "正在读取桌面项目...",
        };

        _organizeButton = CreateHeroButton("桌面整理", Color.FromArgb(255, 255, 255), Color.FromArgb(16, 71, 148));
        _organizeButton.Click += async (_, _) => await _organizeAction();

        _restoreButton = CreateHeroButton("恢复布局", Color.FromArgb(38, 255, 255, 255), Color.White);
        _restoreButton.Click += async (_, _) => await _restoreAction();

        _refreshButton = CreateHeroButton("刷新当前桌面", Color.FromArgb(38, 255, 255, 255), Color.White);
        _refreshButton.Click += async (_, _) => await _refreshAction();

        var buttonPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        buttonPanel.Controls.AddRange([_organizeButton, _restoreButton, _refreshButton]);

        contentPanel.Controls.Add(_windowTagLabel);
        contentPanel.Controls.Add(_titleLabel);
        contentPanel.Controls.Add(_summaryLabel);
        contentPanel.Controls.Add(buttonPanel);

        heroPanel.Controls.Add(contentPanel);
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
            Height = 40,
            Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Text = "分类导航",
            TextAlign = ContentAlignment.BottomLeft,
        };

        var sidebarSubTitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 76,
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular),
            Text = "左侧切换分类，右侧查看详情。\r\n桌面上的项目统一归到“桌面”。",
            Padding = new Padding(0, 4, 0, 6),
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
            Text = "双击可以直接打开，右键可以打开位置、移动分类、还原桌面或放入回收站。",
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

        grid.Columns.Add(new DataGridViewImageColumn
        {
            HeaderText = string.Empty,
            FillWeight = 4,
            MinimumWidth = 34,
            Width = 34,
            ImageLayout = DataGridViewImageCellLayout.Zoom,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "名称",
            FillWeight = 28,
            MinimumWidth = 240,
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
        _restoreToDesktopMenuItem = new ToolStripMenuItem("还原到桌面", null, async (_, _) => await RestoreSelectedItemToDesktopAsync());
        _moveToRecycleBinMenuItem = new ToolStripMenuItem("放入回收站", null, async (_, _) => await MoveSelectedItemToRecycleBinAsync());
        _itemContextMenu.Items.Add(_restoreToDesktopMenuItem);
        _moveMenuItem = new ToolStripMenuItem("移动到");
        _itemContextMenu.Items.Add(_moveMenuItem);
        _itemContextMenu.Items.Add(new ToolStripSeparator());
        _itemContextMenu.Items.Add(_moveToRecycleBinMenuItem);
        _itemContextMenu.Opening += ItemContextMenuOnOpening;
        grid.ContextMenuStrip = _itemContextMenu;

        return grid;
    }

    private static Button CreateHeroButton(string text, Color backColor, Color foreColor)
    {
        var button = new Button
        {
            Text = text,
            Width = 132,
            Height = 38,
            Margin = new Padding(0, 0, 12, 0),
            BackColor = backColor,
            ForeColor = foreColor,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand,
        };

        button.FlatAppearance.BorderSize = backColor.A < 255 ? 1 : 0;
        button.FlatAppearance.BorderColor = Color.FromArgb(76, 194, 214, 240);
        button.FlatAppearance.MouseOverBackColor = backColor.A < 255
            ? Color.FromArgb(54, 255, 255, 255)
            : Color.FromArgb(242, 246, 255);
        button.FlatAppearance.MouseDownBackColor = backColor.A < 255
            ? Color.FromArgb(70, 255, 255, 255)
            : Color.FromArgb(228, 236, 250);
        return button;
    }

    private void RebuildCategoryCards()
    {
        _categoryFlowPanel.SuspendLayout();
        _categoryFlowPanel.Controls.Clear();

        foreach (var pair in _groups)
        {
            var description = string.Equals(pair.Key, "桌面", StringComparison.OrdinalIgnoreCase)
                ? "当前在桌面的项目"
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

        _hintLabel.Text = string.Equals(category, "桌面", StringComparison.OrdinalIgnoreCase)
            ? "这里显示当前在桌面的项目。你可以继续保留，也可以右键手动归到某个分类。"
            : "双击项目可直接打开，右键可移动分类、还原到桌面或放入回收站。";

        foreach (var item in items)
        {
            var rowIndex = _itemGrid.Rows.Add(
                GetItemImage(item),
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
            .Where(category => !string.Equals(category, "桌面", StringComparison.OrdinalIgnoreCase))
            .Where(category => !string.Equals(category, item.Category, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var category in movableCategories)
        {
            var menuItem = new ToolStripMenuItem(category);
            menuItem.Click += async (_, _) => await MoveItemAsync(item, category);
            _moveMenuItem.DropDownItems.Add(menuItem);
        }

        _moveMenuItem.Enabled = item.CanMove && _moveMenuItem.DropDownItems.Count > 0;
        _restoreToDesktopMenuItem.Enabled =
            item.CanMove &&
            !string.Equals(item.LocationLabel, "桌面", StringComparison.OrdinalIgnoreCase);
        _moveToRecycleBinMenuItem.Enabled = true;
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

    private async Task RestoreSelectedItemToDesktopAsync()
    {
        var item = GetSelectedItem();
        if (item is null)
        {
            return;
        }

        try
        {
            await _restoreToDesktopAction(item);
            await _refreshAction();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "桌面收纳盒", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task MoveSelectedItemToRecycleBinAsync()
    {
        var item = GetSelectedItem();
        if (item is null)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            $"确定要把“{item.Name}”放入回收站吗？",
            "桌面收纳盒",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question);

        if (result != DialogResult.OK)
        {
            return;
        }

        try
        {
            await _moveToRecycleBinAction(item);
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

    private Image GetItemImage(DesktopItem item)
    {
        var cacheKey = $"{item.ItemType}|{item.FullPath}";
        if (_iconCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var image = TryExtractShellIcon(item.FullPath) ?? TryExtractShellIcon(item.OriginalPath) ?? CreateFallbackIcon(item);
        _iconCache[cacheKey] = image;
        return image;
    }

    private static Image? TryExtractShellIcon(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.Exists(path))
        {
            return null;
        }

        NativeMethods.ShFileInfo fileInfo;
        var result = NativeMethods.SHGetFileInfo(
            path,
            0,
            out fileInfo,
            (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.ShFileInfo>(),
            NativeMethods.ShgfiIcon | NativeMethods.ShgfiSmallIcon);
        if (result == IntPtr.Zero || fileInfo.hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            using var icon = Icon.FromHandle(fileInfo.hIcon);
            return icon.ToBitmap();
        }
        finally
        {
            NativeMethods.DestroyIcon(fileInfo.hIcon);
        }
    }

    private static Image CreateFallbackIcon(DesktopItem item)
    {
        var bitmap = new Bitmap(18, 18);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        var backColor = item.ItemType == "文件夹"
            ? Color.FromArgb(246, 190, 66)
            : Color.FromArgb(77, 143, 255);
        using var brush = new SolidBrush(backColor);
        using var accentBrush = new SolidBrush(Color.FromArgb(230, 255, 255, 255));
        using var path = CreateRoundedRectangle(new Rectangle(1, 2, 16, 14), 4);

        graphics.FillPath(brush, path);
        graphics.FillRectangle(accentBrush, 4, 5, 10, 2);
        graphics.FillRectangle(accentBrush, 4, 9, 7, 2);
        return bitmap;
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

    private sealed class InfoStripPanel : Panel
    {
        public InfoStripPanel()
        {
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (Width <= 1 || Height <= 1)
            {
                return;
            }

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using var brush = new SolidBrush(BackColor);
            using var pen = new Pen(Color.FromArgb(206, 220, 238));

            e.Graphics.FillRectangle(brush, bounds);
            e.Graphics.DrawRectangle(pen, bounds);
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
