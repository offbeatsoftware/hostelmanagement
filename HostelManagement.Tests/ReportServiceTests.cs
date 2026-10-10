using System.Globalization;
using ClosedXML.Excel;
using HostelManagement.Forms.Views;
using HostelManagement.Models;
using HostelManagement.Reports;
using HostelManagement.Services;
using PdfSharp.Pdf.IO;
using Xunit;

namespace HostelManagement.Tests;

public sealed class ReportServiceTests : TestDatabase
{
    private static readonly DateTime Today = DateTime.Today;

    private Hostel Hostel => HostelService.GetHostel(HostelId)!;
    private Room? _room;

    private Room Room
    {
        get
        {
            if (_room is null)
            {
                _room = RoomService.Save(new Room { HostelId = HostelId, RoomNumber = "101", Floor = "1", SharingTypeId = SharingTypeId(3), Gender = RoomGender.Male });
            }
            return _room;
        }
    }

    private int StudentInRoom(string name, string aadhaar = "", int? collegeId = null)
    {
        int id = StudentService.Save(
            new Student
            {
                StudentName = name, Gender = RoomGender.Male, Mobile = "9876543210", CollegeId = collegeId ?? CollegeId,
                AdmissionDate = YearStart, AadhaarNumber = aadhaar, Course = "B.Com", ClassName = "First year",
                FatherName = $"Father of {name}", FatherMobile = "9812345678", FatherEmail = "father@example.com",
            }).StudentId;
        CheckInWithFee(id, Room, 60_000m);
        return id;
    }

    private static Invoice FeeOf(int studentId) => InvoiceService.GetForYear(studentId, AcademicYear.Current)!;

    private static Payment Pay(Invoice invoice, decimal amount, DateTime date, string method = PaymentMethod.Cash) =>
        PaymentService.Record(new Payment
        {
            InvoiceId = invoice.InvoiceId, PaymentDate = date, Amount = amount, PaymentMethod = method,
            Reference = method == PaymentMethod.Cash ? "" : "REF1",
        });

    private static List<ReportRow> DataRows(ReportTable report) => report.Rows.Where(r => r.Style == ReportRowStyle.Normal).ToList();

    [Fact]
    public void StudentList_FiltersByStatusAndCollege_AndMasksAadhaar()
    {
        int otherCollege = AddCollege(HostelId, "Other College");
        StudentInRoom("Aman", aadhaar: "234567890124");
        int ravi = StudentInRoom("Ravi");
        StudentInRoom("Karan", collegeId: otherCollege);
        AllocationService.CheckOut(ravi, Today);

        ReportTable all = ReportService.StudentList(Hostel);
        ReportTable active = ReportService.StudentList(Hostel, StudentStatus.Active);
        ReportTable activeOther = ReportService.StudentList(Hostel, StudentStatus.Active, otherCollege);

        Assert.Equal(3, DataRows(all).Count);
        Assert.Equal(["Aman", "Karan"], DataRows(active).Select(r => r.Values[0]));
        Assert.Equal(["Karan"], DataRows(activeOther).Select(r => r.Values[0]));
        Assert.Contains("Other College", activeOther.Subtitles[1]);

        object?[] aman = DataRows(all).Single(r => (string)r.Values[0]! == "Aman").Values;
        Assert.Equal("XXXX XXXX 0124", aman[5]);
        Assert.Equal("B.Com, First year", aman[2]);
        Assert.Equal("101", aman[3]);
        Assert.Equal("Father of Aman", aman[6]);
        Assert.DoesNotContain(all.Rows.SelectMany(r => r.Values), v => v is string s && s.Contains("234567890124"));
        Assert.Equal("3 students", all.Rows[^1].Values[0]);
    }

    [Fact]
    public void RoomOccupancy_ListsStudentsAndTotals()
    {
        StudentInRoom("Ravi");
        StudentInRoom("Aman");
        RoomService.Save(new Room { HostelId = HostelId, RoomNumber = "102", SharingTypeId = SharingTypeId(2), Gender = RoomGender.Male });

        ReportTable report = ReportService.RoomOccupancy(Hostel);

        object?[] room101 = DataRows(report)[0].Values;
        Assert.Equal("101", room101[0]);
        Assert.Equal(3, room101[4]);
        Assert.Equal(2, room101[5]);
        Assert.Equal(1, room101[6]);
        Assert.Equal("Aman, Ravi", room101[7]);
        Assert.Equal("Active", room101[8]);

        ReportRow total = report.Rows[^1];
        Assert.Equal(ReportRowStyle.Total, total.Style);
        Assert.Equal(5, total.Values[4]);
        Assert.Equal(2, total.Values[5]);
        Assert.Equal(3, total.Values[6]);
    }

    [Fact]
    public void PaymentsReceived_HasDailyAndMethodTotals_WithinTheDates()
    {
        Invoice aman = FeeOf(StudentInRoom("Aman"));
        Invoice ravi = FeeOf(StudentInRoom("Ravi"));
        DateTime day1 = Today.AddDays(-3);
        DateTime day2 = Today.AddDays(-1);
        Pay(aman, 1_000m, day1);
        Pay(ravi, 2_000m, day1, PaymentMethod.Upi);
        Pay(aman, 4_000m, day2, PaymentMethod.Upi);
        Pay(aman, 8_000m, Today.AddDays(-10));   // before the From date

        ReportTable report = ReportService.PaymentsReceived(Hostel, day1, Today);

        Assert.Equal(3, DataRows(report).Count);
        List<(string? Label, object? Amount)> lines = report.Rows.Where(r => r.Style != ReportRowStyle.Normal)
            .Select(r => ((string?)r.Values[1], r.Values[6])).ToList();
        Assert.Equal(
        [
            ($"Total for {day1.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)} (2 payments)", (object?)3_000m),
            ($"Total for {day2.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)} (1 payment)", 4_000m),
            ("Cash total (1 payment)", 1_000m),
            ("UPI total (2 payments)", 6_000m),
            ("Total received (3 payments)", 7_000m),
        ], lines);

        ReportTable upiOnly = ReportService.PaymentsReceived(Hostel, day1, Today, PaymentMethod.Upi);
        Assert.Equal(6_000m, upiOnly.Rows[^1].Values[6]);
        Assert.Contains("UPI", upiOnly.Subtitles[1]);

        Assert.Throws<ValidationException>(() => ReportService.PaymentsReceived(Hostel, Today, Today.AddDays(-1)));
    }

    [Fact]
    public void Invoices_FilterByAcademicYearAndStatus_WithTotals()
    {
        int amanId = StudentInRoom("Aman");
        Invoice aman = FeeOf(amanId);
        Invoice ravi = FeeOf(StudentInRoom("Ravi"));
        Invoice amanLastYear = InvoiceService.Create(amanId, AcademicYear.Current - 1, new YearFee(30_000m, 5_000m));
        Pay(aman, 60_000m, Today);

        ReportTable thisYear = ReportService.Invoices(Hostel, AcademicYear.Current);
        ReportTable unpaid = ReportService.Invoices(Hostel, AcademicYear.Current, InvoiceStatus.Unpaid);
        ReportTable allYears = ReportService.Invoices(Hostel, null);

        Assert.Equal([aman.InvoiceNumber, ravi.InvoiceNumber], DataRows(thisYear).Select(r => r.Values[0]));
        Assert.Equal(AcademicYear.Label(AcademicYear.Current), DataRows(thisYear)[0].Values[1]);
        Assert.Equal(120_000m, thisYear.Rows[^1].Values[5]);
        Assert.Equal(60_000m, thisYear.Rows[^1].Values[6]);
        Assert.Equal(60_000m, thisYear.Rows[^1].Values[7]);
        Assert.Equal([ravi.InvoiceNumber], DataRows(unpaid).Select(r => r.Values[0]));
        Assert.Equal(3, DataRows(allYears).Count);
        Assert.Equal(amanLastYear.InvoiceNumber, DataRows(allYears)[0].Values[0]);
        Assert.Equal(5_000m, DataRows(allYears)[0].Values[4]);
        Assert.Contains("All academic years", allYears.Subtitles[1]);
    }

    [Fact]
    public void PendingDues_HasAStudentLineThenTheirInvoices()
    {
        int aman = StudentInRoom("Aman");
        Pay(FeeOf(aman), 15_000m, Today);

        ReportTable report = ReportService.PendingDues(Hostel, Today);

        Assert.Equal(ReportRowStyle.Group, report.Rows[0].Style);
        Assert.Equal("Aman", report.Rows[0].Values[0]);
        Assert.Equal("Father of Aman, 9812345678", report.Rows[0].Values[2]);
        Assert.Equal(60_000m, report.Rows[0].Values[4]);
        Assert.Equal(15_000m, report.Rows[0].Values[5]);
        Assert.Equal(45_000m, report.Rows[0].Values[6]);
        Assert.StartsWith("    SBH/", (string)report.Rows[1].Values[0]!);
        Assert.Equal(45_000m, report.Rows[^1].Values[6]);
    }

    [Fact]
    public void Pdf_LongReportHasSeveralPages()
    {
        var report = new ReportTable
        {
            Title = "Test",
            Subtitles = ["Test Hostel"],
            Columns = [new("Name", Width: 2), new("Date", ReportValueKind.Date), new("Amount", ReportValueKind.Money)],
            Rows = Enumerable.Range(1, 120).Select(i => new ReportRow([$"Student {i}", Today, 1_000m * i]))
                .Append(new ReportRow(["Total", null, 7_260_000m], ReportRowStyle.Total)).ToList(),
        };
        string path = Path.Combine(DataFolder, "Reports", "test.pdf");

        ReportPdfWriter.Write(report, path);

        using PdfSharp.Pdf.PdfDocument pdf = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        Assert.True(pdf.PageCount > 1);
    }

    [Fact]
    public void Excel_HasRealNumbersDatesAndBoldTotals()
    {
        Invoice invoice = FeeOf(StudentInRoom("Aman"));
        Pay(invoice, 1_20_00m, Today);
        ReportTable report = ReportService.PaymentsReceived(Hostel, Today, Today);
        string path = Path.Combine(DataFolder, "Reports", report.FileName + ".xlsx");

        ReportExcelWriter.Write(report, path);

        using var workbook = new XLWorkbook(path);
        IXLWorksheet sheet = workbook.Worksheet(1);
        Assert.Equal("Payments Received", sheet.Name);
        IXLCell header = sheet.CellsUsed().First(c => c.GetString() == "Receipt no.");
        IXLRow firstData = sheet.Row(header.Address.RowNumber + 1);
        Assert.Equal(Today, firstData.Cell(1).GetDateTime());
        Assert.Equal(12_000m, firstData.Cell(7).GetValue<decimal>());
        Assert.Contains("₹", firstData.Cell(7).Style.NumberFormat.Format);
        IXLRow total = sheet.LastRowUsed()!;
        Assert.Equal("Total received (1 payment)", total.Cell(2).GetString());
        Assert.True(total.Cell(2).Style.Font.Bold);
    }

    [Fact]
    public void SafeName_KeepsLettersAndDigits() =>
        Assert.Equal("Shri-Balaji-Hostel-Block-A", ReportService.SafeName("Shri Balaji Hostel / Block A"));

    [Fact]
    public void View_ShowsEveryReport()
    {
        Invoice invoice = FeeOf(StudentInRoom("Aman"));
        Pay(invoice, 5_000m, Today);
        Hostel hostel = Hostel;

        MainFormTests.RunOnStaThread(() =>
        {
            using var form = new Form { Size = new Size(1200, 800) };
            var view = new ReportsView(hostel);
            form.Controls.Add(view);
            form.Show();
            Application.DoEvents();

            ListBox list = AllControls(view).OfType<ListBox>().Single();
            DataGridView grid = AllControls(view).OfType<DataGridView>().Single();
            for (int i = 0; i < list.Items.Count; i++)
            {
                list.SelectedIndex = i;
                Application.DoEvents();
                Assert.True(grid.Columns.Count > 0, $"{list.Items[i]} shows no columns");
                Assert.True(grid.Rows.Count > 0, $"{list.Items[i]} shows no rows");
            }
            form.Close();
        });
    }

    private static IEnumerable<Control> AllControls(Control parent) =>
        parent.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(AllControls(c)));
}
