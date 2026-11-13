namespace HostelManagement.Models;

/// <summary>
/// Money paid back to a student (client decision, version 1.5): full or part of what was paid on a fee, any number of
/// times, also after the student has left. A refund lowers the fee by the same amount, so the pending amount does not
/// change. Each refund has its own number (SBH/RF/2026-2027/0001) and receipt.
/// </summary>
public sealed class Refund
{
    public int RefundId { get; set; }
    public string RefundNumber { get; set; } = string.Empty;
    public int StudentId { get; set; }
    public int InvoiceId { get; set; }
    public DateTime RefundDate { get; set; }
    public decimal Amount { get; set; }

    /// <summary>How the money was paid back (cash, UPI, bank transfer, cheque).</summary>
    public string RefundMethod { get; set; } = PaymentMethod.Cash;

    public string Reference { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }

    // Read only values filled in when refunds are listed.
    public string StudentName { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
}
