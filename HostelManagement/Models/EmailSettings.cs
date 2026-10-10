namespace HostelManagement.Models;

/// <summary>Kinds of email, stored in EmailHistory.EmailType.</summary>
public static class EmailType
{
    public const string Invoice = "Invoice";
    public const string Receipt = "Receipt";
    public const string DueReminder = "DueReminder";
    public const string Absence = "Absence";

    public static string DisplayName(string type) => type switch
    {
        DueReminder => "Fee reminder",
        _ => type,
    };
}

/// <summary>Result of sending an email, stored in EmailHistory.Status.</summary>
public static class EmailStatus
{
    public const string Sent = "Sent";
    public const string Failed = "Failed";
}

/// <summary>Subject and message of one kind of email; {Fields} are replaced when the email is sent.</summary>
public sealed record EmailTemplate(string Subject, string Body);

/// <summary>
/// The Gmail account that sends invoices, receipts, fee reminders and attendance emails (client decision) and the
/// editable email texts. One account is used for all hostels.
/// </summary>
public sealed class EmailSettings
{
    public const string GmailHost = "smtp.gmail.com";
    public const int GmailPort = 587;

    /// <summary>The hostel's Gmail account (client decision); the app password is entered on the Email Settings screen.</summary>
    public const string DefaultSenderEmail = "shribalajihostelsuddhowala@gmail.com";

    public string SmtpHost { get; set; } = GmailHost;
    public int SmtpPort { get; set; } = GmailPort;
    public string SenderEmail { get; set; } = DefaultSenderEmail;
    public string SenderName { get; set; } = string.Empty;

    /// <summary>The Gmail app password (16 letters), never the normal Gmail password.</summary>
    public string AppPassword { get; set; } = string.Empty;

    public EmailTemplate Invoice { get; set; } = DefaultInvoice;
    public EmailTemplate Receipt { get; set; } = DefaultReceipt;
    public EmailTemplate Reminder { get; set; } = DefaultReminder;
    public EmailTemplate Absence { get; set; } = DefaultAbsence;

    public bool IsConfigured => SenderEmail.Length > 0 && AppPassword.Length > 0 && SmtpHost.Length > 0;

    public static EmailTemplate DefaultInvoice { get; } = new(
        "Invoice {InvoiceNumber} for {StudentName}, {HostelName}",
        """
        Dear {ParentName},

        Please find attached invoice {InvoiceNumber} for the hostel fee of {StudentName} for the academic year {AcademicYear}.

        Room rent: {RoomRent}
        Transport: {Transport}
        Total fee: {Amount}
        Paid so far: {PaidAmount}
        Pending: {Pending}

        Thank you,
        {HostelName}
        {HostelPhone}
        """);

    public static EmailTemplate DefaultReceipt { get; } = new(
        "Payment receipt {ReceiptNumber} for {StudentName}, {HostelName}",
        """
        Dear {ParentName},

        Thank you for your payment of {PaidAmount} on {PaymentDate} towards the hostel fee of {StudentName} ({AcademicYear}).
        The receipt is attached.

        Total fee: {TotalAmount}
        Balance pending: {Pending}

        Thank you,
        {HostelName}
        {HostelPhone}
        """);

    public static EmailTemplate DefaultAbsence { get; } = new(
        "{StudentName} was not present at {HostelName} on {AttendanceDate}",
        """
        Dear {ParentName},

        This is to inform you that {StudentName} (room {RoomNumber}) was not present in the hostel at the night attendance on {AttendanceDate}.

        If you were not aware of this, please contact the hostel.

        Thank you,
        {HostelName}
        {HostelPhone}
        """);

    public static EmailTemplate DefaultReminder { get; } = new(
        "Fee reminder for {StudentName}, {HostelName}",
        """
        Dear {ParentName},

        This is a reminder about the hostel fee of {StudentName}.

        {FeeDetails}

        Total fee: {TotalAmount}
        Paid so far: {PaidAmount}
        Pending amount: {Pending}

        Kindly pay the pending amount at the earliest. The invoice with all payments is attached.
        Please ignore this message if you have already paid.

        Thank you,
        {HostelName}
        {HostelPhone}
        """);
}

/// <summary>An email ready to send, with the details needed for the email history.</summary>
public sealed class OutgoingEmail
{
    public string EmailType { get; init; } = string.Empty;
    public int StudentId { get; init; }
    public string StudentName { get; init; } = string.Empty;

    /// <summary>The attendance record an absence email is about.</summary>
    public int? AttendanceId { get; init; }

    /// <summary>The invoices the email is about (one history row is kept per invoice).</summary>
    public List<int> InvoiceIds { get; init; } = [];

    public string RecipientEmail { get; init; } = string.Empty;
    public string RecipientName { get; init; } = string.Empty;

    /// <summary>The admin's email address, which receives a copy of every email to a parent (empty when not set).</summary>
    public string CopyToEmail { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public List<string> AttachmentPaths { get; init; } = [];
}

/// <summary>One row of the email history.</summary>
public sealed class EmailHistoryEntry
{
    public int EmailHistoryId { get; set; }
    public int StudentId { get; set; }
    public int? InvoiceId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string EmailType { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime SentDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;

    // Read only values filled in when the history is listed.
    public string StudentName { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public string TypeText => Models.EmailType.DisplayName(EmailType);
}
