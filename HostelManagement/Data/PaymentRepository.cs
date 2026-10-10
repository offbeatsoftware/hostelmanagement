using System.Data;
using System.Data.OleDb;
using HostelManagement.Models;

namespace HostelManagement.Data;

public static class PaymentRepository
{
    private const string SelectPayments =
        "SELECT p.*, s.[StudentName], i.[InvoiceNumber] " +
        "FROM (([Payment] AS p INNER JOIN [Student] AS s ON p.[StudentId] = s.[StudentId]) " +
        "INNER JOIN [Invoice] AS i ON p.[InvoiceId] = i.[InvoiceId]) " +
        "INNER JOIN [College] AS c ON s.[CollegeId] = c.[CollegeId]";

    /// <summary>Payments of the hostel's students, newest first.</summary>
    public static List<Payment> GetForHostel(int hostelId) =>
        Db.Query(SelectPayments + " WHERE c.[HostelId] = ? ORDER BY p.[PaymentDate] DESC, p.[PaymentId] DESC", Map,
            Db.Param("@HostelId", hostelId));

    /// <summary>Payments of one invoice, oldest first.</summary>
    public static List<Payment> GetForInvoice(int invoiceId) =>
        Db.Query(SelectPayments + " WHERE p.[InvoiceId] = ? ORDER BY p.[PaymentDate], p.[PaymentId]", Map,
            Db.Param("@InvoiceId", invoiceId));

    public static Payment? Get(int paymentId) =>
        Db.Query(SelectPayments + " WHERE p.[PaymentId] = ?", Map, Db.Param("@PaymentId", paymentId)).FirstOrDefault();

    /// <summary>The amount already paid against the invoice (read inside the transaction that adds a payment).</summary>
    public static decimal GetPaidAmount(OleDbConnection connection, OleDbTransaction transaction, int invoiceId)
    {
        object? paid = Db.Scalar(connection, transaction, "SELECT SUM([Amount]) FROM [Payment] WHERE [InvoiceId] = ?",
            Db.Param("@InvoiceId", invoiceId));
        return paid is null or DBNull ? 0m : Convert.ToDecimal(paid);
    }

    /// <summary>All receipt numbers starting with the prefix (for the next number in the sequence).</summary>
    public static List<string> GetNumbersStartingWith(OleDbConnection connection, OleDbTransaction transaction, string prefix) =>
        Db.Query(connection, transaction, "SELECT [ReceiptNumber] FROM [Payment] WHERE LEFT([ReceiptNumber], ?) = ?",
            r => r.GetText("ReceiptNumber"),
            Db.Param("@Length", prefix.Length),
            Db.Param("@Prefix", prefix));

    public static int Insert(OleDbConnection connection, OleDbTransaction transaction, Payment payment) =>
        Db.Insert(connection, transaction,
            "INSERT INTO [Payment] ([ReceiptNumber], [StudentId], [InvoiceId], [PaymentDate], [Amount], [PaymentMethod], " +
            "[Reference], [Remarks], [CreatedDate]) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
            Db.Param("@ReceiptNumber", payment.ReceiptNumber),
            Db.Param("@StudentId", payment.StudentId),
            Db.Param("@InvoiceId", payment.InvoiceId),
            Db.Param("@PaymentDate", payment.PaymentDate),
            Db.Param("@Amount", payment.Amount),
            Db.Param("@PaymentMethod", payment.PaymentMethod),
            Db.OptionalText("@Reference", payment.Reference),
            Db.OptionalText("@Remarks", payment.Remarks),
            Db.Param("@CreatedDate", payment.CreatedDate));

    /// <summary>Changes a payment's date, amount, method, reference and remarks (the receipt number stays).</summary>
    public static int Update(OleDbConnection connection, OleDbTransaction transaction, Payment payment) =>
        Db.Execute(connection, transaction,
            "UPDATE [Payment] SET [PaymentDate] = ?, [Amount] = ?, [PaymentMethod] = ?, [Reference] = ?, [Remarks] = ? " +
            "WHERE [PaymentId] = ?",
            Db.Param("@PaymentDate", payment.PaymentDate),
            Db.Param("@Amount", payment.Amount),
            Db.Param("@PaymentMethod", payment.PaymentMethod),
            Db.OptionalText("@Reference", payment.Reference),
            Db.OptionalText("@Remarks", payment.Remarks),
            Db.Param("@PaymentId", payment.PaymentId));

    public static int Delete(OleDbConnection connection, OleDbTransaction transaction, int paymentId) =>
        Db.Execute(connection, transaction, "DELETE FROM [Payment] WHERE [PaymentId] = ?", Db.Param("@PaymentId", paymentId));

    private static Payment Map(IDataRecord record) => new()
    {
        PaymentId = record.GetInt("PaymentId"),
        ReceiptNumber = record.GetText("ReceiptNumber"),
        StudentId = record.GetInt("StudentId"),
        InvoiceId = record.GetInt("InvoiceId"),
        PaymentDate = record.GetDate("PaymentDate"),
        Amount = record.GetMoney("Amount"),
        PaymentMethod = record.GetText("PaymentMethod"),
        Reference = record.GetText("Reference"),
        Remarks = record.GetText("Remarks"),
        CreatedDate = record.GetDate("CreatedDate"),
        StudentName = record.GetText("StudentName"),
        InvoiceNumber = record.GetText("InvoiceNumber"),
    };
}
