namespace HostelManagement.Utilities;

/// <summary>Sorts text with numbers the way people expect: "2" before "10", "A-9" before "A-10".</summary>
public sealed class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        x ??= string.Empty;
        y ??= string.Empty;
        int i = 0, j = 0;

        while (i < x.Length && j < y.Length)
        {
            if (char.IsAsciiDigit(x[i]) && char.IsAsciiDigit(y[j]))
            {
                int startX = i, startY = j;
                while (i < x.Length && char.IsAsciiDigit(x[i])) i++;
                while (j < y.Length && char.IsAsciiDigit(y[j])) j++;

                string numberX = x[startX..i].TrimStart('0');
                string numberY = y[startY..j].TrimStart('0');
                int result = numberX.Length != numberY.Length
                    ? numberX.Length.CompareTo(numberY.Length)
                    : string.CompareOrdinal(numberX, numberY);
                if (result != 0)
                {
                    return result;
                }
            }
            else
            {
                int result = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
                if (result != 0)
                {
                    return result;
                }
                i++;
                j++;
            }
        }

        return (x.Length - i).CompareTo(y.Length - j);
    }
}
