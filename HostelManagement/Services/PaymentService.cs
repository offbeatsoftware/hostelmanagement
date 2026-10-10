using System.Globalization;
using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Services;

/// <summary>Everything printed on a receipt. The invoice's paid and pending amounts include this payment.</summary>
public sealed record ReceiptPrintData(Payment Payment, Invoice Invoice, Hostel Hostel, Student Student);

/// <summary>
/// Payments (client decisions): a payment entered wrongly can be corrected or deleted, and every correction is
/// kept in the change history. Every payment is made against a student's yearly invoice; the student pays
/// any amount any number of times, but never more than the amount still pending (no advance or extra payments); methods are cash,
/// UPI, bank transfer and cheque, and every method except cash needs a reference. Each payment gets a
/// receipt number such as SBH/R/2026-27/0001, one sequence per academic year of the payment date.
/// </summary>
public static class PaymentService
{
    public const string ReceiptPrefix = "SBH/R";

    public static List<Payment> GetPayments(int hostelId) => PaymentRepository.GetForHostel(hostelId);

    public static List<Payment> GetPaymentsForInvoice(int invoiceId) => PaymentRepository.GetForInvoice(invoiceId);

    /// <summary>Invoices of the hostel that still have an amount pending, oldest first.</summary>
    public static List<Invoice> GetInvoicesWithPending(int hostelId) =>
        InvoiceService.GetInvoices(hostelId)
            .Where(i => i.PendingAmount > 0)
            .OrderBy(i => i.AcademicYear)
            .ThenBy(i => i.StudentName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(i => i.InvoiceId)
            .ToList();

    /// <summary>Records a payment against an invoice and gives it the next receipt number.</summary>
    public static Payment Record(Payment payment)
    {
        payment.PaymentMethod = Validators.Clean(payment.PaymentMethod);
        payment.Reference = Validators.Clean(payment.Reference);
        payment.Remarks = Validators.Clean(payment.Remarks);
        payment.PaymentDate = payment.PaymentDate.Date;

        Invoice invoice = InvoiceService.GetInvoice(payment.InvoiceId)
            ?? throw new ValidationException("Please select the invoice.");
        Validate(payment);

        payment.StudentId = invoice.StudentId;
        payment.CreatedDate = DateTime.Now;
        string prefix = $"{ReceiptPrefix}/{InvoiceService.AcademicYearLabel(payment.PaymentDate)}/";

        int paymentId = Db.InTransaction((connection, transaction) =>
        {
            // Read inside the transaction so two quick payments cannot together exceed the invoice.
            decimal pending = invoice.TotalAmount - PaymentRepository.GetPaidAmount(connection, transaction, invoice.InvoiceId);
            if (pending <= 0)
            {
                throw new ValidationException($"Invoice {invoice.InvoiceNumber} is already fully paid.");
            }
            if (payment.Amount > pending)
            {
                throw new ValidationException(
                    $"The amount cannot be more than the pending amount of {Money.Format(pending)}. " +
                    "Advance or extra payments are not accepted.");
            }

            payment.ReceiptNumber = prefix + NextSequence(PaymentRepository.GetNumbersStartingWith(connection, transaction, prefix), prefix)
                .ToString("0000", CultureInfo.InvariantCulture);
            return PaymentRepository.Insert(connection, transaction, payment);
        });

        AppLogger.Info($"Recorded payment {payment.ReceiptNumber} of {payment.Amount} against invoice {invoice.InvoiceNumber}.");
        return PaymentRepository.Get(paymentId)!;
    }

    /// <summary>
    /// Corrects a payment entered wrongly (client decision): date, amount, method, reference and remarks. The
    /// receipt number stays the same, the amount can never be more than the fee still allows, and the change is
    /// recorded in the payment's change history.
    /// </summary>
    public static Payment Update(Payment edited, string? reason = null)
    {
        Payment existing = PaymentRepository.Get(edited.PaymentId)
            ?? throw new ValidationException("This payment no longer exists.");
        var payment = new Payment
        {
            PaymentId = existing.PaymentId,
            ReceiptNumber = existing.ReceiptNumber,
            StudentId = existing.StudentId,
            InvoiceId = existing.InvoiceId,
            PaymentDate = edited.PaymentDate.Date,
            Amount = edited.Amount,
            PaymentMethod = Validators.Clean(edited.PaymentMethod),
            Reference = Validators.Clean(edited.Reference),
            Remarks = Validators.Clean(edited.Remarks),
        };
        Validate(payment);
        string cleanReason = CleanReason(reason);

        string details = DescribeChanges(existing, payment);
        if (details.Length == 0)
        {
            throw new ValidationException("Nothing was changed.");
        }

        Invoice invoice = InvoiceService.GetInvoice(existing.InvoiceId)
            ?? throw new ValidationException("The payment's invoice no longer exists.");
        Db.InTransaction((connection, transaction) =>
        {
            // The other payments plus this one may not exceed the fee (read inside the transaction).
            decimal otherPayments = PaymentRepository.GetPaidAmount(connection, transaction, invoice.InvoiceId) - existing.Amount;
            decimal allowed = invoice.TotalAmount - otherPayments;
            if (payment.Amount > allowed)
            {
                throw new ValidationException(
                    $"The amount cannot be more than {Money.Format(allowed)}, the fee still open without this payment. " +
                    "Advance or extra payments are not accepted.");
            }

            PaymentRepository.Update(connection, transaction, payment);
            PaymentChangeRepository.Insert(connection, transaction, new PaymentChange
            {
                PaymentId = payment.PaymentId,
                ReceiptNumber = payment.ReceiptNumber,
                StudentId = payment.StudentId,
                ChangeType = PaymentChangeType.Edited,
                ChangedDate = DateTime.Now,
                Details = details,
                Reason = cleanReason,
            });
        });

        AppLogger.Info($"Edited payment {payment.ReceiptNumber}: {details}");
        return PaymentRepository.Get(payment.PaymentId)!;
    }

    /// <summary>
    /// Deletes a payment entered by mistake; the invoice's pending amount goes up again. The deletion is recorded
    /// in the change history with the payment's details.
    /// </summary>
    public static void Delete(int paymentId, string? reason = null)
    {
        Payment payment = PaymentRepository.Get(paymentId)
            ?? throw new ValidationException("This payment no longer exists.");
        string cleanReason = CleanReason(reason);

        Db.InTransaction((connection, transaction) =>
        {
            PaymentRepository.Delete(connection, transaction, paymentId);
            PaymentChangeRepository.Insert(connection, transaction, new PaymentChange
            {
                PaymentId = payment.PaymentId,
                ReceiptNumber = payment.ReceiptNumber,
                StudentId = payment.StudentId,
                ChangeType = PaymentChangeType.Deleted,
                ChangedDate = DateTime.Now,
                Details = $"Deleted {Money.Format(payment.Amount)} paid on {Date(payment.PaymentDate)} by {payment.PaymentMethod}" +
                          (payment.Reference.Length > 0 ? $" (ref. {payment.Reference})" : "") +
                          $", invoice {payment.InvoiceNumber}",
                Reason = cleanReason,
            });
        });
        AppLogger.Info($"Deleted payment {payment.ReceiptNumber} of {payment.Amount} against invoice {payment.InvoiceNumber}.");
    }

    /// <summary>Every correction and deletion of the hostel's payments, newest first.</summary>
    public static List<PaymentChange> GetChanges(int hostelId) => PaymentChangeRepository.GetForHostel(hostelId);

    /// <summary>The corrections of one payment, oldest first.</summary>
    public static List<PaymentChange> GetChangesForPayment(int paymentId) => PaymentChangeRepository.GetForPayment(paymentId);

    /// <summary>"Amount Rs. 10,000.00 to Rs. 12,000.00; Paid by Cash to UPI", or empty when nothing changed.</summary>
    private static string DescribeChanges(Payment before, Payment after)
    {
        var parts = new List<string>();
        if (before.PaymentDate.Date != after.PaymentDate.Date)
        {
            parts.Add($"Date {Date(before.PaymentDate)} to {Date(after.PaymentDate)}");
        }
        if (before.Amount != after.Amount)
        {
            parts.Add($"Amount {Money.Format(before.Amount)} to {Money.Format(after.Amount)}");
        }
        if (before.PaymentMethod != after.PaymentMethod)
        {
            parts.Add($"Paid by {before.PaymentMethod} to {after.PaymentMethod}");
        }
        if (before.Reference != after.Reference)
        {
            parts.Add($"Reference '{before.Reference}' to '{after.Reference}'");
        }
        if (before.Remarks != after.Remarks)
        {
            parts.Add("Remarks changed");
        }
        return string.Join("; ", parts);
    }

    private static string CleanReason(string? reason)
    {
        string clean = Validators.Clean(reason);
        Validators.CheckLength(clean, 255, "Reason");
        return clean;
    }

    private static string Date(DateTime date) => date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>Hostel, student and invoice details for printing the receipt.</summary>
    public static ReceiptPrintData GetReceiptData(int paymentId)
    {
        Payment payment = PaymentRepository.Get(paymentId)
            ?? throw new ValidationException("This payment no longer exists.");
        InvoicePrintData invoiceData = InvoiceService.GetPrintData(payment.InvoiceId);
        return new ReceiptPrintData(payment, invoiceData.Invoice, invoiceData.Hostel, invoiceData.Student);
    }

    private static void Validate(Payment payment)
    {
        if (payment.PaymentDate > DateTime.Today)
        {
            throw new ValidationException("The payment date cannot be in the future.");
        }
        if (payment.Amount <= 0)
        {
            throw new ValidationException("Please enter an amount greater than zero.");
        }
        if (decimal.Round(payment.Amount, 2) != payment.Amount)
        {
            throw new ValidationException("The amount can have at most two decimal places (paise).");
        }
        if (!PaymentMethod.All.Contains(payment.PaymentMethod))
        {
            throw new ValidationException("Please select how the payment was made.");
        }
        if (PaymentMethod.RequiresReference(payment.PaymentMethod) && payment.Reference.Length == 0)
        {
            throw new ValidationException($"Please enter the {PaymentMethod.ReferenceName(payment.PaymentMethod).ToLowerInvariant()}.");
        }
        Validators.CheckLength(payment.Reference, 100, "Reference");
        Validators.CheckLength(payment.Remarks, 255, "Remarks");
    }

    private static int NextSequence(IEnumerable<string> numbers, string prefix) =>
        numbers
            .Select(n => int.TryParse(n[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out int value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
}
