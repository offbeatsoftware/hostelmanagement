using System.Globalization;

namespace HostelManagement.Utilities;

/// <summary>Formats and reads rupee amounts the same way everywhere (for example ₹4,500.00).</summary>
public static class Money
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-IN");

    public static string Format(decimal amount) => amount.ToString("C2", Culture);

    /// <summary>Accepts "4500", "4,500.00" or "₹4,500"; rejects anything else.</summary>
    public static bool TryParse(string? text, out decimal amount)
    {
        string cleaned = (text ?? string.Empty).Replace("₹", string.Empty).Replace("Rs.", string.Empty).Trim();
        return decimal.TryParse(cleaned, NumberStyles.Number, Culture, out amount);
    }
}
