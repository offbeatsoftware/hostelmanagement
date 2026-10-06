using System.Globalization;
using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Services;

/// <summary>Everything printed on a receipt. The invoice's paid and pending amounts include this payment.</summary>
public sealed record ReceiptPrintData(Payment Payment, Invoice Invoice, Hostel Hostel, Student Student, Parent? Parent);

/// <summary>
/// Payments (client decisions, Phase 9): every payment is made against an invoice; part payments are
/// allowed but never more than the amount still pending (no advance or extra payments); methods are cash,
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
            .OrderBy(i => i.InvoiceDate)
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

    /// <summary>Deletes a payment entered by mistake; the invoice's pending amount goes up again.</summary>
    public static void Delete(int paymentId)
    {
        Payment payment = PaymentRepository.Get(paymentId)
            ?? throw new ValidationException("This payment no longer exists.");
        PaymentRepository.Delete(paymentId);
        AppLogger.Info($"Deleted payment {payment.ReceiptNumber} of {payment.Amount} against invoice {payment.InvoiceNumber}.");
    }

    /// <summary>Hostel, student, parent and invoice details for printing the receipt.</summary>
    public static ReceiptPrintData GetReceiptData(int paymentId)
    {
        Payment payment = PaymentRepository.Get(paymentId)
            ?? throw new ValidationException("This payment no longer exists.");
        InvoicePrintData invoiceData = InvoiceService.GetPrintData(payment.InvoiceId);
        return new ReceiptPrintData(payment, invoiceData.Invoice, invoiceData.Hostel, invoiceData.Student, invoiceData.Parent);
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
