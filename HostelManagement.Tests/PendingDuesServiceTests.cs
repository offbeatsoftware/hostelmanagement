using HostelManagement.Forms.Views;
using HostelManagement.Models;
using HostelManagement.Reports;
using HostelManagement.Services;
using PdfSharp.Pdf.IO;
using Xunit;

namespace HostelManagement.Tests;

/// <summary>Pending fees and the Fee Reminders screen (no due dates; the admin reminds whenever he wants).</summary>
public sealed class PendingDuesServiceTests : TestDatabase
{
    private static readonly DateTime Today = DateTime.Today;

    private Room? _room;

    private Room Room => _room ??= AddRoom(beds: 3);

    [Fact]
    public void GetDues_GroupsInvoicesPerStudent_HighestPendingFirst()
    {
        int aman = AddStudentWithParents("Aman");
        int ravi = AddStudentWithParents("Ravi");
        int paidUp = AddStudentWithParents("Paid Up");
        Invoice amanLastYear = InvoiceService.Create(aman, AcademicYear.Current - 1, new YearFee(30_000m, 0m));
        Invoice amanThisYear = CheckInWithFee(aman, Room, 50_000m, 12_000m);
        Pay(amanThisYear.InvoiceId, 10_000m);
        Pay(amanThisYear.InvoiceId, 5_000m);
        CheckInWithFee(ravi, Room, 45_000m);
        Pay(CheckInWithFee(paidUp, Room, 40_000m).InvoiceId, 40_000m);

        List<StudentDue> dues = PendingDuesService.GetDues(HostelId);

        Assert.Equal(["Aman", "Ravi"], dues.Select(d => d.StudentName));
        StudentDue amans = dues[0];
        Assert.Equal([amanLastYear.InvoiceId, amanThisYear.InvoiceId], amans.Invoices.Select(i => i.InvoiceId));
        Assert.Equal(92_000m, amans.TotalAmount);
        Assert.Equal(15_000m, amans.PaidAmount);
        Assert.Equal(77_000m, amans.PendingAmount);
        Assert.Equal($"{AcademicYear.Label(AcademicYear.Current - 1)}, {AcademicYear.Label(AcademicYear.Current)}", amans.YearsText);
        Assert.Equal("101", amans.RoomNumber);
        Assert.Equal("Rakesh Aman, 9812345678", amans.FatherText);
        Assert.Equal(45_000m, dues[1].PendingAmount);
    }

    [Fact]
    public void GetDues_ShowWhoTheReminderGoesTo_FatherThenMother()
    {
        CheckInWithFee(AddStudentWithParents("Father Email", fatherEmail: "father@example.com", motherEmail: "mother@example.com"), Room, 1_000m);
        CheckInWithFee(AddStudentWithParents("Mother Email", fatherEmail: "", motherEmail: "mother@example.com"), Room, 2_000m);
        CheckInWithFee(AddStudentWithParents("No Email", fatherEmail: "", motherEmail: ""), Room, 3_000m);

        Dictionary<string, string> emailText = PendingDuesService.GetDues(HostelId).ToDictionary(d => d.StudentName, d => d.EmailText);

        Assert.Equal("Father: father@example.com", emailText["Father Email"]);
        Assert.Equal("Mother: mother@example.com", emailText["Mother Email"]);
        Assert.Equal("No father or mother email", emailText["No Email"]);
    }

    [Fact]
    public void GetDues_IncludesStudentsWhoHaveLeft()
    {
        int student = AddStudentWithParents("Aman");
        CheckInWithFee(student, Room, 60_000m);
        AllocationService.CheckOut(student, Today);

        StudentDue due = PendingDuesService.GetDues(HostelId).Single();

        Assert.Equal(StudentStatus.Left, due.StudentStatus);
        Assert.Equal(string.Empty, due.RoomNumber);
        Assert.Equal(60_000m, due.PendingAmount);
    }

    [Fact]
    public void GetDues_ShowsOnlyTheSelectedHostel()
    {
        CheckInWithFee(AddStudentWithParents("Aman"), Room, 60_000m);

        Assert.Empty(PendingDuesService.GetDues(AddHostel("Other Hostel")));
    }

    [Fact]
    public void Pdf_LongListContinuesOnFurtherPages()
    {
        Hostel hostel = HostelService.GetHostel(HostelId)!;
        List<StudentDue> dues = Enumerable.Range(1, 60).Select(n => new StudentDue
        {
            StudentId = n,
            StudentName = $"Student {n}",
            StudentStatus = n % 10 == 0 ? StudentStatus.Left : StudentStatus.Active,
            RoomNumber = $"{100 + n}",
            FatherName = $"Father {n}",
            FatherMobile = "9812345678",
            Invoices =
            [
                new Invoice { InvoiceNumber = $"SBH/2025-26/{n:0000}", AcademicYear = 2025, RoomRent = 50_000m, PaidAmount = 10_000m },
                new Invoice { InvoiceNumber = $"SBH/2026-27/{n:0000}", AcademicYear = 2026, RoomRent = 50_000m, TransportAmount = 12_000m },
            ],
        }).ToList();
        string path = Path.Combine(DataFolder, "Reports", PendingDuesPdfWriter.FileName(hostel, Today));

        PendingDuesPdfWriter.Write(hostel, dues, Today, path);

        using PdfSharp.Pdf.PdfDocument pdf = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        Assert.True(pdf.PageCount > 1, $"Expected several pages, got {pdf.PageCount}.");
    }

    [Fact]
    public void Pdf_WithoutDues_IsStillWritten()
    {
        Hostel hostel = HostelService.GetHostel(HostelId)!;
        string path = Path.Combine(DataFolder, "Reports", "empty.pdf");

        PendingDuesPdfWriter.Write(hostel, [], Today, path);

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(path), 0, 4));
    }

    [Fact]
    public void Pdf_FileNameIsSafe()
    {
        var hostel = new Hostel { HostelName = "Shri Balaji Hostel / Block A" };

        Assert.Equal("PendingFees_Shri-Balaji-Hostel-Block-A_2026-10-06.pdf",
            PendingDuesPdfWriter.FileName(hostel, new DateTime(2026, 10, 6)));
    }

    [Fact]
    public void View_ListsStudents_TickBoxesChooseWhoToRemind_AndThePreviewShowsTheAmounts()
    {
        Invoice aman = CheckInWithFee(AddStudentWithParents("Aman"), Room, 50_000m, 12_000m);
        Pay(aman.InvoiceId, 15_000m);
        CheckInWithFee(AddStudentWithParents("Ravi"), Room, 45_000m);
        Hostel hostel = HostelService.GetHostel(HostelId)!;

        MainFormTests.RunOnStaThread(() =>
        {
            using var form = new Form { Size = new Size(1280, 800) };
            var view = new FeeRemindersView(hostel);
            form.Controls.Add(view);
            form.Show();
            Application.DoEvents();

            DataGridView grid = AllControls(view).OfType<DataGridView>().Single();
            Button send = AllControls(view).OfType<Button>().Single(b => b.Text.StartsWith("Send Reminder", StringComparison.Ordinal));
            Assert.Equal(2, grid.Rows.Count);
            Assert.False(send.Enabled);

            grid.CurrentCell = grid.Rows[0].Cells[0];
            grid.Rows[0].Cells[0].Value = true;
            grid.NotifyCurrentCellDirty(true);
            Application.DoEvents();
            Assert.True(send.Enabled);
            Assert.Equal("Send Reminder (1)", send.Text);

            AllControls(view).OfType<Button>().Single(b => b.Text == "Select All").PerformClick();
            Assert.Equal("Send Reminder (2)", send.Text);

            TextBox preview = AllControls(view).OfType<TextBox>().Single(t => t.ReadOnly);
            Assert.Contains("To: Father: Rakesh Aman <rakesh@example.com>", preview.Text);
            Assert.Contains("Pending amount: Rs. 47,000.00", preview.Text);
            Assert.Contains("Paid so far: Rs. 15,000.00", preview.Text);
            form.Close();
        });
    }

    private static IEnumerable<Control> AllControls(Control parent) =>
        parent.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(AllControls(c)));
}
