using HostelManagement.Services;
using HostelManagement.Utilities;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using static HostelManagement.Reports.PdfText;

namespace HostelManagement.Reports;

/// <summary>
/// Writes the residency agreement as an A4 PDF: the title in red as on the client's form, wrapped paragraphs
/// over as many pages as needed, filled in values in bold, and signature lines at the end. The student's photo
/// (passport size) is printed at the top right of the first page; without a photo the box says where to paste one.
/// </summary>
public static class AgreementPdfWriter
{
    private const double Margin = 56;
    private const double LineHeight = 14.5;
    private const double ParagraphGap = 5;
    private const double PhotoWidth = 84;
    private const double PhotoHeight = 105;
    private static readonly XColor TitleRed = XColor.FromArgb(220, 30, 30);

    /// <summary>Saves the agreement in the application's Agreements folder and returns its full path.</summary>
    public static string SaveToAgreementsFolder(AgreementDocument document, DateTime date)
    {
        Directory.CreateDirectory(AppPaths.AgreementsFolder);
        string path = Path.Combine(AppPaths.AgreementsFolder, AgreementService.FileName(document, date));
        Write(document, path);
        return path;
    }

    public static void Write(AgreementDocument document, string path)
    {
        using var pdf = new PdfDocument();
        pdf.Info.Title = $"Residency agreement, {document.StudentName}";
        pdf.Info.Author = AppInfo.BusinessName;

        XFont body = Font(10.5);
        XFont bold = Font(10.5, bold: true);
        XFont heading = Font(12, bold: true);
        XFont title = Font(16, bold: true);
        XFont small = Font(8);

        XGraphics g = null!;
        double y = 0;
        double width = 0;
        double bottom = 0;

        void NewPage()
        {
            g?.Dispose();
            PdfPage page = pdf.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            g = XGraphics.FromPdfPage(page);
            width = page.Width.Point - 2 * Margin;
            bottom = page.Height.Point - Margin;
            y = Margin;
        }

        void Ensure(double height)
        {
            if (y + height > bottom)
            {
                NewPage();
            }
        }

        NewPage();

        // ---- Photo, top right of the first page; the text beside it is narrower ----
        double photoX = Margin + width - PhotoWidth;
        double photoBottom = Margin + PhotoHeight + 8;
        if (document.PhotoFile is not null && LoadPhoto(document.PhotoFile, PhotoWidth / PhotoHeight) is XImage photo)
        {
            using (photo)
            {
                g.DrawImage(photo, photoX, Margin, PhotoWidth, PhotoHeight);
            }
        }
        else
        {
            g.DrawString("Paste photo", small, new XSolidBrush(Muted), new XRect(photoX, Margin, PhotoWidth, PhotoHeight), XStringFormats.Center);
        }
        g.DrawRectangle(new XPen(XColors.Black, 0.6), photoX, Margin, PhotoWidth, PhotoHeight);
        XGraphics firstPage = g;
        double TextWidth() => g == firstPage && y < photoBottom ? width - PhotoWidth - 14 : width;

        foreach (string line in document.FilledText.Split('\n').Select(l => l.TrimEnd('\r')))
        {
            if (line.Trim().Length == 0)
            {
                y += ParagraphGap;
            }
            else if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                Ensure(30);
                g.DrawString(Plain(line[2..]), title, new XSolidBrush(TitleRed), new XRect(Margin, y, TextWidth(), 22), XStringFormats.TopCenter);
                y += 32;
            }
            else if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                Ensure(LineHeight * 3);
                y += 4;
                g.DrawString(Plain(line[3..]), heading, XBrushes.Black, Margin, y + 11);
                y += LineHeight + 4;
            }
            else
            {
                foreach (List<Word> wrapped in Wrap(g, Words(line), TextWidth(), body, bold))
                {
                    Ensure(LineHeight);
                    double x = Margin;
                    for (int i = 0; i < wrapped.Count; i++)
                    {
                        Word word = wrapped[i];
                        if (i > 0 && !word.Attached)
                        {
                            x += g.MeasureString(" ", body).Width;
                        }
                        XFont font = word.Bold ? bold : body;
                        g.DrawString(word.Text, font, XBrushes.Black, x, y + 10.5);
                        x += g.MeasureString(word.Text, font).Width;
                    }
                    y += LineHeight;
                }
                y += ParagraphGap;
            }
        }

        // ---- Signatures (made on screen, printed above the lines) ----
        AgreementSignatures? signed = document.Signatures;
        const double SignatureHeight = 40;
        Ensure(220);
        y += 24;
        double column = width / 2;
        foreach ((AgreementSigner leftSigner, string left, string leftName, AgreementSigner rightSigner, string right, string rightName) in new[]
                 {
                     (AgreementSigner.Owner, "First Party (Owner)", "", AgreementSigner.Student, "Second Party (Student)", document.StudentName),
                     (AgreementSigner.Parent, "Parent / Guardian", document.ParentName, AgreementSigner.Witness, "Witness", signed?.WitnessName ?? ""),
                 })
        {
            y += SignatureHeight + 8;
            double leftX = Margin;
            double rightX = Margin + column + 20;
            double lineWidth = column - 40;
            DrawSignature(g, signed?.For(leftSigner), leftX, y - SignatureHeight - 2, lineWidth, SignatureHeight);
            DrawSignature(g, signed?.For(rightSigner), rightX, y - SignatureHeight - 2, lineWidth, SignatureHeight);
            g.DrawLine(new XPen(XColors.Black, 0.6), leftX, y, leftX + lineWidth, y);
            g.DrawLine(new XPen(XColors.Black, 0.6), rightX, y, Margin + width, y);
            g.DrawString(left, bold, XBrushes.Black, leftX, y + 13);
            g.DrawString(right, bold, XBrushes.Black, rightX, y + 13);
            g.DrawString(leftName, body, XBrushes.Black, leftX, y + 27);
            g.DrawString(rightName, body, XBrushes.Black, rightX, y + 27);
            y += 34;
        }
        if (signed?.SignedDate is DateTime signedOn)
        {
            y += 10;
            g.DrawString($"Signed on {signedOn:dd MMM yyyy}", body, XBrushes.Black, Margin, y + 10);
        }
        g.Dispose();

        for (int i = 0; i < pdf.PageCount; i++)
        {
            PdfPage page = pdf.Pages[i];
            using XGraphics footer = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            footer.DrawString($"Page {i + 1} of {pdf.PageCount}", small, new XSolidBrush(Muted),
                new XRect(Margin, page.Height.Point - Margin + 16, page.Width.Point - 2 * Margin, 10), XStringFormats.TopRight);
        }

        pdf.Save(path);
    }

    /// <summary>Draws a signature image fitted into the box, standing on its bottom edge; nothing when there is none.</summary>
    private static void DrawSignature(XGraphics g, string? file, double x, double y, double width, double height)
    {
        using Image? image = AgreementSignatureService.Load(file);
        if (image is null)
        {
            return;
        }
        double scale = Math.Min(width / image.Width, height / image.Height);
        double drawWidth = image.Width * scale;
        double drawHeight = image.Height * scale;
        using var png = new MemoryStream();
        image.Save(png, System.Drawing.Imaging.ImageFormat.Png);
        png.Position = 0;
        using XImage signature = XImage.FromStream(png);
        g.DrawImage(signature, x, y + height - drawHeight, drawWidth, drawHeight);
    }

    /// <summary>A piece of text drawn in one font; Attached means no space before it (punctuation after a value).</summary>
    internal readonly record struct Word(string Text, bool Bold, bool Attached);

    /// <summary>Splits a paragraph into words, each marked bold when it is part of a filled in value.</summary>
    internal static List<Word> Words(string line)
    {
        var words = new List<Word>();
        bool isBold = false;
        bool attached = false;
        var current = new System.Text.StringBuilder();
        void Flush()
        {
            if (current.Length > 0)
            {
                words.Add(new Word(current.ToString(), isBold, attached && words.Count > 0));
                current.Clear();
            }
        }

        foreach (char c in line)
        {
            if (c == AgreementService.ValueStart || c == AgreementService.ValueEnd)
            {
                bool hadText = current.Length > 0;
                Flush();
                isBold = c == AgreementService.ValueStart;
                // Text right after a value (or a value right after text) continues the same word.
                attached = hadText || (c == AgreementService.ValueEnd && words.Count > 0);
            }
            else if (c == ' ')
            {
                Flush();
                attached = false;
            }
            else
            {
                current.Append(c);
            }
        }
        Flush();
        return words;
    }

    private static IEnumerable<List<Word>> Wrap(XGraphics g, List<Word> words, double width, XFont body, XFont bold)
    {
        var line = new List<Word>();
        double used = 0;
        double space = g.MeasureString(" ", body).Width;
        foreach (Word word in words)
        {
            double gap = line.Count > 0 && !word.Attached ? space : 0;
            double wordWidth = g.MeasureString(word.Text, word.Bold ? bold : body).Width;
            if (line.Count > 0 && !word.Attached && used + gap + wordWidth > width)
            {
                yield return line;
                line = [];
                used = 0;
                gap = 0;
            }
            used += gap + wordWidth;
            line.Add(line.Count == 0 ? word with { Attached = false } : word);
        }
        if (line.Count > 0)
        {
            yield return line;
        }
    }

    private static string Plain(string text) =>
        text.Replace(AgreementService.ValueStart.ToString(), "", StringComparison.Ordinal)
            .Replace(AgreementService.ValueEnd.ToString(), "", StringComparison.Ordinal);
}
