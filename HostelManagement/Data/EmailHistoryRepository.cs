using System.Data;
using HostelManagement.Models;

namespace HostelManagement.Data;

public static class EmailHistoryRepository
{
    /// <summary>The most recent emails (all hostels), newest first.</summary>
    public static List<EmailHistoryEntry> GetRecent(int maxRows) =>
        Db.Query(
            $"SELECT TOP {maxRows} h.*, s.[StudentName], i.[InvoiceNumber] " +
            "FROM ([EmailHistory] AS h INNER JOIN [Student] AS s ON h.[StudentId] = s.[StudentId]) " +
            "LEFT JOIN [Invoice] AS i ON h.[InvoiceId] = i.[InvoiceId] " +
            "ORDER BY h.[SentDate] DESC, h.[EmailHistoryId] DESC",
            Map);

    /// <summary>When each student of the hostel was last sent a reminder successfully.</summary>
    public static Dictionary<int, DateTime> GetLastReminderDates(int hostelId) =>
        Db.Query(
                "SELECT h.[StudentId], MAX(h.[SentDate]) AS [LastSent] " +
                "FROM ([EmailHistory] AS h INNER JOIN [Student] AS s ON h.[StudentId] = s.[StudentId]) " +
                "INNER JOIN [College] AS c ON s.[CollegeId] = c.[CollegeId] " +
                "WHERE c.[HostelId] = ? AND h.[EmailType] = ? AND h.[Status] = ? GROUP BY h.[StudentId]",
                r => (StudentId: r.GetInt("StudentId"), LastSent: r.GetDate("LastSent")),
                Db.Param("@HostelId", hostelId),
                Db.Param("@EmailType", EmailType.DueReminder),
                Db.Param("@Status", EmailStatus.Sent))
            .ToDictionary(p => p.StudentId, p => p.LastSent);

    public static void Insert(EmailHistoryEntry entry) =>
        Db.Execute(
            "INSERT INTO [EmailHistory] ([StudentId], [InvoiceId], [RecipientEmail], [EmailType], [Subject], [SentDate], " +
            "[Status], [ErrorMessage]) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
            Db.Param("@StudentId", entry.StudentId),
            Db.Param("@InvoiceId", entry.InvoiceId),
            Db.Param("@RecipientEmail", entry.RecipientEmail),
            Db.Param("@EmailType", entry.EmailType),
            Db.OptionalText("@Subject", entry.Subject.Length > 255 ? entry.Subject[..255] : entry.Subject),
            Db.Param("@SentDate", entry.SentDate),
            Db.Param("@Status", entry.Status),
            Db.OptionalText("@ErrorMessage", entry.ErrorMessage.Length > 255 ? entry.ErrorMessage[..255] : entry.ErrorMessage));

    private static EmailHistoryEntry Map(IDataRecord record) => new()
    {
        EmailHistoryId = record.GetInt("EmailHistoryId"),
        StudentId = record.GetInt("StudentId"),
        InvoiceId = record["InvoiceId"] is DBNull ? null : record.GetInt("InvoiceId"),
        RecipientEmail = record.GetText("RecipientEmail"),
        EmailType = record.GetText("EmailType"),
        Subject = record.GetText("Subject"),
        SentDate = record.GetDate("SentDate"),
        Status = record.GetText("Status"),
        ErrorMessage = record.GetText("ErrorMessage"),
        StudentName = record.GetText("StudentName"),
        InvoiceNumber = record.GetText("InvoiceNumber"),
    };
}
