using System.Data;
using System.Data.OleDb;
using HostelManagement.Models;

namespace HostelManagement.Data;

public static class RefundRepository
{
    private const string SelectRefunds =
        "SELECT r.*, s.[StudentName], i.[InvoiceNumber] " +
        "FROM ([Refund] AS r INNER JOIN [Student] AS s ON r.[StudentId] = s.[StudentId]) " +
        "INNER JOIN [Invoice] AS i ON r.[InvoiceId] = i.[InvoiceId]";

    /// <summary>Refunds of the hostel's students, newest first.</summary>
    public static List<Refund> GetForHostel(int hostelId) =>
        Db.Query(SelectRefunds + " WHERE s.[HostelId] = ? ORDER BY r.[RefundDate] DESC, r.[RefundId] DESC", Map,
            Db.Param("@HostelId", hostelId));

    /// <summary>Refunds of one invoice, oldest first.</summary>
    public static List<Refund> GetForInvoice(int invoiceId) =>
        Db.Query(SelectRefunds + " WHERE r.[InvoiceId] = ? ORDER BY r.[RefundDate], r.[RefundId]", Map,
            Db.Param("@InvoiceId", invoiceId));

    public static Refund? Get(int refundId) =>
        Db.Query(SelectRefunds + " WHERE r.[RefundId] = ?", Map, Db.Param("@RefundId", refundId)).FirstOrDefault();

    /// <summary>All refund numbers starting with the prefix (for the next number in the sequence).</summary>
    public static List<string> GetNumbersStartingWith(OleDbConnection connection, OleDbTransaction transaction, string prefix) =>
        Db.Query(connection, transaction, "SELECT [RefundNumber] FROM [Refund] WHERE LEFT([RefundNumber], ?) = ?",
            r => r.GetText("RefundNumber"),
            Db.Param("@Length", prefix.Length),
            Db.Param("@Prefix", prefix));

    public static int Insert(OleDbConnection connection, OleDbTransaction transaction, Refund refund) =>
        Db.Insert(connection, transaction,
            "INSERT INTO [Refund] ([RefundNumber], [StudentId], [InvoiceId], [RefundDate], [Amount], [RefundMethod], " +
            "[Reference], [Reason], [CreatedDate]) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
            Db.Param("@RefundNumber", refund.RefundNumber),
            Db.Param("@StudentId", refund.StudentId),
            Db.Param("@InvoiceId", refund.InvoiceId),
            Db.Param("@RefundDate", refund.RefundDate),
            Db.Param("@Amount", refund.Amount),
            Db.Param("@RefundMethod", refund.RefundMethod),
            Db.OptionalText("@Reference", refund.Reference),
            Db.Param("@Reason", refund.Reason),
            Db.Param("@CreatedDate", refund.CreatedDate));

    public static int Delete(OleDbConnection connection, OleDbTransaction transaction, int refundId) =>
        Db.Execute(connection, transaction, "DELETE FROM [Refund] WHERE [RefundId] = ?", Db.Param("@RefundId", refundId));

    private static Refund Map(IDataRecord record) => new()
    {
        RefundId = record.GetInt("RefundId"),
        RefundNumber = record.GetText("RefundNumber"),
        StudentId = record.GetInt("StudentId"),
        InvoiceId = record.GetInt("InvoiceId"),
        RefundDate = record.GetDate("RefundDate"),
        Amount = record.GetMoney("Amount"),
        RefundMethod = record.GetText("RefundMethod"),
        Reference = record.GetText("Reference"),
        Reason = record.GetText("Reason"),
        CreatedDate = record.GetDate("CreatedDate"),
        StudentName = record.GetText("StudentName"),
        InvoiceNumber = record.GetText("InvoiceNumber"),
    };
}
