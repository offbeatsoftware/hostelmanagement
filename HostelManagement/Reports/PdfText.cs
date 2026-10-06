using HostelManagement.Utilities;
using PdfSharp.Drawing;
using PdfSharp.Fonts;

namespace HostelManagement.Reports;

/// <summary>Text helpers shared by the invoice and receipt PDFs.</summary>
public static class PdfText
{
    public const string FontName = "Arial";

    public static readonly XColor Terracotta = XColor.FromArgb(192, 101, 43);
    public static readonly XColor Muted = XColor.FromArgb(100, 100, 100);
    public static readonly XColor Line = XColor.FromArgb(210, 200, 185);
    public static readonly XColor HeaderFill = XColor.FromArgb(245, 236, 220);

    static PdfText()
    {
        // Use the fonts installed in Windows.
        GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }

    /// <summary>Creates a font; the first call configures PDFsharp to use the Windows fonts.</summary>
    public static XFont Font(double size, bool bold = false) =>
        new(FontName, size, bold ? XFontStyleEx.Bold : XFontStyleEx.Regular);

    /// <summary>"Rs. 1,25,000.00" (the rupee sign is not available in every PDF font).</summary>
    public static string Rupees(decimal amount) => "Rs. " + amount.ToString("N2", Money.Culture);

    /// <summary>Joins label and value pairs, leaving out pairs whose value is empty.</summary>
    public static string Join(params string[] labelValuePairs)
    {
        var parts = new List<string>();
        for (int i = 0; i + 1 < labelValuePairs.Length; i += 2)
        {
            if (labelValuePairs[i + 1].Length > 0)
            {
                parts.Add(labelValuePairs[i] + labelValuePairs[i + 1]);
            }
        }
        return string.Join("", parts).Trim(' ', ',');
    }

    /// <summary>Shortens text with "..." so it fits the width.</summary>
    public static string Fit(XGraphics g, string text, XFont font, double width)
    {
        if (g.MeasureString(text, font).Width <= width)
        {
            return text;
        }
        while (text.Length > 1 && g.MeasureString(text + "...", font).Width > width)
        {
            text = text[..^1];
        }
        return text + "...";
    }

    /// <summary>
    /// The amount in words in the Indian system, as written on receipts:
    /// 125000.50 → "Rupees One Lakh Twenty Five Thousand and Fifty Paise Only".
    /// </summary>
    public static string AmountInWords(decimal amount)
    {
        amount = Math.Round(Math.Abs(amount), 2, MidpointRounding.AwayFromZero);
        long rupees = (long)decimal.Truncate(amount);
        int paise = (int)((amount - rupees) * 100);

        string words = "Rupees " + (rupees == 0 ? "Zero" : IndianWords(rupees));
        if (paise > 0)
        {
            words += " and " + IndianWords(paise) + " Paise";
        }
        return words + " Only";
    }

    private static readonly string[] Ones =
    [
        "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
        "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen",
    ];

    private static readonly string[] Tens =
        ["", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"];

    private static string IndianWords(long number)
    {
        var parts = new List<string>();
        foreach ((long size, string name) in new[] { (10_000_000L, "Crore"), (100_000L, "Lakh"), (1_000L, "Thousand"), (100L, "Hundred") })
        {
            if (number >= size)
            {
                // Crores can exceed 99 (for example 150 crore), so they are written in words recursively.
                parts.Add(IndianWords(number / size) + " " + name);
                number %= size;
            }
        }
        if (number > 0)
        {
            parts.Add(number < 20 ? Ones[number] : (Tens[number / 10] + " " + Ones[number % 10]).Trim());
        }
        return string.Join(" ", parts);
    }
}
