using System.Drawing.Drawing2D;

namespace HostelManagement.Forms;

/// <summary>
/// A box to sign in with the mouse, a touchpad, a pen or a touch screen (Windows passes touch on as mouse
/// movement). The strokes are kept as a transparent image so they print cleanly on the agreement.
/// </summary>
public sealed class SignaturePad : Control
{
    private Bitmap _ink;
    private Point? _last;
    private bool _hasInk;

    public SignaturePad()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
        Cursor = Cursors.Cross;
        _ink = new Bitmap(1, 1);
    }

    /// <summary>Raised when something is drawn or the pad is cleared.</summary>
    public event EventHandler? InkChanged;

    public bool HasInk => _hasInk;

    /// <summary>Removes the signature.</summary>
    public void Clear()
    {
        NewInk();
        _hasInk = false;
        Invalidate();
        InkChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Shows a saved signature, fitted into the pad.</summary>
    public void ShowSignature(Image? signature)
    {
        NewInk();
        _hasInk = false;
        if (signature is not null)
        {
            using Graphics g = Graphics.FromImage(_ink);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            double scale = Math.Min((double)_ink.Width / signature.Width, (double)_ink.Height / signature.Height);
            int width = (int)(signature.Width * scale);
            int height = (int)(signature.Height * scale);
            g.DrawImage(signature, (_ink.Width - width) / 2, (_ink.Height - height) / 2, width, height);
            _hasInk = true;
        }
        Invalidate();
        InkChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The signature cropped to the strokes (transparent background), or null when nothing was drawn.</summary>
    public Bitmap? GetSignature()
    {
        if (!_hasInk)
        {
            return null;
        }
        Rectangle bounds = InkBounds();
        if (bounds.IsEmpty)
        {
            return null;
        }
        bounds.Inflate(6, 6);
        bounds.Intersect(new Rectangle(Point.Empty, _ink.Size));
        return _ink.Clone(bounds, _ink.PixelFormat);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NewInk();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (!_hasInk)
        {
            NewInk();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            _last = e.Location;
            DrawTo(new Point(e.X + 1, e.Y));
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_last is not null && e.Button == MouseButtons.Left)
        {
            DrawTo(e.Location);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _last = null;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // A guide line to sign on, drawn only on screen (not part of the signature).
        using (var guide = new Pen(Color.FromArgb(200, 200, 200)) { DashStyle = DashStyle.Dash })
        {
            int y = Height * 3 / 4;
            e.Graphics.DrawLine(guide, 10, y, Width - 10, y);
        }
        e.Graphics.DrawImageUnscaled(_ink, 0, 0);
        if (!_hasInk)
        {
            TextRenderer.DrawText(e.Graphics, "Sign here", Font, new Rectangle(0, 0, Width, Height * 3 / 4),
                Color.FromArgb(170, 170, 170), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        ControlPaint.DrawBorder(e.Graphics, ClientRectangle, Color.FromArgb(160, 160, 160), ButtonBorderStyle.Solid);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _ink.Dispose();
        }
        base.Dispose(disposing);
    }

    private void DrawTo(Point point)
    {
        using (Graphics g = Graphics.FromImage(_ink))
        using (var pen = new Pen(Color.FromArgb(20, 30, 90), 2.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.DrawLine(pen, _last ?? point, point);
        }
        _last = point;
        _hasInk = true;
        Invalidate();
        InkChanged?.Invoke(this, EventArgs.Empty);
    }

    private void NewInk()
    {
        _ink.Dispose();
        _ink = new Bitmap(Math.Max(Width, 1), Math.Max(Height, 1), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
    }

    /// <summary>The smallest rectangle around the drawn pixels.</summary>
    private Rectangle InkBounds()
    {
        int left = _ink.Width, top = _ink.Height, right = -1, bottom = -1;
        for (int y = 0; y < _ink.Height; y++)
        {
            for (int x = 0; x < _ink.Width; x++)
            {
                if (_ink.GetPixel(x, y).A > 0)
                {
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            }
        }
        return right < 0 ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }
}
