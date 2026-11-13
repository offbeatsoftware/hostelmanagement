namespace HostelManagement.Models;

/// <summary>How WhatsApp messages are sent.</summary>
public static class WhatsAppMode
{
    /// <summary>The program opens each parent's chat in WhatsApp on this PC with the message typed; the admin presses Send.</summary>
    public const string Manual = "Manual";

    /// <summary>Sent automatically as Meta-approved templates through Meta's WhatsApp Cloud API.</summary>
    public const string Api = "Api";
}

/// <summary>Where the manual mode opens a parent's chat.</summary>
public static class WhatsAppOpenIn
{
    /// <summary>The WhatsApp (Business) app installed on this PC.</summary>
    public const string DesktopApp = "DesktopApp";

    /// <summary>WhatsApp Web (web.whatsapp.com) in the browser.</summary>
    public const string Web = "Web";
}

/// <summary>
/// WhatsApp fee reminders and attendance messages (client decisions, version 1.4). Manual mode (the default) uses the
/// hostel's normal WhatsApp on this PC: each message is typed into the parent's chat and the admin presses Send; no
/// Meta account is needed. API mode sends Meta-approved templates from a WhatsApp Business Platform account.
/// </summary>
public sealed class WhatsAppSettings
{
    public const string DefaultFeeText =
        "Dear {ParentName},\n" +
        "This is a fee reminder from {HostelName} for {StudentName} for the academic year {AcademicYear}.\n" +
        "Total fee: ₹{TotalAmount}\n" +
        "Paid: ₹{PaidAmount}\n" +
        "Pending: ₹{Pending}\n\n" +
        "To pay the fee:\n" +
        "Account name: SHRI BALAJI HOSTEL\n" +
        "Account number: 50200116556437\n" +
        "IFSC: HDFC0008528 (HDFC Bank, E C Road)\n" +
        "Account type: Current\n" +
        "Or pay by scanning our UPI QR code with any UPI app (GPay, PhonePe, Paytm, BHIM).\n\n" +
        "After paying, please share the payment receipt or UTR number on {HostelPhone}. Thank you.";

    public const string DefaultAttendanceText =
        "Dear {ParentName},\n" +
        "This is to inform you that {StudentName} was absent from {HostelName} on {AttendanceDate}.\n" +
        "For any question, please call {HostelPhone}. Thank you.";

    /// <summary>The {Fields} each manual message may use.</summary>
    public static IReadOnlyList<string> FeeFields { get; } =
        ["ParentName", "StudentName", "HostelName", "HostelPhone", "AcademicYear", "TotalAmount", "PaidAmount", "Pending"];

    public static IReadOnlyList<string> AttendanceFields { get; } =
        ["ParentName", "StudentName", "HostelName", "HostelPhone", "AttendanceDate"];

    public string Mode { get; set; } = WhatsAppMode.Manual;
    public string OpenIn { get; set; } = WhatsAppOpenIn.DesktopApp;
    public string FeeText { get; set; } = DefaultFeeText;
    public string AttendanceText { get; set; } = DefaultAttendanceText;

    public const string DefaultFeeTemplate = "fee_reminder";
    public const string DefaultAttendanceTemplate = "attendance_alert";
    public const string DefaultLanguage = "en";

    /// <summary>The Phone Number ID of the hostel's number (Meta App Dashboard > WhatsApp > API Setup).</summary>
    public string PhoneNumberId { get; set; } = string.Empty;

    /// <summary>The WhatsApp Business Account ID ("Messaging account ID").</summary>
    public string BusinessAccountId { get; set; } = string.Empty;

    /// <summary>The permanent access token of the system user; stored encrypted for this Windows user.</summary>
    public string AccessToken { get; set; } = string.Empty;

    public string FeeTemplate { get; set; } = DefaultFeeTemplate;
    public string AttendanceTemplate { get; set; } = DefaultAttendanceTemplate;

    /// <summary>The templates' language code in WhatsApp Manager ("en" for English).</summary>
    public string Language { get; set; } = DefaultLanguage;

    /// <summary>
    /// The payment QR code shown as the fee reminder's header image, relative to the data folder (empty when the
    /// fee reminder template has no image header).
    /// </summary>
    public string PaymentQrPath { get; set; } = string.Empty;

    public bool IsManual => Mode != WhatsAppMode.Api;

    /// <summary>True when messages can be sent: always in manual mode; with the Phone Number ID and token in API mode.</summary>
    public bool IsConfigured => IsManual || (PhoneNumberId.Length > 0 && AccessToken.Length > 0);
}

/// <summary>
/// One WhatsApp template message to a parent, prepared on the screen's thread and sent in the background.
/// <see cref="Parameters"/> fill the template's {{1}}, {{2}}... in order; <see cref="Preview"/> is the text as the
/// parent will read it (with the agreed template wording).
/// </summary>
public sealed class WhatsAppMessage
{
    public string MessageType { get; init; } = string.Empty;
    public int StudentId { get; init; }
    public string StudentName { get; init; } = string.Empty;
    public List<int> InvoiceIds { get; init; } = [];

    public string RecipientName { get; init; } = string.Empty;
    public string Relation { get; init; } = string.Empty;

    /// <summary>The parent's number with the country code and no "+" or spaces, for example 919876543210.</summary>
    public string Mobile { get; init; } = string.Empty;

    public string TemplateName { get; init; } = string.Empty;
    public IReadOnlyList<string> Parameters { get; init; } = [];

    /// <summary>True when the payment QR image is sent as the template's header.</summary>
    public bool WithPaymentQr { get; init; }

    /// <summary>Kept in the history to recognise a message already sent (for example the attendance date).</summary>
    public string HistoryText { get; init; } = string.Empty;

    public string Preview { get; init; } = string.Empty;

    /// <summary>The full text typed into the parent's chat in manual mode (same as <see cref="Preview"/>).</summary>
    public string Text => Preview;
}
