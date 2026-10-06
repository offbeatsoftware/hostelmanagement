namespace HostelManagement.Models;

/// <summary>
/// How a payment was made (client decision, Phase 9). Every method except cash needs a reference
/// (UPI transaction id, bank reference or cheque number).
/// </summary>
public static class PaymentMethod
{
    public const string Cash = "Cash";
    public const string Upi = "UPI";
    public const string BankTransfer = "Bank transfer";
    public const string Cheque = "Cheque";

    public static IReadOnlyList<string> All { get; } = [Cash, Upi, BankTransfer, Cheque];

    public static bool RequiresReference(string method) => method != Cash;

    /// <summary>What the reference is called for the method, for example "Cheque number".</summary>
    public static string ReferenceName(string method) => method switch
    {
        Upi => "UPI transaction id",
        BankTransfer => "Bank reference",
        Cheque => "Cheque number",
        _ => "Reference",
    };
}

/// <summary>A payment against one invoice. Each payment has its own receipt number.</summary>
public sealed class Payment
{
    public int PaymentId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public int StudentId { get; set; }
    public int InvoiceId { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = Models.PaymentMethod.Cash;
    public string Reference { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }

    // Read only values filled in when payments are listed.
    public string StudentName { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
}
