using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace DesktopAssistantLite.App.Forms;

internal sealed class CaptureOverlayForm : Form
{
    private readonly Bitmap _screenBitmap;
    private readonly Rectangle _virtualBounds;
    private readonly Font _hintFont;
    private readonly Font _sizeFont;
    private Point _selectionStart;
    private Rectangle _selectionRect;
    private bool _selecting;

    private CaptureOverlayForm(Bitmap screenBitmap, Rectangle virtualBounds)
    {
        _screenBitmap = screenBitmap;
        _virtualBounds = virtualBounds;
        _hintFont = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
        _sizeFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);

        DoubleBuffered = true;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        Bounds = virtualBounds;
        Cursor = Cursors.Cross;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;
        BackColor = Color.Black;

        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        Shown += (_, _) => Activate();
        KeyDown += (_, args) =>
        {
            if (args.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
            else if (args.KeyCode == Keys.Enter && _selectionRect.Width >= 4 && _selectionRect.Height >= 4)
            {
                CapturedImage = _screenBitmap.Clone(_selectionRect, PixelFormat.Format32bppArgb);
                DialogResult = DialogResult.OK;
                Close();
            }
        };
    }

    public Bitmap? CapturedImage { get; private set; }

    public static Bitmap? CaptureRegion()
    {
        var screenBounds = SystemInformation.VirtualScreen;
        using var fullScreen = new Bitmap(screenBounds.Width, screenBounds.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(fullScreen))
        {
            graphics.CopyFromScreen(screenBounds.Location, Point.Empty, screenBounds.Size);
        }

        using var overlay = new CaptureOverlayForm((Bitmap)fullScreen.Clone(), screenBounds);
        return overlay.ShowDialog() == DialogResult.OK ? overlay.CapturedImage : null;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawImageUnscaled(_screenBitmap, 0, 0);

        if (_selectionRect.Width > 0 && _selectionRect.Height > 0)
        {
            DrawSelectionMask(e.Graphics);
            DrawSelectionBorder(e.Graphics);
            DrawSelectionSizeBadge(e.Graphics);
        }
        else
        {
            DrawTopHint(e.Graphics);
        }

        base.OnPaint(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _screenBitmap.Dispose();
            _hintFont.Dispose();
            _sizeFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private void DrawSelectionMask(Graphics graphics)
    {
        using var overlayBrush = new SolidBrush(Color.FromArgb(88, 8, 15, 28));

        var topRect = new Rectangle(0, 0, Width, _selectionRect.Top);
        var leftRect = new Rectangle(0, _selectionRect.Top, _selectionRect.Left, _selectionRect.Height);
        var rightRect = new Rectangle(_selectionRect.Right, _selectionRect.Top, Width - _selectionRect.Right, _selectionRect.Height);
        var bottomRect = new Rectangle(0, _selectionRect.Bottom, Width, Height - _selectionRect.Bottom);

        graphics.FillRectangle(overlayBrush, topRect);
        graphics.FillRectangle(overlayBrush, leftRect);
        graphics.FillRectangle(overlayBrush, rightRect);
        graphics.FillRectangle(overlayBrush, bottomRect);
    }

    private void DrawSelectionBorder(Graphics graphics)
    {
        using var borderPen = new Pen(Color.FromArgb(37, 99, 235), 2F);
        using var anchorBrush = new SolidBrush(Color.White);
        graphics.DrawRectangle(borderPen, _selectionRect);

        var anchors = new[]
        {
            new Rectangle(_selectionRect.Left - 3, _selectionRect.Top - 3, 6, 6),
            new Rectangle(_selectionRect.Right - 3, _selectionRect.Top - 3, 6, 6),
            new Rectangle(_selectionRect.Left - 3, _selectionRect.Bottom - 3, 6, 6),
            new Rectangle(_selectionRect.Right - 3, _selectionRect.Bottom - 3, 6, 6),
        };

        foreach (var anchor in anchors)
        {
            graphics.FillEllipse(anchorBrush, anchor);
        }
    }

    private void DrawSelectionSizeBadge(Graphics graphics)
    {
        var badgeText = $"{_selectionRect.Width} × {_selectionRect.Height}";
        var badgeSize = TextRenderer.MeasureText(badgeText, _sizeFont);
        var badgeRect = new Rectangle(
            Math.Max(12, _selectionRect.Left),
            Math.Max(12, _selectionRect.Top - badgeSize.Height - 12),
            badgeSize.Width + 16,
            badgeSize.Height + 8);

        using var badgeBrush = new SolidBrush(Color.FromArgb(225, 15, 23, 42));
        using var path = CreateRoundedRectangle(badgeRect, 8);
        graphics.FillPath(badgeBrush, path);
        TextRenderer.DrawText(
            graphics,
            badgeText,
            _sizeFont,
            badgeRect,
            Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private void DrawTopHint(Graphics graphics)
    {
        const string hint = "拖动选择区域，Enter 确认，Esc 取消";
        var hintSize = TextRenderer.MeasureText(hint, _hintFont);
        var hintRect = new Rectangle((Width - hintSize.Width - 30) / 2, 20, hintSize.Width + 30, hintSize.Height + 12);

        using var hintBrush = new SolidBrush(Color.FromArgb(190, 15, 23, 42));
        using var path = CreateRoundedRectangle(hintRect, 14);
        graphics.FillPath(hintBrush, path);
        TextRenderer.DrawText(
            graphics,
            hint,
            _hintFont,
            hintRect,
            Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _selecting = true;
        _selectionStart = e.Location;
        _selectionRect = Rectangle.Empty;
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_selecting)
        {
            return;
        }

        _selectionRect = Rectangle.FromLTRB(
            Math.Min(_selectionStart.X, e.X),
            Math.Min(_selectionStart.Y, e.Y),
            Math.Max(_selectionStart.X, e.X),
            Math.Max(_selectionStart.Y, e.Y));
        Invalidate();
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (!_selecting)
        {
            return;
        }

        _selecting = false;
        if (_selectionRect.Width < 4 || _selectionRect.Height < 4)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        CapturedImage = _screenBitmap.Clone(_selectionRect, PixelFormat.Format32bppArgb);
        DialogResult = DialogResult.OK;
        Close();
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
