namespace HostelManagement.Models;

/// <summary>
/// The academic year runs from July to June (client decision). A year is stored as the calendar year in
/// which it starts: 2026 is the academic year July 2026 to June 2027, shown as "2026-27".
/// </summary>
public static class AcademicYear
{
    public const int StartMonth = 7;

    /// <summary>The academic year that contains the date.</summary>
    public static int Of(DateTime date) => date.Month >= StartMonth ? date.Year : date.Year - 1;

    public static int Current => Of(DateTime.Today);

    /// <summary>"2026-27" for the academic year starting in July 2026.</summary>
    public static string Label(int startYear) => $"{startYear}-{(startYear + 1) % 100:00}";

    public static DateTime Start(int startYear) => new(startYear, StartMonth, 1);

    /// <summary>The last day of the academic year (30 June).</summary>
    public static DateTime End(int startYear) => Start(startYear).AddYears(1).AddDays(-1);
}
