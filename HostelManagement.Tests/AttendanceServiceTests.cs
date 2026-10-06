using HostelManagement.Data;
using HostelManagement.Forms.Views;
using HostelManagement.Models;
using HostelManagement.Services;
using Xunit;

namespace HostelManagement.Tests;

public sealed class AttendanceServiceTests : TestDatabase
{
    private sealed class FakeSender : IEmailSender
    {
        public List<OutgoingEmail> Sent { get; } = [];
        public void Send(EmailSettings settings, OutgoingEmail email) => Sent.Add(email);
    }

    private static readonly DateTime Today = DateTime.Today;
    private readonly FakeSender _sender = new();
    private Room? _room;

    public AttendanceServiceTests()
    {
        EmailService.Sender = _sender;
    }

    private Room Room => _room ??= RoomService.Save(new Room
        { HostelId = HostelId, RoomNumber = "101", SharingTypeId = SharingTypeId(3), Gender = RoomGender.Male });

    private int StudentInRoom(string name, DateTime checkIn)
    {
        int id = StudentService.Save(
            new Student { StudentName = name, Gender = RoomGender.Male, Mobile = "9876543210", CollegeId = CollegeId, AdmissionDate = checkIn },
            new Parent { ParentName = $"Parent of {name}", Mobile = "9812345678", Email = $"{name.ToLowerInvariant()}.parent@example.com" }).StudentId;
        AllocationService.CheckIn(id, Room.RoomId, checkIn);
        return id;
    }

    private static void SetUpGmail() =>
        EmailSettingsService.Save(new EmailSettings { SenderEmail = "hostel@gmail.com", AppPassword = "abcdabcdabcdabcd" });

    private void MarkAbsent(DateTime date, params int[] studentIds)
    {
        AttendanceSheet sheet = AttendanceService.GetSheet(HostelId, date);
        foreach (AttendanceEntry entry in sheet.Entries)
        {
            entry.IsPresent = !studentIds.Contains(entry.StudentId);
        }
        AttendanceService.Save(HostelId, date, sheet.Entries);
    }

    [Fact]
    public void Sheet_ListsTheStudentsInARoomThatNight_AllPresentAtFirst()
    {
        int aman = StudentInRoom("Aman", Today.AddDays(-10));
        int ravi = StudentInRoom("Ravi", Today.AddDays(-10));
        StudentInRoom("Karan", Today);                      // joined today: not on yesterday's sheet
        AllocationService.CheckOut(ravi, Today.AddDays(-1)); // left yesterday: not on yesterday's sheet

        AttendanceSheet yesterday = AttendanceService.GetSheet(HostelId, Today.AddDays(-1));
        AttendanceSheet twoDaysAgo = AttendanceService.GetSheet(HostelId, Today.AddDays(-2));

        Assert.Equal(["Aman"], yesterday.Entries.Select(e => e.StudentName));
        Assert.Equal(["Aman", "Ravi"], twoDaysAgo.Entries.Select(e => e.StudentName));
        Assert.All(twoDaysAgo.Entries, e => Assert.True(e.IsPresent));
        Assert.False(twoDaysAgo.IsMarked);
        Assert.Equal("101, bed 1", twoDaysAgo.Entries.Single(e => e.StudentId == aman).RoomAndBed);
        Assert.Equal("aman.parent@example.com", twoDaysAgo.Entries.Single(e => e.StudentId == aman).ParentEmail);
    }

    [Fact]
    public void Save_StoresAbsences_AndCanBeCorrectedLater()
    {
        int aman = StudentInRoom("Aman", Today.AddDays(-5));
        int ravi = StudentInRoom("Ravi", Today.AddDays(-5));
        DateTime date = Today.AddDays(-2);

        MarkAbsent(date, ravi);
        AttendanceSheet saved = AttendanceService.GetSheet(HostelId, date);
        Assert.True(saved.IsMarked);
        Assert.Equal(1, saved.AbsentCount);
        Assert.False(saved.Entries.Single(e => e.StudentId == ravi).IsPresent);

        MarkAbsent(date, aman);
        AttendanceSheet corrected = AttendanceService.GetSheet(HostelId, date);
        Assert.Equal([aman], corrected.Entries.Where(e => !e.IsPresent).Select(e => e.StudentId));
        Assert.Equal(2, Count("Attendance"));

        Assert.False(AttendanceService.GetSheet(HostelId, Today).IsMarked);
    }

    [Fact]
    public void Save_Rules()
    {
        StudentInRoom("Aman", Today.AddDays(-5));
        AttendanceSheet sheet = AttendanceService.GetSheet(HostelId, Today);

        Assert.Contains("future", Assert.Throws<ValidationException>(
            () => AttendanceService.Save(HostelId, Today.AddDays(1), sheet.Entries)).Message);
        Assert.Contains("not in a room", Assert.Throws<ValidationException>(
            () => AttendanceService.Save(HostelId, Today.AddDays(-6), sheet.Entries)).Message);
        sheet.Entries[0].Remarks = new string('x', 256);
        Assert.Contains("at most 255", Assert.Throws<ValidationException>(
            () => AttendanceService.Save(HostelId, Today, sheet.Entries)).Message);
        Assert.Equal(0, Count("Attendance"));
    }

    [Fact]
    public void AbsenceEmails_GoToTheParentsOfAbsentStudents_Once()
    {
        SetUpGmail();
        AuthService.SaveContact("owner@gmail.com", "");
        StudentInRoom("Aman", Today.AddDays(-5));
        int ravi = StudentInRoom("Ravi", Today.AddDays(-5));
        DateTime date = Today.AddDays(-1);
        MarkAbsent(date, ravi);

        (List<OutgoingEmail> emails, List<string> problems) = EmailService.PrepareAbsences(HostelId, date);

        Assert.Empty(problems);
        OutgoingEmail email = Assert.Single(emails);
        Assert.Equal("ravi.parent@example.com", email.RecipientEmail);
        Assert.Equal("owner@gmail.com", email.CopyToEmail);
        Assert.Equal(EmailType.Absence, email.EmailType);
        Assert.Contains($"on {date:dd MMM yyyy}", email.Subject);
        Assert.Contains("Ravi (room 101) was not present", email.Body);

        Assert.Null(EmailService.SendAndLog(email));
        Assert.NotNull(AttendanceService.GetSheet(HostelId, date).Entries.Single(e => e.StudentId == ravi).ParentEmailedDate);
        Assert.Empty(EmailService.PrepareAbsences(HostelId, date).Emails);
        Assert.Single(EmailService.PrepareAbsences(HostelId, date, includeAlreadyEmailed: true).Emails);
        Assert.Equal(EmailType.Absence, EmailService.GetHistory().Single().EmailType);
    }

    [Fact]
    public void AbsenceEmail_TextCanBeEdited()
    {
        SetUpGmail();
        EmailSettings settings = EmailSettingsService.Get();
        Assert.Equal(EmailSettings.DefaultAbsence, settings.Absence);

        settings.Absence = new EmailTemplate("Absent: {StudentName}", "{StudentName} absent on {AttendanceDate}. {Remarks}");
        EmailSettingsService.Save(settings);
        int aman = StudentInRoom("Aman", Today.AddDays(-5));
        AttendanceSheet sheet = AttendanceService.GetSheet(HostelId, Today);
        sheet.Entries[0].IsPresent = false;
        sheet.Entries[0].Remarks = "Went home without leave";
        AttendanceService.Save(HostelId, Today, sheet.Entries);

        OutgoingEmail email = EmailService.PrepareAbsences(HostelId, Today).Emails.Single();
        Assert.Equal("Absent: Aman", email.Subject);
        Assert.Equal($"Aman absent on {Today:dd MMM yyyy}. Went home without leave", email.Body);

        settings.Absence = new EmailTemplate("Absent", "{InvoiceNumber}");
        Assert.Contains("{InvoiceNumber}", Assert.Throws<ValidationException>(() => EmailSettingsService.Save(settings)).Message);
    }

    [Fact]
    public void View_MarkAbsentSaveAndListAbsent()
    {
        StudentInRoom("Aman", Today.AddDays(-5));
        StudentInRoom("Ravi", Today.AddDays(-5));
        Hostel hostel = HostelService.GetHostel(HostelId)!;

        MainFormTests.RunOnStaThread(() =>
        {
            using var form = new Form { Size = new Size(1200, 800) };
            var view = new AttendanceView(hostel);
            form.Controls.Add(view);
            form.Show();
            Application.DoEvents();

            List<DataGridView> grids = AllControls(view).OfType<DataGridView>().ToList();
            DataGridView sheet = grids[0];
            DataGridView absent = grids[1];
            Assert.Equal(2, sheet.Rows.Count);
            Assert.Equal(0, absent.Rows.Count);

            var entries = (System.ComponentModel.BindingList<AttendanceEntry>)sheet.DataSource!;
            entries[1].IsPresent = false;
            AllControls(view).OfType<Button>().Single(b => b.Text == "Save Attendance").PerformClick();
            Application.DoEvents();

            Assert.Equal(1, absent.Rows.Count);
            form.Close();
        });
    }

    private static IEnumerable<Control> AllControls(Control parent) =>
        parent.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(AllControls(c)));
}
