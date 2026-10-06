using HostelManagement.Forms.Views;
using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;
using Xunit;

namespace HostelManagement.Tests;

public sealed class DashboardServiceTests : TestDatabase
{
    private static readonly DateTime Today = DateTime.Today;
    private static readonly DateTime YearStart = BillingPeriods.ForAcademicYear(Today, BillingFrequency.HalfYearly)[0].From;

    // A period of the current academic year and one of the year before.
    private static BillingPeriod ThisYear => BillingPeriods.For(Today, BillingFrequency.HalfYearly);
    private static BillingPeriod LastYear => BillingPeriods.ForAcademicYear(Today.AddYears(-1), BillingFrequency.HalfYearly)[0];

    private Room? _room;

    private int StudentInRoom(string name)
    {
        if (_room is null)
        {
            RoomService.UpdateRent(SharingTypeId(3), 120_000m);
            _room = RoomService.Save(new Room { HostelId = HostelId, RoomNumber = "101", SharingTypeId = SharingTypeId(3), Gender = RoomGender.Male });
        }
        int id = StudentService.Save(
            new Student { StudentName = name, Gender = RoomGender.Male, Mobile = "9876543210", CollegeId = CollegeId, AdmissionDate = LastYear.From },
            new Parent { ParentName = "Parent", Mobile = "9812345678", Email = "parent@example.com" }).StudentId;
        AllocationService.CheckIn(id, _room.RoomId, LastYear.From);
        return id;
    }

    private static void Pay(Invoice invoice, decimal amount, DateTime date) =>
        PaymentService.Record(new Payment { InvoiceId = invoice.InvoiceId, PaymentDate = date, Amount = amount });

    [Fact]
    public void Get_EmptyHostel_ShowsZerosAndTwelveMonthsFromJuly()
    {
        DashboardData data = DashboardService.Get(HostelId, Today);

        Assert.Equal(0, data.TotalStudents);
        Assert.Equal(0m, data.PendingAmount);
        Assert.Empty(data.MostOverdue);
        Assert.Equal(12, data.PaymentsPerMonth.Count);
        Assert.Equal(new DateTime(YearStart.Year, 7, 1), data.PaymentsPerMonth[0].Month);
        Assert.Equal("Jul", data.PaymentsPerMonth[0].Label);
        Assert.Equal(InvoiceService.AcademicYearLabel(Today), data.AcademicYear);
    }

    [Fact]
    public void Get_CountsStudentsBedsInvoicesPaymentsAndDues()
    {
        int aman = StudentInRoom("Aman");
        int ravi = StudentInRoom("Ravi");
        RoomService.Save(new Room { HostelId = HostelId, RoomNumber = "102", SharingTypeId = SharingTypeId(2), Gender = RoomGender.Male });
        AllocationService.CheckOut(ravi, Today);

        Invoice old = InvoiceService.Create(aman, LastYear, LastYear.From);                 // last academic year, overdue
        Invoice current = InvoiceService.Create(aman, ThisYear, Today);                     // this academic year, not yet due
        Pay(old, 10_000m, Today);
        Pay(old, 5_000m, new DateTime(Today.Year, Today.Month, 1).AddDays(-1));             // last month

        DashboardData data = DashboardService.Get(HostelId, Today);

        Assert.Equal(1, data.ActiveStudents);
        Assert.Equal(1, data.LeftStudents);
        Assert.Equal(5, data.TotalBeds);
        Assert.Equal(1, data.OccupiedBeds);
        Assert.Equal(4, data.FreeBeds);
        Assert.Equal(current.TotalAmount, data.InvoicedThisYear);
        Assert.Equal(10_000m, data.ReceivedThisMonth);
        Assert.Equal(60_000m - 15_000m + 60_000m, data.PendingAmount);
        Assert.Equal(45_000m, data.OverdueAmount);
        Assert.Equal(1, data.OverdueStudents);
        Assert.Equal(["Aman"], data.MostOverdue.Select(d => d.StudentName));
        Assert.Equal(10_000m, data.LatestPayments[0].Amount);
        Assert.Equal(2, data.RecentCheckIns.Count);
        Assert.Equal(10_000m, data.PaymentsPerMonth.Single(m => m.Month == new DateTime(Today.Year, Today.Month, 1)).Amount);
        Assert.Equal(15_000m, data.PaymentsPerMonth.Sum(m => m.Amount) + (Today.Month == 7 ? 5_000m : 0m));
    }

    [Fact]
    public void Get_ListsAtMostFiveOfEach()
    {
        RoomService.UpdateRent(SharingTypeId(3), 120_000m);
        for (int r = 0; r < 3; r++)
        {
            Room room = RoomService.Save(new Room { HostelId = HostelId, RoomNumber = $"10{r}", SharingTypeId = SharingTypeId(3), Gender = RoomGender.Male });
            for (int s = 0; s < 2; s++)
            {
                int id = StudentService.Save(
                    new Student { StudentName = $"Student {r}{s}", Gender = RoomGender.Male, Mobile = "9876543210", CollegeId = CollegeId, AdmissionDate = LastYear.From },
                    new Parent { ParentName = "Parent", Mobile = "9812345678", Email = "parent@example.com" }).StudentId;
                AllocationService.CheckIn(id, room.RoomId, LastYear.From);
                Invoice invoice = InvoiceService.Create(id, LastYear, LastYear.From);
                Pay(invoice, 1_000m, Today);
            }
        }

        DashboardData data = DashboardService.Get(HostelId, Today);

        Assert.Equal(5, data.MostOverdue.Count);
        Assert.Equal(5, data.LatestPayments.Count);
        Assert.Equal(5, data.RecentCheckIns.Count);
    }

    [Theory]
    [InlineData("0", "₹0")]
    [InlineData("850", "₹850")]
    [InlineData("45000", "₹45K")]
    [InlineData("62500", "₹62.5K")]
    [InlineData("120000", "₹1.2L")]
    [InlineData("25000000", "₹2.5Cr")]
    public void Money_CompactUsesTheIndianSystem(string amount, string expected) =>
        Assert.Equal(expected, Money.Compact(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void View_ShowsTilesAndLabelsTheCurrentMonth()
    {
        int aman = StudentInRoom("Aman");
        Invoice invoice = InvoiceService.Create(aman, LastYear, LastYear.From);
        Pay(invoice, 12_000m, Today);
        Hostel hostel = HostelService.GetHostel(HostelId)!;

        MainFormTests.RunOnStaThread(() =>
        {
            using var form = new Form { Size = new Size(1200, 800) };
            var view = new DashboardView(hostel);
            form.Controls.Add(view);
            form.Show();
            Application.DoEvents();

            List<Control> controls = AllControls(view).ToList();
            Assert.Contains(controls, c => c is Label { Text: "Students" });
            Assert.Contains(controls, c => c is Label l && l.Text.StartsWith("⚠ ", StringComparison.Ordinal));
            PaymentsChart chart = controls.OfType<PaymentsChart>().Single();
            Assert.Single(chart.LabelledBars());

            using var bitmap = new Bitmap(chart.Width, chart.Height);
            chart.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            form.Close();
        });
    }

    private static IEnumerable<Control> AllControls(Control parent) =>
        parent.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(AllControls(c)));
}
