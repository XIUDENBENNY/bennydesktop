using System.Drawing.Drawing2D;

namespace DesktopAssistantLite.App.Forms;

internal sealed class AppToastForm : Form
{
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
        Width = 420;
        Height = 158;
        BackColor = Color.FromArgb(9, 13, 23);
        ForeColor = Color.White;
        DoubleBuffered = true;
        Opacity = 0;
        Padding = new Padding(1);

        _captionLabel = new Label
        {
            AutoSize = false,
            Height = 20,
            ForeColor = Color.FromArgb(121, 199, 255),
            Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Bold),
            Text = "桌面助手",
        };

        _titleLabel = new Label
        {
            AutoSize = false,
            Height = 34,
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei UI", 12.5F, FontStyle.Bold),
            Text = title,
        };

        _messageLabel = new Label
        {
            AutoSize = false,
            ForeColor = Color.FromArgb(215, 226, 239),
            Font = new Font("Microsoft YaHei UI", 10.2F, FontStyle.Regular),
            Text = message,
        };

        Controls.Add(_messageLabel);
        Controls.Add(_titleLabel);
        Controls.Add(_captionLabel);

        _animationTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _animationTimer.Tick += (_, _) => AdvanceAnimation();

        Load += (_, _) =>
        {
            var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
            _targetLocation = new Point(area.Right - Width - 26, area.Top + 26);
            Location = new Point(_targetLocation.X + 38, _targetLocation.Y);
            Region = new Region(CreateRoundedRectangle(new Rectangle(0, 0, Width, Height), 22));
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

        using var cardPath = CreateRoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), 22);
        using var backgroundBrush = new LinearGradientBrush(
            ClientRectangle,
            Color.FromArgb(15, 22, 39),
            Color.FromArgb(12, 17, 30),
            LinearGradientMode.Vertical);
        using var borderPen = new Pen(Color.FromArgb(36, 61, 92));
        using var accentBrush = new SolidBrush(Color.FromArgb(52, 211, 153));
        using var iconBackBrush = new SolidBrush(Color.FromArgb(32, 53, 86, 126));
        using var iconLinePen = new Pen(Color.White, 3.2F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };

        e.Graphics.FillPath(backgroundBrush, cardPath);
        e.Graphics.DrawPath(borderPen, cardPath);
        e.Graphics.FillRectangle(accentBrush, 18, 22, 54, 5);

        var iconBounds = new Rectangle(20, 54, 48, 48);
        e.Graphics.FillEllipse(iconBackBrush, iconBounds);
        e.Graphics.DrawLine(iconLinePen, 35, 78, 43, 86);
        e.Graphics.DrawLine(iconLinePen, 43, 86, 58, 66);
        e.Graphics.FillEllipse(accentBrush, 33, 63, 8, 8);

        _captionLabel.Location = new Point(90, 18);
        _captionLabel.Width = Width - 110;
        _titleLabel.Location = new Point(90, 40);
        _titleLabel.Width = Width - 110;
        _messageLabel.Location = new Point(90, 76);
        _messageLabel.Width = Width - 116;
        _messageLabel.Height = Height - 90;
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
                var newX = Math.Max(_targetLocation.X, Left - 8);
                Location = new Point(newX, _targetLocation.Y);
                if (Opacity >= 0.98 && newX == _targetLocation.X)
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
                Location = new Point(Left + 8, _targetLocation.Y);
                if (Opacity <= 0.02)
                {
                    _animationTimer.Stop();
                    Close();
                }
                break;
        }
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
