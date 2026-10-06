using HostelManagement.Models;
using Xunit;

namespace HostelManagement.Tests;

public sealed class BillingPeriodsTests
{
    [Theory]
    [InlineData("2026-07-01", "2026-07-01", "2026-12-31")]
    [InlineData("2026-10-15", "2026-07-01", "2026-12-31")]
    [InlineData("2026-12-31", "2026-07-01", "2026-12-31")]
    [InlineData("2027-01-01", "2027-01-01", "2027-06-30")]
    [InlineData("2027-06-30", "2027-01-01", "2027-06-30")]
    public void HalfYearly_FollowsTheAcademicYearFromJuly(string date, string from, string to)
    {
        BillingPeriod period = BillingPeriods.For(DateTime.Parse(date), BillingFrequency.HalfYearly);

        Assert.Equal(DateTime.Parse(from), period.From);
        Assert.Equal(DateTime.Parse(to), period.To);
    }

    [Theory]
    [InlineData("2026-07-10", "2026-07-01", "2026-09-30")]
    [InlineData("2026-11-30", "2026-10-01", "2026-12-31")]
    [InlineData("2027-02-28", "2027-01-01", "2027-03-31")]
    [InlineData("2027-04-01", "2027-04-01", "2027-06-30")]
    public void Quarterly_FollowsTheAcademicYearFromJuly(string date, string from, string to)
    {
        BillingPeriod period = BillingPeriods.For(DateTime.Parse(date), BillingFrequency.Quarterly);

        Assert.Equal(DateTime.Parse(from), period.From);
        Assert.Equal(DateTime.Parse(to), period.To);
    }

    [Fact]
    public void AcademicYear_HasTwoOrFourPeriodsStartingInJuly()
    {
        Assert.Equal(["Jul 2026 to Dec 2026", "Jan 2027 to Jun 2027"],
            BillingPeriods.ForAcademicYear(new DateTime(2027, 3, 1), BillingFrequency.HalfYearly).Select(p => p.Name));
        Assert.Equal(["Jul 2026 to Sep 2026", "Oct 2026 to Dec 2026", "Jan 2027 to Mar 2027", "Apr 2027 to Jun 2027"],
            BillingPeriods.ForAcademicYear(new DateTime(2026, 8, 1), BillingFrequency.Quarterly).Select(p => p.Name));
    }

    [Theory]
    [InlineData(120000, BillingFrequency.HalfYearly, 60000)]
    [InlineData(120000, BillingFrequency.Quarterly, 30000)]
    [InlineData(100001, BillingFrequency.Quarterly, 25000.25)]
    [InlineData(10000.10, BillingFrequency.Quarterly, 2500.03)]
    public void InstallmentAmount_IsYearlyRentDividedByInstallments(decimal yearly, string frequency, decimal expected) =>
        Assert.Equal(expected, BillingPeriods.InstallmentAmount(yearly, frequency));
}
