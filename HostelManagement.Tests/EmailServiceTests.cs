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
    private Room? _room;

    public EmailServiceTests()
    {
        EmailService.Sender = _sender;
    }

    private static void SetUpGmail() =>
        EmailSettingsService.Save(new EmailSettings { SenderEmail = "hostel@gmail.com", SenderName = "Shri Balaji Hostel", AppPassword = "abcd efgh ijkl mnop" });

    /// <summary>A student in a room with a fee of 60000 (rent 50000, transport 10000) for this academic year.</summary>
    private int StudentInRoom(string name, bool fatherEmail = true, bool motherEmail = true)
    {
        if (_room is null)
        {
            HostelService.Save(new Hostel { HostelId = HostelId, HostelName = "Test Hostel", Phone = "0141 2222222" });
            _room = AddRoom(beds: 3);
        }
        string lower = name.ToLowerInvariant().Replace(' ', '.');
        int id = AddStudentWithParents(name,
            fatherEmail: fatherEmail ? $"{lower}.father@example.com" : "",
            motherEmail: motherEmail ? $"{lower}.mother@example.com" : "");
        CheckInWithFee(id, _room, 50_000m, 10_000m);
        return id;
    }

    private static Invoice Fee(int studentId) => InvoiceService.GetForYear(studentId, AcademicYear.Current)!;

    private StudentDue Due(int studentId) => PendingDuesService.GetDue(HostelId, studentId)!;

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
        settings.Reminder = new EmailTemplate("Fees due for {StudentName}", "Dear {ParentName}, please pay {Pending}.");

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
    public void InvalidSmtpServer_GivesAConnectionMessage()
    {
        EmailService.Sender = new SmtpEmailSender();
        // Nothing listens on port 1 of this computer, so the connection is refused at once.
        var settings = new EmailSettings { SmtpHost = "127.0.0.1", SmtpPort = 1, SenderEmail = "hostel@gmail.com", AppPassword = "x" };

        string? error = EmailService.SendTest(settings, "owner@example.com");

        Assert.Contains("Could not connect to 127.0.0.1", error);
    }

    [Fact]
    public void InvalidRecipientAddress_GivesAnAddressMessage()
    {
        EmailService.Sender = new SmtpEmailSender();
        var settings = new EmailSettings { SmtpHost = "127.0.0.1", SmtpPort = 1, SenderEmail = "hostel@gmail.com", AppPassword = "x" };
        var email = new OutgoingEmail { EmailType = EmailType.Invoice, RecipientEmail = "parent at example", Subject = "S", Body = "B" };

        Assert.Contains("not valid", EmailService.Send(settings, email));
    }

    [Fact]
    public void Fill_ReplacesKnownFieldsAndLeavesOthers() =>
        Assert.Equal("Dear Rakesh, {Unknown}",
            EmailSettingsService.Fill("Dear {ParentName}, {Unknown}", new Dictionary<string, string> { ["ParentName"] = "Rakesh" }));

    [Fact]
    public void PrepareInvoice_WithoutGmailAccount_AsksToSetItUp()
    {
        Invoice invoice = Fee(StudentInRoom("Aman"));

        Assert.Contains("Email Settings", Assert.Throws<ValidationException>(() => EmailService.PrepareInvoice(invoice.InvoiceId)).Message);
    }

    [Fact]
    public void InvoiceEmail_GoesToTheFatherWithThePdf_AndIsLogged()
    {
        SetUpGmail();
        Invoice invoice = Fee(StudentInRoom("Aman"));

        OutgoingEmail email = EmailService.PrepareInvoice(invoice.InvoiceId);
        Assert.Null(EmailService.SendAndLog(email));

        OutgoingEmail sent = _sender.Sent.Single();
        Assert.Equal("aman.father@example.com", sent.RecipientEmail);
        Assert.Equal($"Invoice {invoice.InvoiceNumber} for Aman, Test Hostel", sent.Subject);
        Assert.Contains("Dear Rakesh Aman,", sent.Body);
        Assert.Contains($"academic year {AcademicYear.Label(AcademicYear.Current)}", sent.Body);
        Assert.Contains("Room rent: Rs. 50,000.00", sent.Body);
        Assert.Contains("Transport: Rs. 10,000.00", sent.Body);
        Assert.Contains("Total fee: Rs. 60,000.00", sent.Body);
        Assert.Contains("Pending: Rs. 60,000.00", sent.Body);
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
        Invoice invoice = Fee(StudentInRoom("Aman"));
        EmailSettings settings = EmailSettingsService.Get();
        settings.Invoice = new EmailTemplate("Invoice {InvoiceNumber}", "Questions? Call {AdminPhone} or write to {AdminEmail}.");
        EmailSettingsService.Save(settings);

        OutgoingEmail email = EmailService.PrepareInvoice(invoice.InvoiceId);

        Assert.Equal("owner@gmail.com", email.CopyToEmail);
        Assert.Equal("Questions? Call 98290 12345 or write to owner@gmail.com.", email.Body);
        Payment payment = Pay(invoice.InvoiceId, 100m);
        Assert.Equal("owner@gmail.com", EmailService.PrepareReceipt(payment.PaymentId).CopyToEmail);
    }

    [Fact]
    public void NoAdminEmail_NoCopy()
    {
        SetUpGmail();
        Invoice invoice = Fee(StudentInRoom("Aman"));

        Assert.Equal("", EmailService.PrepareInvoice(invoice.InvoiceId).CopyToEmail);
    }

    [Fact]
    public void FailedEmail_IsLoggedWithAMessageTheAdminCanActOn()
    {
        SetUpGmail();
        Invoice invoice = Fee(StudentInRoom("Aman"));
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
        Invoice invoice = Fee(StudentInRoom("Aman"));
        Payment payment = PaymentService.Record(new Payment
            { InvoiceId = invoice.InvoiceId, PaymentDate = Today, Amount = 15_000m, PaymentMethod = PaymentMethod.Upi, Reference = "UPI1" });

        OutgoingEmail email = EmailService.PrepareReceipt(payment.PaymentId);

        Assert.Equal(EmailType.Receipt, email.EmailType);
        Assert.Contains(payment.ReceiptNumber, email.Subject);
        Assert.Contains("Rs. 15,000.00", email.Body);
        Assert.Equal("aman.father@example.com", email.RecipientEmail);
        Assert.Contains("Total fee: Rs. 60,000.00", email.Body);
        Assert.Contains("Balance pending: Rs. 45,000.00", email.Body);
        Assert.StartsWith("Receipt_", Path.GetFileName(email.AttachmentPaths.Single()));
    }

    [Fact]
    public void FeeReminder_ShowsTotalPaidAndPending_WithTheInvoicePdf_AndGoesToTheFather()
    {
        SetUpGmail();
        int aman = StudentInRoom("Aman");
        Payment first = Pay(Fee(aman).InvoiceId, 10_000m);
        Pay(Fee(aman).InvoiceId, 3_000m);

        OutgoingEmail reminder = EmailService.PrepareReminder(Due(aman));

        Assert.Equal(EmailType.DueReminder, reminder.EmailType);
        Assert.Equal("aman.father@example.com", reminder.RecipientEmail);
        Assert.Equal("Fee reminder for Aman, Test Hostel", reminder.Subject);
        Assert.Contains("Total fee: Rs. 60,000.00", reminder.Body);
        Assert.Contains("Paid so far: Rs. 13,000.00", reminder.Body);
        Assert.Contains("Pending amount: Rs. 47,000.00", reminder.Body);
        Assert.Contains($"{Fee(aman).InvoiceNumber}): room rent Rs. 50,000.00 + transport Rs. 10,000.00 = Rs. 60,000.00", reminder.Body);
        Assert.Equal([Fee(aman).InvoiceId], reminder.InvoiceIds);
        Assert.StartsWith("Invoice_", Path.GetFileName(reminder.AttachmentPaths.Single()));
        Assert.DoesNotContain("{", reminder.Body);
        Assert.NotNull(first);
    }

    [Fact]
    public void FeeReminders_GoToTheChosenStudents_MotherWhenTheFatherHasNoEmail_AndProblemsAreListed()
    {
        SetUpGmail();
        int aman = StudentInRoom("Aman");
        int ravi = StudentInRoom("Ravi", fatherEmail: false);
        int noEmail = StudentInRoom("No Email", fatherEmail: false, motherEmail: false);
        StudentInRoom("Not Chosen");

        (List<OutgoingEmail> emails, List<string> problems) = EmailService.PrepareReminders([Due(aman), Due(ravi), Due(noEmail)]);

        Assert.Equal(["aman.father@example.com", "ravi.mother@example.com"], emails.Select(e => e.RecipientEmail));
        Assert.Equal("Sunita Ravi", emails[1].RecipientName);
        Assert.Contains("Dear Sunita Ravi,", emails[1].Body);
        Assert.Equal("No Email: Neither the father nor the mother of No Email has an email address. Add one on the Students screen.",
            Assert.Single(problems));

        Assert.Null(Due(aman).LastReminderDate);
        EmailService.SendAndLog(emails[0]);
        Assert.Equal(Today, Due(aman).LastReminderDate?.Date);
    }

    [Fact]
    public void FeeReminder_TextChangedOnTheScreen_IsUsedForThatSending_AndCanBeSavedAsDefault()
    {
        SetUpGmail();
        int aman = StudentInRoom("Aman");
        var text = new EmailTemplate("Please pay {Pending}", "Dear {ParentName}, {StudentName} owes {Pending} of {TotalAmount}.");

        OutgoingEmail email = EmailService.PrepareReminder(Due(aman), text);
        (string to, string subject, string body) = EmailService.PreviewReminder(Due(aman), text);

        Assert.Equal("Please pay Rs. 60,000.00", email.Subject);
        Assert.Equal("Dear Rakesh Aman, Aman owes Rs. 60,000.00 of Rs. 60,000.00.", email.Body);
        Assert.Equal((email.Subject, email.Body), (subject, body));
        Assert.Equal("Father: Rakesh Aman <aman.father@example.com>", to);
        Assert.Equal(EmailSettings.DefaultReminder, EmailSettingsService.Get().Reminder);

        EmailSettingsService.SaveReminderText(text);
        Assert.Equal(text, EmailSettingsService.Get().Reminder);

        var unknown = new EmailTemplate("Overdue {Overdue}", "Body");
        Assert.Contains("{Overdue}", Assert.Throws<ValidationException>(() => EmailService.ValidateReminderText(unknown)).Message);
        Assert.Throws<ValidationException>(() => EmailSettingsService.SaveReminderText(unknown));
    }

    [Fact]
    public void FeeReminder_StudentWithNothingPending_IsRejected()
    {
        SetUpGmail();
        int aman = StudentInRoom("Aman");
        var paidUp = new StudentDue { StudentId = aman, StudentName = "Aman", Invoices = [] };

        Assert.Contains("no pending fee", Assert.Throws<ValidationException>(() => EmailService.PrepareReminder(paidUp)).Message);
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
