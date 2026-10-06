using System.Globalization;
using HostelManagement.Models;
using HostelManagement.Utilities;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using static HostelManagement.Reports.PdfText;

namespace HostelManagement.Reports;

/// <summary>
/// Writes the pending dues list as a landscape A4 PDF: one bold line per student with the totals,
/// followed by that student's unpaid invoices. Long lists continue on further pages.
/// </summary>
public static class PendingDuesPdfWriter
{
    private const double Margin = 36;
    private const double RowHeight = 17;

    // Description, Room, Parent / mobile, Invoice date, Due date, Total, Paid, Pending, Overdue.
    private static readonly double[] Columns = [170, 45, 140, 65, 65, 70, 70, 75, 55];

    private static readonly string[] Headers =
        ["Student / invoice", "Room", "Parent / mobile", "Invoice date", "Due date", "Total", "Paid", "Pending", "Overdue"];

    /// <summary>A file name such as PendingDues_Shri-Balaji-Hostel_2026-10-06.pdf.</summary>
    public static string FileName(Hostel hostel, DateTime asOf)
    {
        string name = string.Concat(hostel.HostelName.Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');
        while (name.Contains("--", StringComparison.Ordinal))
        {
            name = name.Replace("--", "-", StringComparison.Ordinal);
        }
        return $"PendingDues_{name}_{asOf:yyyy-MM-dd}.pdf";
    }

    public static void Write(Hostel hostel, IReadOnlyList<StudentDue> dues, DateTime asOf, string path)
    {
        using var document = new PdfDocument();
        document.Info.Title = $"Pending dues, {hostel.HostelName}, {asOf:dd MMM yyyy}";
        document.Info.Author = AppInfo.BusinessName;

        XFont title = Font(16, bold: true);
        XFont body = Font(8.5);
        XFont bold = Font(8.5, bold: true);
        XFont small = Font(7.5);
        var muted = new XSolidBrush(Muted);
        var overdueBrush = new XSolidBrush(XColor.FromArgb(176, 42, 42));

        PdfPage page = null!;
        XGraphics g = null!;
        double y = 0;
        double width = 0;
        double bottom = 0;

        void NewPage()
        {
            g?.Dispose();
            page = document.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            page.Orientation = PdfSharp.PageOrientation.Landscape;
            g = XGraphics.FromPdfPage(page);
            width = page.Width.Point - 2 * Margin;
            bottom = page.Height.Point - Margin - 20;
            y = Margin;

            if (document.PageCount == 1)
            {
                g.DrawString(AppInfo.BusinessName, title, new XSolidBrush(Terracotta), Margin, y + 14);
                g.DrawString("PENDING DUES", title, XBrushes.Black, new XRect(Margin, y, width, 20), XStringFormats.TopRight);
                y += 24;
                g.DrawString($"{hostel.HostelName}   |   as on {asOf.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)}   |   " +
                             $"payment due {Invoice.PaymentDueDays} days after the invoice date", body, muted, Margin, y + 8);
                y += 14;
                decimal pending = dues.Sum(d => d.PendingAmount);
                decimal overdue = dues.Sum(d => d.OverdueAmount);
                g.DrawString($"{dues.Count} students   |   {dues.Sum(d => d.InvoiceCount)} invoices   |   " +
                             $"pending {Rupees(pending)}   |   overdue {Rupees(overdue)}", bold, XBrushes.Black, Margin, y + 9);
                y += 18;
                g.DrawLine(new XPen(Terracotta, 1.2), Margin, y, Margin + width, y);
                y += 8;
            }

            g.DrawRectangle(new XSolidBrush(HeaderFill), Margin, y, width, RowHeight + 2);
            DrawRow(g, bold, XBrushes.Black, y + 1, Headers, indent: 0);
            y += RowHeight + 4;
        }

        NewPage();
        if (dues.Count == 0)
        {
            g.DrawString("No pending dues. Every invoice is fully paid.", body, XBrushes.Black, Margin, y + 12);
        }

        foreach (StudentDue due in dues)
        {
            // Keep a student's line together with at least its first invoice.
            if (y + RowHeight * 2 > bottom)
            {
                NewPage();
            }

            string name = due.StudentStatus == StudentStatus.Left ? $"{due.StudentName} (left)" : due.StudentName;
            DrawRow(g, bold, XBrushes.Black, y,
                [name, due.RoomNumber, due.ParentText, "", "",
                 Rupees(due.Invoices.Sum(i => i.TotalAmount)), Rupees(due.Invoices.Sum(i => i.PaidAmount)),
                 Rupees(due.PendingAmount), due.IsOverdue ? $"{due.DaysOverdue} days" : ""],
                indent: 0);
            y += RowHeight;

            foreach (Invoice invoice in due.Invoices)
            {
                if (y + RowHeight > bottom)
                {
                    NewPage();
                }
                int days = invoice.DaysOverdue(asOf);
                DrawRow(g, body, days > 0 ? overdueBrush : XBrushes.Black, y,
                    [$"{invoice.InvoiceNumber}  ({invoice.PeriodText})", "", "",
                     invoice.InvoiceDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
                     invoice.DueDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
                     Rupees(invoice.TotalAmount), Rupees(invoice.PaidAmount), Rupees(invoice.PendingAmount),
                     days > 0 ? $"{days} days" : ""],
                    indent: 12);
                y += RowHeight;
            }

            g.DrawLine(new XPen(Line, 0.5), Margin, y + 2, Margin + width, y + 2);
            y += 6;
        }
        g.Dispose();

        // Page numbers, once the number of pages is known.
        for (int i = 0; i < document.PageCount; i++)
        {
            PdfPage p = document.Pages[i];
            using XGraphics footer = XGraphics.FromPdfPage(p, XGraphicsPdfPageOptions.Append);
            double footerY = p.Height.Point - Margin;
            footer.DrawString($"Computer generated report from the {AppInfo.ProductName}.", small, muted, Margin, footerY);
            footer.DrawString($"Page {i + 1} of {document.PageCount}", small, muted,
                new XRect(Margin, footerY - 8, p.Width.Point - 2 * Margin, 10), XStringFormats.TopRight);
        }

        document.Save(path);
    }

    private static void DrawRow(XGraphics g, XFont font, XBrush brush, double y, string[] cells, double indent)
    {
        double x = Margin;
        for (int i = 0; i < cells.Length; i++)
        {
            bool alignRight = i >= 5;
            double left = i == 0 ? indent : 0;
            var cell = new XRect(x + 4 + left, y, Columns[i] - 8 - left, RowHeight);
            g.DrawString(Fit(g, cells[i], font, cell.Width), font, brush, cell,
                alignRight ? XStringFormats.CenterRight : XStringFormats.CenterLeft);
            x += Columns[i];
        }
    }
}
