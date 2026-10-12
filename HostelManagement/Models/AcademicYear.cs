using System.Globalization;

namespace HostelManagement.Models;

/// <summary>
/// The academic year starts on the 1st of the month chosen in Application Settings (July unless changed) and
/// ends the day before the same date a year later. A year is stored as the calendar year in which it starts:
/// with a July start, 2026 is the academic year 1 July 2026 to 30 June 2027, shown as "2026-2027" (client decision).
/// </summary>
public static class AcademicYear
{
    public const int DefaultStartMonth = 7;

    /// <summary>The month the academic year starts in (1 to 12), set from the database when it is opened.</summary>
    public static int StartMonth { get; private set; } = DefaultStartMonth;

    internal static void UseStartMonth(int month)
    {
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), month, "A month is 1 to 12.");
        }
        StartMonth = month;
    }

    /// <summary>The academic year that contains the date.</summary>
    public static int Of(DateTime date) => date.Month >= StartMonth ? date.Year : date.Year - 1;

    public static int Current => Of(DateTime.Today);

    /// <summary>"2026-2027" for the academic year starting in 2026; "2026" when the year runs January to December.</summary>
    public static string Label(int startYear) => Label(startYear, StartMonth);

    /// <summary>The label of a year with the given start month (used to preview a start month before it is saved).</summary>
    public static string Label(int startYear, int startMonth) =>
        startMonth == 1
            ? startYear.ToString(CultureInfo.InvariantCulture)
            : string.Create(CultureInfo.InvariantCulture, $"{startYear}-{startYear + 1}");

    /// <summary>
    /// The short label used in invoice and receipt numbers before version 1.3 ("2026-27"), so numbering of a
    /// year carries on after numbers made earlier.
    /// </summary>
    public static string ShortLabel(int startYear) =>
        StartMonth == 1
            ? startYear.ToString(CultureInfo.InvariantCulture)
            : string.Create(CultureInfo.InvariantCulture, $"{startYear}-{(startYear + 1) % 100:00}");

    public static DateTime Start(int startYear) => new(startYear, StartMonth, 1);

    /// <summary>The last day of the academic year (30 June with a July start).</summary>
    public static DateTime End(int startYear) => Start(startYear).AddYears(1).AddDays(-1);

    /// <summary>"1 Jul 2026 to 30 Jun 2027".</summary>
    public static string PeriodText(int startYear) =>
        string.Create(CultureInfo.InvariantCulture, $"{Start(startYear):d MMM yyyy} to {End(startYear):d MMM yyyy}");

    /// <summary>The month name of a start month, "July".</summary>
    public static string MonthName(int month) => CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month);

    /// <summary>"30 June (the next year)": when a year with this start month ends.</summary>
    public static string EndText(int startMonth)
    {
        DateTime end = new DateTime(2001, startMonth, 1).AddYears(1).AddDays(-1);
        return string.Create(CultureInfo.InvariantCulture,
            $"{end:d MMMM}{(startMonth == 1 ? " (the same year)" : " (the next year)")}");
    }
}
