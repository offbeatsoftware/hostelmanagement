using HostelManagement.Forms.Views;
using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;
using Xunit;

namespace HostelManagement.Tests;

public sealed class DashboardServiceTests : TestDatabase
{
    private static readonly DateTime Today = DateTime.Today;

    private Room? _room;

    private int StudentInRoom(string name, decimal rent)
    {
        _room ??= AddRoom(beds: 3);
        int id = AddStudentWithParents(name);
        CheckInWithFee(id, _room, rent);
        return id;
    }

    [Fact]
    public void Get_EmptyHostel_ShowsZerosAndTwelveMonthsFromJuly()
    {
        DashboardData data = DashboardService.Get(HostelId, Today);

        Assert.Equal(0, data.TotalStudents);
        Assert.Equal(0m, data.PendingAmount);
        Assert.Empty(data.HighestPending);
        Assert.Equal(12, data.PaymentsPerMonth.Count);
        Assert.Equal(new DateTime(YearStart.Year, 7, 1), data.PaymentsPerMonth[0].Month);
        Assert.Equal("Jul", data.PaymentsPerMonth[0].Label);
        Assert.Equal(InvoiceService.AcademicYearLabel(Today), data.AcademicYear);
    }

    [Fact]
    public void Get_CountsStudentsBedsFeesPaymentsAndPending()
    {
        int aman = StudentInRoom("Aman", 60_000m);
        int ravi = StudentInRoom("Ravi", 40_000m);
        AddRoom("102", beds: 2);
        AllocationService.CheckOut(ravi, Today);
        Invoice lastYear = InvoiceService.Create(aman, AcademicYear.Current - 1, new YearFee(30_000m, 0m));
        DateTime lastMonth = new DateTime(Today.Year, Today.Month, 1).AddDays(-1);

        Pay(InvoiceService.GetForYear(aman, AcademicYear.Current)!.InvoiceId, 10_000m, Today);
        Pay(lastYear.InvoiceId, 5_000m, lastMonth);

        DashboardData data = DashboardService.Get(HostelId, Today);

        Assert.Equal(1, data.ActiveStudents);
        Assert.Equal(1, data.LeftStudents);
        Assert.Equal(5, data.TotalBeds);
        Assert.Equal(1, data.OccupiedBeds);
        Assert.Equal(4, data.FreeBeds);
        Assert.Equal(100_000m, data.InvoicedThisYear);
        Assert.Equal(10_000m, data.ReceivedThisMonth);
        Assert.Equal(10_000m + (lastMonth >= YearStart ? 5_000m : 0m), data.ReceivedThisYear);
        Assert.Equal(50_000m + 40_000m + 25_000m, data.PendingAmount);
        Assert.Equal(2, data.PendingStudents);
        Assert.Equal(["Aman", "Ravi"], data.HighestPending.Select(d => d.StudentName));
        Assert.Equal(10_000m, data.LatestPayments[0].Amount);
        Assert.Equal(2, data.RecentCheckIns.Count);
        Assert.Equal(10_000m, data.PaymentsPerMonth.Single(m => m.Month == new DateTime(Today.Year, Today.Month, 1)).Amount);
    }

    [Fact]
    public void Get_ListsAtMostFiveOfEach()
    {
        for (int r = 0; r < 3; r++)
        {
            Room room = AddRoom($"10{r}", beds: 3);
            for (int s = 0; s < 2; s++)
            {
                Invoice invoice = CheckInWithFee(AddStudentWithParents($"Student {r}{s}"), room, 50_000m);
                Pay(invoice.InvoiceId, 1_000m);
            }
        }

        DashboardData data = DashboardService.Get(HostelId, Today);

        Assert.Equal(5, data.HighestPending.Count);
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
        int aman = StudentInRoom("Aman", 50_000m);
        Pay(InvoiceService.GetForYear(aman, AcademicYear.Current)!.InvoiceId, 12_000m);
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
            Assert.Contains(controls, c => c is Label { Text: "⚠ Pending" });
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
