using HostelManagement.Forms;
using HostelManagement.Models;
using HostelManagement.Reports;
using HostelManagement.Services;
using Xunit;

namespace HostelManagement.Tests;

public sealed class PaymentServiceTests : TestDatabase
{
    private const decimal InvoiceTotal = 60_000m;

    /// <summary>A student in a room with a yearly fee of 60000 (50000 rent and 10000 transport).</summary>
    private Invoice CreateInvoice(string name = "Aman")
    {
        Room room = RoomService.GetRooms(HostelId).FirstOrDefault(r => r.Available > 0) ?? AddRoom($"{100 + Count("Room") + 1}", beds: 3);
        return CheckInWithFee(AddStudentWithParents(name), room, 50_000m, 10_000m);
    }

    private static Payment Pay(Invoice invoice, decimal amount, string method = PaymentMethod.Cash, string reference = "",
        DateTime? date = null) =>
        PaymentService.Record(new Payment
        {
            InvoiceId = invoice.InvoiceId,
            PaymentDate = date ?? DateTime.Today,
            Amount = amount,
            PaymentMethod = method,
            Reference = reference,
        });

    private static string ReceiptPrefix(DateTime date) => $"SBH/R/{InvoiceService.AcademicYearLabel(date)}/";

    [Fact]
    public void Record_PartPayments_UpdateInvoicePaidPendingAndStatus()
    {
        Invoice invoice = CreateInvoice();

        Payment first = Pay(invoice, 25_000m);
        Invoice afterFirst = InvoiceService.GetInvoice(invoice.InvoiceId)!;
        Assert.Equal(25_000m, afterFirst.PaidAmount);
        Assert.Equal(35_000m, afterFirst.PendingAmount);
        Assert.Equal(InvoiceStatus.PartlyPaid, afterFirst.Status);

        Pay(invoice, 35_000m, PaymentMethod.Upi, "UPI123456");
        Invoice afterSecond = InvoiceService.GetInvoice(invoice.InvoiceId)!;
        Assert.Equal(0m, afterSecond.PendingAmount);
        Assert.Equal(InvoiceStatus.Paid, afterSecond.Status);

        Assert.Equal(invoice.StudentId, first.StudentId);
        Assert.Equal("Aman", first.StudentName);
        Assert.Equal(invoice.InvoiceNumber, first.InvoiceNumber);
        Assert.Equal(2, PaymentService.GetPaymentsForInvoice(invoice.InvoiceId).Count);
    }

    [Fact]
    public void Record_MoreThanPending_IsRejected()
    {
        Invoice invoice = CreateInvoice();
        Pay(invoice, 50_000m);

        var ex = Assert.Throws<ValidationException>(() => Pay(invoice, 10_000.01m));
        Assert.Contains("cannot be more than the pending amount", ex.Message);
        Assert.Contains("10,000.00", ex.Message);

        Pay(invoice, 10_000m);
        Assert.Contains("already fully paid", Assert.Throws<ValidationException>(() => Pay(invoice, 1m)).Message);
        Assert.Equal(2, Count("Payment"));
    }

    [Theory]
    [InlineData(PaymentMethod.Upi, "UPI transaction id")]
    [InlineData(PaymentMethod.BankTransfer, "bank reference")]
    [InlineData(PaymentMethod.Cheque, "cheque number")]
    public void Record_NonCashWithoutReference_IsRejected(string method, string expected)
    {
        Invoice invoice = CreateInvoice();

        var ex = Assert.Throws<ValidationException>(() => Pay(invoice, 1000m, method, "  "));

        Assert.Contains(expected, ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, Count("Payment"));
    }

    [Fact]
    public void Record_CashWithoutReference_IsAllowed()
    {
        Invoice invoice = CreateInvoice();

        Payment payment = Pay(invoice, 1000m);

        Assert.Equal(PaymentMethod.Cash, payment.PaymentMethod);
        Assert.Equal(string.Empty, payment.Reference);
    }

    [Fact]
    public void Record_Rules()
    {
        Invoice invoice = CreateInvoice();

        Assert.Contains("greater than zero", Assert.Throws<ValidationException>(() => Pay(invoice, 0m)).Message);
        Assert.Contains("greater than zero", Assert.Throws<ValidationException>(() => Pay(invoice, -5m)).Message);
        Assert.Contains("two decimal places", Assert.Throws<ValidationException>(() => Pay(invoice, 100.555m)).Message);
        Assert.Contains("future", Assert.Throws<ValidationException>(() => Pay(invoice, 100m, date: DateTime.Today.AddDays(1))).Message);
        Assert.Contains("how the payment", Assert.Throws<ValidationException>(() => Pay(invoice, 100m, method: "Card")).Message);
        Assert.Contains("select the invoice", Assert.Throws<ValidationException>(
            () => PaymentService.Record(new Payment { InvoiceId = 999, PaymentDate = DateTime.Today, Amount = 100m })).Message);
        Assert.Contains("at most 100", Assert.Throws<ValidationException>(
            () => Pay(invoice, 100m, PaymentMethod.Cheque, new string('1', 101))).Message);
        Assert.Equal(0, Count("Payment"));
    }

    [Fact]
    public void Record_NumbersReceiptsPerAcademicYearOfThePaymentDate()
    {
        Invoice invoice = CreateInvoice();
        DateTime lastYear = DateTime.Today.AddYears(-1);

        Payment first = Pay(invoice, 1000m);
        Payment second = Pay(invoice, 1000m);
        Payment older = Pay(invoice, 1000m, date: lastYear);

        Assert.Equal(ReceiptPrefix(DateTime.Today) + "0001", first.ReceiptNumber);
        Assert.Equal(ReceiptPrefix(DateTime.Today) + "0002", second.ReceiptNumber);
        Assert.Equal(ReceiptPrefix(lastYear) + "0001", older.ReceiptNumber);
    }

    [Fact]
    public void Delete_MakesTheAmountPendingAgain_AndInvoiceCanThenBeDeleted()
    {
        Invoice invoice = CreateInvoice();
        Payment payment = Pay(invoice, 60_000m);
        Assert.Throws<ValidationException>(() => InvoiceService.Delete(invoice.InvoiceId));

        PaymentService.Delete(payment.PaymentId);

        Assert.Equal(InvoiceTotal, InvoiceService.GetInvoice(invoice.InvoiceId)!.PendingAmount);
        InvoiceService.Delete(invoice.InvoiceId);
        Assert.Equal(0, Count("Invoice"));
    }

    [Fact]
    public void Update_CorrectsAWrongEntry_KeepsTheReceiptNumber_AndRecordsTheChange()
    {
        Invoice invoice = CreateInvoice();
        Payment payment = Pay(invoice, 10_000m);

        Payment corrected = PaymentService.Update(new Payment
        {
            PaymentId = payment.PaymentId,
            PaymentDate = DateTime.Today.AddDays(-2),
            Amount = 12_000m,
            PaymentMethod = PaymentMethod.Upi,
            Reference = "UPI778899",
        }, "Wrong amount typed");

        Assert.Equal(payment.ReceiptNumber, corrected.ReceiptNumber);
        Assert.Equal(12_000m, corrected.Amount);
        Assert.Equal(PaymentMethod.Upi, corrected.PaymentMethod);
        Assert.Equal(48_000m, InvoiceService.GetInvoice(invoice.InvoiceId)!.PendingAmount);

        PaymentChange change = Assert.Single(PaymentService.GetChangesForPayment(payment.PaymentId));
        Assert.Equal(PaymentChangeType.Edited, change.ChangeType);
        Assert.Equal("Wrong amount typed", change.Reason);
        Assert.Contains("Amount ₹10,000.00 to ₹12,000.00", change.Details);
        Assert.Contains("Paid by Cash to UPI", change.Details);
        Assert.Contains("Reference '' to 'UPI778899'", change.Details);
        Assert.Contains("Date ", change.Details);
        Assert.Equal("Aman", change.StudentName);
    }

    [Fact]
    public void Update_Rules()
    {
        Invoice invoice = CreateInvoice();
        Payment first = Pay(invoice, 50_000m);
        Payment second = Pay(invoice, 5_000m);
        Payment Edit(decimal amount, string method = PaymentMethod.Cash, string reference = "", DateTime? date = null) =>
            PaymentService.Update(new Payment
            {
                PaymentId = second.PaymentId, PaymentDate = date ?? second.PaymentDate, Amount = amount,
                PaymentMethod = method, Reference = reference,
            });

        // 60,000 fee less the other payment of 50,000: at most 10,000.
        Assert.Contains("cannot be more than ₹10,000.00", Assert.Throws<ValidationException>(() => Edit(10_000.01m)).Message);
        Assert.Contains("Nothing was changed", Assert.Throws<ValidationException>(() => Edit(5_000m)).Message);
        Assert.Contains("greater than zero", Assert.Throws<ValidationException>(() => Edit(0m)).Message);
        Assert.Contains("future", Assert.Throws<ValidationException>(() => Edit(6_000m, date: DateTime.Today.AddDays(1))).Message);
        Assert.Contains("cheque number", Assert.Throws<ValidationException>(() => Edit(6_000m, PaymentMethod.Cheque)).Message);
        Assert.Contains("no longer exists", Assert.Throws<ValidationException>(() =>
            PaymentService.Update(new Payment { PaymentId = 999_999, PaymentDate = DateTime.Today, Amount = 1m })).Message);

        Assert.Equal(10_000m, Edit(10_000m).Amount);
        Assert.Equal(0m, InvoiceService.GetInvoice(invoice.InvoiceId)!.PendingAmount);
        Assert.Equal(50_000m, PaymentService.GetPaymentsForInvoice(invoice.InvoiceId).Single(p => p.PaymentId == first.PaymentId).Amount);
        Assert.Single(PaymentService.GetChanges(HostelId));
    }

    [Fact]
    public void Delete_IsRecordedInTheChangeHistoryWithTheReason()
    {
        Invoice invoice = CreateInvoice();
        Payment payment = Pay(invoice, 7_500m, PaymentMethod.Cheque, "000321");

        PaymentService.Delete(payment.PaymentId, "Entered twice");

        Assert.Empty(PaymentService.GetPaymentsForInvoice(invoice.InvoiceId));
        PaymentChange change = Assert.Single(PaymentService.GetChanges(HostelId));
        Assert.Equal(PaymentChangeType.Deleted, change.ChangeType);
        Assert.Equal(payment.ReceiptNumber, change.ReceiptNumber);
        Assert.Equal("Entered twice", change.Reason);
        Assert.Contains("Deleted ₹7,500.00", change.Details);
        Assert.Contains("by Cheque (ref. 000321)", change.Details);
        Assert.Throws<ValidationException>(() => StudentService.Delete(invoice.StudentId));
    }

    [Fact]
    public void PaymentScreens_EditDeleteAndHistoryCanBeOpened()
    {
        Invoice invoice = CreateInvoice();
        Payment payment = Pay(invoice, 1_000m);
        PaymentService.Delete(Pay(invoice, 500m).PaymentId, "Test");
        Invoice current = InvoiceService.GetInvoice(invoice.InvoiceId)!;
        List<PaymentChange> changes = PaymentService.GetChanges(HostelId);

        MainFormTests.RunOnStaThread(() =>
        {
            foreach (Form form in new Form[]
                     {
                         new PaymentEditForm(payment, current), new PaymentDeleteForm(payment),
                         new PaymentHistoryForm("History", changes),
                     })
            {
                using (form)
                {
                    form.Show();
                    Application.DoEvents();
                    form.Close();
                }
            }
        });
    }

    [Fact]
    public void GetPayments_ShowsOnlyTheHostelsPayments_NewestFirst()
    {
        Invoice invoice = CreateInvoice();
        Pay(invoice, 1000m, date: DateTime.Today.AddDays(-3));
        Payment newest = Pay(invoice, 2000m);

        int otherHostel = AddHostel("Other Hostel");

        Assert.Equal([newest.PaymentId, newest.PaymentId - 1], PaymentService.GetPayments(HostelId).Select(p => p.PaymentId));
        Assert.Empty(PaymentService.GetPayments(otherHostel));
    }

    [Fact]
    public void GetInvoicesWithPending_LeavesOutPaidInvoices()
    {
        Invoice paid = CreateInvoice("Aman");
        Invoice open = CreateInvoice("Ravi");
        Pay(paid, InvoiceTotal);
        Pay(open, 100m);

        Assert.Equal([open.InvoiceId], PaymentService.GetInvoicesWithPending(HostelId).Select(i => i.InvoiceId));
    }

    [Fact]
    public void Receipt_IsWrittenToTheReceiptsFolderWithTheBalanceAfterThePayment()
    {
        Invoice invoice = CreateInvoice();
        Payment payment = Pay(invoice, 25_000m, PaymentMethod.Cheque, "000123");

        ReceiptPrintData data = PaymentService.GetReceiptData(payment.PaymentId);
        string path = ReceiptPdfWriter.SaveToReceiptsFolder(data);

        Assert.Equal(35_000m, data.Invoice.PendingAmount);
        Assert.Equal("Aman", data.Student.StudentName);
        Assert.Equal(Path.Combine(DataFolder, "Receipts", ReceiptPdfWriter.FileName(payment)), path);
        Assert.StartsWith("Receipt_SBH-R-", Path.GetFileName(path));
        byte[] bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Theory]
    [InlineData("0", "Rupees Zero Only")]
    [InlineData("5", "Rupees Five Only")]
    [InlineData("19", "Rupees Nineteen Only")]
    [InlineData("40", "Rupees Forty Only")]
    [InlineData("1500", "Rupees One Thousand Five Hundred Only")]
    [InlineData("60000", "Rupees Sixty Thousand Only")]
    [InlineData("125000.50", "Rupees One Lakh Twenty Five Thousand and Fifty Paise Only")]
    [InlineData("99999.99", "Rupees Ninety Nine Thousand Nine Hundred Ninety Nine and Ninety Nine Paise Only")]
    [InlineData("10000000", "Rupees One Crore Only")]
    [InlineData("1234567890", "Rupees One Hundred Twenty Three Crore Forty Five Lakh Sixty Seven Thousand Eight Hundred Ninety Only")]
    public void AmountInWords_UsesTheIndianSystem(string amount, string expected) =>
        Assert.Equal(expected, PdfText.AmountInWords(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void PaymentForm_SuggestsThePendingAmountOfTheChosenInvoice()
    {
        Invoice invoice = CreateInvoice();
        Pay(invoice, 10_000m);
        List<Invoice> invoices = PaymentService.GetInvoicesWithPending(HostelId);

        MainFormTests.RunOnStaThread(() =>
        {
            using var form = new PaymentForm(invoices, invoice.InvoiceId);
            form.Show();
            Application.DoEvents();

            TextBox amount = form.Controls.OfType<TableLayoutPanel>().Single().Controls.OfType<TextBox>().First();
            Assert.Equal("50,000.00", amount.Text);
            form.Close();
        });
    }
}
