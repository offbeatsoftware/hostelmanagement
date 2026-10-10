using System.Drawing.Imaging;
using HostelManagement.Services;
using HostelManagement.Utilities;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using static HostelManagement.Reports.PdfText;

namespace HostelManagement.Reports;

/// <summary>
/// Prints I-cards in the usual card size (85.6 x 54 mm), eight on an A4 page with cutting space between them.
/// A student card has a terracotta band, a transport card a blue one with "TRANSPORT PASS".
/// </summary>
public static class IdCardPdfWriter
{
    private const double MmToPoint = 72 / 25.4;
    private const double CardWidth = 85.6 * MmToPoint;
    private const double CardHeight = 54 * MmToPoint;
    private const double Gap = 8 * MmToPoint;
    private const int Columns = 2;
    private const int Rows = 4;

    private static readonly XColor TransportBlue = XColor.FromArgb(0, 102, 170);

    public static void Write(IReadOnlyList<IdCard> cards, bool transport, string path)
    {
        using var document = new PdfDocument();
        document.Info.Title = transport ? "Transport I-cards" : "Student I-cards";
        document.Info.Author = AppInfo.BusinessName;

        for (int start = 0; start < cards.Count; start += Columns * Rows)
        {
            PdfPage page = document.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            using XGraphics g = XGraphics.FromPdfPage(page);
            double left = (page.Width.Point - (Columns * CardWidth + (Columns - 1) * Gap)) / 2;
            double top = (page.Height.Point - (Rows * CardHeight + (Rows - 1) * Gap)) / 2;

            foreach ((IdCard card, int index) in cards.Skip(start).Take(Columns * Rows).Select((c, i) => (c, i)))
            {
                double x = left + index % Columns * (CardWidth + Gap);
                double y = top + index / Columns * (CardHeight + Gap);
                DrawCard(g, card, transport, x, y);
            }
        }

        document.Save(path);
    }

    private static void DrawCard(XGraphics g, IdCard card, bool transport, double x, double y)
    {
        XColor colour = transport ? TransportBlue : Terracotta;
        var border = new XPen(colour, 1);
        g.DrawRoundedRectangle(border, XBrushes.White, x, y, CardWidth, CardHeight, 10, 10);

        // ---- Band: hostel name and card title ----
        const double band = 30;
        var bandPath = new XGraphicsPath();
        bandPath.AddRoundedRectangle(x, y, CardWidth, band + 10, 10, 10);
        g.Save();
        g.IntersectClip(new XRect(x, y, CardWidth, band));
        g.DrawPath(new XSolidBrush(colour), bandPath);
        g.Restore();
        g.DrawString(Fit(g, card.HostelName, Font(9, bold: true), CardWidth - 16), Font(9, bold: true), XBrushes.White,
            new XRect(x + 8, y + 3, CardWidth - 16, 13), XStringFormats.TopCenter);
        string title = transport ? $"TRANSPORT PASS  {card.AcademicYear}" : "STUDENT IDENTITY CARD";
        g.DrawString(title, Font(7, bold: true), XBrushes.White, new XRect(x + 8, y + 16, CardWidth - 16, 11), XStringFormats.TopCenter);

        // ---- Photo ----
        const double photoWidth = 58;
        const double photoHeight = 72;
        double photoX = x + 8;
        double photoY = y + band + 6;
        if (card.PhotoFile is not null && LoadPhoto(card.PhotoFile, photoWidth / photoHeight) is XImage photo)
        {
            using (photo)
            {
                g.DrawImage(photo, photoX, photoY, photoWidth, photoHeight);
            }
        }
        else
        {
            g.DrawString("PHOTO", Font(7), new XSolidBrush(Muted), new XRect(photoX, photoY, photoWidth, photoHeight), XStringFormats.Center);
        }
        g.DrawRectangle(new XPen(Line, 0.8), photoX, photoY, photoWidth, photoHeight);

        // ---- Details ----
        double textX = photoX + photoWidth + 8;
        double textWidth = x + CardWidth - 8 - textX;
        double lineY = photoY;
        XFont nameFont = Font(9, bold: true);
        g.DrawString(Fit(g, card.StudentName, nameFont, textWidth), nameFont, XBrushes.Black, textX, lineY + 9);
        lineY += 14;

        var lines = new List<(string Label, string Value)>();
        if (!transport)
        {
            lines.Add(("Father", card.FatherName));
        }
        lines.Add(("College", card.CollegeName));
        lines.Add(("Room", card.RoomText.Replace("Room ", string.Empty, StringComparison.Ordinal)));
        lines.Add(("Mobile", card.StudentMobile));
        if (!transport)
        {
            lines.Add(("Father mob.", card.FatherMobile));
        }

        XFont labelFont = Font(6.5);
        XFont valueFont = Font(6.5, bold: true);
        foreach ((string label, string value) in lines.Where(l => l.Value.Length > 0))
        {
            g.DrawString(label, labelFont, new XSolidBrush(Muted), textX, lineY + 7);
            g.DrawString(Fit(g, value, valueFont, textWidth - 40), valueFont, XBrushes.Black, textX + 40, lineY + 7);
            lineY += 10;
        }

        // ---- Footer: validity and signature ----
        double footerY = y + CardHeight - 13;
        g.DrawString($"Valid till {card.ValidTill:dd MMM yyyy}", Font(6.5, bold: true), new XSolidBrush(colour), photoX, footerY + 7);
        g.DrawLine(new XPen(Muted, 0.5), x + CardWidth - 70, footerY, x + CardWidth - 10, footerY);
        g.DrawString("Warden", Font(6), new XSolidBrush(Muted), new XRect(x + CardWidth - 70, footerY + 1, 60, 8), XStringFormats.TopCenter);
        if (card.HostelPhone.Length > 0)
        {
            g.DrawString($"Ph. {card.HostelPhone}", Font(6), new XSolidBrush(Muted),
                new XRect(x + 8, footerY - 1, CardWidth - 90, 8), XStringFormats.TopRight);
        }
    }

    /// <summary>The photo cropped to the frame's shape and stored as JPG, so any photo format works.</summary>
    private static XImage? LoadPhoto(string file, double aspect)
    {
        try
        {
            using var stream = new MemoryStream(File.ReadAllBytes(file));
            using var original = Image.FromStream(stream);
            int width = original.Width;
            int height = original.Height;
            Rectangle crop = width / (double)height > aspect
                ? new Rectangle((width - (int)(height * aspect)) / 2, 0, (int)(height * aspect), height)
                : new Rectangle(0, (height - (int)(width / aspect)) / 2, width, (int)(width / aspect));

            const int outputHeight = 400;
            using var cropped = new Bitmap((int)(outputHeight * aspect), outputHeight);
            using (Graphics graphics = Graphics.FromImage(cropped))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(original, new Rectangle(0, 0, cropped.Width, cropped.Height), crop, GraphicsUnit.Pixel);
            }
            var jpg = new MemoryStream();
            cropped.Save(jpg, ImageFormat.Jpeg);
            jpg.Position = 0;
            return XImage.FromStream(jpg);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or OutOfMemoryException or UnauthorizedAccessException)
        {
            AppLogger.Error($"The photo {file} could not be used on an I-card.", ex);
            return null;
        }
    }
}
