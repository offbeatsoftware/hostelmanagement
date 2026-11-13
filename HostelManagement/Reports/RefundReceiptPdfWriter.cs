using System.Globalization;
using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using static HostelManagement.Reports.PdfText;

namespace HostelManagement.Reports;

/// <summary>
/// Writes a refund receipt as a PDF (client decision, version 1.5), laid out like the payment receipt: the amount
/// paid back, how, the reason, and the fee after the refund.
/// </summary>
public static class RefundReceiptPdfWriter
{
    private const double Margin = 40;

    /// <summary>Saves the refund receipt PDF in the application's Receipts folder and returns its full path.</summary>
    public static string SaveToReceiptsFolder(RefundPrintData data)
    {
        Directory.CreateDirectory(AppPaths.ReceiptsFolder);
        string path = Path.Combine(AppPaths.ReceiptsFolder, FileName(data.Refund));
        Write(data, path);
        return path;
    }

    /// <summary>A safe file name such as Refund_SBH-RF-2026-2027-0001.pdf.</summary>
    public static string FileName(Refund refund) => $"Refund_{refund.RefundNumber.Replace('/', '-')}.pdf";

    public static void Write(RefundPrintData data, string path)
    {
        Refund refund = data.Refund;
        Invoice invoice = data.Invoice;
        using var document = new PdfDocument();
        document.Info.Title = $"Refund receipt {refund.RefundNumber}";
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

        // ---- Header: business and hostel on the left, REFUND RECEIPT on the right ----
        double y = Margin;
        g.DrawString(AppInfo.BusinessName, title, new XSolidBrush(Terracotta), Margin, y + 18);
        g.DrawString("REFUND RECEIPT", title, XBrushes.Black, new XRect(Margin, y, width, 24), XStringFormats.TopRight);
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
        g.DrawString("Refunded to", heading, XBrushes.Black, Margin, y + 10);
        y += 18;
        Student s = data.Student;
        foreach ((string text, XFont font) in new[]
                 {
                     (s.StudentName, bold),
                     (s.CollegeName, body),
                     (Join("Mobile: ", s.Mobile), body),
                     (Join("Father: ", data.Student.FatherName), body),
                 }.Where(l => l.Item1.Length > 0))
        {
            g.DrawString(text, font, XBrushes.Black, Margin, y + 9);
            y += 14;
        }

        double yRight = top;
        foreach ((string label, string value) in new[]
                 {
                     ("Refund number", refund.RefundNumber),
                     ("Refund date", refund.RefundDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)),
                     ("Paid back by", refund.RefundMethod),
                     (PaymentMethod.ReferenceName(refund.RefundMethod), refund.Reference),
                 }.Where(l => l.Item2.Length > 0))
        {
            g.DrawString(label, body, muted, right, yRight + 9);
            g.DrawString(Fit(g, value, bold, width / 2 - 130), bold, XBrushes.Black, right + 105, yRight + 9);
            yRight += 16;
        }
        y = Math.Max(y, yRight) + 14;

        // ---- Amount refunded ----
        g.DrawRectangle(new XSolidBrush(HeaderFill), Margin, y, width, 46);
        g.DrawString("Amount refunded", body, muted, Margin + 10, y + 16);
        g.DrawString(Rupees(refund.Amount), Font(16, bold: true), XBrushes.Black,
            new XRect(Margin, y + 4, width - 10, 22), XStringFormats.TopRight);
        g.DrawString(Fit(g, AmountInWords(refund.Amount), bold, width - 20), bold, XBrushes.Black, Margin + 10, y + 36);
        y += 58;

        string from = $"From the hostel fee for the academic year {invoice.YearText} (invoice {invoice.InvoiceNumber}).";
        g.DrawString(Fit(g, from, body, width), body, XBrushes.Black, Margin, y + 9);
        y += 16;
        g.DrawString(Fit(g, "Reason: " + refund.Reason, body, width), body, XBrushes.Black, Margin, y + 9);
        y += 16;

        // ---- The fee after this refund (a refund lowers the fee and the paid amount alike) ----
        y += 8;
        var lines = new List<(string Label, decimal Amount, XFont Font)> { ("Fee agreed", invoice.FeeAmount, body) };
        if (invoice.WaivedAmount > 0)
        {
            lines.Add(("Waived", invoice.WaivedAmount, body));
        }
        lines.Add(("Refunded in total", invoice.RefundedAmount, body));
        lines.Add(("Fee after refunds", invoice.TotalAmount, body));
        lines.Add(("Paid after refunds", invoice.PaidAmount, body));
        lines.Add(("Balance pending", invoice.PendingAmount, bold));
        foreach ((string label, decimal amount, XFont font) in lines)
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
        g.DrawString($"Computer generated refund receipt from the {AppInfo.ProductName}.", small, muted, Margin, y + 12);

        document.Save(path);
    }
}
