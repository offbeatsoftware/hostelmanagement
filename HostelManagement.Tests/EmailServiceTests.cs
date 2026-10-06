using System.Net.Mail;
using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Services;
using Xunit;

namespace HostelManagement.Tests;

public sealed class EmailServiceTests : TestDatabase
{
    /// <summary>Records the emails instead of sending them; can be told to fail.</summary>
    private sealed class FakeSender : IEmailSender
    {
        public List<OutgoingEmail> Sent { get; } = [];
        public Exception? Failure { get; set; }

        public void Send(EmailSettings settings, OutgoingEmail email)
        {
            Assert.Equal("hostel@gmail.com", settings.SenderEmail);
            if (Failure is not null)
            {
                throw Failure;
            }
            Sent.Add(email);
        }
    }

    private readonly FakeSender _sender = new();
    private static readonly DateTime Today = DateTime.Today;
    private static BillingPeriod Previous => BillingPeriods.For(
        BillingPeriods.For(Today, BillingFrequency.HalfYearly).From.AddDays(-1), BillingFrequency.HalfYearly);
    private static BillingPeriod BeforePrevious => BillingPeriods.For(Previous.From.AddDays(-1), BillingFrequency.HalfYearly);
    private Room? _room;

    public EmailServiceTests()
    {
        EmailService.Sender = _sender;
    }

    private static void SetUpGmail() =>
        EmailSettingsService.Save(new EmailSettings { SenderEmail = "hostel@gmail.com", SenderName = "Shri Balaji Hostel", AppPassword = "abcd efgh ijkl mnop" });

    private int StudentInRoom(string name)
    {
        if (_room is null)
        {
            HostelService.Save(new Hostel { HostelId = HostelId, HostelName = "Test Hostel", Phone = "0141 2222222" });
            RoomService.UpdateRent(SharingTypeId(3), 120_000m);
            _room = RoomService.Save(new Room { HostelId = HostelId, RoomNumber = "101", SharingTypeId = SharingTypeId(3), Gender = RoomGender.Male });
        }
        int id = StudentService.Save(
            new Student { StudentName = name, Gender = RoomGender.Male, Mobile = "9876543210", CollegeId = CollegeId, AdmissionDate = BeforePrevious.From },
            new Parent { ParentName = $"Parent of {name}", Mobile = "9812345678", Email = $"{name.ToLowerInvariant()}.parent@example.com" }).StudentId;
        AllocationService.CheckIn(id, _room.RoomId, BeforePrevious.From);
        return id;
    }

    private static Invoice Invoice(int studentId, BillingPeriod period, int daysAgo) =>
        InvoiceService.Create(studentId, period, Today.AddDays(-daysAgo));

    [Fact]
    public void Settings_AreSavedWithTheAppPasswordEncrypted()
    {
        SetUpGmail();

        EmailSettings settings = EmailSettingsService.Get();
        Assert.Equal("hostel@gmail.com", settings.SenderEmail);
        Assert.Equal("abcdefghijklmnop", settings.AppPassword);
        Assert.Equal(EmailSettings.GmailHost, settings.SmtpHost);
        Assert.Equal(587, settings.SmtpPort);
        Assert.Equal(EmailSettings.DefaultInvoice, settings.Invoice);
        Assert.True(settings.IsConfigured);

        string stored = Convert.ToString(Db.Scalar("SELECT [SettingValue] FROM [AppSetting] WHERE [SettingKey] = 'Email.AppPassword'"))!;
        Assert.StartsWith("dpapi:", stored);
        Assert.DoesNotContain("abcd", stored);
    }

    [Fact]
    public void Settings_EditedTextsAreKept()
    {
        SetUpGmail();
        EmailSettings settings = EmailSettingsService.Get();
        settings.Reminder = new EmailTemplate("Fees due for {StudentName}", "Dear {ParentName}, please pay {Overdue}.");

        EmailSettingsService.Save(settings);

        Assert.Equal("Fees due for {StudentName}", EmailSettingsService.Get().Reminder.Subject);
        Assert.Equal(EmailSettings.DefaultReceipt, EmailSettingsService.Get().Receipt);
    }

    [Fact]
    public void Settings_Rules()
    {
        Assert.Contains("Gmail address", Assert.Throws<ValidationException>(() => EmailSettingsService.Save(new EmailSettings { SenderEmail = "", AppPassword = "x" })).Message);
        Assert.Contains("valid sender", Assert.Throws<ValidationException>(() => EmailSettingsService.Save(new EmailSettings { SenderEmail = "hostel", AppPassword = "x" })).Message);
        Assert.Contains("app password", Assert.Throws<ValidationException>(() => EmailSettingsService.Save(new EmailSettings { SenderEmail = "a@gmail.com" })).Message);

        var unknownField = new EmailSettings { SenderEmail = "a@gmail.com", AppPassword = "x", Receipt = new("Receipt {ReceiptNo}", "Body") };
        var ex = Assert.Throws<ValidationException>(() => EmailSettingsService.Save(unknownField));
        Assert.Contains("{ReceiptNo}", ex.Message);
        Assert.Contains("{ReceiptNumber}", ex.Message);

        var empty = new EmailSettings { SenderEmail = "a@gmail.com", AppPassword = "x", Invoice = new("Subject", "  ") };
        Assert.Contains("invoice email", Assert.Throws<ValidationException>(() => EmailSettingsService.Save(empty)).Message);
    }

    [Fact]
    public void Settings_StartWithTheHostelsGmailAddress_ButNoPassword()
    {
        EmailSettings settings = EmailSettingsService.Get();

        Assert.Equal("shribalajihostelsuddhowala@gmail.com", settings.SenderEmail);
        Assert.Equal("", settings.AppPassword);
        Assert.False(settings.IsConfigured);
    }

    [Fact]
    public void TestEmail_GoesToTheChosenAddress_OrToTheSender()
    {
        SetUpGmail();
        EmailSettings settings = EmailSettingsService.Get();

        Assert.Null(EmailService.SendTest(settings, " owner@example.com "));
        Assert.Null(EmailService.SendTest(settings));
        Assert.Contains("valid email", EmailService.SendTest(settings, "not an address"));

        Assert.Equal(["owner@example.com", "hostel@gmail.com"], _sender.Sent.Select(e => e.RecipientEmail));
        Assert.Empty(EmailService.GetHistory());
    }

    [Fact]
    public void Fill_ReplacesKnownFieldsAndLeavesOthers() =>
        Assert.Equal("Dear Rakesh, {Unknown}",
            EmailSettingsService.Fill("Dear {ParentName}, {Unknown}", new Dictionary<string, string> { ["ParentName"] = "Rakesh" }));

    [Fact]
    public void PrepareInvoice_WithoutGmailAccount_AsksToSetItUp()
    {
        Invoice invoice = Invoice(StudentInRoom("Aman"), Previous, daysAgo: 1);

        Assert.Contains("Email Settings", Assert.Throws<ValidationException>(() => EmailService.PrepareInvoice(invoice.InvoiceId)).Message);
    }

    [Fact]
    public void InvoiceEmail_GoesToTheParentWithThePdf_AndIsLogged()
    {
        SetUpGmail();
        Invoice invoice = Invoice(StudentInRoom("Aman"), Previous, daysAgo: 1);

        OutgoingEmail email = EmailService.PrepareInvoice(invoice.InvoiceId);
        Assert.Null(EmailService.SendAndLog(email));

        OutgoingEmail sent = _sender.Sent.Single();
        Assert.Equal("aman.parent@example.com", sent.RecipientEmail);
        Assert.Equal($"Invoice {invoice.InvoiceNumber} for Aman, Test Hostel", sent.Subject);
        Assert.Contains("Dear Parent of Aman,", sent.Body);
        Assert.Contains("Amount: Rs. 60,000.00", sent.Body);
        Assert.Contains($"Due date: {invoice.DueDate:dd MMM yyyy}", sent.Body);
        Assert.Contains("0141 2222222", sent.Body);
        Assert.DoesNotContain("{", sent.Body);
        Assert.True(File.Exists(sent.AttachmentPaths.Single()));

        EmailHistoryEntry history = EmailService.GetHistory().Single();
        Assert.Equal(EmailType.Invoice, history.EmailType);
        Assert.Equal(EmailStatus.Sent, history.Status);
        Assert.Equal(invoice.InvoiceNumber, history.InvoiceNumber);
        Assert.Equal("Aman", history.StudentName);
        Assert.Throws<ValidationException>(() => InvoiceService.Delete(invoice.InvoiceId));
    }

    [Fact]
    public void EveryEmail_IsCopiedToTheAdmin()
    {
        SetUpGmail();
        AuthService.SaveContact("owner@gmail.com", "98290 12345");
        Invoice invoice = Invoice(StudentInRoom("Aman"), Previous, daysAgo: 1);
        EmailSettings settings = EmailSettingsService.Get();
        settings.Invoice = new EmailTemplate("Invoice {InvoiceNumber}", "Questions? Call {AdminPhone} or write to {AdminEmail}.");
        EmailSettingsService.Save(settings);

        OutgoingEmail email = EmailService.PrepareInvoice(invoice.InvoiceId);

        Assert.Equal("owner@gmail.com", email.CopyToEmail);
        Assert.Equal("Questions? Call 98290 12345 or write to owner@gmail.com.", email.Body);
        Payment payment = PaymentService.Record(new Payment { InvoiceId = invoice.InvoiceId, PaymentDate = Today, Amount = 100m });
        Assert.Equal("owner@gmail.com", EmailService.PrepareReceipt(payment.PaymentId).CopyToEmail);
    }

    [Fact]
    public void NoAdminEmail_NoCopy()
    {
        SetUpGmail();
        Invoice invoice = Invoice(StudentInRoom("Aman"), Previous, daysAgo: 1);

        Assert.Equal("", EmailService.PrepareInvoice(invoice.InvoiceId).CopyToEmail);
    }

    [Fact]
    public void FailedEmail_IsLoggedWithAMessageTheAdminCanActOn()
    {
        SetUpGmail();
        Invoice invoice = Invoice(StudentInRoom("Aman"), Previous, daysAgo: 1);
        _sender.Failure = new SmtpException("The SMTP server requires a secure connection or the client was not authenticated. " +
                                            "The server response was: 5.7.0 Authentication Required.");

        string? error = EmailService.SendAndLog(EmailService.PrepareInvoice(invoice.InvoiceId));

        Assert.Contains("did not accept", error);
        EmailHistoryEntry history = EmailService.GetHistory().Single();
        Assert.Equal(EmailStatus.Failed, history.Status);
        Assert.Equal(error, history.ErrorMessage);
    }

    [Fact]
    public void ReceiptEmail_ShowsThePaymentAndBalance()
    {
        SetUpGmail();
        Invoice invoice = Invoice(StudentInRoom("Aman"), Previous, daysAgo: 1);
        Payment payment = PaymentService.Record(new Payment
            { InvoiceId = invoice.InvoiceId, PaymentDate = Today, Amount = 15_000m, PaymentMethod = PaymentMethod.Upi, Reference = "UPI1" });

        OutgoingEmail email = EmailService.PrepareReceipt(payment.PaymentId);

        Assert.Equal(EmailType.Receipt, email.EmailType);
        Assert.Contains(payment.ReceiptNumber, email.Subject);
        Assert.Contains("Rs. 15,000.00", email.Body);
        Assert.Contains("Balance pending on this invoice: Rs. 45,000.00", email.Body);
        Assert.StartsWith("Receipt_", Path.GetFileName(email.AttachmentPaths.Single()));
    }

    [Fact]
    public void Reminders_GoOnlyToStudentsWithOverdueInvoices_AndListOnlyThoseInvoices()
    {
        SetUpGmail();
        int aman = StudentInRoom("Aman");
        int ravi = StudentInRoom("Ravi");
        Invoice amanOverdue = Invoice(aman, BeforePrevious, daysAgo: 40);
        Invoice amanNotDue = Invoice(aman, Previous, daysAgo: 2);
        Invoice(ravi, Previous, daysAgo: 2);

        (List<OutgoingEmail> emails, List<string> problems) = EmailService.PrepareReminders(HostelId, Today);

        Assert.Empty(problems);
        OutgoingEmail reminder = Assert.Single(emails);
        Assert.Equal("aman.parent@example.com", reminder.RecipientEmail);
        Assert.Equal([amanOverdue.InvoiceId], reminder.InvoiceIds);
        Assert.Contains(amanOverdue.InvoiceNumber, reminder.Body);
        Assert.DoesNotContain(amanNotDue.InvoiceNumber, reminder.Body);
        Assert.Contains("Total pending: Rs. 1,20,000.00", reminder.Body);
        Assert.Single(reminder.AttachmentPaths);

        Assert.Null(PendingDuesService.GetDues(HostelId, Today).Single(d => d.StudentId == aman).LastReminderDate);
        EmailService.SendAndLog(reminder);
        Assert.Equal(Today, PendingDuesService.GetDues(HostelId, Today).Single(d => d.StudentId == aman).LastReminderDate?.Date);
    }

    [Fact]
    public void Reminder_StudentWithNothingOverdue_IsRejected()
    {
        SetUpGmail();
        Invoice(StudentInRoom("Aman"), Previous, daysAgo: 2);
        StudentDue due = PendingDuesService.GetDues(HostelId, Today).Single();

        Assert.Contains("no overdue", Assert.Throws<ValidationException>(() => EmailService.PrepareReminder(due)).Message);
    }

    [Fact]
    public void Screens_WithEmailButtonsCanBeCreated()
    {
        Hostel hostel = HostelService.GetHostel(HostelId)!;
        MainFormTests.RunOnStaThread(() =>
        {
            using var form = new Form { Size = new Size(1200, 800) };
            form.Controls.Add(new HostelManagement.Forms.Views.EmailSettingsView());
            form.Show();
            Application.DoEvents();
            Assert.Contains(AllControls(form).OfType<TextBox>(), t => t.Text == EmailSettings.GmailHost);
            form.Close();
        });
    }

    private static IEnumerable<Control> AllControls(Control parent) =>
        parent.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(AllControls(c)));
}
