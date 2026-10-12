using HostelManagement.Models;
using HostelManagement.Services;
using Xunit;

namespace HostelManagement.Tests;

/// <summary>The academic year start month chosen in Application Settings.</summary>
public sealed class AcademicYearTests : TestDatabase
{
    [Fact]
    public void Default_RunsFromJulyToJune()
    {
        Assert.Equal(7, AcademicYear.StartMonth);
        Assert.Equal(2026, AcademicYear.Of(new DateTime(2026, 7, 1)));
        Assert.Equal(2025, AcademicYear.Of(new DateTime(2026, 6, 30)));
        Assert.Equal("2026-27", AcademicYear.Label(2026));
        Assert.Equal(new DateTime(2027, 6, 30), AcademicYear.End(2026));
        Assert.Equal("1 Jul 2026 to 30 Jun 2027", AcademicYear.PeriodText(2026));
        Assert.Equal("30 June (the next year)", AcademicYear.EndText(7));
    }

    [Fact]
    public void AprilStart_MovesTheYearBoundary_AndIsKeptInTheDatabase()
    {
        AcademicYearService.SaveStartMonth(4);

        Assert.Equal(2026, AcademicYear.Of(new DateTime(2027, 3, 31)));
        Assert.Equal(2027, AcademicYear.Of(new DateTime(2027, 4, 1)));
        Assert.Equal(new DateTime(2027, 3, 31), AcademicYear.End(2026));
        Assert.Equal("31 March (the next year)", AcademicYear.EndText(4));

        AcademicYear.UseStartMonth(AcademicYear.DefaultStartMonth);
        AcademicYearService.Load();
        Assert.Equal(4, AcademicYear.StartMonth);
    }

    [Fact]
    public void JanuaryStart_IsTheCalendarYear()
    {
        AcademicYearService.SaveStartMonth(1);

        Assert.Equal(2027, AcademicYear.Of(new DateTime(2027, 1, 1)));
        Assert.Equal("2027", AcademicYear.Label(2027));
        Assert.Equal(new DateTime(2027, 12, 31), AcademicYear.End(2027));
        Assert.Equal("31 December (the same year)", AcademicYear.EndText(1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void InvalidMonth_IsRejected(int month)
    {
        Assert.Throws<ValidationException>(() => AcademicYearService.SaveStartMonth(month));
        Assert.Equal(7, AcademicYear.StartMonth);
    }

    [Fact]
    public void CheckIn_UsesTheChosenYear_ForTheFeeAndInvoiceNumber()
    {
        AcademicYearService.SaveStartMonth(4);
        Room room = AddRoom("201", beds: 2);
        int student = AddStudentWithParents("Neha Verma");
        DateTime checkIn = AcademicYear.Start(AcademicYear.Current);

        Invoice invoice = CheckInWithFee(student, room, 60_000m, date: checkIn);

        Assert.Equal(AcademicYear.Current, invoice.AcademicYear);
        Assert.Contains($"/{AcademicYear.Label(AcademicYear.Current)}/", invoice.InvoiceNumber);
    }
}
