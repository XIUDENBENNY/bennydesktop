using System.Diagnostics;
using DesktopAssistantLite.App.Services;

namespace DesktopAssistantLite.App.Forms;

internal sealed class SearchForm : Form
{
    private readonly SearchIndexService _searchIndexService;
    private readonly IReadOnlyList<string> _searchPaths;
    private TextBox _queryTextBox = null!;
    private Button _searchButton = null!;
    private Button _rebuildButton = null!;
    private Label _statusLabel = null!;
    private ListView _resultListView = null!;

    public SearchForm(SearchIndexService searchIndexService, IReadOnlyList<string> searchPaths)
    {
        _searchIndexService = searchIndexService;
        _searchPaths = searchPaths;

        Text = "搜索文件";
        Width = 1060;
        Height = 720;
        MinimumSize = new Size(900, 620);
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
        var resultCard = BuildResultCard();

        rootPanel.Controls.Add(resultCard);
        rootPanel.Controls.Add(headerCard);
        Controls.Add(rootPanel);
    }

    public Task FocusSearchAsync()
    {
        _queryTextBox.Focus();
        _queryTextBox.SelectAll();
        return Task.CompletedTask;
    }

    private Control BuildHeaderCard()
    {
        var card = new Panel
        {
            Dock = DockStyle.Top,
            Height = 170,
            BackColor = Color.White,
            Padding = new Padding(22, 18, 22, 18),
        };

        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 36,
            Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Text = "搜索文件",
        };

        _statusLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(0, 4, 0, 8),
            ForeColor = Color.FromArgb(100, 116, 139),
            Text = $"索引目录：{string.Join("  |  ", _searchPaths)}",
        };

        var inputGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 46,
            ColumnCount = 3,
            Margin = new Padding(0),
        };
        inputGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        inputGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
        inputGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132F));

        _queryTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 12, 4),
            Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Regular),
        };
        _queryTextBox.KeyDown += async (_, args) =>
        {
            if (args.KeyCode == Keys.Enter)
            {
                args.SuppressKeyPress = true;
                await RunSearchAsync();
            }
        };

        _searchButton = CreateActionButton("搜索", Color.FromArgb(25, 113, 194), Color.White);
        _searchButton.Click += async (_, _) => await RunSearchAsync();

        _rebuildButton = CreateActionButton("重建索引", Color.FromArgb(241, 245, 249), Color.FromArgb(30, 41, 59));
        _rebuildButton.Click += async (_, _) => await RebuildIndexAsync();

        inputGrid.Controls.Add(_queryTextBox, 0, 0);
        inputGrid.Controls.Add(_searchButton, 1, 0);
        inputGrid.Controls.Add(_rebuildButton, 2, 0);

        card.Controls.Add(inputGrid);
        card.Controls.Add(_statusLabel);
        card.Controls.Add(titleLabel);
        return WrapCard(card, addBottomSpacing: true);
    }

    private Control BuildResultCard()
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(18),
        };

        var hintLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = Color.FromArgb(100, 116, 139),
            Text = "双击结果可直接打开文件。",
        };

        _resultListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.None,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular),
        };
        _resultListView.Columns.Add("名称", 260);
        _resultListView.Columns.Add("目录", 520);
        _resultListView.Columns.Add("修改时间", 190);
        _resultListView.Resize += (_, _) => ResizeColumns();
        _resultListView.DoubleClick += (_, _) => OpenSelectedItem();

        card.Controls.Add(_resultListView);
        card.Controls.Add(hintLabel);
        return WrapCard(card, addBottomSpacing: false);
    }

    private static Button CreateActionButton(string text, Color backColor, Color foreColor)
    {
        var button = new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 4),
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
            Dock = content.Dock == DockStyle.Top ? DockStyle.Top : DockStyle.Fill,
            BackColor = Color.Transparent,
            Height = content.Dock == DockStyle.Top ? content.Height + (addBottomSpacing ? 16 : 0) : 0,
            Padding = addBottomSpacing ? new Padding(0, 0, 0, 16) : Padding.Empty,
        };
        content.Dock = DockStyle.Fill;
        host.Controls.Add(content);
        return host;
    }

    private void ResizeColumns()
    {
        if (_resultListView.Columns.Count == 0)
        {
            return;
        }

        var width = Math.Max(_resultListView.ClientSize.Width - 6, 700);
        _resultListView.Columns[0].Width = (int)(width * 0.24);
        _resultListView.Columns[1].Width = (int)(width * 0.52);
        _resultListView.Columns[2].Width = width - _resultListView.Columns[0].Width - _resultListView.Columns[1].Width;
    }

    private async Task RunSearchAsync()
    {
        var query = _queryTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            _statusLabel.Text = "请输入要搜索的关键字。";
            return;
        }

        _searchButton.Enabled = false;
        _statusLabel.Text = "搜索中...";
        _resultListView.Items.Clear();

        try
        {
            var results = await _searchIndexService.SearchAsync(query);
            foreach (var result in results)
            {
                var item = new ListViewItem(result.Name);
                item.SubItems.Add(result.DirectoryPath);
                item.SubItems.Add(result.LastWriteTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                item.Tag = result.FullPath;
                _resultListView.Items.Add(item);
            }

            _statusLabel.Text = $"搜索完成，共 {results.Count} 条结果。";
            ResizeColumns();
        }
        finally
        {
            _searchButton.Enabled = true;
        }
    }

    private async Task RebuildIndexAsync()
    {
        _rebuildButton.Enabled = false;
        _statusLabel.Text = "正在重建索引...";
        try
        {
            await _searchIndexService.RebuildAsync(_searchPaths);
            _statusLabel.Text = "索引重建完成。";
        }
        finally
        {
            _rebuildButton.Enabled = true;
        }
    }

    private void OpenSelectedItem()
    {
        if (_resultListView.SelectedItems.Count == 0 || _resultListView.SelectedItems[0].Tag is not string path)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"打开失败：{ex.Message}", "搜索文件", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
