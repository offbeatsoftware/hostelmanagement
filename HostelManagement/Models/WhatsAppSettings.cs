namespace HostelManagement.Models;

/// <summary>
/// The hostel's WhatsApp Business account on Meta's WhatsApp Cloud API (client decision, version 1.4): fee reminders
/// and attendance messages are sent as Meta-approved templates from the hostel's number.
/// </summary>
public sealed class WhatsAppSettings
{
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

    public bool IsConfigured => PhoneNumberId.Length > 0 && AccessToken.Length > 0;
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
}
