namespace HostelManagement.Models;

/// <summary>
/// How often a hostel bills its students (client decision, chosen per hostel):
/// twice a year or four times a year. Rent is entered per year and split into equal installments.
/// </summary>
public static class BillingFrequency
{
    public const string HalfYearly = "HalfYearly";
    public const string Quarterly = "Quarterly";

    public static IReadOnlyList<string> All { get; } = [HalfYearly, Quarterly];

    public static string DisplayName(string frequency) => frequency switch
    {
        HalfYearly => "Twice a year",
        Quarterly => "4 times a year",
        _ => string.Empty,
    };

    public static int InstallmentsPerYear(string frequency) => frequency == Quarterly ? 4 : 2;

    public static int MonthsPerPeriod(string frequency) => 12 / InstallmentsPerYear(frequency);
}

/// <summary>One billing period, for example 1 Jul 2026 to 31 Dec 2026.</summary>
public sealed record BillingPeriod(DateTime From, DateTime To)
{
    public string Name => $"{From:MMM yyyy} to {To:MMM yyyy}";
}

/// <summary>
/// Billing periods follow the academic year starting in July (client decision):
/// twice a year = Jul to Dec and Jan to Jun; four times a year = Jul to Sep, Oct to Dec, Jan to Mar, Apr to Jun.
/// </summary>
public static class BillingPeriods
{
    public const int AcademicYearStartMonth = 7;

    /// <summary>The billing period that contains the date.</summary>
    public static BillingPeriod For(DateTime date, string frequency)
    {
        int months = BillingFrequency.MonthsPerPeriod(frequency);
        int monthsIntoYear = (date.Month - AcademicYearStartMonth + 12) % 12;
        int academicYear = date.Month >= AcademicYearStartMonth ? date.Year : date.Year - 1;

        DateTime from = new DateTime(academicYear, AcademicYearStartMonth, 1).AddMonths(monthsIntoYear / months * months);
        return new BillingPeriod(from, from.AddMonths(months).AddDays(-1));
    }

    /// <summary>The periods of the academic year that contains the date, in order.</summary>
    public static IReadOnlyList<BillingPeriod> ForAcademicYear(DateTime date, string frequency)
    {
        int academicYear = date.Month >= AcademicYearStartMonth ? date.Year : date.Year - 1;
        DateTime first = new(academicYear, AcademicYearStartMonth, 1);
        int months = BillingFrequency.MonthsPerPeriod(frequency);

        return Enumerable.Range(0, BillingFrequency.InstallmentsPerYear(frequency))
            .Select(i => first.AddMonths(i * months))
            .Select(from => new BillingPeriod(from, from.AddMonths(months).AddDays(-1)))
            .ToList();
    }

    /// <summary>The rent for one installment: yearly rent divided by the installments per year, in rupees and paise.</summary>
    public static decimal InstallmentAmount(decimal yearlyAmount, string frequency) =>
        Math.Round(yearlyAmount / BillingFrequency.InstallmentsPerYear(frequency), 2, MidpointRounding.AwayFromZero);
}
