using HostelManagement.Models;
using HostelManagement.Reports;
using HostelManagement.Services;
using Xunit;

namespace HostelManagement.Tests;

/// <summary>Yearly fees agreed per student (client decision, version 1.2).</summary>
public sealed class InvoiceServiceTests : TestDatabase
{
    private static string Prefix(int year) => $"SBH/{AcademicYear.Label(year)}/";

    [Fact]
    public void CheckIn_WithFee_CreatesTheYearlyInvoiceWithRentAndTransport()
    {
        int student = AddStudentWithParents();

        Invoice invoice = CheckInWithFee(student, AddRoom(), rent: 50_000m, transport: 12_000m);

        Assert.Equal(Prefix(AcademicYear.Current) + "0001", invoice.InvoiceNumber);
        Assert.Equal(AcademicYear.Current, invoice.AcademicYear);
        Assert.Equal(YearStart, invoice.InvoiceDate);
        Assert.Equal(50_000m, invoice.RoomRent);
        Assert.Equal(12_000m, invoice.TransportAmount);
        Assert.Equal(62_000m, invoice.TotalAmount);
        Assert.True(invoice.HasTransport);
        Assert.Equal(0m, invoice.PaidAmount);
        Assert.Equal(62_000m, invoice.PendingAmount);
        Assert.Equal(InvoiceStatus.Unpaid, invoice.Status);
    }

    [Fact]
    public void TwoStudentsInTheSameRoom_HaveTheirOwnFees()
    {
        Room room = AddRoom(beds: 2);

        Invoice aman = CheckInWithFee(AddStudentWithParents("Aman"), room, 50_000m);
        Invoice ravi = CheckInWithFee(AddStudentWithParents("Ravi"), room, 45_000m, 8_000m);

        Assert.Equal(50_000m, aman.TotalAmount);
        Assert.False(aman.HasTransport);
        Assert.Equal(53_000m, ravi.TotalAmount);
        Assert.Equal(Prefix(AcademicYear.Current) + "0002", ravi.InvoiceNumber);
    }

    [Theory]
    [InlineData(0, 0, "room rent for the year")]
    [InlineData(50_000, -1, "cannot be negative")]
    [InlineData(50_000.123, 0, "two decimal places")]
    [InlineData(20_000_000, 0, "cannot be more than")]
    public void CheckIn_FeeIsChecked_AndNothingIsSavedWhenItIsWrong(double rent, double transport, string expected)
    {
        int student = AddStudentWithParents();

        var ex = Assert.Throws<ValidationException>(() =>
            AllocationService.CheckIn(student, AddRoom().RoomId, YearStart, fee: new YearFee((decimal)rent, (decimal)transport)));

        Assert.Contains(expected, ex.Message);
        Assert.Equal(0, Count("Invoice"));
        Assert.Equal(0, Count("RoomAllocation"));
    }

    [Fact]
    public void CheckIn_StudentWhoAlreadyHasAFeeForTheYear_KeepsIt()
    {
        int student = AddStudentWithParents();
        InvoiceService.Create(student, AcademicYear.Current, new YearFee(40_000m, 0m), YearStart);

        var ex = Assert.Throws<ValidationException>(() =>
            AllocationService.CheckIn(student, AddRoom().RoomId, YearStart, fee: new YearFee(50_000m, 0m)));
        Assert.Contains("already has a fee", ex.Message);

        AllocationService.CheckIn(student, AddRoom("102").RoomId, YearStart);
        Assert.Equal(1, Count("Invoice"));
        Assert.Equal(40_000m, InvoiceService.GetForYear(student, AcademicYear.Current)!.TotalAmount);
    }

    [Fact]
    public void Create_NumbersInvoicesPerAcademicYear()
    {
        int year = AcademicYear.Current;
        List<string> numbers = Enumerable.Range(1, 3)
            .Select(i => InvoiceService.Create(AddStudentWithParents($"Student {i}"), year, new YearFee(10_000m * i, 0m)).InvoiceNumber)
            .ToList();
        Invoice nextYear = InvoiceService.Create(AddStudentWithParents("Next"), year + 1, new YearFee(60_000m, 0m));

        Assert.Equal([Prefix(year) + "0001", Prefix(year) + "0002", Prefix(year) + "0003"], numbers);
        Assert.Equal(Prefix(year + 1) + "0001", nextYear.InvoiceNumber);
    }

    [Fact]
    public void Create_Rules()
    {
        int student = AddStudentWithParents();
        InvoiceService.Create(student, AcademicYear.Current, new YearFee(50_000m, 0m));

        Assert.Contains("already has a fee",
            Assert.Throws<ValidationException>(() => InvoiceService.Create(student, AcademicYear.Current, new YearFee(1m, 0m))).Message);
        Assert.Contains("this academic year or the next",
            Assert.Throws<ValidationException>(() => InvoiceService.Create(student, AcademicYear.Current + 2, new YearFee(1m, 0m))).Message);
        Assert.Contains("future",
            Assert.Throws<ValidationException>(() =>
                InvoiceService.Create(student, AcademicYear.Current + 1, new YearFee(1m, 0m), DateTime.Today.AddDays(1))).Message);
        Assert.Contains("select the student",
            Assert.Throws<ValidationException>(() => InvoiceService.Create(999_999, AcademicYear.Current, new YearFee(1m, 0m))).Message);
    }

    [Fact]
    public void Payments_InAnyInstalments_BalanceIsTotalMinusEverythingPaid()
    {
        // The client's example: rent 50,000 and transport 12,000; 10,000 paid at admission and 5,000 later.
        Invoice invoice = CheckInWithFee(AddStudentWithParents(), AddRoom(), 50_000m, 12_000m);

        Pay(invoice.InvoiceId, 10_000m, YearStart);
        Pay(invoice.InvoiceId, 5_000m, YearStart.AddDays(15) > DateTime.Today ? DateTime.Today : YearStart.AddDays(15));
        Invoice partly = InvoiceService.GetInvoice(invoice.InvoiceId)!;

        Assert.Equal(15_000m, partly.PaidAmount);
        Assert.Equal(47_000m, partly.PendingAmount);
        Assert.Equal(InvoiceStatus.PartlyPaid, partly.Status);

        Pay(invoice.InvoiceId, 47_000m);
        Invoice paid = InvoiceService.GetInvoice(invoice.InvoiceId)!;
        Assert.Equal(0m, paid.PendingAmount);
        Assert.Equal(InvoiceStatus.Paid, paid.Status);
    }

    [Fact]
    public void UpdateFee_ChangesRentAndTransport_ButNotBelowWhatIsPaid()
    {
        Invoice invoice = CheckInWithFee(AddStudentWithParents(), AddRoom(), 50_000m, 12_000m);
        Pay(invoice.InvoiceId, 15_000m);

        Invoice changed = InvoiceService.UpdateFee(invoice.InvoiceId, new YearFee(40_000m, 0m, "Transport stopped"));
        Assert.Equal(40_000m, changed.TotalAmount);
        Assert.Equal(25_000m, changed.PendingAmount);
        Assert.False(changed.HasTransport);
        Assert.Equal("Transport stopped", changed.Remarks);

        var ex = Assert.Throws<ValidationException>(() => InvoiceService.UpdateFee(invoice.InvoiceId, new YearFee(10_000m, 0m)));
        Assert.Contains("cannot be less", ex.Message);
        Assert.Equal(40_000m, InvoiceService.GetInvoice(invoice.InvoiceId)!.TotalAmount);
    }

    [Fact]
    public void NewYearFees_ListStudentsInARoomWithoutAFee_WithLastYearForReference()
    {
        Room room = AddRoom(beds: 3);
        int year = AcademicYear.Current;
        int aman = AddStudentWithParents("Aman");
        int ravi = AddStudentWithParents("Ravi");
        CheckInWithFee(aman, room, 50_000m, 12_000m);
        CheckInWithFee(ravi, room, 45_000m);
        AddStudentWithParents("Not In A Room");

        List<NewYearFeeCandidate> candidates = InvoiceService.GetNewYearCandidates(HostelId, year + 1);
        Assert.Equal(["Aman", "Ravi"], candidates.Select(c => c.Student.StudentName));
        Assert.Equal(62_000m, candidates[0].LastYear!.TotalAmount);
        Assert.Equal("101, bed 1", candidates[0].RoomText);
        Assert.Empty(InvoiceService.GetNewYearCandidates(HostelId, year));

        List<Invoice> created = InvoiceService.CreateForYear(year + 1, new Dictionary<int, YearFee> { [aman] = new(55_000m, 13_000m) });

        Assert.Equal(Prefix(year + 1) + "0001", Assert.Single(created).InvoiceNumber);
        Assert.Equal(68_000m, InvoiceService.GetForYear(aman, year + 1)!.TotalAmount);
        Assert.Equal(["Ravi"], InvoiceService.GetNewYearCandidates(HostelId, year + 1).Select(c => c.Student.StudentName));
    }

    [Fact]
    public void NewYearFees_OneWrongFee_SavesNothing()
    {
        Room room = AddRoom(beds: 2);
        int aman = AddStudentWithParents("Aman");
        int ravi = AddStudentWithParents("Ravi");
        CheckInWithFee(aman, room, 50_000m);
        CheckInWithFee(ravi, room, 45_000m);

        var ex = Assert.Throws<ValidationException>(() => InvoiceService.CreateForYear(AcademicYear.Current + 1,
            new Dictionary<int, YearFee> { [aman] = new(55_000m, 0m), [ravi] = new(0m, 0m) }));

        Assert.StartsWith("Ravi:", ex.Message);
        Assert.Equal(2, Count("Invoice"));
        Assert.Contains("at least one student",
            Assert.Throws<ValidationException>(() => InvoiceService.CreateForYear(AcademicYear.Current + 1, new Dictionary<int, YearFee>())).Message);
    }

    [Fact]
    public void Delete_InvoiceWithoutPayments_RemovesIt_ButNotOneWithPayments()
    {
        Invoice unpaid = InvoiceService.Create(AddStudentWithParents("Aman"), AcademicYear.Current, new YearFee(50_000m, 0m));
        Invoice paid = InvoiceService.Create(AddStudentWithParents("Ravi"), AcademicYear.Current, new YearFee(50_000m, 0m));
        Pay(paid.InvoiceId, 1_000m);

        InvoiceService.Delete(unpaid.InvoiceId);
        var ex = Assert.Throws<ValidationException>(() => InvoiceService.Delete(paid.InvoiceId));

        Assert.Null(InvoiceService.GetInvoice(unpaid.InvoiceId));
        Assert.Contains("Edit Fee", ex.Message);
    }

    [Fact]
    public void Pdf_ListsEveryPaymentAndIsWrittenToTheInvoicesFolder()
    {
        Invoice invoice = CheckInWithFee(AddStudentWithParents(), AddRoom(), 50_000m, 12_000m);
        Pay(invoice.InvoiceId, 10_000m, YearStart);
        Pay(invoice.InvoiceId, 5_000m);

        InvoicePrintData data = InvoiceService.GetPrintData(invoice.InvoiceId);
        string path = InvoicePdfWriter.SaveToInvoicesFolder(data);

        Assert.Equal(2, data.Payments.Count);
        Assert.Equal(10_000m, data.Payments[0].Amount);
        Assert.Equal("Room 101, bed 1", data.RoomText);
        Assert.Equal(47_000m, data.Invoice.PendingAmount);
        Assert.True(new FileInfo(path).Length > 1000);
        Assert.Equal(Utilities.AppPaths.InvoicesFolder, Path.GetDirectoryName(path));
    }

    [Theory]
    [InlineData("2026-06-30", 2025, "2025-26")]
    [InlineData("2026-07-01", 2026, "2026-27")]
    [InlineData("2027-03-15", 2026, "2026-27")]
    [InlineData("2099-12-31", 2099, "2099-00")]
    public void AcademicYear_RunsFromJulyToJune(string date, int year, string label)
    {
        DateTime day = DateTime.Parse(date, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(year, AcademicYear.Of(day));
        Assert.Equal(label, AcademicYear.Label(year));
        Assert.Equal(new DateTime(year, 7, 1), AcademicYear.Start(year));
        Assert.Equal(new DateTime(year + 1, 6, 30), AcademicYear.End(year));
        Assert.Equal(label, InvoiceService.AcademicYearLabel(day));
    }
}
