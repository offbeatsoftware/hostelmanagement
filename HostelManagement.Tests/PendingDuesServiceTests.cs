using HostelManagement.Forms.Views;
using HostelManagement.Models;
using HostelManagement.Reports;
using HostelManagement.Services;
using PdfSharp.Pdf.IO;
using Xunit;

namespace HostelManagement.Tests;

public sealed class PendingDuesServiceTests : TestDatabase
{
    private static readonly DateTime Today = DateTime.Today;

    // The two half yearly periods before the current one; invoices are 60000 each.
    private static BillingPeriod Previous => BillingPeriods.For(
        BillingPeriods.For(Today, BillingFrequency.HalfYearly).From.AddDays(-1), BillingFrequency.HalfYearly);

    private static BillingPeriod BeforePrevious => BillingPeriods.For(Previous.From.AddDays(-1), BillingFrequency.HalfYearly);

    private Room? _room;

    private int StudentInRoom(string name)
    {
        if (_room is null)
        {
            RoomService.UpdateRent(SharingTypeId(3), 120_000m);
            _room = RoomService.Save(new Room { HostelId = HostelId, RoomNumber = "101", SharingTypeId = SharingTypeId(3), Gender = RoomGender.Male });
        }

        int id = StudentService.Save(
            new Student { StudentName = name, Gender = RoomGender.Male, Mobile = "9876543210", CollegeId = CollegeId, AdmissionDate = BeforePrevious.From },
            new Parent { ParentName = $"Parent of {name}", Mobile = "9812345678", Email = "parent@example.com" }).StudentId;
        AllocationService.CheckIn(id, _room.RoomId, BeforePrevious.From);
        return id;
    }

    private static Invoice Invoice(int studentId, BillingPeriod period, int daysAgo) =>
        InvoiceService.Create(studentId, period, Today.AddDays(-daysAgo));

    private static void Pay(Invoice invoice, decimal amount) =>
        PaymentService.Record(new Payment { InvoiceId = invoice.InvoiceId, PaymentDate = Today, Amount = amount });

    [Fact]
    public void Invoice_IsDueFifteenDaysAfterItsDate_AndOverdueAfterThat()
    {
        var invoice = new Invoice { InvoiceDate = new DateTime(2026, 7, 1), TotalAmount = 1000m };

        Assert.Equal(new DateTime(2026, 7, 16), invoice.DueDate);
        Assert.Equal(0, invoice.DaysOverdue(new DateTime(2026, 7, 16)));
        Assert.Equal(1, invoice.DaysOverdue(new DateTime(2026, 7, 17)));
        Assert.Equal(30, invoice.DaysOverdue(new DateTime(2026, 8, 15)));

        invoice.PaidAmount = 1000m;
        Assert.Equal(0, invoice.DaysOverdue(new DateTime(2026, 8, 15)));
    }

    [Fact]
    public void GetDues_GroupsUnpaidInvoicesPerStudent_MostOverdueFirst()
    {
        int aman = StudentInRoom("Aman");
        int ravi = StudentInRoom("Ravi");
        int paidUp = StudentInRoom("Paid Up");

        Invoice amanOld = Invoice(aman, BeforePrevious, daysAgo: 40);   // 25 days overdue
        Invoice amanNew = Invoice(aman, Previous, daysAgo: 5);          // not yet due
        Invoice raviOld = Invoice(ravi, Previous, daysAgo: 60);         // 45 days overdue
        Pay(amanOld, 10_000m);
        Pay(Invoice(paidUp, Previous, daysAgo: 60), 60_000m);

        List<StudentDue> dues = PendingDuesService.GetDues(HostelId, Today);

        Assert.Equal(["Ravi", "Aman"], dues.Select(d => d.StudentName));
        StudentDue ravis = dues[0];
        Assert.Equal(45, ravis.DaysOverdue);
        Assert.Equal(60_000m, ravis.OverdueAmount);
        Assert.Equal($"Overdue 45 days", ravis.DueText);
        Assert.Equal("101", ravis.RoomNumber);
        Assert.Equal("Parent of Ravi, 9812345678", ravis.ParentText);
        Assert.Equal([raviOld.InvoiceId], ravis.Invoices.Select(i => i.InvoiceId));

        StudentDue amans = dues[1];
        Assert.Equal([amanOld.InvoiceId, amanNew.InvoiceId], amans.Invoices.Select(i => i.InvoiceId));
        Assert.Equal(2, amans.InvoiceCount);
        Assert.Equal(110_000m, amans.PendingAmount);
        Assert.Equal(50_000m, amans.OverdueAmount);
        Assert.Equal(25, amans.DaysOverdue);
    }

    [Fact]
    public void GetDues_InvoiceNotYetDue_IsPendingButNotOverdue()
    {
        Invoice(StudentInRoom("Aman"), Previous, daysAgo: 15);

        StudentDue due = PendingDuesService.GetDues(HostelId, Today).Single();

        Assert.False(due.IsOverdue);
        Assert.Equal(0m, due.OverdueAmount);
        Assert.Equal(60_000m, due.PendingAmount);
        Assert.Equal($"Due {Today:dd MMM yyyy}", due.DueText);
    }

    [Fact]
    public void GetDues_IncludesStudentsWhoHaveLeft()
    {
        int student = StudentInRoom("Aman");
        Invoice(student, Previous, daysAgo: 30);
        AllocationService.CheckOut(student, Today);

        StudentDue due = PendingDuesService.GetDues(HostelId, Today).Single();

        Assert.Equal(StudentStatus.Left, due.StudentStatus);
        Assert.Equal(string.Empty, due.RoomNumber);
        Assert.Equal(60_000m, due.PendingAmount);
    }

    [Fact]
    public void GetDues_ShowsOnlyTheSelectedHostel()
    {
        Invoice(StudentInRoom("Aman"), Previous, daysAgo: 30);

        Assert.Empty(PendingDuesService.GetDues(AddHostel("Other Hostel"), Today));
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
            ParentName = $"Parent {n}",
            ParentMobile = "9812345678",
            AsOf = Today,
            Invoices =
            [
                new Invoice { InvoiceNumber = $"SBH/2026-27/{n:0000}", InvoiceDate = Today.AddDays(-40), BillingFrom = Previous.From,
                              BillingTo = Previous.To, TotalAmount = 60_000m, PaidAmount = 10_000m },
                new Invoice { InvoiceNumber = $"SBH/2026-27/{n + 100:0000}", InvoiceDate = Today, BillingFrom = Previous.From,
                              BillingTo = Previous.To, TotalAmount = 30_000m },
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

        Assert.Equal("PendingDues_Shri-Balaji-Hostel-Block-A_2026-10-06.pdf",
            PendingDuesPdfWriter.FileName(hostel, new DateTime(2026, 10, 6)));
    }

    [Fact]
    public void View_ShowsOneRowPerStudentAndTheirInvoices()
    {
        int aman = StudentInRoom("Aman");
        Invoice(aman, BeforePrevious, daysAgo: 40);
        Invoice(aman, Previous, daysAgo: 5);
        Invoice(StudentInRoom("Ravi"), Previous, daysAgo: 60);
        Hostel hostel = HostelService.GetHostel(HostelId)!;

        MainFormTests.RunOnStaThread(() =>
        {
            using var form = new Form { Size = new Size(1200, 800) };
            var view = new PendingDuesView(hostel);
            form.Controls.Add(view);
            form.Show();
            Application.DoEvents();

            List<DataGridView> grids = AllControls(view).OfType<DataGridView>().ToList();
            Assert.Equal(2, grids[0].Rows.Count);
            Assert.Equal(1, grids[1].Rows.Count); // Ravi, the most overdue, is selected first

            grids[0].CurrentCell = grids[0].Rows[1].Cells[0];
            Application.DoEvents();
            Assert.Equal(2, grids[1].Rows.Count);

            CheckBox overdueOnly = AllControls(view).OfType<CheckBox>().Single();
            overdueOnly.Checked = true;
            Assert.Equal(2, grids[0].Rows.Count);
            form.Close();
        });
    }

    private static IEnumerable<Control> AllControls(Control parent) =>
        parent.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(AllControls(c)));
}
