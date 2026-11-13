using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Reports;
using HostelManagement.Services;
using Xunit;

namespace HostelManagement.Tests;

/// <summary>
/// Version 1.5: check-out with a fee pending, refunds (full, partial, several, after check-out) with a receipt,
/// inactive students made active again or deleted permanently.
/// </summary>
public sealed class RefundServiceTests : TestDatabase
{
    /// <summary>A student in a room with a yearly fee of 60000 (50000 rent and 10000 transport).</summary>
    private (int StudentId, Invoice Invoice) StudentWithFee(string name = "Aman")
    {
        Room room = RoomService.GetRooms(HostelId).FirstOrDefault(r => r.Available > 0) ?? AddRoom($"{100 + Count("Room") + 1}", beds: 3);
        int studentId = AddStudentWithParents(name);
        return (studentId, CheckInWithFee(studentId, room, 50_000m, 10_000m));
    }

    private static Refund Refund(Invoice invoice, decimal amount, string method = PaymentMethod.Cash, string reference = "",
        string reason = "Left the hostel early", DateTime? date = null) =>
        RefundService.Record(new Refund
        {
            InvoiceId = invoice.InvoiceId,
            RefundDate = date ?? DateTime.Today,
            Amount = amount,
            RefundMethod = method,
            Reference = reference,
            Reason = reason,
        });

    private static Invoice Reload(Invoice invoice) => InvoiceService.GetInvoice(invoice.InvoiceId)!;

    [Fact]
    public void CheckOut_WithFeePending_IsAllowed_AndTheFeeStaysPending()
    {
        (int studentId, Invoice invoice) = StudentWithFee();
        Pay(invoice.InvoiceId, 20_000m);
        Assert.Equal(40_000m, AllocationService.GetPendingFee(studentId));

        AllocationService.CheckOut(studentId, DateTime.Today);

        Assert.Equal(StudentStatus.Left, StudentService.GetStudent(studentId)!.Status);
        Assert.Null(AllocationService.GetCurrentAllocation(studentId));
        Invoice after = Reload(invoice);
        Assert.Equal(40_000m, after.PendingAmount);
        Assert.Equal(0m, after.WaivedAmount);
    }

    [Fact]
    public void CheckOut_WithoutAnyPayment_CanWaiveTheWholeFee()
    {
        (int studentId, Invoice invoice) = StudentWithFee();

        AllocationService.CheckOut(studentId, DateTime.Today, "Left after a week", waivePending: true);

        Invoice after = Reload(invoice);
        Assert.Equal(60_000m, after.FeeAmount);
        Assert.Equal(60_000m, after.WaivedAmount);
        Assert.Equal(0m, after.TotalAmount);
        Assert.Equal(0m, after.PendingAmount);
        Assert.Equal(InvoiceStatus.Paid, after.Status);
        Assert.Equal(0m, AllocationService.GetPendingFee(studentId));
    }

    [Fact]
    public void CheckOut_WaivesOnlyWhatIsStillPending()
    {
        (int studentId, Invoice invoice) = StudentWithFee();
        Pay(invoice.InvoiceId, 15_000m);

        AllocationService.CheckOut(studentId, DateTime.Today, waivePending: true);

        Invoice after = Reload(invoice);
        Assert.Equal(45_000m, after.WaivedAmount);
        Assert.Equal(15_000m, after.TotalAmount);
        Assert.Equal(15_000m, after.PaidAmount);
        Assert.Equal(0m, after.PendingAmount);
    }

    [Fact]
    public void Refund_PartialAndSeveralTimes_ReducesPaidAndFee_PendingUnchanged()
    {
        (_, Invoice invoice) = StudentWithFee();
        Pay(invoice.InvoiceId, 30_000m);

        Refund first = Refund(invoice, 5_000m);
        Refund second = Refund(invoice, 2_500.50m, PaymentMethod.Upi, "UPI777");

        Invoice after = Reload(invoice);
        Assert.Equal(60_000m, after.FeeAmount);
        Assert.Equal(30_000m, after.ReceivedAmount);
        Assert.Equal(7_500.50m, after.RefundedAmount);
        Assert.Equal(22_499.50m, after.PaidAmount);
        Assert.Equal(52_499.50m, after.TotalAmount);
        Assert.Equal(30_000m, after.PendingAmount);

        string prefix = $"SBH/RF/{AcademicYear.Label(AcademicYear.Of(DateTime.Today))}/";
        Assert.Equal(prefix + "0001", first.RefundNumber);
        Assert.Equal(prefix + "0002", second.RefundNumber);
        Assert.Equal("Aman", first.StudentName);
        Assert.Equal(invoice.InvoiceNumber, first.InvoiceNumber);
        Assert.Equal(2, RefundService.GetForInvoice(invoice.InvoiceId).Count);
        Assert.Equal(2, RefundService.GetRefunds(HostelId).Count);
    }

    [Fact]
    public void Refund_InFull_LeavesNothingPaid_AndNoMoreCanBeRefunded()
    {
        (_, Invoice invoice) = StudentWithFee();
        Pay(invoice.InvoiceId, 10_000m);
        Pay(invoice.InvoiceId, 5_000m);

        Refund(invoice, 15_000m);

        Invoice after = Reload(invoice);
        Assert.Equal(0m, after.PaidAmount);
        Assert.Equal(45_000m, after.PendingAmount);
        Assert.Contains("Nothing is paid", Assert.Throws<ValidationException>(() => Refund(invoice, 1m)).Message);
        Assert.Empty(RefundService.GetRefundableInvoices(invoice.StudentId));
    }

    [Fact]
    public void Refund_MoreThanPaidAfterEarlierRefunds_IsRejected()
    {
        (_, Invoice invoice) = StudentWithFee();
        Pay(invoice.InvoiceId, 10_000m);
        Refund(invoice, 4_000m);

        ValidationException error = Assert.Throws<ValidationException>(() => Refund(invoice, 6_000.01m));

        Assert.Contains("cannot be more than", error.Message);
        Assert.Single(RefundService.GetForInvoice(invoice.InvoiceId));
    }

    [Fact]
    public void Refund_Rules()
    {
        (_, Invoice invoice) = StudentWithFee();
        Pay(invoice.InvoiceId, 10_000m);

        Assert.Throws<ValidationException>(() => Refund(invoice, 0m));
        Assert.Throws<ValidationException>(() => Refund(invoice, -5m));
        Assert.Throws<ValidationException>(() => Refund(invoice, 10.555m));
        Assert.Throws<ValidationException>(() => Refund(invoice, 100m, reason: " "));
        Assert.Throws<ValidationException>(() => Refund(invoice, 100m, PaymentMethod.Upi, reference: ""));
        Assert.Throws<ValidationException>(() => Refund(invoice, 100m, date: DateTime.Today.AddDays(1)));
        Assert.Throws<ValidationException>(() => Refund(invoice, 100m, method: "Gold"));
        Assert.Empty(RefundService.GetForInvoice(invoice.InvoiceId));
    }

    [Fact]
    public void Refund_IsAllowedAfterTheStudentHasLeft()
    {
        (int studentId, Invoice invoice) = StudentWithFee();
        Pay(invoice.InvoiceId, 20_000m);
        AllocationService.CheckOut(studentId, DateTime.Today, waivePending: true);

        Assert.Single(RefundService.GetRefundableInvoices(studentId));
        Refund(invoice, 8_000m);

        Invoice after = Reload(invoice);
        Assert.Equal(12_000m, after.PaidAmount);
        Assert.Equal(12_000m, after.TotalAmount);
        Assert.Equal(0m, after.PendingAmount);
    }

    [Fact]
    public void DeleteRefund_PutsThePaidAmountBack_AndIsInTheChangeHistory()
    {
        (_, Invoice invoice) = StudentWithFee();
        Pay(invoice.InvoiceId, 10_000m);
        Refund refund = Refund(invoice, 3_000m);

        Assert.Throws<ValidationException>(() => RefundService.Delete(refund.RefundId, ""));
        RefundService.Delete(refund.RefundId, "Entered twice");

        Assert.Equal(10_000m, Reload(invoice).PaidAmount);
        Assert.Empty(RefundService.GetForInvoice(invoice.InvoiceId));
        PaymentChange change = PaymentService.GetChanges(HostelId).Single(c => c.ChangeType == PaymentChangeType.RefundDeleted);
        Assert.Equal(refund.RefundNumber, change.ReceiptNumber);
        Assert.Equal("Entered twice", change.Reason);
    }

    [Fact]
    public void PaymentsBelowTheRefunds_CannotBeDeletedOrReduced()
    {
        (_, Invoice invoice) = StudentWithFee();
        Payment payment = Pay(invoice.InvoiceId, 10_000m);
        Refund(invoice, 6_000m);

        Assert.Throws<ValidationException>(() => PaymentService.Delete(payment.PaymentId, "Wrong"));
        payment.Amount = 5_000m;
        Assert.Throws<ValidationException>(() => PaymentService.Update(payment, "Wrong amount"));
        Assert.Equal(4_000m, Reload(invoice).PaidAmount);
    }

    [Fact]
    public void InvoiceWithARefund_CannotBeDeleted()
    {
        (_, Invoice invoice) = StudentWithFee();
        Payment payment = Pay(invoice.InvoiceId, 1_000m);
        Refund refund = Refund(invoice, 1_000m);

        Assert.Throws<ValidationException>(() => InvoiceService.Delete(invoice.InvoiceId));
        RefundService.Delete(refund.RefundId, "Test");
        PaymentService.Delete(payment.PaymentId, "Test");
        InvoiceService.Delete(invoice.InvoiceId);
    }

    [Fact]
    public void RefundReceipt_IsWrittenToTheReceiptsFolder()
    {
        (_, Invoice invoice) = StudentWithFee();
        Pay(invoice.InvoiceId, 25_000m);
        Refund refund = Refund(invoice, 5_000m, PaymentMethod.BankTransfer, "NEFT123");

        RefundPrintData data = RefundService.GetPrintData(refund.RefundId);
        string path = RefundReceiptPdfWriter.SaveToReceiptsFolder(data);

        Assert.Equal(20_000m, data.Invoice.PaidAmount);
        Assert.Equal(35_000m, data.Invoice.PendingAmount);
        Assert.Equal(Path.Combine(DataFolder, "Receipts", RefundReceiptPdfWriter.FileName(refund)), path);
        Assert.StartsWith("Refund_SBH-RF-", Path.GetFileName(path));
        byte[] bytes = File.ReadAllBytes(path);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void InvoicePdf_ListsTheRefundsAndTheWaiver()
    {
        (int studentId, Invoice invoice) = StudentWithFee();
        Pay(invoice.InvoiceId, 25_000m);
        Refund(invoice, 5_000m);
        AllocationService.CheckOut(studentId, DateTime.Today, waivePending: true);

        InvoicePrintData data = InvoiceService.GetPrintData(invoice.InvoiceId);
        string path = Path.Combine(DataFolder, "invoice.pdf");
        InvoicePdfWriter.Write(data, path);

        Assert.Single(data.Refunds!);
        Assert.Equal(35_000m, data.Invoice.WaivedAmount);
        Assert.True(new FileInfo(path).Length > 1000);
    }

    [Fact]
    public void Dashboard_ReceivedIsNetOfRefunds()
    {
        (_, Invoice invoice) = StudentWithFee();
        Pay(invoice.InvoiceId, 10_000m);
        Refund(invoice, 4_000m);

        DashboardData data = DashboardService.Get(HostelId, DateTime.Today);

        Assert.Equal(6_000m, data.ReceivedThisMonth);
        Assert.Equal(6_000m, data.ReceivedThisYear);
    }

    [Fact]
    public void MakeActive_BringsAnInactiveStudentBack()
    {
        (int studentId, _) = StudentWithFee();
        AllocationService.CheckOut(studentId, DateTime.Today);

        StudentService.MakeActive(studentId);

        Assert.Equal(StudentStatus.Active, StudentService.GetStudent(studentId)!.Status);
        Assert.Throws<ValidationException>(() => StudentService.MakeActive(studentId));
    }

    [Fact]
    public void DeletePermanently_OnlyForInactiveStudents_WithAReason()
    {
        (int studentId, _) = StudentWithFee();

        Assert.Throws<ValidationException>(() => StudentService.DeletePermanently(studentId, "Left", backupFirst: false));
        AllocationService.CheckOut(studentId, DateTime.Today);
        Assert.Throws<ValidationException>(() => StudentService.DeletePermanently(studentId, " ", backupFirst: false));
        Assert.NotNull(StudentService.GetStudent(studentId));
    }

    [Fact]
    public void DeletePermanently_RemovesEveryRecordOfTheStudent_AndKeepsOthers()
    {
        (int studentId, Invoice invoice) = StudentWithFee("Aman");
        (int otherId, Invoice otherInvoice) = StudentWithFee("Ravi");
        Pay(invoice.InvoiceId, 20_000m);
        Pay(otherInvoice.InvoiceId, 1_000m);
        Refund(invoice, 2_000m);
        Payment deleted = Pay(invoice.InvoiceId, 100m);
        PaymentService.Delete(deleted.PaymentId, "Test");
        AllocationService.CheckOut(studentId, DateTime.Today);

        StudentService.DeletePermanently(studentId, "Asked by the owner", backupFirst: false);

        Assert.Null(StudentService.GetStudent(studentId));
        Assert.Equal(1, Count("Student"));
        Assert.Equal(1, Count("Invoice"));
        Assert.Equal(1, Count("Payment"));
        Assert.Equal(0, Count("Refund"));
        Assert.Equal(1, Count("RoomAllocation"));
        Assert.Equal(otherId, StudentService.GetStudents(HostelId).Single().StudentId);
        Assert.Equal(1_000m, Reload(otherInvoice).PaidAmount);
        Assert.Empty(PaymentService.GetChanges(HostelId).Where(c => c.StudentId == studentId));
    }
}
