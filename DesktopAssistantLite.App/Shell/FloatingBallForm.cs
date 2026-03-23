using System.Drawing.Drawing2D;

namespace DesktopAssistantLite.App.Shell;

internal sealed class FloatingBallForm : Form
{
    private const int BallSize = 77;
    private const int PeekSize = 10;
    private const int DragThreshold = 6;
    private const int CornerCut = 13;

    private readonly System.Windows.Forms.Timer _hideTimer;
    private readonly System.Windows.Forms.Timer _animationTimer;
    private Point _mouseDownScreenPoint;
    private Point _dragStartLocation;
    private bool _mousePressed;
    private bool _dragging;
    private bool _pointerInside;
    private bool _dockedLeft;
    private bool _dockedRight;
    private int _memoryPercentage;
    private long _usedMb;
    private float _idlePhase;
    private float _boostRotation;
    private VisualState _visualState = VisualState.Idle;
    private DateTime _stateUntilUtc;

    public FloatingBallForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(BallSize, BallSize);
        TopMost = true;
        BackColor = Color.FromArgb(10, 16, 30);
        ForeColor = Color.White;
        DoubleBuffered = true;
        Cursor = Cursors.Hand;
        Opacity = 0.8;

        _hideTimer = new System.Windows.Forms.Timer { Interval = 2200 };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            if (!_pointerInside && !_dragging)
            {
                HideToEdge();
            }
        };

        _animationTimer = new System.Windows.Forms.Timer { Interval = 33 };
        _animationTimer.Tick += (_, _) =>
        {
            _idlePhase += 0.08F;
            if (_idlePhase > MathF.PI * 2)
            {
                _idlePhase -= MathF.PI * 2;
            }

            if (_visualState == VisualState.Boosting)
            {
                _boostRotation += 8F;
                if (_boostRotation >= 360F)
                {
                    _boostRotation -= 360F;
                }
            }

            if (_visualState is VisualState.Success or VisualState.Failure &&
                DateTime.UtcNow >= _stateUntilUtc)
            {
                _visualState = VisualState.Idle;
            }

            Invalidate();
        };
        _animationTimer.Start();
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

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        UpdateRoundRegion();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateRoundRegion();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _pointerInside = true;
        RevealFromEdge();
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _pointerInside = false;
        ScheduleHide();
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _mousePressed = true;
        _dragging = false;
        _mouseDownScreenPoint = PointToScreen(e.Location);
        _dragStartLocation = Location;
        RevealFromEdge();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_mousePressed)
        {
            return;
        }

        var currentScreenPoint = PointToScreen(e.Location);
        var dx = currentScreenPoint.X - _mouseDownScreenPoint.X;
        var dy = currentScreenPoint.Y - _mouseDownScreenPoint.Y;

        if (!_dragging && (Math.Abs(dx) >= DragThreshold || Math.Abs(dy) >= DragThreshold))
        {
            _dragging = true;
            Cursor = Cursors.SizeAll;
        }

        if (_dragging)
        {
            Location = new Point(_dragStartLocation.X + dx, _dragStartLocation.Y + dy);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_mousePressed)
        {
            return;
        }

        _mousePressed = false;

        if (_dragging)
        {
            _dragging = false;
            Cursor = Cursors.Hand;
            SnapToWorkingArea();
            ScheduleHide();
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _mousePressed = false;
        _dragging = false;
        Cursor = Cursors.Hand;
        RevealFromEdge();
        PrimaryActionRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var pulse = 0.5F + (MathF.Sin(_idlePhase) + 1F) * 0.5F;
        var shellBounds = new Rectangle(3, 3, Width - 7, Height - 7);
        var coreBounds = new Rectangle(8, 8, Width - 17, Height - 17);
        var ringBounds = new Rectangle(12, 12, Width - 25, Height - 25);
        var textBounds = new Rectangle(15, 15, Width - 31, Height - 31);

        DrawGlow(e.Graphics, shellBounds, pulse);

        using var shellPath = CreateCyberPath(shellBounds, CornerCut);
        using var corePath = CreateCyberPath(coreBounds, CornerCut - 4);
        using var fillBrush = new LinearGradientBrush(
            shellBounds,
            GetTopColor(),
            GetBottomColor(),
            LinearGradientMode.Vertical);
        using var coreBrush = new LinearGradientBrush(
            coreBounds,
            Color.FromArgb(10, 27, 53),
            Color.FromArgb(6, 14, 31),
            LinearGradientMode.ForwardDiagonal);
        using var borderPen = new Pen(Color.FromArgb(118, 87, 202, 255), 1.4F);
        using var coreBorderPen = new Pen(Color.FromArgb(42, 102, 242, 255), 1F);
        using var scanPen = new Pen(Color.FromArgb(110, 76, 226, 255), 1.7F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        using var cyanAccentPen = new Pen(Color.FromArgb(142, 84, 219, 255), 1.3F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };

        e.Graphics.FillPath(fillBrush, shellPath);
        e.Graphics.FillPath(coreBrush, corePath);
        e.Graphics.DrawPath(borderPen, shellPath);
        e.Graphics.DrawPath(coreBorderPen, corePath);
        e.Graphics.DrawArc(scanPen, new Rectangle(18, 10, Width - 37, 9), 180, 150);
        e.Graphics.DrawLine(cyanAccentPen, 11, 16, 16, 11);
        e.Graphics.DrawLine(cyanAccentPen, Width - 17, 11, Width - 12, 16);
        e.Graphics.DrawLine(cyanAccentPen, 11, Height - 16, 16, Height - 11);
        e.Graphics.DrawLine(cyanAccentPen, Width - 17, Height - 11, Width - 12, Height - 16);

        DrawProgressRing(e.Graphics, ringBounds, pulse);
        DrawContent(e.Graphics, textBounds);
    }

    public void SetMemoryUsage(int percentage, long usedMb)
    {
        _memoryPercentage = Math.Max(0, percentage);
        _usedMb = Math.Max(0, usedMb);
        Invalidate();
    }

    public void StartBoostAnimation()
    {
        _visualState = VisualState.Boosting;
        _boostRotation = 0F;
        Invalidate();
    }

    public void CompleteBoostAnimation()
    {
        _visualState = VisualState.Success;
        _stateUntilUtc = DateTime.UtcNow.AddMilliseconds(900);
        Invalidate();
    }

    public void FailBoostAnimation()
    {
        _visualState = VisualState.Failure;
        _stateUntilUtc = DateTime.UtcNow.AddMilliseconds(900);
        Invalidate();
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
        if (!AutoHideEnabled || !Visible || _visualState == VisualState.Boosting)
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
        if (!AutoHideEnabled || _dragging || _visualState == VisualState.Boosting)
        {
            return;
        }

        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void DrawGlow(Graphics graphics, Rectangle outerBounds, float pulse)
    {
        var glowColor = _visualState switch
        {
            VisualState.Success => Color.FromArgb(88, 208, 255),
            VisualState.Failure => Color.FromArgb(235, 110, 110),
            _ => Color.FromArgb(58, 126, 235),
        };

        for (var i = 0; i < 2; i++)
        {
            var spread = 1 + (i * 2) + (int)Math.Round(pulse * 0.6F);
            var bounds = Rectangle.Inflate(outerBounds, spread, spread);
            var alpha = Math.Max(4, 8 - (i * 2));
            using var brush = new SolidBrush(Color.FromArgb(alpha, glowColor));
            using var glowPath = CreateCyberPath(bounds, CornerCut + spread);
            graphics.FillPath(brush, glowPath);
        }
    }

    private void DrawProgressRing(Graphics graphics, Rectangle ringBounds, float pulse)
    {
        using var trackPen = new Pen(Color.FromArgb(12, 255, 255, 255), 2.7F);
        graphics.DrawArc(trackPen, ringBounds, 0, 360);

        var accentColor = _visualState switch
        {
            VisualState.Success => Color.FromArgb(136, 230, 255),
            VisualState.Failure => Color.FromArgb(255, 148, 148),
            _ => Color.FromArgb(86, 215, 255),
        };
        var startAngle = _visualState == VisualState.Boosting ? -90F + _boostRotation : -90F;
        var sweepAngle = _visualState switch
        {
            VisualState.Boosting => 72F + (pulse * 26F),
            VisualState.Success => 360F,
            VisualState.Failure => 360F,
            _ => Math.Clamp(_memoryPercentage, 4, 100) * 3.6F,
        };

        using var accentPen = new Pen(Color.FromArgb(200, accentColor), 2.7F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        graphics.DrawArc(accentPen, ringBounds, startAngle, sweepAngle);
    }

    private void DrawContent(Graphics graphics, Rectangle textBounds)
    {
        switch (_visualState)
        {
            case VisualState.Boosting:
                DrawBolt(graphics, Color.White);
                DrawFooter(graphics, "清理中");
                break;
            case VisualState.Success:
                DrawCheck(graphics, Color.White);
                DrawFooter(graphics, "完成");
                break;
            case VisualState.Failure:
                DrawCross(graphics, Color.White);
                DrawFooter(graphics, "重试");
                break;
            default:
                DrawIdleContent(graphics, textBounds);
                break;
        }
    }

    private void DrawIdleContent(Graphics graphics, Rectangle textBounds)
    {
        using var valueFont = new Font("Microsoft YaHei UI", 11.2F, FontStyle.Bold);
        using var percentFont = new Font("Microsoft YaHei UI", 5.8F, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.White);
        using var subBrush = new SolidBrush(Color.FromArgb(198, 233, 255));

        var valueText = _memoryPercentage.ToString();
        var valueSize = graphics.MeasureString(valueText, valueFont);
        var totalWidth = valueSize.Width + graphics.MeasureString("%", percentFont).Width - 2;
        var startX = (Width - totalWidth) / 2F;

        graphics.DrawString(valueText, valueFont, textBrush, startX, textBounds.Top + 4F);
        graphics.DrawString("%", percentFont, subBrush, startX + valueSize.Width - 1, textBounds.Top + 9F);
    }

    private void DrawFooter(Graphics graphics, string text)
    {
        using var font = new Font("Microsoft YaHei UI", 6.5F, FontStyle.Bold);
        using var brush = new SolidBrush(Color.FromArgb(231, 244, 255));
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };
        graphics.DrawString(text, font, brush, new RectangleF(0, 43, Width, 10), format);
    }

    private void DrawBolt(Graphics graphics, Color color)
    {
        var points = new[]
        {
            new PointF(45, 21),
            new PointF(37, 36),
            new PointF(43, 36),
            new PointF(34, 53),
            new PointF(39, 40),
            new PointF(33, 40),
        };

        using var brush = new SolidBrush(color);
        graphics.FillPolygon(brush, points);
    }

    private void DrawCheck(Graphics graphics, Color color)
    {
        using var pen = new Pen(color, 4F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        graphics.DrawLines(pen, new[] { new Point(26, 39), new Point(34, 47), new Point(49, 30) });
    }

    private void DrawCross(Graphics graphics, Color color)
    {
        using var pen = new Pen(color, 3.8F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        graphics.DrawLine(pen, 28, 30, 48, 50);
        graphics.DrawLine(pen, 48, 30, 28, 50);
    }

    private Color GetTopColor()
    {
        return _visualState switch
        {
            VisualState.Success => Color.FromArgb(20, 112, 179),
            VisualState.Failure => Color.FromArgb(140, 68, 78),
            _ => Color.FromArgb(6, 14, 34),
        };
    }

    private Color GetBottomColor()
    {
        return _visualState switch
        {
            VisualState.Success => Color.FromArgb(18, 138, 232),
            VisualState.Failure => Color.FromArgb(207, 86, 98),
            _ => Color.FromArgb(10, 52, 112),
        };
    }

    private void UpdateRoundRegion()
    {
        using var path = CreateCyberPath(new Rectangle(0, 0, Width - 1, Height - 1), CornerCut);
        Region = new Region(path);
    }

    private static GraphicsPath CreateCyberPath(Rectangle bounds, int cut)
    {
        var path = new GraphicsPath();
        var left = bounds.Left;
        var top = bounds.Top;
        var right = bounds.Right;
        var bottom = bounds.Bottom;

        path.AddPolygon(new[]
        {
            new Point(left + cut, top),
            new Point(right - cut, top),
            new Point(right, top + cut),
            new Point(right, bottom - cut),
            new Point(right - cut, bottom),
            new Point(left + cut, bottom),
            new Point(left, bottom - cut),
            new Point(left, top + cut),
        });
        path.CloseFigure();
        return path;
    }

    private enum VisualState
    {
        Idle,
        Boosting,
        Success,
        Failure,
    }
}
