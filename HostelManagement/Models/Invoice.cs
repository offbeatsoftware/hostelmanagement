namespace HostelManagement.Models;

/// <summary>Invoice status, calculated from the payments (never stored).</summary>
public static class InvoiceStatus
{
    public const string Unpaid = "Unpaid";
    public const string PartlyPaid = "Partly paid";
    public const string Paid = "Paid";
}

/// <summary>
/// An invoice for one student and one billing period. The total is the sum of the items; paid and
/// pending amounts come from the payments.
/// </summary>
public sealed class Invoice
{
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public int StudentId { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime BillingFrom { get; set; }
    public DateTime BillingTo { get; set; }
    public decimal TotalAmount { get; set; }

    // Read only values filled in when invoices are listed.
    public string StudentName { get; set; } = string.Empty;
    public decimal PaidAmount { get; set; }

    public decimal PendingAmount => TotalAmount - PaidAmount;

    public string PeriodText => $"{BillingFrom:MMM yyyy} to {BillingTo:MMM yyyy}";

    public string Status => PaidAmount <= 0 ? InvoiceStatus.Unpaid
        : PendingAmount <= 0 ? InvoiceStatus.Paid
        : InvoiceStatus.PartlyPaid;

    /// <summary>How the invoice is shown when choosing it for a payment.</summary>
    public string PickerText => $"{InvoiceNumber}   {StudentName}   (pending {Utilities.Money.Format(PendingAmount)})";

    public List<InvoiceItem> Items { get; set; } = [];
}

/// <summary>One line of an invoice: rent, an extra service, or a service included in the rent (amount 0).</summary>
public sealed class InvoiceItem
{
    public int InvoiceItemId { get; set; }
    public int InvoiceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
}
