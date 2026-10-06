using System.Globalization;
using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using static HostelManagement.Reports.PdfText;

namespace HostelManagement.Reports;

/// <summary>Writes an invoice as an A4 PDF (for printing, saving and, later, emailing).</summary>
public static class InvoicePdfWriter
{
    private const double Margin = 40;

    /// <summary>Saves the invoice PDF in the application's Invoices folder and returns its full path.</summary>
    public static string SaveToInvoicesFolder(InvoicePrintData data)
    {
        Directory.CreateDirectory(AppPaths.InvoicesFolder);
        string path = Path.Combine(AppPaths.InvoicesFolder, FileName(data.Invoice));
        Write(data, path);
        return path;
    }

    /// <summary>A safe file name such as Invoice_SBH-2026-27-0001.pdf.</summary>
    public static string FileName(Invoice invoice) => $"Invoice_{invoice.InvoiceNumber.Replace('/', '-')}.pdf";

    public static void Write(InvoicePrintData data, string path)
    {
        Invoice invoice = data.Invoice;
        using var document = new PdfDocument();
        document.Info.Title = $"Invoice {invoice.InvoiceNumber}";
        document.Info.Author = AppInfo.BusinessName;

        PdfPage page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        using XGraphics g = XGraphics.FromPdfPage(page);
        double width = page.Width.Point - 2 * Margin;

        var title = Font(20, bold: true);
        var heading = Font(11, bold: true);
        var body = Font(9.5);
        var bold = Font(9.5, bold: true);
        var small = Font(8);

        // ---- Header: business and hostel on the left, INVOICE on the right ----
        double y = Margin;
        g.DrawString(AppInfo.BusinessName, title, new XSolidBrush(Terracotta), Margin, y + 18);
        g.DrawString("INVOICE", title, XBrushes.Black, new XRect(Margin, y, width, 24), XStringFormats.TopRight);
        y += 30;
        g.DrawString(data.Hostel.HostelName, heading, XBrushes.Black, Margin, y + 10);
        y += 16;
        foreach (string line in new[]
                 {
                     data.Hostel.Address,
                     Join("Phone: ", data.Hostel.Phone, "   Email: ", data.Hostel.Email),
                 }.Where(l => l.Length > 0))
        {
            g.DrawString(line, body, new XSolidBrush(Muted), Margin, y + 9);
            y += 13;
        }
        y += 8;
        g.DrawLine(new XPen(Terracotta, 1.5), Margin, y, Margin + width, y);
        y += 16;

        // ---- Bill to (left) and invoice details (right) ----
        double right = Margin + width / 2 + 20;
        double top = y;
        g.DrawString("Bill to", heading, XBrushes.Black, Margin, y + 10);
        y += 18;
        Student s = data.Student;
        foreach ((string text, XFont font) in new[]
                 {
                     (s.StudentName, bold),
                     (Join("", data.RoomText, ", ", s.CollegeName), body),
                     (Join("Course: ", s.Course, "   Class: ", s.ClassName), body),
                     (Join("Mobile: ", s.Mobile), body),
                     (Join("Parent / guardian: ", data.Parent?.ParentName ?? ""), body),
                     (Join("", data.Parent?.Mobile ?? "", "   ", data.Parent?.Email ?? ""), body),
                 }.Where(l => l.Item1.Length > 0))
        {
            g.DrawString(text, font, XBrushes.Black, Margin, y + 9);
            y += 14;
        }

        double yRight = top;
        foreach ((string label, string value) in new[]
                 {
                     ("Invoice number", invoice.InvoiceNumber),
                     ("Invoice date", invoice.InvoiceDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)),
                     ("Billing period", $"{invoice.BillingFrom:dd MMM yyyy} to {invoice.BillingTo:dd MMM yyyy}"),
                     ("Status", invoice.Status),
                 })
        {
            g.DrawString(label, body, new XSolidBrush(Muted), right, yRight + 9);
            g.DrawString(value, bold, XBrushes.Black, right + 90, yRight + 9);
            yRight += 16;
        }
        y = Math.Max(y, yRight) + 16;

        // ---- Items ----
        double[] columns = [width - 255, 45, 100, 110];
        string[] headers = ["Description", "Qty", "Rate", "Amount"];
        g.DrawRectangle(new XSolidBrush(HeaderFill), Margin, y, width, 22);
        DrawRow(g, bold, y, columns, headers);
        y += 22;

        foreach (InvoiceItem item in invoice.Items)
        {
            DrawRow(g, body, y, columns,
                [item.Description, item.Quantity.ToString(CultureInfo.InvariantCulture), Rupees(item.Rate), Rupees(item.Amount)]);
            y += 20;
            g.DrawLine(new XPen(Line, 0.5), Margin, y, Margin + width, y);
        }

        // ---- Totals ----
        y += 10;
        foreach ((string label, decimal amount, XFont font) in new[]
                 {
                     ("Total", invoice.TotalAmount, bold),
                     ("Paid", invoice.PaidAmount, body),
                     ("Pending", invoice.PendingAmount, bold),
                 })
        {
            g.DrawString(label, font, XBrushes.Black,
                new XRect(Margin + width - 250, y, 130, 16), XStringFormats.TopRight);
            g.DrawString(Rupees(amount), font, XBrushes.Black,
                new XRect(Margin + width - 115, y, 110, 16), XStringFormats.TopRight);
            y += 18;
        }

        // ---- Footer ----
        double footer = page.Height.Point - Margin;
        g.DrawLine(new XPen(Line, 0.5), Margin, footer - 18, Margin + width, footer - 18);
        g.DrawString($"Computer generated invoice from the {AppInfo.ProductName}.", small, new XSolidBrush(Muted),
            Margin, footer - 6);

        document.Save(path);
    }

    private static void DrawRow(XGraphics g, XFont font, double y, double[] columns, string[] cells)
    {
        double x = Margin;
        for (int i = 0; i < cells.Length; i++)
        {
            XStringFormat format = i == 0 ? XStringFormats.CenterLeft : XStringFormats.CenterRight;
            var cell = new XRect(x + 6, y, columns[i] - 12, 20);
            g.DrawString(Fit(g, cells[i], font, cell.Width), font, XBrushes.Black, cell, format);
            x += columns[i];
        }
    }
}
