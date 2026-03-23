using System.Drawing.Drawing2D;

namespace DesktopAssistantLite.App.Forms;

internal sealed class AppToastForm : Form
{
    private readonly System.Windows.Forms.Timer _closeTimer;

    public AppToastForm(string title, string message)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Width = 460;
        Height = 156;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        Padding = new Padding(0);

        var container = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(0),
        };

        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(18, 24, 38),
            Padding = new Padding(22, 18, 22, 18),
        };
        card.Paint += (_, args) =>
        {
            args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
            using var path = CreateRoundedRectangle(bounds, 20);
            using var backgroundBrush = new SolidBrush(Color.FromArgb(18, 24, 38));
            using var accentBrush = new SolidBrush(Color.FromArgb(37, 99, 235));
            using var borderPen = new Pen(Color.FromArgb(36, 53, 84));

            args.Graphics.FillPath(backgroundBrush, path);
            args.Graphics.DrawPath(borderPen, path);
            args.Graphics.FillRectangle(accentBrush, 22, 22, 52, 5);
        };

        var titleLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 34,
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
            Text = title,
        };

        var messageLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(216, 224, 235),
            Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Regular),
            Text = message,
        };

        card.Controls.Add(messageLabel);
        card.Controls.Add(titleLabel);
        container.Controls.Add(card);
        Controls.Add(container);

        Load += (_, _) =>
        {
            var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
            Location = new Point(
                area.Left + (area.Width - Width) / 2,
                area.Top + (area.Height - Height) / 2);
            Region = new Region(CreateRoundedRectangle(new Rectangle(0, 0, Width, Height), 20));
        };

        _closeTimer = new System.Windows.Forms.Timer { Interval = 2200 };
        _closeTimer.Tick += (_, _) =>
        {
            _closeTimer.Stop();
            Close();
        };
        Shown += (_, _) => _closeTimer.Start();
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _closeTimer.Dispose();
        }

        base.Dispose(disposing);
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
}
