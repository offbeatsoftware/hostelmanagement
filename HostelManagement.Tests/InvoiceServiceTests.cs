using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Reports;
using HostelManagement.Services;
using Xunit;

namespace HostelManagement.Tests;

public sealed class InvoiceServiceTests : TestDatabase
{
    private const decimal YearlyDoubleRent = 120_000m;

    // A billing period completely in the past, so every date in it can be used for check-in.
    private BillingPeriod PastPeriod(string frequency = BillingFrequency.HalfYearly) =>
        BillingPeriods.For(BillingPeriods.For(DateTime.Today, frequency).From.AddDays(-1), frequency);

    private Room SetUpRoom(string frequency = BillingFrequency.HalfYearly, bool setRent = true)
    {
        if (frequency != BillingFrequency.HalfYearly)
        {
            HostelService.Save(new Hostel { HostelId = HostelId, HostelName = "Test Hostel", BillingFrequency = frequency });
        }
        if (setRent)
        {
            RoomService.UpdateRent(SharingTypeId(2), YearlyDoubleRent);
        }
        return RoomService.Save(new Room { HostelId = HostelId, RoomNumber = "101", SharingTypeId = SharingTypeId(2), Gender = RoomGender.Male });
    }

    private int StudentInRoom(Room room, DateTime checkIn, string name = "Aman", IReadOnlyCollection<int>? services = null)
    {
        int id = StudentService.Save(
            new Student { StudentName = name, Gender = RoomGender.Male, Mobile = "9876543210", CollegeId = CollegeId, AdmissionDate = checkIn },
            new Parent { ParentName = "Rakesh", Mobile = "9812345678", Email = "rakesh@example.com" },
            extraServiceIds: services).StudentId;
        AllocationService.CheckIn(id, room.RoomId, checkIn);
        return id;
    }

    private ServiceItem Transport(decimal rate)
    {
        ServiceItem transport = ServiceItemService.GetServices(HostelId).Single(s => s.ServiceName == "Transport");
        transport.MonthlyRate = rate;
        return ServiceItemService.Save(transport);
    }

    private static int MonthsInclusive(DateTime from, DateTime to) => (to.Year - from.Year) * 12 + to.Month - from.Month + 1;

    [Fact]
    public void Preview_RentIsHalfTheYearlyRent_IncludedServicesAtNoCharge()
    {
        BillingPeriod period = PastPeriod();
        int student = StudentInRoom(SetUpRoom(), period.From);

        Invoice invoice = InvoiceService.Preview(student, period);

        Assert.Equal(60_000m, invoice.Items[0].Amount);
        Assert.Equal($"Room rent: room 101 (Double sharing), {period.Name}", invoice.Items[0].Description);
        Assert.Equal(["Laundry (included in rent)", "Wi-Fi (included in rent)"], invoice.Items.Skip(1).Select(i => i.Description));
        Assert.All(invoice.Items.Skip(1), i => Assert.Equal(0m, i.Amount));
        Assert.Equal(60_000m, invoice.TotalAmount);
    }

    [Fact]
    public void Preview_QuarterlyHostel_ChargesAQuarterOfTheYearlyRent()
    {
        BillingPeriod period = PastPeriod(BillingFrequency.Quarterly);
        int student = StudentInRoom(SetUpRoom(BillingFrequency.Quarterly), period.From);

        Assert.Equal(30_000m, InvoiceService.Preview(student, period).TotalAmount);
    }

    [Fact]
    public void Preview_StudentJoiningMidPeriod_PaysTheFullInstallment()
    {
        BillingPeriod period = PastPeriod();
        int student = StudentInRoom(SetUpRoom(), period.To.AddDays(-5));

        Assert.Equal(60_000m, InvoiceService.Preview(student, period).Items[0].Amount);
    }

    [Fact]
    public void Preview_TransportIsChargedPerMonth_PartMonthCountsAsFullMonth()
    {
        BillingPeriod period = PastPeriod();
        ServiceItem transport = Transport(1500m);
        DateTime joined = period.From.AddDays(40);
        int student = StudentInRoom(SetUpRoom(), joined, services: [transport.ServiceId]);
        int months = MonthsInclusive(joined, period.To);

        Invoice invoice = InvoiceService.Preview(student, period);

        InvoiceItem line = invoice.Items.Single(i => i.Description.StartsWith("Transport"));
        Assert.Equal(months, line.Quantity);
        Assert.Equal(1500m, line.Rate);
        Assert.Equal(months * 1500m, line.Amount);
        Assert.Equal(60_000m + months * 1500m, invoice.TotalAmount);
    }

    [Fact]
    public void Preview_TransportStoppedMidPeriod_CountsOnlyTheMonthsUsed()
    {
        BillingPeriod period = PastPeriod();
        ServiceItem transport = Transport(1000m);
        int student = StudentInRoom(SetUpRoom(), period.From, services: [transport.ServiceId]);
        Db.Execute("UPDATE [StudentService] SET [EndDate] = ?", Db.Param("@EndDate", period.From.AddMonths(1).AddDays(3)));

        InvoiceItem line = InvoiceService.Preview(student, period).Items.Single(i => i.Description.StartsWith("Transport"));

        Assert.Equal(2, line.Quantity);
        Assert.Equal(2000m, line.Amount);
    }

    [Fact]
    public void Preview_Rules()
    {
        BillingPeriod period = PastPeriod();
        Room room = SetUpRoom(setRent: false);
        int student = StudentInRoom(room, period.From);

        Assert.Contains("yearly rent", Assert.Throws<ValidationException>(() => InvoiceService.Preview(student, period)).Message);

        RoomService.UpdateRent(SharingTypeId(2), YearlyDoubleRent);
        var quarter = new BillingPeriod(period.From, period.From.AddMonths(3).AddDays(-1));
        Assert.Contains("not a billing period", Assert.Throws<ValidationException>(() => InvoiceService.Preview(student, quarter)).Message);

        BillingPeriod earlier = BillingPeriods.For(period.From.AddDays(-1), BillingFrequency.HalfYearly);
        Assert.Contains("not in a room", Assert.Throws<ValidationException>(() => InvoiceService.Preview(student, earlier)).Message);

        InvoiceService.Create(student, period);
        Assert.Contains("already has an invoice", Assert.Throws<ValidationException>(() => InvoiceService.Preview(student, period)).Message);
    }

    [Fact]
    public void Create_NumbersInvoicesPerAcademicYear()
    {
        BillingPeriod period = PastPeriod();
        Room room = SetUpRoom();
        int first = StudentInRoom(room, period.From, "First");
        int second = StudentInRoom(room, period.From, "Second");
        string year = InvoiceService.AcademicYearLabel(period.From);

        Invoice a = InvoiceService.Create(first, period);
        Invoice b = InvoiceService.Create(second, period);

        Assert.Equal($"SBH/{year}/0001", a.InvoiceNumber);
        Assert.Equal($"SBH/{year}/0002", b.InvoiceNumber);
        Assert.Equal(3, a.Items.Count);
        Assert.Equal(60_000m, a.TotalAmount);
    }

    [Theory]
    [InlineData("2026-07-01", "2026-27")]
    [InlineData("2027-06-30", "2026-27")]
    [InlineData("2099-12-01", "2099-00")]
    public void AcademicYearLabel_StartsInJuly(string date, string expected) =>
        Assert.Equal(expected, InvoiceService.AcademicYearLabel(DateTime.Parse(date)));

    [Fact]
    public void Invoices_ShowPaidPendingAndStatusFromPayments()
    {
        BillingPeriod period = PastPeriod();
        int student = StudentInRoom(SetUpRoom(), period.From);
        Invoice invoice = InvoiceService.Create(student, period);
        Assert.Equal(InvoiceStatus.Unpaid, InvoiceService.GetInvoices(HostelId).Single().Status);

        PaymentService.Record(new Payment { InvoiceId = invoice.InvoiceId, PaymentDate = DateTime.Today, Amount = 20_000m });

        Invoice listed = InvoiceService.GetInvoices(HostelId).Single();
        Assert.Equal(20_000m, listed.PaidAmount);
        Assert.Equal(40_000m, listed.PendingAmount);
        Assert.Equal(InvoiceStatus.PartlyPaid, listed.Status);
        Assert.Contains("cannot be deleted", Assert.Throws<ValidationException>(() => InvoiceService.Delete(invoice.InvoiceId)).Message);
    }

    [Fact]
    public void Delete_InvoiceWithoutPayments_RemovesItAndItsItems()
    {
        BillingPeriod period = PastPeriod();
        int student = StudentInRoom(SetUpRoom(), period.From);
        Invoice invoice = InvoiceService.Create(student, period);

        InvoiceService.Delete(invoice.InvoiceId);

        Assert.Equal(0, Count("Invoice"));
        Assert.Equal(0, Count("InvoiceItem"));
    }

    [Fact]
    public void Pdf_IsWrittenToTheInvoicesFolder()
    {
        BillingPeriod period = PastPeriod();
        ServiceItem transport = Transport(1500m);
        int student = StudentInRoom(SetUpRoom(), period.From, services: [transport.ServiceId]);
        Invoice invoice = InvoiceService.Create(student, period);

        string path = InvoicePdfWriter.SaveToInvoicesFolder(InvoiceService.GetPrintData(invoice.InvoiceId));

        Assert.Equal(Path.Combine(DataFolder, "Invoices", InvoicePdfWriter.FileName(invoice)), path);
        byte[] bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }
}
