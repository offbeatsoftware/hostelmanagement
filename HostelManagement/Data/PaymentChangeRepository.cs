using System.Data;
using System.Data.OleDb;
using HostelManagement.Models;

namespace HostelManagement.Data;

public static class PaymentChangeRepository
{
    private const string SelectChanges =
        "SELECT x.*, s.[StudentName] FROM ([PaymentChange] AS x INNER JOIN [Student] AS s ON x.[StudentId] = s.[StudentId]) " +
        "INNER JOIN [College] AS c ON s.[CollegeId] = c.[CollegeId]";

    /// <summary>The changes to the payments of the hostel's students, newest first.</summary>
    public static List<PaymentChange> GetForHostel(int hostelId) =>
        Db.Query(SelectChanges + " WHERE c.[HostelId] = ? ORDER BY x.[ChangedDate] DESC, x.[PaymentChangeId] DESC", Map,
            Db.Param("@HostelId", hostelId));

    /// <summary>The changes to one payment, oldest first.</summary>
    public static List<PaymentChange> GetForPayment(int paymentId) =>
        Db.Query(SelectChanges + " WHERE x.[PaymentId] = ? ORDER BY x.[ChangedDate], x.[PaymentChangeId]", Map,
            Db.Param("@PaymentId", paymentId));

    public static void Insert(OleDbConnection connection, OleDbTransaction transaction, PaymentChange change) =>
        Db.Execute(connection, transaction,
            "INSERT INTO [PaymentChange] ([PaymentId], [ReceiptNumber], [StudentId], [ChangeType], [ChangedDate], [Details], [Reason]) " +
            "VALUES (?, ?, ?, ?, ?, ?, ?)",
            Db.Param("@PaymentId", change.PaymentId),
            Db.Param("@ReceiptNumber", change.ReceiptNumber),
            Db.Param("@StudentId", change.StudentId),
            Db.Param("@ChangeType", change.ChangeType),
            Db.Param("@ChangedDate", change.ChangedDate),
            Db.Param("@Details", change.Details.Length <= 255 ? change.Details : change.Details[..255]),
            Db.OptionalText("@Reason", change.Reason));

    private static PaymentChange Map(IDataRecord record) => new()
    {
        PaymentChangeId = record.GetInt("PaymentChangeId"),
        PaymentId = record.GetInt("PaymentId"),
        ReceiptNumber = record.GetText("ReceiptNumber"),
        StudentId = record.GetInt("StudentId"),
        ChangeType = record.GetText("ChangeType"),
        ChangedDate = record.GetDate("ChangedDate"),
        Details = record.GetText("Details"),
        Reason = record.GetText("Reason"),
        StudentName = record.GetText("StudentName"),
    };
}
