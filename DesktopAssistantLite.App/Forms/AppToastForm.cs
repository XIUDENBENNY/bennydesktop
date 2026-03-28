using System.Drawing.Drawing2D;

namespace DesktopAssistantLite.App.Forms;

internal sealed class AppToastForm : Form
{
    private const int CornerRadius = 20;

    private readonly Label _captionLabel;
    private readonly Label _titleLabel;
    private readonly Label _messageLabel;
    private readonly System.Windows.Forms.Timer _animationTimer;
    private Point _targetLocation;
    private ToastPhase _phase = ToastPhase.Showing;
    private int _holdTicks = 110;

    public AppToastForm(string title, string message)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Width = 436;
        Height = 152;
        BackColor = Color.FromArgb(248, 251, 255);
        ForeColor = Color.FromArgb(20, 44, 86);
        DoubleBuffered = true;
        Opacity = 0;
        Padding = new Padding(1);

        _captionLabel = new Label
        {
            AutoSize = false,
            Height = 20,
            ForeColor = Color.FromArgb(58, 108, 184),
            Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Bold),
            Text = "桌面收纳盒",
        };

        _titleLabel = new Label
        {
            AutoSize = false,
            Height = 30,
            ForeColor = Color.FromArgb(18, 44, 88),
            Font = new Font("Microsoft YaHei UI", 12.4F, FontStyle.Bold),
            Text = title,
        };

        _messageLabel = new Label
        {
            AutoSize = false,
            ForeColor = Color.FromArgb(78, 97, 128),
            Font = new Font("Microsoft YaHei UI", 9.9F, FontStyle.Regular),
            Text = message,
        };

        Controls.Add(_messageLabel);
        Controls.Add(_titleLabel);
        Controls.Add(_captionLabel);

        _animationTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _animationTimer.Tick += (_, _) => AdvanceAnimation();

        Load += (_, _) =>
        {
            var area = Screen.FromPoint(Cursor.Position).WorkingArea;
            _targetLocation = new Point(
                area.Left + (area.Width - Width) / 2,
                area.Top + (area.Height - Height) / 2);
            Location = new Point(_targetLocation.X, _targetLocation.Y + 16);
            Region = new Region(CreateRoundedRectangle(new Rectangle(0, 0, Width, Height), CornerRadius));
        };
        Shown += (_, _) => _animationTimer.Start();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate;
            return cp;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var cardPath = CreateRoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius);
        using var cardBrush = new SolidBrush(Color.FromArgb(248, 251, 255));
        using var borderPen = new Pen(Color.FromArgb(206, 220, 238));
        using var accentBrush = new SolidBrush(Color.FromArgb(33, 91, 170));
        using var iconBackBrush = new SolidBrush(Color.FromArgb(28, 98, 181));
        using var accentTileBrush = new SolidBrush(Color.FromArgb(194, 230, 255));
        using var whiteBrush = new SolidBrush(Color.White);

        e.Graphics.FillPath(cardBrush, cardPath);
        e.Graphics.DrawPath(borderPen, cardPath);
        e.Graphics.FillRectangle(accentBrush, 0, 0, Width, 6);

        using var iconPath = CreateRoundedRectangle(new Rectangle(22, 26, 48, 48), 14);
        e.Graphics.FillPath(iconBackBrush, iconPath);
        DrawBrandIcon(e.Graphics, whiteBrush, accentTileBrush, 31, 35);

        _captionLabel.Location = new Point(92, 24);
        _captionLabel.Width = Width - 118;
        _titleLabel.Location = new Point(92, 46);
        _titleLabel.Width = Width - 118;
        _messageLabel.Location = new Point(92, 80);
        _messageLabel.Width = Width - 118;
        _messageLabel.Height = Height - 96;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animationTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void AdvanceAnimation()
    {
        switch (_phase)
        {
            case ToastPhase.Showing:
                Opacity = Math.Min(0.98, Opacity + 0.14);
                var newY = Math.Max(_targetLocation.Y, Top - 5);
                Location = new Point(_targetLocation.X, newY);
                if (Opacity >= 0.98 && newY == _targetLocation.Y)
                {
                    _phase = ToastPhase.Holding;
                }
                break;

            case ToastPhase.Holding:
                _holdTicks--;
                if (_holdTicks <= 0)
                {
                    _phase = ToastPhase.Hiding;
                }
                break;

            case ToastPhase.Hiding:
                Opacity = Math.Max(0, Opacity - 0.16);
                Location = new Point(_targetLocation.X, Top - 5);
                if (Opacity <= 0.02)
                {
                    _animationTimer.Stop();
                    Close();
                }
                break;
        }
    }

    private static void DrawBrandIcon(Graphics graphics, Brush tileBrush, Brush accentBrush, int x, int y)
    {
        FillRoundedTile(graphics, tileBrush, x, y, 10, 10, 3);
        FillRoundedTile(graphics, tileBrush, x + 14, y, 10, 10, 3);
        FillRoundedTile(graphics, tileBrush, x, y + 14, 10, 10, 3);
        FillRoundedTile(graphics, accentBrush, x + 14, y + 14, 10, 10, 3);
    }

    private static void FillRoundedTile(Graphics graphics, Brush brush, int x, int y, int width, int height, int radius)
    {
        using var path = CreateRoundedRectangle(new Rectangle(x, y, width, height), radius);
        graphics.FillPath(brush, path);
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

    private enum ToastPhase
    {
        Showing,
        Holding,
        Hiding,
    }
}
