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
/// pending amounts come from the payments. Since version 1.5 (client decisions) part of the fee can be waived at
/// check-out, and a refund pays money back and lowers the fee by the same amount, so the pending amount does not
/// change: fee due = agreed fee - waived - refunded, paid = payments - refunds, pending = due - paid.
/// </summary>
public sealed class Invoice
{
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public int StudentId { get; set; }
    public DateTime InvoiceDate { get; set; }

    /// <summary>The academic year, stored as the year it starts in (2026 for 2026-2027).</summary>
    public int AcademicYear { get; set; }

    public decimal RoomRent { get; set; }

    /// <summary>Transport for the year; 0 when the student does not use transport.</summary>
    public decimal TransportAmount { get; set; }

    public string Remarks { get; set; } = string.Empty;

    /// <summary>The part of the fee the admin let go (at check-out); 0 when none.</summary>
    public decimal WaivedAmount { get; set; }

    // Read only values filled in when invoices are listed.
    public string StudentName { get; set; } = string.Empty;

    /// <summary>Money received less money refunded.</summary>
    public decimal PaidAmount { get; set; }

    /// <summary>Money paid back to the student (it also lowers the fee due).</summary>
    public decimal RefundedAmount { get; set; }

    /// <summary>The fee agreed for the year: room rent plus transport.</summary>
    public decimal FeeAmount => RoomRent + TransportAmount;

    /// <summary>All payments received, before refunds.</summary>
    public decimal ReceivedAmount => PaidAmount + RefundedAmount;

    /// <summary>The fee due: the agreed fee less what was waived and refunded.</summary>
    public decimal TotalAmount => FeeAmount - WaivedAmount - RefundedAmount;

    public bool HasAdjustments => WaivedAmount != 0 || RefundedAmount != 0;

    public decimal PendingAmount => TotalAmount - PaidAmount;

    public bool HasTransport => TransportAmount > 0;

    /// <summary>"2026-2027".</summary>
    public string YearText => Models.AcademicYear.Label(AcademicYear);

    public string Status => PendingAmount <= 0 ? InvoiceStatus.Paid
        : PaidAmount <= 0 ? InvoiceStatus.Unpaid
        : InvoiceStatus.PartlyPaid;

    /// <summary>How the invoice is shown when choosing it for a payment.</summary>
    public string PickerText => $"{InvoiceNumber}   {StudentName}   (pending {Utilities.Money.Format(PendingAmount)})";
}
