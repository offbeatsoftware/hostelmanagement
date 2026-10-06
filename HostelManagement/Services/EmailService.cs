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
/// Emails (client decisions, Phase 11): invoices go to the primary parent only, with the invoice PDF;
/// receipts are emailed when the admin clicks the button, with the receipt PDF; reminders go to the parents
/// of students with overdue invoices, one student at a time or all at once, with the overdue invoice PDFs.
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
        Parent parent = RequireParentEmail(data.Student, data.Parent);
        Invoice invoice = data.Invoice;

        var values = CommonValues(data.Student, parent, data.Hostel);
        values["InvoiceNumber"] = invoice.InvoiceNumber;
        values["Period"] = $"{invoice.BillingFrom:dd MMM yyyy} to {invoice.BillingTo:dd MMM yyyy}";
        values["Amount"] = PdfText.Rupees(invoice.TotalAmount);
        values["DueDate"] = Date(invoice.DueDate);
        values["Pending"] = PdfText.Rupees(invoice.PendingAmount);

        return Build(EmailType.Invoice, settings.Invoice, values, data.Student, parent, [invoice.InvoiceId],
            [InvoicePdfWriter.SaveToInvoicesFolder(data)]);
    }

    public static OutgoingEmail PrepareReceipt(int paymentId)
    {
        EmailSettings settings = GetConfiguredSettings();
        ReceiptPrintData data = PaymentService.GetReceiptData(paymentId);
        Parent parent = RequireParentEmail(data.Student, data.Parent);
        Payment payment = data.Payment;

        var values = CommonValues(data.Student, parent, data.Hostel);
        values["InvoiceNumber"] = data.Invoice.InvoiceNumber;
        values["ReceiptNumber"] = payment.ReceiptNumber;
        values["PaidAmount"] = PdfText.Rupees(payment.Amount);
        values["PaymentDate"] = Date(payment.PaymentDate);
        values["PaymentMethod"] = payment.PaymentMethod;
        values["Pending"] = PdfText.Rupees(data.Invoice.PendingAmount);

        return Build(EmailType.Receipt, settings.Receipt, values, data.Student, parent, [data.Invoice.InvoiceId],
            [ReceiptPdfWriter.SaveToReceiptsFolder(data)]);
    }

    /// <summary>A reminder about the student's overdue invoices, with their PDFs attached.</summary>
    public static OutgoingEmail PrepareReminder(StudentDue due)
    {
        EmailSettings settings = GetConfiguredSettings();
        List<Invoice> overdue = due.Invoices.Where(i => i.DaysOverdue(due.AsOf) > 0).ToList();
        if (overdue.Count == 0)
        {
            throw new ValidationException($"{due.StudentName} has no overdue invoices, so no reminder is needed.");
        }

        List<InvoicePrintData> invoices = overdue.Select(i => InvoiceService.GetPrintData(i.InvoiceId)).ToList();
        Student student = invoices[0].Student;
        Parent parent = RequireParentEmail(student, invoices[0].Parent);

        var values = CommonValues(student, parent, invoices[0].Hostel);
        values["InvoiceList"] = string.Join(Environment.NewLine, overdue.Select(i =>
            $"{i.InvoiceNumber} ({i.PeriodText}): {PdfText.Rupees(i.PendingAmount)} pending, due {Date(i.DueDate)}"));
        values["Pending"] = PdfText.Rupees(due.PendingAmount);
        values["Overdue"] = PdfText.Rupees(due.OverdueAmount);

        return Build(EmailType.DueReminder, settings.Reminder, values, student, parent, overdue.Select(i => i.InvoiceId).ToList(),
            invoices.Select(InvoicePdfWriter.SaveToInvoicesFolder).ToList());
    }

    /// <summary>
    /// Reminders for every student of the hostel with overdue invoices. Students that cannot be emailed
    /// (for example without a parent email) are returned as problems instead of stopping the others.
    /// </summary>
    public static (List<OutgoingEmail> Emails, List<string> Problems) PrepareReminders(int hostelId, DateTime asOf)
    {
        var emails = new List<OutgoingEmail>();
        var problems = new List<string>();
        foreach (StudentDue due in PendingDuesService.GetDues(hostelId, asOf).Where(d => d.IsOverdue))
        {
            try
            {
                emails.Add(PrepareReminder(due));
            }
            catch (ValidationException ex)
            {
                problems.Add($"{due.StudentName}: {ex.Message}");
            }
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

    /// <summary>Keeps one history row per invoice the email was about.</summary>
    public static void Log(OutgoingEmail email, string? error)
    {
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
            Body = $"This test email shows that {AppInfo.BusinessName} can send invoices, receipts and reminders from {settings.SenderEmail}.",
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

    private static Parent RequireParentEmail(Student student, Parent? parent) =>
        parent is { Email.Length: > 0 }
            ? parent
            : throw new ValidationException($"{student.StudentName} has no primary parent with an email address.");

    private static Dictionary<string, string> CommonValues(Student student, Parent parent, Hostel hostel)
    {
        AdminUser? admin = AdminUserRepository.GetFirst();
        return new Dictionary<string, string>
        {
            ["StudentName"] = student.StudentName,
            ["ParentName"] = parent.ParentName,
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
        Parent parent, List<int> invoiceIds, List<string> attachments) => new()
    {
        EmailType = type,
        StudentId = student.StudentId,
        StudentName = student.StudentName,
        InvoiceIds = invoiceIds,
        RecipientEmail = parent.Email,
        RecipientName = parent.ParentName,
        CopyToEmail = CopyTo(parent.Email),
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
