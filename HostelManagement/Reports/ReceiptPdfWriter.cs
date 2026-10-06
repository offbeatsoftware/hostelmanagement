using System.Globalization;
using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using static HostelManagement.Reports.PdfText;

namespace HostelManagement.Reports;

/// <summary>Writes a payment receipt as a PDF (the top half of an A4 page, so it can be printed and cut).</summary>
public static class ReceiptPdfWriter
{
    private const double Margin = 40;

    /// <summary>Saves the receipt PDF in the application's Receipts folder and returns its full path.</summary>
    public static string SaveToReceiptsFolder(ReceiptPrintData data)
    {
        Directory.CreateDirectory(AppPaths.ReceiptsFolder);
        string path = Path.Combine(AppPaths.ReceiptsFolder, FileName(data.Payment));
        Write(data, path);
        return path;
    }

    /// <summary>A safe file name such as Receipt_SBH-R-2026-27-0001.pdf.</summary>
    public static string FileName(Payment payment) => $"Receipt_{payment.ReceiptNumber.Replace('/', '-')}.pdf";

    public static void Write(ReceiptPrintData data, string path)
    {
        Payment payment = data.Payment;
        Invoice invoice = data.Invoice;
        using var document = new PdfDocument();
        document.Info.Title = $"Receipt {payment.ReceiptNumber}";
        document.Info.Author = AppInfo.BusinessName;

        PdfPage page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        using XGraphics g = XGraphics.FromPdfPage(page);
        double width = page.Width.Point - 2 * Margin;

        XFont title = Font(20, bold: true);
        XFont heading = Font(11, bold: true);
        XFont body = Font(9.5);
        XFont bold = Font(9.5, bold: true);
        XFont small = Font(8);
        var muted = new XSolidBrush(Muted);

        // ---- Header: business and hostel on the left, RECEIPT on the right ----
        double y = Margin;
        g.DrawString(AppInfo.BusinessName, title, new XSolidBrush(Terracotta), Margin, y + 18);
        g.DrawString("RECEIPT", title, XBrushes.Black, new XRect(Margin, y, width, 24), XStringFormats.TopRight);
        y += 30;
        g.DrawString(data.Hostel.HostelName, heading, XBrushes.Black, Margin, y + 10);
        y += 16;
        foreach (string line in new[]
                 {
                     data.Hostel.Address,
                     Join("Phone: ", data.Hostel.Phone, "   Email: ", data.Hostel.Email),
                 }.Where(l => l.Length > 0))
        {
            g.DrawString(line, body, muted, Margin, y + 9);
            y += 13;
        }
        y += 8;
        g.DrawLine(new XPen(Terracotta, 1.5), Margin, y, Margin + width, y);
        y += 16;

        // ---- Received from (left) and receipt details (right) ----
        double right = Margin + width / 2 + 20;
        double top = y;
        g.DrawString("Received from", heading, XBrushes.Black, Margin, y + 10);
        y += 18;
        Student s = data.Student;
        foreach ((string text, XFont font) in new[]
                 {
                     (s.StudentName, bold),
                     (s.CollegeName, body),
                     (Join("Mobile: ", s.Mobile), body),
                     (Join("Parent / guardian: ", data.Parent?.ParentName ?? ""), body),
                 }.Where(l => l.Item1.Length > 0))
        {
            g.DrawString(text, font, XBrushes.Black, Margin, y + 9);
            y += 14;
        }

        double yRight = top;
        foreach ((string label, string value) in new[]
                 {
                     ("Receipt number", payment.ReceiptNumber),
                     ("Payment date", payment.PaymentDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)),
                     ("Paid by", payment.PaymentMethod),
                     (PaymentMethod.ReferenceName(payment.PaymentMethod), payment.Reference),
                 }.Where(l => l.Item2.Length > 0))
        {
            g.DrawString(label, body, muted, right, yRight + 9);
            g.DrawString(Fit(g, value, bold, width / 2 - 130), bold, XBrushes.Black, right + 105, yRight + 9);
            yRight += 16;
        }
        y = Math.Max(y, yRight) + 14;

        // ---- Amount received ----
        g.DrawRectangle(new XSolidBrush(HeaderFill), Margin, y, width, 46);
        g.DrawString("Amount received", body, muted, Margin + 10, y + 16);
        g.DrawString(Rupees(payment.Amount), Font(16, bold: true), XBrushes.Black,
            new XRect(Margin, y + 4, width - 10, 22), XStringFormats.TopRight);
        g.DrawString(Fit(g, AmountInWords(payment.Amount), bold, width - 20), bold, XBrushes.Black, Margin + 10, y + 36);
        y += 58;

        string towards = $"Towards invoice {invoice.InvoiceNumber} dated {invoice.InvoiceDate:dd MMM yyyy} " +
                         $"for {invoice.BillingFrom:dd MMM yyyy} to {invoice.BillingTo:dd MMM yyyy}.";
        g.DrawString(Fit(g, towards, body, width), body, XBrushes.Black, Margin, y + 9);
        y += 16;
        if (payment.Remarks.Length > 0)
        {
            g.DrawString(Fit(g, "Remarks: " + payment.Remarks, body, width), body, XBrushes.Black, Margin, y + 9);
            y += 16;
        }

        // ---- Invoice balance after this payment ----
        y += 8;
        foreach ((string label, decimal amount, XFont font) in new[]
                 {
                     ("Invoice total", invoice.TotalAmount, body),
                     ("Paid till date", invoice.PaidAmount, body),
                     ("Balance pending", invoice.PendingAmount, bold),
                 })
        {
            g.DrawString(label, font, XBrushes.Black, new XRect(Margin + width - 250, y, 130, 16), XStringFormats.TopRight);
            g.DrawString(Rupees(amount), font, XBrushes.Black, new XRect(Margin + width - 115, y, 110, 16), XStringFormats.TopRight);
            y += 18;
        }

        // ---- Signature and footer ----
        y += 30;
        g.DrawLine(new XPen(Line, 0.5), Margin + width - 160, y, Margin + width, y);
        g.DrawString("Authorised signatory", small, muted, new XRect(Margin + width - 160, y + 3, 160, 12), XStringFormats.TopCenter);
        y += 26;
        g.DrawLine(new XPen(Line, 0.5), Margin, y, Margin + width, y);
        g.DrawString($"Computer generated receipt from the {AppInfo.ProductName}.", small, muted, Margin, y + 12);

        document.Save(path);
    }
}
