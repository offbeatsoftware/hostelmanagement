using System.Net.Mail;

namespace HostelManagement.Utilities;

/// <summary>Shared input checks used by the services.</summary>
public static class Validators
{
    /// <summary>Trims the text and turns null into an empty string.</summary>
    public static string Clean(string? text) => text?.Trim() ?? string.Empty;

    /// <summary>Empty is allowed; otherwise must be a single valid email address.</summary>
    public static bool IsValidEmailOrEmpty(string email)
    {
        if (email.Length == 0)
        {
            return true;
        }

        try
        {
            var address = new MailAddress(email);
            return address.Address == email && email.Contains('.', StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Empty is allowed; otherwise digits with optional +, spaces, hyphens and brackets,
    /// containing 6 to 15 digits (covers landline and mobile numbers).
    /// </summary>
    public static bool IsValidPhoneOrEmpty(string phone)
    {
        if (phone.Length == 0)
        {
            return true;
        }

        if (phone.Any(c => !char.IsAsciiDigit(c) && c is not ('+' or ' ' or '-' or '(' or ')')))
        {
            return false;
        }

        int digits = phone.Count(char.IsAsciiDigit);
        return digits is >= 6 and <= 15;
    }

    /// <summary>Throws a <see cref="Services.ValidationException"/> when the text is longer than the column allows.</summary>
    public static void CheckLength(string value, int maxLength, string fieldName)
    {
        if (value.Length > maxLength)
        {
            throw new Services.ValidationException($"{fieldName} can be at most {maxLength} characters.");
        }
    }
}
