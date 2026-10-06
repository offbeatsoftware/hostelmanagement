using HostelManagement.Forms;
using HostelManagement.Models;
using HostelManagement.Reports;
using HostelManagement.Services;
using Xunit;

namespace HostelManagement.Tests;

public sealed class PaymentServiceTests : TestDatabase
{
    // Half of the yearly Double sharing rent of 120000.
    private const decimal InvoiceTotal = 60_000m;

    /// <summary>A student in a room with a 60000 invoice for the previous billing period.</summary>
    private Invoice CreateInvoice(string name = "Aman")
    {
        BillingPeriod period = BillingPeriods.For(
            BillingPeriods.For(DateTime.Today, BillingFrequency.HalfYearly).From.AddDays(-1), BillingFrequency.HalfYearly);

        RoomService.UpdateRent(SharingTypeId(2), 120_000m);
        Room room = RoomService.GetRooms(HostelId).FirstOrDefault()
            ?? RoomService.Save(new Room { HostelId = HostelId, RoomNumber = "101", SharingTypeId = SharingTypeId(2), Gender = RoomGender.Male });

        int studentId = StudentService.Save(
            new Student { StudentName = name, Gender = RoomGender.Male, Mobile = "9876543210", CollegeId = CollegeId, AdmissionDate = period.From },
            new Parent { ParentName = "Rakesh", Mobile = "9812345678", Email = "rakesh@example.com" }).StudentId;
        AllocationService.CheckIn(studentId, room.RoomId, period.From);
        return InvoiceService.Create(studentId, period);
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
