using System.Data;
using System.Data.OleDb;
using HostelManagement.Models;

namespace HostelManagement.Data;

public static class InvoiceRepository
{
    // PaidAmount = sum of the payments for the invoice (Phase 9).
    private const string SelectInvoices =
        "SELECT i.*, s.[StudentName], " +
        "(SELECT SUM(p.[Amount]) FROM [Payment] AS p WHERE p.[InvoiceId] = i.[InvoiceId]) AS [PaidAmount] " +
        "FROM ([Invoice] AS i INNER JOIN [Student] AS s ON i.[StudentId] = s.[StudentId]) " +
        "INNER JOIN [College] AS c ON s.[CollegeId] = c.[CollegeId]";

    /// <summary>Invoices of the hostel's students, newest first.</summary>
    public static List<Invoice> GetForHostel(int hostelId) =>
        Db.Query(SelectInvoices + " WHERE c.[HostelId] = ? ORDER BY i.[InvoiceDate] DESC, i.[InvoiceId] DESC", Map,
            Db.Param("@HostelId", hostelId));

    public static Invoice? Get(int invoiceId)
    {
        Invoice? invoice = Db.Query(SelectInvoices + " WHERE i.[InvoiceId] = ?", Map, Db.Param("@InvoiceId", invoiceId))
            .FirstOrDefault();
        if (invoice is not null)
        {
            invoice.Items = Db.Query("SELECT * FROM [InvoiceItem] WHERE [InvoiceId] = ? ORDER BY [InvoiceItemId]", MapItem,
                Db.Param("@InvoiceId", invoiceId));
        }
        return invoice;
    }

    /// <summary>True when the student already has an invoice for the billing period starting on this date.</summary>
    public static bool Exists(int studentId, DateTime billingFrom) =>
        Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM [Invoice] WHERE [StudentId] = ? AND [BillingFrom] = ?",
            Db.Param("@StudentId", studentId),
            Db.Param("@BillingFrom", billingFrom.Date))) > 0;

    /// <summary>All invoice numbers starting with the prefix (for the next number in the sequence).</summary>
    public static List<string> GetNumbersStartingWith(OleDbConnection connection, OleDbTransaction transaction, string prefix) =>
        Db.Query(connection, transaction, "SELECT [InvoiceNumber] FROM [Invoice] WHERE LEFT([InvoiceNumber], ?) = ?",
            r => r.GetText("InvoiceNumber"),
            Db.Param("@Length", prefix.Length),
            Db.Param("@Prefix", prefix));

    public static int Insert(OleDbConnection connection, OleDbTransaction transaction, Invoice invoice)
    {
        int invoiceId = Db.Insert(connection, transaction,
            "INSERT INTO [Invoice] ([InvoiceNumber], [StudentId], [InvoiceDate], [BillingFrom], [BillingTo], [TotalAmount]) " +
            "VALUES (?, ?, ?, ?, ?, ?)",
            Db.Param("@InvoiceNumber", invoice.InvoiceNumber),
            Db.Param("@StudentId", invoice.StudentId),
            Db.Param("@InvoiceDate", invoice.InvoiceDate),
            Db.Param("@BillingFrom", invoice.BillingFrom),
            Db.Param("@BillingTo", invoice.BillingTo),
            Db.Param("@TotalAmount", invoice.TotalAmount));

        foreach (InvoiceItem item in invoice.Items)
        {
            Db.Execute(connection, transaction,
                "INSERT INTO [InvoiceItem] ([InvoiceId], [Description], [Quantity], [Rate], [Amount]) VALUES (?, ?, ?, ?, ?)",
                Db.Param("@InvoiceId", invoiceId),
                Db.Param("@Description", item.Description),
                Db.Param("@Quantity", item.Quantity),
                Db.Param("@Rate", item.Rate),
                Db.Param("@Amount", item.Amount));
        }
        return invoiceId;
    }

    public static void Delete(OleDbConnection connection, OleDbTransaction transaction, int invoiceId)
    {
        Db.Execute(connection, transaction, "DELETE FROM [InvoiceItem] WHERE [InvoiceId] = ?", Db.Param("@InvoiceId", invoiceId));
        Db.Execute(connection, transaction, "DELETE FROM [Invoice] WHERE [InvoiceId] = ?", Db.Param("@InvoiceId", invoiceId));
    }

    /// <summary>Payments and emails that keep an invoice in use.</summary>
    public static int CountReferences(int invoiceId) =>
        Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM [Payment] WHERE [InvoiceId] = ?", Db.Param("@InvoiceId", invoiceId))) +
        Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM [EmailHistory] WHERE [InvoiceId] = ?", Db.Param("@InvoiceId", invoiceId)));

    private static Invoice Map(IDataRecord record) => new()
    {
        InvoiceId = record.GetInt("InvoiceId"),
        InvoiceNumber = record.GetText("InvoiceNumber"),
        StudentId = record.GetInt("StudentId"),
        InvoiceDate = record.GetDate("InvoiceDate"),
        BillingFrom = record.GetDate("BillingFrom"),
        BillingTo = record.GetDate("BillingTo"),
        TotalAmount = record.GetMoney("TotalAmount"),
        StudentName = record.GetText("StudentName"),
        PaidAmount = record.GetMoney("PaidAmount"),
    };

    private static InvoiceItem MapItem(IDataRecord record) => new()
    {
        InvoiceItemId = record.GetInt("InvoiceItemId"),
        InvoiceId = record.GetInt("InvoiceId"),
        Description = record.GetText("Description"),
        Quantity = record.GetInt("Quantity"),
        Rate = record.GetMoney("Rate"),
        Amount = record.GetMoney("Amount"),
    };
}
