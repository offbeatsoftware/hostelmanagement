using System.Globalization;

namespace HostelManagement.Utilities;

/// <summary>Formats and reads rupee amounts the same way everywhere (for example ₹4,500.00).</summary>
public static class Money
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-IN");

    public static string Format(decimal amount) => amount.ToString("C2", Culture);

    /// <summary>Short amount for chart axes in the Indian system: ₹850, ₹45K, ₹1.2L, ₹2.5Cr.</summary>
    public static string Compact(decimal amount) => amount switch
    {
        >= 10_000_000m => "₹" + (amount / 10_000_000m).ToString("0.#", CultureInfo.InvariantCulture) + "Cr",
        >= 100_000m => "₹" + (amount / 100_000m).ToString("0.#", CultureInfo.InvariantCulture) + "L",
        >= 1_000m => "₹" + (amount / 1_000m).ToString("0.#", CultureInfo.InvariantCulture) + "K",
        _ => "₹" + amount.ToString("0", CultureInfo.InvariantCulture),
    };

    /// <summary>Accepts "4500", "4,500.00" or "₹4,500"; rejects anything else.</summary>
    public static bool TryParse(string? text, out decimal amount)
    {
        string cleaned = (text ?? string.Empty).Replace("₹", string.Empty).Replace("Rs.", string.Empty).Trim();
        return decimal.TryParse(cleaned, NumberStyles.Number, Culture, out amount);
    }
}
