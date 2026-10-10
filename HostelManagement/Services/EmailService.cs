using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Reports;
using HostelManagement.Utilities;

namespace HostelManagement.Services;

/// <summary>Sends an email. Replaced in the automated tests so that no real email is sent.</summary>
public interface IEmailSender
{
    void Send(EmailSettings settings, OutgoingEmail email);
}

/// <summary>Sends through Gmail (smtp.gmail.com, port 587, encrypted) with the account's app password.</summary>
public sealed class SmtpEmailSender : IEmailSender
{
    public void Send(EmailSettings settings, OutgoingEmail email)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(settings.SenderEmail, settings.SenderName.Length > 0 ? settings.SenderName : AppInfo.BusinessName),
            Subject = email.Subject,
            Body = email.Body,
            IsBodyHtml = false,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
        };
        message.To.Add(new MailAddress(email.RecipientEmail, email.RecipientName));
        if (email.CopyToEmail.Length > 0)
        {
            message.CC.Add(new MailAddress(email.CopyToEmail));
        }
        foreach (string path in email.AttachmentPaths)
        {
            message.Attachments.Add(new Attachment(path, "application/pdf"));
        }

        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
        {
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(settings.SenderEmail, settings.AppPassword),
            Timeout = 30_000,
        };
        client.Send(message);
    }
}

/// <summary>
/// Emails (client decisions): invoices, receipts and fee reminders go to the father, or to the mother when the
/// father has no email; attendance emails go to the mother, or to the father when the mother has no email.
/// The admin gets a copy of every email. Invoices and receipts are emailed when the admin clicks the button, with
/// the PDF; fee reminders are sent whenever the admin chooses (never automatically), with the invoice PDFs.
/// Emails are prepared (database and PDFs) and logged on the calling thread; only <see cref="Send"/> talks
/// to Gmail, so the screens can send in the background without touching the database there.
/// </summary>
public static class EmailService
{
    public static IEmailSender Sender { get; set; } = new SmtpEmailSender();

    public static OutgoingEmail PrepareInvoice(int invoiceId)
    {
        EmailSettings settings = GetConfiguredSettings();
        InvoicePrintData data = InvoiceService.GetPrintData(invoiceId);
        EmailContact contact = RequireFeeContact(data.Student);
        Invoice invoice = data.Invoice;

        var values = CommonValues(data.Student, contact, data.Hostel);
        values["InvoiceNumber"] = invoice.InvoiceNumber;
        values["AcademicYear"] = invoice.YearText;
        values["RoomRent"] = PdfText.Rupees(invoice.RoomRent);
        values["Transport"] = PdfText.Rupees(invoice.TransportAmount);
        values["Amount"] = PdfText.Rupees(invoice.TotalAmount);
        values["PaidAmount"] = PdfText.Rupees(invoice.PaidAmount);
        values["Pending"] = PdfText.Rupees(invoice.PendingAmount);

        return Build(EmailType.Invoice, settings.Invoice, values, data.Student, contact, [invoice.InvoiceId],
            [InvoicePdfWriter.SaveToInvoicesFolder(data)]);
    }

    public static OutgoingEmail PrepareReceipt(int paymentId)
    {
        EmailSettings settings = GetConfiguredSettings();
        ReceiptPrintData data = PaymentService.GetReceiptData(paymentId);
        EmailContact contact = RequireFeeContact(data.Student);
        Payment payment = data.Payment;

        var values = CommonValues(data.Student, contact, data.Hostel);
        values["InvoiceNumber"] = data.Invoice.InvoiceNumber;
        values["AcademicYear"] = data.Invoice.YearText;
        values["ReceiptNumber"] = payment.ReceiptNumber;
        values["PaidAmount"] = PdfText.Rupees(payment.Amount);
        values["PaymentDate"] = Date(payment.PaymentDate);
        values["PaymentMethod"] = payment.PaymentMethod;
        values["TotalAmount"] = PdfText.Rupees(data.Invoice.TotalAmount);
        values["Pending"] = PdfText.Rupees(data.Invoice.PendingAmount);

        return Build(EmailType.Receipt, settings.Receipt, values, data.Student, contact, [data.Invoice.InvoiceId],
            [ReceiptPdfWriter.SaveToReceiptsFolder(data)]);
    }

    /// <summary>
    /// A fee reminder with the student's total, paid and pending amounts and the invoice PDFs. The admin may
    /// change the text for this sending (<paramref name="template"/>); otherwise the saved text is used.
    /// </summary>
    public static OutgoingEmail PrepareReminder(StudentDue due, EmailTemplate? template = null)
    {
        EmailSettings settings = GetConfiguredSettings();
        (Student student, EmailContact contact, Dictionary<string, string> values, List<InvoicePrintData> invoices) =
            ReminderValues(due);

        return Build(EmailType.DueReminder, template ?? settings.Reminder, values, student, contact,
            due.Invoices.Select(i => i.InvoiceId).ToList(),
            invoices.Select(InvoicePdfWriter.SaveToInvoicesFolder).ToList());
    }

    /// <summary>The fee reminder as it will be sent to one student's parent, without creating the PDFs (for the preview).</summary>
    public static (string To, string Subject, string Body) PreviewReminder(StudentDue due, EmailTemplate template)
    {
        (Student student, EmailContact contact, Dictionary<string, string> values, _) = ReminderValues(due);
        OutgoingEmail email = Build(EmailType.DueReminder, template, values, student, contact, [], []);
        string copy = email.CopyToEmail.Length > 0 ? $"   (copy to {email.CopyToEmail})" : "";
        return ($"{contact.Relation}: {contact.Name} <{contact.Email}>{copy}", email.Subject, email.Body);
    }

    private static (Student, EmailContact, Dictionary<string, string>, List<InvoicePrintData>) ReminderValues(StudentDue due)
    {
        if (due.PendingAmount <= 0)
        {
            throw new ValidationException($"{due.StudentName} has no pending fee, so no reminder is needed.");
        }

        List<InvoicePrintData> invoices = due.Invoices.Select(i => InvoiceService.GetPrintData(i.InvoiceId)).ToList();
        Student student = invoices[0].Student;
        EmailContact contact = RequireFeeContact(student);

        Dictionary<string, string> values = CommonValues(student, contact, invoices[0].Hostel);
        values["FeeDetails"] = string.Join(Environment.NewLine, due.Invoices.Select(FeeLine));
        values["TotalAmount"] = PdfText.Rupees(due.TotalAmount);
        values["PaidAmount"] = PdfText.Rupees(due.PaidAmount);
        values["Pending"] = PdfText.Rupees(due.PendingAmount);
        return (student, contact, values, invoices);
    }

    /// <summary>
    /// Fee reminders for the chosen students. Students that cannot be emailed (for example without a father or
    /// mother email) are returned as problems instead of stopping the others.
    /// </summary>
    public static (List<OutgoingEmail> Emails, List<string> Problems) PrepareReminders(IEnumerable<StudentDue> dues,
        EmailTemplate? template = null)
    {
        var emails = new List<OutgoingEmail>();
        var problems = new List<string>();
        foreach (StudentDue due in dues)
        {
            try
            {
                emails.Add(PrepareReminder(due, template));
            }
            catch (ValidationException ex)
            {
                problems.Add($"{due.StudentName}: {ex.Message}");
            }
        }
        return (emails, problems);
    }

    /// <summary>Checks a reminder text the admin changed before sending: subject and message, and only known fields.</summary>
    public static void ValidateReminderText(EmailTemplate template) =>
        EmailSettingsService.ValidateTemplate(EmailType.DueReminder, "fee reminder", template);

    /// <summary>
    /// Absence emails for the absent students of a saved attendance date (by default only those whose parent was
    /// not emailed yet). Students that cannot be emailed are returned as problems instead of stopping the others.
    /// </summary>
    public static (List<OutgoingEmail> Emails, List<string> Problems) PrepareAbsences(int hostelId, DateTime date, bool includeAlreadyEmailed = false)
    {
        EmailSettings settings = GetConfiguredSettings();
        AttendanceSheet sheet = AttendanceService.GetSheet(hostelId, date);
        Hostel hostel = HostelService.GetHostel(hostelId) ?? throw new ValidationException("Please select the hostel.");
        var emails = new List<OutgoingEmail>();
        var problems = new List<string>();

        foreach (AttendanceEntry entry in sheet.Entries.Where(e => e.IsSaved && !e.IsPresent))
        {
            if (entry.ParentEmailedDate is not null && !includeAlreadyEmailed)
            {
                continue;
            }
            Student? student = StudentService.GetStudent(entry.StudentId);
            if (student?.AttendanceContact is not EmailContact contact)
            {
                problems.Add($"{entry.StudentName}: neither the mother nor the father has an email address.");
                continue;
            }

            var values = CommonValues(student, contact, hostel);
            values["AttendanceDate"] = Date(sheet.Date);
            values["RoomNumber"] = entry.RoomNumber;
            values["Remarks"] = entry.Remarks;
            OutgoingEmail email = Build(EmailType.Absence, settings.Absence, values, student, contact, [], []);
            emails.Add(new OutgoingEmail
            {
                EmailType = email.EmailType,
                StudentId = email.StudentId,
                StudentName = email.StudentName,
                AttendanceId = entry.AttendanceId,
                RecipientEmail = email.RecipientEmail,
                RecipientName = email.RecipientName,
                CopyToEmail = email.CopyToEmail,
                Subject = email.Subject,
                Body = email.Body,
            });
        }
        return (emails, problems);
    }

    /// <summary>Sends one email through Gmail. Returns null when sent, otherwise a message the admin can act on.</summary>
    public static string? Send(EmailSettings settings, OutgoingEmail email)
    {
        try
        {
            Sender.Send(settings, email);
            return null;
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Sending {email.EmailType} email to {email.RecipientEmail} failed.", ex);
            return FriendlyError(settings, ex);
        }
    }

    /// <summary>Keeps one history row per invoice the email was about; marks an absence as emailed.</summary>
    public static void Log(OutgoingEmail email, string? error)
    {
        if (error is null && email.AttendanceId is int attendanceId)
        {
            AttendanceRepository.SetParentEmailed(attendanceId, DateTime.Now);
        }

        IEnumerable<int?> invoiceIds = email.InvoiceIds.Count > 0 ? email.InvoiceIds.Select(id => (int?)id) : [null];
        foreach (int? invoiceId in invoiceIds)
        {
            EmailHistoryRepository.Insert(new EmailHistoryEntry
            {
                StudentId = email.StudentId,
                InvoiceId = invoiceId,
                RecipientEmail = email.RecipientEmail,
                EmailType = email.EmailType,
                Subject = email.Subject,
                SentDate = DateTime.Now,
                Status = error is null ? EmailStatus.Sent : EmailStatus.Failed,
                ErrorMessage = error ?? string.Empty,
            });
        }
    }

    /// <summary>Sends and logs one email on the calling thread. Returns null when sent, otherwise the problem.</summary>
    public static string? SendAndLog(OutgoingEmail email)
    {
        string? error = Send(GetConfiguredSettings(), email);
        Log(email, error);
        return error;
    }

    /// <summary>Sends a test email (not logged) to the given address, or to the sender's own address.</summary>
    public static string? SendTest(EmailSettings settings, string? toEmail = null)
    {
        string recipient = Validators.Clean(toEmail);
        if (recipient.Length > 0 && !Validators.IsValidEmailOrEmpty(recipient))
        {
            return "Please enter a valid email address to send the test to.";
        }
        return Send(settings, new OutgoingEmail
        {
            EmailType = "Test",
            RecipientEmail = recipient.Length > 0 ? recipient : settings.SenderEmail,
            RecipientName = recipient.Length > 0 ? string.Empty : settings.SenderName,
            Subject = $"Test email from the {AppInfo.ProductName}",
            Body = $"This test email shows that {AppInfo.BusinessName} can send invoices, receipts, fee reminders and attendance emails from {settings.SenderEmail}.",
        });
    }

    public static List<EmailHistoryEntry> GetHistory(int maxRows = 500) => EmailHistoryRepository.GetRecent(maxRows);

    public static EmailSettings GetConfiguredSettings()
    {
        EmailSettings settings = EmailSettingsService.Get();
        if (!settings.IsConfigured)
        {
            throw new ValidationException(
                "The Gmail account is not set up yet (or its app password must be entered again on this computer). " +
                "Open Email Settings first.");
        }
        return settings;
    }

    private static EmailContact RequireFeeContact(Student student) =>
        student.FeeContact
            ?? throw new ValidationException(
                $"Neither the father nor the mother of {student.StudentName} has an email address. Add one on the Students screen.");

    /// <summary>"2026-27 (invoice SBH/2026-27/0001): total Rs. 62,000, paid Rs. 15,000, pending Rs. 47,000".</summary>
    private static string FeeLine(Invoice invoice)
    {
        string parts = invoice.HasTransport
            ? $"room rent {PdfText.Rupees(invoice.RoomRent)} + transport {PdfText.Rupees(invoice.TransportAmount)} = {PdfText.Rupees(invoice.TotalAmount)}"
            : $"room rent {PdfText.Rupees(invoice.TotalAmount)}";
        return $"{invoice.YearText} (invoice {invoice.InvoiceNumber}): {parts}, paid {PdfText.Rupees(invoice.PaidAmount)}, " +
               $"pending {PdfText.Rupees(invoice.PendingAmount)}";
    }

    private static Dictionary<string, string> CommonValues(Student student, EmailContact contact, Hostel hostel)
    {
        AdminUser? admin = AdminUserRepository.GetFirst();
        return new Dictionary<string, string>
        {
            ["StudentName"] = student.StudentName,
            ["ParentName"] = contact.Name,
            ["HostelName"] = hostel.HostelName,
            ["HostelPhone"] = hostel.Phone,
            ["AdminEmail"] = admin?.Email ?? string.Empty,
            ["AdminPhone"] = admin?.Phone ?? string.Empty,
        };
    }

    /// <summary>The admin gets a copy of every email to a parent, unless the parent's address is the admin's own.</summary>
    private static string CopyTo(string recipient)
    {
        string admin = AuthService.GetAdminEmail();
        return admin.Equals(recipient, StringComparison.OrdinalIgnoreCase) ? string.Empty : admin;
    }

    private static OutgoingEmail Build(string type, EmailTemplate template, Dictionary<string, string> values, Student student,
        EmailContact contact, List<int> invoiceIds, List<string> attachments) => new()
    {
        EmailType = type,
        StudentId = student.StudentId,
        StudentName = student.StudentName,
        InvoiceIds = invoiceIds,
        RecipientEmail = contact.Email,
        RecipientName = contact.Name,
        CopyToEmail = CopyTo(contact.Email),
        Subject = EmailSettingsService.Fill(template.Subject, values).ReplaceLineEndings(" ").Trim(),
        Body = EmailSettingsService.Fill(template.Body, values).Trim(),
        AttachmentPaths = attachments,
    };

    private static string Date(DateTime date) => date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    private static string FriendlyError(EmailSettings settings, Exception ex)
    {
        string details = ex.ToString();
        if (details.Contains("5.7.0", StringComparison.Ordinal) || details.Contains("5.7.8", StringComparison.Ordinal) ||
            details.Contains("Authentication", StringComparison.OrdinalIgnoreCase))
        {
            return "Gmail did not accept the sender address and app password. Check them in Email Settings " +
                   "(use a Gmail app password, not the normal password).";
        }
        if (ex is SmtpException { InnerException: System.Net.Sockets.SocketException or IOException } ||
            details.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("Failure sending mail", StringComparison.OrdinalIgnoreCase))
        {
            return $"Could not connect to {settings.SmtpHost}. Check the internet connection and try again.";
        }
        if (ex is FormatException)
        {
            return "An email address is not valid. Check the sender and the parent's email address.";
        }
        return "The email could not be sent. Technical details were written to the log file.";
    }
}
