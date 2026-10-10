namespace HostelManagement.Models;

/// <summary>What happened to a payment: corrected or deleted by the admin.</summary>
public static class PaymentChangeType
{
    public const string Edited = "Edited";
    public const string Deleted = "Deleted";
}

/// <summary>
/// One correction of a payment (client decision): every edit and delete is kept with what changed, when and why,
/// so wrong entries can be corrected and the corrections can still be traced.
/// </summary>
public sealed class PaymentChange
{
    public int PaymentChangeId { get; set; }
    public int PaymentId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public int StudentId { get; set; }
    public string ChangeType { get; set; } = PaymentChangeType.Edited;
    public DateTime ChangedDate { get; set; }

    /// <summary>For example "Amount Rs. 10,000.00 to Rs. 12,000.00; Paid by Cash to UPI".</summary>
    public string Details { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    // Read only, filled in when changes are listed.
    public string StudentName { get; set; } = string.Empty;
}
