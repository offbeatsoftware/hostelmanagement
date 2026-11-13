using System.Globalization;
using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Services;

/// <summary>Everything printed on a refund receipt.</summary>
public sealed record RefundPrintData(Refund Refund, Invoice Invoice, Hostel Hostel, Student Student);

/// <summary>
/// Refunds (client decisions, version 1.5): the admin pays back all or part of what was paid on a fee, any number of
/// times, also after the student has left. A refund can never be more than what is still paid on the fee after
/// earlier refunds; it lowers the fee by the same amount, so the pending amount stays the same. Methods are those of
/// payments, and every method except cash needs a reference; a reason is required. A refund entered by mistake can be
/// deleted, which is kept in the payment change history.
/// </summary>
public static class RefundService
{
    public const string NumberPrefix = "SBH/RF";

    public static List<Refund> GetRefunds(int hostelId) => RefundRepository.GetForHostel(hostelId);

    public static List<Refund> GetForInvoice(int invoiceId) => RefundRepository.GetForInvoice(invoiceId);

    public static Refund? GetRefund(int refundId) => RefundRepository.Get(refundId);

    /// <summary>The student's fees that still have money paid on them (the ones a refund can be made from).</summary>
    public static List<Invoice> GetRefundableInvoices(int studentId) =>
        InvoiceRepository.GetForStudent(studentId).Where(i => i.PaidAmount > 0).ToList();

    public static Refund Record(Refund input)
    {
        Invoice invoice = InvoiceRepository.Get(input.InvoiceId)
            ?? throw new ValidationException("Please select the fee to refund from.");
        var refund = new Refund
        {
            StudentId = invoice.StudentId,
            InvoiceId = invoice.InvoiceId,
            RefundDate = input.RefundDate.Date,
            Amount = input.Amount,
            RefundMethod = Validators.Clean(input.RefundMethod),
            Reference = Validators.Clean(input.Reference),
            Reason = Validators.Clean(input.Reason),
            CreatedDate = DateTime.Now,
        };
        Validate(refund);

        int year = AcademicYear.Of(refund.RefundDate);
        string prefix = $"{NumberPrefix}/{AcademicYear.Label(year)}/";
        int refundId = Db.InTransaction((connection, transaction) =>
        {
            // Read inside the transaction so two quick refunds cannot together exceed what was paid.
            decimal refundable = PaymentRepository.GetPaidAmount(connection, transaction, invoice.InvoiceId);
            if (refundable <= 0)
            {
                throw new ValidationException($"Nothing is paid on invoice {invoice.InvoiceNumber}, so nothing can be refunded.");
            }
            if (refund.Amount > refundable)
            {
                throw new ValidationException(
                    $"The refund cannot be more than {Money.Format(refundable)}, the amount paid on invoice {invoice.InvoiceNumber} " +
                    "after earlier refunds.");
            }
            refund.RefundNumber = prefix + InvoiceService.NextNumber(prefix, prefix,
                    p => RefundRepository.GetNumbersStartingWith(connection, transaction, p))
                .ToString("0000", CultureInfo.InvariantCulture);
            return RefundRepository.Insert(connection, transaction, refund);
        });

        AppLogger.Info($"Refund {refund.RefundNumber} of {refund.Amount} on invoice {invoice.InvoiceNumber}.");
        return RefundRepository.Get(refundId)!;
    }

    /// <summary>Deletes a refund entered by mistake; the fee and paid amount go back up. Kept in the change history.</summary>
    public static void Delete(int refundId, string? reason)
    {
        Refund refund = RefundRepository.Get(refundId) ?? throw new ValidationException("This refund no longer exists.");
        string cleanReason = Validators.Clean(reason);
        if (cleanReason.Length == 0)
        {
            throw new ValidationException("Please enter the reason, for example \"entered twice by mistake\".");
        }
        Validators.CheckLength(cleanReason, 255, "Reason");

        Db.InTransaction((connection, transaction) =>
        {
            RefundRepository.Delete(connection, transaction, refundId);
            PaymentChangeRepository.Insert(connection, transaction, new PaymentChange
            {
                // Not a payment: 0 keeps it out of any payment's own history; the number identifies the refund.
                PaymentId = 0,
                ReceiptNumber = refund.RefundNumber,
                StudentId = refund.StudentId,
                ChangeType = PaymentChangeType.RefundDeleted,
                ChangedDate = DateTime.Now,
                Details = $"Deleted refund of {Money.Format(refund.Amount)} on {Date(refund.RefundDate)} by {refund.RefundMethod}" +
                          (refund.Reference.Length > 0 ? $" (ref. {refund.Reference})" : "") + $", invoice {refund.InvoiceNumber}",
                Reason = cleanReason,
            });
        });
        AppLogger.Info($"Deleted refund {refund.RefundNumber}.");
    }

    public static RefundPrintData GetPrintData(int refundId)
    {
        Refund refund = RefundRepository.Get(refundId) ?? throw new ValidationException("This refund no longer exists.");
        InvoicePrintData invoiceData = InvoiceService.GetPrintData(refund.InvoiceId);
        return new RefundPrintData(refund, invoiceData.Invoice, invoiceData.Hostel, invoiceData.Student);
    }

    private static void Validate(Refund refund)
    {
        if (refund.RefundDate > DateTime.Today)
        {
            throw new ValidationException("The refund date cannot be in the future.");
        }
        if (refund.Amount <= 0)
        {
            throw new ValidationException("Please enter an amount greater than zero.");
        }
        if (decimal.Round(refund.Amount, 2) != refund.Amount)
        {
            throw new ValidationException("The amount can have at most two decimal places (paise).");
        }
        if (!PaymentMethod.All.Contains(refund.RefundMethod))
        {
            throw new ValidationException("Please select how the money was paid back.");
        }
        if (PaymentMethod.RequiresReference(refund.RefundMethod) && refund.Reference.Length == 0)
        {
            throw new ValidationException($"Please enter the {PaymentMethod.ReferenceName(refund.RefundMethod).ToLowerInvariant()}.");
        }
        if (refund.Reason.Length == 0)
        {
            throw new ValidationException("Please enter the reason for the refund, for example \"left the hostel in December\".");
        }
        Validators.CheckLength(refund.Reference, 100, "Reference");
        Validators.CheckLength(refund.Reason, 255, "Reason");
    }

    private static string Date(DateTime date) => date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
}
