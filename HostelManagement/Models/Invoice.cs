namespace HostelManagement.Models;

/// <summary>Invoice status, calculated from the payments (never stored).</summary>
public static class InvoiceStatus
{
    public const string Unpaid = "Unpaid";
    public const string PartlyPaid = "Partly paid";
    public const string Paid = "Paid";
}

/// <summary>
/// A student's fee for one academic year (client decision, version 1.2): the room rent and the transport
/// amount agreed with the student for that year. The student pays it in any number of payments; paid and
/// pending amounts come from the payments.
/// </summary>
public sealed class Invoice
{
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public int StudentId { get; set; }
    public DateTime InvoiceDate { get; set; }

    /// <summary>The academic year, stored as the year it starts in (2026 for 2026-27).</summary>
    public int AcademicYear { get; set; }

    public decimal RoomRent { get; set; }

    /// <summary>Transport for the year; 0 when the student does not use transport.</summary>
    public decimal TransportAmount { get; set; }

    public string Remarks { get; set; } = string.Empty;

    // Read only values filled in when invoices are listed.
    public string StudentName { get; set; } = string.Empty;
    public decimal PaidAmount { get; set; }

    public decimal TotalAmount => RoomRent + TransportAmount;

    public decimal PendingAmount => TotalAmount - PaidAmount;

    public bool HasTransport => TransportAmount > 0;

    /// <summary>"2026-27".</summary>
    public string YearText => Models.AcademicYear.Label(AcademicYear);

    public string Status => PendingAmount <= 0 ? InvoiceStatus.Paid
        : PaidAmount <= 0 ? InvoiceStatus.Unpaid
        : InvoiceStatus.PartlyPaid;

    /// <summary>How the invoice is shown when choosing it for a payment.</summary>
    public string PickerText => $"{InvoiceNumber}   {StudentName}   (pending {Utilities.Money.Format(PendingAmount)})";
}
