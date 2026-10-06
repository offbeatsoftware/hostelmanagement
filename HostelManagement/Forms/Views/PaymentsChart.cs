using System.Drawing.Drawing2D;
using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>
/// Bar chart of payments received per month of the academic year (one series, so no legend: the card title
/// names it). Recessive grid and axis, thin bars with rounded tops, labels only on the current and the
/// highest month, and a tooltip with the exact amount on every bar.
/// </summary>
public sealed class PaymentsChart : Control
{
    private const int LeftAxis = 56;
    private const int BottomAxis = 26;
    private const int TopSpace = 22;

    private static readonly Color Bar = UiTheme.Primary;
    private static readonly Color BarHover = Color.FromArgb(0, 78, 130);
    private static readonly Color Grid = Color.FromArgb(232, 228, 220);

    private readonly ToolTip _toolTip = new() { InitialDelay = 0, ReshowDelay = 0 };
    private IReadOnlyList<MonthTotal> _months = [];
    private DateTime _currentMonth = DateTime.Today;
    private int _hoverIndex = -1;

    public PaymentsChart()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
        Font = UiTheme.BodyFont;
        MinimumSize = new Size(300, 160);
    }

    public void SetData(IReadOnlyList<MonthTotal> months, DateTime today)
    {
        _months = months;
        _currentMonth = new DateTime(today.Year, today.Month, 1);
        _hoverIndex = -1;
        Invalidate();
    }

    /// <summary>The amount labelled above a bar: the current month and the highest month.</summary>
    internal IEnumerable<int> LabelledBars()
    {
        int current = _months.ToList().FindIndex(m => m.Month == _currentMonth);
        int highest = _months.Count == 0 || _months.Max(m => m.Amount) <= 0 ? -1
            : _months.ToList().FindIndex(m => m.Amount == _months.Max(x => x.Amount));
        return new[] { current, highest }.Where(i => i >= 0 && _months[i].Amount > 0).Distinct();
    }

    private Rectangle PlotArea => new(LeftAxis, TopSpace, Math.Max(Width - LeftAxis - 12, 1), Math.Max(Height - TopSpace - BottomAxis, 1));

    private decimal AxisMax()
    {
        decimal max = _months.Count == 0 ? 0 : _months.Max(m => m.Amount);
        if (max <= 0)
        {
            return 1000m;
        }
        // A "nice" top value (1, 2, 2.5 or 5 times a power of ten) so the four gridlines get round labels.
        decimal step = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)(max / 4))));
        foreach (decimal factor in new[] { 1m, 2m, 2.5m, 5m, 10m })
        {
            if (step * factor * 4 >= max)
            {
                return step * factor * 4;
            }
        }
        return step * 40;
    }

    private Rectangle BarBounds(int index, Rectangle plot, decimal axisMax)
    {
        float slot = plot.Width / (float)Math.Max(_months.Count, 1);
        int barWidth = Math.Max((int)(slot * 0.6f), 4);
        int height = (int)Math.Round((double)(_months[index].Amount / axisMax) * plot.Height);
        int x = plot.Left + (int)(slot * index + (slot - barWidth) / 2);
        return new Rectangle(x, plot.Bottom - height, barWidth, height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle plot = PlotArea;
        decimal axisMax = AxisMax();

        using var gridPen = new Pen(Grid, 1);
        using var mutedBrush = new SolidBrush(UiTheme.TextMuted);
        using var textBrush = new SolidBrush(UiTheme.TextPrimary);
        var right = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
        var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };

        for (int i = 0; i <= 4; i++)
        {
            int y = plot.Bottom - plot.Height * i / 4;
            g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            g.DrawString(Money.Compact(axisMax * i / 4), Font, mutedBrush, new RectangleF(0, y - 10, LeftAxis - 8, 20), right);
        }

        float slot = plot.Width / (float)Math.Max(_months.Count, 1);
        for (int i = 0; i < _months.Count; i++)
        {
            var labelArea = new RectangleF(plot.Left + slot * i, plot.Bottom + 5, slot, BottomAxis);
            bool isCurrent = _months[i].Month == _currentMonth;
            g.DrawString(_months[i].Label, isCurrent ? UiTheme.BodyBoldFont : Font, isCurrent ? textBrush : mutedBrush, labelArea, center);

            Rectangle bar = BarBounds(i, plot, axisMax);
            if (bar.Height <= 0)
            {
                continue;
            }
            using var brush = new SolidBrush(i == _hoverIndex ? BarHover : Bar);
            using GraphicsPath path = RoundedTop(bar, Math.Min(4, Math.Min(bar.Height, bar.Width / 2)));
            g.FillPath(brush, path);
        }

        foreach (int i in LabelledBars())
        {
            Rectangle bar = BarBounds(i, plot, axisMax);
            var area = new RectangleF(bar.X - 40, bar.Top - 20, bar.Width + 80, 18);
            g.DrawString(Money.Compact(_months[i].Amount), UiTheme.BodyBoldFont, textBrush, area, center);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Rectangle plot = PlotArea;
        float slot = plot.Width / (float)Math.Max(_months.Count, 1);
        // The whole column of a month is the hover target, not just the bar.
        int index = e.X >= plot.Left && e.X < plot.Right && e.Y >= plot.Top && e.Y <= plot.Bottom + BottomAxis
            ? Math.Min((int)((e.X - plot.Left) / slot), _months.Count - 1)
            : -1;
        if (index == _hoverIndex)
        {
            return;
        }

        _hoverIndex = index;
        Invalidate();
        if (index >= 0)
        {
            MonthTotal month = _months[index];
            _toolTip.Show($"{month.Month:MMMM yyyy}: {Money.Format(month.Amount)} received", this, e.X + 12, e.Y - 24);
        }
        else
        {
            _toolTip.Hide(this);
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverIndex = -1;
        _toolTip.Hide(this);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }
        base.Dispose(disposing);
    }

    private static GraphicsPath RoundedTop(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0)
        {
            path.AddRectangle(r);
            return path;
        }
        int d = radius * 2;
        path.AddLine(r.Left, r.Bottom, r.Left, r.Top + radius);
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddLine(r.Right, r.Top + radius, r.Right, r.Bottom);
        path.CloseFigure();
        return path;
    }
}
