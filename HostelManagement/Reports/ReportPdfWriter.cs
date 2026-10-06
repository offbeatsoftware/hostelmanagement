using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using static HostelManagement.Reports.PdfText;

namespace HostelManagement.Reports;

/// <summary>Writes any <see cref="ReportTable"/> as an A4 PDF with a repeated header row and page numbers.</summary>
public static class ReportPdfWriter
{
    private const double Margin = 32;
    private const double RowHeight = 16;

    public static void Write(ReportTable report, string path)
    {
        using var document = new PdfDocument();
        document.Info.Title = report.Title;
        document.Info.Author = AppInfo.BusinessName;

        XFont title = Font(15, bold: true);
        XFont body = Font(8);
        XFont bold = Font(8, bold: true);
        XFont small = Font(7);
        var muted = new XSolidBrush(Muted);

        XGraphics g = null!;
        double y = 0;
        double width = 0;
        double bottom = 0;
        double[] columns = [];

        void NewPage()
        {
            g?.Dispose();
            PdfPage page = document.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            page.Orientation = report.Landscape ? PdfSharp.PageOrientation.Landscape : PdfSharp.PageOrientation.Portrait;
            g = XGraphics.FromPdfPage(page);
            width = page.Width.Point - 2 * Margin;
            bottom = page.Height.Point - Margin - 18;
            double totalWeight = report.Columns.Sum(c => c.Width);
            columns = report.Columns.Select(c => width * c.Width / totalWeight).ToArray();
            y = Margin;

            if (document.PageCount == 1)
            {
                g.DrawString(AppInfo.BusinessName, title, new XSolidBrush(Terracotta), Margin, y + 13);
                g.DrawString(report.Title.ToUpperInvariant(), title, XBrushes.Black, new XRect(Margin, y, width, 18), XStringFormats.TopRight);
                y += 22;
                foreach (string line in report.Subtitles.Where(s => s.Length > 0))
                {
                    g.DrawString(Fit(g, line, body, width), body, muted, Margin, y + 8);
                    y += 12;
                }
                y += 4;
                g.DrawLine(new XPen(Terracotta, 1.2), Margin, y, Margin + width, y);
                y += 6;
            }

            g.DrawRectangle(new XSolidBrush(HeaderFill), Margin, y, width, RowHeight + 2);
            DrawCells(g, bold, XBrushes.Black, y + 1, columns, report.Columns, report.Columns.Select(c => (object?)c.Header).ToArray(), header: true);
            y += RowHeight + 4;
        }

        NewPage();
        if (report.Rows.Count(r => r.Style == ReportRowStyle.Normal || r.Style == ReportRowStyle.Group) == 0)
        {
            g.DrawString("Nothing to show for the chosen filters.", body, muted, Margin, y + 10);
            y += RowHeight;
        }

        foreach (ReportRow row in report.Rows)
        {
            if (y + RowHeight > bottom)
            {
                NewPage();
            }
            bool emphasised = row.Style != ReportRowStyle.Normal;
            if (row.Style is ReportRowStyle.Subtotal or ReportRowStyle.Total)
            {
                g.DrawRectangle(new XSolidBrush(row.Style == ReportRowStyle.Total ? HeaderFill : XColor.FromArgb(250, 246, 238)),
                    Margin, y, width, RowHeight);
            }
            DrawCells(g, emphasised ? bold : body, XBrushes.Black, y, columns, report.Columns, row.Values, header: false);
            y += RowHeight;
            if (row.Style is ReportRowStyle.Normal or ReportRowStyle.Group)
            {
                g.DrawLine(new XPen(Line, 0.4), Margin, y, Margin + width, y);
            }
        }
        g.Dispose();

        for (int i = 0; i < document.PageCount; i++)
        {
            PdfPage page = document.Pages[i];
            using XGraphics footer = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            double footerY = page.Height.Point - Margin;
            footer.DrawString($"Computer generated report from the {AppInfo.ProductName}.", small, muted, Margin, footerY);
            footer.DrawString($"Page {i + 1} of {document.PageCount}", small, muted,
                new XRect(Margin, footerY - 7, page.Width.Point - 2 * Margin, 9), XStringFormats.TopRight);
        }

        document.Save(path);
    }

    private static void DrawCells(XGraphics g, XFont font, XBrush brush, double y, double[] widths,
        List<ReportColumn> columns, object?[] values, bool header)
    {
        double x = Margin;
        for (int i = 0; i < columns.Count; i++)
        {
            bool alignRight = columns[i].Kind is ReportValueKind.Money or ReportValueKind.Number;
            string text = header ? Convert.ToString(values[i]) ?? "" : ReportService.FormatValue(values[i], columns[i].Kind, pdf: true);
            var cell = new XRect(x + 3, y, widths[i] - 6, RowHeight);
            // A total label in the first column may run into the empty cells beside it.
            if (!header && i == 0 && values.Skip(1).Take(2).All(v => v is null))
            {
                cell = new XRect(x + 3, y, widths.Take(3).Sum() - 6, RowHeight);
            }
            else if (!header && i == 1 && values[0] is null && values.Skip(2).Take(3).All(v => v is null))
            {
                cell = new XRect(x + 3, y, widths.Skip(1).Take(4).Sum() - 6, RowHeight);
            }
            g.DrawString(Fit(g, text, font, cell.Width), font, brush, cell,
                alignRight ? XStringFormats.CenterRight : XStringFormats.CenterLeft);
            x += widths[i];
        }
    }
}
