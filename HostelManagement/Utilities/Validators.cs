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

    // Verhoeff checksum tables, used by UIDAI for the last digit of an Aadhaar number.
    private static readonly int[,] VerhoeffMultiply =
    {
        { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, { 1, 2, 3, 4, 0, 6, 7, 8, 9, 5 }, { 2, 3, 4, 0, 1, 7, 8, 9, 5, 6 },
        { 3, 4, 0, 1, 2, 8, 9, 5, 6, 7 }, { 4, 0, 1, 2, 3, 9, 5, 6, 7, 8 }, { 5, 9, 8, 7, 6, 0, 4, 3, 2, 1 },
        { 6, 5, 9, 8, 7, 1, 0, 4, 3, 2 }, { 7, 6, 5, 9, 8, 2, 1, 0, 4, 3 }, { 8, 7, 6, 5, 9, 3, 2, 1, 0, 4 },
        { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 },
    };

    private static readonly int[,] VerhoeffPermute =
    {
        { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, { 1, 5, 7, 6, 2, 8, 3, 0, 9, 4 }, { 5, 8, 0, 3, 7, 9, 6, 1, 4, 2 },
        { 8, 9, 1, 6, 0, 4, 3, 5, 2, 7 }, { 9, 4, 5, 3, 1, 2, 6, 8, 7, 0 }, { 4, 2, 8, 6, 5, 7, 3, 9, 0, 1 },
        { 2, 7, 9, 3, 8, 0, 6, 4, 1, 5 }, { 7, 0, 4, 6, 9, 1, 3, 2, 5, 8 },
    };

    /// <summary>Removes the spaces and hyphens people type in Aadhaar numbers ("2345 6789 0124").</summary>
    public static string CleanAadhaar(string? text) =>
        new((text ?? string.Empty).Where(c => c is not (' ' or '-')).ToArray());

    /// <summary>
    /// Empty is allowed; otherwise 12 digits, not starting with 0 or 1, with a valid Verhoeff
    /// check digit (catches most typing mistakes).
    /// </summary>
    public static bool IsValidAadhaarOrEmpty(string aadhaar)
    {
        if (aadhaar.Length == 0)
        {
            return true;
        }
        if (aadhaar.Length != 12 || !aadhaar.All(char.IsAsciiDigit) || aadhaar[0] is '0' or '1')
        {
            return false;
        }

        int check = 0;
        for (int i = 0; i < aadhaar.Length; i++)
        {
            int digit = aadhaar[aadhaar.Length - 1 - i] - '0';
            check = VerhoeffMultiply[check, VerhoeffPermute[i % 8, digit]];
        }
        return check == 0;
    }
}
