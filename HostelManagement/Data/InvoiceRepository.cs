using System.Data;
using System.Data.OleDb;
using HostelManagement.Models;

namespace HostelManagement.Data;

public static class InvoiceRepository
{
    // PaidAmount = sum of the payments for the invoice.
    private const string SelectInvoices =
        "SELECT i.*, s.[StudentName], " +
        "(SELECT SUM(p.[Amount]) FROM [Payment] AS p WHERE p.[InvoiceId] = i.[InvoiceId]) AS [PaidAmount] " +
        "FROM ([Invoice] AS i INNER JOIN [Student] AS s ON i.[StudentId] = s.[StudentId]) " +
        "INNER JOIN [College] AS c ON s.[CollegeId] = c.[CollegeId]";

    /// <summary>Invoices of the hostel's students, newest first.</summary>
    public static List<Invoice> GetForHostel(int hostelId) =>
        Db.Query(SelectInvoices + " WHERE c.[HostelId] = ? ORDER BY i.[InvoiceDate] DESC, i.[InvoiceId] DESC", Map,
            Db.Param("@HostelId", hostelId));

    /// <summary>A student's invoices, newest academic year first.</summary>
    public static List<Invoice> GetForStudent(int studentId) =>
        Db.Query(SelectInvoices + " WHERE i.[StudentId] = ? ORDER BY i.[AcademicYear] DESC", Map,
            Db.Param("@StudentId", studentId));

    public static Invoice? Get(int invoiceId) =>
        Db.Query(SelectInvoices + " WHERE i.[InvoiceId] = ?", Map, Db.Param("@InvoiceId", invoiceId)).FirstOrDefault();

    /// <summary>The student's invoice for the academic year, if any.</summary>
    public static Invoice? GetForYear(int studentId, int academicYear) =>
        Db.Query(SelectInvoices + " WHERE i.[StudentId] = ? AND i.[AcademicYear] = ?", Map,
            Db.Param("@StudentId", studentId),
            Db.Param("@AcademicYear", academicYear)).FirstOrDefault();

    public static bool Exists(OleDbConnection connection, OleDbTransaction transaction, int studentId, int academicYear) =>
        Convert.ToInt32(Db.Scalar(connection, transaction,
            "SELECT COUNT(*) FROM [Invoice] WHERE [StudentId] = ? AND [AcademicYear] = ?",
            Db.Param("@StudentId", studentId),
            Db.Param("@AcademicYear", academicYear))) > 0;

    /// <summary>All invoice numbers starting with the prefix (for the next number in the sequence).</summary>
    public static List<string> GetNumbersStartingWith(OleDbConnection connection, OleDbTransaction transaction, string prefix) =>
        Db.Query(connection, transaction, "SELECT [InvoiceNumber] FROM [Invoice] WHERE LEFT([InvoiceNumber], ?) = ?",
            r => r.GetText("InvoiceNumber"),
            Db.Param("@Length", prefix.Length),
            Db.Param("@Prefix", prefix));

    public static int Insert(OleDbConnection connection, OleDbTransaction transaction, Invoice invoice) =>
        Db.Insert(connection, transaction,
            "INSERT INTO [Invoice] ([InvoiceNumber], [StudentId], [InvoiceDate], [AcademicYear], [RoomRent], [TransportAmount], [Remarks]) " +
            "VALUES (?, ?, ?, ?, ?, ?, ?)",
            Db.Param("@InvoiceNumber", invoice.InvoiceNumber),
            Db.Param("@StudentId", invoice.StudentId),
            Db.Param("@InvoiceDate", invoice.InvoiceDate),
            Db.Param("@AcademicYear", invoice.AcademicYear),
            Db.Param("@RoomRent", invoice.RoomRent),
            Db.Param("@TransportAmount", invoice.TransportAmount),
            Db.OptionalText("@Remarks", invoice.Remarks));

    /// <summary>Changes the agreed amounts of an invoice.</summary>
    public static int UpdateFee(int invoiceId, decimal roomRent, decimal transportAmount, string remarks) =>
        Db.Execute("UPDATE [Invoice] SET [RoomRent] = ?, [TransportAmount] = ?, [Remarks] = ? WHERE [InvoiceId] = ?",
            Db.Param("@RoomRent", roomRent),
            Db.Param("@TransportAmount", transportAmount),
            Db.OptionalText("@Remarks", remarks),
            Db.Param("@InvoiceId", invoiceId));

    public static void Delete(OleDbConnection connection, OleDbTransaction transaction, int invoiceId) =>
        Db.Execute(connection, transaction, "DELETE FROM [Invoice] WHERE [InvoiceId] = ?", Db.Param("@InvoiceId", invoiceId));

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
        AcademicYear = record.GetInt("AcademicYear"),
        RoomRent = record.GetMoney("RoomRent"),
        TransportAmount = record.GetMoney("TransportAmount"),
        Remarks = record.GetText("Remarks"),
        StudentName = record.GetText("StudentName"),
        PaidAmount = record.GetMoney("PaidAmount"),
    };
}
