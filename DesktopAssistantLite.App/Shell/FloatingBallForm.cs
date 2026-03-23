using System.Drawing.Drawing2D;

namespace DesktopAssistantLite.App.Shell;

internal sealed class FloatingBallForm : Form
{
    private const int BallSize = 78;
    private const int PeekSize = 20;

    private readonly Label _memoryPercentLabel;
    private readonly Label _memoryUsageLabel;
    private readonly System.Windows.Forms.Timer _hideTimer;
    private Point _dragOffset;
    private bool _dragging;
    private bool _dockedLeft;
    private bool _dockedRight;

    public FloatingBallForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(BallSize, BallSize);
        TopMost = true;
        BackColor = Color.FromArgb(10, 15, 28);
        ForeColor = Color.White;
        DoubleBuffered = true;

        _memoryPercentLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 42,
            TextAlign = ContentAlignment.BottomCenter,
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei UI", 13.5F, FontStyle.Bold),
            Text = "0%",
            Cursor = Cursors.SizeAll,
        };

        _memoryUsageLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopCenter,
            ForeColor = Color.FromArgb(207, 226, 255),
            Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular),
            Text = "0 MB",
            Cursor = Cursors.SizeAll,
        };

        Controls.Add(_memoryUsageLabel);
        Controls.Add(_memoryPercentLabel);

        _hideTimer = new System.Windows.Forms.Timer { Interval = 2200 };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            if (!ClientRectangle.Contains(PointToClient(Cursor.Position)))
            {
                HideToEdge();
            }
        };

        BindPointerEvents(this);
        BindPointerEvents(_memoryPercentLabel);
        BindPointerEvents(_memoryUsageLabel);
    }

    public event EventHandler? PrimaryActionRequested;

    public bool AutoHideEnabled { get; set; } = true;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var createParams = base.CreateParams;
            createParams.ExStyle |= NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate;
            return createParams;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var outerPath = CreateCirclePath(new Rectangle(2, 2, Width - 5, Height - 5));
        using var fillBrush = new LinearGradientBrush(
            new Rectangle(0, 0, Width, Height),
            Color.FromArgb(18, 48, 95),
            Color.FromArgb(19, 148, 212),
            LinearGradientMode.ForwardDiagonal);
        using var shadowBrush = new SolidBrush(Color.FromArgb(60, 6, 10, 22));
        using var borderPen = new Pen(Color.FromArgb(180, 225, 245, 255), 2F);
        using var innerPen = new Pen(Color.FromArgb(42, 255, 255, 255), 10F);

        e.Graphics.FillEllipse(shadowBrush, 4, 5, Width - 10, Height - 10);
        e.Graphics.FillPath(fillBrush, outerPath);
        e.Graphics.DrawPath(innerPen, CreateCirclePath(new Rectangle(11, 11, Width - 23, Height - 23)));
        e.Graphics.DrawPath(borderPen, outerPath);
    }

    public void SetMemoryUsage(int percentage, long usedMb)
    {
        _memoryPercentLabel.Text = $"{percentage}%";
        _memoryUsageLabel.Text = $"{usedMb} MB";
    }

    public void SnapToWorkingArea()
    {
        var screen = Screen.FromPoint(Location);
        var workingArea = screen.WorkingArea;
        var centerX = Left + (Width / 2);
        var dockLeft = centerX < workingArea.Left + (workingArea.Width / 2);

        Top = Math.Max(workingArea.Top + 20, Math.Min(Top, workingArea.Bottom - Height - 20));
        Left = dockLeft ? workingArea.Left + 8 : workingArea.Right - Width - 8;
        _dockedLeft = dockLeft;
        _dockedRight = !dockLeft;
    }

    public void RevealFromEdge()
    {
        if (_dockedLeft)
        {
            Left = Screen.FromPoint(Location).WorkingArea.Left + 8;
        }
        else if (_dockedRight)
        {
            Left = Screen.FromPoint(Location).WorkingArea.Right - Width - 8;
        }
    }

    public void HideToEdge()
    {
        if (!AutoHideEnabled || !Visible)
        {
            return;
        }

        if (_dockedLeft)
        {
            Left = Screen.FromPoint(Location).WorkingArea.Left - Width + PeekSize;
        }
        else if (_dockedRight)
        {
            Left = Screen.FromPoint(Location).WorkingArea.Right - PeekSize;
        }
    }

    private void ScheduleHide()
    {
        if (!AutoHideEnabled || _dragging)
        {
            return;
        }

        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void BindPointerEvents(Control control)
    {
        control.MouseDown += OnMouseDown;
        control.MouseMove += OnMouseMove;
        control.MouseUp += OnMouseUp;
        control.MouseEnter += (_, _) => RevealFromEdge();
        control.MouseLeave += (_, _) => ScheduleHide();
        control.DoubleClick += (_, _) => PrimaryActionRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _dragging = true;
        _dragOffset = e.Location;
        RevealFromEdge();
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var screenPoint = PointToScreen(e.Location);
        Location = new Point(screenPoint.X - _dragOffset.X, screenPoint.Y - _dragOffset.Y);
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        SnapToWorkingArea();
        ScheduleHide();
    }

    private static GraphicsPath CreateCirclePath(Rectangle bounds)
    {
        var path = new GraphicsPath();
        path.AddEllipse(bounds);
        return path;
    }
}
