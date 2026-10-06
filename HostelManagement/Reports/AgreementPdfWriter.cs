using HostelManagement.Services;
using HostelManagement.Utilities;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using static HostelManagement.Reports.PdfText;

namespace HostelManagement.Reports;

/// <summary>
/// Writes the residency agreement as an A4 PDF: the title in red as on the client's form, wrapped paragraphs
/// over as many pages as needed, filled in values in bold, and signature lines at the end.
/// </summary>
public static class AgreementPdfWriter
{
    private const double Margin = 56;
    private const double LineHeight = 14.5;
    private const double ParagraphGap = 5;
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
        foreach (string line in document.FilledText.Split('\n').Select(l => l.TrimEnd('\r')))
        {
            if (line.Trim().Length == 0)
            {
                y += ParagraphGap;
            }
            else if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                Ensure(30);
                g.DrawString(Plain(line[2..]), title, new XSolidBrush(TitleRed), new XRect(Margin, y, width, 22), XStringFormats.TopCenter);
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
                foreach (List<(string Text, bool Bold)> wrapped in Wrap(g, Words(line), width, body, bold))
                {
                    Ensure(LineHeight);
                    double x = Margin;
                    foreach ((string text, bool isBold) in wrapped)
                    {
                        XFont font = isBold ? bold : body;
                        g.DrawString(text, font, XBrushes.Black, x, y + 10.5);
                        x += g.MeasureString(text + " ", font).Width;
                    }
                    y += LineHeight;
                }
                y += ParagraphGap;
            }
        }

        // ---- Signatures ----
        Ensure(170);
        y += 30;
        double column = width / 2;
        foreach ((string left, string leftName, string right, string rightName) in new[]
                 {
                     ("First Party (Owner)", "", "Second Party (Student)", document.StudentName),
                     ("Parent / Guardian", document.ParentName, "Witness", ""),
                 })
        {
            y += 36;
            g.DrawLine(new XPen(XColors.Black, 0.6), Margin, y, Margin + column - 40, y);
            g.DrawLine(new XPen(XColors.Black, 0.6), Margin + column + 20, y, Margin + width, y);
            g.DrawString(left, bold, XBrushes.Black, Margin, y + 13);
            g.DrawString(right, bold, XBrushes.Black, Margin + column + 20, y + 13);
            g.DrawString(leftName, body, XBrushes.Black, Margin, y + 27);
            g.DrawString(rightName, body, XBrushes.Black, Margin + column + 20, y + 27);
            y += 34;
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

    /// <summary>Splits a paragraph into words, each marked bold when it is part of a filled in value.</summary>
    internal static List<(string Text, bool Bold)> Words(string line)
    {
        var words = new List<(string, bool)>();
        bool isBold = false;
        var current = new System.Text.StringBuilder();
        void Flush()
        {
            if (current.Length > 0)
            {
                words.Add((current.ToString(), isBold));
                current.Clear();
            }
        }

        foreach (char c in line)
        {
            if (c == AgreementService.ValueStart || c == AgreementService.ValueEnd)
            {
                Flush();
                isBold = c == AgreementService.ValueStart;
            }
            else if (c == ' ')
            {
                Flush();
            }
            else
            {
                current.Append(c);
            }
        }
        Flush();
        return words;
    }

    private static IEnumerable<List<(string Text, bool Bold)>> Wrap(XGraphics g, List<(string Text, bool Bold)> words,
        double width, XFont body, XFont bold)
    {
        var line = new List<(string, bool)>();
        double used = 0;
        double space = g.MeasureString(" ", body).Width;
        foreach ((string text, bool isBold) in words)
        {
            double wordWidth = g.MeasureString(text, isBold ? bold : body).Width;
            if (line.Count > 0 && used + space + wordWidth > width)
            {
                yield return line;
                line = [];
                used = 0;
            }
            used += (line.Count > 0 ? space : 0) + wordWidth;
            line.Add((text, isBold));
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
